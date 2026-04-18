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
        public float strength = 1f;    // Sharpen: 0~2, Blur: 0~5 (sigma値)
        public int kernelSize = 5;     // 有効値: 3, 5, 7, 9（奇数のみ）
        public int iterations = 1;     // 1~8: シェーダーを繰り返す回数

        private ComputeShader sharpenShader;
        private RenderTexture tempRT;
        private RenderTexture pingPongRT;

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

            int clampedIterations = Mathf.Clamp(iterations, 1, 8);

            EnsureRenderTexture(ref tempRT, source.width, source.height);
            if (clampedIterations > 1)
                EnsureRenderTexture(ref pingPongRT, source.width, source.height);

            int kernelIndex = sharpenShader.FindKernel("CSMain");
            int threadGroupsX = Mathf.Max(1, Mathf.CeilToInt(source.width / 8.0f));
            int threadGroupsY = Mathf.Max(1, Mathf.CeilToInt(source.height / 8.0f));

            sharpenShader.SetInt("Mode", (int)mode);
            sharpenShader.SetFloat("Strength", strength);
            sharpenShader.SetInt("KernelSize", clampedKernel);
            sharpenShader.SetInts("TextureSize", source.width, source.height);

            // 1回目: source → tempRT（常にこのパスを通る）
            sharpenShader.SetTexture(kernelIndex, "Source", source);
            sharpenShader.SetTexture(kernelIndex, "Result", tempRT);
            SetMaskParameter(sharpenShader, kernelIndex, mask);
            sharpenShader.Dispatch(kernelIndex, threadGroupsX, threadGroupsY, 1);

            if (clampedIterations == 1)
                return tempRT;

            // 2回目以降: tempRT ↔ pingPongRT をping-pongしながら繰り返す
            RenderTexture lastResult = tempRT;
            for (int i = 1; i < clampedIterations; i++)
            {
                RenderTexture src = (i % 2 == 1) ? tempRT    : pingPongRT;
                RenderTexture dst = (i % 2 == 1) ? pingPongRT : tempRT;

                sharpenShader.SetTexture(kernelIndex, "Source", src);
                sharpenShader.SetTexture(kernelIndex, "Result", dst);
                SetMaskParameter(sharpenShader, kernelIndex, mask);
                sharpenShader.Dispatch(kernelIndex, threadGroupsX, threadGroupsY, 1);

                lastResult = dst;
            }

            return lastResult;
        }

        public override void Cleanup()
        {
            if (tempRT != null)
            {
                tempRT.Release();
                tempRT = null;
            }
            if (pingPongRT != null)
            {
                pingPongRT.Release();
                pingPongRT = null;
            }
        }
    }
}
