# 🔥 Path of Ember: System Architecture & Game Systems Summary

> **Executive Summary**: This document outlines the technical architecture, gameplay loops, and systems tuning for *Path of Ember*. It is designed as a high-level reference for the development team and stakeholders, detailing how discrete systems interact to create the cohesive rogue-lite experience.

---

## ⚔️ Core Player Systems
*Responsive, physics-based controls designed for high-precision combat.*

### 🏃 Player Controller (`PlayerMovement.cs`)
*   **Physics-Based Movement**: Utilizing `Rigidbody.linearVelocity` guarantees smooth collision resolution against environmental hazards and enemies, preventing jitter common in transform-based movement.
*   **Input Handling**: Hybrid input system supporting both **Virtual Joystick** (Mobile) and **WASD** (Desktop) with normalized vectors to prevent diagonal speed boosting.
*   **Bridge Boundary Logic**: A clamped X-position (`±4.87`) constraint keeps the player firmly on the bridge without requiring invisible walls, saving physics processing overhead.

### 🏹 Combat & Abilities (`PlayerShooting.cs`, `PlayerAbilities.cs`)
*   **Auto-Targeting Architecture**: Raycast-verified target selection prioritizes the nearest visible enemy. The upper-body skeletal rig procedurally rotates to aim (`OnAnimatorIK`) while legs maintain movement direction.
*   **Projectile System**: Uses `ObjectPoolManager` for zero-allocation instancing. Projectiles align to velocity vector for realistic flight arcs.
*   **Ability Framework**:
    *   **Active**: Shields, Speed Boosts, Multi-shot.
    *   **Passive**: Stat modifiers handled by flexible `TalentData` ScriptableObjects.

### ❤️ Health & Resiliency (`PlayerHealth.cs`)
*   **Damage Pipeline**: `TakeDamage()` -> `CameraShake` + `HitFlash` -> `InvulnerabilityFrame`.
*   **Visual Feedback**:
    *   **Hit Flash**: Direct material emission manipulation using `_EmissionColor` for visibility even in dark environments.
    *   **Camera Shake**: Perlin-noise based shake (`CameraShakeManager`) provides visceral impact feedback without disorienting the player.

---

## 🤖 Enemy Ecosystem (AI)
*A hierarchical AI system built on `EnemyAIBase` sharing core logic (Pathfinding, LoS) with distinct behavioral specializations.*

### 🧟 Chaser (The Grunt)
*   **Role**: Pressure & Crowd Control.
*   **AI Logic**: Uses NavMesh to relentlessly pathfind to the player.
*   **Tech Detail**: Animation blending allows attacking while moving, preventing the "stop-and-pop" behavior often seen in simple AI.

### 💣 Bomber (The Threat)
*   **Role**: High-Priority Hazard.
*   **AI Logic**: Sprint-speed pathfinding with a suicide trigger radius.
*   **Visual Telegraph**: Uses `Material.SetColor("_EmissionColor")` to pulse orange/red before detonation, giving players a fair reaction window.
*   **Risk/Reward**: Grants **50% Bonus XP** on self-destruct, incentivizing risky proximity play.

### 🎯 Sniper (The Turret)
*   **Role**: Area Denial.
*   **AI Logic**: "Stutter-step" state machine (`Move` -> `Aim` -> `Fire`).
*   **Telegraph**: Renders a dedicated `LineRenderer` laser sight 0.75s before firing.
*   **Tuning**: Projectiles are non-homing, rewarding strafing movement.

### 👹 Miniboss (The Gatekeeper)
*   **System Integration**:
    *   **Arena Logic**: Spawns dynamic blockades (`MinibossArenaTrigger`) that trap the player.
    *   **Hazard Control**: Automatically **pauses** the global Lava Zone (`HazardZoneMeteors`) to ensure a fair fight, resuming it only upon death.
*   **Attacks**:
    *   **Meteor Rain**: Indirect fire forcing movement.
    *   **Fireball Volley**: Direct damage requiring cover or dodging.

### 💀 Titan Boss (The Climax)
*   **Phases**: Multi-stage encounter controlled by `TitanBossController`.
*   **Attacks**:
    *   **Core Blast**: Launches projectiles upward that return as arena-wide strikes.
    *   **Summon Hand**: Spawns enemies (Chasers) to split player attention.
    *   **Fist Smash**: Massive AoE physical damage.

