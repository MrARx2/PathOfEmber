# Path of Ember - Setup Guide

## Overview
This is a top-down 2D roguelite shooter where the player moves upward through procedurally generated chunks while avoiding a rising Hazard Zone.

## Unity Version
**Unity 6.2** - Make sure you're using this version for compatibility.

---

## 1. Player Setup

### Required Components on Player GameObject:
1. **Rigidbody** (3D Rigidbody, not Rigidbody2D)
   - Use Gravity: Automatically set to `false` by script
   - Constraints: Automatically set to freeze rotation and Z position by script

2. **PlayerMovement Script**
   - Move Speed: `5` (adjust for faster/slower movement)
   - Joystick: Assign your FloatingJoystick UI element (optional, auto-finds if not assigned)

3. **Collider** (BoxCollider, CapsuleCollider, or SphereCollider)
   - Set size to match your player model/sprite

### Controls:
- **Keyboard**: WASD or Arrow Keys
- **Mobile**: Touch anywhere to show floating joystick, drag to move

---

## 2. Chunk Manager Setup

### Create ChunkManager GameObject:
1. Create empty GameObject named "ChunkManager"
2. Add `ChunkManager` script

### Configure ChunkManager:
- **Player**: Drag your Player GameObject here
- **First Chunk Prefab**: Your starting chunk (can be different design)
- **Chunk Prefab**: Regular chunk used for all other chunks
- **Render Distance**: `2` (loads 2 chunks ahead and behind)
- **Chunk Width**: `10` (width of each chunk on X-axis)
- **Chunk Height**: `10` (height of each chunk on Y-axis - THIS IS THE PROGRESSION DIRECTION)
- **Chunk Unload Distance**: `20` (chunks beyond this distance get destroyed)

### How Chunks Work:
- Chunks spawn **upward** on the Y-axis (endless runner style)
- Player at Y=0 loads Chunk 0 (first chunk)
- Player at Y=10 loads Chunk 1
- Player at Y=20 loads Chunk 2, etc.
- Old chunks behind the player are automatically unloaded

### Creating Chunk Prefabs:
1. Create a GameObject for your chunk
2. Add visual elements (sprites, tilemaps, etc.)
3. Add colliders for walls/obstacles
4. Make it a prefab
5. Assign to ChunkManager

**Important**: Your chunk prefab should be designed to fit within the `chunkWidth` x `chunkHeight` dimensions.

---

## 3. Floating Joystick Setup (Mobile)

### UI Hierarchy:
```
Canvas (Screen Space - Overlay)
└── FloatingJoystick (Panel)
    ├── Background (Image) - The outer circle
    └── Handle (Image) - The inner draggable circle
```

### Setup Steps:
1. Create a Canvas (if you don't have one)
2. Create a Panel, name it "FloatingJoystick"
3. Add `FloatingJoystick` script to the Panel
4. Create child Image named "Background"
5. Create child Image inside Background named "Handle"
6. Assign Background and Handle in the FloatingJoystick inspector

### Configure FloatingJoystick:
- **Joystick Range**: `50` (how far the handle can move from center)
- **Dead Zone**: `0.1` (ignore small movements below this threshold)
- **Background**: Assign the Background RectTransform
- **Handle**: Assign the Handle RectTransform
- **Canvas Group**: Auto-assigned (used to show/hide joystick)

### How It Works:
- Touch anywhere on screen → joystick appears at touch position
- Drag to move player
- Release → joystick disappears
- Keyboard input takes priority over joystick when both are available

---

## 4. Input System Setup

### Install Input System Package:
1. Window → Package Manager
2. Search for "Input System"
3. Install

### Configure Player Settings:
1. Edit → Project Settings → Player
2. Under "Active Input Handling" select: **Input System Package (New)**
3. Restart Unity when prompted

**Note**: The code uses `Keyboard.current` from the new Input System, so you don't need to create Input Actions manually.

---

## 5. Tuning Parameters

### Movement Feel:
- **Move Speed** (PlayerMovement): Higher = faster movement (no rotation in top-down view)

### Chunk Spawning:
- **Chunk Height**: Distance between chunks (affects progression speed)
- **Render Distance**: How many chunks ahead to load (higher = more visible but more memory)
- **Chunk Unload Distance**: When to destroy old chunks (keep this reasonable to save memory)

### Joystick Feel:
- **Joystick Range**: Larger = more precise control but requires bigger finger movement
- **Dead Zone**: Larger = ignores small movements (good for preventing drift)

---

## 6. Common Issues & Solutions

### Player not moving:
- Check Rigidbody2D Gravity Scale is 0
- Check Player GameObject has PlayerMovement script
- Check Input System package is installed
- Try keyboard (WASD) to test if joystick is the issue

### Chunks spawning weird:
- Make sure Player is assigned in ChunkManager
- Check Chunk Height matches your chunk prefab size
- Verify chunk prefabs are assigned

### Joystick not appearing:
- Check Canvas is set to "Screen Space - Overlay"
- Verify Background and Handle are assigned
- Check CanvasGroup component exists on FloatingJoystick

### Input errors:
- Make sure "Active Input Handling" is set to "Input System Package (New)"
- Restart Unity after changing this setting

---

## 7. Testing Checklist

- [ ] Player spawns at Y=0
- [ ] First chunk loads at Y=0
- [ ] Player can move with WASD/Arrow keys
- [ ] Player can move with joystick (on mobile or touch screen)
- [ ] Player rotates to face movement direction
- [ ] New chunks load as player moves upward
- [ ] Old chunks unload when player moves away
- [ ] Joystick appears on touch and disappears on release

---

## 8. Next Steps

After basic movement and chunks work:
1. Add enemies to chunk prefabs
2. Implement Hazard Zone that rises from below
3. Add power-up system
4. Implement auto-fire when standing still
5. Add UI for health, score, time

---

## Code Structure

### PlayerMovement.cs
- **Purpose**: Handles player movement and rotation
- **Key Methods**:
  - `GetMovementInput()`: Checks joystick and keyboard for input
  - `MovePlayer()`: Applies movement and rotation
  - `ApplyKnockback()`: For Hazard Zone pushing player

### ChunkManager.cs
- **Purpose**: Spawns and manages chunks dynamically
- **Key Methods**:
  - `GetChunkPosition()`: Calculates which chunk player is in
  - `LoadChunk()`: Creates a new chunk
  - `UnloadChunk()`: Destroys a chunk
  - `UpdateChunks()`: Manages which chunks should be active

### FloatingJoystick.cs
- **Purpose**: Mobile touch input
- **Key Methods**:
  - `OnPointerDown()`: Touch started
  - `OnDrag()`: Touch moving
  - `OnPointerUp()`: Touch released

---

## Performance Tips

1. Keep `renderDistance` low (2-3 chunks)
2. Use object pooling for enemies/projectiles (not implemented yet)
3. Unload chunks aggressively to save memory
4. Use sprite atlases to reduce draw calls
5. Limit particle effects on mobile

---

Good luck with your game! 🔥
