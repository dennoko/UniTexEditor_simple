using UnityEngine;
using UnityEditor;
using System.IO;

namespace UniTexEditor
{
    public partial class UniTexEditorWindow
    {
        private GUIStyle boxStyle;
        private GUIStyle titleStyle;
        
        private void InitializeStyles()
        {
            if (boxStyle == null)
            {
                boxStyle = new GUIStyle(EditorStyles.helpBox);
                boxStyle.padding = new RectOffset(10, 10, 10, 10);
                boxStyle.margin = new RectOffset(0, 0, 5, 5);
            }
            
            if (titleStyle == null)
            {
                titleStyle = new GUIStyle(EditorStyles.boldLabel);
                titleStyle.fontSize = 12;
                titleStyle.margin = new RectOffset(0, 0, 0, 5);
            }
        }

        private void OnGUI()
        {
            InitializeStyles();
            
            // ヘッダー
            DrawHeader();
            
            // プレビューエリア（上部固定、リロード不要な場合に最適化）
            DrawPreviewArea();
            
            // 設定エリア（スクロール可能）
            // プレビューエリアとフッターの間の領域を埋める
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            DrawSettingsArea();
            EditorGUILayout.EndScrollView();
            
            // フッター（アクションボタン）
            DrawFooter();
        }
        
        private void DrawHeader()
        {
            EditorGUILayout.Space(5);
            GUILayout.Label("UniTex Editor", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);
        }
        
        private void DrawPreviewArea()
        {
            GUILayout.BeginVertical(EditorStyles.helpBox);
            
            // ツールバー
            GUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label("Preview", EditorStyles.miniLabel);
            GUILayout.FlexibleSpace();
            
            bool newAutoPreview = GUILayout.Toggle(autoPreview, "Auto Update", EditorStyles.toolbarButton);
            if (newAutoPreview != autoPreview)
            {
                autoPreview = newAutoPreview;
                if (autoPreview) RequestPreviewUpdate(true);
            }
            
            if (GUILayout.Button("Update", EditorStyles.toolbarButton))
            {
                UpdatePreview();
            }
            GUILayout.EndHorizontal();

            // プレビュー画像表示
            // 固定高さを確保して表示（画面サイズに応じて調整可能だが、今回は固定高さを確保）
            float previewHeight = Mathf.Min(position.height * 0.4f, 400f);
            Rect previewRect = GUILayoutUtility.GetRect(0, previewHeight, GUILayout.ExpandWidth(true));
            
            if (resultPreview != null)
            {
                // 背景（チェッカーボード）
                if (Event.current.type == EventType.Repaint)
                {
                    DrawCheckerboard(previewRect);
                }
                
                // 画像描画
                // アスペクト比を維持して中央表示
                float textureAspect = (float)resultPreview.width / resultPreview.height;
                float rectAspect = previewRect.width / previewRect.height;
                
                Rect drawRect = previewRect;
                if (textureAspect > rectAspect)
                {
                    // 横に合わせる
                    float h = previewRect.width / textureAspect;
                    drawRect.y += (previewRect.height - h) * 0.5f;
                    drawRect.height = h;
                }
                else
                {
                    // 縦に合わせる
                    float w = previewRect.height * textureAspect;
                    drawRect.x += (previewRect.width - w) * 0.5f;
                    drawRect.width = w;
                }
                
                GUI.DrawTexture(drawRect, resultPreview, ScaleMode.ScaleToFit, true);
                
                // 解像度情報
                GUI.Label(new Rect(previewRect.x + 5, previewRect.y + previewHeight - 20, 200, 20), 
                    $"{resultPreview.width}x{resultPreview.height} ({resultPreview.format})", EditorStyles.miniLabel);
            }
            else
            {
                // プレビューなしの場合のプレースホルダー
                GUI.Box(previewRect, sourceTexture != null ? "Generating Preview..." : "No Source Texture", EditorStyles.centeredGreyMiniLabel);
            }
            
            GUILayout.EndVertical();
        }
        
