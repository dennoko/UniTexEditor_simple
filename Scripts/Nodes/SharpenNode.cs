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
    /// シャープネス・ぼかし処理ノード（2パス分離可能フィルタ）
    /// </summary>
    [Serializable]
    public class SharpenNode : ProcessingNode
    {
        public SharpenMode mode = SharpenMode.Sharpen;
        public float strength = 1f;      // シャープネス: 0~2, ブラー: 0~1
        public float range = 3f;         // ブラー範囲（ピクセル単位の半径、1~20）
        
        private ComputeShader horizontalShader;
        private ComputeShader verticalShader;
        private RenderTexture intermediateRT;
        private RenderTexture tempRT;
        
        public SharpenNode()
        {
            nodeName = "Sharpen/Blur";
        }
        
        public override RenderTexture Process(RenderTexture source, RenderTexture mask = null)
        {
            if (!enabled) return source;

            Debug.Log($"[SharpenNode] Process start - mode={mode} strength={strength} range={range} source={source?.width}x{source?.height}");
            
            // シェーダーのロード
            if (horizontalShader == null)
            {
                horizontalShader = Resources.Load<ComputeShader>("SharpenHorizontal");
                if (horizontalShader == null)
                {
                    Debug.LogError("SharpenHorizontal.compute not found in Resources folder!");
                    return source;
                }
            }
            
            if (verticalShader == null)
            {
                verticalShader = Resources.Load<ComputeShader>("SharpenVertical");
                if (verticalShader == null)
                {
                    Debug.LogError("SharpenVertical.compute not found in Resources folder!");
                    return source;
                }
            }
            
            // 範囲を制限
            range = Mathf.Clamp(range, 0.5f, 20f);
            
            // 中間バッファの作成（水平パスの結果を格納）
            if (intermediateRT != null && (intermediateRT.width != source.width || intermediateRT.height != source.height))
            {
                intermediateRT.Release();
                intermediateRT = null;
            }
            
            if (intermediateRT == null)
            {
                intermediateRT = new RenderTexture(source.width, source.height, 0, RenderTextureFormat.ARGBFloat);
                intermediateRT.enableRandomWrite = true;
                intermediateRT.Create();
            }
            
            // 最終結果用の一時バッファ
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
            
            // === パス1: 水平方向のブラー ===
            int horizontalKernel = -1;
            try { horizontalKernel = horizontalShader.FindKernel("CSMain"); } 
            catch (Exception e) { Debug.LogError($"[SharpenNode] Horizontal FindKernel error: {e.Message}"); }

            if (horizontalKernel < 0)
            {
                Debug.LogError("[SharpenNode] Horizontal CSMain kernel not found");
                return source;
            }
            
            horizontalShader.SetTexture(horizontalKernel, "Source", source);
            horizontalShader.SetTexture(horizontalKernel, "Result", intermediateRT);
            horizontalShader.SetInt("Mode", (int)mode);
            horizontalShader.SetFloat("Strength", strength);
            horizontalShader.SetFloat("Range", range);
            horizontalShader.SetInts("TextureSize", new int[] { source.width, source.height });
            
            try
            {
                int threadGroupsX = Mathf.CeilToInt(source.width / 8.0f);
                int threadGroupsY = Mathf.CeilToInt(source.height / 8.0f);
                if (threadGroupsX <= 0) threadGroupsX = 1;
                if (threadGroupsY <= 0) threadGroupsY = 1;
                horizontalShader.Dispatch(horizontalKernel, threadGroupsX, threadGroupsY, 1);
            }
            catch (Exception e)
            {
                Debug.LogError($"[SharpenNode] Horizontal Dispatch error: {e.Message}\n{e.StackTrace}");
                return source;
            }
            
            // === パス2: 垂直方向のブラー + マスク適用 + 最終処理 ===
            int verticalKernel = -1;
            try { verticalKernel = verticalShader.FindKernel("CSMain"); } 
            catch (Exception e) { Debug.LogError($"[SharpenNode] Vertical FindKernel error: {e.Message}"); }

            if (verticalKernel < 0)
            {
                Debug.LogError("[SharpenNode] Vertical CSMain kernel not found");
                return source;
            }
            
            verticalShader.SetTexture(verticalKernel, "Source", intermediateRT);
            verticalShader.SetTexture(verticalKernel, "Original", source);
            verticalShader.SetTexture(verticalKernel, "Result", tempRT);
            
            if (mask != null)
            {
                verticalShader.SetTexture(verticalKernel, "Mask", mask);
                verticalShader.SetInt("UseMask", 1);
            }
            else
            {
                RenderTexture dummyMask = RenderTexture.GetTemporary(1, 1, 0, RenderTextureFormat.RFloat);
                verticalShader.SetTexture(verticalKernel, "Mask", dummyMask);
                verticalShader.SetInt("UseMask", 0);
                RenderTexture.ReleaseTemporary(dummyMask);
            }
            
            verticalShader.SetInt("Mode", (int)mode);
            verticalShader.SetFloat("Strength", strength);
            verticalShader.SetFloat("Range", range);
            verticalShader.SetInts("TextureSize", new int[] { source.width, source.height });
            
            try
            {
                int threadGroupsX = Mathf.CeilToInt(source.width / 8.0f);
                int threadGroupsY = Mathf.CeilToInt(source.height / 8.0f);
                if (threadGroupsX <= 0) threadGroupsX = 1;
                if (threadGroupsY <= 0) threadGroupsY = 1;
                verticalShader.Dispatch(verticalKernel, threadGroupsX, threadGroupsY, 1);
            }
            catch (Exception e)
            {
                Debug.LogError($"[SharpenNode] Vertical Dispatch error: {e.Message}\n{e.StackTrace}");
                return source;
            }

            Debug.Log("[SharpenNode] 2-pass process completed");

            return tempRT;
        }
        
        public override void Cleanup()
        {
            if (intermediateRT != null)
            {
                intermediateRT.Release();
                intermediateRT = null;
            }
            
            if (tempRT != null)
            {
                tempRT.Release();
                tempRT = null;
            }
        }
    }
}
