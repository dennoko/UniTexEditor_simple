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

        private ComputeShader cvgShader;

        partial void DrawColorVariation()
        {
            DrawToggleSection(Localization.GetText("section_cvg"), ref showCVG, () =>
            {
                GUILayout.Label(Localization.GetText("cvg_desc"), EditorStyles.wordWrappedLabel);
                EditorGUILayout.Space(5);

                GUILayout.Label(Localization.GetText("cvg_settings"), EditorStyles.boldLabel);
                cvgHueCount     = EditorGUILayout.IntSlider(Localization.GetContent("cvg_hue_steps"),    cvgHueCount,    1,  36);
                cvgSatCount     = EditorGUILayout.IntSlider(Localization.GetContent("cvg_sat_steps"),    cvgSatCount,    1,  10);
                cvgGenGrayscale = EditorGUILayout.Toggle(Localization.GetContent("cvg_gen_grayscale"), cvgGenGrayscale);

                EditorGUILayout.Space(5);

                GUILayout.Label(Localization.GetText("cvg_output"), EditorStyles.boldLabel);
                GUILayout.BeginHorizontal();
                string outputLabel = string.IsNullOrEmpty(cvgCustomOutputPath)
                    ? "[Source]_variations_Date/"
                    : $"...{Path.GetFileName(cvgCustomOutputPath)}";
                GUILayout.Label(string.Format(Localization.GetText("cvg_output_path"), outputLabel), EditorStyles.miniLabel);
                if (GUILayout.Button(Localization.GetContent("btn_select"), EditorStyles.miniButton, GUILayout.Width(60)))
                {
                    string path = EditorUtility.OpenFolderPanel("Select Output Folder", "Assets", "");
                    if (!string.IsNullOrEmpty(path))
                    {
                        cvgCustomOutputPath = path.StartsWith(Application.dataPath)
                            ? "Assets" + path.Substring(Application.dataPath.Length)
                            : path;
                    }
                }
                GUILayout.EndHorizontal();

                EditorGUILayout.Space(10);

                if (GUILayout.Button(Localization.GetContent("cvg_btn_generate"), actionButtonStyle))
                    GenerateColorVariations();
            });
        }

        private void GenerateColorVariations()
        {
            if (processor.SourceTexture == null)
            {
                EditorUtility.DisplayDialog("Error", "No source texture loaded.", "OK");
                return;
            }

            // 1. ベースイメージをフル解像度・Linear 色空間で取得
            //    ConfigureProcessor() を使って UpdatePreview/ApplyAndSave と同一のパイプラインを適用する
            Texture2D baseTexture = null;
            try
            {
                ConfigureProcessor();
                baseTexture = processor.GetResultAsTexture2D();
            }
            catch (System.Exception e)
            {
                Debug.LogError($"CVG Base Generation Failed: {e.Message}");
                // 例外発生時も確実に解放
                if (baseTexture != null) DestroyImmediate(baseTexture);
                return;
            }

            if (baseTexture == null) return;

            // 2. 出力ディレクトリを準備
            string sourcePath = AssetDatabase.GetAssetPath(sourceTexture);
            string baseDir = string.IsNullOrEmpty(cvgCustomOutputPath)
                ? Path.GetDirectoryName(sourcePath)
                : cvgCustomOutputPath;
            if (string.IsNullOrEmpty(baseDir)) baseDir = "Assets";

            string fileName    = Path.GetFileNameWithoutExtension(sourcePath);
            string dateStr     = System.DateTime.Now.ToString("yyyyMMdd");
            string outputFolder = Path.Combine(baseDir, $"{fileName}_variations_{dateStr}");

            if (!Directory.Exists(outputFolder))
                Directory.CreateDirectory(outputFolder);

            // 3. バリエーションを生成
            if (cvgShader == null) cvgShader = Resources.Load<ComputeShader>("ColorVariation");
            if (cvgShader == null)
            {
                Debug.LogError("ColorVariation shader not found");
                DestroyImmediate(baseTexture);
                return;
            }

            try
            {
                int total   = cvgHueCount * cvgSatCount + (cvgGenGrayscale ? 1 : 0);
                int current = 0;

                EditorUtility.DisplayProgressBar("Generating Variations", "Init...", 0f);

                RenderTexture baseRT = RenderTexture.GetTemporary(baseTexture.width, baseTexture.height, 0, RenderTextureFormat.ARGB32);
                baseRT.enableRandomWrite = true;
                baseRT.Create();
                Graphics.Blit(baseTexture, baseRT);

                RenderTexture varRT = RenderTexture.GetTemporary(baseTexture.width, baseTexture.height, 0, RenderTextureFormat.ARGB32);
                varRT.enableRandomWrite = true;
                varRT.Create();

                if (cvgGenGrayscale)
                {
                    EditorUtility.DisplayProgressBar("Generating Variations", "Grayscale Variation...", (float)current / total);

                    DispatchCVGShader(baseRT, varRT, Vector3.one, 0f, 0f);

                    Texture2D varTex     = TextureProcessor.RenderTextureToTexture2D(varRT);
                    Texture2D varTexSRGB = TextureProcessor.ConvertLinearToSRGB(varTex);
                    File.WriteAllBytes(Path.Combine(outputFolder, $"{fileName}_grayscale.png"), varTexSRGB.EncodeToPNG());
                    DestroyImmediate(varTex);
                    DestroyImmediate(varTexSRGB);
                    current++;
                }

                for (int h = 0; h < cvgHueCount; h++)
                {
                    for (int s = 0; s < cvgSatCount; s++)
                    {
                        EditorUtility.DisplayProgressBar(
                            "Generating Variations",
                            $"Hue {h + 1}/{cvgHueCount} Sat {s + 1}/{cvgSatCount}",
                            (float)current / total);

                        float hueShiftNorm = (float)h / cvgHueCount;
                        float satScale     = (float)(s + 1) / cvgSatCount;

                        DispatchCVGShader(baseRT, varRT, Vector3.one, hueShiftNorm, satScale);

                        Texture2D varTex     = TextureProcessor.RenderTextureToTexture2D(varRT);
                        Texture2D varTexSRGB = TextureProcessor.ConvertLinearToSRGB(varTex);
                        File.WriteAllBytes(Path.Combine(outputFolder, $"{fileName}_h{h}_s{s}.png"), varTexSRGB.EncodeToPNG());
                        DestroyImmediate(varTex);
                        DestroyImmediate(varTexSRGB);
                        current++;
                    }
                }

                RenderTexture.ReleaseTemporary(baseRT);
                RenderTexture.ReleaseTemporary(varRT);
            }
            finally
            {
                // baseTexture は try ブロックの外で作られているため finally で解放を保証する
                if (baseTexture != null) DestroyImmediate(baseTexture);
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
            cvgShader.SetInt("RGBMode", 1); // Multiplicative
            cvgShader.SetFloats("RGBParams", rgbParams.x, rgbParams.y, rgbParams.z);
            cvgShader.SetFloat("HueShift", hueShift);
            cvgShader.SetFloat("SatScale", satScale);

            int threadX = Mathf.CeilToInt(dest.width  / 8.0f);
            int threadY = Mathf.CeilToInt(dest.height / 8.0f);
            cvgShader.Dispatch(kernel, threadX, threadY, 1);
        }
    }
}
