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
        Overlay = 4
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
        public float blendStrength = 1f;

        public bool tiling = true;
        public Vector2 scale = Vector2.one;
        public Vector2 offset = Vector2.zero;

        private ComputeShader blendShader;
        private RenderTexture tempRT;

        public BlendNode()
        {
            nodeName = "Blend";
        }

        protected override RenderTexture ProcessInternal(RenderTexture source, RenderTexture mask = null)
        {
            if (blendTexture == null) return source;

            if (blendShader == null)
            {
                blendShader = Resources.Load<ComputeShader>("Blend");
                if (blendShader == null)
                {
                    Debug.LogError("Blend.compute not found in Resources folder!");
                    return source;
                }
            }

            EnsureRenderTexture(ref tempRT, source.width, source.height);

            int kernelIndex = blendShader.FindKernel("CSMain");

            blendShader.SetTexture(kernelIndex, "Source", source);
            blendShader.SetTexture(kernelIndex, "BlendTexture", blendTexture);
            blendShader.SetTexture(kernelIndex, "Result", tempRT);
            blendShader.SetTexture(kernelIndex, "BlendMask", blendMaskTexture != null ? blendMaskTexture : Texture2D.whiteTexture);
            blendShader.SetInt("UseBlendMask", blendMaskTexture != null ? 1 : 0);
            SetMaskParameter(blendShader, kernelIndex, mask);

            blendShader.SetInt("BlendMode", (int)blendMode);
            blendShader.SetFloat("BlendStrength", blendStrength);

            blendShader.SetInt("Tiling", tiling ? 1 : 0);
            blendShader.SetFloats("Scale", scale.x, scale.y);
            blendShader.SetFloats("Offset", offset.x, offset.y);

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
