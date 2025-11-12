# Path of Ember - Game Design Document

## 1. High Concept
Path of Ember is a fast-paced top-down 3D roguelite shooter inspired by Archero 2. Each run lasts around 5 minutes, pushing the player upward through procedurally generated arenas filled with enemies, walls, and hazards. A rising Hazard Zone shoots deadly projectiles every few seconds, forcing the player to stay on the move and maintain upward momentum. The objective is to survive and progress as far as possible. After 5 minutes, the player enters Overtime Mode, where difficulty scales aggressively and survival becomes the only goal.

## 2. Core Gameplay Loop
- Move upward through procedurally generated combat arenas.
- Defeat waves of enemies to unlock power-ups.
- Avoid the rising Hazard Zone that shoots projectiles and deals heavy damage.
- Survive 5 minutes to complete a standard run.
- Enter Overtime Mode for infinite challenge and higher rewards.
- Die, collect meta rewards, and prepare for the next run.

## 3. Player Objective
- **Primary Objective:** Survive for 5 minutes while progressing upward.
- **Secondary Objective:** Push past the time limit into Overtime to achieve higher scores and unlocks.

## 4. Core Mechanics
### Movement
- Top-down, single-joystick control
- Auto-fire when standing still, or proximity-based shooting

### Combat
- Variety of ranged enemies with distinct patterns
- Destructible walls for strategic cover
- Hazard Zone with dynamic projectiles

## 5. Game Progression & Overtime System
- **0:00 – 2:00:** Learning phase
- **2:00 – 4:00:** Increased difficulty
- **4:00 – 5:00:** Boss fight or large waves
- **5:00+:** Overtime Mode with exponential difficulty scaling

## 6. Procedural Map System
- Modular tile segments
- Different zone types (Empty, Combat, Mini-Boss, Reward)
- Dynamic enemy and obstacle spawning

## 7. Enemy & Boss Design
- Multiple enemy archetypes (Chaser, Shooter, Tank, Summoner)
- Boss encounters with unique patterns
- Overtime variants with enhanced abilities

## 8. Power-Up System
- Weapon Mods
- Defense Mods
- Utility Mods
- Overtime-exclusive Mods

## 9. Scoring & Rewards
- Distance Reached
- Time Survived
- Enemies Defeated
- Overtime Bonus

## 10. Technical Overview
- **Engine:** Unity 3D
- **Core Systems:**
  - Game Manager
  - Procedural Map Generator
  - Enemy Spawner
  - Hazard Zone Controller
  - Power-Up Manager
  - UI Controller

## 11. Design Pillars
1. Constant Motion
2. Risk and Reward
3. Clarity and Feedback
4. Short Runs, Long Mastery
5. Tension through Progression
