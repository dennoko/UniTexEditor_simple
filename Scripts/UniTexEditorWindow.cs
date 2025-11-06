using UnityEngine;
using UnityEditor;
using System.IO;

namespace UniTexEditor
{
    /// <summary>
    /// UniTexEditor メインエディタウィンドウ
    /// </summary>
    public class UniTexEditorWindow : EditorWindow
    {
        private TextureProcessor processor;
        private Texture2D sourceTexture;
        private Texture2D maskTexture;
        private Mesh sourceMesh;
        private GameObject sourceGameObject; // SkinnedMeshRenderer対応
        private Texture2D resultPreview;
        
        // 色調補正パラメータ
        private bool showColorCorrection = true;
        private float hueShift = 0f;
        private float saturation = 1f;
        private float brightness = 1f;
        private float gamma = 1f;
        
        // ブレンドパラメータ
        private bool showBlend = false;
        private Texture2D blendTexture;
        private BlendMode blendMode = BlendMode.Normal;
        private float blendStrength = 1f;
        private Color hdrColor = Color.white;
        
        // UVブラーパラメータ
        private bool showUVBlur = false;
        private int blurRadius = 5;
        private float blurSigma = 2f;
        
        // シャープネス/ぼかしパラメータ
        private bool showSharpen = false;
        private SharpenMode sharpenMode = SharpenMode.Sharpen;
        private float sharpenStrength = 1f;
        private int sharpenKernelSize = 5;
        
        // トーンカーブパラメータ
        private bool showToneCurve = false;
        private AnimationCurve rgbCurve = AnimationCurve.Linear(0, 0, 1, 1);
        private AnimationCurve redCurve = AnimationCurve.Linear(0, 0, 1, 1);
        private AnimationCurve greenCurve = AnimationCurve.Linear(0, 0, 1, 1);
        private AnimationCurve blueCurve = AnimationCurve.Linear(0, 0, 1, 1);
        private bool useRGBCurve = true;
        private bool useRedCurve = false;
        private bool useGreenCurve = false;
        private bool useBlueCurve = false;
        
        // マスクオプション（常時ON）
        private bool invertMask = false;
        private float maskStrength = 1f;
        
        // 出力設定
        private bool overwriteSource = false;
        private string outputPath = "";
        private string customOutputPath = ""; // ユーザーが明示的に指定したパス
        private bool convertToSRGBOnSave = true; // 保存時にsRGBに変換するか
        
        // プレビュー
        private bool autoPreview = true;
        private Vector2 scrollPosition;
        private Vector2 previewScrollPosition;
        private const float PREVIEW_MAX_SIZE = 512f; // プレビュー最大サイズ
        private const int PREVIEW_RESOLUTION = 512; // プレビュー計算解像度
        
        [MenuItem("Window/UniTex Editor")]
        public static void ShowWindow()
        {
            var window = GetWindow<UniTexEditorWindow>("UniTex Editor");
            window.minSize = new Vector2(400, 600);
        }
        
        private void OnEnable()
        {
            processor = new TextureProcessor();
        }
        
        private void OnDisable()
        {
            processor?.Cleanup();
            
            // プレビューテクスチャもクリーンアップ
            if (resultPreview != null)
            {
                DestroyImmediate(resultPreview);
                resultPreview = null;
            }
        }
        
