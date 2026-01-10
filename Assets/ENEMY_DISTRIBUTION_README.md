# Enemy Distribution & XP System Reference

## GUID Reference
| Enemy | GUID |
|-------|------|
| Chaser | `875345555c5440b45b20a67898072601` |
| Bomber | `8e8cdaafd86fbd84b96d3d96c4433ca3` |
| Sniper | `1bc38cafcc1faf446a0b0b791e1af79b` |

---

## Enemy Distribution Per Chunk (Sorted by Index)

| # | Chunk | Chasers | Bombers | Snipers | MiniBoss | Total |
|---|-------|---------|---------|---------|----------|-------|
| 01 | FinalMud01 | 3 | 0 | 0 | 0 | 3 |
| 02 | FinalMud02 | 5 | 0 | 2 | 0 | 7 |
| 03 | FinalMud03 | 5 | 1 | 2 | 0 | 8 |
| 04 | FinalMud04 | 3 | 3 | 2 | 0 | 8 |
| 05 | FinalGrass05 | 4 | 4 | 2 | 0 | 10 |
| 06 | FinalGrass06 | 7 | 1 | 2 | 0 | 10 |
| 07 | FinalGrass07 | 4 | 3 | 3 | 0 | 10 |
| 08 | FinalGrass08 | 4 | 4 | 2 | 0 | 10 |
| 09 | FinalGrass09 | 0 | 4 | 7 | 0 | 11 |
| 10 | FinalMud10 | 6 | 1 | 3 | 0 | 10 |
| 11 | FinalMud11 | 0 | 0 | 0 | 1 | 1 |
| 12 | FinalLava12 | 8 | 2 | 2 | 0 | 12 |
| 13 | FinalLava13 | 3 | 6 | 4 | 0 | 13 |
| 14 | FinalLava14 | 5 | 2 | 2 | 0 | 9 |
| 15 | FinalLava15 | 6 | 3 | 2 | 0 | 11 |

---

## Summary Statistics

| Enemy Type | Total Count | Percentage |
|------------|-------------|------------|
| **Chaser** | 63 | 47.4% |
| **Bomber** | 34 | 25.6% |
| **Sniper** | 35 | 26.3% |
| **MiniBoss** | 1 | 0.8% |
| **TOTAL** | **133** | 100% |

### Chunks: 15
### Average Enemies per Chunk: 8.9

---

## Zone Breakdown

### Mud Zone (01-04, 10-11)
| Stat | Value |
|------|-------|
| Total Enemies | 37 |
| Chasers | 22 (59%) |
| Bombers | 5 (14%) |
| Snipers | 9 (24%) |
| MiniBoss | 1 (3%) |

### Grass Zone (05-09)
| Stat | Value |
|------|-------|
| Total Enemies | 51 |
| Chasers | 19 (37%) |
| Bombers | 16 (31%) |
| Snipers | 16 (31%) |

### Lava Zone (12-15)
| Stat | Value |
|------|-------|
| Total Enemies | 45 |
| Chasers | 22 (49%) |
| Bombers | 13 (29%) |
| Snipers | 10 (22%) |

---

## Notes
- FinalGrass09 is sniper-heavy (7 snipers, no chasers)
- FinalLava13 has the most bombers (6)
- FinalLava12 has the most chasers (8)
- FinalMud11 is MiniBoss-only encounter
- Difficulty ramps up through zones (3 → 7-10 → 9-13 enemies)

*Last Updated: 2026-01-10*
