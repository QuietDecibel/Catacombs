# Unity 3D Agent Playbook

This repository is currently a C++/Qt project.  
This document defines how I will operate as your Unity-focused coding agent for future tasks.

## What I can build for you in Unity

- Full 3D gameplay loops (player controller, combat, enemies, objectives, progression)
- Physics-driven interactions (Rigidbody, collisions, triggers, forces, constraints)
- Production-style UI systems (HUD, menus, settings, inventory, dialogue, overlays)
- Input + UI navigation for keyboard/mouse + gamepad (Unity Input System + EventSystem)
- Data-driven content (ScriptableObjects, balancing tables, prefabs, scene composition)
- Save/load, game states, pause flow, and scene transitions
- UI implementation from visual drafts (wireframes, screenshots, style boards)

## Default technical stack (unless you request otherwise)

- Unity LTS (latest stable LTS available in your environment)
- C# scripts in modular architecture
- New Input System
- UGUI for runtime HUD/menus (and UIToolkit if explicitly requested)
- Physics: 3D PhysX with fixed timestep tuning
- TextMeshPro for text rendering

## Build pipeline I will follow

1. **Spec intake**
   - Gather gameplay goals, target platform, control scheme, camera style, and reference visuals.
2. **Systems design**
   - Define scene graph, prefab boundaries, script responsibilities, and event flow.
3. **UI draft translation**
   - Convert visual draft into Canvas hierarchy + anchors + layout groups + prefab library.
   - Implement navigation graph for keyboard/gamepad (explicit selectable links when needed).
4. **Mechanics + physics implementation**
   - Build movement, interaction, combat, AI, win/lose conditions.
   - Set collision matrix, layers, tags, and tuning constants.
5. **Integration**
   - Connect UI to game state, data models, and input actions.
6. **Validation**
   - Play mode verification checklist for navigation, responsiveness, and physics stability.
7. **Iteration**
   - Tighten feel: frame pacing, input buffering, camera smoothing, UI readability.

## UI-from-visual-draft workflow

When you provide a UI draft image (or describe one), I will:

1. Identify screen type (main menu, HUD, inventory, modal, etc.).
2. Extract layout grid, spacing, hierarchy, and interaction hotspots.
3. Build a matching prefab structure:
   - Root Canvas
   - Panels/containers
   - Reusable controls (buttons, sliders, toggles, list items)
4. Wire navigation:
   - Default focus target per screen
   - Directional links for all selectable elements
   - Back/cancel behavior and modal focus traps
5. Bind to backing data + controller scripts.
6. Add responsive behavior for aspect ratios and safe areas.

## Navigation standards I will enforce

- Every interactable is reachable by keyboard/gamepad.
- Initial selected object is deterministic on screen open.
- No dead-ends or focus loss when opening/closing modals.
- Cancel always returns to expected prior state.
- Disabled options are skipped or handled intentionally.

## Physics/mechanics standards I will enforce

- Separate visual model from collider structure where useful.
- Use layers to control collision intent (player, enemy, projectile, world, UI raycasts).
- Avoid framerate-dependent gameplay logic (FixedUpdate for physics paths).
- Use clear config assets for tuning (move speed, jump, drag, gravity multipliers, etc.).
- Minimize hidden magic numbers in scripts.

## What to send me each time for best results

- Target Unity version (if fixed)
- Platform (PC/console/mobile/web)
- Input modes (KBM/gamepad/touch)
- Any visual draft assets (image links or files)
- Desired gameplay loop for the feature
- Acceptance criteria (what "done" means)

## Delivery format for implementation tasks

For each feature, I will return:

- Files created/edited
- Scene/prefab/script structure decisions
- Input actions + navigation map
- Test checklist and known edge cases
- Follow-up options for polish/performance

## Optional next step

If you want, I can start by generating a **Unity starter architecture spec** in this repo:

- Scene map (Boot, MainMenu, Gameplay, Pause, Settings)
- Core script namespaces and folder layout
- UI prefab catalog and naming conventions
- Input action asset design
- Physics/layer matrix plan
