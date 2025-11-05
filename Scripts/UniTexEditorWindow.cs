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
        
        // マスクオプション
        private bool useMask = false;
        private bool invertMask = false;
        private float maskStrength = 1f;
        
        // 出力オプション
        private bool overwriteSource = false;
        private string outputPath = "Assets/";
        
        // プレビュー
        private bool autoPreview = true;
        private Vector2 scrollPosition;
        private Vector2 previewScrollPosition;
        private const float PREVIEW_MAX_SIZE = 512f; // プレビュー最大サイズ
        
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
                if (autoPreview) UpdatePreview();
            }
            
            EditorGUILayout.Space();
            
            // ===== マスクセクション =====
            useMask = EditorGUILayout.Toggle("マスクを使用", useMask);
            if (useMask)
            {
                EditorGUI.indentLevel++;
                EditorGUI.BeginChangeCheck();
                maskTexture = (Texture2D)EditorGUILayout.ObjectField("マスクテクスチャ", maskTexture, typeof(Texture2D), false);
                if (EditorGUI.EndChangeCheck())
                {
                    processor.MaskTexture = maskTexture;
                    if (autoPreview) UpdatePreview();
                }
                invertMask = EditorGUILayout.Toggle("マスク反転", invertMask);
                maskStrength = EditorGUILayout.Slider("マスク強度", maskStrength, 0f, 1f);
                EditorGUI.indentLevel--;
            }
            
            EditorGUILayout.Space();
            
            // ===== メッシュ（UV Blur用）セクション =====
            GUILayout.Label("メッシュ（UV Blur用、オプション）", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            sourceMesh = (Mesh)EditorGUILayout.ObjectField("メッシュ", sourceMesh, typeof(Mesh), false);
            if (EditorGUI.EndChangeCheck() && autoPreview)
            {
                UpdatePreview();
            }
            
            if (sourceMesh != null)
            {
                EditorGUILayout.HelpBox("メッシュが指定されています。UVアイランドブラーが利用可能です。", MessageType.Info);
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
            
            // ===== UV アイランドブラーセクション =====
            GUI.enabled = sourceMesh != null;
            showUVBlur = EditorGUILayout.Foldout(showUVBlur, "UVアイランドブラー", true);
            if (showUVBlur && sourceMesh != null)
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
                EditorGUILayout.HelpBox("UVアイランドの境界を越えないブラー処理を適用します。", MessageType.Info);
            }
            GUI.enabled = true;
            
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
                
                // 表示可能領域を取得
                float availableWidth = EditorGUIUtility.currentViewWidth - 40f;
                float maxSize = Mathf.Min(PREVIEW_MAX_SIZE, availableWidth);
                
                // アスペクト比を維持しつつ、正方形の表示領域を確保
                float previewSize = maxSize;
                
                // テクスチャのアスペクト比を計算
                float textureAspect = (float)resultPreview.width / resultPreview.height;
                float displayWidth, displayHeight;
                
                if (textureAspect > 1f)
                {
                    // 横長のテクスチャ
                    displayWidth = previewSize;
                    displayHeight = previewSize / textureAspect;
                }
                else
                {
                    // 縦長または正方形のテクスチャ
                    displayWidth = previewSize * textureAspect;
                    displayHeight = previewSize;
                }
                
                // 中央配置のために余白を計算
                float xOffset = (previewSize - displayWidth) * 0.5f;
                
                EditorGUILayout.BeginVertical(GUILayout.Width(previewSize), GUILayout.Height(previewSize));
                GUILayout.Space(xOffset > 0 ? 0 : (previewSize - displayHeight) * 0.5f);
                
                EditorGUILayout.BeginHorizontal();
                if (xOffset > 0) GUILayout.Space(xOffset);
                
                Rect previewRect = GUILayoutUtility.GetRect(displayWidth, displayHeight);
                
                // 背景（チェッカーボード）を描画
                DrawCheckerboard(previewRect);
                
                // プレビューを描画
                EditorGUI.DrawPreviewTexture(previewRect, resultPreview);
                
                if (xOffset > 0) GUILayout.Space(xOffset);
                EditorGUILayout.EndHorizontal();
                
                EditorGUILayout.EndVertical();
            }
            
            EditorGUILayout.Space();
            
            // ===== 出力セクション =====
            GUILayout.Label("出力", EditorStyles.boldLabel);
            overwriteSource = EditorGUILayout.Toggle("ソースを上書き", overwriteSource);
            
            if (!overwriteSource)
            {
                EditorGUILayout.BeginHorizontal();
                outputPath = EditorGUILayout.TextField("出力パス", outputPath);
                if (GUILayout.Button("...", GUILayout.Width(30)))
                {
                    string path = EditorUtility.SaveFilePanelInProject("保存先を選択", "EditedTexture", "png", "保存先を選択してください");
                    if (!string.IsNullOrEmpty(path))
                    {
                        outputPath = path;
                    }
                }
                EditorGUILayout.EndHorizontal();
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
            }
            
            // UVブラーノードを追加
            if (showUVBlur && sourceMesh != null)
            {
                var uvBlurNode = new UVIslandBlurNode
                {
                    sourceMesh = sourceMesh,
                    blurRadius = blurRadius,
                    blurSigma = blurSigma
                };
                processor.AddNode(uvBlurNode);
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
            }
            
            // プレビューを生成（Texture2Dとしてコピー）
            try
            {
                Texture2D result = processor.GetResultAsTexture2D();
                if (result != null)
                {
                    resultPreview = result;
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"プレビュー生成エラー: {e.Message}");
            }
            
            // UIを再描画
            Repaint();
        }
        
        private void ApplyAndSave()
        {
            if (sourceTexture == null)
            {
                EditorUtility.DisplayDialog("エラー", "ソーステクスチャが指定されていません。", "OK");
                return;
            }
            
            UpdatePreview();
            
            if (resultPreview == null)
            {
                EditorUtility.DisplayDialog("エラー", "プレビューの生成に失敗しました。", "OK");
                return;
            }
            
            // 保存処理
            byte[] bytes = resultPreview.EncodeToPNG();
            
            string savePath;
            if (overwriteSource)
            {
                savePath = AssetDatabase.GetAssetPath(sourceTexture);
            }
            else
            {
                savePath = outputPath;
                if (string.IsNullOrEmpty(savePath))
                {
                    savePath = EditorUtility.SaveFilePanelInProject("保存先を選択", "EditedTexture", "png", "保存先を選択してください");
                }
            }
            
            if (string.IsNullOrEmpty(savePath))
            {
                return;
            }
            
            File.WriteAllBytes(savePath, bytes);
            AssetDatabase.Refresh();
            
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
    }
}
