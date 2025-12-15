# PowerUps Design Document

This document outlines the planned PowerUps for Path of Ember, categorized by rarity.

## Common (60% Drop Rate) - Green

*   **Movement Speed**: Increases player movement speed by x1.1 (10%).
*   **Hazard Resistance**: (Placeholder) Implementation for resistance against environmental hazards (to be added later).
*   **1 Time Shield**: Blocks one instance of damage from a projectile or enemy. The shield is removed after use.
*   **Health Heal**: Restores HP to the player.
    *   *System Requirement*: Max HP = 1000. Enemies deal damage on hit.
*   **Freeze Potion**: Auto-consumed when close. Freezes nearby enemies in place (stops movement and animation).
*   **Venom Potion**: Auto-consumed when close. Deals Damage Over Time (DoT) to nearby enemies.
*   **Max HP+**: Increases player's Maximum HP by 10% (e.g., 1000 -> 1100).

## Rare (30% Drop Rate) - Blue

*   **Piercing**: Projectiles pass through the first enemy hit instead of destroying on impact.
*   **Bouncing Bullets**: Projectiles bounce off walls/environment (but not enemies) towards nearby targets.
*   **Freeze Shot**: Main weapon arrows freeze enemies for 1 second.
    *   *Cooldown*: An enemy cannot be frozen again for 5 seconds after being unfrozen.
*   **Venom Shot**: Main weapon arrows apply a Poison effect.
    *   *Damage*: ~100 HP per second.
    *   *Duration*: 3 seconds.
*   **Maximum Freeze**: Extends the duration of freeze effects (Shots or Potions/Meteors) to 2 seconds.
*   **Maximum Venom**: Increases Venom damage to ~200 HP per second.

## Legendary (10% Drop Rate) - Red

*   **Multishot**: Doubles the current projectile count.
    *   *Example*: If player has Triple Shot, this makes it shoot 2x3 = 6 projectiles (or fires the Triple Shot burst twice).
*   **Triple Shot**: Fires 3 projectiles in a spread (1 forward, 2 diagonal).
*   **Max HP+++**: Increases Maximum HP by 30%.
*   **Invulnerability Potion**: Spawns a potion that grants Invulnerability for 2 seconds.
    *   *Visual*: Player must have a visual effect indicating invulnerability.
*   **Attack Speed+**: Increases shooting tempo by +20%.

## Implementation Notes
*   **Health System**: Needs to be implemented first (Base 1000 HP).
*   **Status Effects**: Need a system to handle Freeze/Poison states on enemies.
*   **Stacking**: Logic for how multiple powerups stack (especially stat boosts like HP and Speed) needs to be defined in `PowerUpManager` when implemented.
