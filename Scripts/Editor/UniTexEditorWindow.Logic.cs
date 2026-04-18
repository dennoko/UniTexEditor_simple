using UnityEngine;
using UnityEditor;
using System.IO;

namespace UniTexEditor
{
    public partial class UniTexEditorWindow
    {
        // ─── プレビュー更新フロー ─────────────────────────────────────────

        /// <summary>
        /// プレビューの再計算をリクエストする。
        /// 変更検知から直接呼び出し、OnGUI 処理終了後に確実にリビルドさせる。
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

            if (previewUpdateScheduled) return;

            previewUpdateScheduled = true;
            EditorApplication.delayCall += ProcessPendingPreview;
        }

        /// <summary>
        /// 遅延呼び出しで実際のプレビュー再計算を行う
        /// </summary>
        private void ProcessPendingPreview()
        {
            previewUpdateScheduled = false;

            if (!autoPreview || !previewDirty) return;

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

            if (resultPreview != null)
            {
                DestroyImmediate(resultPreview);
                resultPreview = null;
            }

            previewDirty = false;

            ConfigureProcessor();

            try
            {
                Texture2D linearResult = processor.GetResultAsTexture2D(PREVIEW_RESOLUTION);
                if (linearResult != null)
                {
                    resultPreview = TextureProcessor.ConvertLinearToSRGB(linearResult);
                    DestroyImmediate(linearResult);
                }
                else
                {
                    Debug.LogWarning("プレビュー生成結果が null です");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"プレビュー生成エラー: {e.Message}\n{e.StackTrace}");
            }

            Repaint();
        }

        // ─── ノード構築（唯一の実装場所） ────────────────────────────────

        /// <summary>
        /// 現在の GUI パラメータに基づいて processor を再構成する。
        /// UpdatePreview / ApplyAndSave / GenerateColorVariations の共通処理。
        /// </summary>
        private void ConfigureProcessor()
        {
            processor.MaskTexture = maskTexture;
            processor.InvertMask = invertMask;
            processor.MaskStrength = maskStrength;

            processor.ClearNodes();

            if (showColorCorrection && (hueShift != 0 || saturation != 1f || brightness != 1f || gamma != 1f || ccBlendOpacity > 0f))
            {
                processor.AddNode(new ColorCorrectionNode
                {
                    hueShift = hueShift,
                    saturation = saturation,
                    brightness = brightness,
                    gamma = gamma,
                    targetColor = ccTargetColor,
                    blendMode = ccBlendMode,
                    blendOpacity = ccBlendOpacity
                });
            }

            if (showBlend && blendTexture != null)
            {
                processor.AddNode(new BlendNode
                {
                    blendTexture = blendTexture,
                    blendMaskTexture = blendMaskTexture,
                    blendMode = blendMode,
                    blendStrength = blendStrength,
                    tiling = blendTiling,
                    scale = blendScale,
                    offset = blendOffset
                });
            }

            if (showToneCurve && (useRGBCurve || useRedCurve || useGreenCurve || useBlueCurve))
            {
                processor.AddNode(new ToneCurveNode
                {
                    rgbCurve = rgbCurve,
                    redCurve = redCurve,
                    greenCurve = greenCurve,
                    blueCurve = blueCurve,
                    useRGBCurve = useRGBCurve,
                    useRedCurve = useRedCurve,
                    useGreenCurve = useGreenCurve,
                    useBlueCurve = useBlueCurve
                });
            }

            if (showSharpen)
            {
                processor.AddNode(new SharpenNode
                {
                    mode = sharpenMode,
                    strength = sharpenStrength,
                    kernelSize = sharpenKernelSize,
                    iterations = sharpenIterations
                });
            }

            if (showChannelMixer)
            {
                processor.AddNode(new ChannelMixerNode
                {
                    outRed = cmOutRed,
                    outGreen = cmOutGreen,
                    outBlue = cmOutBlue,
                    outAlpha = cmOutAlpha
                });
            }

            if (showLevels)
            {
                processor.AddNode(new LevelsNode
                {
                    minInput = lvlMinInput,
                    maxInput = lvlMaxInput,
                    minOutput = lvlMinOutput,
                    maxOutput = lvlMaxOutput,
                    midGamma = lvlMidGamma
                });
            }
        }

        // ─── 保存処理 ────────────────────────────────────────────────────

