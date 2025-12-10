# Path of Ember

## Overview
Path of Ember is a roguelike action game featuring procedural map generation and Archero-style movement mechanics. The game focuses on run-and-gun gameplay where the player must stop moving to shoot, creating a strategic rhythm of positioning and attacking.

## Core Mechanics

### Movement & Combat
- **Control**: Single-stick floating joystick.
- **Move vs. Shoot**: Player moves while dragging; auto-attacks only while stationary.
- **Feel**: Snappy acceleration/deceleration with no diagonal speed boost.
- **Mechanic**: Stutter-stepping (micro-move, stop, fire, repeat) is a key mastery skill.

### Procedural Generation
- The world is generated in finite chunks.
- **Sequence**: Start Chunk -> Lava Biome -> Grass Biome -> Mud Biome -> Boss Chunk.
- **Streaming**: Chunks are loaded/unloaded relative to player position to maintain performance.

### Implementation Status
- **Player Movement**: Implemented (`PlayerMovement.cs`) with collision handling, boundaries, and joystick input.
- **Map Generation**: Implemented (`ChunkManager.cs`) supporting sequential biome spawning.
- **Combat**: Basic shooting (`PlayerShooting.cs`) and projectile logic (`Projectile.cs`) are in place.

## Roadmap & Pending Features
- **PowerUps System**: Roguelike upgrade system (to be implemented).
- **Enemy AI**: Telegraphing attacks, line-of-sight logic, and varied movement patterns.
- **Biome Visuals**: distinct visual assets for Lava, Grass, and Mud biomes.
- **UI**: HUD for health, ammo/powerups, and progress.

## References
- **Movement Research**: See `Assets/Docs/Archero_Movement_Research.md`
- **Map Research**: See `Assets/Procedural Generated Map.md`
