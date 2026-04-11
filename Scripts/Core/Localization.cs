using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace UniTexEditor
{
    [Serializable]
    public class LocalizationData
    {
        public List<LocalizationItem> items;
    }

    [Serializable]
    public class LocalizationItem
    {
        public string key;
        public string text;
        public string tooltip;
    }

    public static class Localization
    {
        private static string currentLanguage = "ja";
        private static Dictionary<string, GUIContent> contentCache = new Dictionary<string, GUIContent>();

        public static string CurrentLanguage
        {
            get => currentLanguage;
            set
            {
                if (currentLanguage != value)
                {
                    currentLanguage = value;
                    LoadLanguage(currentLanguage);
                    EditorPrefs.SetString("UniTexEditor_Language", currentLanguage);
                }
            }
        }

        public static void Initialize()
        {
            currentLanguage = EditorPrefs.GetString("UniTexEditor_Language", "ja");
            LoadLanguage(currentLanguage);
        }

        private static void LoadLanguage(string langCode)
        {
            contentCache.Clear();

            TextAsset jsonAsset = Resources.Load<TextAsset>($"Localization/{langCode}");
            if (jsonAsset == null)
            {
                jsonAsset = Resources.Load<TextAsset>("Localization/ja");
                if (jsonAsset == null) return;
            }

            try
            {
                var data = JsonUtility.FromJson<LocalizationData>(jsonAsset.text);
                foreach (var item in data.items)
                {
                    contentCache[item.key] = new GUIContent(item.text, item.tooltip);
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[UniTexEditor] Failed to load localization: {e.Message}");
            }
        }

        /// <summary>
        /// 指定したキーの GUIContent を取得。
        /// 見つからない場合はキー自身を text とする GUIContent を生成してキャッシュする。
        /// </summary>
        public static GUIContent GetContent(string key)
        {
            if (contentCache.TryGetValue(key, out var content))
                return content;

            // キャッシュミスした場合もキャッシュに登録して以後のアロケートを防ぐ
            var fallback = new GUIContent(key);
            contentCache[key] = fallback;
            return fallback;
        }

        /// <summary>
        /// 指定したキーのテキストを取得
        /// </summary>
        public static string GetText(string key)
        {
            if (contentCache.TryGetValue(key, out var content))
                return content.text;
            return key;
        }
    }
}
