using Microsoft.Xna.Framework;
using VectorEngine.Rendering;

namespace VectorEngine.Effects;

/// <summary>
/// The engine's ready-made <see cref="ParticleEffect"/>s, the same bursts <see cref="ParticleField.Emit(int, ParticleEffectType, Color?)"/> plays, plus
/// <see cref="Burst"/>, which reproduces Vector Rumble's classic <see cref="ParticleSystem"/> explosion.
/// </summary>
public static class ParticleEffects
{
    /// <summary>Coloured pieces drifting down from a point (a texture particle, so the manager needs a texture).</summary>
    public static readonly ParticleEffect Confetti = new ParticleEffect
    {
        Count = 30,
        Direction = ParticleDirection.BoxDown,
        SpeedMode = ParticleSpeed.Fixed,
        SpeedMin = 100f,
        LifeMin = 1f,
        LifeMax = 4f,
        ColorMode = ParticleColorMode.RandomRgb,
        Render = ParticleRenderType.Texture,
        ScaleMin = 0.3f,
        ScaleMax = 0.6f,
    };

    /// <summary>A burst of warm line streaks flying out in every direction.</summary>
    public static readonly ParticleEffect Explosions = new ParticleEffect
    {
        Count = 30,
        Direction = ParticleDirection.Circle,
        SpeedMode = ParticleSpeed.Between,
        SpeedMin = 100f,
        SpeedMax = 300f,
        LifeMin = 0.5f,
        LifeMax = 2f,
        ColorMode = ParticleColorMode.RandomWarm,
        Render = ParticleRenderType.LineList,
        ScaleMin = 0.2f,
        ScaleMax = 0.6f,
        TailLength = 0.5f,
        TailLengthStep = 0.5f,
        TailLengthVariants = 10,
    };

    /// <summary>Texture particles flying out that each burst into ten warm streaks where they die.</summary>
    public static readonly ParticleEffect Fireworks = new ParticleEffect
    {
        Count = 30,
        Direction = ParticleDirection.Box,
        SpeedMode = ParticleSpeed.Fixed,
        SpeedMin = 150f,
        LifeMin = 1f,
        LifeMax = 3f,
        ColorMode = ParticleColorMode.RandomRgb,
        Render = ParticleRenderType.Texture,
        ScaleMin = 0.5f,
        ScaleMax = 1f,
        OnDeath = new ParticleEffect
        {
            Count = 10,
            Direction = ParticleDirection.Circle,
            SpeedMode = ParticleSpeed.Between,
            SpeedMin = 100f,
            SpeedMax = 300f,
            LifeMin = 0.5f,
            LifeMax = 2f,
            ColorMode = ParticleColorMode.RandomWarm,
            Render = ParticleRenderType.LineList,
            ScaleMin = 0.2f,
            ScaleMax = 0.6f,
            TailLength = 0.5f,
            TailLengthStep = 0.5f,
            TailLengthVariants = 10,
        },
    };

    /// <summary>Small, slow, faint white texture particles.</summary>
    public static readonly ParticleEffect Sparkles = new ParticleEffect
    {
        Count = 20,
        Direction = ParticleDirection.Box,
        SpeedMode = ParticleSpeed.Fixed,
        SpeedMin = 50f,
        LifeMin = 0.5f,
        LifeMax = 1.5f,
        ColorMode = ParticleColorMode.FadedWhite,
        Render = ParticleRenderType.Texture,
        ScaleMin = 0.2f,
        ScaleMax = 0.4f,
    };

    /// <summary>The preset behind an <see cref="ParticleEffectType"/> that is a plain burst, or null (rain needs the screen's size and has its own code).</summary>
    public static ParticleEffect For(ParticleEffectType type)
    {
        switch (type)
        {
            case ParticleEffectType.Confetti: return Confetti;
            case ParticleEffectType.Explosions: return Explosions;
            case ParticleEffectType.Fireworks: return Fireworks;
            case ParticleEffectType.Sparkles: return Sparkles;
            default: return null;
        }
    }

    /// <summary>
    /// Vector Rumble's classic explosion as data, the same particles as <c>new ParticleSystem(position, direction, count, minVelocity, maxVelocity, life,
    /// tailLength, colors)</c>: every particle shares one lifetime, flies off in a random direction at <c>minVelocity + maxVelocity * random</c> (so the speed
    /// really runs from min to min + max) plus <c>direction</c>, takes a colour from <paramref name="colors"/>, slows by 98% a second and loses 45 alpha a
    /// second, and is drawn as a one pixel line from where it is back along its velocity times <paramref name="tailLength"/>. Play it with
    /// <c>field.Emit(effect, position, direction)</c>.
    /// </summary>
    public static ParticleEffect Burst(int count, float minVelocity, float maxVelocity, float life, float tailLength, params Color[] colors)
        => new ParticleEffect
        {
            Count = count,
            Direction = ParticleDirection.BoxNormalised,
            SpeedMode = ParticleSpeed.MinPlusMaxTimesRandom,
            SpeedMin = minVelocity,
            SpeedMax = maxVelocity,
            LifeMin = life,
            LifeMax = life,
            ColorMode = ParticleColorMode.Palette,
            Colors = colors,
            Render = ParticleRenderType.LineList,
            LineWidth = 1,
            VelocityTail = tailLength,
            Drag = 0.98f,
            Fade = ParticleFade.PerSecond,
            AlphaFadePerSecond = 45f,
        };
}
