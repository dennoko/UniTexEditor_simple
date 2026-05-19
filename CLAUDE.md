# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

**UniTexEditor_simple** is a non-destructive GPU-accelerated texture editing tool for the Unity Editor. Users can perform color correction, texture blending, sharpening, tone curve adjustments, and more directly within Unity, using Compute Shaders for all image processing.

Opened via **Tools > dennokoworks > UniTex Editor** in the Unity menu.

## Build & Test

This is a Unity Editor extension with no external build system. Compilation happens automatically via Unity's assembly definition system.

- **Assembly**: `Scripts/UniTexEditor.asmdef` — Editor-only, no external dependencies
- **Tests**: `Scripts/Tests/UniTexEditorTests.cs` — run via Unity's Test Runner (Window > General > Test Runner)
- **Requirements**: Unity 2020.3+, Compute Shader support (DX11 / Metal / Vulkan)

## Architecture

### Data Flow

```
User Input (EditorWindow UI)
  → Node list rebuilt (UniTexEditorWindow)
  → TextureProcessor.ProcessAll()
      → Each enabled ProcessingNode dispatches its Compute Shader
      → Ping-pong between RenderTextures (ARGBFloat)
  → Preview: 512×512 RenderTexture displayed in EditorWindow
  → Save: full-res processing, exported as PNG (Linear→sRGB)
```

### Key Components

**`Scripts/Core/TextureProcessor.cs`** — Pipeline orchestrator. Manages the ordered list of `ProcessingNode` instances, RenderTexture lifecycle (via `RenderTexture.GetTemporary()`), and Linear/sRGB color space conversion. Entry point: `ProcessAll()`.

**`Scripts/Editor/UniTexEditorWindow.cs`** (+ partials) — Main EditorWindow split into four partial classes:
- `UniTexEditorWindow.cs`: Window lifecycle, all parameter state fields
- `UniTexEditorWindow.GUI.cs`: `OnGUI()` rendering
- `UniTexEditorWindow.Logic.cs`: Preview updates, save logic
- `UniTexEditorWindow.Preset.cs`: Preset save/load

**`Scripts/Nodes/`** — One class per processing type, all inheriting `ProcessingNode`. Each node holds a reference to its Compute Shader and implements `Process(RenderTexture src, RenderTexture dst)`. Current nodes: `ColorCorrectionNode`, `BlendNode`, `SharpenNode`, `ToneCurveNode`, `LevelsNode`, `ChannelMixerNode`.

**`Resources/*.compute`** — GPU Compute Shaders, one per processing type. Loaded at runtime via `Resources.Load<ComputeShader>()`. All image math runs here; there is no CPU fallback.

**`Scripts/Core/Localization.cs`** — Loads `Resources/Localization/en.json` or `ja.json`, caches `GUIContent` objects, persists language choice in `EditorPrefs`.

**`Scripts/Editor/Addons/ColorVariation/`** — CVG addon: generates hue/saturation variations from the current preview and writes timestamped output folders.

### Extending with a New Processing Node

1. Add a new `*.compute` shader in `Resources/`
2. Create a new class in `Scripts/Nodes/` inheriting `ProcessingNode`
3. Add the node's parameters to `UniTexEditorWindow.cs` and wire up UI in `UniTexEditorWindow.GUI.cs`
4. Instantiate and add the node in `UniTexEditorWindow.Logic.cs` when rebuilding the pipeline

### Design Constraints

- **No CPU fallback**: All processing requires Compute Shader support.
- **ARGBFloat throughout**: RenderTextures use `RenderTextureFormat.ARGBFloat` to preserve precision; final PNG export converts to sRGB.
- **Non-destructive**: Original textures are never modified; edits are re-applied from the node list each preview/save cycle.
- **Editor-only**: The assembly definition excludes this code from runtime builds.

## Documentation

- `Docs/Impl/ARCHITECTURE.md` — System design and component responsibilities
- `Docs/Impl/ROADMAP.md` — Feature priorities (High/Medium/Low)
- `Docs/Impl/ImplementationPlan.md` — Refactoring phases
- `Docs/USAGE_jp.md` / `Docs/USAGE.md` — End-user manuals (JP/EN)
