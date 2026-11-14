# Changelog

## 0.1.0-preview.1 (unreleased)

First extraction from Vector Rumble and Solid Ops.

- `VectorEngine.Core`: game base class, screens and menus, input and action bindings, virtual gamepad, line rendering and bloom, audio, settings, localization, particles, world simulation, start-up splash.
- `VectorEngine.Networking`: optional LAN layer with a protocol version byte on every packet (Vector Rumble moved from protocol 1 to 2).
- Verified on Desktop and Android.
- Particle effects are data: `ParticleEffect`, `ParticleEffects` presets, `ParticleField` (headless simulation, seedable `Random`) under `ParticleManager`, and named registration. `ParticleEffects.Burst` reproduces the classic `ParticleSystem` explosion frame for frame.
