# Movement System Overview

This document breaks down how `ParkourMovementController` works and how you can extend it for your own prototypes. The script is written for Unity's `CharacterController` component because it gives deterministic movement, keeps collision handling straightforward, and works well for first-person and third-person games alike.

## Core responsibilities

`ParkourMovementController` is responsible for:

1. Reading input from Unity's legacy input system (`Input.GetAxis` and `Input.GetButton`).
2. Tracking high-level locomotion states such as **grounded**, **airborne**, **crouched**, and **wall running**.
3. Producing a final velocity vector by blending horizontal motion, vertical motion, and state-specific adjustments.
4. Applying the velocity to the `CharacterController` each frame.

The controller intentionally avoids any animation or camera coupling, so you can keep those systems modular.

## State machine

At runtime the character flows through the following states:

- **Grounded** – default movement with configurable walk, sprint, and crouch speeds.
- **Airborne** – standard jumping/falling with optional double jumps.
- **Wall Running** – triggered when the character is airborne, moving forward, and a valid wall is detected to the left or right.
- **Crouched** – modifies the character height and movement speed.

The controller does not use a formal `enum` for the state machine; instead, booleans such as `isGrounded`, `isWallRunning`, and `isCrouching` determine how the velocity is composed. This approach keeps the script flexible so you can blend new behaviors without rewriting the structure.

### Wall running detection

Wall runs are detected with two raycasts (left and right). When one hits a collider that matches the `Wall Mask`, the controller:

- Stores the wall normal for later use in jumping and directional calculations.
- Accelerates the player along the cross product of the wall normal and the global up vector.
- Reduces gravity while wall running to allow longer traversal without an artificial boost.

Jumping while wall running launches the player away from the wall using configurable push and upward forces. When the wall is lost (raycast misses, the player moves too slowly, or falls below the minimum height) the script gracefully exits the wall-run state.

### Crouch logic

Pressing the crouch key immediately shortens the `CharacterController` and adjusts its center. Attempting to stand up first checks for headroom using a capsule cast so the player does not clip through ceilings. You can tune the crouch height in the inspector to match your character's proportions.

## Inspector parameters

The most important tunables are:

| Group | Parameter | Description |
|-------|-----------|-------------|
| Movement | Walk/Sprint/Crouch Speed | Base speeds for different locomotion modes. |
| Movement | Acceleration & Air Control | Lerp factors that determine how fast the player reaches target velocities. |
| Jumping | Jump Height | Controls the upward velocity applied when jumping from the ground. |
| Jumping | Extra Air Jumps & Double Jump Height | Allow mid-air jumps when wall running is not active. |
| Gravity | Gravity & Grounded Gravity | Control falling speed and the sticky force that keeps the controller grounded. |
| Wall Running | Check Distance, Speed, Gravity, Jump Forces | Determine how forgiving and fast wall runs feel. |
| Ground Detection | Ground Check Transform/Radius/Mask | Optional override for manual ground detection using a sphere check. |

`FirstPersonCameraController` exposes sensitivity, pitch limits, and smoothing parameters for camera motion. It expects to be attached to the Camera object with a reference to the player root transform so yaw rotates the body while pitch tilts only the camera.

## Extending the controller

Here are a few common extensions and where to start:

- **Mantling / Ledge Grab** – Hook into the section that resolves jumps (`HandleJump`). Detect when the player is near a ledge and replace the jump impulse with a coroutine that animates the character upward onto the ledge.
- **Slide or Dash** – Introduce a new state flag and adjust horizontal speeds in `ApplyMovement`. You can reuse the crouch height logic to lower the character collider temporarily.
- **Stamina System** – Track a stamina value, drain it while sprinting or wall running, and gate the states when stamina is depleted. The script exposes helper properties like `IsSprinting` to drive UI feedback.
- **Animation** – Use the `CurrentVelocity` and state properties in an animator controller to blend walk/run cycles, air animations, and wall-run poses.

## Debugging tips

- Toggle Gizmos in the Scene view to see the ground-check sphere and wall-check rays drawn by `OnDrawGizmosSelected`.
- Enable the `Use Inspector` checkboxes for `Ground Mask` and `Wall Mask` to ensure your level geometry is included.
- Watch the `Inspector` during play mode; the serialized fields such as `IsWallRunning` expose the current state, which makes diagnosing edge cases easier.

By starting with this framework you can focus on crafting interesting level layouts, traversal challenges, and game-specific rules rather than rebuilding core locomotion for every prototype.
