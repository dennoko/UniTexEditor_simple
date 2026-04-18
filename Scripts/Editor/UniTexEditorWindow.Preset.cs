using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;

namespace UniTexEditor
{
    public partial class UniTexEditorWindow
    {
        private const string PRESET_FOLDER        = "Assets/UniTexEditor_Presets";
        private const string PRESET_TYPE_PARAMS   = "params";
        private const string PRESET_TYPE_FULL     = "full";

        private static readonly string[] PresetTypeOptions = { "パラメータのみ", "テクスチャ込み" };

        // Preset UI state
        private string         _presetName            = "";
        private int            _presetTypeIndex       = 0; // 0=params, 1=full
        private List<string>   _presetPaths           = new List<string>();
        private string[]       _presetDisplayNames    = new string[0];
        private int            _selectedPresetIndex   = 0;
        private string         _presetInfoText        = "";
        private bool           _presetListInitialized = false;

        // ─── UI ──────────────────────────────────────────────────────────────

        partial void DrawPresetSection()
        {
            if (!_presetListInitialized)
            {
                RefreshPresetList();
                _presetListInitialized = true;
            }

            DrawSection("PRESET", () =>
            {
                // ── 保存エリア ──
                GUILayout.Label("保存", EditorStyles.boldLabel);
                EditorGUILayout.Space(2);

                GUILayout.BeginHorizontal();
                GUILayout.Label("名前", GUILayout.Width(36));
                _presetName = EditorGUILayout.TextField(_presetName);
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                GUILayout.Label("種別", GUILayout.Width(36));
                _presetTypeIndex = GUILayout.Toolbar(_presetTypeIndex, PresetTypeOptions, EditorStyles.miniButton);
                GUILayout.EndHorizontal();

                EditorGUILayout.Space(4);

                using (new EditorGUI.DisabledGroupScope(string.IsNullOrWhiteSpace(_presetName)))
                {
                    if (GUILayout.Button("保存", UniTexTheme.MiniButtonStyle))
                    {
                        SavePreset(_presetName, _presetTypeIndex == 1);
                        GUI.FocusControl(null);
                    }
                }

                DrawSeparator();

                // ── 読み込みエリア ──
                GUILayout.BeginHorizontal();
                GUILayout.Label("読み込み", EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("↺", UniTexTheme.MiniButtonStyle, GUILayout.Width(24)))
                    RefreshPresetList();
                GUILayout.EndHorizontal();

                EditorGUILayout.Space(2);

                if (_presetDisplayNames.Length == 0)
                {
                    GUILayout.Label("プリセットがありません", UniTexTheme.CaptionStyle);
                }
                else
                {
                    int newIndex = EditorGUILayout.Popup(_selectedPresetIndex, _presetDisplayNames);
                    if (newIndex != _selectedPresetIndex)
                    {
                        _selectedPresetIndex = newIndex;
                        UpdatePresetInfo();
                    }

                    if (!string.IsNullOrEmpty(_presetInfoText))
                    {
                        EditorGUILayout.Space(2);
                        GUILayout.Label(_presetInfoText, UniTexTheme.CaptionStyle);
                    }

                    EditorGUILayout.Space(4);

                    GUILayout.BeginHorizontal();
                    if (GUILayout.Button("読み込み", UniTexTheme.MiniButtonStyle))
                        LoadPreset(_presetPaths[_selectedPresetIndex]);

                    if (GUILayout.Button("削除", UniTexTheme.MiniButtonStyle))
                    {
                        string displayName = _presetDisplayNames[_selectedPresetIndex];
                        if (EditorUtility.DisplayDialog("削除確認",
                            $"プリセット「{displayName}」を削除しますか？", "削除", "キャンセル"))
                        {
                            DeletePreset(_presetPaths[_selectedPresetIndex]);
                        }
                    }
                    GUILayout.EndHorizontal();
                }
            });
        }

        // ─── プリセット一覧更新 ───────────────────────────────────────────────

