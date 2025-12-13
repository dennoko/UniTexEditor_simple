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
        
        public override RenderTexture Process(RenderTexture source, RenderTexture mask = null)
        {
            if (!enabled) return source;
            if (minInput == 0f && maxInput == 1f && minOutput == 0f && maxOutput == 1f && midGamma == 1f)
                return source;
            
            if (levelsShader == null)
            {
                levelsShader = Resources.Load<ComputeShader>("Levels");
                if (levelsShader == null) return source;
            }
            
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
            
            int kernel = levelsShader.FindKernel("CSMain");
            
            levelsShader.SetTexture(kernel, "Source", source);
            levelsShader.SetTexture(kernel, "Result", tempRT);
            
            if (mask != null)
            {
                levelsShader.SetTexture(kernel, "Mask", mask);
                levelsShader.SetInt("UseMask", 1);
            }
            else
            {
                RenderTexture dummy = RenderTexture.GetTemporary(1, 1, 0, RenderTextureFormat.RFloat);
                levelsShader.SetTexture(kernel, "Mask", dummy);
                levelsShader.SetInt("UseMask", 0);
                RenderTexture.ReleaseTemporary(dummy);
            }
            
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
