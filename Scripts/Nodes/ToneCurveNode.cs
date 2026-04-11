using UnityEngine;
using System;

namespace UniTexEditor
{
    /// <summary>
    /// トーンカーブによる色調整ノード
    /// </summary>
    [Serializable]
    public class ToneCurveNode : ProcessingNode
    {
        public AnimationCurve rgbCurve = AnimationCurve.Linear(0, 0, 1, 1);
        public AnimationCurve redCurve = AnimationCurve.Linear(0, 0, 1, 1);
        public AnimationCurve greenCurve = AnimationCurve.Linear(0, 0, 1, 1);
        public AnimationCurve blueCurve = AnimationCurve.Linear(0, 0, 1, 1);

        public bool useRGBCurve = true;
        public bool useRedCurve = false;
        public bool useGreenCurve = false;
        public bool useBlueCurve = false;

        private ComputeShader toneCurveShader;
        private RenderTexture tempRT;

        private ComputeBuffer rgbBuffer;
        private ComputeBuffer redBuffer;
        private ComputeBuffer greenBuffer;
        private ComputeBuffer blueBuffer;

        private const int CURVE_RESOLUTION = 256;

        // Process 呼び出しごとの配列アロケートを避けるための再利用バッファ
        private readonly float[] _lutWorkBuffer = new float[CURVE_RESOLUTION];

        // 恒等変換 LUT（[0, 1/255, 2/255, ..., 1]）をキャッシュ
        private static float[] s_identityLUT;

        private static float[] GetIdentityLUT()
        {
            if (s_identityLUT != null) return s_identityLUT;

            s_identityLUT = new float[CURVE_RESOLUTION];
            for (int i = 0; i < CURVE_RESOLUTION; i++)
                s_identityLUT[i] = i / (float)(CURVE_RESOLUTION - 1);
            return s_identityLUT;
        }

        public ToneCurveNode()
        {
            nodeName = "ToneCurve";
        }

        protected override RenderTexture ProcessInternal(RenderTexture source, RenderTexture mask = null)
        {
            if (toneCurveShader == null)
            {
                toneCurveShader = Resources.Load<ComputeShader>("ToneCurve");
                if (toneCurveShader == null)
                {
                    Debug.LogError("ToneCurve.compute not found in Resources folder!");
                    return source;
                }
            }

            EnsureRenderTexture(ref tempRT, source.width, source.height);

            // 有効なカーブはサンプリング、無効なカーブは恒等 LUT をセット
            UpdateCurveBuffer(ref rgbBuffer, rgbCurve, useRGBCurve);
            UpdateCurveBuffer(ref redBuffer, redCurve, useRedCurve);
            UpdateCurveBuffer(ref greenBuffer, greenCurve, useGreenCurve);
            UpdateCurveBuffer(ref blueBuffer, blueCurve, useBlueCurve);

            int kernelIndex = toneCurveShader.FindKernel("CSMain");

            toneCurveShader.SetTexture(kernelIndex, "Source", source);
            toneCurveShader.SetTexture(kernelIndex, "Result", tempRT);
            SetMaskParameter(toneCurveShader, kernelIndex, mask);

            toneCurveShader.SetInt("UseRGBCurve", useRGBCurve ? 1 : 0);
            toneCurveShader.SetInt("UseRedCurve", useRedCurve ? 1 : 0);
            toneCurveShader.SetInt("UseGreenCurve", useGreenCurve ? 1 : 0);
            toneCurveShader.SetInt("UseBlueCurve", useBlueCurve ? 1 : 0);

            toneCurveShader.SetBuffer(kernelIndex, "RGBCurve", rgbBuffer);
            toneCurveShader.SetBuffer(kernelIndex, "RedCurve", redBuffer);
            toneCurveShader.SetBuffer(kernelIndex, "GreenCurve", greenBuffer);
            toneCurveShader.SetBuffer(kernelIndex, "BlueCurve", blueBuffer);

            int threadGroupsX = Mathf.CeilToInt(source.width / 8.0f);
            int threadGroupsY = Mathf.CeilToInt(source.height / 8.0f);
            toneCurveShader.Dispatch(kernelIndex, threadGroupsX, threadGroupsY, 1);

            return tempRT;
        }

        /// <summary>
        /// AnimationCurve を ComputeBuffer に変換する。
        /// useIt が false の場合は恒等 LUT をアップロードして AnimationCurve のサンプリングを省く。
        /// </summary>
        private void UpdateCurveBuffer(ref ComputeBuffer buffer, AnimationCurve curve, bool useIt)
        {
            if (buffer == null || buffer.count != CURVE_RESOLUTION)
            {
                buffer?.Release();
                buffer = new ComputeBuffer(CURVE_RESOLUTION, sizeof(float));
            }

            if (!useIt)
            {
                buffer.SetData(GetIdentityLUT());
                return;
            }

            for (int i = 0; i < CURVE_RESOLUTION; i++)
                _lutWorkBuffer[i] = Mathf.Clamp01(curve.Evaluate(i / (float)(CURVE_RESOLUTION - 1)));

            buffer.SetData(_lutWorkBuffer);
        }

        public override void Cleanup()
        {
            if (tempRT != null) { tempRT.Release(); tempRT = null; }
            rgbBuffer?.Release();   rgbBuffer   = null;
            redBuffer?.Release();   redBuffer   = null;
            greenBuffer?.Release(); greenBuffer = null;
            blueBuffer?.Release();  blueBuffer  = null;
        }
    }
}
