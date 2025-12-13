using UnityEngine;
using UnityEditor;

namespace UniTexEditor
{
    /// <summary>
    /// UniTexEditor メインエディタウィンドウ
    /// </summary>
    public partial class UniTexEditorWindow : EditorWindow
    {
        private TextureProcessor processor;
        private Texture2D sourceTexture;
        private Texture2D maskTexture;
        private Mesh sourceMesh;
        private GameObject sourceGameObject; // SkinnedMeshRenderer対応
        private Texture2D resultPreview;
        
        // 色調補正パラメータ
        private bool showColorCorrection = true;
        private float hueShift = 0f;
        private float saturation = 1f;
        private float brightness = 1f;
        private float gamma = 1f;
        private Color ccTargetColor = Color.white;
        private BlendMode ccBlendMode = BlendMode.Normal;
        private float ccBlendOpacity = 0f;
        
        // ブレンドパラメータ
        private bool showBlend = false;
        private Texture2D blendTexture;
        private Texture2D blendMaskTexture;
        private BlendMode blendMode = BlendMode.Normal;
        private float blendStrength = 1f;
        private Color hdrColor = Color.white;
        
        // Advanced Options
        private bool showAdvanced = false;
        
        // Levels
        private bool showLevels = false;
        private float lvlMinInput = 0f;
        private float lvlMaxInput = 1f;
        private float lvlMinOutput = 0f;
        private float lvlMaxOutput = 1f;
        private float lvlMidGamma = 1f;
        
        // Channel Mixer
        private bool showChannelMixer = false;
        private ChannelSource cmOutRed = ChannelSource.Red;
        private ChannelSource cmOutGreen = ChannelSource.Green;
        private ChannelSource cmOutBlue = ChannelSource.Blue;
        private ChannelSource cmOutAlpha = ChannelSource.Alpha;
        
        // シャープネス/ぼかしパラメータ
        private bool showSharpen = false;
        private SharpenMode sharpenMode = SharpenMode.Sharpen;
        private float sharpenStrength = 1f;
        private int sharpenKernelSize = 5;
        
        // トーンカーブパラメータ
        private bool showToneCurve = false;
        private AnimationCurve rgbCurve = AnimationCurve.Linear(0, 0, 1, 1);
        private AnimationCurve redCurve = AnimationCurve.Linear(0, 0, 1, 1);
        private AnimationCurve greenCurve = AnimationCurve.Linear(0, 0, 1, 1);
        private AnimationCurve blueCurve = AnimationCurve.Linear(0, 0, 1, 1);
        private bool useRGBCurve = true;
        private bool useRedCurve = false;
        private bool useGreenCurve = false;
        private bool useBlueCurve = false;
        
        // マスクオプション（常時ON）
        private bool invertMask = false;
        private float maskStrength = 1f;
        
        // 出力設定
        private bool overwriteSource = false;
        private string outputPath = "";
        private string customOutputPath = ""; // ユーザーが明示的に指定したパス
        private bool convertToSRGBOnSave = true; // 保存時にsRGBに変換するか
        
        // プレビュー
        private bool autoPreview = true;
        private Vector2 scrollPosition;
        private Vector2 previewScrollPosition;
        private const float PREVIEW_MAX_SIZE = 512f; // プレビュー最大サイズ
        private const int PREVIEW_RESOLUTION = 512; // プレビュー計算解像度
        private bool previewDirty;
        private bool previewUpdateScheduled;
        
        // ステータスバー
        public enum StatusType
        {
            None,
            Info,
            Success,
            Error
        }
        private string statusMessage = "Ready";
        private StatusType statusType = StatusType.Info;
        
        [MenuItem("Tools/UniTex Editor")]
        public static void ShowWindow()
        {
            var window = GetWindow<UniTexEditorWindow>("UniTex Editor");
            window.minSize = new Vector2(400, 600);
        }
        
        private void OnEnable()
        {
            processor = new TextureProcessor();
        }
        
        private void OnDisable()
        {
            processor?.Cleanup();
            
            // プレビューテクスチャもクリーンアップ
            if (resultPreview != null)
            {
                DestroyImmediate(resultPreview);
                resultPreview = null;
            }

            EditorApplication.delayCall -= ProcessPendingPreview;
            previewUpdateScheduled = false;
            previewDirty = false;
        }
    }
}
