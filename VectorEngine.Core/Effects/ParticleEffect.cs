using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using VectorEngine.Rendering;

namespace VectorEngine.Effects;

/// <summary>Which way a burst's particles head, before the speed is applied.</summary>
public enum ParticleDirection
{
    /// <summary>Any direction, evenly (a random angle).</summary>
    Circle,
    /// <summary>A random point in a square, not normalised: the speed is a maximum and the corners are faster than the middle of an edge.</summary>
    Box,
    /// <summary>Like <see cref="Box"/> but only downward (y from 0 down to 1): confetti falling from a point.</summary>
    BoxDown,
    /// <summary>A random point in a square, then normalised, so every particle has the speed asked for. This is how Vector Rumble's explosions work.</summary>
    BoxNormalised,
}

/// <summary>How a particle's speed is chosen from <see cref="ParticleEffect.SpeedMin"/> and <see cref="ParticleEffect.SpeedMax"/>.</summary>
public enum ParticleSpeed
{
    /// <summary>Always <see cref="ParticleEffect.SpeedMin"/>, no randomness.</summary>
    Fixed,
    /// <summary>A random value between the two.</summary>
    Between,
    /// <summary>
    /// <c>min + max * random</c>, so the range really runs from min to min + max (Vector Rumble's explosions: "256, 128" means 256 to 384). Kept so the old bursts
    /// can be reproduced; prefer <see cref="Between"/> for new effects.
    /// </summary>
    MinPlusMaxTimesRandom,
}

/// <summary>How a particle's colour is chosen.</summary>
public enum ParticleColorMode
{
    /// <summary>The first colour, no randomness.</summary>
    Fixed,
    /// <summary>One of <see cref="ParticleEffect.Colors"/>, picked at random.</summary>
    Palette,
    /// <summary>Any colour at all.</summary>
    RandomRgb,
    /// <summary>A random red/orange/yellow (the engine's explosions).</summary>
    RandomWarm,
    /// <summary>White at a random opacity between half and full (sparkles).</summary>
    FadedWhite,
}

/// <summary>How a particle goes transparent.</summary>
public enum ParticleFade
{
    /// <summary>Gradually, so it is gone as its life ends.</summary>
    ByLifetime,
    /// <summary>Not at all: it keeps its opacity until it dies.</summary>
    None,
    /// <summary>A fixed amount per second (<see cref="ParticleEffect.AlphaFadePerSecond"/>), however long it lives.</summary>
    PerSecond,
}

/// <summary>
/// A burst of particles described as data: how many, how fast, where they head, how long they live, what colour and shape they are and how they
/// fade. <see cref="ParticleField.Emit(ParticleEffect, Vector2, Vector2?, Color[])"/> plays one at a point. The built-in effects are in
/// <see cref="ParticleEffects"/>; a game writes its own the same way, or registers it by name with <see cref="ParticleField.Register"/>.
/// <para>
/// The order the random numbers are drawn in (direction, speed, life, colour, scale, tail) is fixed, so a seeded <see cref="Random"/> repeats a burst exactly.
/// A range whose two ends are equal draws nothing, which is what lets a preset reproduce the old effects number for number.
/// </para>
/// </summary>
public sealed class ParticleEffect
{
    /// <summary>How many particles a burst makes (a caller may ask for another number).</summary>
    public int Count { get; init; } = 16;

    public ParticleDirection Direction { get; init; } = ParticleDirection.Circle;
    public ParticleSpeed SpeedMode { get; init; } = ParticleSpeed.Between;
    /// <summary>Pixels per second. For <see cref="ParticleDirection.Box"/> and <see cref="ParticleDirection.BoxDown"/> this is the multiplier.</summary>
    public float SpeedMin { get; init; } = 50f;
    public float SpeedMax { get; init; } = 100f;

    /// <summary>Seconds a particle lives (a random value between the two; the same number gives every particle the same life).</summary>
    public float LifeMin { get; init; } = 1f;
    public float LifeMax { get; init; } = 1f;

    public ParticleColorMode ColorMode { get; init; } = ParticleColorMode.Fixed;
    public Color[] Colors { get; init; } = new[] { Color.White };

    public ParticleRenderType Render { get; init; } = ParticleRenderType.LineList;
    /// <summary>The shape of <see cref="ParticleRenderType.EmptyPolygon"/> and <see cref="ParticleRenderType.FilledPolygon"/> particles.</summary>
    public VectorPolygon Polygon { get; init; }

    /// <summary>Scale of a particle: a random value between the two (a texture's size multiplier, or a line's width / 5).</summary>
    public float ScaleMin { get; init; } = 1f;
    public float ScaleMax { get; init; } = 1f;
    /// <summary>For polygon particles that grow or shrink over their life. NaN (the default) leaves scale alone.</summary>
    public float StartScale { get; init; } = float.NaN;
    public float EndScale { get; init; } = float.NaN;
    public ScaleEasingMode ScaleEasing { get; init; } = ScaleEasingMode.Linear;
    /// <summary>Fade by how far the scale has grown (a ripple) instead of by age.</summary>
    public bool FadeWithScale { get; init; }
    public byte PeakAlpha { get; init; } = 255;

    public ParticleFade Fade { get; init; } = ParticleFade.ByLifetime;
    /// <summary>For <see cref="ParticleFade.PerSecond"/>: alpha (0 to 255) lost per second.</summary>
    public float AlphaFadePerSecond { get; init; }

    /// <summary>Drag per second; null keeps the engine's default (0.9). 0 is a steady glide, 3 stops flying chips quickly.</summary>
    public float? Drag { get; init; }
    /// <summary>Acceleration in pixels per second squared.</summary>
    public Vector2 Gravity { get; init; }

