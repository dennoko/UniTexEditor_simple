# UniTexEditor - Usage Guide

A lightweight texture editing tool for Unity Editor, supporting color correction, compositing, and image processing.

## Features

- **Real-time preview**: Instantly visualize parameter changes
- **Multi-language support**: Switch between Japanese and English
- **GPU-accelerated**: Fast image processing via Compute Shaders
- **Non-destructive editing**: Original image is preserved throughout editing

## Installation

1. Place this folder under `Assets/Editor/` in your Unity project
2. Open the editor from the Unity menu: `dennokoworks > UniTex Editor`

## Basic Workflow

### 1. Open the editor

Select `dennokoworks > UniTex Editor` from the menu bar to open the editor window.

### 2. Load a texture

In the **Input** section, drag and drop the texture you want to edit into the "Source Texture" field.

### 3. Edit

Enable each section by checking its toggle in the section header, then adjust the parameters. Results are displayed in real time in the preview area.

### 4. Save

Click the **Apply & Save** button at the bottom of the window to save the result.

---

## UI Overview

### Header

The title bar at the top of the window. Use the **JA** / **EN** buttons in the upper-right corner to switch the display language.

### Preview Area

Displays a real-time preview of the current result. A checkerboard background indicates transparent areas. The texture resolution and format are shown in the lower-left corner of the preview.

- **Auto Update**: When enabled, the preview refreshes automatically on every parameter change.
- **Update**: Manually triggers a preview refresh.

### Settings Area

Contains all processing sections in a scrollable list. Sections with a toggle (the checkbox in the section header) are only applied to the processing pipeline when enabled; disabled sections are collapsed and skipped.

### Footer

Output destination settings and the Apply & Save / Reset All actions.

### Status Bar

Displays the current state at the very bottom of the window. Shows "Ready" when idle, a success message after saving, or an error message if something went wrong. Non-ready messages clear automatically after 5 seconds.

---

## Feature Reference

### Input

Always active — no toggle.

- **Source Texture**: The image to edit
- **Global Mask**: A mask to restrict the processing area (white = apply, black = skip)
  - **Invert Mask**: Swap white and black in the mask
  - **Mask Strength**: How strongly the mask is applied (0–1)
  - **Save Inverted**: Save the inverted mask as a separate PNG file

---

### Color Correction

Adjusts the overall color of the image.

- **Hue**: Rotate the hue (−180° to 180°)
- **Saturation**: Color vividness (0 = grayscale, 1 = original, 2 = double)
- **Brightness**: Lightness (0 = black, 1 = original, 2 = double)
- **Gamma**: Mid-tone brightness correction (0.1 to 3.0)
- **Color Blend**: Blend a solid color into the image
  - Target Color: The color to blend
  - Blend Mode: Compositing method (Normal, Multiply, Add, Screen, Overlay)
  - Opacity: Blend strength (0–1)

---

### Tone Curve

Fine-tune brightness and individual channels using curves. Each channel has its own enable toggle — only curves whose toggle is checked are applied.

- **RGB**: Curve applied to all channels uniformly (enabled by default)
- **Red / Green / Blue**: Per-channel curves (each independently toggleable)

---

### Blend

Composite another texture on top of the source.

- **Blend Texture**: The image to layer on top
- **Blend Mask**: A mask to restrict the compositing area
- **Blend Mode**: Compositing method
- **Opacity**: Blend strength (0–1)
- **Tiling**: Repeat the texture
- **Scale**: Scale the texture (X, Y)
- **Offset**: Shift the texture position (X, Y)

---

### Levels

Adjust input/output levels to control contrast.

- **Input Levels**: Min/Max slider to set the black point and white point of the image
- **Midtones (Gamma)**: Adjust the brightness of mid-range values (0.1–5.0)
- **Output Levels**: Min/Max slider to clamp the output range

---

### Sharpen / Blur

Apply edge enhancement or blur effects.

- **Mode**: Choose between Sharpen (edge enhancement) and Blur (softening).

