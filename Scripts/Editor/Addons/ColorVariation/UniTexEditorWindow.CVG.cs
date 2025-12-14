using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;

namespace UniTexEditor
{
    public partial class UniTexEditorWindow
    {
        // CVG Fields
        private bool showCVG = false;
        private int cvgHueCount = 8;
        private int cvgSatCount = 3;
        private string cvgCustomOutputPath = "";
        
        // Preview
        private ComputeShader cvgShader;
        
        partial void DrawColorVariation()
        {
            DrawToggleSection("Color Variation Generator", ref showCVG, () => {
                
                GUILayout.Label("Generates variations based on the current preview result.", EditorStyles.wordWrappedLabel);
                EditorGUILayout.Space(5);
                
                // Variation Settings
                GUILayout.Label("Variation Settings", EditorStyles.boldLabel);
                cvgHueCount = EditorGUILayout.IntSlider("Hue Steps", cvgHueCount, 1, 36);
                cvgSatCount = EditorGUILayout.IntSlider("Saturation Steps", cvgSatCount, 1, 10);
                
                EditorGUILayout.Space(5);
                
                // Output
                GUILayout.Label("Output", EditorStyles.boldLabel);
                GUILayout.BeginHorizontal();
                GUILayout.Label(string.IsNullOrEmpty(cvgCustomOutputPath) ? "Output: [Source]_variations_Date/" : $"...{Path.GetFileName(cvgCustomOutputPath)}", EditorStyles.miniLabel);
                if (GUILayout.Button("Select...", EditorStyles.miniButton, GUILayout.Width(60)))
                {
                    string path = EditorUtility.OpenFolderPanel("Select Output Folder", "Assets", "");
                    if (!string.IsNullOrEmpty(path))
                    {
                        // Make relative
                        if (path.StartsWith(Application.dataPath))
                        {
                            cvgCustomOutputPath = "Assets" + path.Substring(Application.dataPath.Length);
                        }
                        else
                        {
                             cvgCustomOutputPath = path; // External path
                        }
                    }
                }
                GUILayout.EndHorizontal();
                
                EditorGUILayout.Space(10);
                
                if (GUILayout.Button("Generate Variations", actionButtonStyle))
                {
                    GenerateColorVariations();
                }
            });
        }
        
