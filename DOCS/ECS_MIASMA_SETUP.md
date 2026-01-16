# ECS/DOTS Miasma System Setup Guide

## Overview

We're migrating the miasma system to Unity's ECS/DOTS (Entity Component System) for maximum performance. This is Unity's modern, best-practice approach for high-performance systems.

## What is ECS/DOTS?

- **Entities**: Game objects (just IDs, no data)
- **Components**: Data only (no logic)
- **Systems**: Logic that processes entities with specific components
- **Jobs**: Multithreaded work that runs on all CPU cores
- **Burst**: Compiles to native code for maximum speed

## Benefits for Miasma System

1. **Multithreaded**: Updates run on all CPU cores simultaneously
2. **No Jitter**: Smooth updates, no frame spikes
3. **Scalable**: Can handle millions of tiles
4. **Cache-Friendly**: Data organized for CPU efficiency
5. **Future-Proof**: Unity's direction for performance

## Setup Steps

### 1. Install Packages (Automatic)

The packages have been added to `manifest.json`. Unity will download them automatically:
- `com.unity.entities` - Core ECS system
- `com.unity.collections` - Native collections
- `com.unity.jobs` - Job system
- `com.unity.burst` - Native code compilation
- `com.unity.mathematics` - Math library

**Wait for Unity to finish importing packages** (check bottom-right progress bar).

### 2. Enable Burst Compiler

- Go to **Edit → Project Settings → Player**
- Under **Other Settings**, find **Scripting Backend**
- Set to **IL2CPP** (required for Burst)
- **API Compatibility Level**: .NET Standard 2.1

### 3. Create ECS World

The `MiasmaECSManager` will create the ECS world automatically when you add it to a GameObject.

### 4. Integration with Existing System

The ECS system will work alongside your existing `MiasmaManager`:
- `MiasmaManager` → Handles game logic (clearing, regrowth)
- `MiasmaECSManager` → Manages ECS world and entities
- `MiasmaRenderingSystem` → Renders tiles (replaces MiasmaRenderer)
- `MiasmaClearingSystem` → Handles clearing (called from MiasmaManager)

## Architecture

```
Old System (MonoBehaviour):
MiasmaManager → Dictionary<Vector2Int, float> → MiasmaRenderer → GPU Instancing

New System (ECS):
MiasmaManager → MiasmaECSManager → Entities (Components) → Systems (Jobs) → Rendering
```

## Migration Path

1. **Phase 1**: ECS system runs alongside old system (both active)
2. **Phase 2**: Gradually move logic to ECS systems
3. **Phase 3**: Remove old MiasmaRenderer, use only ECS

## Files Created

- `Assets/Scripts/ECS/Miasma/Components/MiasmaTileComponent.cs` - Component data
- `Assets/Scripts/ECS/Miasma/Systems/MiasmaClearingSystem.cs` - Clearing logic
- `Assets/Scripts/ECS/Miasma/Systems/MiasmaRenderingSystem.cs` - Rendering
- `Assets/Scripts/ECS/Miasma/MiasmaECSManager.cs` - Bridge/manager

## Next Steps

1. Wait for packages to import
2. Add `MiasmaECSManager` to a GameObject in your scene
3. Test that entities are created
4. Gradually migrate logic from old system

## Troubleshooting

**If packages don't install:**
- Check Unity version (need 2021.3+)
- Try manually adding via Package Manager

**If compilation errors:**
- Make sure Burst is enabled
- Check that all packages imported successfully

**If nothing renders:**
- Check that entities are being created
- Verify MiasmaRenderingSystem is running
- Check Console for errors
