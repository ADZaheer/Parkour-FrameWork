# Parkour Framework

A Unity-focused sandbox for rapidly prototyping parkour movement mechanics. The goal is to provide a clean starting point that already understands running, crouching, wall running, and double jumping so you can immediately focus on level design, visuals, and advanced tricks.

## Repository layout

```
Assets/
  Scripts/
    Camera/
      FirstPersonCameraController.cs   # Basic mouse-look controller for FPS-style games
    Movement/
      ParkourMovementController.cs     # CharacterController-based locomotion with parkour states
Docs/
  MovementSystem.md                    # Deep dive into the movement state machine and extension ideas
```

## Getting started in Unity

1. Create a new 3D Unity project (2021.3 LTS or newer is recommended for the built-in CharacterController).
2. Copy the contents of this repository into your project directory (or pull it in as a submodule).
3. Create a **Player** GameObject and add the following components:
   - `CharacterController`
   - `ParkourMovementController`
4. Configure the inspector values on `ParkourMovementController`. Start with the defaults and tweak speeds, jump heights, and wall-run parameters for your prototype.
5. Create a child GameObject that holds the `Camera` and add `FirstPersonCameraController` to it. Drag the parent player transform into the script's `playerRoot` field.
6. Ensure your project uses Unity's legacy input manager (Edit → Project Settings → Input Manager) with the default axes `Horizontal`, `Vertical`, `Mouse X`, `Mouse Y`, and `Jump`. The scripts read from these mappings and expose configurable key bindings for sprinting and crouching.

### Quick play checklist

- Place the player on a simple level built with colliders.
- Set `Ground Mask` to include the layers that represent the floor.
- Provide `Wall Mask` so the controller knows which surfaces can be used for wall runs.
- Tune `Min Wall Run Height` to prevent wall runs when the player is already touching the ground.
- Use the Gizmos visualization (select the player in the Scene view) to see the ground and wall checks during play mode.

## Feature highlights

- **Ground locomotion** with acceleration, sprinting, and crouching.
- **Vertical gameplay** through jump, double jump, and wall-jump actions.
- **Wall running** that aligns the player's motion along detected surfaces and modulates gravity while active.
- **Context-aware input handling** so sprinting and crouching play nicely with other states.
- **Camera look controller** with smoothing and clamped pitch for a comfortable first-person experience.

## Extending the framework

The documentation in [`Docs/MovementSystem.md`](Docs/MovementSystem.md) outlines how the movement script is structured and points to areas where you can add mantling, ledge grabbing, sliding, stamina systems, or animation integration. The script is intentionally heavily commented to ease customization.

If you add systems like climbing, combat, or networking, consider organizing them under new folders inside `Assets/Scripts/` to keep the project tidy.