        private void OnGUI()
        {
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            
            GUILayout.Label("UniTex Editor - テクスチャ編集", EditorStyles.boldLabel);
            EditorGUILayout.Space();
            
            // ===== 入力セクション =====
            GUILayout.Label("入力", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            sourceTexture = (Texture2D)EditorGUILayout.ObjectField("ソーステクスチャ", sourceTexture, typeof(Texture2D), false);
            if (EditorGUI.EndChangeCheck())
            {
                processor.SourceTexture = sourceTexture;
                
                // デフォルト出力パスを更新（カスタムパスが指定されていない場合のみ）
                if (sourceTexture != null && string.IsNullOrEmpty(customOutputPath))
                {
                    UpdateDefaultOutputPath();
                }
                
                if (autoPreview) UpdatePreview();
            }
            
            EditorGUILayout.Space();
            
            // ===== マスクセクション（常時表示）=====
            GUILayout.Label("マスク", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            maskTexture = (Texture2D)EditorGUILayout.ObjectField("マスクテクスチャ", maskTexture, typeof(Texture2D), false);
            if (EditorGUI.EndChangeCheck())
            {
                processor.MaskTexture = maskTexture;
                if (autoPreview) UpdatePreview();
            }
            invertMask = EditorGUILayout.Toggle("マスク反転", invertMask);
            maskStrength = EditorGUILayout.Slider("マスク強度", maskStrength, 0f, 1f);
            
            EditorGUILayout.Space();
            
            // ===== メッシュ（UV Blur用）セクション =====
            GUILayout.Label("メッシュ（UV Blur用、オプション）", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            
            // メッシュ直接指定
            sourceMesh = (Mesh)EditorGUILayout.ObjectField("メッシュ", sourceMesh, typeof(Mesh), false);
            
            // GameObjectから自動取得
            sourceGameObject = (GameObject)EditorGUILayout.ObjectField("GameObject（自動取得）", sourceGameObject, typeof(GameObject), true);
            
            if (EditorGUI.EndChangeCheck())
            {
                // GameObjectが指定されている場合、そこからメッシュを取得
                if (sourceGameObject != null)
                {
                    Mesh extractedMesh = ExtractMeshFromGameObject(sourceGameObject);
                    if (extractedMesh != null)
                    {
                        sourceMesh = extractedMesh;
                        EditorGUILayout.HelpBox($"メッシュを自動取得: {extractedMesh.name}", MessageType.Info);
                    }
                    else
                    {
                        EditorGUILayout.HelpBox("GameObjectにMeshFilterまたはSkinnedMeshRendererが見つかりません。", MessageType.Warning);
                    }
                }
                
                if (autoPreview) UpdatePreview();
            }
            
            if (sourceMesh != null)
            {
                EditorGUILayout.HelpBox($"メッシュ: {sourceMesh.name} ({sourceMesh.vertexCount} verts) - UVアイランドブラーが利用可能です。", MessageType.Info);
            }
            
            EditorGUILayout.Space();
            
            // ===== 色調補正セクション =====
            showColorCorrection = EditorGUILayout.Foldout(showColorCorrection, "色調補正", true);
            if (showColorCorrection)
            {
                EditorGUI.indentLevel++;
                EditorGUI.BeginChangeCheck();
                
                hueShift = EditorGUILayout.Slider("色相シフト", hueShift, -180f, 180f);
                saturation = EditorGUILayout.Slider("彩度", saturation, 0f, 2f);
                brightness = EditorGUILayout.Slider("明度", brightness, 0f, 2f);
                gamma = EditorGUILayout.Slider("ガンマ", gamma, 0.1f, 3f);
                
                if (EditorGUI.EndChangeCheck() && autoPreview)
                {
                    UpdatePreview();
                }
                
                EditorGUI.indentLevel--;
            }
            
            EditorGUILayout.Space();
            
            // ===== ブレンドセクション =====
            showBlend = EditorGUILayout.Foldout(showBlend, "テクスチャ合成", true);
            if (showBlend)
            {
                EditorGUI.indentLevel++;
                EditorGUI.BeginChangeCheck();
                
                blendTexture = (Texture2D)EditorGUILayout.ObjectField("合成テクスチャ", blendTexture, typeof(Texture2D), false);
                blendMode = (BlendMode)EditorGUILayout.EnumPopup("ブレンドモード", blendMode);
                blendStrength = EditorGUILayout.Slider("合成強度", blendStrength, 0f, 1f);
                
                if (blendMode == BlendMode.HDRAdd || blendMode == BlendMode.HDRMultiply)
                {
                    hdrColor = EditorGUILayout.ColorField(new GUIContent("HDR カラー"), hdrColor, true, true, true);
                }
                
                if (EditorGUI.EndChangeCheck() && autoPreview)
                {
                    UpdatePreview();
                }
                
                EditorGUI.indentLevel--;
            }
            
            EditorGUILayout.Space();
            
            // ===== シャープネス/ぼかしセクション =====
            showSharpen = EditorGUILayout.Foldout(showSharpen, "シャープネス / ぼかし", true);
            if (showSharpen)
            {
                EditorGUI.indentLevel++;
                EditorGUI.BeginChangeCheck();
                
                sharpenMode = (SharpenMode)EditorGUILayout.EnumPopup("モード", sharpenMode);
                
                if (sharpenMode == SharpenMode.Sharpen)
                {
                    sharpenStrength = EditorGUILayout.Slider("シャープネス強度", sharpenStrength, 0f, 2f);
                    EditorGUILayout.HelpBox("Unsharp Mask方式でエッジを強調します。", MessageType.Info);
                }
                else
                {
                    sharpenStrength = EditorGUILayout.Slider("ぼかし強度", sharpenStrength, 0f, 1f);
                    EditorGUILayout.HelpBox("ガウシアンブラーを適用します。", MessageType.Info);
                }
                
                sharpenKernelSize = EditorGUILayout.IntSlider("カーネルサイズ", sharpenKernelSize, 3, 9);
                if (sharpenKernelSize % 2 == 0) sharpenKernelSize++; // 奇数に強制
                
                if (EditorGUI.EndChangeCheck() && autoPreview)
                {
                    UpdatePreview();
                }
                
                EditorGUI.indentLevel--;
            }
            
            EditorGUILayout.Space();
            
            // ===== UV アイランドブラーセクション =====
            showUVBlur = EditorGUILayout.Foldout(showUVBlur, "UVアイランドブラー（メッシュ不要）", true);
            if (showUVBlur)
            {
                EditorGUI.indentLevel++;
                
                EditorGUI.BeginChangeCheck();
                
                blurRadius = EditorGUILayout.IntSlider("ブラー半径", blurRadius, 1, 20);
                blurSigma = EditorGUILayout.Slider("ブラーシグマ", blurSigma, 0.5f, 10f);
                
                if (EditorGUI.EndChangeCheck() && autoPreview)
                {
                    UpdatePreview();
                }
                
                EditorGUI.indentLevel--;
                EditorGUILayout.HelpBox("テクスチャの色差からUV境界を自動検出します。メッシュ不要で高速動作します。", MessageType.Info);
            }
            
            EditorGUILayout.Space();
            
            // ===== トーンカーブセクション =====
            showToneCurve = EditorGUILayout.Foldout(showToneCurve, "トーンカーブ（色調整）", true);
            if (showToneCurve)
            {
                EditorGUI.indentLevel++;
                EditorGUI.BeginChangeCheck();
                
                useRGBCurve = EditorGUILayout.Toggle("RGB カーブを使用", useRGBCurve);
                if (useRGBCurve)
                {
                    EditorGUI.indentLevel++;
                    rgbCurve = EditorGUILayout.CurveField("RGB カーブ", rgbCurve, Color.white, new Rect(0, 0, 1, 1));
                    EditorGUI.indentLevel--;
                }
                
                EditorGUILayout.Space(5);
                
                useRedCurve = EditorGUILayout.Toggle("R カーブを使用", useRedCurve);
                if (useRedCurve)
                {
                    EditorGUI.indentLevel++;
                    redCurve = EditorGUILayout.CurveField("R カーブ", redCurve, Color.red, new Rect(0, 0, 1, 1));
                    EditorGUI.indentLevel--;
                }
                
                useGreenCurve = EditorGUILayout.Toggle("G カーブを使用", useGreenCurve);
                if (useGreenCurve)
                {
                    EditorGUI.indentLevel++;
                    greenCurve = EditorGUILayout.CurveField("G カーブ", greenCurve, Color.green, new Rect(0, 0, 1, 1));
                    EditorGUI.indentLevel--;
                }
                
                useBlueCurve = EditorGUILayout.Toggle("B カーブを使用", useBlueCurve);
                if (useBlueCurve)
                {
                    EditorGUI.indentLevel++;
                    blueCurve = EditorGUILayout.CurveField("B カーブ", blueCurve, Color.blue, new Rect(0, 0, 1, 1));
                    EditorGUI.indentLevel--;
                }
                
                if (EditorGUI.EndChangeCheck() && autoPreview)
                {
                    UpdatePreview();
                }
                
                EditorGUI.indentLevel--;
                EditorGUILayout.HelpBox("カーブを調整してトーン（明暗）を細かく制御できます。RGBは全チャンネル共通、R/G/Bは個別調整です。", MessageType.Info);
            }
            
            EditorGUILayout.Space();
            
            // ===== プレビューセクション =====
            GUILayout.Label("プレビュー", EditorStyles.boldLabel);
            autoPreview = EditorGUILayout.Toggle("自動プレビュー", autoPreview);
            
            if (!autoPreview && GUILayout.Button("プレビュー更新"))
            {
                UpdatePreview();
            }
            
            if (resultPreview != null)
            {
                EditorGUILayout.Space();
                GUILayout.Label($"プレビュー ({resultPreview.width}x{resultPreview.height})", EditorStyles.miniLabel);
                
                // 表示サイズを計算（アスペクト比を維持）
                float textureAspect = (float)resultPreview.width / resultPreview.height;
                float displayWidth, displayHeight;
                
                float availableWidth = EditorGUIUtility.currentViewWidth - 40f;
                float maxDisplaySize = Mathf.Min(PREVIEW_MAX_SIZE, availableWidth);
                
                if (textureAspect > 1f)
                {
                    // 横長
                    displayWidth = maxDisplaySize;
                    displayHeight = maxDisplaySize / textureAspect;
                }
                else
                {
                    // 縦長または正方形
                    displayHeight = maxDisplaySize;
                    displayWidth = maxDisplaySize * textureAspect;
                }
                
                // 中央配置
                GUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                
                Rect previewRect = GUILayoutUtility.GetRect(displayWidth, displayHeight, GUILayout.ExpandWidth(false));
                
                // 背景（チェッカーボード）を描画
                if (Event.current.type == EventType.Repaint)
                {
                    DrawCheckerboard(previewRect);
                }
                
                // プレビューを描画（alphaBlend=trueで透明度対応）
                GUI.DrawTexture(previewRect, resultPreview, ScaleMode.ScaleToFit, true);
                
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
            }
            else if (sourceTexture != null)
            {
                EditorGUILayout.HelpBox("プレビューを生成中... パラメータを調整してください。", MessageType.Info);
            }
            
            EditorGUILayout.Space();
            
            // ===== 出力セクション =====
            GUILayout.Label("出力", EditorStyles.boldLabel);
            overwriteSource = EditorGUILayout.Toggle("ソースを上書き", overwriteSource);
            
            if (!overwriteSource)
            {
                EditorGUILayout.BeginHorizontal();
                
                // 表示用のパス（デフォルトまたはカスタム）
                string displayPath = string.IsNullOrEmpty(customOutputPath) ? outputPath : customOutputPath;
                EditorGUI.BeginChangeCheck();
                string newPath = EditorGUILayout.TextField("出力パス", displayPath);
                if (EditorGUI.EndChangeCheck() && newPath != displayPath)
                {
                    // ユーザーが手動で編集した場合
                    customOutputPath = newPath;
                    outputPath = newPath;
                }
                
                if (GUILayout.Button("...", GUILayout.Width(30)))
                {
                    string defaultName = sourceTexture != null ? Path.GetFileNameWithoutExtension(AssetDatabase.GetAssetPath(sourceTexture)) + "_edited" : "EditedTexture";
                    string path = EditorUtility.SaveFilePanelInProject("保存先を選択", defaultName, "png", "保存先を選択してください");
                    if (!string.IsNullOrEmpty(path))
                    {
                        customOutputPath = path;
                        outputPath = path;
                    }
                }
                
                EditorGUILayout.EndHorizontal();
                
                // デフォルトパスの説明
                if (string.IsNullOrEmpty(customOutputPath) && !string.IsNullOrEmpty(outputPath))
                {
                    EditorGUILayout.HelpBox("デフォルト: ソーステクスチャと同じフォルダに保存されます。", MessageType.Info);
                }
            }
            
            // 色空間変換オプション
            EditorGUILayout.Space(5);
            convertToSRGBOnSave = EditorGUILayout.Toggle(new GUIContent("sRGB変換して保存", 
                "ON: 一般的な画像ビューア向け（推奨）\nOFF: Linear色空間のまま保存（高精度）"), 
                convertToSRGBOnSave);
            
            if (!convertToSRGBOnSave)
            {
                EditorGUILayout.HelpBox("⚠ Linear色空間で保存すると、一部の画像ビューアで暗く表示される場合があります。", MessageType.Warning);
            }
            
            EditorGUILayout.Space();
            
            // ===== 実行ボタン =====
            GUI.enabled = sourceTexture != null;
            if (GUILayout.Button("適用して保存", GUILayout.Height(40)))
            {
                ApplyAndSave();
            }
            GUI.enabled = true;
            
            if (GUILayout.Button("リセット"))
            {
                ResetParameters();
            }
            
            EditorGUILayout.EndScrollView();
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
                    bool isColor1 = ((x / checkSize) + (y / checkSize)) % 2 == 0;
                    EditorGUI.DrawRect(new Rect(rect.x + x, rect.y + y, 
                        Mathf.Min(checkSize, rect.width - x), 
                        Mathf.Min(checkSize, rect.height - y)), 
                        isColor1 ? color1 : color2);
                }
            }
        }
        
        private void UpdatePreview()
        {
            if (sourceTexture == null) return;
            
            // 既存のプレビューを破棄
            if (resultPreview != null)
            {
                DestroyImmediate(resultPreview);
                resultPreview = null;
            }
            
            // ノードをクリアして再構築
            processor.ClearNodes();
            
            bool hasNodes = false;
            
            // 色調補正ノードを追加
            if (showColorCorrection && (hueShift != 0 || saturation != 1f || brightness != 1f || gamma != 1f))
            {
                var colorNode = new ColorCorrectionNode
                {
                    hueShift = hueShift,
                    saturation = saturation,
                    brightness = brightness,
                    gamma = gamma
                };
                processor.AddNode(colorNode);
                hasNodes = true;
            }
            
            // ブレンドノードを追加
            if (showBlend && blendTexture != null)
            {
                var blendNode = new BlendNode
                {
                    blendTexture = blendTexture,
                    blendMode = blendMode,
                    blendStrength = blendStrength,
                    hdrColor = hdrColor
                };
                processor.AddNode(blendNode);
                hasNodes = true;
            }
            
            // UVブラーノードを追加（メッシュ不要版）
            if (showUVBlur)
            {
                var uvBlurNode = new UVIslandBlurNode
                {
                    blurRadius = blurRadius,
                    blurSigma = blurSigma
                };
                processor.AddNode(uvBlurNode);
                hasNodes = true;
            }
            
            // シャープネス/ぼかしノードを追加
            if (showSharpen)
            {
                var sharpenNode = new SharpenNode
                {
                    mode = sharpenMode,
                    strength = sharpenStrength,
                    kernelSize = sharpenKernelSize
                };
                processor.AddNode(sharpenNode);
                hasNodes = true;
            }
            
            // トーンカーブノードを追加
            if (showToneCurve && (useRGBCurve || useRedCurve || useGreenCurve || useBlueCurve))
            {
                var toneCurveNode = new ToneCurveNode
                {
                    rgbCurve = rgbCurve,
                    redCurve = redCurve,
                    greenCurve = greenCurve,
                    blueCurve = blueCurve,
                    useRGBCurve = useRGBCurve,
                    useRedCurve = useRedCurve,
                    useGreenCurve = useGreenCurve,
                    useBlueCurve = useBlueCurve
                };
                processor.AddNode(toneCurveNode);
                hasNodes = true;
            }
            
            // プレビューを生成（Texture2Dとしてコピー）
            // パフォーマンスのため512x512に制限
            try
            {
                Texture2D linearResult;
                
                if (hasNodes)
                {
                    // ノードがある場合は処理を実行（Linear色空間で取得）
                    linearResult = processor.GetResultAsTexture2D(PREVIEW_RESOLUTION);
                }
                else
                {
                    // ノードがない場合はソーステクスチャをそのまま使用（リサイズのみ）
                    linearResult = ResizeTexture(sourceTexture, PREVIEW_RESOLUTION);
                }
                
                if (linearResult != null)
                {
                    // GUI表示用にLinear→sRGB変換
                    resultPreview = TextureProcessor.ConvertLinearToSRGB(linearResult);
                    DestroyImmediate(linearResult); // Linear版は破棄
                    
                    Debug.Log($"プレビュー生成成功: {resultPreview.width}x{resultPreview.height}, format={resultPreview.format}, hasNodes={hasNodes}");
                }
                else
                {
                    Debug.LogWarning("プレビュー生成結果がnullです");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"プレビュー生成エラー: {e.Message}\n{e.StackTrace}");
            }
            
            // UIを再描画
            Repaint();
        }
        
        /// <summary>
        /// テクスチャをリサイズ（アスペクト比維持）
        /// 常にLinear色空間で返す
        /// </summary>
        private Texture2D ResizeTexture(Texture2D source, int maxResolution)
        {
            if (source == null) return null;
            
            // 元の解像度が小さければそのまま
            if (source.width <= maxResolution && source.height <= maxResolution)
            {
                // コピーを作成（Linear色空間）
                Texture2D copy = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false, true);
                Graphics.CopyTexture(source, copy);
                return copy;
            }
            
            // リサイズが必要な場合
            int newWidth, newHeight;
            float aspect = (float)source.width / source.height;
            
            if (source.width > source.height)
            {
                newWidth = maxResolution;
                newHeight = Mathf.RoundToInt(maxResolution / aspect);
            }
            else
            {
                newHeight = maxResolution;
                newWidth = Mathf.RoundToInt(maxResolution * aspect);
            }
            
            // RenderTextureを使ってリサイズ
            RenderTexture rt = RenderTexture.GetTemporary(newWidth, newHeight, 0, RenderTextureFormat.ARGB32);
            Graphics.Blit(source, rt);
            
            // Linear色空間でTexture2Dを作成
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = rt;
            Texture2D resized = new Texture2D(newWidth, newHeight, TextureFormat.RGBA32, false, true);
            resized.ReadPixels(new Rect(0, 0, newWidth, newHeight), 0, 0);
            resized.Apply();
            RenderTexture.active = previous;
            
            RenderTexture.ReleaseTemporary(rt);
            
            return resized;
        }
        