        private void DrawSettingsArea()
        {
            GUILayout.BeginVertical();
            
            // --- Input Section ---
            DrawSection("Input Settings", () => {
                EditorGUI.BeginChangeCheck();
                sourceTexture = (Texture2D)EditorGUILayout.ObjectField("Source Texture", sourceTexture, typeof(Texture2D), false);
                if (EditorGUI.EndChangeCheck())
                {
                    processor.SourceTexture = sourceTexture;
                    if (sourceTexture != null && string.IsNullOrEmpty(customOutputPath))
                    {
                        UpdateDefaultOutputPath();
                    }
                    RequestPreviewUpdate();
                }
                
                // Mask
                EditorGUILayout.Space(5);
                EditorGUI.BeginChangeCheck();
                maskTexture = (Texture2D)EditorGUILayout.ObjectField("Mask Texture", maskTexture, typeof(Texture2D), false);
                if (EditorGUI.EndChangeCheck())
                {
                    processor.MaskTexture = maskTexture;
                    RequestPreviewUpdate();
                }
                
                if (maskTexture != null)
                {
                    EditorGUI.indentLevel++;
                    invertMask = EditorGUILayout.Toggle("Invert Mask", invertMask);
                    maskStrength = EditorGUILayout.Slider("Mask Strength", maskStrength, 0f, 1f);
                    EditorGUI.indentLevel--;
                }
            });
            
            // --- Color Correction ---
            DrawToggleSection("Color Correction", ref showColorCorrection, () => {
                EditorGUI.BeginChangeCheck();
                
                hueShift = EditorGUILayout.Slider("Hue Shift", hueShift, -180f, 180f);
                saturation = EditorGUILayout.Slider("Saturation", saturation, 0f, 2f);
                brightness = EditorGUILayout.Slider("Brightness", brightness, 0f, 2f);
                gamma = EditorGUILayout.Slider("Gamma", gamma, 0.1f, 3f);
                
                EditorGUILayout.Space(5);
                GUILayout.Label("Color Blending", EditorStyles.boldLabel);
                ccTargetColor = EditorGUILayout.ColorField("Target Color", ccTargetColor);
                ccBlendMode = (BlendMode)EditorGUILayout.EnumPopup("Blend Mode", ccBlendMode);
                ccBlendOpacity = EditorGUILayout.Slider("Opacity", ccBlendOpacity, 0f, 1f);
                
                if (EditorGUI.EndChangeCheck())
                {
                    RequestPreviewUpdate();
                }
            });
            
            // --- Blend ---
            DrawToggleSection("Blend / Composite", ref showBlend, () => {
                EditorGUI.BeginChangeCheck();
                
                blendTexture = (Texture2D)EditorGUILayout.ObjectField("Blend Texture", blendTexture, typeof(Texture2D), false);
                blendMaskTexture = (Texture2D)EditorGUILayout.ObjectField("Blend Mask", blendMaskTexture, typeof(Texture2D), false);
                blendMode = (BlendMode)EditorGUILayout.EnumPopup("Blend Mode", blendMode);
                blendStrength = EditorGUILayout.Slider("Opacity", blendStrength, 0f, 1f);
                
                if (blendMode == BlendMode.HDRAdd || blendMode == BlendMode.HDRMultiply)
                {
                    hdrColor = EditorGUILayout.ColorField(new GUIContent("HDR Color"), hdrColor, true, true, true);
                }
                
                if (EditorGUI.EndChangeCheck())
                {
                    RequestPreviewUpdate();
                }
            });
            
            // --- Tone Curve ---
            DrawToggleSection("Tone Curve", ref showToneCurve, () => {
                EditorGUI.BeginChangeCheck();
                
                useRGBCurve = EditorGUILayout.Toggle("RGB Curve", useRGBCurve);
                if (useRGBCurve)
                {
                    rgbCurve = EditorGUILayout.CurveField(" ", rgbCurve, Color.white, new Rect(0, 0, 1, 1));
                }
                
                EditorGUILayout.Space(5);
                
                useRedCurve = EditorGUILayout.Toggle("Red Curve", useRedCurve);
                if (useRedCurve)
                {
                    redCurve = EditorGUILayout.CurveField(" ", redCurve, Color.red, new Rect(0, 0, 1, 1));
                }
                
                useGreenCurve = EditorGUILayout.Toggle("Green Curve", useGreenCurve);
                if (useGreenCurve)
                {
                    greenCurve = EditorGUILayout.CurveField(" ", greenCurve, Color.green, new Rect(0, 0, 1, 1));
                }
                
                useBlueCurve = EditorGUILayout.Toggle("Blue Curve", useBlueCurve);
                if (useBlueCurve)
                {
                    blueCurve = EditorGUILayout.CurveField(" ", blueCurve, Color.blue, new Rect(0, 0, 1, 1));
                }
                
                if (EditorGUI.EndChangeCheck())
                {
                    RequestPreviewUpdate();
                }
            });

            // --- Sharpen / Blur ---
            DrawToggleSection("Sharpen / Blur", ref showSharpen, () => {
                EditorGUI.BeginChangeCheck();
                
                sharpenMode = (SharpenMode)EditorGUILayout.EnumPopup("Mode", sharpenMode);
                
                if (sharpenMode == SharpenMode.Sharpen)
                {
                    sharpenStrength = EditorGUILayout.Slider("Strength", sharpenStrength, 0f, 2f);
                }
                else
                {
                    sharpenStrength = EditorGUILayout.Slider("Strength", sharpenStrength, 0f, 1f);
                }
                
                sharpenKernelSize = EditorGUILayout.IntSlider("Kernel Size", sharpenKernelSize, 3, 9);
                if (sharpenKernelSize % 2 == 0) sharpenKernelSize++;
                
                if (EditorGUI.EndChangeCheck())
                {
                    RequestPreviewUpdate();
                }
            });
            
            // --- Advanced Options ---
            DrawToggleSection("Advanced Options", ref showAdvanced, () => {
                
                // Levels
                GUILayout.Label("Levels", EditorStyles.boldLabel);
                EditorGUI.BeginChangeCheck();
                bool newShowLevels = EditorGUILayout.Toggle("Enable Levels", showLevels);
                if (newShowLevels != showLevels)
                {
                    showLevels = newShowLevels;
                    RequestPreviewUpdate();
                }
                
                if (showLevels)
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.LabelField("Input Levels");
                    EditorGUILayout.MinMaxSlider(ref lvlMinInput, ref lvlMaxInput, 0f, 1f);
                    EditorGUILayout.BeginHorizontal();
                    lvlMinInput = EditorGUILayout.FloatField(lvlMinInput, GUILayout.Width(50));
                    GUILayout.FlexibleSpace();
                    lvlMaxInput = EditorGUILayout.FloatField(lvlMaxInput, GUILayout.Width(50));
                    EditorGUILayout.EndHorizontal();
                    
                    lvlMidGamma = EditorGUILayout.Slider("Midtone (Gamma)", lvlMidGamma, 0.1f, 3f);
                    
                    EditorGUILayout.Space(5);
                    EditorGUILayout.LabelField("Output Levels");
                    EditorGUILayout.MinMaxSlider(ref lvlMinOutput, ref lvlMaxOutput, 0f, 1f);
                    EditorGUILayout.BeginHorizontal();
                    lvlMinOutput = EditorGUILayout.FloatField(lvlMinOutput, GUILayout.Width(50));
                    GUILayout.FlexibleSpace();
                    lvlMaxOutput = EditorGUILayout.FloatField(lvlMaxOutput, GUILayout.Width(50));
                    EditorGUILayout.EndHorizontal();
                    EditorGUI.indentLevel--;
                    
                    if (EditorGUI.EndChangeCheck())
                    {
                        RequestPreviewUpdate();
                    }
                }
                
                EditorGUILayout.Space(10);
                
                // Channel Mixer
                GUILayout.Label("Channel Mixer", EditorStyles.boldLabel);
                EditorGUI.BeginChangeCheck();
                bool newShowCM = EditorGUILayout.Toggle("Enable Channel Mixer", showChannelMixer);
                if (newShowCM != showChannelMixer)
                {
                    showChannelMixer = newShowCM;
                    RequestPreviewUpdate();
                }
                
                if (showChannelMixer)
                {
                    EditorGUI.indentLevel++;
                    cmOutRed = (ChannelSource)EditorGUILayout.EnumPopup("Red Output <=", cmOutRed);
                    cmOutGreen = (ChannelSource)EditorGUILayout.EnumPopup("Green Output <=", cmOutGreen);
                    cmOutBlue = (ChannelSource)EditorGUILayout.EnumPopup("Blue Output <=", cmOutBlue);
                    cmOutAlpha = (ChannelSource)EditorGUILayout.EnumPopup("Alpha Output <=", cmOutAlpha);
                    EditorGUI.indentLevel--;
                    
                    if (EditorGUI.EndChangeCheck())
                    {
                        RequestPreviewUpdate();
                    }
                }
            });
            
