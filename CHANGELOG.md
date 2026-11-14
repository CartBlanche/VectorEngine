# Changelog

## 0.1.0-preview.5

- The start-up splash shows "VectorEngine <version>" in the bottom right corner (`SplashOptions.ShowVersion`, on by default). `EngineInfo.Version` is read from the built assembly, so it follows the package version.
- `SplashOptions.BloomIntensity`: a game can tone the intro's bloom down without changing the bloom of the rest of the game.
- The splash is no longer cut short by the click or key that launched the game: input in its first half second is ignored, and a mouse release does not skip it.
- `ScreenManager` reloads its font after a language change, and `AudioManager.SetCategoryVolume` sets the volume of an XACT category (music and effects sliders).

## 0.1.0-preview.4

- **Breaking:** `ParticleSystem` and `WorldBase.ParticleSystems` are removed. A world now has `WorldBase.Particles`, a `ParticleField` updated where the particle systems were; play `ParticleEffects.Burst(...)` (or any `ParticleEffect`) into it. Both games moved over; `Burst` is tested frame for frame against the original system (kept in the tests as a reference).

## 0.1.0-preview.3

- Logo files in `branding/` (transparent dark and light themes, a solid tile, favicon and social preview) built by `branding/make-logos.py` from the splash's own geometry. The packages carry the tile as their icon.

## 0.1.0-preview.2

- Particle effects are data: `ParticleEffect`, `ParticleEffects` presets, `ParticleField` (headless simulation, seedable `Random`) under `ParticleManager`, named registration. `ParticleEffects.Burst` reproduces the classic `ParticleSystem` explosion frame for frame.

## 0.1.0-preview.1

First extraction from Vector Rumble and Solid Ops.

- `VectorEngine.Core`: game base class, screens and menus, input and action bindings, virtual gamepad, line rendering and bloom, audio, settings, localization, particles, world simulation, start-up splash.
- `VectorEngine.Networking`: optional LAN layer with a protocol version byte on every packet (Vector Rumble moved from protocol 1 to 2).
- Verified on Desktop and Android.