        private void ApplyAndSave()
        {
            if (sourceTexture == null)
            {
                EditorUtility.DisplayDialog("エラー", "ソーステクスチャが指定されていません。", "OK");
                return;
            }
            
            // 保存時はフル解像度で処理
            Texture2D fullResResult = null;
            try
            {
                // ノードを再構築（UpdatePreviewと同じロジック）
                processor.ClearNodes();
                
                if (showColorCorrection && (hueShift != 0 || saturation != 1f || brightness != 1f || gamma != 1f))
                {
                    var colorNode = new ColorCorrectionNode
                    {
                        hueShift = hueShift,
                        saturation = saturation,
                        brightness = brightness,
                        gamma = gamma
                    };
                    processor.AddNode(colorNode);
                }
                
                if (showBlend && blendTexture != null)
                {
                    var blendNode = new BlendNode
                    {
                        blendTexture = blendTexture,
                        blendMode = blendMode,
                        blendStrength = blendStrength,
                        hdrColor = hdrColor
                    };
                    processor.AddNode(blendNode);
                }
                
                if (showUVBlur)
                {
                    var uvBlurNode = new UVIslandBlurNode
                    {
                        blurRadius = blurRadius,
                        blurSigma = blurSigma
                    };
                    processor.AddNode(uvBlurNode);
                }
                
                if (showSharpen)
                {
                    var sharpenNode = new SharpenNode
                    {
                        mode = sharpenMode,
                        strength = sharpenStrength,
                        kernelSize = sharpenKernelSize
                    };
                    processor.AddNode(sharpenNode);
                }
                
                if (showToneCurve && (useRGBCurve || useRedCurve || useGreenCurve || useBlueCurve))
                {
                    var toneCurveNode = new ToneCurveNode
                    {
                        rgbCurve = rgbCurve,
                        redCurve = redCurve,
                        greenCurve = greenCurve,
                        blueCurve = blueCurve,
                        useRGBCurve = useRGBCurve,
                        useRedCurve = useRedCurve,
                        useGreenCurve = useGreenCurve,
                        useBlueCurve = useBlueCurve
                    };
                    processor.AddNode(toneCurveNode);
                }
                
                // フル解像度で処理（Linear色空間で取得）
                fullResResult = processor.GetResultAsTexture2D();
            }
            catch (System.Exception e)
            {
                Debug.LogError($"フル解像度処理エラー: {e.Message}");
                EditorUtility.DisplayDialog("エラー", "テクスチャ処理に失敗しました。", "OK");
                return;
            }
            
            if (fullResResult == null)
            {
                EditorUtility.DisplayDialog("エラー", "テクスチャの生成に失敗しました。", "OK");
                return;
            }
            
            // 色空間変換（オプション）
            Texture2D textureToSave = fullResResult;
            if (convertToSRGBOnSave)
            {
                // Linear → sRGB 変換
                textureToSave = TextureProcessor.ConvertLinearToSRGB(fullResResult);
                DestroyImmediate(fullResResult); // Linear版は破棄
                Debug.Log("保存用にLinear→sRGB変換を実行しました");
            }
            else
            {
                Debug.Log("Linear色空間のまま保存します");
            }
            
            // 保存処理
            byte[] bytes = textureToSave.EncodeToPNG();
            
            string savePath;
            if (overwriteSource)
            {
                savePath = AssetDatabase.GetAssetPath(sourceTexture);
            }
            else
            {
                // カスタムパスが指定されていればそれを使用、なければデフォルトパス
                savePath = string.IsNullOrEmpty(customOutputPath) ? outputPath : customOutputPath;
                
                if (string.IsNullOrEmpty(savePath))
                {
                    string defaultName = sourceTexture != null ? Path.GetFileNameWithoutExtension(AssetDatabase.GetAssetPath(sourceTexture)) + "_edited" : "EditedTexture";
                    savePath = EditorUtility.SaveFilePanelInProject("保存先を選択", defaultName, "png", "保存先を選択してください");
                }
            }
            
            if (string.IsNullOrEmpty(savePath))
            {
                DestroyImmediate(textureToSave);
                return;
            }
            
            File.WriteAllBytes(savePath, bytes);
            AssetDatabase.Refresh();
            
            DestroyImmediate(textureToSave);
            
            EditorUtility.DisplayDialog("成功", $"テクスチャを保存しました:\n{savePath}", "OK");
        }
        
