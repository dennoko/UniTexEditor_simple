using UnityEngine;
using UnityEditor;
using System.IO;

namespace UniTexEditor
{
    public partial class UniTexEditorWindow
    {
        /// <summary>
        /// プレビューの再計算をリクエスト
        /// 変更検知から直接呼び出し、OnGUI処理終了後に確実にリビルドさせる
        /// </summary>
        private void RequestPreviewUpdate(bool forceImmediate = false)
        {
            previewDirty = true;

            if (forceImmediate)
            {
                UpdatePreview();
                return;
            }

            if (!autoPreview)
            {
                Repaint();
                return;
            }

            if (previewUpdateScheduled)
            {
                return;
            }

            previewUpdateScheduled = true;
            EditorApplication.delayCall += ProcessPendingPreview;
        }

        /// <summary>
        /// 遅延呼び出しで実際のプレビュー再計算を行う
        /// </summary>
        private void ProcessPendingPreview()
        {
            previewUpdateScheduled = false;

            if (!autoPreview || !previewDirty)
            {
                return;
            }

            UpdatePreview();
        }
        
        private void UpdatePreview()
        {
            EditorApplication.delayCall -= ProcessPendingPreview;
            previewUpdateScheduled = false;

            if (sourceTexture == null)
            {
                previewDirty = false;
                return;
            }
            
            // マスク設定を適用
            processor.MaskTexture = maskTexture;
            processor.InvertMask = invertMask;
            processor.MaskStrength = maskStrength;
            
            // 既存のプレビューを破棄
            if (resultPreview != null)
            {
                DestroyImmediate(resultPreview);
                resultPreview = null;
            }

            previewDirty = false;
            
            // ノードをクリアして再構築
            processor.ClearNodes();
            
            bool hasNodes = false;
            
            // 色調補正ノードを追加
            if (showColorCorrection && (hueShift != 0 || saturation != 1f || brightness != 1f || gamma != 1f || ccBlendOpacity > 0f))
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
                hasNodes = true;
            }
            
            // ブレンドノードを追加
            if (showBlend && blendTexture != null)
            {
                var blendNode = new BlendNode
                {
                    blendTexture = blendTexture,
                    blendMaskTexture = blendMaskTexture,
                    blendMode = blendMode,
                    blendStrength = blendStrength,
                    hdrColor = hdrColor
                };
                processor.AddNode(blendNode);
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
            
            // Channel Mixer (Advanced)
            if (showChannelMixer)
            {
                var cmNode = new ChannelMixerNode
                {
                    outRed = cmOutRed,
                    outGreen = cmOutGreen,
                    outBlue = cmOutBlue,
                    outAlpha = cmOutAlpha
                };
                processor.AddNode(cmNode);
                hasNodes = true;
            }
            
            // Levels (Advanced)
            if (showLevels)
            {
                var lvlNode = new LevelsNode
                {
                    minInput = lvlMinInput,
                    maxInput = lvlMaxInput,
                    minOutput = lvlMinOutput,
                    maxOutput = lvlMaxOutput,
                    midGamma = lvlMidGamma
                };
                processor.AddNode(lvlNode);
                hasNodes = true;
            }
            
            // プレビューを生成（Texture2Dとしてコピー）
            // パフォーマンスのため512x512に制限
            try
            {
                Texture2D linearResult;
                
                // ノード有無に関係なく処理パイプラインを通してLinear色空間の結果を取得
                linearResult = processor.GetResultAsTexture2D(PREVIEW_RESOLUTION);
                
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
                processor.MaskTexture = maskTexture;
                processor.InvertMask = invertMask;
                processor.MaskStrength = maskStrength;
                
                if (showColorCorrection && (hueShift != 0 || saturation != 1f || brightness != 1f || gamma != 1f || ccBlendOpacity > 0f))
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
                        hdrColor = hdrColor
                    };
                    processor.AddNode(blendNode);
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
                
                if (showChannelMixer)
                {
                    var cmNode = new ChannelMixerNode
                    {
                        outRed = cmOutRed,
                        outGreen = cmOutGreen,
                        outBlue = cmOutBlue,
                        outAlpha = cmOutAlpha
                    };
                    processor.AddNode(cmNode);
                }
                
                if (showLevels)
                {
                    var lvlNode = new LevelsNode
                    {
                        minInput = lvlMinInput,
                        maxInput = lvlMaxInput,
                        minOutput = lvlMinOutput,
                        maxOutput = lvlMaxOutput,
                        midGamma = lvlMidGamma
                    };
                    processor.AddNode(lvlNode);
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
                    string timestamp = System.DateTime.Now.ToString("yyMMdd_HHmmss");
                    string defaultName = sourceTexture != null ? Path.GetFileNameWithoutExtension(AssetDatabase.GetAssetPath(sourceTexture)) + $"_edited_{timestamp}" : $"EditedTexture_{timestamp}";
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
            ccTargetColor = Color.white;
            ccBlendMode = BlendMode.Normal;
            ccBlendOpacity = 0f;
            blendStrength = 1f;
            hdrColor = Color.white;
            sharpenStrength = 1f;
            sharpenKernelSize = 5;
            
            // トーンカーブをリセット
            rgbCurve = AnimationCurve.Linear(0, 0, 1, 1);
            redCurve = AnimationCurve.Linear(0, 0, 1, 1);
            greenCurve = AnimationCurve.Linear(0, 0, 1, 1);
            blueCurve = AnimationCurve.Linear(0, 0, 1, 1);
            
            // Advanced Reset
            lvlMinInput = 0f;
            lvlMaxInput = 1f;
            lvlMinOutput = 0f;
            lvlMaxOutput = 1f;
            lvlMidGamma = 1f;
            
            cmOutRed = ChannelSource.Red;
            cmOutGreen = ChannelSource.Green;
            cmOutBlue = ChannelSource.Blue;
            cmOutAlpha = ChannelSource.Alpha;
            
            RequestPreviewUpdate();
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
            
            // ソースファイルと同じディレクトリ、ファイル名に "_edited_yyMMdd_HHmmss" を追加
            string directory = Path.GetDirectoryName(sourcePath);
            string fileNameWithoutExt = Path.GetFileNameWithoutExtension(sourcePath);
            string extension = Path.GetExtension(sourcePath);
            
            // 拡張子がない、またはpng以外の場合はpngを使用
            if (string.IsNullOrEmpty(extension) || extension.ToLower() != ".png")
            {
                extension = ".png";
            }
            
            // タイムスタンプを付与
            string timestamp = System.DateTime.Now.ToString("yyMMdd_HHmmss");
            string newFileName = $"{fileNameWithoutExt}_edited_{timestamp}{extension}";
            
            outputPath = Path.Combine(directory, newFileName);
            
            // Windowsパス区切りをUnity形式に変換
            outputPath = outputPath.Replace("\\", "/");
            
            Debug.Log($"デフォルト出力パス設定: {outputPath}");
        }
    }
}
