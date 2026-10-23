# Paw Arena

Paw Arena is an independent Unity physics-arena project inspired by the high-level match loop described in the project GDD. It does not reuse Cat Battles branding or assets.

## Current foundation

The first base targets the offline P0 vertical slice:

- GDD-aligned game-flow states: Boot, Lobby, PreMatch, Playing, Spectating, Results.
- Configurable PC/gamepad input for Move, Look, Punch, Grab, Lift, Jump, and Ability.
- Rigidbody movement with acceleration, camera-relative direction, jump buffering, and coyote time.
- ScriptableObject movement tuning using the GDD's provisional 6 m/s movement and 1.5 m jump height.
- Edit Mode tests for valid and invalid game-flow transitions.

Punch, Grab/Lift/Throw, ring-out, match presentation, and networking remain separate follow-up slices. Throw input is intentionally not invented because the GDD marks it as TBD.

## Project structure

```text
Assets/_Project/
  Animations/          Character and UI animation assets
  Audio/               Music, gameplay SFX, and UI audio
  Documentation/       Project references and retained template documentation
  Fonts/               Font assets
  Localization/        Localized tables and locale assets
  Materials/           Character, environment, UI, and VFX materials
  Models/              Character, environment, and prop models
  Prefabs/             Character, gameplay, UI, and VFX prefabs
  Scenes/              Boot, Lobby, Arena, and Prototype scenes
  ScriptableObjects/   Catalogs, configs, events, and runtime sets
  Scripts/
    Bootstrap/         Composition root and startup wiring
    Core/              Pure game rules, data, interfaces, and events
    Gameplay/          Feature code such as Player, Combat, Arena, and Match
    Infrastructure/    Persistence, networking adapters, and platform services
    UI/                Views and presenters
    Editor/            Unity Editor-only tooling
    Tests/             Edit Mode and Play Mode tests
  Settings/            Input and rendering configuration
  Shaders/             Project-authored shaders
  Sprites/             Icons and UI sprites
  Textures/            Character, environment, and UI textures
  UI/                  Shared UI Toolkit or presentation assets
  VFX/                 VFX Graph and particle assets

Assets/Plugins/         Third-party plugins only
```

Keep every Paw Arena-authored asset under `Assets/_Project`. Third-party packages belong in `Assets/Plugins` or Unity's `Packages` folder. Do not create a `Resources` folder unless a concrete runtime-loading requirement justifies it.

Assembly dependencies flow in one direction:

```text
PawArena.Gameplay -> PawArena.Core
PawArena.Core.Tests -> PawArena.Core
```

## Local player setup

1. Use `Assets/_Project/ScriptableObjects/Configs/Player/PlayerMovementConfig.asset`, or create another config from `Assets > Create > Paw Arena > Player > Movement Config`.
2. Add `Rigidbody`, `CapsuleCollider`, `PlayerInputReader`, and `PlayerMotor` to the player root.
3. Assign `Assets/_Project/Settings/Input/PawArena.inputactions` to `PlayerInputReader`.
4. Assign the movement config and input reader to `PlayerMotor`.
5. Optionally assign the camera transform as the movement reference.
6. Put arena geometry on the selected ground mask.

The default bindings are:

- WASD: Move
- Mouse: Look
- Left mouse: Punch
- Hold right mouse: Grab
- Left Shift: Lift
- Space: Jump
- Q: Ability

## Branch workflow

- `main`: stable integration branch.
- `develop`: active development branch.

Do not commit Unity-generated folders such as Library, Temp, Logs, UserSettings, or generated IDE project files.
