# 🔥 Path of Ember: System Architecture & Tuning Summary

> **Overview**: This document provides a deep dive into the technical and gameplay systems of *Path of Ember*. It categorizes systems from high-level gameplay loops down to specific tuning values, designed for quick reference during presentations.

---

## ⚔️ Core Gameplay & Combat
The heart of the experience. Fast-paced, responsive, and satisfying.

### 🏃 Player Controller
*File: `PlayerMovement.cs`*
*   **Movement**: Physics-based logic using `Rigidbody.linearVelocity` for smooth collisions without jitter.
*   **Controls**: Supports both **Joystick** (Mobile) and **WASD** (Desktop).
*   **Tuning**:
    *   **Base Speed**: `5.0` units/sec (Snappy but controllable).
    *   **Acceleration**: `0.08s` (Almost instant, feels responsive like Archero).
    *   **Deceleration**: `0.06s` (Stops on a dime for precision dodging).
    *   **Boundary**: Clamped at X `±4.87` to keep player on the bridge.

### 🏹 Combat System
*File: `PlayerShooting.cs`*
*   **Auto-Targeting**: Automatically detects the nearest enemy within range and rotates the character visual (upper body) to face them.
*   **Attack Speed**: Base fire rate is `1.5 shots/sec`.
*   **Tuning**:
    *   **Range**: `12.0` units (Covers most of the visible screen).
    *   **Projectile Speed**: `18.0` units/sec (Fast enough to hit moving targets cleanly).
    *   **Tempo**: Global `shootingTempo` multiplier scales animation speed and fire rate in sync.

---

## 🤖 Enemy Intelligence (AI)
Enemies are built on a shared hierarchy (`EnemyAIBase`) for consistent behavior (movement, line-of-sight, contact damage) but feature unique specializations.

### 🧟 Chaser (The Grunt)
*File: `ChaserAI.cs`*
*   **Behavior**: Relentlessly pursues the player to deal melee damage.
*   **Specialty**: Uses upper-body animation blending to punch while moving.
*   **Tuning**:
    *   **Speed**: `3.5` (Slower than player, allowing kiting).
    *   **Damage**: `20` per hit.
    *   **Tactics**: Stops at `0.4` units to attack.

### 💣 Bomber (The Kamikaze)
*File: `BomberAI.cs`*
*   **Behavior**: High-speed rusher that self-destructs upon reaching the player.
*   **Visuals**: Swells up and glows orange (`_EmissionColor`) before exploding.
*   **Tuning**:
    *   **Speed**: `5.25` (1.5x base speed - Dangerous!).
    *   **Detonation**: Triggers at `2.5` units.
    *   **Effect**: After `1.0s` delay, deals `50` damage in a `3.5` unit radius.
    *   **Reward**: Grants **50% XP** on self-destruct (fair reward for survival).

### 🎯 Sniper (The Turret)
*File: `SniperAI.cs`*
*   **Behavior**: "Stutter-step" movement. Moves in cardinal directions (Up/Down/Left/Right), then stops to aim and fire.
*   **Visuals**: Displays a **LASER SIGHT** (LineRenderer) that turns red before firing.
*   **Tuning**:
    *   **Move Timeout**: `1.5s` (Forces repositioning).
    *   **Aim Time**: `0.75s` (Gives player time to dodge).
    *   **Tactic**: Does not rotate body while moving; only rotates to face player when shooting.

---

## 🌍 World Generation & Hazards
The world is infinite, dangerous, and procedurally assembled.

### 🏗️ Infinite Bridge System
*File: `ChunkManager.cs`*
*   **Logic**: Spawns predefined "Chunks" (Prefabs) in a linear sequence.
*   **Biomes**: Randomly selects from **Lava**, **Grass**, and **Mud** biomes.
*   **Pooling**: Recycles chunks behind the player to front to maintain 0 GC (Garbage Collection) allocation during gameplay.
*   **Tuning**:
    *   **Chunk Length**: `10` units.
    *   **Buffer**: Keeps `3` chunks ahead and `1` behind.

### 🌋 Hazard Zone (The "Storm")
*File: `HazardZoneMeteors.cs`*
*   **Concept**: A death zone that follows the player, forcing forward momentum.
*   **Feature**: Spawns meteors with increasing intensity the deeper you fall behind.
*   **Tuning**:
    *   **Advance Speed**: `0.5` units/sec (Slow, creeping pressure).
    *   **Warning Area**: `10` units from edge (Meteors start falling here).
    *   **Intensity**:
        *   **Edge**: `0.5` meteors/sec.
        *   **Deep**: `5.0` meteors/sec (Survival impossible).
    *   **Accuracy**: Meteors become more accurate (targeting player directly) as depth increases.

---

## 🔮 Progression & Talents
A roguelite layer providing power spikes and replayability.

### ⭐️ XP & Leveling
*File: `XPSystem.cs`*
*   **Scaling**: XP requirement increases by **15%** per level.
*   **Flow**: Coins grant XP -> Level Up -> Triggers **Prayer Wheel**.

### 🎡 Prayer Wheel (Talent Selection)
*File: `TalentSelectionManager.cs`*
*   **Rarity System**:
    *   **Common (Grey)**: 50% chance.
    *   **Rare (Blue)**: 30% chance.
    *   **Legendary (Gold)**: 20% chance.
*   **Visuals**: 3D spinning wheel selects 3 random upgrades from `TalentDatabase`.

---

## ⚙️ Technical Performance Systems
Optimized for mobile to ensure 60 FPS.

### 💾 Loading Screen (Smart Preloader)
*File: `LoadingScreenManager.cs` & `AssetPreloader.cs`*
*   **Logic**: Hides the "lag" of instantiating heavy objects.
*   **Phases**:
    1.  **Scene Load (20%)**: Unity loads the scene.
    2.  **Asset Warming (80%)**: Spawns Chunks, Projectiles, and VFX into the pool off-screen.
*   **Result**: Game starts with zero frame drops because everything is already in memory.

### ♻️ Object Pooling
*File: `ObjectPoolManager.cs`*
*   **Function**: Reuses objects (Arrows, Enemies, Chunks) instead of `Destroy()`/`Instantiate()`.
*   **Impact**: Eliminates CPU spikes from memory allocation, crucial for endless runners.

---

## 📊 Quick Tuning Reference Table

| Entity | HP/Damage | Speed | Range/Radius | Notes |
| :--- | :--- | :--- | :--- | :--- |
| **Player** | 100 HP | 5.0 | 12.0 (Bow) | Snappy controls |
| **Chaser** | Default HP | 3.5 | 0.4 (Punch) | Kitable |
| **Bomber** | Low HP | **5.25** | 3.5 (Explosion) | **PRIORITY TARGET** |
| **Sniper** | Medium HP | 3.5 | Infinite (Proj) | Telegraphs attacks |
| **Meteor** | Instant Kill | N/A | 1.5 (Impact) | Visual warning first |

---

*Generated for Path of Ember Presentation* 🚀
