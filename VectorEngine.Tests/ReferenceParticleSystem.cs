using System;
using Microsoft.Xna.Framework;

namespace VectorEngine.Tests;

/// <summary>
/// Vector Rumble's original <c>ParticleSystem</c> (the engine's copy was removed in 0.1.0-preview.4), kept here with its drawing taken out as the
/// reference <c>ParticleEffects.Burst</c> is tested against. Same arithmetic, same order of random draws.
/// </summary>
sealed class ReferenceParticleSystem
{
    const float AlphaReductionPerSecond = 45f;
    const float VelocityPercentReductionPerSecond = 0.98f;

    public struct Particle
    {
        public Vector2 Position;
        public Vector2 Velocity;
        public Color Color;
    }

    public Particle[] Particles { get; }
    float lifeRemaining;

    public bool IsActive => lifeRemaining > 0f;

    public ReferenceParticleSystem(Random random, Vector2 position, Vector2 direction, int count,
        float minVelocity, float maxVelocity, float life, params Color[] colors)
    {
        Particles = new Particle[Math.Max(0, count)];
        for (int i = 0; i < Particles.Length; i++)
        {
            Particles[i].Position = position;
            Particles[i].Velocity = new Vector2(
                1.0f - 2.0f * (float)random.NextDouble(),
                1.0f - 2.0f * (float)random.NextDouble());
            Particles[i].Velocity.Normalize();
            Particles[i].Velocity *= minVelocity + maxVelocity * (float)random.NextDouble();
            Particles[i].Velocity += direction;
            Particles[i].Color = colors[random.Next(colors.Length)];
        }
        lifeRemaining = life;
    }

    public void Update(float elapsedTime)
    {
        if (lifeRemaining > 0f)
        {
            for (int i = 0; i < Particles.Length; i++)
            {
                Particles[i].Position += Particles[i].Velocity * elapsedTime;
                Particles[i].Color = new Color(
                    Particles[i].Color.R,
                    Particles[i].Color.G,
                    Particles[i].Color.B,
                    (byte)((float)Particles[i].Color.A - AlphaReductionPerSecond * elapsedTime));
                Particles[i].Velocity -= Particles[i].Velocity * (VelocityPercentReductionPerSecond * elapsedTime);
            }
            lifeRemaining -= elapsedTime;
        }
    }
}
