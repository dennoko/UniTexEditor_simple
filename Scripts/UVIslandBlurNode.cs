using UnityEngine;
using System;

namespace UniTexEditor
{
    /// <summary>
    /// UV アイランド境界を越えないブラー処理ノード
    /// マスクベース方式：テクスチャの勾配から境界を検出
    /// </summary>
    [Serializable]
    public class UVIslandBlurNode : ProcessingNode
    {
        public int blurRadius = 5;
        public float blurSigma = 2f;
        public float boundaryThreshold = 0.1f;  // 境界判定の色差閾値
        public int dilationRadius = 5;          // 境界からの膨張半径
        
        private ComputeShader blurShader;
        private RenderTexture tempRT1;
        private RenderTexture tempRT2;
        private RenderTexture boundaryMask;
        
        // キャッシュ管理
        private Texture2D cachedSourceTexture;
        private int cachedWidth = -1;
        private int cachedHeight = -1;
        
        public UVIslandBlurNode()
        {
            nodeName = "UVIslandBlur";
        }
        
        public override RenderTexture Process(RenderTexture source, RenderTexture mask = null)
        {
            if (!enabled) return source;
            
            Debug.Log($"[UVIslandBlur] Process start - resolution={source.width}x{source.height}");
            var startTime = System.DateTime.Now;
            
            if (blurShader == null)
            {
                blurShader = Resources.Load<ComputeShader>("UVIslandBlur");
                if (blurShader == null)
                {
                    Debug.LogError("UVIslandBlur.compute not found in Resources folder!");
                    return source;
                }
            }
            
            // 境界マスクを生成（キャッシュ）
            // 解像度が変わった場合のみ再生成
            if (boundaryMask == null || 
                cachedWidth != source.width || 
                cachedHeight != source.height)
            {
                Debug.Log($"[UVIslandBlur] Generating boundary mask - resolution={source.width}x{source.height}");
                var maskStartTime = System.DateTime.Now;
                
                // RenderTextureをTexture2Dに変換（一時的）
                Texture2D sourceTexture = RenderTextureToTexture2D(source);
                
                // 境界マスクを生成
                if (boundaryMask != null)
                {
                    boundaryMask.Release();
                }
                boundaryMask = UVBoundaryMaskUtility.GenerateBoundaryMask(
                    sourceTexture, 
                    boundaryThreshold, 
                    dilationRadius
                );
                
                UnityEngine.Object.DestroyImmediate(sourceTexture);
                
                cachedWidth = source.width;
                cachedHeight = source.height;
                
                var maskElapsed = (System.DateTime.Now - maskStartTime).TotalMilliseconds;
                Debug.Log($"[UVIslandBlur] Boundary mask generation completed in {maskElapsed:F0}ms");
            }
            else
            {
                Debug.Log("[UVIslandBlur] Using cached boundary mask");
            }
            
            if (boundaryMask == null)
            {
                Debug.LogError("Failed to generate boundary mask!");
                return source;
            }
            
            // 一時的なRenderTextureを作成
            if (tempRT1 == null || tempRT1.width != source.width || tempRT1.height != source.height)
            {
                if (tempRT1 != null) tempRT1.Release();
                tempRT1 = new RenderTexture(source.width, source.height, 0, RenderTextureFormat.ARGBFloat);
                tempRT1.enableRandomWrite = true;
                tempRT1.Create();
            }
            
            if (tempRT2 == null || tempRT2.width != source.width || tempRT2.height != source.height)
            {
                if (tempRT2 != null) tempRT2.Release();
                tempRT2 = new RenderTexture(source.width, source.height, 0, RenderTextureFormat.ARGBFloat);
                tempRT2.enableRandomWrite = true;
                tempRT2.Create();
            }
            
            int threadGroupsX = Mathf.CeilToInt(source.width / 8.0f);
            int threadGroupsY = Mathf.CeilToInt(source.height / 8.0f);
            
            // 横方向パス
            int kernelH = blurShader.FindKernel("CSMainHorizontal");
            blurShader.SetTexture(kernelH, "Source", source);
            blurShader.SetTexture(kernelH, "Result", tempRT1);
            blurShader.SetTexture(kernelH, "BoundaryMask", boundaryMask);
            blurShader.SetInt("BlurRadius", blurRadius);
            blurShader.SetFloat("BlurSigma", blurSigma);
            blurShader.Dispatch(kernelH, threadGroupsX, threadGroupsY, 1);
            
            // 縦方向パス
            int kernelV = blurShader.FindKernel("CSMainVertical");
            blurShader.SetTexture(kernelV, "Source", tempRT1);
            blurShader.SetTexture(kernelV, "Result", tempRT2);
            blurShader.SetTexture(kernelV, "BoundaryMask", boundaryMask);
            blurShader.SetInt("BlurRadius", blurRadius);
            blurShader.SetFloat("BlurSigma", blurSigma);
            blurShader.Dispatch(kernelV, threadGroupsX, threadGroupsY, 1);
            
            var elapsed = (System.DateTime.Now - startTime).TotalMilliseconds;
            Debug.Log($"[UVIslandBlur] Process completed in {elapsed:F0}ms");
            
            return tempRT2;
        }
        
        private Texture2D RenderTextureToTexture2D(RenderTexture rt)
        {
            // Linear色空間でTexture2Dを作成（ガンマ補正を適切に処理）
            Texture2D tex = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false, true);
            
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            RenderTexture.active = previous;
            
            return tex;
        }
        
        public override void Cleanup()
        {
            if (tempRT1 != null)
            {
                tempRT1.Release();
                tempRT1 = null;
            }
            
            if (tempRT2 != null)
            {
                tempRT2.Release();
                tempRT2 = null;
            }
            
            if (boundaryMask != null)
            {
                boundaryMask.Release();
                boundaryMask = null;
            }
            
            cachedSourceTexture = null;
            cachedWidth = -1;
            cachedHeight = -1;
        }
    }
}
