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
        private bool blendTiling = true;
        private Vector2 blendScale = Vector2.one;
        private Vector2 blendOffset = Vector2.zero;

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
        private int sharpenIterations = 1;

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

        // マスクオプション
        private bool invertMask = false;
        private float maskStrength = 1f;

        // 出力設定
        private bool overwriteSource = false;
        private string customOutputPath = "";

        // プレビュー
        private bool autoPreview = true;
        private Vector2 scrollPosition;
        private const float PREVIEW_MAX_SIZE = 512f;
        private const int PREVIEW_RESOLUTION = 512;
        private bool previewDirty;
        private bool previewUpdateScheduled;

        // チェッカーボードテクスチャ（OnEnable で生成、OnDisable で破棄）
        private Texture2D checkerboardTexture;

        // ステータスバー
        public enum StatusType { None, Info, Success, Error }
        private string statusMessage = "Ready";
        private StatusType statusType = StatusType.Info;
        // Info 以外のステータスを自動リセットする時刻（EditorApplication.timeSinceStartup）
        // 負値の場合はリセット無効
        private double _statusResetTime = -1.0;

        [MenuItem("dennokoworks/UniTex Editor")]
        public static void ShowWindow()
        {
            var window = GetWindow<UniTexEditorWindow>("UniTex Editor");
            window.minSize = new Vector2(400, 600);
        }

        private void OnEnable()
        {
            Localization.Initialize();

            if (processor == null)
                processor = new TextureProcessor();

            if (resultPreview != null)
            {
                DestroyImmediate(resultPreview);
                resultPreview = null;
            }
        }

        private void OnDisable()
        {
            processor?.Cleanup();

            if (resultPreview != null)
            {
                DestroyImmediate(resultPreview);
                resultPreview = null;
            }

            if (checkerboardTexture != null)
            {
                DestroyImmediate(checkerboardTexture);
                checkerboardTexture = null;
            }

            EditorApplication.delayCall -= ProcessPendingPreview;
            previewUpdateScheduled = false;
            previewDirty = false;
        }
    }
}