    /// <summary>A line particle's tail is its velocity times this, so it shortens as the particle slows. NaN leaves the engine's default.</summary>
    public float VelocityTail { get; init; } = float.NaN;
    /// <summary>A line particle's tail as a fixed length in pixels. NaN leaves the engine's default.</summary>
    public float StreakLength { get; init; } = float.NaN;
    /// <summary>A line's width in pixels; 0 derives it from the scale.</summary>
    public int LineWidth { get; init; }

    /// <summary>
    /// The stored tail length of a texture particle: <see cref="TailLength"/>, plus a random whole number of <see cref="TailLengthStep"/>s when
    /// <see cref="TailLengthVariants"/> is above zero (the engine's explosions use 0.5 + 0 to 9 halves).
    /// </summary>
    public float TailLength { get; init; }
    public float TailLengthStep { get; init; }
    public int TailLengthVariants { get; init; }

    /// <summary>Played at the spot where each particle dies (fireworks: a particle that bursts into more particles). Keep it from playing itself.</summary>
    public ParticleEffect OnDeath { get; init; }

    /// <summary>
    /// Makes the particles of one burst and adds them to <paramref name="into"/>.
    /// </summary>
    /// <param name="random">The randomness to draw from.</param>
    /// <param name="at">Where the burst starts.</param>
    /// <param name="into">The list the particles are added to.</param>
    /// <param name="inherit">Added to every particle's velocity (a burst from something that is moving).</param>
    /// <param name="count">Overrides <see cref="Count"/>.</param>
    /// <param name="colors">Overrides <see cref="Colors"/>: a palette (or a single colour that every particle gets, with no randomness) in place of the preset's.</param>
    public void Spawn(Random random, Vector2 at, List<Particle> into, Vector2 inherit = default, int? count = null, Color[] colors = null)
    {
        ArgumentNullException.ThrowIfNull(random);
        ArgumentNullException.ThrowIfNull(into);
        int n = Math.Max(0, count ?? Count);

        var mode = ColorMode;
        var palette = Colors;
        if (colors != null && colors.Length > 0)
        {
            palette = colors;
            mode = colors.Length == 1 ? ParticleColorMode.Fixed : ParticleColorMode.Palette;
        }
        if (palette == null || palette.Length == 0)
            palette = new[] { Color.White };

        for (int i = 0; i < n; i++)
        {
            Vector2 velocity = Heading(random) * Speed(random) + inherit;
            float life = Between(random, LifeMin, LifeMax);
            Color color = PickColor(random, mode, palette);
            float scale = Between(random, ScaleMin, ScaleMax);
            float tail = TailLength;
            if (TailLengthVariants > 0)
                tail += random.Next(TailLengthVariants) * TailLengthStep;

            var particle = new Particle(at, velocity, life, color, scale, Render, tail, Polygon)
            {
                Gravity = Gravity,
                Drag = Drag,
                StreakLength = StreakLength,
                LineWidth = LineWidth,
                StartScale = StartScale,
                EndScale = EndScale,
                ScaleEasing = ScaleEasing,
                FadeWithScale = FadeWithScale,
                PeakAlpha = PeakAlpha,
                VelocityTail = VelocityTail,
                FadesOut = Fade != ParticleFade.None,
            };
            if (Fade == ParticleFade.PerSecond)
                particle.AlphaFadePerSecond = AlphaFadePerSecond;

            if (OnDeath != null)
            {
                var next = OnDeath;
                particle.OnDeath += where => next.Spawn(random, where, into);
            }
            into.Add(particle);
        }
    }

    // the unit vector (or square point) a particle heads along, before speed
    Vector2 Heading(Random random)
    {
        switch (Direction)
        {
            case ParticleDirection.Box:
                return new Vector2((float)(random.NextDouble() * 2 - 1), (float)(random.NextDouble() * 2 - 1));
            case ParticleDirection.BoxDown:
                return new Vector2((float)(random.NextDouble() * 2 - 1), (float)random.NextDouble());
            case ParticleDirection.BoxNormalised:
            {
                var v = new Vector2(1.0f - 2.0f * (float)random.NextDouble(), 1.0f - 2.0f * (float)random.NextDouble());
                v.Normalize();
                return v;
            }
            default:
            {
                float angle = (float)(random.NextDouble() * Math.PI * 2);
                return new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle));
            }
        }
    }

    float Speed(Random random)
    {
        switch (SpeedMode)
        {
            case ParticleSpeed.Fixed:
                return SpeedMin;
            case ParticleSpeed.MinPlusMaxTimesRandom:
                return SpeedMin + SpeedMax * (float)random.NextDouble();
            default:
                return Between(random, SpeedMin, SpeedMax);
        }
    }

    // a random value between two numbers; equal ends cost no randomness
    static float Between(Random random, float min, float max)
        => min == max ? min : min + (float)random.NextDouble() * (max - min);

    static Color PickColor(Random random, ParticleColorMode mode, Color[] palette)
    {
        switch (mode)
        {
            case ParticleColorMode.Palette:
                return palette[random.Next(palette.Length)];
            case ParticleColorMode.RandomRgb:
                return new Color(random.Next(256), random.Next(256), random.Next(256));
            case ParticleColorMode.RandomWarm:
                return new Color(random.Next(200, 256), random.Next(100, 200), random.Next(0, 100));
            case ParticleColorMode.FadedWhite:
                return Color.White * ((float)random.NextDouble() * 0.5f + 0.5f);
            default:
                return palette[0];
        }
    }
}
