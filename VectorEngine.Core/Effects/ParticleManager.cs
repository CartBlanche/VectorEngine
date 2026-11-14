using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using VectorEngine.Diagnostics;
using VectorEngine.Rendering;

namespace VectorEngine.Effects;

/// <summary>
/// A system for maintaining and rendering particles in this game: a <see cref="ParticleField"/> that also draws itself.
/// </summary>
public class ParticleManager : ParticleField
{
    private List<Particle> emptyPolygonParticles;
    private List<Particle> filledPolygonParticles;
    private List<Particle> lineParticles;
    private List<Particle> textureParticles;

    private SpriteBatch spriteBatch;
    private LineBatch lineBatch;
    private Texture2D texture;
    private readonly IRenderContext context;
    private Vector2 textureOrigin;

    /// <inheritdoc />
    public override Vector2 Area { get => context.BaseScreenSize; set { } }

    /// The matrix particles are drawn with. Null (the default) is the screen's global transformation; a screen that moves a camera sets its own
    /// so effects stay where the action is.
    /// </summary>
    public Matrix? Transform { get; set; }


    public Texture2D Texture
    {
        get => texture;
        set => texture = value;
    }

    /// <param name="context">The screen being drawn to: its virtual size and window transform.</param>
    /// <param name="spriteBatch">Draws the texture particles.</param>
    /// <param name="lineBatch">Draws the line and polygon particles.</param>
    public ParticleManager(IRenderContext context, SpriteBatch spriteBatch, LineBatch lineBatch)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(spriteBatch);
        ArgumentNullException.ThrowIfNull(lineBatch);
        this.context = context;
        this.spriteBatch = spriteBatch;
        this.lineBatch = lineBatch;

