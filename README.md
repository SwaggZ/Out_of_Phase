# Out of Phase

A first-person 3D dimension-switching detective game built in Unity. You play as a cyberpunk police investigator tracking an interdimensional killer across five parallel dimensions.

## Overview

**Engine:** Unity 6000.3.8f1 (URP)  
**Input:** New Input System  
**Rendering:** Universal Render Pipeline  
**Genre:** First-Person Investigation / Puzzle

The player navigates through linear story sections, switching between five overlapping dimensions using a GTA5-style radial wheel. Each dimension reshapes the environment — same geometry, different reality. Certain areas lock or hide dimensions to control pacing and puzzle design.

### Dimensions

| # | Name | Theme |
|---|------|-------|
| 0 | Home | Futuristic / Cyberpunk |
| 1 | Ruined | Decayed Past |
| 2 | Overgrown | Nature-Reclaimed Future |
| 3 | Corrupted | Horror / Distorted |
| 4 | Abstract | Glitched / Surreal |

## Project Structure

```
Assets/Scripts/
├── Cinematic/          # Cinematic sequences & cutscene playback
├── Dialogue/           # Dialogue data & manager
├── Dimension/          # Core dimension system, audio, transition FX, wheel
├── Interaction/        # Interactable objects (doors, chests, pickups, dig spots)
├── Inventory/          # Inventory, hotbar, slots, UI
├── Items/              # Item definitions, tool actions (key, pickaxe, shovel, torch)
├── NPC/                # NPC controller & behaviour
├── Player/             # Movement, camera look, footsteps, held item animation
├── Progression/        # Sections, checkpoints, save/load, section gates
└── UI/                 # Menus, settings, crosshair, HUD elements
```

## Systems

### Player
- **PlayerMovement** — CharacterController-based FPS movement (walk, sprint, jump, crouch)
- **PlayerLook** — Mouse look with sensitivity settings
- **FootstepController** — Surface-aware walk/run/landing sounds
- **HeldItemAnimator** — Bob, sway, and tool-use animation for held items

### Dimension System
- **DimensionManager** — Singleton managing active dimension, transitions, lock/hide state
- **DimensionWheel** — Radial selector UI, dynamically rebuilds for visible dimensions
- **DimensionContainer** — Per-section component toggling dimension root GameObjects
- **DimensionTransitionEffect** — Glitch/scanline/chromatic transition with epilepsy-safe mode
- **DimensionLockVolume** — Trigger zone that greys out dimensions (ref-counted)
- **DimensionHideVolume** — Trigger zone that removes dimensions from the wheel entirely
- **DimensionSkybox** — Per-dimension skybox, fog, ambient light, and sun settings
- **DimensionAudioController** — Per-dimension ambient audio with crossfade
- **DimensionCooldownUI** — Visual cooldown indicator after switching
- **DimensionSyncGlitchEffect** — "Unstable sync" red overlay when forced into a dimension
- **DimensionObject** — Individual object visibility per dimension
- **AmbientOneShots** — Randomised ambient sound effects
- **SFXPlayer** — Global one-shot sound effect playback

### Inventory & Items
- **Inventory** — Slot-based inventory with stack support
- **HotbarController** — Quick-select bar with held item display
- **InventoryUI** — Full inventory screen
- **ItemDefinition** — ScriptableObject defining items (icon, stack size, durability, prefab)
- **KeyItemDefinition** — Extended item definition for key items
- **ToolAction** — Base class for tool behaviours (KeyAction, PickaxeAction, ShovelAction, TorchAction)

### Interaction
- **Interactor** — Raycast-based interaction system
- **InteractionPromptUI** — Context-sensitive interaction prompt
- **ItemPickup** — World pickup objects
- **ItemDropper** — Drop items into the world
- **DoorInteractable** — Openable doors (optionally locked)
- **ChestInteractable** — Lootable containers
- **DigSpot** — Shovel-targetable dig locations
- **MineableRock** — Pickaxe-targetable mining nodes
- **TorchPlacement** — Torch mounting points

### Progression
- **SectionManager** — Linear section progression, keeps N previous sections loaded
- **SectionGate** — Trigger volume advancing to next section
- **CheckpointManager** — Save/load via PlayerPrefs JSON (position, dimension, inventory, flags)
- **CheckpointTrigger** — Auto-save trigger volumes

### Cinematics & Dialogue
- **CinematicManager** — Cutscene playback with camera shots, letterbox, typewriter text
- **CinematicTrigger** — Trigger volume that starts a cinematic
- **CinematicData** — ScriptableObject defining shot sequences
- **DialogueManager** — Dialogue UI with speaker names, typewriter effect, input progression
- **DialogueData** — ScriptableObject defining dialogue lines

### NPC
- **NPCController** — NPC behaviour, look-at-player, dialogue triggers

### UI & Menus
- **PauseMenuUI** — Pause menu with resume/settings/quit
- **MainMenuUI** — Main menu with new game/continue/settings/quit
- **SettingsManager** — Persistent settings (volume, sensitivity, FOV, epilepsy mode)
- **SettingsUI** — Settings panel with sliders and toggles
- **CrosshairUI** — Dynamic crosshair
- **ItemNameDisplay** — Shows held item name on HUD
- **GameLoader** — Scene loading and game state initialisation

## Scene Setup

### Hierarchy
```
Root
├── Player                      (CharacterController + all player scripts)
│   └── PlayerCamera            (Camera + AudioListener)
├── DimensionManager            (singleton)
├── SectionManager              (singleton)
├── CheckpointManager           (singleton)
├── DialogueManager             (singleton)
├── CinematicManager            (singleton)
├── DimensionSkybox             (singleton)
├── Section_0                   (DimensionContainer)
│   ├── Shared                  (visible in all dimensions)
│   ├── Dim_0
│   ├── Dim_1
│   ├── Dim_2
│   ├── Dim_3
│   └── Dim_4
├── Section_1
│   ├── Shared
│   └── ...
└── ...
```

### Player GameObject Components
| Component | Purpose |
|-----------|---------|
| CharacterController | Physics & collision |
| PlayerMovement | FPS movement |
| PlayerLook | Mouse look |
| FootstepController | Walk/run/land SFX |
| HeldItemAnimator | Item bob & sway |
| Inventory | Item storage |
| HotbarController | Quick-select bar |
| InventoryUI | Inventory screen |
| Interactor | Raycast interaction |
| InteractionPromptUI | Prompt display |
| ItemDropper | Drop items |
| DimensionWheel | Dimension selector |
| DimensionTransitionEffect | Transition FX |
| DimensionCooldownUI | Cooldown display |
| CrosshairUI | Crosshair |
| ItemNameDisplay | Item name HUD |
| PauseMenuUI | Pause menu |

## Controls

| Action | Binding |
|--------|---------|
| Move | WASD |
| Look | Mouse |
| Jump | Space |
| Sprint | Left Shift |
| Crouch | Left Ctrl |
| Interact | E |
| Inventory | Tab |
| Hotbar | 1-5 / Scroll |
| Dimension Wheel | Hold Q |
| Pause | Escape |

## Requirements

- Unity 6000.3.8f1 or later
- Universal Render Pipeline
- Input System package
- TextMeshPro

## License

All rights reserved.
