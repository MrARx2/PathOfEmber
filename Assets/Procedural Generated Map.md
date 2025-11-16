# Procedural Generated Map

This document explains how the ChunkManager builds and streams the fixed chunk sequence.

## Sequence
- First: `firstChunkPrefab` (index 0)
- Lava biome: `lavaBiomePrefabs[0..3]` in order (1 → 4)
- Grass biome: `grassBiomePrefabs[0..3]` in order (1 → 4)
- Mud biome: `mudBiomePrefabs[0..3]` in order (1 → 4)
- Last: `lastChunkPrefab` (final boss)

The world is finite. After the last chunk, no more chunks are spawned.

## Placement and Alignment
- Origin: `ChunkManager.transform.position`.
- Chunk i position: `origin + Vector3.forward * i * (chunkLength + chunkGap)`.
- All prefabs (including first and last) must share the same Z length (`chunkLength`) to fit seamlessly.
- `chunkGap` may be 0 for no spacing.

## Inspector Setup
- References
  - `player`: Player Transform
  - `firstChunkPrefab`
  - `lastChunkPrefab`
- Biome Prefabs (arrays of size 4)
  - `lavaBiomePrefabs[0..3]`
  - `grassBiomePrefabs[0..3]`
  - `mudBiomePrefabs[0..3]`
- Chunk Settings
  - `chunkLength`: Z-length of chunks
  - `chunkGap`: spacing between chunks (optional)
  - `chunksAhead`, `chunksBehind`: streaming window

## Streaming
- The manager keeps `chunksBehind` before and `chunksAhead` after the current player chunk loaded.
- Unused chunks outside this window are unloaded.

## Notes
- No debug material coloring is applied. Prefabs use their original materials.
- If a biome array is empty or has null entries, that slot is skipped and a log is printed.
- Logs print each loaded chunk index, name, and Z position to help diagnose placement.
