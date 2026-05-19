using UnityEngine;
using UnityEditor;
using System.IO;

namespace UniTexEditor
{
    public partial class UniTexEditorWindow
    {
        // Addons (CVG など) から直接参照されるため field として保持する。
        // UniTexTheme.ActionButtonStyle の参照をキャッシュする。
        private GUIStyle actionButtonStyle;

        private void InitializeStyles()
        {
            UniTexTheme.Initialize();
            actionButtonStyle = UniTexTheme.ActionButtonStyle;
        }

        private void OnGUI()
        {
            // Info 以外のステータスを一定時間後に自動リセットする
            if (_statusResetTime > 0 && EditorApplication.timeSinceStartup > _statusResetTime)
            {
                statusMessage    = "Ready";
                statusType       = StatusType.Info;
                _statusResetTime = -1.0;
            }

            InitializeStyles();

            // ── ウィンドウ背景 (surface.level0) ──────────────────────────────
            EditorGUI.DrawRect(new Rect(0, 0, position.width, position.height), UniTexTheme.Surface0);

            DrawHeader();
            DrawPreviewArea();

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            DrawSettingsArea();
            EditorGUILayout.EndScrollView();

            DrawFooter();
            DrawStatusBar();
        }

        // ─── ヘッダー ──────────────────────────────────────────────────────

