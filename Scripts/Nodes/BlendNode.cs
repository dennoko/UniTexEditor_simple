using UnityEngine;
using System;

namespace UniTexEditor
{
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
        public Texture2D blendMaskTexture;
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
            
            // パラメータをセット
            blendShader.SetTexture(kernelIndex, "Source", source);
            blendShader.SetTexture(kernelIndex, "BlendTexture", blendTexture);
            blendShader.SetTexture(kernelIndex, "Result", tempRT);
            blendShader.SetTexture(kernelIndex, "BlendMask", blendMaskTexture != null ? blendMaskTexture : Texture2D.whiteTexture);
            blendShader.SetInt("UseBlendMask", blendMaskTexture != null ? 1 : 0);
            
            if (mask != null)
            {
                blendShader.SetTexture(kernelIndex, "Mask", mask);
                blendShader.SetInt("UseMask", 1);
            }
            else
            {
                // マスクがない場合はダミーテクスチャを設定
                RenderTexture dummyMask = RenderTexture.GetTemporary(1, 1, 0, RenderTextureFormat.RFloat);
                blendShader.SetTexture(kernelIndex, "Mask", dummyMask);
                blendShader.SetInt("UseMask", 0);
                RenderTexture.ReleaseTemporary(dummyMask);
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