#### Sharpen Mode

Uses Unsharp Mask to enhance edges.

| Parameter | Range | Description |
|---|---|---|
| Strength | 0–2 | Detail amplification per pass. 1.0 is standard sharpening; 2.0 is aggressive. |
| Kernel Size | 3–9 | Size of the reference blur. Larger values detect edges over a wider area. |
| Iterations | 1–8 | Number of times the pass is repeated. Each iteration further sharpens edges. 1–2 is usually sufficient. |

> **Note**: High strength or many iterations can produce halo artifacts (bright/dark fringes around edges).

#### Blur Mode

Applies a Gaussian blur to soften the image.

| Parameter | Range | Description |
|---|---|---|
| Strength | 0–5 | Gaussian sigma — directly controls the spread of the blur per pass. Higher values produce stronger blur. |
| Kernel Size | 3–9 | Sampling range. A larger kernel relative to the sigma improves quality. |
| Iterations | 1–8 | Number of passes. Repeating n times is equivalent to multiplying sigma by √n. |

> **Quick reference**: Light blur → Strength 0.5–1.0 / Iterations 1. Strong blur → Strength 2–3 / Iterations 3–5.

---

### Channel Mixer

Remap the contents of RGBA channels.

- **Red / Green / Blue / Alpha Output**: Select the source channel for each output channel

---

### Color Variation Generator (CVG)

Click the section header to expand. Automatically generates color variants from the current preview result.

- **Hue Steps**: Number of divisions around the hue wheel (1 to 36)
- **Saturation Steps**: Number of saturation levels (1 to 10)
- **Generate Grayscale**: Also generate a saturation-zero variant
- **Output**: Destination folder (defaults to the same folder as the source image)

Generated files are saved in a folder named `[source_name]_variations_[date]/`.

---

### Preset

Click the section header to expand. Save and recall parameter configurations as JSON files.  
Preset files are automatically stored in the `Assets/UniTexEditor_Presets/` folder.

#### Saving a Preset

1. Enter a name in the **Name** field
2. Select the **Type**:
   - **Parameters only**: Saves all numeric values, toggles, and curves — no texture references
   - **Include Textures**: Also saves references to the mask and blend textures (by GUID, so renames and moves within Unity are handled safely)
3. Click the **Save** button
   - If a preset with the same name already exists, a confirmation dialog will ask whether to overwrite it

> **Note**: The source texture is never included in a preset. When loading, whichever source texture is currently set remains unchanged.

#### Loading a Preset

1. Select a preset from the dropdown — the info line shows its type, save date, and how many sections are active
2. Click **Load**
   - For texture-inclusive presets, if any referenced texture cannot be found, a warning is shown but all other parameters are still applied

#### Deleting a Preset

Select a preset from the dropdown and click **Delete**. A confirmation dialog will appear before the file is removed.

> **↺ Button**: Manually refreshes the preset list — useful if you added files to the preset folder outside of Unity.

---

## Language

Click the **JA** / **EN** button in the upper-right corner of the editor window to switch between Japanese and English.

---

## Output Settings

Choose how to save the result at the bottom of the window.

- **OUTPUT: (AUTO)** (default): Saves a new file in the same folder as the source image with a timestamp appended (e.g. `texture_edited_260520_143022.png`). No existing files are overwritten.
- **Overwrite Source**: Overwrite the original file directly (**Warning**: this cannot be undone)
- **Select...**: Choose a specific output file path (available when Overwrite Source is off)

The output is always converted to the sRGB color space, ensuring correct display in standard image viewers.

---

## Tips

- **Auto-update**: Enable "Auto Update" in the preview area to refresh the preview automatically on every parameter change.
- **Reset Section**: Each enabled section has a "Reset" button in its header to restore that section's parameters to defaults.
- **Reset All**: The button at the bottom of the window resets every parameter to its initial state (requires confirmation).

---

## Requirements

- Unity 2022.3.22f1 or later
- Compute Shader-compatible GPU (DirectX 11, Metal, etc.)
