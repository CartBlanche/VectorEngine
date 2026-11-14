using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using VectorEngine.Rendering;

namespace VectorEngine.Effects;

/// <summary>
/// The particles of an effects layer and everything that creates and moves them, with no drawing and no graphics device, so it runs (and is
/// tested) headless. <see cref="ParticleManager"/> adds the drawing. Effects come from the built-in <c>Emit...</c> methods or from a
/// <see cref="ParticleEffect"/> description played with <see cref="Emit(ParticleEffect, Vector2, Vector2?, Color[])"/>.
/// </summary>
public class ParticleField
{
    /// <summary>The particles in flight. Anything that draws them reads this.</summary>
    protected readonly List<Particle> particles = new List<Particle>();

    /// <summary>The randomness effects use. Visual only: games and tests may replace it (a seeded one gives the same burst every time).</summary>
    public Random Random { get => random; set => random = value ?? throw new ArgumentNullException(nameof(value)); }
    private Random random = new Random();

    private Vector2 position;

    /// <summary>The size of the area an effect may use (rain falls across all of it). The manager keeps this at the screen's virtual size.</summary>
    public virtual Vector2 Area { get; set; } = new Vector2(1280, 720);

    public Vector2 Position
    {
        get => position;
        set => position = value;
    }

    /// <summary>
    /// Emit small sparks: <paramref name="count"/> particles at <paramref name="at"/> flying off in random directions at up to <paramref name="speed"/>
    /// pixels per second, each living between the two lifetimes (seconds). A general-purpose spark burst (a taser, a short circuit, a hit).
    /// </summary>
    public void EmitSparks(Vector2 at, int count, Color color, float speed, float minLifetime, float maxLifetime, float scale)
    {
        for (int i = 0; i < count; i++)
        {
            var velocity = new Vector2((float)(random.NextDouble() * 2 - 1), (float)(random.NextDouble() * 2 - 1)) * speed;
            float lifetime = minLifetime + (float)random.NextDouble() * (maxLifetime - minLifetime);
            particles.Add(new Particle(at, velocity, lifetime, color * ((float)random.NextDouble() * 0.5f + 0.5f), scale));
        }
    }

    /// <summary>
    /// A ring that grows from nothing to <paramref name="radius"/> world pixels and fades: where a drop lands. The same shape as the
    /// main menu's ripple, but any size, so it can follow the camera zoom.
    /// </summary>
    public void EmitRipple(Vector2 at, float radius, Color color, float life = 1.2f)
    {
        var ripple = new Particle(at, Vector2.Zero, life, color, 1f, ParticleRenderType.EmptyPolygon, 0f, dropRipple)
        {
            StartScale = 0.05f,
            EndScale = radius / 10f,   // dropRipple has a radius of 10
            ScaleEasing = ScaleEasingMode.EaseOutQuad,
            FadeWithScale = true,
        };
        particles.Add(ripple);
    }

    /// <summary>
    /// A bubble breaking the surface of lava: a ring that swells from a third of <paramref name="radius"/> to all of it in <paramref name="life"/>
    /// seconds (slowly at first and faster as it stretches, like a bubble in thick lava), then pops in a few sparks (<paramref name="pop"/>) and
    /// calls <paramref name="onPop"/>. All sizes are in world pixels.
    /// </summary>
    public void EmitBubble(Vector2 at, float radius, Color color, float life, Color pop, Action onPop = null)
    {
        var bubble = new Particle(at, Vector2.Zero, life, color, 1f, ParticleRenderType.EmptyPolygon, 0f, dropRipple)
        {
            StartScale = radius / 10f * 0.33f,
            EndScale = radius / 10f,
            ScaleEasing = ScaleEasingMode.EaseInQuad,
            FadeWithScale = false,
        };
        bubble.OnDeath += _ =>
        {
            EmitSparks(at, 7, pop, radius * 11f, 0.18f, 0.4f, 0.22f);
            onPop?.Invoke();
        };
        particles.Add(bubble);
    }