        private void ApplyAndSave()
        {
            if (sourceTexture == null)
            {
                SetStatus("Error: Source Texture is not set.", StatusType.Error);
                return;
            }

            // フル解像度で処理（Linear 色空間で取得）
            Texture2D fullResResult = null;
            try
            {
                ConfigureProcessor();
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

            // Linear → sRGB 変換して PNG エンコード
            Texture2D textureToSave = TextureProcessor.ConvertLinearToSRGB(fullResResult);
            DestroyImmediate(fullResResult);

            byte[] bytes = textureToSave.EncodeToPNG();
            DestroyImmediate(textureToSave); // エンコード後は不要なので即解放

            // 保存先パスを決定
            string savePath = ResolveSavePath();
            if (string.IsNullOrEmpty(savePath)) return;

            File.WriteAllBytes(savePath, bytes);
            AssetDatabase.Refresh();

            SetStatus($"Success: Saved to {savePath}", StatusType.Success);
        }

        /// <summary>
        /// 保存先パスを決定する。
        /// overwriteSource → ソースパスを上書き
        /// customOutputPath → ユーザー指定パス
        /// それ以外 → ソースと同フォルダにタイムスタンプ付きで自動生成
        /// </summary>
        private string ResolveSavePath()
        {
            if (overwriteSource)
                return AssetDatabase.GetAssetPath(sourceTexture);

            if (!string.IsNullOrEmpty(customOutputPath))
                return customOutputPath;

            // 自動生成：保存実行時のタイムスタンプを使用して常に新鮮なファイル名にする
            string srcPath = AssetDatabase.GetAssetPath(sourceTexture);
            string dir = string.IsNullOrEmpty(srcPath) ? "Assets" : Path.GetDirectoryName(srcPath);
            string baseName = string.IsNullOrEmpty(srcPath)
                ? "EditedTexture"
                : Path.GetFileNameWithoutExtension(srcPath);
            string timestamp = System.DateTime.Now.ToString("yyMMdd_HHmmss");
            return Path.Combine(dir, $"{baseName}_edited_{timestamp}.png").Replace("\\", "/");
        }

        // ─── リセット ───────────────────────────────────────────────────

        private void ResetParameters()
        {
            hueShift = 0f;
            saturation = 1f;
            brightness = 1f;
            gamma = 1f;         // 以前欠落していた gamma のリセットを追加

            ccTargetColor = Color.white;
            ccBlendMode = BlendMode.Normal;
            ccBlendOpacity = 0f;

            blendTexture = null;
            blendMaskTexture = null;
            blendMode = BlendMode.Normal;
            blendStrength = 1f;
            blendTiling = true;
            blendScale = Vector2.one;
            blendOffset = Vector2.zero;

            sharpenMode = SharpenMode.Sharpen;
            sharpenStrength = 1f;
            sharpenKernelSize = 5;
            sharpenIterations = 1;

            rgbCurve = AnimationCurve.Linear(0, 0, 1, 1);
            redCurve = AnimationCurve.Linear(0, 0, 1, 1);
            greenCurve = AnimationCurve.Linear(0, 0, 1, 1);
            blueCurve = AnimationCurve.Linear(0, 0, 1, 1);
            useRGBCurve = true;
            useRedCurve = false;
            useGreenCurve = false;
            useBlueCurve = false;

            lvlMinInput = 0f;
            lvlMaxInput = 1f;
            lvlMinOutput = 0f;
            lvlMaxOutput = 1f;
            lvlMidGamma = 1f;

            cmOutRed = ChannelSource.Red;
            cmOutGreen = ChannelSource.Green;
            cmOutBlue = ChannelSource.Blue;
            cmOutAlpha = ChannelSource.Alpha;

            SetStatus("Parameters have been reset.", StatusType.Info);
            RequestPreviewUpdate();
        }

        // ─── ステータス ─────────────────────────────────────────────────

        /// <summary>
        /// ステータスバーの表示を設定する。
        /// Info 以外のタイプは 5 秒後に自動的に Info へ戻る。
        /// </summary>
        private void SetStatus(string message, StatusType type)
        {
            statusMessage = message;
            statusType = type;
            _statusResetTime = (type != StatusType.Info)
                ? EditorApplication.timeSinceStartup + 5.0
                : -1.0;
            Repaint();
        }

        // ─── マスク反転保存 ──────────────────────────────────────────────

        /// <summary>
        /// マスク画像を反転して保存する
        /// </summary>
        private void SaveInvertedMask()
        {
            if (maskTexture == null)
            {
                EditorUtility.DisplayDialog("Error", "Mask Texture is not set.", "OK");
                return;
            }

            string defaultName = maskTexture.name + "_inverted";
            string path = EditorUtility.SaveFilePanel("Save Inverted Mask", "Assets", defaultName, "png");
            if (string.IsNullOrEmpty(path)) return;

            // new RenderTexture を使用して enableRandomWrite を Create() 前に設定する
            // （GetTemporary は既に作成済みのため enableRandomWrite を後から変更できない）
            var rt = new RenderTexture(maskTexture.width, maskTexture.height, 0, RenderTextureFormat.RFloat);
            rt.enableRandomWrite = true;
            rt.Create();

            var maskShader = Resources.Load<ComputeShader>("MaskProcess");
            if (maskShader != null)
            {
                int kernel = maskShader.FindKernel("CSMain");
                maskShader.SetTexture(kernel, "Source", maskTexture);
                maskShader.SetTexture(kernel, "Result", rt);
                maskShader.SetInt("Invert", 1);
                maskShader.SetFloat("Strength", 1.0f);

                int tx = Mathf.CeilToInt(maskTexture.width / 8.0f);
                int ty = Mathf.CeilToInt(maskTexture.height / 8.0f);
                maskShader.Dispatch(kernel, tx, ty, 1);
            }
            else
            {
                Debug.LogWarning("MaskProcess compute shader not found.");
            }

            Texture2D result = TextureProcessor.RenderTextureToTexture2D(rt);
            rt.Release();

            byte[] bytes = result.EncodeToPNG();
            DestroyImmediate(result);

            File.WriteAllBytes(path, bytes);
            AssetDatabase.Refresh();
            SetStatus(Localization.GetText("msg_save_inverted_success") + Path.GetFileName(path), StatusType.Success);
        }
    }
}
