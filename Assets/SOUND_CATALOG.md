# Path of Ember - Sound Catalog

Complete catalog of all game sounds organized by trigger/action.

---

## 🎵 Sound Groups (Random Variants)

These sounds have multiple variants. The system will pick one randomly each time.

| Group Name | Trigger/Action | Clips | Location |
|------------|----------------|-------|----------|
| `PlayerHurt` | Player takes damage | Hurt1, Hurt2, Hurt3 | `Sounds/Player/` |
| `CoinPickup` | Player collects coin/currency | Coin1, Coin2, Coin3 | `Sounds/Player/` |
| `EnemyHit` | Arrow hits enemy | EnemyHit1, EnemyHit2, EnemyHit3 | `Sounds/Player/` |
| `FreezeApply` | Freeze effect applied | Freeze1, Freeze2, Freeze3 | `Sounds/Player/` |
| `VenomApply` | Venom/poison effect applied | Venom1, Venom2, Venom3 | `Sounds/Player/` |
| `ArrowWallHit` | Arrow hits wall/obstacle | WallHit1, WallHit2, WallHit3 | `Sounds/Player/` |

---

## 🏹 Player Sounds (Single)

| Sound Name | Trigger/Action | File |
|------------|----------------|------|
| `BowShot` | Player fires arrow | `BowShot.wav` |
| `Step` | Player footstep | `Step.wav` |
| `Heal` | Player heals | `Heal.wav` |
| `FireHurt` | Player takes fire damage tick | `FireHurt.wav` |
| `InvulnerabilityState` | Shield/invulnerability active | `InvulnerabiilityState.wav` |
| `PotionPickup` | Player picks up potion | `PotionPickUp.wav` |
| `PotionFall` | Potion drops | `PotionFall.wav` |
| `XPUp` | Player levels up | `XPUp.wav` |
| `PlayerDie` | Player death | `POE PlayerDie.wav` |

---

## 👹 Enemy Sounds

### Chaser
| Sound Name | Trigger/Action | File |
|------------|----------------|------|
| `ChaserDie` | Chaser death | `POE Chaser Die.wav` |
| `ChaserDistinct` | Chaser nearby alert | `POE Chaser Distinct.wav` |

### Bomber
| Sound Name | Trigger/Action | File |
|------------|----------------|------|
| `BomberBip` | Bomber countdown beep | `BomberBip.wav` |
| `BomberBoom` | Bomber explosion | `BomberBoom.wav` |
| `BomberDie` | Bomber death (no explosion) | `BomberDie.wav` |
| `BomberDistinct` | Bomber nearby alert | `BomberDistinct.wav` |

### Sniper
| Sound Name | Trigger/Action | File |
|------------|----------------|------|
| `SniperAim` | Sniper aiming | `POE Sniper Aim.wav` |
| `SniperShoot` | Sniper fires | `POE Sniper Shoot.wav` |
| `SniperDie` | Sniper death | `POE Sniper Die.wav` |
| `SniperDistinct` | Sniper nearby alert | `POE Sniper Distinct.wav` |

### MiniBoss
| Sound Name | Trigger/Action | File |
|------------|----------------|------|
| `MiniBossDie` | MiniBoss death | `POE MiniBossDie.wav` |
| `MiniBossFireBallCall` | MiniBoss summons fireball | `POE MiniBossFireBallCall.wav` |
| `MiniBossMeteorCall` | MiniBoss summons meteor | `POE MiniBossMeteorCall.wav` |
| `MiniBossRageBlast` | MiniBoss rage attack | `POE MiniBossRageBlast.wav` |

### Titan (Boss)
| Sound Name | Trigger/Action | File |
|------------|----------------|------|
| `TitanDie` | Titan death | `TitanDie.wav` |
| `TitanFist` | Titan slam attack | `TitanFist.wav` |
| `TitanMeteorBlast` | Titan meteor attack | `TitanMeteorBlast.wav` |
| `TitanRage` | Titan enrage | `TitanRage.wav` |
| `TitanSummon` | Titan summons enemies | `TitanSummon.wav` |
| `TitanSummonCrunch` | Summon impact | `TitanSummonCrunch.wav` |

---

## ☄️ Hazard Sounds

| Sound Name | Trigger/Action | File |
|------------|----------------|------|
| `MeteorFall` | Meteor falling (warning) | `MeteorFall.wav` |
| `MeteorBoom` | Meteor impact | `MeteorBoom.wav` |
| `SpawnPuff` | Enemy spawn effect | `SpawnPuff.wav` |

---

## 🎰 Prayer Wheel (Talent Selection)

| Sound Name | Trigger/Action | File |
|------------|----------------|------|
| `PrayerWheelClick` | Click/select | `Click.wav` |
| `PrayerWheelSpin` | Wheel spinning | `POE PrayerWheelSpin.wav` |
| `PrayerWheelEnd` | Wheel stops | `POE PrayerWheelEnd.wav` |
| `PrayerWheelCommon` | Common talent selected | `POE PrayerWheelCommon.wav` |
| `PrayerWheelRare` | Rare talent selected | `POE PrayerWheelRare.wav` |
| `PrayerWheelLegendary` | Legendary talent selected | `POE PrayerWheelLegandary.wav` |

---

## 🌊 Ambient Sounds (Looping)

| Sound Name | Trigger/Action | File |
|------------|----------------|------|
| `AmbientFire` | Fire crackling | `Fire.wav` |
| `AmbientLava` | Lava bubbling | `LavaFlow.wav` |
| `AmbientRiver` | Water flowing | `RiverFlow.wav` |
| `AmbientWind` | Wind blowing | `Wind.wav` |

---

## 🎶 Music

| Sound Name | Trigger/Action | File |
|------------|----------------|------|
| `MenuTrack` | Main menu BGM | `MenuTrack.wav` |

---

## Integration Checklist

- [ ] Player: BowShot on fire
- [ ] Player: Step on footstep
- [ ] Player: PlayerHurt on damage (random variant)
- [ ] Player: FireHurt on fire damage tick
- [ ] Player: Heal on heal
- [ ] Player: PlayerDie on death
- [ ] Enemy: Hit sounds on arrow impact (random variant)
- [ ] Enemy: Death sounds per enemy type
- [ ] Hazard: MeteorFall + MeteorBoom
- [ ] UI: PrayerWheel sounds
- [ ] Ambient: Zone-based ambient loops
- [ ] Music: Menu track
