# Bouncing Arrows Power-Up Implementation Plan

## Status: ✅ IMPLEMENTED

---

## What Was Implemented

### ArrowProjectile.cs
- Added `hasBouncing` flag, `maxBounces = 2`, `wallLayers` LayerMask
- Added `HandleBounce()` method using `Vector3.Reflect` for accurate reflection
- Fallback logic for flat box colliders using dominant axis flip
- Lifetime resets on each bounce
- Arrow moves slightly away from wall to prevent double-bounce

### PlayerAbilities.cs  
- Already had `HasBouncingBullets` property ✅

### PlayerShooting.cs
- Added `projectile.HasBouncing = abilities.HasBouncingBullets` in FireProjectile()

---

## How To Use

1. **Set Wall Layers on Arrow Prefab**: 
   - Select Arrow prefab
   - In "Bouncing Arrows" section, set `Wall Layers` to include "Walls"

2. **Enable Power-Up**:
   - Right-click PlayerAbilities > "Debug: Grant Bouncing Bullets"
   - Or call `playerAbilities.GrantBouncingBullets()`

3. **Behavior**:
   - Arrow hits wall → bounces in reflected direction
   - Lifetime resets on bounce
   - Max 2 bounces, then destroyed on 3rd wall hit

### 2. Detect Wall Collision

Change from trigger-only to also detect wall collisions:

```csharp
private void OnTriggerEnter(Collider other)
{
    // Check if it's a wall
    if (hasBouncing && IsWallLayer(other.gameObject.layer))
    {
        HandleBounce(other);
        return; // Don't destroy on wall hit
    }
    
    // ... existing enemy damage code ...
}

private bool IsWallLayer(int layer)
{
    return ((1 << layer) & wallLayers) != 0;
}
```

### 3. Calculate Bounce Reflection

```csharp
private void HandleBounce(Collider wallCollider)
{
    if (bounceCount >= maxBounces)
    {
        Destroy(gameObject);
        return;
    }
    
    // Get the wall's surface normal at hit point
    // Option A: Raycast to get precise normal
    RaycastHit hit;
    if (Physics.Raycast(transform.position - moveDir * 0.5f, moveDir, out hit, 1f, wallLayers))
    {
        Vector3 newDir = Vector3.Reflect(moveDir, hit.normal);
        newDir.y = 0; // Keep flat
        SetDirection(newDir);
        bounceCount++;
    }
}
```

### 4. PlayerAbilities Integration

Add the bouncing power-up to `PlayerAbilities.cs`:

```csharp
public bool HasBouncingArrows { get; set; } = false;
```

### 5. PlayerShooting Configuration

When spawning arrows, set the bouncing flag:

```csharp
if (abilities.HasBouncingArrows)
{
    arrow.HasBouncing = true;
}
```

---

## Configuration Options

| Setting | Default | Description |
|---------|---------|-------------|
| `maxBounces` | 3 | How many times arrow can bounce |
| `wallLayers` | Walls | Which layers trigger bounce |
| `damageReductionPerBounce` | 0% | Optional: reduce damage each bounce |
| `speedMultiplierPerBounce` | 1.0 | Optional: slow down each bounce |

---

## Edge Cases to Handle

1. **Corner bounces** - Arrow hits corner, might bounce back toward player
2. **Rapid bounces** - Add small cooldown between bounces to prevent infinite loops
3. **Piercing + Bouncing** - Define behavior (both? bouncing disabled when piercing?)
4. **Bounce VFX** - Optional spark effect on wall impact

---

## Files to Modify

| File | Changes |
|------|---------|
| `ArrowProjectile.cs` | Add bounce logic, wall detection |
| `PlayerAbilities.cs` | Add `HasBouncingArrows` property |
| `PlayerShooting.cs` | Set bounce flag when spawning arrows |

---

## Prerequisites

- [ ] Set up walls with "Walls" layer across the scene
- [ ] Ensure walls have non-trigger colliders

---

## Testing Checklist

- [ ] Arrow bounces off walls at correct angle
- [ ] Bounce count is respected (max 3)
- [ ] Arrow still damages enemies after bouncing
- [ ] Arrow is destroyed after max bounces
- [ ] Power-up toggle works correctly
- [ ] Works with piercing arrows (if allowed)

---

*Ready to implement when walls are set up! 🏹*