            GUILayout.EndVertical();
        }
        
        private void DrawFooter()
        {
            GUILayout.BeginVertical(EditorStyles.helpBox); // フッター領域
            
            // 出力設定
            overwriteSource = EditorGUILayout.ToggleLeft("Output: Overwrite Source", overwriteSource);
            
            if (!overwriteSource)
            {
                EditorGUILayout.BeginHorizontal();
                string displayPath = string.IsNullOrEmpty(customOutputPath) ? outputPath : customOutputPath;
                
                EditorGUI.BeginChangeCheck();
                string newPath = EditorGUILayout.TextField(displayPath); // ラベルなしでスペース節約
                if (EditorGUI.EndChangeCheck() && newPath != displayPath)
                {
                    customOutputPath = newPath;
                    outputPath = newPath;
                }
                
                if (GUILayout.Button("Select...", GUILayout.Width(60)))
                {
                    string timestamp = System.DateTime.Now.ToString("yyMMdd_HHmmss");
                    string defaultName = sourceTexture != null ? Path.GetFileNameWithoutExtension(AssetDatabase.GetAssetPath(sourceTexture)) + $"_edited_{timestamp}" : $"EditedTexture_{timestamp}";
                    string path = EditorUtility.SaveFilePanelInProject("Save Location", defaultName, "png", "Select save location");
                    if (!string.IsNullOrEmpty(path))
                    {
                        customOutputPath = path;
                        outputPath = path;
                    }
                }
                EditorGUILayout.EndHorizontal();
            }
            
            convertToSRGBOnSave = EditorGUILayout.ToggleLeft(new GUIContent("Convert to sRGB on Save (Recommended for Viewers)", "OFF: Keeps Linear (High Precision)"), convertToSRGBOnSave);
            
            EditorGUILayout.Space(5);
            
            // アクションボタン
            GUI.enabled = sourceTexture != null;
            if (GUILayout.Button("Apply & Save", GUILayout.Height(30)))
            {
                ApplyAndSave();
            }
            GUI.enabled = true;
            
            // リセットは小さく右下に
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Reset All Parameters", EditorStyles.miniButton))
            {
                ResetParameters();
            }
            GUILayout.EndHorizontal();
            
            GUILayout.EndVertical();
        }

