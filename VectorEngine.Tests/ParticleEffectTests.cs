using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.Xna.Framework;
using VectorEngine.Effects;
using VectorEngine.Rendering;
using Xunit;

namespace VectorEngine.Tests;

public class ParticleEffectTests
{
    const float Dt = 1f / 60f;

    sealed class TestField : ParticleField
    {
        public IReadOnlyList<Particle> Alive => particles;
    }

    // the old ParticleSystem keeps its particles in a private struct array, so read it by reflection
    static List<(Vector2 pos, Vector2 vel, Color color)> Legacy(ParticleSystem system)
    {
        var array = (Array)typeof(ParticleSystem).GetField("particles", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(system);
        var list = new List<(Vector2, Vector2, Color)>();
        foreach (var item in array)
        {
            var t = item.GetType();
            list.Add(((Vector2)t.GetField("Position").GetValue(item), (Vector2)t.GetField("Velocity").GetValue(item), (Color)t.GetField("Color").GetValue(item)));
        }
        return list;
    }

    static readonly Color[] Palette = { Color.Red, Color.Orange, Color.Yellow, Color.White };

    public static IEnumerable<object[]> LegacyBursts()
    {
        // the calls Vector Rumble and Solid Ops actually make
        yield return new object[] { 128, 64f, 256f, 3f, 0.05f, 0f, 0f };
        yield return new object[] { 64, 256f, 1024f, 3f, 0.05f, 0f, 0f };
        yield return new object[] { 64, 256f, 128f, 1.8f, 0.05f, 0f, 0f };     // the menu's fireworks
        yield return new object[] { 24, 32f, 64f, 2f, 0.1f, 0f, 0f };          // a power-up
        yield return new object[] { 32, 32f, 64f, 2f, 0.1f, 40f, -25f };       // with a moving source
    }

    [Theory]
    [MemberData(nameof(LegacyBursts))]
    public void Burst_preset_reproduces_the_classic_particle_system_frame_for_frame(int count, float minV, float maxV, float life, float tail, float dx, float dy)
    {
        var origin = new Vector2(300, 200);
        var direction = new Vector2(dx, dy);

        ParticleSystem.Random = new Random(1234);
        var legacy = new ParticleSystem(origin, direction, count, minV, maxV, life, tail, Palette);

        var field = new TestField { Random = new Random(1234) };
        field.Emit(ParticleEffects.Burst(count, minV, maxV, life, tail, Palette), origin, direction);

        Assert.Equal(count, field.Alive.Count);
        int frames = 0;
        while (legacy.IsActive)
        {
            var old = Legacy(legacy);
            Assert.Equal(old.Count, field.Alive.Count);
            for (int i = 0; i < old.Count; i++)
            {
                var p = field.Alive[i];
                Assert.Equal(old[i].pos.X, p.Position.X, 0.01f);
                Assert.Equal(old[i].pos.Y, p.Position.Y, 0.01f);
                Assert.Equal(old[i].vel.X, p.CurrentVelocity.X, 0.01f);
                Assert.Equal(old[i].vel.Y, p.CurrentVelocity.Y, 0.01f);
                Assert.Equal(old[i].color, p.Color);                      // same colour AND same alpha, byte for byte
                Assert.Equal(tail, p.VelocityTail);                       // the line is drawn back along velocity * tail
                Assert.Equal(1, p.LineWidth);
            }
            legacy.Update(Dt);
            field.Update(Dt);
            frames++;
            Assert.True(frames < 1000);
        }
        // both are over together: the shared lifetime is each particle's own
        Assert.Empty(field.Alive);
    }

    [Fact]
    public void A_seeded_random_repeats_a_burst_exactly()
    {
        List<Vector2> Run()
        {
            var f = new TestField { Random = new Random(7) };
            f.Emit(ParticleEffects.Explosions, new Vector2(10, 10), 25);
            for (int i = 0; i < 30; i++) f.Update(Dt);
            var l = new List<Vector2>();
            foreach (var p in f.Alive) l.Add(p.Position);
            return l;
        }
        Assert.Equal(Run(), Run());
    }

    [Fact]
    public void Equal_ends_of_a_range_draw_no_randomness()
    {
        var a = new Random(5);
        var b = new Random(5);
        var effect = new ParticleEffect { Count = 1, SpeedMode = ParticleSpeed.Fixed, SpeedMin = 10f, LifeMin = 2f, LifeMax = 2f };
        effect.Spawn(a, Vector2.Zero, new List<Particle>());
        b.NextDouble();      // only the direction was drawn
        Assert.Equal(b.Next(), a.Next());
    }

    [Fact]
    public void Count_and_colours_can_be_overridden_per_burst()
    {
        var f = new TestField { Random = new Random(1) };
        f.Emit(ParticleEffects.Sparkles, Vector2.Zero, 7, Color.Cyan);
        Assert.Equal(7, f.Alive.Count);
        Assert.All(f.Alive, p => Assert.Equal(Color.Cyan, p.Color));      // one colour: every particle gets exactly it
    }

    [Fact]
    public void Registered_effects_play_by_name_and_a_typo_fails_loudly()
    {
        var f = new TestField { Random = new Random(1) };
        f.Register("ShipBoom", ParticleEffects.Burst(10, 64f, 256f, 3f, 0.05f, Color.Red));
        f.Emit("shipboom", Vector2.Zero);                                  // names ignore case
        Assert.Equal(10, f.Alive.Count);
        Assert.Throws<KeyNotFoundException>(() => f.Emit("shipbom", Vector2.Zero));
    }

    [Fact]
    public void Fireworks_burst_into_more_particles_where_they_die()
    {
        var f = new TestField { Random = new Random(3) };
        f.Emit(ParticleEffects.Fireworks, new Vector2(100, 100), 4);
        int most = 0;
        for (int i = 0; i < 400; i++)
        {
            f.Update(Dt);
            most = Math.Max(most, f.Alive.Count);
        }
        Assert.True(most > 4, "the dying particles should have burst into streaks");
        Assert.Empty(f.Alive);
    }
}

public class BuiltInEffectSnapshotTests
{
    sealed class F : ParticleField { public IReadOnlyList<Particle> Alive => particles; }

