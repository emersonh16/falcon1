# Derelict Drifters - Unity Build Onboarding

## Project Overview

**Derelict Drifters** is a 2.5D isometric roguelike game built in **Unity 2022 LTS (URP)**. The player controls a derelict machine (walker) exploring a world covered in miasma, using a beam system to clear paths and survive.

### Core Vision
- **Isometric 2.5D view** (30° elevation, 45° azimuth orthographic camera)
- **Miasma system** - fog-like substance that covers the world
- **Beam system** - player's tool to clear miasma (bubble/cone/laser modes)
- **Cardinal movement** - WASD moves in screen-space directions
- **Event-driven architecture** - managers communicate via C# events

## Architecture

### Core Principles (Same as Godot build)

1. **Separation of Concerns**
   - **Managers** (singletons) = Logic/Data
   - **Renderers** (MonoBehaviours) = Visual representation
   - **Game Objects** = Player, enemies, etc.

2. **Event-Driven Communication**
   - Systems communicate via C# events/Actions
   - No direct references between systems
   - Managers emit events, renderers subscribe

3. **Single Source of Truth**
   - Each manager owns its data
   - Other systems access via public API

### Folder Structure

```
Assets/
├── Scripts/
│   ├── Managers/           # Singleton managers
│   │   ├── MiasmaManager.cs
│   │   ├── BeamManager.cs
│   │   ├── WindManager.cs
│   │   └── GameManager.cs
│   │
│   ├── Renderers/          # Visual representation
│   │   ├── MiasmaRenderer.cs
│   │   ├── BeamRenderer.cs
│   │   └── GroundRenderer.cs
│   │
│   ├── Derelict/           # Player character
│   │   └── DerelictController.cs
│   │
│   ├── Camera/             # Camera systems
│   │   └── IsometricCamera.cs
│   │
│   ├── Beam/               # Beam system components
│   │   └── BeamInput.cs
│   │
│   └── UI/                 # UI systems
│       └── FPSDisplay.cs
│
├── Prefabs/                # Reusable prefabs
├── Materials/              # Materials and shaders
├── Scenes/                 # Game scenes
└── Resources/              # Runtime-loaded assets
```

## Coordinate System

### World Coordinates (Unity)
- **X**: East/West (positive = East)
- **Y**: Up/Down (positive = Up)
- **Z**: North/South (positive = North)

### Layer Positions
- **Y = -0.5**: Ground tiles
- **Y = 0.0**: Derelict position (ground level)
- **Y = 0.01**: Beam visual (flat on ground)
- **Y = 0.01**: Miasma sheet (flat on ground)

### Camera Setup
- **Type**: Orthographic
- **Elevation**: 30° (X rotation)
- **Azimuth**: 45° (Y rotation)
- **Size**: Adjustable for zoom

### Screen Space → World Space (at 45° azimuth)
- **W (up screen)** → World: (-X, +Z) = Northwest
- **S (down screen)** → World: (+X, -Z) = Southeast
- **A (left screen)** → World: (-X, -Z) = Southwest
- **D (right screen)** → World: (+X, +Z) = Northeast

## Unity vs Godot Differences

| Concept | Godot | Unity |
|---------|-------|-------|
| Singletons | Autoload | Static instance pattern |
| Signals | `signal` keyword | C# `event Action` |
| Scene nodes | Node inheritance | MonoBehaviour components |
| Vector3i | Built-in | Use Vector3Int |
| _process() | Override | Update() |
| _ready() | Override | Start() / Awake() |

## Performance Strategy

For the miasma system (thousands of tiles):
1. **GPU Instancing** - Render many tiles with single draw call
2. **Graphics.DrawMeshInstanced** - Efficient batch rendering
3. **Compute Shaders** - GPU-side tile logic (if needed)
4. **Job System + Burst** - Multithreaded CPU work (if needed)

## Getting Started

1. Open project in Unity 2022 LTS
2. Open `Scenes/SampleScene`
3. Add IsometricCamera script to Main Camera
4. Press Play to test

## Current State

- [x] Project created (URP)
- [x] Folder structure set up
- [ ] Isometric camera
- [ ] Ground rendering
- [ ] Miasma system
- [ ] Beam system
- [ ] Player movement

---

**Engine:** Unity 2022 LTS (URP)
**Main Scene:** `Scenes/SampleScene`
