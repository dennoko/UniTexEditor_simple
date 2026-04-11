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
        /// ノード処理を実行するテンプレートメソッド。
        /// enabled チェックを担い、実処理は ProcessInternal に委譲する。
        /// </summary>
        public RenderTexture Process(RenderTexture source, RenderTexture mask = null)
        {
            if (!enabled) return source;
            return ProcessInternal(source, mask);
        }

        /// <summary>
        /// サブクラスで実装する実際の処理。
        /// enabled チェックは不要（Process が保証する）。
        /// </summary>
        protected abstract RenderTexture ProcessInternal(RenderTexture source, RenderTexture mask = null);

        /// <summary>
        /// ノードのクリーンアップ
        /// </summary>
        public virtual void Cleanup() { }

        // ─── 共通ヘルパー ────────────────────────────────────────────────

        /// <summary>
        /// マスクパラメータを ComputeShader にセットする。
        /// mask が null の場合はダミー 1×1 テクスチャでフォールバックする。
        /// </summary>
        protected static void SetMaskParameter(ComputeShader shader, int kernel, RenderTexture mask)
        {
            if (mask != null)
            {
                shader.SetTexture(kernel, "Mask", mask);
                shader.SetInt("UseMask", 1);
            }
            else
            {
                RenderTexture dummy = RenderTexture.GetTemporary(1, 1, 0, RenderTextureFormat.RFloat);
                shader.SetTexture(kernel, "Mask", dummy);
                shader.SetInt("UseMask", 0);
                RenderTexture.ReleaseTemporary(dummy);
            }
        }

        /// <summary>
        /// ARGBFloat の RenderTexture がソースと同サイズであることを保証する。
        /// サイズが異なれば解放して作り直す。
        /// </summary>
        protected static void EnsureRenderTexture(ref RenderTexture rt, int width, int height)
        {
            if (rt != null && (rt.width != width || rt.height != height))
            {
                rt.Release();
                rt = null;
            }
            if (rt == null)
            {
                rt = new RenderTexture(width, height, 0, RenderTextureFormat.ARGBFloat);
                rt.enableRandomWrite = true;
                rt.Create();
            }
        }
    }
}
