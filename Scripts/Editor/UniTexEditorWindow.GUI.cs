using UnityEngine;
using UnityEditor;
using System.IO;

namespace UniTexEditor
{
    public partial class UniTexEditorWindow
    {
        private GUIStyle boxStyle;
        private GUIStyle titleStyle;
        private GUIStyle actionButtonStyle;
        
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
            
            if (actionButtonStyle == null)
            {
                actionButtonStyle = new GUIStyle(EditorStyles.miniButton);
                actionButtonStyle.fontSize = 12;
                actionButtonStyle.fixedHeight = 30;
                actionButtonStyle.fontStyle = FontStyle.Bold;
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
            
            // ステータスバー
            DrawStatusBar();
        }
        
        private void DrawHeader()
        {
            EditorGUILayout.Space(5);
            GUILayout.Label(Localization.GetText("header_title"), EditorStyles.boldLabel);
            EditorGUILayout.Space(5);
            
            // Language Selection (Header Right)
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("JA", EditorStyles.miniButtonLeft, GUILayout.Width(30))) Localization.CurrentLanguage = "ja";
            if (GUILayout.Button("EN", EditorStyles.miniButtonRight, GUILayout.Width(30))) Localization.CurrentLanguage = "en";
            GUILayout.EndHorizontal();
        }
        
