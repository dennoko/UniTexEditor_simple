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

Enable each section and adjust its parameters. Results are displayed in real time in the preview area.

### 4. Save

Click the **Apply & Save** button at the bottom of the window to save the result.

---

## Feature Reference

### Input

- **Source Texture**: The image to edit
- **Global Mask**: A mask to restrict the processing area (white = apply, black = skip)
  - Invert Mask: Swap the white and black of the mask
  - Mask Strength: Controls how strongly the mask is applied
  - Save Inverted: Save the inverted mask as a separate file

---

### Color Correction

Adjusts the overall color of the image.

- **Hue**: Rotate the hue (-180° to 180°)
- **Saturation**: Color vividness (0 = grayscale, 1 = original, 2 = double)
- **Brightness**: Lightness (0 = black, 1 = original, 2 = double)
- **Gamma**: Mid-tone brightness correction (0.1 to 3.0)
- **Color Blend**: Blend a solid color into the image
  - Target Color: The color to blend
  - Blend Mode: Compositing method (Normal, Multiply, Add, Screen, Overlay)
  - Opacity: Blend strength

---

### Tone Curve

Fine-tune brightness and individual channels using curves.

- **RGB**: Curve applied to all channels uniformly
- **Red / Green / Blue**: Per-channel curves

---

### Blend

Composite another texture on top of the source.

- **Blend Texture**: The image to layer on top
- **Blend Mask**: A mask to restrict the compositing area
- **Blend Mode**: Compositing method
- **Opacity**: Blend strength
- **Tiling**: Repeat the texture
- **Scale**: Scale the texture
- **Offset**: Shift the texture position

---

### Levels

Adjust input/output levels to control contrast.

- **Input Levels**: Set the black point and white point of the image
- **Midtones (Gamma)**: Adjust the brightness of mid-range values
- **Output Levels**: Clamp the output range

---

### Sharpen / Blur

Apply edge enhancement or blur effects.

- **Mode**: Choose between Sharpen (edge enhancement) and Blur (softening).

#### Sharpen Mode

Uses Unsharp Mask to enhance edges.

| Parameter | Range | Description |
|---|---|---|
| Strength | 0 ~ 2 | Detail amplification per pass. 1.0 is standard sharpening; 2.0 is aggressive. |
| Kernel Size | 3 / 5 / 7 / 9 | Size of the reference blur. Larger values detect edges over a wider area. |
| Iterations | 1 ~ 8 | Number of times the pass is repeated. Each iteration further sharpens edges. 1–2 is usually sufficient. |

> **Note**: High strength or many iterations can produce halo artifacts (bright/dark fringes around edges).

#### Blur Mode

Applies a Gaussian blur to soften the image.

| Parameter | Range | Description |
|---|---|---|
| Strength | 0 ~ 5 | Gaussian sigma — directly controls the spread of the blur per pass. Higher values produce stronger blur. |
| Kernel Size | 3 / 5 / 7 / 9 | Sampling range. A larger kernel relative to the sigma improves quality. |
| Iterations | 1 ~ 8 | Number of passes. Repeating n times is equivalent to multiplying sigma by √n. |

> **Quick reference**: Light blur → Strength 0.5–1.0 / Iterations 1. Strong blur → Strength 2–3 / Iterations 3–5.

---

### Channel Mixer

Remap the contents of RGBA channels.

- **Red / Green / Blue / Alpha Output**: Select the source channel for each output channel

---

### Preset

Save and recall parameter configurations as JSON files.  
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

### Color Variation Generator (CVG)

Automatically generates color variants from the current preview result.

- **Hue Steps**: Number of divisions around the hue wheel (1 to 36)
- **Saturation Steps**: Number of saturation levels (1 to 10)
- **Generate Grayscale**: Also generate a saturation-zero variant
- **Output**: Destination folder (defaults to the same folder as the source image)

Generated files are saved in a folder named `[source_name]_variations_[date]/`.

---

## Language

Click the **JA** / **EN** button in the upper-right corner of the editor window to switch between Japanese and English.

---

## Output Settings

Choose how to save the result at the bottom of the window.

- **Overwrite Source**: Overwrite the original file directly (**Warning**: this cannot be undone)
- **Select...**: Choose a new file path to save as a separate file

The output is always converted to the sRGB color space, ensuring correct display in standard image viewers.

---

## Tips

- **Auto-update**: Enable "Auto Update" in the preview area to refresh the preview automatically on every parameter change.
- **Reset Section**: Each section has a "Reset" button to restore its parameters to defaults.
- **Reset All**: The button at the bottom of the window resets every parameter to its initial state.

---

## Requirements

- Unity 2022.3.22f1 or later
- Compute Shader-compatible GPU (DirectX 11, Metal, etc.)
