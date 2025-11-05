using UnityEngine;
using System.Collections.Generic;

namespace UniTexEditor
{
    /// <summary>
    /// テクスチャ処理パイプラインのメインクラス
    /// 非破壊レイヤシステムを管理
    /// </summary>
    public class TextureProcessor
    {
        private List<ProcessingNode> nodes = new List<ProcessingNode>();
        private Texture2D sourceTexture;
        private Texture2D maskTexture;
        private RenderTexture workingRT;
        
        public Texture2D SourceTexture 
        { 
            get => sourceTexture;
            set
            {
                sourceTexture = value;
                InitializeWorkingTexture();
            }
        }
        
        public Texture2D MaskTexture 
        { 
            get => maskTexture;
            set => maskTexture = value;
        }
        
        public List<ProcessingNode> Nodes => nodes;
        
        /// <summary>
        /// ノードを追加
        /// </summary>
        public void AddNode(ProcessingNode node)
        {
            nodes.Add(node);
        }
        
        /// <summary>
        /// ノードを削除
        /// </summary>
        public void RemoveNode(ProcessingNode node)
        {
            node.Cleanup();
            nodes.Remove(node);
        }
        
        /// <summary>
        /// すべてのノードをクリア
        /// </summary>
        public void ClearNodes()
        {
            foreach (var node in nodes)
            {
                node.Cleanup();
            }
            nodes.Clear();
        }
        
        /// <summary>
        /// ワーキングテクスチャを初期化
        /// </summary>
        private void InitializeWorkingTexture()
        {
            if (sourceTexture == null) return;
            
            if (workingRT != null)
            {
                workingRT.Release();
            }
            
            workingRT = new RenderTexture(sourceTexture.width, sourceTexture.height, 0, RenderTextureFormat.ARGBFloat);
            workingRT.enableRandomWrite = true;
            workingRT.Create();
            
            // ソーステクスチャをコピー
            Graphics.Blit(sourceTexture, workingRT);
        }
        
        /// <summary>
        /// すべてのノードを適用して結果を取得
        /// </summary>
        public RenderTexture ProcessAll()
        {
            if (sourceTexture == null)
            {
                Debug.LogWarning("Source texture is not set!");
                return null;
            }
            
            if (workingRT == null)
            {
                InitializeWorkingTexture();
            }
            
            // マスクをRenderTextureに変換（必要な場合）
            RenderTexture maskRT = null;
            if (maskTexture != null)
            {
                maskRT = new RenderTexture(maskTexture.width, maskTexture.height, 0, RenderTextureFormat.RFloat);
                maskRT.enableRandomWrite = true;
                maskRT.Create();
                Graphics.Blit(maskTexture, maskRT);
            }
            
            // 各ノードを順番に適用
            RenderTexture current = workingRT;
            RenderTexture temp = null;
            
            foreach (var node in nodes)
            {
                if (!node.enabled) continue;
                
                temp = node.Process(current, maskRT);
                
                // 前のテンポラリを解放（ワーキングテクスチャは保持）
                if (current != workingRT && current != temp)
                {
                    current.Release();
                }
                
                current = temp;
            }
            
            // マスクのクリーンアップ
            if (maskRT != null)
            {
                maskRT.Release();
            }
            
            return current;
        }
        
        /// <summary>
        /// RenderTextureをTexture2Dに変換
        /// </summary>
        public static Texture2D RenderTextureToTexture2D(RenderTexture rt)
        {
            RenderTexture.active = rt;
            Texture2D tex = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            return tex;
        }
        
        /// <summary>
        /// 結果をTexture2Dとして取得
        /// </summary>
        public Texture2D GetResultAsTexture2D()
        {
            RenderTexture result = ProcessAll();
            if (result == null) return null;
            
            return RenderTextureToTexture2D(result);
        }
        
        /// <summary>
        /// 結果をTexture2Dとして取得（解像度指定）
        /// </summary>
        /// <param name="maxResolution">最大解像度（幅と高さの大きい方）</param>
        public Texture2D GetResultAsTexture2D(int maxResolution)
        {
            RenderTexture result = ProcessAll();
            if (result == null) return null;
            
            // 元の解像度が小さければそのまま
            if (result.width <= maxResolution && result.height <= maxResolution)
            {
                return RenderTextureToTexture2D(result);
            }
            
            // リサイズが必要な場合
            int newWidth, newHeight;
            float aspect = (float)result.width / result.height;
            
            if (result.width > result.height)
            {
                newWidth = maxResolution;
                newHeight = Mathf.RoundToInt(maxResolution / aspect);
            }
            else
            {
                newHeight = maxResolution;
                newWidth = Mathf.RoundToInt(maxResolution * aspect);
            }
            
            // リサイズ用のRenderTextureを作成
            RenderTexture resizedRT = RenderTexture.GetTemporary(newWidth, newHeight, 0, RenderTextureFormat.ARGB32);
            Graphics.Blit(result, resizedRT);
            
            Texture2D resizedTex = RenderTextureToTexture2D(resizedRT);
            RenderTexture.ReleaseTemporary(resizedRT);
            
            return resizedTex;
        }
        
        /// <summary>
        /// クリーンアップ
        /// </summary>
        public void Cleanup()
        {
            foreach (var node in nodes)
            {
                node.Cleanup();
            }
            
            if (workingRT != null)
            {
                workingRT.Release();
                workingRT = null;
            }
        }
    }
}