        // Helper for standard section
        private void DrawSection(string title, System.Action content)
        {
            GUILayout.BeginVertical(boxStyle);
            GUILayout.Label(title, titleStyle);
            EditorGUILayout.Space(2);
            content?.Invoke();
            GUILayout.EndVertical();
        }

        // Helper for toggleable section
        private void DrawToggleSection(string title, ref bool toggle, System.Action content)
        {
            GUILayout.BeginVertical(boxStyle);
            
            // トグル付きヘッダー
            bool newToggle = EditorGUILayout.ToggleLeft(title, toggle, titleStyle);
            if (newToggle != toggle)
            {
                toggle = newToggle;
                RequestPreviewUpdate(); // ON/OFF切り替え時に更新
            }
            
            if (toggle)
            {
                EditorGUILayout.Space(2);
                content?.Invoke();
            }
            
            GUILayout.EndVertical();
        }
        
        /// <summary>
        /// チェッカーボード背景を描画（透明度確認用）
        /// </summary>
        private void DrawCheckerboard(Rect rect)
        {
            Color color1 = new Color(0.8f, 0.8f, 0.8f);
            Color color2 = new Color(0.6f, 0.6f, 0.6f);
            int checkSize = 8;
            
            for (int y = 0; y < rect.height; y += checkSize)
            {
                for (int x = 0; x < rect.width; x += checkSize)
                {
                    // 描画範囲外チェック（パフォーマンス最適化）
                    float w = Mathf.Min(checkSize, rect.width - x);
                    float h = Mathf.Min(checkSize, rect.height - y);
                    if (w <= 0 || h <= 0) continue;

                    bool isColor1 = ((x / checkSize) + (y / checkSize)) % 2 == 0;
                    EditorGUI.DrawRect(new Rect(rect.x + x, rect.y + y, w, h), isColor1 ? color1 : color2);
                }
            }
        }
    }
}