        private void DrawPreviewArea()
        {
            GUILayout.BeginVertical(EditorStyles.helpBox);
            
            // ツールバー
            GUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label("Preview", EditorStyles.miniLabel);
            GUILayout.FlexibleSpace();
            
            bool newAutoPreview = GUILayout.Toggle(autoPreview, Localization.GetContent("preview_auto_update"), EditorStyles.toolbarButton);
            if (newAutoPreview != autoPreview)
            {
                autoPreview = newAutoPreview;
                if (autoPreview) RequestPreviewUpdate(true);
            }
            
            if (GUILayout.Button(Localization.GetContent("preview_update_btn"), EditorStyles.toolbarButton))
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
            DrawSection(Localization.GetText("section_input"), () => {
                EditorGUI.BeginChangeCheck();
                sourceTexture = (Texture2D)EditorGUILayout.ObjectField(Localization.GetContent("label_source"), sourceTexture, typeof(Texture2D), false);
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
                maskTexture = (Texture2D)EditorGUILayout.ObjectField(Localization.GetContent("label_mask"), maskTexture, typeof(Texture2D), false);
                if (EditorGUI.EndChangeCheck())
                {
                    processor.MaskTexture = maskTexture;
                    RequestPreviewUpdate();
                }
                
                if (maskTexture != null)
                {
                    EditorGUI.indentLevel++;
                    invertMask = EditorGUILayout.Toggle(Localization.GetContent("label_invert_mask"), invertMask);
                    maskStrength = EditorGUILayout.Slider(Localization.GetContent("label_mask_strength"), maskStrength, 0f, 1f);
                    
                    // Save Inverted Mask Button
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.PrefixLabel(" "); // インデント合わせ
                    if (GUILayout.Button(Localization.GetContent("btn_save_inverted"), EditorStyles.miniButton))
                    {
                        SaveInvertedMask();
                        // フォーカスを外す
                        GUI.FocusControl(null);
                    }
                    EditorGUILayout.EndHorizontal();
                    
                    EditorGUI.indentLevel--;
                }
            });
            
            // --- Color Correction ---
            DrawToggleSection(Localization.GetText("section_color_correction"), ref showColorCorrection, () => {
                EditorGUI.BeginChangeCheck();
                
                hueShift = EditorGUILayout.Slider(Localization.GetContent("label_hue_shift"), hueShift, -180f, 180f);
                saturation = EditorGUILayout.Slider(Localization.GetContent("label_saturation"), saturation, 0f, 2f);
                brightness = EditorGUILayout.Slider(Localization.GetContent("label_brightness"), brightness, 0f, 2f);
                gamma = EditorGUILayout.Slider(Localization.GetContent("label_gamma"), gamma, 0.1f, 3f);
                GUILayout.Label(Localization.GetText("label_cc_blending"), EditorStyles.boldLabel);
                ccTargetColor = EditorGUILayout.ColorField(Localization.GetContent("label_target_color"), ccTargetColor);
                ccBlendMode = (BlendMode)EditorGUILayout.EnumPopup(Localization.GetContent("label_blend_mode"), ccBlendMode);
                ccBlendOpacity = EditorGUILayout.Slider(Localization.GetContent("label_opacity"), ccBlendOpacity, 0f, 1f);
                
                if (EditorGUI.EndChangeCheck())
                {
                    RequestPreviewUpdate();
                }
            }, () => {
                // Reset Logic
                hueShift = 0f;
                saturation = 1f;
                brightness = 1f;
                brightness = 1f;
                gamma = 1f;
                ccTargetColor = Color.white;
                ccBlendMode = BlendMode.Normal;
                ccBlendOpacity = 0f;
            });
            
            // --- Tone Curve ---
            DrawToggleSection(Localization.GetText("section_tone_curve"), ref showToneCurve, () => {
                EditorGUI.BeginChangeCheck();
                
                useRGBCurve = EditorGUILayout.ToggleLeft(Localization.GetText("label_toggle_rgb"), useRGBCurve);
                if (useRGBCurve) rgbCurve = EditorGUILayout.CurveField(Localization.GetText("label_curve_rgb_toggle"), rgbCurve, Color.white, new Rect(0,0,1,1));
                
                useRedCurve = EditorGUILayout.ToggleLeft(Localization.GetText("label_toggle_r"), useRedCurve);
                if (useRedCurve) redCurve = EditorGUILayout.CurveField(Localization.GetText("label_curve_r_toggle"), redCurve, Color.red, new Rect(0,0,1,1));
                
                useGreenCurve = EditorGUILayout.ToggleLeft(Localization.GetText("label_toggle_g"), useGreenCurve);
                if (useGreenCurve) greenCurve = EditorGUILayout.CurveField(Localization.GetText("label_curve_g_toggle"), greenCurve, Color.green, new Rect(0,0,1,1));
                
                useBlueCurve = EditorGUILayout.ToggleLeft(Localization.GetText("label_toggle_b"), useBlueCurve);
                if (useBlueCurve) blueCurve = EditorGUILayout.CurveField(Localization.GetText("label_curve_b_toggle"), blueCurve, Color.blue, new Rect(0,0,1,1));
                
                if (EditorGUI.EndChangeCheck())
                {
                    RequestPreviewUpdate();
                }
            }, () => {
                rgbCurve = AnimationCurve.Linear(0,0,1,1);
                redCurve = AnimationCurve.Linear(0,0,1,1);
                greenCurve = AnimationCurve.Linear(0,0,1,1);
                blueCurve = AnimationCurve.Linear(0,0,1,1);
                useRGBCurve = true;
                useRedCurve = false;
                useGreenCurve = false;
                useBlueCurve = false;
            });
            
            // --- Blend ---
            DrawToggleSection(Localization.GetText("section_blend"), ref showBlend, () => {
                EditorGUI.BeginChangeCheck();
                
                blendTexture = (Texture2D)EditorGUILayout.ObjectField(Localization.GetContent("label_blend_tex"), blendTexture, typeof(Texture2D), false);
                blendMaskTexture = (Texture2D)EditorGUILayout.ObjectField(Localization.GetContent("label_blend_mask"), blendMaskTexture, typeof(Texture2D), false);
                blendMode = (BlendMode)EditorGUILayout.EnumPopup(Localization.GetContent("label_blend_mode"), blendMode);
                blendStrength = EditorGUILayout.Slider(Localization.GetContent("label_opacity"), blendStrength, 0f, 1f);
                
                // Transform
                EditorGUI.indentLevel++;
                blendTiling = EditorGUILayout.Toggle(Localization.GetContent("label_tiling"), blendTiling);
                blendScale = EditorGUILayout.Vector2Field(Localization.GetContent("label_scale"), blendScale);
                blendOffset = EditorGUILayout.Vector2Field(Localization.GetContent("label_offset"), blendOffset);
                EditorGUI.indentLevel--;
                
                if (EditorGUI.EndChangeCheck())
                {
                    RequestPreviewUpdate();
                }
            }, () => {
                // Reset Logic
                blendTexture = null;
                blendMaskTexture = null;
                blendMode = BlendMode.Normal;
                blendStrength = 1f;
                blendTiling = true;
                blendScale = Vector2.one;
                blendOffset = Vector2.zero;
            });
            
            // --- Levels ---
            DrawToggleSection(Localization.GetText("section_levels"), ref showLevels, () => {
                EditorGUI.BeginChangeCheck();
                
                EditorGUILayout.MinMaxSlider(Localization.GetText("label_input_levels_slider"), ref lvlMinInput, ref lvlMaxInput, 0f, 1f);
                GUILayout.BeginHorizontal();
                GUILayout.Label($"{Localization.GetText("label_min")}: {lvlMinInput:F2}", EditorStyles.miniLabel);
                GUILayout.FlexibleSpace();
                GUILayout.Label($"{Localization.GetText("label_max")}: {lvlMaxInput:F2}", EditorStyles.miniLabel);
                GUILayout.EndHorizontal();
                
                lvlMidGamma = EditorGUILayout.Slider(Localization.GetText("label_mid_gamma_slider"), lvlMidGamma, 0.1f, 5f);
                
                EditorGUILayout.Space(2);
                
                EditorGUILayout.MinMaxSlider(Localization.GetText("label_output_levels_slider"), ref lvlMinOutput, ref lvlMaxOutput, 0f, 1f);
                GUILayout.BeginHorizontal();
                GUILayout.Label($"{Localization.GetText("label_min")}: {lvlMinOutput:F2}", EditorStyles.miniLabel);
                GUILayout.FlexibleSpace();
                GUILayout.Label($"{Localization.GetText("label_max")}: {lvlMaxOutput:F2}", EditorStyles.miniLabel);
                GUILayout.EndHorizontal();
                
                if (EditorGUI.EndChangeCheck())
                {
                    RequestPreviewUpdate();
                }
            }, () => {
                lvlMinInput = 0f;
                lvlMaxInput = 1f;
                lvlMinOutput = 0f;
                lvlMaxOutput = 1f;
                lvlMidGamma = 1f;
            });

            // --- Sharpen / Blur ---
            DrawToggleSection(Localization.GetText("section_sharpen"), ref showSharpen, () => {
                EditorGUI.BeginChangeCheck();
                
                sharpenMode = (SharpenMode)EditorGUILayout.EnumPopup(Localization.GetText("label_sharpen_mode"), sharpenMode);
                sharpenStrength = EditorGUILayout.Slider(Localization.GetContent("label_sharpen_strength"), sharpenStrength, 0f, 2f);
                sharpenRange = EditorGUILayout.Slider(Localization.GetContent("label_sharpen_range"), sharpenRange, 0.5f, 20f);
                
                if (EditorGUI.EndChangeCheck())
                {
                    RequestPreviewUpdate();
                }
            }, () => {
                sharpenMode = SharpenMode.Sharpen;
                sharpenStrength = 1f;
                sharpenRange = 3f;
            });
            
            // --- Channel Mixer ---
            DrawToggleSection(Localization.GetText("section_channel_mixer"), ref showChannelMixer, () => {
                EditorGUI.BeginChangeCheck();
                
                GUILayout.Label(Localization.GetText("label_cm_source"), EditorStyles.miniLabel);
                cmOutRed = (ChannelSource)EditorGUILayout.EnumPopup(Localization.GetText("label_cm_red_output"), cmOutRed);
                cmOutGreen = (ChannelSource)EditorGUILayout.EnumPopup(Localization.GetText("label_cm_green_output"), cmOutGreen);
                cmOutBlue = (ChannelSource)EditorGUILayout.EnumPopup(Localization.GetText("label_cm_blue_output"), cmOutBlue);
                cmOutAlpha = (ChannelSource)EditorGUILayout.EnumPopup(Localization.GetText("label_cm_alpha_output"), cmOutAlpha);
                
                if (EditorGUI.EndChangeCheck())
                {
                    RequestPreviewUpdate();
                }
            }, () => {
                cmOutRed = ChannelSource.Red;
                cmOutGreen = ChannelSource.Green;
                cmOutBlue = ChannelSource.Blue;
                cmOutAlpha = ChannelSource.Alpha;
            });
            
            // --- Color Variation Generator (Addon Trigger) ---
            DrawColorVariation();
            
            // ... (lines 234-419 omitted) ...

            GUILayout.EndVertical();
        }
        
