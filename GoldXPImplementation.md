# Gold Coin XP System - Implementation Guide

> **Core Concept:** In Path of Ember, XP = Gold Coins. Players don't see "XP" - they collect gold coins that represent experience points.

---

## System Overview

```
Enemy Death → Calculate Coins → Spawn Coins (Burst) → Magnetic Pull → Collection → XP Award
```

---

## 1. Coin Amount Calculation

Rather than hardcoding coins per enemy, use a **formula-based approach** that scales automatically:

| XP Value | Coins | Example Enemy |
|----------|-------|---------------|
| 1-5 | 1 | — |
| 6-15 | 2-3 | Chaser (10 XP → 2 coins) |
| 16-30 | 4-6 | Bomber/Sniper (~15 XP → 3 coins) |
| 50+ | 10 | Miniboss (50 XP → 10 coins) |

### Recommended Formula
```csharp
int coins = Mathf.Clamp(Mathf.CeilToInt(xpReward / 5f), 1, maxCoins);
```

This scales automatically - if you tweak enemy XP values later, coins adjust without manual mapping.

### Hazard Kill Integration
Since meteors grant 50% XP, coins should reflect this:
```csharp
int xpToGrant = ...; // Already calculated with 0.5x multiplier for hazards
coinManager.SpawnCoins(enemyPosition, xpToGrant);
```
The coin system receives the **final XP amount**, so hazard kills naturally spawn fewer coins.

---

## 2. Visual Coin Tiers (Optional Polish)

For visual variety and fewer spawned objects with high-value enemies:

| Coin Type | XP Value | Visual Description |
|-----------|----------|-------------------|
| **Bronze** | 1 XP | Small, copper color |
| **Silver** | 5 XP | Medium, silver shine |
| **Gold** | 10 XP | Large, golden glow |

**Example:** Miniboss dropping 50 XP = 1 Gold + 4 Silver (5 objects) instead of 10 Bronze (10 objects).
Fewer objects, more impressive visual!

---

## 3. Spawn Animation - "The Burst"

### Phase 1: Explosion Burst (0.3s)
- Coins spawn at enemy death position
- Each coin flies outward in a **random arc** (Diablo/Hades style loot explosion)
- Use `Rigidbody` with initial velocity + gravity, OR animate with DOTween/AnimationCurve
- Add slight **random spin** on each coin for life

**Key Parameters:**
| Parameter | Value | Description |
|-----------|-------|-------------|
| `burstForce` | 3-5 units | Horizontal spread distance |
| `burstHeight` | 1-2 units | Vertical pop height |
| `spawnDelay` | 0.02-0.05s | Stagger between coins (cascading effect) |

### Phase 2: Settle (0.2-0.4s)
- Coins land and briefly rest on ground
- Small bounce on landing (physics or simulated)

---

## 4. Magnetic Collection

### Phase 3: Attraction
Two trigger modes to consider:

1. **Proximity-based:** Coins start flying to player when within X radius
2. **Timed:** After 0.5s settle time, all coins auto-attract (ensures collection)
3. **Hybrid:** Whichever happens first

### Attraction Curve
- Start slow, accelerate toward player (ease-in curve)
- Use `Vector3.MoveTowards` with increasing speed, or lerp with exponential factor
- Optional: Trail renderer or particle trail behind coins during flight

### Collection Moment
- On contact with player collider → Add XP, play collect SFX, spawn small VFX burst
- Coins should **overlap collection timing** - rapid "ding-ding-ding" sounds feel satisfying

---

## 5. Audio Design

| Event | Sound Character |
|-------|-----------------|
| Coin spawn | Light metallic "clink" (pitch-varied per coin) |
| Coin bounce | Soft tap on ground |
| Coin collect | Satisfying "ding" (pitch increases with rapid collection) |
| All coins collected | Subtle completion chime (optional) |

### Pitch Scaling Trick
Each successive coin collected raises pitch slightly:
```
1.0 → 1.05 → 1.1 → 1.15 → ...
```
Resets after 0.5s gap between collections. Creates that addictive "combo" feel!

---

## 6. Component Structure

```
GoldCoinManager (Singleton)
├── Handles object pooling
├── Manages collection sounds (pitch scaling)
└── Exposes SpawnCoins(Vector3 position, int xpAmount)

GoldCoin (Prefab)
├── Rigidbody (for burst physics)
├── Collider (trigger for collection)
├── GoldCoinBehavior script
│   ├── State: Bursting → Settling → Attracted → Collected
│   ├── Magnetic pull logic
│   └── VFX/SFX triggers
├── Visual (coin mesh + spinning animation)
└── Optional: Trail Renderer (for attraction phase)
```

---

## 7. State Machine for GoldCoin

```csharp
public enum CoinState
{
    Bursting,    // Initial explosion outward
    Settling,    // Resting on ground briefly
    Attracted,   // Flying toward player
    Collected    // Absorbed, granting XP
}
```

### State Transitions
```
Bursting → (velocity near zero) → Settling
Settling → (timer OR proximity) → Attracted
Attracted → (reached player) → Collected → Destroy/Pool
```

---

## 8. Key Settings to Expose in Inspector

### GoldCoinManager
- `coinPrefab` - The coin prefab
- `xpPerCoin` - How much XP each coin represents (default: 5)
- `maxCoinsPerDrop` - Cap on coins spawned (default: 10)
- `collectSoundPitchStep` - Pitch increment per rapid collection

### GoldCoinBehavior
- `burstForce` - Initial explosion force
- `burstHeight` - Vertical component of burst
- `settleTime` - How long coins rest before attraction
- `attractionRadius` - Distance to start magnetic pull
- `attractionSpeed` - Base speed toward player
- `attractionAcceleration` - Speed increase over time

---

## 9. Integration Points

### Enemy Death (EnemyHealth.cs)
```csharp
// In Die() method, after XP calculation:
if (GoldCoinManager.Instance != null && xpToGrant > 0)
{
    GoldCoinManager.Instance.SpawnCoins(VisualCenter, xpToGrant);
}
```

### Player Collection
- Add a trigger collider on Player tagged for coin collection
- OR use Physics.OverlapSphere in GoldCoin to detect player

---

## 10. Performance Considerations

- **Object Pooling:** Essential for coin prefabs - don't Instantiate/Destroy constantly
- **Coin Limit:** Cap maximum active coins in scene (recycle oldest if exceeded)
- **LOD:** Disable shadows on coins, use simple materials
- **Batching:** Ensure coins use same material for GPU instancing

---

## Open Design Questions

Before implementation, decide on:

1. **Coin tiers** - Single coin type or Bronze/Silver/Gold visual variety?
2. **Collection trigger** - Proximity-based, auto-collect after delay, or both?
3. **Physics preference** - Real Rigidbody physics for burst, or simulated curves (lighter performance)?
4. **Pooling** - Use existing pooling system or create dedicated one for coins?

---

## Reference Games for Feel

- **Hades** - Excellent coin burst and magnetic collection
- **Diablo III** - Satisfying gold pickup sounds and visuals
- **Vampire Survivors** - High volume coin collection with pitch scaling

---

*Created: 2026-01-12*
