using UnityEngine;
using System;

namespace UniTexEditor
{
    public enum ChannelSource
    {
        Red = 0,
        Green = 1,
        Blue = 2,
        Alpha = 3,
        One = 4,
        Zero = 5
    }

    [Serializable]
    public class ChannelMixerNode : ProcessingNode
    {
        public ChannelSource outRed = ChannelSource.Red;
        public ChannelSource outGreen = ChannelSource.Green;
        public ChannelSource outBlue = ChannelSource.Blue;
        public ChannelSource outAlpha = ChannelSource.Alpha;

        private ComputeShader mixerShader;
        private RenderTexture tempRT;

        public ChannelMixerNode()
        {
            nodeName = "Channel Mixer";
        }

        protected override RenderTexture ProcessInternal(RenderTexture source, RenderTexture mask = null)
        {
            // 全チャンネルがデフォルトのままなら処理不要
            if (outRed == ChannelSource.Red && outGreen == ChannelSource.Green &&
                outBlue == ChannelSource.Blue && outAlpha == ChannelSource.Alpha)
                return source;

            if (mixerShader == null)
            {
                mixerShader = Resources.Load<ComputeShader>("ChannelMixer");
                if (mixerShader == null) return source;
            }

            EnsureRenderTexture(ref tempRT, source.width, source.height);

            int kernel = mixerShader.FindKernel("CSMain");

            mixerShader.SetTexture(kernel, "Source", source);
            mixerShader.SetTexture(kernel, "Result", tempRT);
            SetMaskParameter(mixerShader, kernel, mask);

            mixerShader.SetInt("OutR", (int)outRed);
            mixerShader.SetInt("OutG", (int)outGreen);
            mixerShader.SetInt("OutB", (int)outBlue);
            mixerShader.SetInt("OutA", (int)outAlpha);

            int tx = Mathf.CeilToInt(source.width / 8.0f);
            int ty = Mathf.CeilToInt(source.height / 8.0f);
            mixerShader.Dispatch(kernel, tx, ty, 1);

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