    /// <summary>
    /// A drop falling from <paramref name="from"/> to <paramref name="to"/> in <paramref name="fallSeconds"/>: a short streak that speeds up
    /// under gravity, then a ripple of <paramref name="rippleRadius"/> where it lands. All sizes are in world pixels.
    /// </summary>
    public void EmitDrop(Vector2 from, Vector2 to, Color color, float fallSeconds, float streakLength, int width, float rippleRadius)
    {
        var fall = to - from;
        var start = fall / fallSeconds * 0.3f;   // a drop is not at rest when it lets go
        var gravity = 2f * (fall - start * fallSeconds) / (fallSeconds * fallSeconds);
        var drop = new Particle(from, start, fallSeconds, color, 1f, ParticleRenderType.LineList)
        {
            Gravity = gravity,
            Drag = 0f,
            StreakLength = streakLength,
            LineWidth = width,
            FadesOut = false,
        };
        drop.OnDeath += _ => EmitRipple(to, rippleRadius, color);
        particles.Add(drop);
    }

    /// <summary>
    /// Fixed-length streaks flying out from <paramref name="at"/> in every direction (chips off a rock face), slowing at <paramref name="drag"/> per second.
    /// All sizes are in world pixels.
    /// </summary>
    public void EmitStreaks(Vector2 at, int count, Color color, float speed, float minLifetime, float maxLifetime, float length, int width, float drag)
    {
        for (int i = 0; i < count; i++)
        {
            float angle = (float)(random.NextDouble() * MathHelper.TwoPi);
            var velocity = new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle)) * speed * (0.5f + (float)random.NextDouble() * 0.5f);
            float lifetime = minLifetime + (float)random.NextDouble() * (maxLifetime - minLifetime);
            particles.Add(new Particle(at, velocity, lifetime, color, 1f, ParticleRenderType.LineList)
            {
                StreakLength = length * (0.6f + (float)random.NextDouble() * 0.4f),
                LineWidth = width,
                Drag = drag,
            });
        }
    }

    /// <summary>
    /// Soft puffs of steam: filled discs that drift slowly, grow from about <paramref name="size"/> to four times that and fade as they grow
    /// (seen from above, steam rises toward the camera, so it swells rather than climbs). All sizes are in world pixels.
    /// </summary>
    public void EmitPuffs(Vector2 at, int count, Color color, float speed, float minLifetime, float maxLifetime, float size)
    {
        var points = new Vector2[10];
        for (int k = 0; k < points.Length; k++)
        {
            float angle = k * MathHelper.TwoPi / points.Length;
            points[k] = new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle)) * 10f;
        }
        var disc = new VectorPolygon(points);

        for (int i = 0; i < count; i++)
        {
            float heading = (float)(random.NextDouble() * MathHelper.TwoPi);
            var velocity = new Vector2((float)Math.Cos(heading), (float)Math.Sin(heading)) * speed * (float)random.NextDouble();
            float lifetime = minLifetime + (float)random.NextDouble() * (maxLifetime - minLifetime);
            float grow = 0.8f + (float)random.NextDouble() * 0.4f;
            particles.Add(new Particle(at, velocity, lifetime, color, 1f, ParticleRenderType.FilledPolygon, 0f, disc)
            {
                StartScale = size / 10f,
                EndScale = size / 10f * 4f * grow,
                ScaleEasing = ScaleEasingMode.EaseOutQuad,
                FadeWithScale = true,
                PeakAlpha = 70,
                Drag = 1.5f,
            });
        }
    }

    /// <summary>
    /// Small rock-shaped pieces breaking away from <paramref name="at"/>: filled polygons of about <paramref name="size"/> world pixels, each a different
    /// irregular shape, that slow and shrink as they go.
    /// </summary>
    public void EmitChunks(Vector2 at, int count, Color color, float speed, float minLifetime, float maxLifetime, float size, float drag)
    {
        for (int i = 0; i < count; i++)
        {
            int sides = 4 + random.Next(2);
            float turn = (float)(random.NextDouble() * MathHelper.TwoPi);
            var points = new Vector2[sides];
            for (int k = 0; k < sides; k++)
            {
                float angle = turn + k * MathHelper.TwoPi / sides + (float)(random.NextDouble() - 0.5) * 0.5f;
                points[k] = new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle)) * 10f * (0.6f + (float)random.NextDouble() * 0.4f);
            }

            float heading = (float)(random.NextDouble() * MathHelper.TwoPi);
            var velocity = new Vector2((float)Math.Cos(heading), (float)Math.Sin(heading)) * speed * (0.5f + (float)random.NextDouble() * 0.5f);
            float lifetime = minLifetime + (float)random.NextDouble() * (maxLifetime - minLifetime);
            particles.Add(new Particle(at, velocity, lifetime, color, 1f, ParticleRenderType.FilledPolygon, 0f, new VectorPolygon(points))
            {
                StartScale = size / 10f,
                EndScale = size / 10f * 0.4f,
                Drag = drag,
            });
        }
    }

    /// <summary>
    /// A firework in two stages: a rocket (a short bright streak) climbs from <paramref name="from"/> to <paramref name="burstAt"/> in
    /// <paramref name="riseSeconds"/>, then bursts into <paramref name="count"/> streaks flying out in a ring (every third one in <paramref name="accent"/>)
    /// that fall under gravity, slow and fade. <paramref name="power"/> is the burst's speed in pixels per second; <paramref name="onBurst"/> is called
    /// with the burst's position (a pop sound). All sizes are in world pixels.
    /// </summary>
    public void EmitFirework(Vector2 from, Vector2 burstAt, Color color, Color accent, float riseSeconds, int count, float power, Action<Vector2> onBurst = null)
    {
        var rocket = new Particle(from, (burstAt - from) / riseSeconds, riseSeconds, color * 0.9f, 1f, ParticleRenderType.LineList)
        {
            Drag = 0f,   // a steady climb, so it reaches the burst point as its life ends
            StreakLength = 16f,
            LineWidth = 2,
            FadesOut = false,
        };
        rocket.OnDeath += at =>
        {
            EmitFireworkBurst(at, count, color, accent, power);
            onBurst?.Invoke(at);
        };
        particles.Add(rocket);
    }

    private void EmitFireworkBurst(Vector2 at, int count, Color color, Color accent, float power)
    {
        for (int i = 0; i < count; i++)
        {
            float angle = MathHelper.TwoPi * i / count + (float)random.NextDouble() * 0.2f;
            float speed = power * (0.55f + 0.45f * (float)random.NextDouble());
            particles.Add(new Particle(at, new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle)) * speed, 1.1f + 0.8f * (float)random.NextDouble(),
                i % 3 == 0 ? accent : color, 1f, ParticleRenderType.LineList)
            {
                Gravity = new Vector2(0f, 130f),
                StreakLength = 7f,
                LineWidth = 2,
            });
        }
    }

    public int ParticleCount
    {
        get
        {
            if (particles != null)
            {
                return particles.Count;
            }
            else
            {
                return 0;
            }
        }
    }

    /// <summary>
    /// Plays one of the built-in effects at <see cref="Position"/> with <paramref name="numberOfParticles"/> particles, in <paramref name="color"/> if given
    /// (otherwise the effect's own random colours). All but rain are <see cref="ParticleEffects"/> presets.
    /// </summary>
    public void Emit(int numberOfParticles, ParticleEffectType effectType, Color? color = null)
    {
        if (effectType == ParticleEffectType.Rain)
        {
            EmitRainDrops(numberOfParticles, position, color);
            return;
        }

        var preset = ParticleEffects.For(effectType);
        if (preset != null)
            Emit(preset, position, numberOfParticles, color.HasValue ? new[] { color.Value } : null);
    }

    void EmitRainDrops(int numberOfParticles, Vector2 emitPosition, Color? color = null)
    {
        for (int i = 0; i < numberOfParticles; i++)
        {
            // Step 1: Destination Point
            Vector2 destinationPoint = new Vector2(
                (float)(random.NextDouble() * Area.X),
                (float)(random.NextDouble() * Area.Y)
            );

            // Step 2: Calculate Drift Towards Center
            Vector2 driftDirection = Vector2.Normalize(emitPosition - destinationPoint); // Direction toward the center
            float driftMagnitude = 20f;//(float)(random.NextDouble() * 50); // Random magnitude of drift
            Vector2 drift = driftDirection * driftMagnitude; // Apply drift magnitude towards center

            // Step 3: Start Point Calculation
            float startDistance = 250f; // (float)(random.NextDouble() * 300 + 200); // Distance behind player
            float angleAway = (float)Math.Atan2(-driftDirection.Y, -driftDirection.X); // Reverse drift direction
            Vector2 startPosition = new Vector2(
                destinationPoint.X + (float)Math.Cos(angleAway) * startDistance,
                destinationPoint.Y + (float)Math.Sin(angleAway) * startDistance
            );

            // Step 4: Slight Drift Towards Center     
            startPosition += drift;

            // Step 5: Particle Properties
            Vector2 direction = Vector2.Normalize(destinationPoint - startPosition);
            float speed = (float)(random.NextDouble() * 300 + 100);
            float lifetime = 1.3f;//(float)(random.NextDouble() * 1.5f + 0.5f);
            Color particleColor = color ?? new Color(random.Next(200, 256), random.Next(100, 200), random.Next(0, 100));
            float scale = 0.7f;//(float)(random.NextDouble() * 0.5f + 0.2f);

            Color actualParticleColor = color ?? Color.Lerp(Color.Gray, Color.White, 1 - (startDistance / 1024));

            var particle = new Particle(
                startPosition,
                direction * speed,
                lifetime,
                actualParticleColor,
                scale,
                ParticleRenderType.LineList,
                tailLength: 10
            );

            particles.Add(particle);

            // Create a rain drop ripple effect 
            particle.OnDeath += EmitRainDropRipple;
        }

        /*const float maxDistance = 1280f;
        //Vector2 vanishingPoint,
        float targetAreaRadius = 100;
        for (int i = 0; i < numberOfParticles; i++)
        {
            // Randomly generate a start position around the screen
            float angle = (float)(random.NextDouble() * MathHelper.TwoPi);
            float distance = random.Next((int)maxDistance);
            Vector2 startPosition = new Vector2(
                emitPosition.X + (float)Math.Cos(angle) * distance,
                emitPosition.Y + (float)Math.Sin(angle) * distance
            );

            // Calculate a target point within a radius around the vanishing point
            float targetAngle = (float)(random.NextDouble() * MathHelper.TwoPi);
            float targetDistance = (float)random.NextDouble() * targetAreaRadius;
            Vector2 targetPoint = new Vector2(
                emitPosition.X + (float)Math.Cos(targetAngle) * targetDistance,
                emitPosition.Y + (float)Math.Sin(targetAngle) * targetDistance
            );

            // Calculate direction towards the target point
            Vector2 direction = targetPoint - startPosition;
            direction.Normalize();

            float speed = MathHelper.Lerp(200, 800, (float)random.NextDouble());

            // Calculate the velocity to move towards the target point
            Vector2 velocity = direction * speed;

            // Lifetime depends on the distance to the target point
            float lifetime = Vector2.Distance(startPosition, targetPoint) / speed;

            // Scale particles based on distance from start (maintaining original perspective)
            float scale = MathHelper.Lerp(0.2f, 1.0f, 1 - (distance / maxDistance));

            Color actualParticleColor = color ?? Color.Lerp(Color.Gray, Color.White, 1 - (distance / maxDistance));

            var particle = new Particle(
                startPosition,
                velocity,
                lifetime,
                actualParticleColor,
                scale,
                ParticleRenderType.LineList,
                tailLength: scale * 0.5f
            );

            particles.Add(particle);

            // Create a rain drop ripple effect 
            particle.OnDeath += EmitRainDropRipple;
        }*/
    }

    VectorPolygon dropRipple = VectorPolygon.CreateCircle(Vector2.Zero, 10f, 16);

    /* TODO  Not needed for now private void EmitRainDropRipples(int numberOfParticles, Vector2 emitPosition, Color? color = null)
    {
        for (int i = 0; i < numberOfParticles; i++)
        {
            EmitRainDropRipple(new Vector2(random.Next(1280), random.Next(720)));
        }
    }*/

    private void EmitRainDropRipple(Vector2 rainDropPosition)
    {
        var particle = new Particle
                (
                    rainDropPosition,
                    new Vector2(0.0f, 0.0f),
                    2.0f,
                    Color.WhiteSmoke,
                    1.5f,
                    ParticleRenderType.EmptyPolygon,
                    10,
                    dropRipple
                );

        // Configure per-particle scaling & easing for ripple:
        // Start tiny, grow to double previous radius (0.5 vs legacy 0.25) with ease-out and fade tied to scale.
        particle.StartScale = 0.05f;
        particle.EndScale = 0.5f; // doubled final radius
        particle.ScaleEasing = ScaleEasingMode.EaseOutQuad;
        particle.FadeWithScale = true;

        particles.Add(particle);
    }

    readonly Dictionary<string, ParticleEffect> registry = new Dictionary<string, ParticleEffect>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Plays <paramref name="effect"/> at <paramref name="at"/>.
    /// </summary>
    /// <param name="direction">Added to every particle's velocity (a burst from something that is moving).</param>
    /// <param name="colors">Colours for this burst in place of the effect's own (one colour gives every particle that colour).</param>
    public void Emit(ParticleEffect effect, Vector2 at, Vector2? direction = null, params Color[] colors)
    {
        ArgumentNullException.ThrowIfNull(effect);
        effect.Spawn(random, at, particles, direction ?? Vector2.Zero, null, colors);
    }

    /// <summary>Plays <paramref name="effect"/> at <paramref name="at"/> with <paramref name="count"/> particles in place of the effect's own number.</summary>
    public void Emit(ParticleEffect effect, Vector2 at, int count, params Color[] colors)
    {
        ArgumentNullException.ThrowIfNull(effect);
        effect.Spawn(random, at, particles, Vector2.Zero, count, colors);
    }

    /// <summary>Names an effect so game code can play it with <see cref="Emit(string, Vector2, Vector2?, Color[])"/>. Registering a name again replaces the effect.</summary>
    public void Register(string name, ParticleEffect effect)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentNullException.ThrowIfNull(effect);
        registry[name] = effect;
    }

    /// <summary>Plays the effect registered under <paramref name="name"/>. An unknown name throws, so a typo does not silently show nothing.</summary>
    public void Emit(string name, Vector2 at, Vector2? direction = null, params Color[] colors)
    {
        if (!registry.TryGetValue(name ?? "", out var effect))
            throw new KeyNotFoundException($"No particle effect is registered as '{name}'.");
        Emit(effect, at, direction, colors);
    }

    /// <summary>Moves every particle on by <paramref name="elapsed"/> seconds and drops the ones that have died (their <c>OnDeath</c> runs).</summary>
    public virtual void Update(float elapsed)
    {
        for (int i = particles.Count - 1; i >= 0; i--)
        {
            particles[i].Update(elapsed);

            if (!particles[i].IsAlive)
            {
                particles.RemoveAt(i);
            }
        }
    }
}