        private void ResetParameters()
        {
            hueShift = 0f;
            saturation = 1f;
            brightness = 1f;
            gamma = 1f;
            blendStrength = 1f;
            hdrColor = Color.white;
            sharpenStrength = 1f;
            sharpenKernelSize = 5;
            
            // トーンカーブをリセット
            rgbCurve = AnimationCurve.Linear(0, 0, 1, 1);
            redCurve = AnimationCurve.Linear(0, 0, 1, 1);
            greenCurve = AnimationCurve.Linear(0, 0, 1, 1);
            blueCurve = AnimationCurve.Linear(0, 0, 1, 1);
            
            if (autoPreview)
            {
                UpdatePreview();
            }
        }
        
        /// <summary>
        /// GameObjectからMeshを抽出（MeshFilter / SkinnedMeshRenderer対応）
        /// </summary>
        private Mesh ExtractMeshFromGameObject(GameObject go)
        {
            if (go == null) return null;
            
            // MeshFilterから取得を試みる
            MeshFilter meshFilter = go.GetComponent<MeshFilter>();
            if (meshFilter != null && meshFilter.sharedMesh != null)
            {
                Debug.Log($"MeshFilterからメッシュを取得: {meshFilter.sharedMesh.name}");
                return meshFilter.sharedMesh;
            }
            
            // SkinnedMeshRendererから取得を試みる
            SkinnedMeshRenderer skinnedMeshRenderer = go.GetComponent<SkinnedMeshRenderer>();
            if (skinnedMeshRenderer != null && skinnedMeshRenderer.sharedMesh != null)
            {
                Debug.Log($"SkinnedMeshRendererからメッシュを取得: {skinnedMeshRenderer.sharedMesh.name}");
                return skinnedMeshRenderer.sharedMesh;
            }
            
            Debug.LogWarning($"GameObject '{go.name}' にMeshFilterまたはSkinnedMeshRendererが見つかりません。");
            return null;
        }
        
        /// <summary>
        /// デフォルト出力パスを更新（ソーステクスチャと同じフォルダ）
        /// </summary>
        private void UpdateDefaultOutputPath()
        {
            if (sourceTexture == null)
            {
                outputPath = "";
                return;
            }
            
            string sourcePath = AssetDatabase.GetAssetPath(sourceTexture);
            if (string.IsNullOrEmpty(sourcePath))
            {
                outputPath = "Assets/";
                return;
            }
            
            // ソースファイルと同じディレクトリ、ファイル名に "_edited" を追加
            string directory = Path.GetDirectoryName(sourcePath);
            string fileNameWithoutExt = Path.GetFileNameWithoutExtension(sourcePath);
            string extension = Path.GetExtension(sourcePath);
            
            // 拡張子がない、またはpng以外の場合はpngを使用
            if (string.IsNullOrEmpty(extension) || extension.ToLower() != ".png")
            {
                extension = ".png";
            }
            
            outputPath = Path.Combine(directory, fileNameWithoutExt + "_edited" + extension);
            
            // Windowsパス区切りをUnity形式に変換
            outputPath = outputPath.Replace("\\", "/");
            
            Debug.Log($"デフォルト出力パス設定: {outputPath}");
        }
    }
}