        private void DrawFooter()
        {
            GUILayout.BeginVertical(boxStyle);
            
            // Output Path
            GUILayout.BeginHorizontal();
            GUILayout.Label(string.IsNullOrEmpty(customOutputPath) ? "Output: (Auto)" : $"Output: ...{Path.GetFileName(customOutputPath)}", EditorStyles.miniLabel);
            GUILayout.FlexibleSpace();
            
            // Overwrite Toggle
            overwriteSource = GUILayout.Toggle(overwriteSource, Localization.GetContent("label_output_overwrite"));
            
            if (!overwriteSource)
            {
                if (GUILayout.Button(Localization.GetContent("btn_select"), EditorStyles.miniButton, GUILayout.Width(60)))
                {
                    string path = EditorUtility.SaveFilePanel("Save Texture", "Assets", "ProcessedTexture", "png");
                    if (!string.IsNullOrEmpty(path))
                    {
                        customOutputPath = path;
                    }
                }
            }
            GUILayout.EndHorizontal();
            
            // sRGB Convert Option Removed (Always ON)
            
            EditorGUILayout.Space(5);
            
            // Apply Button
            if (GUILayout.Button(Localization.GetContent("btn_apply_save"), actionButtonStyle))
            {
                ApplyAndSave();
            }
            
            EditorGUILayout.Space(2);
            
            // Reset All Button
            if (GUILayout.Button(Localization.GetContent("btn_reset_all")))
            {
                if (EditorUtility.DisplayDialog("Reset All", "Are you sure you want to reset all parameters?", "Yes", "No"))
                {
                    ResetParameters();
                }
            }
            
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

        // Helper for toggleable section with Reset button
        private void DrawToggleSection(string title, ref bool toggle, System.Action content, System.Action onReset = null)
        {
            GUILayout.BeginVertical(boxStyle);
            
            // トグル付きヘッダー（リセットボタン付き）
            GUILayout.BeginHorizontal();
            
            EditorGUI.BeginChangeCheck();
            bool newToggle = EditorGUILayout.ToggleLeft(title, toggle, titleStyle, GUILayout.ExpandWidth(true));
            if (EditorGUI.EndChangeCheck())
            {
                toggle = newToggle;
                RequestPreviewUpdate(); // ON/OFF切り替え時に更新
            }

            // リセットボタン（コールバックがある場合のみ表示）
            if (onReset != null)
            {
                // リセットアイコンがあればもっと良いが、テキストで実装
                if (GUILayout.Button(Localization.GetContent("btn_reset"), EditorStyles.miniButton, GUILayout.Width(50)))
                {
                    // リセット実行
                    onReset.Invoke();
                    
                    // 値が変わったはずなのでプレビュー更新
                    RequestPreviewUpdate();
                    
                    // GUIフォーカスを外す（数値入力中などの場合、古い値が残るのを防ぐ）
                    GUI.FocusControl(null);
                }
            }
            
            GUILayout.EndHorizontal();
            
            if (toggle)
            {
                EditorGUILayout.Space(2);
                content?.Invoke();
            }
            
            GUILayout.EndVertical();
        }
        
        /// <summary>
        /// ステータスバーを描画
        /// </summary>
        private void DrawStatusBar()
        {
            Color backgroundColor = GUI.backgroundColor;
            
            // 色の設定 (彩度高め)
            switch (statusType)
            {
                case StatusType.Success:
                    GUI.backgroundColor = new Color(0.2f, 0.8f, 0.2f); // 鮮やかな緑
                    break;
                case StatusType.Error:
                    GUI.backgroundColor = new Color(0.9f, 0.3f, 0.3f); // 鮮やかな赤
                    break;
                case StatusType.Info:
                default:
                    GUI.backgroundColor = Color.gray;
                    break;
            }

            // メッセージ表示
            // 空の場合は高さだけ確保して表示
            string displayMessage = string.IsNullOrEmpty(statusMessage) ? "Ready" : statusMessage;
            EditorGUILayout.HelpBox(displayMessage, MessageType.None);
            
            // 色を戻す
            GUI.backgroundColor = backgroundColor;
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
        /// <summary>
        /// Color Variation Generator Hook
        /// </summary>
        partial void DrawColorVariation();
    }
}
