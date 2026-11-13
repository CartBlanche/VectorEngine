# VectorEngine

A small [MonoGame](https://monogame.net) engine for neon vector-line games: line rendering with bloom, screens and menus, input and bindings, audio, settings, localization, particles, a world simulation and an optional LAN layer. It grew out of two games that now share it, and it is meant for future ones.

Status: **0.1.0-preview**. It runs two shipped games on Desktop and Android. iOS, Windows and WebGL heads are not verified.

## Games using VectorEngine

| Game | Source | Play |
|---|---|---|
| **Vector Rumble**, a twin-stick arena shooter with LAN multiplayer | [CartBlanche/VectorRumble](https://github.com/CartBlanche/VectorRumble) | [itch.io](https://softsavage.itch.io/vector-rumble) |
| **Solid Ops**, a stealth game: plan Pi's route, then watch it play out | [CartBlanche/SolidOps](https://github.com/CartBlanche/SolidOps) | [itch.io](https://softsavage.itch.io/solid-ops) |

Made a game with VectorEngine? Open a pull request adding it to this table.

## What's in it

| Package / project | What it is |
|---|---|
| `VectorEngine.Core` | Everything a game needs. Namespaces are `VectorEngine.*`. |
| `VectorEngine.Networking` | Optional LAN layer. The only project that references `MonoGame.Xna.Framework.Net`. |
| `VectorEngine.Tests` | xUnit tests. |
| `Content` | The engine's shaders (bloom, blur), built by the game's content pipeline. |

### In Core

- **`VectorEngineGame`**: derive from it and pass `VectorEngineGameOptions` (virtual and window size, orientations, VSync, the audio project name, `ScreenManagerOptions`). It creates the graphics device, screen manager and audio manager.
- **Screens**: `ScreenManager`, `GameScreen`, and the default `MenuScreen`, `MenuEntry`, `MessageBoxScreen` and `LoadingScreen`, restyled with a `MenuStyle`. A game with another menu look writes its own screens.
- **Input**: `InputState`, `ActionBindings` (core `GameAction`s plus your own via `bindings.Define(new ActionId("Send"), ...)`; the saved settings format keeps action names) and `VirtualGamePad`, a data-driven touch gamepad with a `VirtualGamePadLayout`.
- **Rendering**: `Display` (virtual resolution, default 720p; `Display.Configure` changes it), `LineBatch`, `VectorPolygon`, `BloomComponent`.
- **Simulation**: `WorldBase`, `Actor`, `WorldActor`, `CollisionResult`. A game derives its own `World` and actors. The update order is fixed: actors, collision, particle systems, a hook, then collection.
- **Effects**: `ParticleManager` and the older `ParticleSystem`.
- **Splash**: `SplashScreen.Start(manager, first)` plays the VectorEngine start-up animation (lines converge into the V+E mark and the name), then runs `first` to add the game's first screens. `VECTORENGINE_NO_SPLASH=1` skips it; `VECTORENGINE_SPLASH_WAIT=1` holds on the first frame until Space, for recording.
- **Settings, localization, audio, diagnostics**: `SettingsManager`, `LocalizationManager`, `AudioManager` (XACT projects), `PerfOverlay`, `AllocProbe`, and `Platform` (mobile or desktop).

The engine has no player-visible text of its own: each game supplies its strings and languages.

### Networking

Packet ids 64-69 are reserved by the engine (`EnginePacketIds`: Ping 64, Pong 65); a game's own ids start at 70. Every packet is `[protocol version][id][body]`: a packet of another version is dropped and counted, and `PacketDispatcher.ProtocolMismatch` fires. Start with `MultiplayerSessionService.Initialize("MyGame", protocolVersion: 1)`; the log is `{game}-net.log`.

## Using it

The engine does not bring a MonoGame platform package with it. Your game head references its own, at the version the engine is built against (**3.8.6.1**).

```xml
<ItemGroup>
  <PackageReference Include="MonoGame.Framework.DesktopGL" Version="3.8.6.1" />       <!-- or your platform's -->
  <PackageReference Include="MonoGame.Content.Builder.Task" Version="3.8.6.1" />
  <PackageReference Include="VectorEngine.Core" Version="0.1.0-preview.1" />
  <PackageReference Include="VectorEngine.Networking" Version="0.1.0-preview.1" />   <!-- optional -->
</ItemGroup>
```

The package adds the engine's shaders (`Engine.mgcb`) to your content build for the head project. Reference `VectorEngine.Core` from the head project itself, not only from a shared library, so that build step reaches it. Set `<VectorEngineContent>false</VectorEngineContent>` if you want to build the shaders yourself.

Then:

1. Derive your game from `VectorEngineGame`.
2. Derive your `World` from `WorldBase`, and your actors from `Actor` or `WorldActor`.
3. Show the splash, then your first screens: `SplashScreen.Start(ScreenManager, () => ScreenManager.AddScreen(new MainMenu()))`.

### Working on the engine and a game together

Clone this repo next to your game (`CartBlanche/VectorEngine` beside `CartBlanche/MyGame`) and reference the projects instead of the packages. Vector Rumble and Solid Ops both do this through an `EngineDir` property that defaults to the sibling folder; pass `-p:EngineDir=/path/to/VectorEngine/` to point elsewhere.

## Build and test

```bash
dotnet build VectorEngine.sln
dotnet test VectorEngine.sln
dotnet pack VectorEngine.sln -c Release -o nupkgs
```

## Known gaps

- Verified on Desktop and Android only.
- `ParticleSystem` is still used by Vector Rumble's menu fireworks; it will be replaced by `ParticleManager` presets.
- Solid Ops' waypoint actor stays in Solid Ops: it depends on its own behaviour classes.
- The virtual gamepad's default art is Vector Rumble's `Textures/gamepad.png`; the game supplies the texture.
- A shared engine logo file (icon, banner) is still to make from the mark's geometry in `VectorEngineLogo`.

## History

How the engine was extracted from the two games, and the decisions along the way, are in [docs/EXTRACTION.md](docs/EXTRACTION.md).

## License

[Microsoft Public License (MS-PL)](LICENSE). VectorEngine began as code from Vector Rumble, a port of the XNA VectorRumble sample, which is under the same licence.