---

## 🌍 World Generation & Environment
*Infinite procedural generation tailored for performant mobile play.*

### 🏗️ Infinite Bridge (`ChunkManager.cs`)
*   **Procedural Pooling**: A rolling buffer of "Chunks" (Prefabs) recycled front-to-back.
*   **NavMesh Architecture**: Bridges use `NavMeshSurface` components that bake at runtime (or pre-baked chunks linked via `NavMeshLink`), allowing AI to traverse generated geometry seamlessly.
*   **Biomes**: Randomly interleaved visual themes (Grass, Lava, Mud) to reduce visual fatigue.

### 🌋 Dynamic Hazard Zone (`HazardZoneMeteors.cs`)
*   **The "Storm" Mechanic**: A moving kill-plane that forces forward momentum.
*   **Dynamic Intensity**: Meteor spawn rate scales with player distance from the safe zone.
    *   *Warning Phase*: Ground decals indicate impact zones.
    *   *Impact Phase*: Physical meteor spawning and explosion AoE.

---

## 🔮 Progression & Economy
*Meta-systems driving replayability and power scaling.*

### 🎡 Dual Prayer Wheels (`PrayerWheelController.cs`)
*   **The Hook**: Primary upgrade mechanic presented as two spinning 3D wheels.
*   **Architecture**:
    *   **Data-Driven**: Wheels are populated dynamically from the `TalentDatabase`.
    *   **Rarity Tiers**: Common (Grey), Rare (Blue), Legendary (Gold) tiers with distinct visual materials and sound cues.
    *   **Anti-Frustration**: Logic ensures no duplicate talents appear in the same spin.

### ⛩️ Yatai Shop (`YataiShopInteraction.cs`)
*   **Economy Sink**: A world-space interactable shop appearing in chunks.
*   **Risk/Reward**: Allows players to spend generic XP/Coins for a **Guaranteed Rarity** spin on the next Prayer Wheel.
*   **Integration**: Seamlessly hooks into the `PrayerWheelController` to override probability weights for the next event.

### ⭐️ XP & Leveling (`XPSystem.cs`, `RunTalentRegistry.cs`)
*   **Persistence**: `RunTalentRegistry` (ScriptableObject) persists player stats across scene loads (Main Menu <-> Game).
*   **Scaling Curve**: geometric progression (15% increase per level) ensures pacing slows naturally as power increases.

---

## 🎧 Audio & Immersion
*A layered audio system for spatial awareness and feedback.*

### � Audio Manager (`AudioManager.cs`)
*   **Pooling**: Creating/Destroying AudioSources is expensive; the manager pools them for zero-allocation playback.
*   **Spatial Audio**:
    *   **3D Settings**: Enemy sounds (footsteps, attacks) use linear rolloff to provide directional cues.
    *   **2D Settings**: UI and Player impacts are prioritized in the mix.
*   **Dynamic Music**: `MusicManager` handles cross-fading between Biome tracks and Boss themes (`TitanTheme`).

### 💥 Game Feel Polish
*   **Damage Numbers**: Screen-space floating text (`PopupManager`) for immediate DPS feedback.
*   **Hit Stop**: Subtle `TimeScale` freeze (0.05s) on critical impacts to emphasize power.
*   **Screen Shake**: Variable intensity shake profiles (`Small`, `Medium`, `Large`) trigger on damage, explosions, and heavy landings.

---

## ⚙️ Technical Performance Profile
*Optimized for consistent 60 FPS on target hardware.*

| System | Optimization Technique | Benefit |
| :--- | :--- | :--- |
| **Asset Warming** | `AssetPreloader.cs` instantiates heavy prefabs during the Loading Screen (masked by UI). | No frame drops when looking at new enemies/VFX for the first time. |
| **Object Pooling** | `ObjectPoolManager.cs` recycles everything from projectiles to entire boss minions. | Zero GC allocation during combat loop. |
| **Shader Stripping** | Explicit `_EMISSION` handling ensures variants are kept only when needed. | Smaller build size, preventing pink shader errors. |
| **Physics** | Layer Collision Matrix optimized to ignore unnecessary checks (e.g., Enemy vs Enemy collisions disabled). | significantly reduced physics CPU cost. |

---

*System Summary Generated for Shipping Build Presentation* 🚀
