---
description: Workflow for adding new gameplay features
---

# Feature Implementation Workflow

This workflow describes the standard process for adding new mechanics or systems to Path of Ember.

1.  **Design & Research**
    - Create a markdown file in `Assets/Docs/` describing the feature (like `Archero_Movement_Research.md`).
    - Define core mechanics, references, and expected behavior.

2.  **Plan Implementation**
    - Create or update an `implementation_plan.md` artifact.
    - Identify scripts to modify (e.g., `PlayerMovement.cs`, `PlayerShooting.cs`).
    - Define new scripts needed.

3.  **Implementation Steps**
    - **Core Logic**: Write the base classes/managers locally.
    - **Integration**: Hook into existing Managers (ChunkManager, GameManager).
    - **Visuals**: Add placeholder assets if final ones aren't available.

4.  **Verification**
    - **Unit Test**: If applicable (mostly for logic like stats).
    - **Play Mode Test**: Run the game in Editor.
    - **Debug**: Use `Debug.Log` to verify internal states (e.g., "PowerUp Acquired: Multishot").
