using UnityEngine;

namespace UniTexEditor
{
    /// <summary>
    /// UV境界マスク生成ユーティリティ
    /// テクスチャの勾配からUVアイランド境界を検出し、膨張マスクを生成
    /// </summary>
    public static class UVBoundaryMaskUtility
    {
        /// <summary>
        /// ソーステクスチャからUV境界マスクを生成
        /// </summary>
        /// <param name="sourceTexture">ソーステクスチャ</param>
        /// <param name="boundaryThreshold">境界判定の色差閾値（0.0～1.0）</param>
        /// <param name="dilationRadius">境界からの膨張半径（ピクセル）</param>
        /// <returns>UV境界マスク（RFloat形式）</returns>
        public static RenderTexture GenerateBoundaryMask(Texture2D sourceTexture, float boundaryThreshold = 0.1f, int dilationRadius = 5)
        {
            if (sourceTexture == null)
            {
                Debug.LogError("[UVBoundaryMask] Source texture is null!");
                return null;
            }
            
            var startTime = System.DateTime.Now;
            
            // Compute Shaderをロード
            ComputeShader shader = Resources.Load<ComputeShader>("UVBoundaryMask");
            if (shader == null)
            {
                Debug.LogError("[UVBoundaryMask] UVBoundaryMask.compute not found in Resources folder!");
                return null;
            }
            
            int width = sourceTexture.width;
            int height = sourceTexture.height;
            
            // ソーステクスチャをRenderTextureに変換
            RenderTexture sourceRT = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGBFloat);
            Graphics.Blit(sourceTexture, sourceRT);
            
            // 境界検出用の一時テクスチャ
            RenderTexture boundaryRT = new RenderTexture(width, height, 0, RenderTextureFormat.RFloat);
            boundaryRT.enableRandomWrite = true;
            boundaryRT.Create();
            
            // 膨張マスク用のテクスチャ
            RenderTexture dilatedRT = new RenderTexture(width, height, 0, RenderTextureFormat.RFloat);
            dilatedRT.enableRandomWrite = true;
            dilatedRT.Create();
            
            // === 境界検出パス ===
            int detectKernel = shader.FindKernel("DetectBoundary");
            shader.SetTexture(detectKernel, "SourceTexture", sourceRT);
            shader.SetTexture(detectKernel, "BoundaryMask", boundaryRT);
            shader.SetInts("TextureSize", new int[] { width, height });
            shader.SetFloat("BoundaryThreshold", boundaryThreshold);
            
            int threadGroupsX = Mathf.CeilToInt(width / 8.0f);
            int threadGroupsY = Mathf.CeilToInt(height / 8.0f);
            shader.Dispatch(detectKernel, threadGroupsX, threadGroupsY, 1);
            
            // === 膨張パス ===
            int dilateKernel = shader.FindKernel("DilateMask");
            shader.SetTexture(dilateKernel, "BoundaryMask", boundaryRT);
            shader.SetTexture(dilateKernel, "DilatedMask", dilatedRT);
            shader.SetInts("TextureSize", new int[] { width, height });
            shader.SetInt("DilationRadius", dilationRadius);
            shader.Dispatch(dilateKernel, threadGroupsX, threadGroupsY, 1);
            
            // クリーンアップ
            RenderTexture.ReleaseTemporary(sourceRT);
            boundaryRT.Release();
            
            var elapsed = (System.DateTime.Now - startTime).TotalMilliseconds;
            Debug.Log($"[UVBoundaryMask] Generated boundary mask in {elapsed:F0}ms (resolution: {width}x{height}, threshold: {boundaryThreshold}, dilation: {dilationRadius}px)");
            
            return dilatedRT;
        }
    }
}