        private void DrawHeader()
        {
            EditorGUILayout.Space(6);

            GUILayout.BeginHorizontal();
            GUILayout.Space(6);
            GUILayout.Label(Localization.GetText("header_title"), UniTexTheme.TitleStyle);
            GUILayout.FlexibleSpace();

            if (GUILayout.Button("JA", UniTexTheme.MiniButtonLeftStyle,  GUILayout.Width(30)))
                Localization.CurrentLanguage = "ja";
            if (GUILayout.Button("EN", UniTexTheme.MiniButtonRightStyle, GUILayout.Width(30)))
                Localization.CurrentLanguage = "en";
            GUILayout.Space(6);
            GUILayout.EndHorizontal();

            EditorGUILayout.Space(6);

            // 全幅セパレーター
            var lineRect = GUILayoutUtility.GetRect(0, 1, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(lineRect, UniTexTheme.Outline);

            EditorGUILayout.Space(4);
        }

        // ─── プレビューエリア ──────────────────────────────────────────────

        private void DrawPreviewArea()
        {
            // 外枠: padding=0 にすることでツールバーをカード端まで伸ばす
            GUILayout.BeginVertical(UniTexTheme.CardOuterStyle);

            // ツールバー行 (surface.level2 背景)
            GUILayout.BeginHorizontal(UniTexTheme.ToolbarStyle);
            GUILayout.Label("PREVIEW", UniTexTheme.SectionHeaderStyle);
            GUILayout.FlexibleSpace();

            bool newAutoPreview = GUILayout.Toggle(
                autoPreview,
                Localization.GetContent("preview_auto_update"),
                EditorStyles.toolbarButton);
            if (newAutoPreview != autoPreview)
            {
                autoPreview = newAutoPreview;
                if (autoPreview) RequestPreviewUpdate(true);
            }

            if (GUILayout.Button(Localization.GetContent("preview_update_btn"), EditorStyles.toolbarButton))
                UpdatePreview();

            GUILayout.EndHorizontal();

            // プレビュー本体 (4px 内側パディング)
            float previewHeight = Mathf.Min(position.height * 0.4f, 400f);
            Rect container = GUILayoutUtility.GetRect(0, previewHeight + 8, GUILayout.ExpandWidth(true));
            Rect previewRect = new Rect(container.x + 4, container.y + 4, container.width - 8, previewHeight);

            if (resultPreview != null)
            {
                if (Event.current.type == EventType.Repaint)
                    DrawCheckerboard(previewRect);

                float textureAspect = (float)resultPreview.width / resultPreview.height;
                float rectAspect    = previewRect.width / previewRect.height;
                Rect drawRect       = previewRect;

                if (textureAspect > rectAspect)
                {
                    float h = previewRect.width / textureAspect;
                    drawRect.y      += (previewRect.height - h) * 0.5f;
                    drawRect.height  = h;
                }
                else
                {
                    float w = previewRect.height * textureAspect;
                    drawRect.x     += (previewRect.width - w) * 0.5f;
                    drawRect.width  = w;
                }

                GUI.DrawTexture(drawRect, resultPreview, ScaleMode.ScaleToFit, true);

                GUI.Label(
                    new Rect(previewRect.x + 5, previewRect.y + previewHeight - 16, 200, 16),
                    $"{resultPreview.width}x{resultPreview.height} ({resultPreview.format})",
                    UniTexTheme.CaptionStyle);
            }
            else
            {
                var placeholderStyle = new GUIStyle(EditorStyles.centeredGreyMiniLabel);
                placeholderStyle.normal.textColor = UniTexTheme.TextTertiary;
                GUI.Box(previewRect,
                    sourceTexture != null ? "Generating Preview..." : "No Source Texture",
                    placeholderStyle);
            }

            GUILayout.EndVertical();
        }

        // ─── 設定エリア ────────────────────────────────────────────────────

        private void DrawSettingsArea()
        {
            GUILayout.BeginVertical();

            // --- Input Section ---
            DrawSection(Localization.GetText("section_input"), () =>
            {
                EditorGUI.BeginChangeCheck();
                sourceTexture = (Texture2D)EditorGUILayout.ObjectField(
                    Localization.GetContent("label_source"), sourceTexture, typeof(Texture2D), false);
                if (EditorGUI.EndChangeCheck())
                {
                    processor.SourceTexture = sourceTexture;
                    RequestPreviewUpdate();
                }

                EditorGUILayout.Space(5);

                EditorGUI.BeginChangeCheck();
                maskTexture = (Texture2D)EditorGUILayout.ObjectField(
                    Localization.GetContent("label_mask"), maskTexture, typeof(Texture2D), false);
                if (EditorGUI.EndChangeCheck())
                {
                    processor.MaskTexture = maskTexture;
                    RequestPreviewUpdate();
                }

                if (maskTexture != null)
                {
                    EditorGUI.indentLevel++;

                    EditorGUI.BeginChangeCheck();
                    invertMask   = EditorGUILayout.Toggle(Localization.GetContent("label_invert_mask"), invertMask);
                    maskStrength = EditorGUILayout.Slider(Localization.GetContent("label_mask_strength"), maskStrength, 0f, 1f);
                    if (EditorGUI.EndChangeCheck())
                        RequestPreviewUpdate();

                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.PrefixLabel(" ");
                    if (GUILayout.Button(Localization.GetContent("btn_save_inverted"), UniTexTheme.MiniButtonStyle))
                    {
                        SaveInvertedMask();
                        GUI.FocusControl(null);
                    }
                    EditorGUILayout.EndHorizontal();

                    EditorGUI.indentLevel--;
                }
            });

            // --- Color Correction ---
            DrawToggleSection(Localization.GetText("section_color_correction"), ref showColorCorrection, () =>
            {
                EditorGUI.BeginChangeCheck();

                hueShift   = EditorGUILayout.Slider(Localization.GetContent("label_hue_shift"),  hueShift,   -180f, 180f);
                saturation = EditorGUILayout.Slider(Localization.GetContent("label_saturation"),  saturation,  0f,   2f);
                brightness = EditorGUILayout.Slider(Localization.GetContent("label_brightness"),  brightness,  0f,   2f);
                gamma      = EditorGUILayout.Slider(Localization.GetContent("label_gamma"),       gamma,       0.1f, 3f);

                GUILayout.Label(Localization.GetText("label_cc_blending"), EditorStyles.boldLabel);
                ccTargetColor  = EditorGUILayout.ColorField(Localization.GetContent("label_target_color"), ccTargetColor);
                ccBlendMode    = (BlendMode)EditorGUILayout.EnumPopup(Localization.GetContent("label_blend_mode"), ccBlendMode);
                ccBlendOpacity = EditorGUILayout.Slider(Localization.GetContent("label_opacity"), ccBlendOpacity, 0f, 1f);

                if (EditorGUI.EndChangeCheck())
                    RequestPreviewUpdate();

            }, onReset: () =>
            {
                hueShift       = 0f;
                saturation     = 1f;
                brightness     = 1f;
                gamma          = 1f;
                ccTargetColor  = Color.white;
                ccBlendMode    = BlendMode.Normal;
                ccBlendOpacity = 0f;
            });

            // --- Tone Curve ---
            DrawToggleSection(Localization.GetText("section_tone_curve"), ref showToneCurve, () =>
            {
                EditorGUI.BeginChangeCheck();

                useRGBCurve = EditorGUILayout.ToggleLeft(Localization.GetText("label_toggle_rgb"), useRGBCurve);
                if (useRGBCurve)
                    rgbCurve = EditorGUILayout.CurveField(Localization.GetText("label_curve_rgb_toggle"), rgbCurve, Color.white, new Rect(0, 0, 1, 1));

                useRedCurve = EditorGUILayout.ToggleLeft(Localization.GetText("label_toggle_r"), useRedCurve);
                if (useRedCurve)
                    redCurve = EditorGUILayout.CurveField(Localization.GetText("label_curve_r_toggle"), redCurve, Color.red, new Rect(0, 0, 1, 1));

                useGreenCurve = EditorGUILayout.ToggleLeft(Localization.GetText("label_toggle_g"), useGreenCurve);
                if (useGreenCurve)
                    greenCurve = EditorGUILayout.CurveField(Localization.GetText("label_curve_g_toggle"), greenCurve, Color.green, new Rect(0, 0, 1, 1));

                useBlueCurve = EditorGUILayout.ToggleLeft(Localization.GetText("label_toggle_b"), useBlueCurve);
                if (useBlueCurve)
                    blueCurve = EditorGUILayout.CurveField(Localization.GetText("label_curve_b_toggle"), blueCurve, Color.blue, new Rect(0, 0, 1, 1));

                if (EditorGUI.EndChangeCheck())
                    RequestPreviewUpdate();

            }, onReset: () =>
            {
                rgbCurve      = AnimationCurve.Linear(0, 0, 1, 1);
                redCurve      = AnimationCurve.Linear(0, 0, 1, 1);
                greenCurve    = AnimationCurve.Linear(0, 0, 1, 1);
                blueCurve     = AnimationCurve.Linear(0, 0, 1, 1);
                useRGBCurve   = true;
                useRedCurve   = false;
                useGreenCurve = false;
                useBlueCurve  = false;
            });

            // --- Blend ---
            DrawToggleSection(Localization.GetText("section_blend"), ref showBlend, () =>
            {
                EditorGUI.BeginChangeCheck();

                blendTexture     = (Texture2D)EditorGUILayout.ObjectField(Localization.GetContent("label_blend_tex"),  blendTexture,     typeof(Texture2D), false);
                blendMaskTexture = (Texture2D)EditorGUILayout.ObjectField(Localization.GetContent("label_blend_mask"), blendMaskTexture, typeof(Texture2D), false);
                blendMode        = (BlendMode)EditorGUILayout.EnumPopup(Localization.GetContent("label_blend_mode"), blendMode);
                blendStrength    = EditorGUILayout.Slider(Localization.GetContent("label_opacity"), blendStrength, 0f, 1f);

                EditorGUI.indentLevel++;
                blendTiling = EditorGUILayout.Toggle(Localization.GetContent("label_tiling"), blendTiling);
                blendScale  = EditorGUILayout.Vector2Field(Localization.GetContent("label_scale"),  blendScale);
                blendOffset = EditorGUILayout.Vector2Field(Localization.GetContent("label_offset"), blendOffset);
                EditorGUI.indentLevel--;

                if (EditorGUI.EndChangeCheck())
                    RequestPreviewUpdate();

            }, onReset: () =>
            {
                blendTexture     = null;
                blendMaskTexture = null;
                blendMode        = BlendMode.Normal;
                blendStrength    = 1f;
                blendTiling      = true;
                blendScale       = Vector2.one;
                blendOffset      = Vector2.zero;
            });

            // --- Levels ---
            DrawToggleSection(Localization.GetText("section_levels"), ref showLevels, () =>
            {
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
                    RequestPreviewUpdate();

            }, onReset: () =>
            {
                lvlMinInput  = 0f;
                lvlMaxInput  = 1f;
                lvlMinOutput = 0f;
                lvlMaxOutput = 1f;
                lvlMidGamma  = 1f;
            });

            // --- Sharpen / Blur ---
            DrawToggleSection(Localization.GetText("section_sharpen"), ref showSharpen, () =>
            {
                EditorGUI.BeginChangeCheck();

                sharpenMode       = (SharpenMode)EditorGUILayout.EnumPopup(Localization.GetText("label_sharpen_mode"), sharpenMode);

                // Sharpen: Strength = ディテール倍率 (0~2)、Blur: Strength = sigma値 (0~5)
                float maxStrength = (sharpenMode == SharpenMode.Sharpen) ? 2f : 5f;
                sharpenStrength   = EditorGUILayout.Slider(Localization.GetText("label_sharpen_strength"), sharpenStrength, 0f, maxStrength);

                sharpenKernelSize  = EditorGUILayout.IntSlider(Localization.GetText("label_sharpen_kernel"), sharpenKernelSize, 3, 9);
                sharpenIterations  = EditorGUILayout.IntSlider(Localization.GetText("label_sharpen_iterations"), sharpenIterations, 1, 8);

                if (EditorGUI.EndChangeCheck())
                    RequestPreviewUpdate();

            }, onReset: () =>
            {
                sharpenMode       = SharpenMode.Sharpen;
                sharpenStrength   = 1f;
                sharpenKernelSize = 5;
                sharpenIterations = 1;
            });

            // --- Channel Mixer ---
            DrawToggleSection(Localization.GetText("section_channel_mixer"), ref showChannelMixer, () =>
            {
                EditorGUI.BeginChangeCheck();

                GUILayout.Label(Localization.GetText("label_cm_source"), EditorStyles.miniLabel);
                cmOutRed   = (ChannelSource)EditorGUILayout.EnumPopup(Localization.GetText("label_cm_red_output"),   cmOutRed);
                cmOutGreen = (ChannelSource)EditorGUILayout.EnumPopup(Localization.GetText("label_cm_green_output"), cmOutGreen);
                cmOutBlue  = (ChannelSource)EditorGUILayout.EnumPopup(Localization.GetText("label_cm_blue_output"),  cmOutBlue);
                cmOutAlpha = (ChannelSource)EditorGUILayout.EnumPopup(Localization.GetText("label_cm_alpha_output"), cmOutAlpha);

                if (EditorGUI.EndChangeCheck())
                    RequestPreviewUpdate();

            }, onReset: () =>
            {
                cmOutRed   = ChannelSource.Red;
                cmOutGreen = ChannelSource.Green;
                cmOutBlue  = ChannelSource.Blue;
                cmOutAlpha = ChannelSource.Alpha;
            });

            // --- Color Variation Generator (Addon) ---
            DrawColorVariation();

            // --- Preset ---
            DrawPresetSection();

            GUILayout.EndVertical();
        }

        // ─── フッター ──────────────────────────────────────────────────────

        private void DrawFooter()
        {
            GUILayout.BeginVertical(UniTexTheme.CardStyle);

            // 出力先表示行
            GUILayout.BeginHorizontal();

            string displayLabel = string.IsNullOrEmpty(customOutputPath)
                ? "OUTPUT: (AUTO)"
                : $"OUTPUT: ...{Path.GetFileName(customOutputPath)}";
            string tooltipPath = string.IsNullOrEmpty(customOutputPath)
                ? "Source と同じフォルダにタイムスタンプ付きで自動保存します"
                : customOutputPath;
            GUILayout.Label(new GUIContent(displayLabel, tooltipPath), UniTexTheme.CaptionStyle);

            GUILayout.FlexibleSpace();

            overwriteSource = GUILayout.Toggle(overwriteSource, Localization.GetContent("label_output_overwrite"));

            if (!overwriteSource)
            {
                if (GUILayout.Button(Localization.GetContent("btn_select"), UniTexTheme.MiniButtonStyle, GUILayout.Width(60)))
                {
                    string path = EditorUtility.SaveFilePanel("Save Texture", "Assets", "ProcessedTexture", "png");
                    if (!string.IsNullOrEmpty(path))
                        customOutputPath = path;
                }
            }

            GUILayout.EndHorizontal();

            DrawSeparator();

            // Apply & Save (primary action)
            if (GUILayout.Button(Localization.GetContent("btn_apply_save"), UniTexTheme.ActionButtonStyle))
                ApplyAndSave();

            EditorGUILayout.Space(4);

            // Reset All (secondary action)
            if (GUILayout.Button(Localization.GetContent("btn_reset_all"), UniTexTheme.SecondaryButtonStyle))
            {
                if (EditorUtility.DisplayDialog("Reset All", "Are you sure you want to reset all parameters?", "Yes", "No"))
                    ResetParameters();
            }

            GUILayout.EndVertical();
        }

        // ─── セクション描画ヘルパー ────────────────────────────────────────

        private void DrawSection(string title, System.Action content)
        {
            GUILayout.BeginVertical(UniTexTheme.CardStyle);
            GUILayout.Label(title.ToUpper(), UniTexTheme.SectionHeaderStyle);
            DrawSeparator();
            content?.Invoke();
            GUILayout.EndVertical();
        }

        /// <summary>
        /// ON/OFF トグル付きセクションを描画する。
        /// OFF のときはセクション名とチェックボックスのみ表示（折りたたみ状態）。
        /// ON のときのみコンテンツとリセットボタンを展開表示する。
        /// </summary>
        private void DrawToggleSection(string title, ref bool toggle, System.Action content, System.Action onReset = null)
        {
            GUILayout.BeginVertical(UniTexTheme.CardStyle);

            // ヘッダー行: トグル ON/OFF で文字色を切り替え
            GUILayout.BeginHorizontal();

            var headerStyle = toggle ? UniTexTheme.ToggleSectionOnStyle : UniTexTheme.ToggleSectionOffStyle;

            EditorGUI.BeginChangeCheck();
            bool newToggle = EditorGUILayout.ToggleLeft(title.ToUpper(), toggle, headerStyle, GUILayout.ExpandWidth(true));
            if (EditorGUI.EndChangeCheck())
            {
                toggle = newToggle;
                RequestPreviewUpdate();
            }

            // リセットボタンは有効時のみ表示
            if (toggle && onReset != null)
            {
                if (GUILayout.Button(Localization.GetContent("btn_reset"), UniTexTheme.MiniButtonStyle, GUILayout.Width(50)))
                {
                    onReset.Invoke();
                    RequestPreviewUpdate();
                    GUI.FocusControl(null);
                }
            }

            GUILayout.EndHorizontal();

            // コンテンツは有効時のみ展開
            if (toggle)
            {
                DrawSeparator();
                content?.Invoke();
            }

            GUILayout.EndVertical();
        }

        /// <summary>
        /// 展開/折りたたみのみを行うセクション（有効化トグルなし）。
        /// PRESET・カラーバリエーション生成など処理パイプラインと無関係なセクションに使用する。
        /// デフォルトは foldout = false（折りたたみ）で呼び出す。
        /// </summary>
        private void DrawFoldoutSection(string title, ref bool foldout, System.Action content)
        {
            GUILayout.BeginVertical(UniTexTheme.CardStyle);

            string label = (foldout ? "▼  " : "▶  ") + title.ToUpper();
            if (GUILayout.Button(label, UniTexTheme.SectionHeaderStyle, GUILayout.ExpandWidth(true)))
                foldout = !foldout;

            if (foldout)
            {
                DrawSeparator();
                content?.Invoke();
            }

            GUILayout.EndVertical();
        }

        // ─── ステータスバー ────────────────────────────────────────────────

        private void DrawStatusBar()
        {
            string displayMessage = string.IsNullOrEmpty(statusMessage) ? "Ready" : statusMessage;
            GUILayout.Box(displayMessage, UniTexTheme.GetStatusStyle(statusType), GUILayout.ExpandWidth(true));
        }

        // ─── セパレーター ─────────────────────────────────────────────────

        private void DrawSeparator()
        {
            var rect = GUILayoutUtility.GetRect(0, 1, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(rect, UniTexTheme.Outline);
            EditorGUILayout.Space(4);
        }

        // ─── チェッカーボード ─────────────────────────────────────────────

        /// <summary>
        /// 透明度確認用チェッカーボード背景を描画する。
        /// テクスチャを一度だけ生成してキャッシュすることで、
        /// OnGUI ループでの大量の DrawRect 呼び出しを回避する。
        /// </summary>
        private void DrawCheckerboard(Rect rect)
        {
            Texture2D checker = GetCheckerboardTexture();
            float uScale = rect.width  / checker.width;
            float vScale = rect.height / checker.height;
            GUI.DrawTextureWithTexCoords(rect, checker, new Rect(0, 0, uScale, vScale));
        }

        private Texture2D GetCheckerboardTexture()
        {
            if (checkerboardTexture != null) return checkerboardTexture;

            const int tileSize  = 128;
            const int checkSize = 8;
            Color c1 = new Color(0.8f, 0.8f, 0.8f);
            Color c2 = new Color(0.6f, 0.6f, 0.6f);

            checkerboardTexture = new Texture2D(tileSize, tileSize, TextureFormat.RGB24, false);
            checkerboardTexture.wrapMode = TextureWrapMode.Repeat;

            var pixels = new Color[tileSize * tileSize];
            for (int y = 0; y < tileSize; y++)
                for (int x = 0; x < tileSize; x++)
                    pixels[y * tileSize + x] = (((x / checkSize) + (y / checkSize)) % 2 == 0) ? c1 : c2;

            checkerboardTexture.SetPixels(pixels);
            checkerboardTexture.Apply(false);
            return checkerboardTexture;
        }

        // ─── Addon フック ─────────────────────────────────────────────────

        partial void DrawColorVariation();
        partial void DrawPresetSection();
    }
}
