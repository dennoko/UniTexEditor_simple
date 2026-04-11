using UnityEngine;
using System;

namespace UniTexEditor
{
    [Serializable]
    public class LevelsNode : ProcessingNode
    {
        public float minInput = 0f;
        public float maxInput = 1f;
        public float minOutput = 0f;
        public float maxOutput = 1f;
        public float midGamma = 1f;

        private ComputeShader levelsShader;
        private RenderTexture tempRT;

        public LevelsNode()
        {
            nodeName = "Levels";
        }

        protected override RenderTexture ProcessInternal(RenderTexture source, RenderTexture mask = null)
        {
            // 全パラメータがデフォルトなら処理不要
            if (minInput == 0f && maxInput == 1f && minOutput == 0f && maxOutput == 1f && midGamma == 1f)
                return source;

            if (levelsShader == null)
            {
                levelsShader = Resources.Load<ComputeShader>("Levels");
                if (levelsShader == null) return source;
            }

            EnsureRenderTexture(ref tempRT, source.width, source.height);

            int kernel = levelsShader.FindKernel("CSMain");

            levelsShader.SetTexture(kernel, "Source", source);
            levelsShader.SetTexture(kernel, "Result", tempRT);
            SetMaskParameter(levelsShader, kernel, mask);

            levelsShader.SetFloat("MinInput", minInput);
            levelsShader.SetFloat("MaxInput", maxInput);
            levelsShader.SetFloat("MinOutput", minOutput);
            levelsShader.SetFloat("MaxOutput", maxOutput);
            levelsShader.SetFloat("MidGamma", midGamma);

            int tx = Mathf.CeilToInt(source.width / 8.0f);
            int ty = Mathf.CeilToInt(source.height / 8.0f);
            levelsShader.Dispatch(kernel, tx, ty, 1);

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
