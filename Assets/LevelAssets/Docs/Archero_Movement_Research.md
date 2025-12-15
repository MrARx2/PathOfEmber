# Archero-Style Movement Research

## Summary
- **Control model**: Single-stick floating joystick. Spawns where you touch, disappears when released.
- **Two-state loop**: Move vs. Shoot. Player moves while dragging; auto-attacks only while stationary.
- **Feel**: Snappy starts/stops with subtle easing. No diagonal speed bonus. Input dead zone to prevent drift.
- **Mastery**: “Stutter-step”/attack-cancel: micro-move, stop, fire, repeat.
- **Enemies**: Predictable telegraphs, line-of-sight gating, simple homing/strafe patterns, spacing pressure.

References:
- Deconstructor of Fun — Archero breakdown: https://www.deconstructoroffun.com/blog/2019/8/9/why-archero-banked-25m-but-leaves-25m-hanging-hlx9n
- Scott Fine — Gameplay analysis: http://scottfinegamedesign.com/design-blog/2019/7/2/archero-part-1-gameplay
- Reddit — Auto-attack canceling: https://www.reddit.com/r/Archero/comments/c4s5g1/autoattack_canceling/
- Beginner’s guide (controls recap): https://www.levelwinner.com/archero-beginners-guide-tips-cheats-strategies/

## Player Movement Patterns (Archero)
- **Floating joystick**: Appears at touch, follows finger; hidden when not used.
- **Normalized movement**: Uniform speed; diagonals aren’t faster.
- **Acceleration profile**: Very short acceleration/deceleration with exponential ease to add polish without lag.
- **Dead zone**: Small dead zone prevents unintentional micro-movements.
- **Collision feel**: Slide along obstacles instead of hard-stopping.
- **Boundaries**: Hard clamping at arena edges.
- **Combat cadence**: Attacks only when stationary, rewarding positional “peeking.”

## Enemy Movement (for later)
- **Telegraphed pressure**: Enemies that lunge, strafe, or home slowly; patterns readable and dodgeable.
- **Line-of-sight triggers**: Some enemies act when they see the player, then commit to an attack window.
- **Spacing**: Mixed speeds and projectile travel times to encourage micro-movement and peeking.

## What We Adopted Now (Player)
- **Snappy easing**: Short accel/decel with exponential smoothing.
- **Obstacle handling**: Sweep test and slide along colliders tagged `"Colider"`.
- **Symmetric X-boundary**: Single inspector variable (e.g., `xBoundary = 4.87`) clamps X to `[-xBoundary, xBoundary]`.
- **Input sources**: Floating joystick (mobile) and keyboard (desktop) with dead-zone consideration from the joystick.
- **No diagonal boost**: Velocity is normalized before scaling by speed.

Implementation location: `Assets/Scripts/PlayerMovement.cs`
- Inspector fields:
  - `moveSpeed` — max horizontal/vertical speed
  - `accelerationTime` — time to reach full speed
  - `decelerationTime` — time to stop from full speed
  - `xBoundary` — symmetric clamp for X (single value, applies as ±X)
- Behavior:
  - Velocity smoothing with exponential impulse.
  - Sweep test; if a `Colider` is hit, project velocity onto contact plane to slide.
  - Clamp position X after physics step.

## Future Options
- Add “stop-to-shoot” state machine (idle = auto-attack, moving = no attack).
- Add aim-lock/face-last-aim heading while moving.
- Curved acceleration during initial 100ms for extra punch.
- Tunables per biome or difficulty tier.
- Enemy movement library implementing telegraph timings and LoS triggers.
