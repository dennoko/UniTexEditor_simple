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
        
        public ToneCurveNode()
        {
            nodeName = "ToneCurve";
        }
        
        public override RenderTexture Process(RenderTexture source, RenderTexture mask = null)
        {
            if (!enabled) return source;
            
            if (toneCurveShader == null)
            {
                toneCurveShader = Resources.Load<ComputeShader>("ToneCurve");
                if (toneCurveShader == null)
                {
                    Debug.LogError("ToneCurve.compute not found in Resources folder!");
                    return source;
                }
            }
            
            // 一時的なRenderTextureを作成
            if (tempRT != null && (tempRT.width != source.width || tempRT.height != source.height))
            {
                tempRT.Release();
                tempRT = null;
            }
            
            if (tempRT == null)
            {
                tempRT = new RenderTexture(source.width, source.height, 0, RenderTextureFormat.ARGBFloat);
                tempRT.enableRandomWrite = true;
                tempRT.Create();
            }
            
            // すべてのカーブバッファを初期化・更新（使用しないものもデフォルト値で埋める）
            UpdateCurveBuffer(ref rgbBuffer, useRGBCurve ? rgbCurve : AnimationCurve.Linear(0, 0, 1, 1));
            UpdateCurveBuffer(ref redBuffer, useRedCurve ? redCurve : AnimationCurve.Linear(0, 0, 1, 1));
            UpdateCurveBuffer(ref greenBuffer, useGreenCurve ? greenCurve : AnimationCurve.Linear(0, 0, 1, 1));
            UpdateCurveBuffer(ref blueBuffer, useBlueCurve ? blueCurve : AnimationCurve.Linear(0, 0, 1, 1));
            
            int kernelIndex = toneCurveShader.FindKernel("CSMain");
            
            // パラメータをセット
            toneCurveShader.SetTexture(kernelIndex, "Source", source);
            toneCurveShader.SetTexture(kernelIndex, "Result", tempRT);
            
            if (mask != null)
            {
                toneCurveShader.SetTexture(kernelIndex, "Mask", mask);
                toneCurveShader.SetInt("UseMask", 1);
            }
            else
            {
                // マスクがない場合はダミーテクスチャを設定
                RenderTexture dummyMask = RenderTexture.GetTemporary(1, 1, 0, RenderTextureFormat.RFloat);
                toneCurveShader.SetTexture(kernelIndex, "Mask", dummyMask);
                toneCurveShader.SetInt("UseMask", 0);
                RenderTexture.ReleaseTemporary(dummyMask);
            }
            
            // カーブバッファをセット（常にすべて設定）
            toneCurveShader.SetInt("UseRGBCurve", useRGBCurve ? 1 : 0);
            toneCurveShader.SetInt("UseRedCurve", useRedCurve ? 1 : 0);
            toneCurveShader.SetInt("UseGreenCurve", useGreenCurve ? 1 : 0);
            toneCurveShader.SetInt("UseBlueCurve", useBlueCurve ? 1 : 0);
            
            toneCurveShader.SetBuffer(kernelIndex, "RGBCurve", rgbBuffer);
            toneCurveShader.SetBuffer(kernelIndex, "RedCurve", redBuffer);
            toneCurveShader.SetBuffer(kernelIndex, "GreenCurve", greenBuffer);
            toneCurveShader.SetBuffer(kernelIndex, "BlueCurve", blueBuffer);
            
            // ディスパッチ
            int threadGroupsX = Mathf.CeilToInt(source.width / 8.0f);
            int threadGroupsY = Mathf.CeilToInt(source.height / 8.0f);
            toneCurveShader.Dispatch(kernelIndex, threadGroupsX, threadGroupsY, 1);
            
            return tempRT;
        }
        
        /// <summary>
        /// AnimationCurveをComputeBufferに変換
        /// </summary>
        private void UpdateCurveBuffer(ref ComputeBuffer buffer, AnimationCurve curve)
        {
            if (buffer == null || buffer.count != CURVE_RESOLUTION)
            {
                if (buffer != null) buffer.Release();
                buffer = new ComputeBuffer(CURVE_RESOLUTION, sizeof(float));
            }
            
            float[] lookupTable = new float[CURVE_RESOLUTION];
            for (int i = 0; i < CURVE_RESOLUTION; i++)
            {
                float t = i / (float)(CURVE_RESOLUTION - 1);
                lookupTable[i] = Mathf.Clamp01(curve.Evaluate(t));
            }
            
            buffer.SetData(lookupTable);
        }
        
        public override void Cleanup()
        {
            if (tempRT != null)
            {
                tempRT.Release();
                tempRT = null;
            }
            
            if (rgbBuffer != null)
            {
                rgbBuffer.Release();
                rgbBuffer = null;
            }
            
            if (redBuffer != null)
            {
                redBuffer.Release();
                redBuffer = null;
            }
            
            if (greenBuffer != null)
            {
                greenBuffer.Release();
                greenBuffer = null;
            }
            
            if (blueBuffer != null)
            {
                blueBuffer.Release();
                blueBuffer = null;
            }
        }
    }
}
