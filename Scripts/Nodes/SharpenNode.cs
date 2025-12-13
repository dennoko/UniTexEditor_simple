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

            Debug.Log($"[SharpenNode] Process start - mode={mode} strength={strength} kernel={kernelSize} source={source?.width}x{source?.height}");
            
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
            
            int kernelIndex = -1;
            try { kernelIndex = sharpenShader.FindKernel("CSMain"); } catch (Exception e) { Debug.LogError($"[SharpenNode] FindKernel error: {e.Message}"); }

            if (kernelIndex < 0)
            {
                Debug.LogError("[SharpenNode] CSMain kernel not found");
                return source;
            }
            
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
            
            try
            {
                // ディスパッチ
                int threadGroupsX = Mathf.CeilToInt(source.width / 8.0f);
                int threadGroupsY = Mathf.CeilToInt(source.height / 8.0f);
                if (threadGroupsX <= 0) threadGroupsX = 1;
                if (threadGroupsY <= 0) threadGroupsY = 1;
                sharpenShader.Dispatch(kernelIndex, threadGroupsX, threadGroupsY, 1);
            }
            catch (Exception e)
            {
                Debug.LogError($"[SharpenNode] Dispatch error: {e.Message}\n{e.StackTrace}");
                return source;
            }

            Debug.Log("[SharpenNode] Process completed");

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
