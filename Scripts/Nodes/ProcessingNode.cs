using UnityEngine;
using System;

namespace UniTexEditor
{
    /// <summary>
    /// 処理ノードの基底クラス（非破壊レイヤシステムの基礎）
    /// </summary>
    [Serializable]
    public abstract class ProcessingNode
    {
        public string nodeName = "Node";
        public bool enabled = true;
        
        /// <summary>
        /// このノードの処理を実行
        /// </summary>
        /// <param name="source">入力テクスチャ</param>
        /// <param name="mask">マスクテクスチャ（オプション）</param>
        /// <returns>処理済みRenderTexture</returns>
        public abstract RenderTexture Process(RenderTexture source, RenderTexture mask = null);
        
        /// <summary>
        /// ノードのクリーンアップ
        /// </summary>
        public virtual void Cleanup()
        {
            // サブクラスで必要に応じてオーバーライド
        }
    }
}