        private void RefreshPresetList()
        {
            EnsurePresetFolder();

            _presetPaths = new List<string>();

            string absoluteFolder = Path.Combine(Application.dataPath, "UniTexEditor_Presets");
            if (Directory.Exists(absoluteFolder))
            {
                string[] files = Directory.GetFiles(absoluteFolder, "*.json");
                foreach (var f in files)
                {
                    // ファイルシステムの絶対パスを Assets/ 相対パスへ変換
                    string dataPath  = Application.dataPath.Replace("\\", "/");
                    string filePath  = f.Replace("\\", "/");
                    string relative  = "Assets" + filePath.Substring(dataPath.Length);
                    _presetPaths.Add(relative);
                }
            }

            _presetDisplayNames = new string[_presetPaths.Count];
            for (int i = 0; i < _presetPaths.Count; i++)
                _presetDisplayNames[i] = Path.GetFileNameWithoutExtension(_presetPaths[i]);

            if (_selectedPresetIndex >= _presetPaths.Count)
                _selectedPresetIndex = Mathf.Max(0, _presetPaths.Count - 1);

            UpdatePresetInfo();
        }

        private void UpdatePresetInfo()
        {
            if (_presetPaths.Count == 0 || _selectedPresetIndex < 0 || _selectedPresetIndex >= _presetPaths.Count)
            {
                _presetInfoText = "";
                return;
            }

            try
            {
                string json = File.ReadAllText(_presetPaths[_selectedPresetIndex]);
                var data    = JsonUtility.FromJson<UniTexPresetData>(json);
                if (data == null) { _presetInfoText = "読み込みエラー"; return; }

                string typeLabel = data.presetType == PRESET_TYPE_FULL ? "テクスチャ込み" : "パラメータのみ";

                int activeCount = 0;
                if (data.showColorCorrection) activeCount++;
                if (data.showBlend)           activeCount++;
                if (data.showLevels)          activeCount++;
                if (data.showChannelMixer)    activeCount++;
                if (data.showSharpen)         activeCount++;
                if (data.showToneCurve)       activeCount++;

                _presetInfoText = $"{typeLabel}  |  {data.createdAt}  |  {activeCount} section(s) active";
            }
            catch
            {
                _presetInfoText = "読み込みエラー";
            }
        }

        // ─── 保存 ────────────────────────────────────────────────────────────

