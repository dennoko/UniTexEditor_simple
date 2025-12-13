using UnityEngine;
using System;

namespace UniTexEditor
{
    /// <summary>
    /// 色調補正ノード（Hue/Saturation/Brightness/Gamma）
    /// </summary>
    [Serializable]
    public class ColorCorrectionNode : ProcessingNode
    {
        public float hueShift = 0f;        // -180 ~ 180
        public float saturation = 1f;      // 0 ~ 2
        public float brightness = 1f;      // 0 ~ 2
        public float gamma = 1f;           // 0.1 ~ 3
        
        public Color targetColor = Color.white;
        public BlendMode blendMode = BlendMode.Normal;
        public float blendOpacity = 0f;
        
        private ComputeShader colorCorrectionShader;
        private RenderTexture tempRT;
        
        public ColorCorrectionNode()
        {
            nodeName = "ColorCorrection";
        }
        
        public override RenderTexture Process(RenderTexture source, RenderTexture mask = null)
        {
            if (!enabled) return source;
            
            if (colorCorrectionShader == null)
            {
                colorCorrectionShader = Resources.Load<ComputeShader>("ColorCorrection");
                if (colorCorrectionShader == null)
                {
                    Debug.LogError("ColorCorrection.compute not found in Resources folder!");
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
            
            int kernelIndex = colorCorrectionShader.FindKernel("CSMain");
            
            // パラメータをセット
            colorCorrectionShader.SetTexture(kernelIndex, "Source", source);
            colorCorrectionShader.SetTexture(kernelIndex, "Result", tempRT);
            
            if (mask != null)
            {
                colorCorrectionShader.SetTexture(kernelIndex, "Mask", mask);
                colorCorrectionShader.SetInt("UseMask", 1);
            }
            else
            {
                // マスクがない場合はダミーテクスチャを設定（Compute Shaderのエラー回避）
                RenderTexture dummyMask = RenderTexture.GetTemporary(1, 1, 0, RenderTextureFormat.RFloat);
                colorCorrectionShader.SetTexture(kernelIndex, "Mask", dummyMask);
                colorCorrectionShader.SetInt("UseMask", 0);
                RenderTexture.ReleaseTemporary(dummyMask);
            }
            
            colorCorrectionShader.SetFloat("HueShift", hueShift);
            colorCorrectionShader.SetFloat("Saturation", saturation);
            colorCorrectionShader.SetFloat("Brightness", brightness);
            colorCorrectionShader.SetFloat("Gamma", gamma);
            
            colorCorrectionShader.SetVector("TargetColor", targetColor);
            colorCorrectionShader.SetInt("BlendMode", (int)blendMode);
            colorCorrectionShader.SetFloat("BlendOpacity", blendOpacity);
            
            // ディスパッチ
            int threadGroupsX = Mathf.CeilToInt(source.width / 8.0f);
            int threadGroupsY = Mathf.CeilToInt(source.height / 8.0f);
            colorCorrectionShader.Dispatch(kernelIndex, threadGroupsX, threadGroupsY, 1);
            
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