    // Recorded from the original hard-coded Emit... methods (the presets were first checked against them particle by particle, over
    // 400 frames, colours and fireworks' second stage included), seed 2024, 12 particles at (50, 60): count, sum x, sum y, colour checksum
    // at 0, 30 and 90 frames. A change to a preset's numbers shows up here.
    [Theory]
    [InlineData(ParticleEffectType.Confetti, 0, 12, 600.00f, 720.00f, 484324L)]
    [InlineData(ParticleEffectType.Confetti, 30, 12, 611.34f, 900.36f, 834782L)]
    [InlineData(ParticleEffectType.Confetti, 90, 10, 596.90f, 936.79f, 547974L)]
    [InlineData(ParticleEffectType.Explosions, 0, 12, 600.00f, 720.00f, 699291L)]
    [InlineData(ParticleEffectType.Explosions, 30, 12, 626.54f, 1030.88f, 713861L)]
    [InlineData(ParticleEffectType.Explosions, 90, 3, 58.65f, 466.50f, 118994L)]
    [InlineData(ParticleEffectType.Fireworks, 0, 12, 600.00f, 720.00f, 484324L)]
    [InlineData(ParticleEffectType.Fireworks, 30, 12, 617.01f, 532.00f, 685414L)]
    [InlineData(ParticleEffectType.Fireworks, 90, 30, 578.12f, 39.15f, 1630770L)]
    [InlineData(ParticleEffectType.Sparkles, 0, 12, 600.00f, 720.00f, 626425L)]
    [InlineData(ParticleEffectType.Sparkles, 30, 12, 546.95f, 761.70f, 753400L)]
    [InlineData(ParticleEffectType.Sparkles, 90, 0, 0f, 0f, 0L)]
    public void Built_in_effects_still_play_the_same(ParticleEffectType type, int frames, int count, float sumX, float sumY, long colours)
    {
        var f = new F { Random = new Random(2024) };
        f.Emit(ParticleEffects.For(type), new Vector2(50, 60), 12);
        for (int i = 0; i < frames; i++) f.Update(1f / 60f);

        float sx = 0, sy = 0; long col = 0;
        foreach (var p in f.Alive) { sx += p.Position.X; sy += p.Position.Y; col += p.Color.PackedValue % 100003; }
        Assert.Equal(count, f.Alive.Count);
        Assert.Equal(sumX, sx, 0.1f);
        Assert.Equal(sumY, sy, 0.1f);
        Assert.Equal(colours, col);
    }

    [Fact]
    public void Emit_by_type_uses_the_preset_and_the_manager_position()
    {
        var f = new F { Random = new Random(2024), Position = new Vector2(50, 60) };
        f.Emit(12, ParticleEffectType.Explosions);
        Assert.Equal(12, f.Alive.Count);
        Assert.All(f.Alive, p => Assert.Equal(new Vector2(50, 60), p.Position));
        f.Emit(5, ParticleEffectType.Sparkles, Color.Cyan);
        Assert.Equal(17, f.Alive.Count);
    }
}