        private void SavePreset(string name, bool includeTextures)
        {
            EnsurePresetFolder();

            var data = new UniTexPresetData
            {
                presetName  = name,
                presetType  = includeTextures ? PRESET_TYPE_FULL : PRESET_TYPE_PARAMS,
                version     = 1,
                createdAt   = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm"),

                // Section states
                showColorCorrection = showColorCorrection,
                showBlend           = showBlend,
                showLevels          = showLevels,
                showChannelMixer    = showChannelMixer,
                showSharpen         = showSharpen,
                showToneCurve       = showToneCurve,

                // Color Correction
                hueShift       = hueShift,
                saturation     = saturation,
                brightness     = brightness,
                gamma          = gamma,
                ccTargetColorR = ccTargetColor.r,
                ccTargetColorG = ccTargetColor.g,
                ccTargetColorB = ccTargetColor.b,
                ccTargetColorA = ccTargetColor.a,
                ccBlendMode    = (int)ccBlendMode,
                ccBlendOpacity = ccBlendOpacity,

                // Tone Curve
                rgbCurve   = PresetCurve.FromCurve(rgbCurve),
                redCurve   = PresetCurve.FromCurve(redCurve),
                greenCurve = PresetCurve.FromCurve(greenCurve),
                blueCurve  = PresetCurve.FromCurve(blueCurve),
                useRGBCurve   = useRGBCurve,
                useRedCurve   = useRedCurve,
                useGreenCurve = useGreenCurve,
                useBlueCurve  = useBlueCurve,

                // Blend
                blendMode    = (int)blendMode,
                blendStrength = blendStrength,
                blendTiling  = blendTiling,
                blendScaleX  = blendScale.x,
                blendScaleY  = blendScale.y,
                blendOffsetX = blendOffset.x,
                blendOffsetY = blendOffset.y,

                // Levels
                lvlMinInput  = lvlMinInput,
                lvlMaxInput  = lvlMaxInput,
                lvlMinOutput = lvlMinOutput,
                lvlMaxOutput = lvlMaxOutput,
                lvlMidGamma  = lvlMidGamma,

                // Sharpen
                sharpenMode       = (int)sharpenMode,
                sharpenStrength   = sharpenStrength,
                sharpenKernelSize = sharpenKernelSize,
                sharpenIterations = sharpenIterations,

                // Channel Mixer
                cmOutRed   = (int)cmOutRed,
                cmOutGreen = (int)cmOutGreen,
                cmOutBlue  = (int)cmOutBlue,
                cmOutAlpha = (int)cmOutAlpha,

                // Mask
                invertMask  = invertMask,
                maskStrength = maskStrength,
            };

            if (includeTextures)
            {
                data.maskTextureGUID      = maskTexture      != null ? AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(maskTexture))      : "";
                data.blendTextureGUID     = blendTexture     != null ? AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(blendTexture))     : "";
                data.blendMaskTextureGUID = blendMaskTexture != null ? AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(blendMaskTexture)) : "";
            }

            // ファイル名に使えない文字を置換
            char[] invalid   = Path.GetInvalidFileNameChars();
            string safeName  = string.Join("_", name.Split(invalid));
            string savePath  = $"{PRESET_FOLDER}/{safeName}.json";

            if (File.Exists(savePath))
            {
                bool overwrite = EditorUtility.DisplayDialog(
                    "上書き確認",
                    $"プリセット「{name}」はすでに存在します。上書きしますか？",
                    "上書き", "キャンセル");
                if (!overwrite) return;
            }

            File.WriteAllText(savePath, JsonUtility.ToJson(data, true));
            AssetDatabase.Refresh();

            RefreshPresetList();

            // 保存したプリセットを自動選択
            int idx = _presetPaths.IndexOf(savePath);
            if (idx >= 0) _selectedPresetIndex = idx;
            UpdatePresetInfo();

            SetStatus($"プリセット「{name}」を保存しました", StatusType.Success);
        }

        // ─── 読み込み ─────────────────────────────────────────────────────────

        private void LoadPreset(string path)
        {
            if (!File.Exists(path))
            {
                SetStatus("プリセットファイルが見つかりません", StatusType.Error);
                return;
            }

            try
            {
                string json = File.ReadAllText(path);
                var data    = JsonUtility.FromJson<UniTexPresetData>(json);
                if (data == null) throw new System.Exception("デシリアライズ失敗");

                // Section states
                showColorCorrection = data.showColorCorrection;
                showBlend           = data.showBlend;
                showLevels          = data.showLevels;
                showChannelMixer    = data.showChannelMixer;
                showSharpen         = data.showSharpen;
                showToneCurve       = data.showToneCurve;

                // Color Correction
                hueShift       = data.hueShift;
                saturation     = data.saturation;
                brightness     = data.brightness;
                gamma          = data.gamma;
                ccTargetColor  = new Color(data.ccTargetColorR, data.ccTargetColorG, data.ccTargetColorB, data.ccTargetColorA);
                ccBlendMode    = (BlendMode)data.ccBlendMode;
                ccBlendOpacity = data.ccBlendOpacity;

                // Tone Curve
                if (data.rgbCurve   != null && data.rgbCurve.keys.Count   > 0) rgbCurve   = data.rgbCurve.ToCurve();
                if (data.redCurve   != null && data.redCurve.keys.Count   > 0) redCurve   = data.redCurve.ToCurve();
                if (data.greenCurve != null && data.greenCurve.keys.Count > 0) greenCurve = data.greenCurve.ToCurve();
                if (data.blueCurve  != null && data.blueCurve.keys.Count  > 0) blueCurve  = data.blueCurve.ToCurve();
                useRGBCurve   = data.useRGBCurve;
                useRedCurve   = data.useRedCurve;
                useGreenCurve = data.useGreenCurve;
                useBlueCurve  = data.useBlueCurve;

                // Blend
                blendMode    = (BlendMode)data.blendMode;
                blendStrength = data.blendStrength;
                blendTiling  = data.blendTiling;
                blendScale   = new Vector2(data.blendScaleX, data.blendScaleY);
                blendOffset  = new Vector2(data.blendOffsetX, data.blendOffsetY);

                // Levels
                lvlMinInput  = data.lvlMinInput;
                lvlMaxInput  = data.lvlMaxInput;
                lvlMinOutput = data.lvlMinOutput;
                lvlMaxOutput = data.lvlMaxOutput;
                lvlMidGamma  = data.lvlMidGamma;

                // Sharpen
                sharpenMode       = (SharpenMode)data.sharpenMode;
                sharpenStrength   = data.sharpenStrength;
                sharpenKernelSize = data.sharpenKernelSize;
                sharpenIterations = data.sharpenIterations;

                // Channel Mixer
                cmOutRed   = (ChannelSource)data.cmOutRed;
                cmOutGreen = (ChannelSource)data.cmOutGreen;
                cmOutBlue  = (ChannelSource)data.cmOutBlue;
                cmOutAlpha = (ChannelSource)data.cmOutAlpha;

                // Mask
                invertMask   = data.invertMask;
                maskStrength = data.maskStrength;

                // テクスチャ（fullプリセットのみ）
                if (data.presetType == PRESET_TYPE_FULL)
                {
                    bool anyMissing = false;

                    if (!string.IsNullOrEmpty(data.maskTextureGUID))
                    {
                        var tex = LoadTextureByGUID(data.maskTextureGUID);
                        if (tex != null) { maskTexture = tex; processor.MaskTexture = tex; }
                        else anyMissing = true;
                    }
                    if (!string.IsNullOrEmpty(data.blendTextureGUID))
                    {
                        var tex = LoadTextureByGUID(data.blendTextureGUID);
                        if (tex != null) blendTexture = tex;
                        else anyMissing = true;
                    }
                    if (!string.IsNullOrEmpty(data.blendMaskTextureGUID))
                    {
                        var tex = LoadTextureByGUID(data.blendMaskTextureGUID);
                        if (tex != null) blendMaskTexture = tex;
                        else anyMissing = true;
                    }

                    string statusMsg = anyMissing
                        ? $"プリセット「{data.presetName}」を読み込みました（一部テクスチャが見つかりません）"
                        : $"プリセット「{data.presetName}」を読み込みました";
                    SetStatus(statusMsg, anyMissing ? StatusType.Error : StatusType.Success);
                }
                else
                {
                    SetStatus($"プリセット「{data.presetName}」を読み込みました", StatusType.Success);
                }

                RequestPreviewUpdate();
            }
            catch (System.Exception e)
            {
                Debug.LogError($"プリセット読み込みエラー: {e.Message}");
                SetStatus("プリセットの読み込みに失敗しました", StatusType.Error);
            }
        }

        // ─── 削除 ────────────────────────────────────────────────────────────

        private void DeletePreset(string path)
        {
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.Refresh();
            RefreshPresetList();
            SetStatus("プリセットを削除しました", StatusType.Info);
        }

        // ─── ユーティリティ ───────────────────────────────────────────────────

        private static Texture2D LoadTextureByGUID(string guid)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(assetPath)) return null;
            return AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
        }

        // ─── フォルダ保証 ─────────────────────────────────────────────────────

        private void EnsurePresetFolder()
        {
            if (!AssetDatabase.IsValidFolder(PRESET_FOLDER))
                AssetDatabase.CreateFolder("Assets", "UniTexEditor_Presets");
        }
    }
}
