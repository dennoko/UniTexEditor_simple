using UnityEngine;
using System;
using System.Collections.Generic;

namespace UniTexEditor
{
    /// <summary>
    /// UV アイランド境界を越えないブラー処理ノード
    /// </summary>
    [Serializable]
    public class UVIslandBlurNode : ProcessingNode
    {
        public Mesh sourceMesh;
        public int blurRadius = 5;
        public float blurSigma = 2f;
        
        private ComputeShader blurShader;
        private RenderTexture tempRT1;
        private RenderTexture tempRT2;
        private Texture2D islandIDMap;
        private List<UVIslandUtility.UVIsland> cachedIslands;
        private Mesh cachedMesh; // キャッシュ検証用
        private int cachedWidth = -1;
        private int cachedHeight = -1;
        
        public UVIslandBlurNode()
        {
            nodeName = "UVIslandBlur";
        }
        
        public override RenderTexture Process(RenderTexture source, RenderTexture mask = null)
        {
            if (!enabled || sourceMesh == null) return source;
            
            if (blurShader == null)
            {
                blurShader = Resources.Load<ComputeShader>("UVIslandBlur");
                if (blurShader == null)
                {
                    Debug.LogError("UVIslandBlur.compute not found in Resources folder!");
                    return source;
                }
            }
            
            // UV アイランド ID マップを生成（キャッシュ）
            // メッシュまたは解像度が変わった場合のみ再生成
            if (islandIDMap == null || cachedIslands == null || 
                cachedMesh != sourceMesh || 
                cachedWidth != source.width || 
                cachedHeight != source.height)
            {
                Debug.Log($"[UVIslandBlur] Generating Island ID Map - mesh={sourceMesh.name}, resolution={source.width}x{source.height}");
                var startTime = System.DateTime.Now;
                
                GenerateIslandIDMap(source.width, source.height);
                
                var elapsed = (System.DateTime.Now - startTime).TotalMilliseconds;
                Debug.Log($"[UVIslandBlur] Island ID Map generation completed in {elapsed:F0}ms");
                
                cachedMesh = sourceMesh;
                cachedWidth = source.width;
                cachedHeight = source.height;
            }
            else
            {
                Debug.Log("[UVIslandBlur] Using cached Island ID Map");
            }
            
            if (islandIDMap == null)
            {
                Debug.LogError("Failed to generate UV Island ID map!");
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
            blurShader.SetTexture(kernelH, "IslandIDMap", islandIDMap);
            blurShader.SetInt("BlurRadius", blurRadius);
            blurShader.SetFloat("BlurSigma", blurSigma);
            blurShader.Dispatch(kernelH, threadGroupsX, threadGroupsY, 1);
            
            // 縦方向パス
            int kernelV = blurShader.FindKernel("CSMainVertical");
            blurShader.SetTexture(kernelV, "Source", tempRT1);
            blurShader.SetTexture(kernelV, "Result", tempRT2);
            blurShader.SetTexture(kernelV, "IslandIDMap", islandIDMap);
            blurShader.SetInt("BlurRadius", blurRadius);
            blurShader.SetFloat("BlurSigma", blurSigma);
            blurShader.Dispatch(kernelV, threadGroupsX, threadGroupsY, 1);
            
            return tempRT2;
        }
        
        private void GenerateIslandIDMap(int width, int height)
        {
            if (sourceMesh == null)
            {
                Debug.LogWarning("Source mesh is not set for UV Island Blur!");
                return;
            }
            
            try
            {
                // UV アイランドを抽出
                cachedIslands = UVIslandUtility.ExtractUVIslands(sourceMesh);
                
                if (cachedIslands.Count == 0)
                {
                    Debug.LogWarning($"No UV islands found in mesh: {sourceMesh.name}");
                    return;
                }
                
                Debug.Log($"Found {cachedIslands.Count} UV islands in mesh: {sourceMesh.name}");
                
                // ID マップを生成
                islandIDMap = UVIslandUtility.GenerateIslandIDMap(cachedIslands, width, height);
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to generate UV Island ID map: {e.Message}");
            }
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
            
            if (islandIDMap != null)
            {
                UnityEngine.Object.DestroyImmediate(islandIDMap);
                islandIDMap = null;
            }
            
            cachedIslands = null;
            cachedMesh = null;
            cachedWidth = -1;
            cachedHeight = -1;
        }
    }
}
