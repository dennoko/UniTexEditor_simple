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
        public float strength = 1f;      // シャープネス: 0~2, ぼかし: 0~1
        public int kernelSize = 5;       // 3, 5, 7, 9
        
        private ComputeShader sharpenShader;
        private RenderTexture tempRT;
        
        public SharpenNode()
        {
            nodeName = "Sharpen/Blur";
        }
        
        public override RenderTexture Process(RenderTexture source, RenderTexture mask = null)
        {
            if (!enabled) return source;
            
            if (sharpenShader == null)
            {
                sharpenShader = Resources.Load<ComputeShader>("Sharpen");
                if (sharpenShader == null)
                {
                    Debug.LogError("Sharpen.compute not found in Resources folder!");
                    return source;
                }
            }
            
            // カーネルサイズを奇数に制限
            kernelSize = Mathf.Clamp(kernelSize, 3, 9);
            if (kernelSize % 2 == 0) kernelSize++;
            
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
            
            int kernelIndex = sharpenShader.FindKernel("CSMain");
            
            // パラメータをセット
            sharpenShader.SetTexture(kernelIndex, "Source", source);
            sharpenShader.SetTexture(kernelIndex, "Result", tempRT);
            
            if (mask != null)
            {
                sharpenShader.SetTexture(kernelIndex, "Mask", mask);
                sharpenShader.SetInt("UseMask", 1);
            }
            else
            {
                // マスクがない場合はダミーテクスチャを設定
                RenderTexture dummyMask = RenderTexture.GetTemporary(1, 1, 0, RenderTextureFormat.RFloat);
                sharpenShader.SetTexture(kernelIndex, "Mask", dummyMask);
                sharpenShader.SetInt("UseMask", 0);
                RenderTexture.ReleaseTemporary(dummyMask);
            }
            
            sharpenShader.SetInt("Mode", (int)mode);
            sharpenShader.SetFloat("Strength", strength);
            sharpenShader.SetInt("KernelSize", kernelSize);
            sharpenShader.SetInts("TextureSize", new int[] { source.width, source.height });
            
            // ディスパッチ
            int threadGroupsX = Mathf.CeilToInt(source.width / 8.0f);
            int threadGroupsY = Mathf.CeilToInt(source.height / 8.0f);
            sharpenShader.Dispatch(kernelIndex, threadGroupsX, threadGroupsY, 1);
            
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
