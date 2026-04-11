using UnityEngine;
using System;

namespace UniTexEditor
{
    /// <summary>
    /// シャープネス / ぼかし処理モード
    /// </summary>
    public enum SharpenMode
    {
        Sharpen = 0,  // シャープネス（Unsharp Mask）
        Blur = 1      // ガウシアンブラー
    }

    /// <summary>
    /// シャープネス・ぼかし処理ノード
    /// </summary>
    [Serializable]
    public class SharpenNode : ProcessingNode
    {
        public SharpenMode mode = SharpenMode.Sharpen;
        public float strength = 1f;    // 0 ~ 5
        public int kernelSize = 5;     // 有効値: 3, 5, 7, 9（奇数のみ）

        private ComputeShader sharpenShader;
        private RenderTexture tempRT;

        public SharpenNode()
        {
            nodeName = "Sharpen/Blur";
        }

        protected override RenderTexture ProcessInternal(RenderTexture source, RenderTexture mask = null)
        {
            if (sharpenShader == null)
            {
                sharpenShader = Resources.Load<ComputeShader>("Sharpen");
                if (sharpenShader == null)
                {
                    Debug.LogError("Sharpen.compute not found in Resources folder!");
                    return source;
                }
            }

            // カーネルサイズを 3〜9 の奇数に正規化
            int clampedKernel = Mathf.Clamp(kernelSize, 3, 9);
            if (clampedKernel % 2 == 0) clampedKernel++;

            EnsureRenderTexture(ref tempRT, source.width, source.height);

            int kernelIndex = sharpenShader.FindKernel("CSMain");

            sharpenShader.SetTexture(kernelIndex, "Source", source);
            sharpenShader.SetTexture(kernelIndex, "Result", tempRT);
            SetMaskParameter(sharpenShader, kernelIndex, mask);

            sharpenShader.SetInt("Mode", (int)mode);
            sharpenShader.SetFloat("Strength", strength);
            sharpenShader.SetInt("KernelSize", clampedKernel);
            sharpenShader.SetInts("TextureSize", source.width, source.height);

            int threadGroupsX = Mathf.Max(1, Mathf.CeilToInt(source.width / 8.0f));
            int threadGroupsY = Mathf.Max(1, Mathf.CeilToInt(source.height / 8.0f));
            sharpenShader.Dispatch(kernelIndex, threadGroupsX, threadGroupsY, 1);

            return tempRT;
        }

        public override void Cleanup()
        {
            if (tempRT != null)
            {
                tempRT.Release();
                tempRT = null;
            }
        }
    }
}