        this.emptyPolygonParticles = new List<Particle>();
        this.filledPolygonParticles = new List<Particle>();
        this.lineParticles = new List<Particle>();
        this.textureParticles = new List<Particle>();
    }

    /// <param name="texture">The picture texture particles use (drawn centred on each particle).</param>
    /// <param name="position">Where <see cref="Emit"/> places the effects.</param>
    public ParticleManager(IRenderContext context, SpriteBatch spriteBatch, LineBatch lineBatch, Texture2D texture, Vector2 position)
        : this(context, spriteBatch, lineBatch)
    {
        Position = position;
        this.texture = texture;
        this.textureOrigin = new Vector2(texture.Width / 2, texture.Height / 2);
    }

    /// <summary>
    /// Update the particle manager.
    /// </summary>
    /// <param name="gameTime">The amount of elapsed time, in seconds.</param>
    public void Update(GameTime gameTime)
    {
        var probe = AllocProbe.Begin();
        Update((float)gameTime.ElapsedGameTime.TotalSeconds);
        AllocProbe.End(typeof(ParticleManager), "Update", probe);
    }

    /// <summary>
    /// Controls the "density" of the tail
    /// Dense Tail (t += 1f): A continuous, almost solid-looking trail.Ideal for effects like glowing streaks.
	/// Sparse Tail (t += 10f): A dotted, fragmented appearance.Useful for effects like spark trails or light debris.
    /// </summary>
    const float tailDensity = 5f;

    public void Draw(GameTime gameTime)
    {
        var probe = AllocProbe.Begin();
        DrawParticles(gameTime);
        AllocProbe.End(typeof(ParticleManager), "Draw", probe);
    }

    private void DrawParticles(GameTime gameTime)
    {
        // Clear down our lists
        this.emptyPolygonParticles.Clear();
        this.filledPolygonParticles.Clear();
        this.lineParticles.Clear();
        this.textureParticles.Clear();

        // Group particles by their type to avoid context twitching
        foreach (var particle in particles)
        {
            if (!particle.IsAlive)
                continue;

            switch (particle.Type)
            {
                case ParticleRenderType.EmptyPolygon:
                    emptyPolygonParticles.Add(particle);
                    break;

                case ParticleRenderType.FilledPolygon:
                    filledPolygonParticles.Add(particle);
                    break;

                case ParticleRenderType.LineList:
                    lineParticles.Add(particle);
                    break;

                case ParticleRenderType.Texture:
                    textureParticles.Add(particle);
                    break;
            }
        }

        // Unified geometry pass: filled polygons first (triangles), then lines & empty polygons (lines) via End ordering.
        bool hasGeometry = filledPolygonParticles.Count > 0 || lineParticles.Count > 0 || emptyPolygonParticles.Count > 0;
        if (hasGeometry)
        {
            lineBatch.Begin(Transform ?? context.GlobalTransformation);

            // 1. Filled polygons (enqueued into triangle buffer)
            // BasePolygon keeps its raw points; Transform rewrites only the transformed copy each frame, so nothing accumulates.
            // Per-particle scaling with easing.
            if (filledPolygonParticles.Count > 0)
            {
                foreach (var particle in filledPolygonParticles)
                {
                    if (particle.BasePolygon != null && particle.LifeTime > 0f && particle.OriginalLifeTime > 0f)
                    {
                        float rawProgress = 1f - MathHelper.Clamp(particle.LifeTime / particle.OriginalLifeTime, 0f, 1f); // 0 -> 1

                        // Resolve start/end scale (fallback to legacy constants if unset)
                        float startScale = float.IsNaN(particle.StartScale) ? 0.05f : particle.StartScale;
                        float endScale = float.IsNaN(particle.EndScale) ? 0.25f : particle.EndScale;

                        // Easing
                        float easedProgress = rawProgress;
                        switch (particle.ScaleEasing)
                        {
                            case ScaleEasingMode.EaseInQuad:
                                easedProgress = rawProgress * rawProgress;
                                break;
                            case ScaleEasingMode.EaseOutQuad:
                                easedProgress = 1f - (1f - rawProgress) * (1f - rawProgress); // ease-out quad
                                break;
                            case ScaleEasingMode.Linear:
                            default:
                                easedProgress = rawProgress;
                                break;
                        }

                        float scale = MathHelper.Lerp(startScale, endScale, easedProgress);

                        // Optional alpha driven by scale growth (Ripple style)
                        if (particle.FadeWithScale)
                        {
                            float alphaFrac = 1f - easedProgress; // fade out as scale grows
                            particle.Color.A = (byte)(particle.PeakAlpha * MathHelper.Clamp(alphaFrac, 0f, 1f));
                        }

                        // Transform only rewrites the transformed points from the raw ones, so there is no cumulative mutation and no need to clone
                        var working = particle.BasePolygon;
                        Matrix world = Matrix.CreateScale(scale) * Matrix.CreateTranslation(particle.Position.X, particle.Position.Y, 0f);
                        working.Transform(world);
                        lineBatch.DrawFilledPolygon(working, particle.Color);
                    }
                }
            }

            // 2. Line trails (line list)
            if (lineParticles.Count > 0)
            {
                foreach (var particle in lineParticles)
                {
                    int thickness = particle.LineWidth > 0 ? particle.LineWidth : (int)(particle.MinScale * 5);
                    if (thickness < 1) thickness = 1;
                    var tail = particle.PreviousPosition;
                    if (!float.IsNaN(particle.VelocityTail))
                        tail = particle.Position - particle.CurrentVelocity * particle.VelocityTail;
                    else if (!float.IsNaN(particle.StreakLength) && particle.CurrentVelocity.LengthSquared() > 1e-4f)
                        tail = particle.Position - Vector2.Normalize(particle.CurrentVelocity) * particle.StreakLength;
                    lineBatch.DrawLineList(
                        particle.Position,
                        tail,
                        particle.Color,
                        thickness
                    );
                }
            }

            // 3. Empty polygon outlines (line list)
            // Same scaling approach as filled polygons so outlines match fill growth visually.
            if (emptyPolygonParticles.Count > 0)
            {
                foreach (var particle in emptyPolygonParticles)
                {
                    if (particle.BasePolygon != null && particle.LifeTime > 0f && particle.OriginalLifeTime > 0f)
                    {
                        float rawProgress = 1f - MathHelper.Clamp(particle.LifeTime / particle.OriginalLifeTime, 0f, 1f);
                        float startScale = float.IsNaN(particle.StartScale) ? 0.05f : particle.StartScale;
                        float endScale = float.IsNaN(particle.EndScale) ? 0.25f : particle.EndScale;

                        float easedProgress = rawProgress;
                        switch (particle.ScaleEasing)
                        {
                            case ScaleEasingMode.EaseInQuad:
                                easedProgress = rawProgress * rawProgress;
                                break;
                            case ScaleEasingMode.EaseOutQuad:
                                easedProgress = 1f - (1f - rawProgress) * (1f - rawProgress);
                                break;
                            case ScaleEasingMode.Linear:
                            default:
                                easedProgress = rawProgress;
                                break;
                        }

                        float scale = MathHelper.Lerp(startScale, endScale, easedProgress);

                        if (particle.FadeWithScale)
                        {
                            float alphaFrac = 1f - easedProgress;
                            particle.Color.A = (byte)(255f * MathHelper.Clamp(alphaFrac, 0f, 1f));
                        }

                        var working = particle.BasePolygon;
                        Matrix world = Matrix.CreateScale(scale) * Matrix.CreateTranslation(particle.Position.X, particle.Position.Y, 0f);
                        working.Transform(world);
                        lineBatch.DrawEmptyPolygon(working, particle.Color);
                    }
                }
            }

            lineBatch.End();
        }

        // Draw texture particles if you have a SpriteBatch
        if (textureParticles.Count > 0 && texture != null)
        {
            spriteBatch.Begin(SpriteSortMode.Deferred, null, null, null, null, null, Transform ?? context.GlobalTransformation);

            foreach (var particle in textureParticles)
            {
                // Calculate the direction and length of the tail
                Vector2 tailDirection = particle.Position - particle.PreviousPosition;
                float tailLength = particle.TailLength * tailDirection.Length();

                // Normalize the tail direction
                if (tailDirection != Vector2.Zero)
                    tailDirection.Normalize();

                // Draw the this particle's texture
                spriteBatch.Draw(
                    texture,
                    particle.Position,
                    null,
                    particle.Color,
                    0.0f,
                    textureOrigin,
                    particle.MinScale,
                    SpriteEffects.None,
                    0f);

                // Draw the particle's tail
                for (float t = 0; t < tailLength; t += tailDensity)
                {
                    Vector2 tailPosition = particle.Position - tailDirection * t;

                    // Fade out the tail
                    float alpha = MathHelper.Clamp(1f - (t / tailLength), 0f, 1f);
                    Color tailColor = particle.Color * alpha;

                    spriteBatch.Draw(
                        texture,
                        tailPosition,
                        null,
                        tailColor,
                        0f,
                        textureOrigin,
                        particle.MinScale * 0.8f, // Shrink the tail particle slightly
                        SpriteEffects.None,
                        0f);
                }
            }

            spriteBatch.End();
        }
    }
}