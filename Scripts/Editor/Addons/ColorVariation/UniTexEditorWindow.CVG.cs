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
        private bool cvgGenGrayscale = false;
        private string cvgCustomOutputPath = "";
        
        // Preview
        private ComputeShader cvgShader;
        
        partial void DrawColorVariation()
        {
            DrawToggleSection(Localization.GetText("section_cvg"), ref showCVG, () => {
                
                GUILayout.Label(Localization.GetText("cvg_desc"), EditorStyles.wordWrappedLabel);
                EditorGUILayout.Space(5);
                
                // Variation Settings
                GUILayout.Label(Localization.GetText("cvg_settings"), EditorStyles.boldLabel);
                cvgHueCount = EditorGUILayout.IntSlider(Localization.GetContent("cvg_hue_steps"), cvgHueCount, 1, 36);
                cvgSatCount = EditorGUILayout.IntSlider(Localization.GetContent("cvg_sat_steps"), cvgSatCount, 1, 10);
                cvgGenGrayscale = EditorGUILayout.Toggle(Localization.GetContent("cvg_gen_grayscale"), cvgGenGrayscale);
                
                EditorGUILayout.Space(5);
                
                // Output
                GUILayout.Label(Localization.GetText("cvg_output"), EditorStyles.boldLabel);
                GUILayout.BeginHorizontal();
                string outputLabel = string.IsNullOrEmpty(cvgCustomOutputPath) ? "[Source]_variations_Date/" : $"...{Path.GetFileName(cvgCustomOutputPath)}";
                GUILayout.Label(string.Format(Localization.GetText("cvg_output_path"), outputLabel), EditorStyles.miniLabel);
                if (GUILayout.Button(Localization.GetContent("btn_select"), EditorStyles.miniButton, GUILayout.Width(60)))
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
                
                if (GUILayout.Button(Localization.GetContent("cvg_btn_generate"), actionButtonStyle))
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
            Texture2D baseTexture = null;
            try
            {
                // Re-setup processor to ensure full res processing
                processor.ClearNodes();
                processor.MaskTexture = maskTexture;
                processor.InvertMask = invertMask;
                processor.MaskStrength = maskStrength;
                
                if (showColorCorrection)
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
                int total = cvgHueCount * cvgSatCount;
                if (cvgGenGrayscale) total++; // Extra step for grayscale
                int current = 0;
                
                EditorUtility.DisplayProgressBar("Generating Variations", "Init...", 0f);
                
                // Use baseTexture (Linear) as source for variations
                RenderTexture baseRT = RenderTexture.GetTemporary(baseTexture.width, baseTexture.height, 0, RenderTextureFormat.ARGB32);
                baseRT.enableRandomWrite = true;
                baseRT.Create();
                Graphics.Blit(baseTexture, baseRT); // Copy Linear content
                
                RenderTexture varRT = RenderTexture.GetTemporary(baseTexture.width, baseTexture.height, 0, RenderTextureFormat.ARGB32);
                varRT.enableRandomWrite = true;
                varRT.Create();
                
                // Optional Grayscale Generation
                if (cvgGenGrayscale)
                {
                    EditorUtility.DisplayProgressBar("Generating Variations", "Grayscale Variation...", (float)current / total);
                    
                    // Saturation = 0, Hue = 0 (doesn't matter)
                    DispatchCVGShader(baseRT, varRT, Vector3.one, 0f, 0f);
                    
                    Texture2D varTex = TextureProcessor.RenderTextureToTexture2D(varRT);
                    Texture2D varTexSRGB = TextureProcessor.ConvertLinearToSRGB(varTex);
                    byte[] bytes = varTexSRGB.EncodeToPNG();
                    File.WriteAllBytes(Path.Combine(outputFolder, $"{fileName}_grayscale.png"), bytes);
                    
                    DestroyImmediate(varTex);
                    DestroyImmediate(varTexSRGB);
                    current++;
                }

                for (int h = 0; h < cvgHueCount; h++)
                {
                    for (int s = 0; s < cvgSatCount; s++)
                    {
                        float progress = (float)current / total;
                        EditorUtility.DisplayProgressBar("Generating Variations", $"Hue {h + 1}/{cvgHueCount} Sat {s + 1}/{cvgSatCount}", progress);
                        
                        float hueShift = (float)h / cvgHueCount; // 0..1
                        float satScale = (float)(s + 1) / cvgSatCount; 
                        
                        DispatchCVGShader(baseRT, varRT, Vector3.one, hueShift, satScale); // Identity RGB multiplier
                        
                        Texture2D varTex = TextureProcessor.RenderTextureToTexture2D(varRT);
                        Texture2D varTexSRGB = TextureProcessor.ConvertLinearToSRGB(varTex);
                        
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