        private void GenerateColorVariations()
        {
            if (processor.SourceTexture == null)
            {
                EditorUtility.DisplayDialog("Error", "No source texture loaded.", "OK");
                return;
            }
            
            // 1. Get Base Image from Main Processor (Full Resolution, Linear)
            // Note: We need linear for processing, but GetResultAsTexture2D returns Linear. Perfect.
            Texture2D baseTexture = null;
            try
            {
                // Ensure nodes are up to date (reuse ApplyAndSave logic or simliar?)
                // Actually ApplyAndSave recreates nodes. We should do the same to be safe.
                // But simplified: assuming user hasn't changed hidden settings.
                // Better to call a "RebuildProcessor" method, but copying ApplyAndSave logic is safer.
                
                // Re-setup processor to ensure full res processing
                processor.ClearNodes();
                processor.MaskTexture = maskTexture;
                processor.InvertMask = invertMask;
                processor.MaskStrength = maskStrength;
                
                if (showColorCorrection) // Include new Mul params handled in Logic.cs logic
                {
                    var colorNode = new ColorCorrectionNode
                    {
                        hueShift = hueShift,
                        saturation = saturation,
                        brightness = brightness,
                        gamma = gamma,
                        targetColor = ccTargetColor,
                        blendMode = ccBlendMode,
                        blendOpacity = ccBlendOpacity
                    };
                    processor.AddNode(colorNode);
                }
                
                if (showBlend && blendTexture != null)
                {
                    var blendNode = new BlendNode
                    {
                        blendTexture = blendTexture,
                        blendMaskTexture = blendMaskTexture,
                        blendMode = blendMode,
                        blendStrength = blendStrength,
                        tiling = blendTiling,
                        scale = blendScale,
                        offset = blendOffset
                    };
                    processor.AddNode(blendNode);
                }
                
                if (showToneCurve)
                {
                     var toneCurveNode = new ToneCurveNode
                    {
                        rgbCurve = rgbCurve, redCurve = redCurve, greenCurve = greenCurve, blueCurve = blueCurve,
                        useRGBCurve = useRGBCurve, useRedCurve = useRedCurve, useGreenCurve = useGreenCurve, useBlueCurve = useBlueCurve
                    };
                    processor.AddNode(toneCurveNode);
                }

                if (showSharpen)
                {
                    var sharpenNode = new SharpenNode { mode = sharpenMode, strength = sharpenStrength, kernelSize = sharpenKernelSize };
                    processor.AddNode(sharpenNode);
                }
                
                if (showChannelMixer)
                {
                    var cmNode = new ChannelMixerNode { outRed = cmOutRed, outGreen = cmOutGreen, outBlue = cmOutBlue, outAlpha = cmOutAlpha };
                    processor.AddNode(cmNode);
                }
                
                if (showLevels)
                {
                    var lvlNode = new LevelsNode { minInput = lvlMinInput, maxInput = lvlMaxInput, minOutput = lvlMinOutput, maxOutput = lvlMaxOutput, midGamma = lvlMidGamma };
                    processor.AddNode(lvlNode);
                }
                
                // Get Full Res Result
                baseTexture = processor.GetResultAsTexture2D();
            }
            catch (System.Exception e)
            {
                Debug.LogError($"CVG Base Generation Failed: {e.Message}");
                return;
            }
            
            if (baseTexture == null) return;

            // 2. Prepare Output Directory
            string sourcePath = AssetDatabase.GetAssetPath(sourceTexture);
            string baseDir = string.IsNullOrEmpty(cvgCustomOutputPath) ? Path.GetDirectoryName(sourcePath) : cvgCustomOutputPath;
            if (string.IsNullOrEmpty(baseDir)) baseDir = "Assets";
            
            string fileName = Path.GetFileNameWithoutExtension(sourcePath);
            string dateStr = System.DateTime.Now.ToString("yyyyMMdd");
            string outputFolder = Path.Combine(baseDir, $"{fileName}_variations_{dateStr}");
            
            if (!Directory.Exists(outputFolder))
            {
                Directory.CreateDirectory(outputFolder);
            }
            
            // 3. Generate Variations
            // Prepare Shader
            if (cvgShader == null) cvgShader = Resources.Load<ComputeShader>("ColorVariation");
            if (cvgShader == null) { Debug.LogError("ColorVariation shader not found"); return; }
            
            try 
            {
                int total = 1 + (cvgHueCount * cvgSatCount);
                int current = 0;
                
                EditorUtility.DisplayProgressBar("Generating Variations", "Processing Base Image...", 0f);
                
                // Save Base Image (Already Processed)
                // Convert to sRGB for saving
                Texture2D baseSRGB = TextureProcessor.ConvertLinearToSRGB(baseTexture);
                byte[] baseBytes = baseSRGB.EncodeToPNG();
                File.WriteAllBytes(Path.Combine(outputFolder, $"{fileName}_base.png"), baseBytes);
                DestroyImmediate(baseSRGB);
                current++;
                
                // Use baseTexture (Linear) as source for variations
                RenderTexture baseRT = RenderTexture.GetTemporary(baseTexture.width, baseTexture.height, 0, RenderTextureFormat.ARGB32);
                baseRT.enableRandomWrite = true;
                baseRT.Create();
                Graphics.Blit(baseTexture, baseRT); // Copy Linear content
                
                RenderTexture varRT = RenderTexture.GetTemporary(baseTexture.width, baseTexture.height, 0, RenderTextureFormat.ARGB32);
                varRT.enableRandomWrite = true;
                varRT.Create();
                
                for (int h = 0; h < cvgHueCount; h++)
                {
                    for (int s = 0; s < cvgSatCount; s++)
                    {
                        float progress = (float)current / total;
                        EditorUtility.DisplayProgressBar("Generating Variations", $"Hue {h}/{cvgHueCount} Sat {s}/{cvgSatCount}", progress);
                        
                        float hueShift = (float)h / cvgHueCount; // 0..1
                        float satScale = (float)(s + 1) / cvgSatCount; 
                        
                        // Pass 2: HSV Shift
                        // RGB Adjust is disabled (Identity)
                        DispatchCVGShader(baseRT, varRT, Vector3.one, hueShift, satScale); // Vector3.one for Multiplicative Identity
                        
                        // Result varRT is Linear (because source was Linear and Shader logic preserves linear-ish logic for colors usually, or needs sRGB conversion depending on Space)
                        // Wait, Compute Shader usually works on values. If source is Linear, result is Linear.
                        // We need to convert to sRGB before saving.
                        
                        Texture2D varTex = TextureProcessor.RenderTextureToTexture2D(varRT); // Gets Linear
                        Texture2D varTexSRGB = TextureProcessor.ConvertLinearToSRGB(varTex); // Convert to sRGB
                        
                        byte[] bytes = varTexSRGB.EncodeToPNG();
                        File.WriteAllBytes(Path.Combine(outputFolder, $"{fileName}_h{h}_s{s}.png"), bytes);
                        
                        DestroyImmediate(varTex);
                        DestroyImmediate(varTexSRGB);
                        
                        current++;
                    }
                }
                
                RenderTexture.ReleaseTemporary(baseRT);
                RenderTexture.ReleaseTemporary(varRT);
                DestroyImmediate(baseTexture);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                AssetDatabase.Refresh();
            }
        }
        
        private void DispatchCVGShader(Texture source, RenderTexture dest, Vector3 rgbParams, float hueShift, float satScale)
        {
            if (cvgShader == null) cvgShader = Resources.Load<ComputeShader>("ColorVariation");
            if (cvgShader == null) return;
            
            int kernel = cvgShader.FindKernel("CSMain");
            cvgShader.SetTexture(kernel, "Source", source);
            cvgShader.SetTexture(kernel, "Result", dest);
            
            // Force Multiplicative Mode (1) but with Identity Params if we want "No Change" in RGB
            // Wait, previous logic: 0=Additive, 1=Multiplicative.
            // If I want Identity in Multiplicative: Params = 1,1,1.
            // If I want Identity in Additive: Params = 0,0,0.
            // I passed Vector3.one in Generate call above. So Mode should be 1 (Multiplicative).
            // OR I can use Additive (0) and Vector3.zero.
            // Let's use Multiplicative (1) separate logic to match previous.
            
            cvgShader.SetInt("RGBMode", 1); // Multiplicative
            cvgShader.SetFloats("RGBParams", rgbParams.x, rgbParams.y, rgbParams.z);
            
            cvgShader.SetFloat("HueShift", hueShift);
            cvgShader.SetFloat("SatScale", satScale);
            
            int threadX = Mathf.CeilToInt(dest.width / 8.0f);
            int threadY = Mathf.CeilToInt(dest.height / 8.0f);
            cvgShader.Dispatch(kernel, threadX, threadY, 1);
        }
        
        private void SaveRTPNG(RenderTexture rt, string path)
        {
            // Use existing utility if possible, but avoiding cross-thread issues or dependency if file loading might fail
            Texture2D tex = TextureProcessor.RenderTextureToTexture2D(rt);
            byte[] bytes = tex.EncodeToPNG();
            File.WriteAllBytes(path, bytes);
            DestroyImmediate(tex);
        }
    }
}
