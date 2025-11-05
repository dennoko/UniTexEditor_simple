using UnityEngine;
using System;

namespace UniTexEditor
{
    /// <summary>
    /// 処理ノードの基底クラス（非破壊レイヤシステムの基礎）
    /// </summary>
    [Serializable]
    public abstract class ProcessingNode
    {
        public string nodeName = "Node";
        public bool enabled = true;
        
        /// <summary>
        /// このノードの処理を実行
        /// </summary>
        /// <param name="source">入力テクスチャ</param>
        /// <param name="mask">マスクテクスチャ（オプション）</param>
        /// <returns>処理済みRenderTexture</returns>
        public abstract RenderTexture Process(RenderTexture source, RenderTexture mask = null);
        
        /// <summary>
        /// ノードのクリーンアップ
        /// </summary>
        public virtual void Cleanup()
        {
            // サブクラスで必要に応じてオーバーライド
        }
    }
    
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
                colorCorrectionShader.SetInt("UseMask", 0);
            }
            
            colorCorrectionShader.SetFloat("HueShift", hueShift);
            colorCorrectionShader.SetFloat("Saturation", saturation);
            colorCorrectionShader.SetFloat("Brightness", brightness);
            colorCorrectionShader.SetFloat("Gamma", gamma);
            
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
    
    /// <summary>
    /// ブレンドモード
    /// </summary>
    public enum BlendMode
    {
        Normal = 0,
        Multiply = 1,
        Add = 2,
        Screen = 3,
        Overlay = 4,
        HDRAdd = 5,
        HDRMultiply = 6
    }
    
    /// <summary>
    /// テクスチャ合成ノード
    /// </summary>
    [Serializable]
    public class BlendNode : ProcessingNode
    {
        public Texture2D blendTexture;
        public BlendMode blendMode = BlendMode.Normal;
        public float blendStrength = 1f;  // 0 ~ 1
        public Color hdrColor = Color.white;  // HDR合成用
        
        private ComputeShader blendShader;
        private RenderTexture tempRT;
        
        public BlendNode()
        {
            nodeName = "Blend";
        }
        
        public override RenderTexture Process(RenderTexture source, RenderTexture mask = null)
        {
            if (!enabled || blendTexture == null) return source;
            
            if (blendShader == null)
            {
                blendShader = Resources.Load<ComputeShader>("Blend");
                if (blendShader == null)
                {
                    Debug.LogError("Blend.compute not found in Resources folder!");
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
            
            int kernelIndex = blendShader.FindKernel("CSMain");
            
            blendShader.SetTexture(kernelIndex, "Source", source);
            blendShader.SetTexture(kernelIndex, "BlendTexture", blendTexture);
            blendShader.SetTexture(kernelIndex, "Result", tempRT);
            
            if (mask != null)
            {
                blendShader.SetTexture(kernelIndex, "Mask", mask);
                blendShader.SetInt("UseMask", 1);
            }
            else
            {
                blendShader.SetInt("UseMask", 0);
            }
            
            blendShader.SetInt("BlendMode", (int)blendMode);
            blendShader.SetFloat("BlendStrength", blendStrength);
            blendShader.SetVector("HDRColor", new Vector4(hdrColor.r, hdrColor.g, hdrColor.b, hdrColor.a));
            
            int threadGroupsX = Mathf.CeilToInt(source.width / 8.0f);
            int threadGroupsY = Mathf.CeilToInt(source.height / 8.0f);
            blendShader.Dispatch(kernelIndex, threadGroupsX, threadGroupsY, 1);
            
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
