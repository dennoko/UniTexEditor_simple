using UnityEngine;
using UnityEditor;

namespace UniTexEditor.Tests
{
    /// <summary>
    /// UniTexEditor の簡易テストユーティリティ
    /// </summary>
    public static class UniTexEditorTests
    {
        [MenuItem("Window/UniTex Editor/Run Simple Test")]
        public static void RunSimpleTest()
        {
            Debug.Log("=== UniTexEditor Simple Test ===");
            
            // 1. テクスチャプロセッサのインスタンス化テスト
            TextureProcessor processor = new TextureProcessor();
            Debug.Log("✓ TextureProcessor instantiated");
            
            // 2. テストテクスチャの作成
            Texture2D testTexture = CreateTestTexture(256, 256);
            processor.SourceTexture = testTexture;
            Debug.Log("✓ Test texture created and set");
            
            // 3. ColorCorrectionNode のテスト
            ColorCorrectionNode colorNode = new ColorCorrectionNode
            {
                hueShift = 30f,
                saturation = 1.2f,
                brightness = 1.1f,
                gamma = 1.0f
            };
            processor.AddNode(colorNode);
            Debug.Log("✓ ColorCorrectionNode added");
            
            // 4. 処理実行（Compute Shaderが利用可能な場合）
            if (SystemInfo.supportsComputeShaders)
            {
                try
                {
                    RenderTexture result = processor.ProcessAll();
                    if (result != null)
                    {
                        Debug.Log($"✓ Processing successful! Result: {result.width}x{result.height}");
                        
                        // プレビューとして保存（Linear色空間で正確に保存）
                        Texture2D resultTex = TextureProcessor.RenderTextureToTexture2D(result, true);
                        byte[] bytes = resultTex.EncodeToPNG();
                        string path = "Assets/UniTexEditor_TestResult.png";
                        System.IO.File.WriteAllBytes(path, bytes);
                        AssetDatabase.Refresh();
                        Debug.Log($"✓ Test result saved to: {path}");
                        
                        Object.DestroyImmediate(resultTex);
                    }
                    else
                    {
                        Debug.LogWarning("Processing returned null result");
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"✗ Processing failed: {e.Message}\n{e.StackTrace}");
                }
            }
            else
            {
                Debug.LogWarning("⚠ Compute Shaders not supported on this platform");
            }
            
            // 5. クリーンアップ
            processor.Cleanup();
            Object.DestroyImmediate(testTexture);
            Debug.Log("✓ Cleanup completed");
            
            Debug.Log("=== Test Complete ===");
        }
        
        [MenuItem("Window/UniTex Editor/Check System Info")]
        public static void CheckSystemInfo()
        {
            Debug.Log("=== System Information ===");
            Debug.Log($"Compute Shaders Supported: {SystemInfo.supportsComputeShaders}");
            Debug.Log($"Graphics Device Type: {SystemInfo.graphicsDeviceType}");
            Debug.Log($"Graphics Device Name: {SystemInfo.graphicsDeviceName}");
            Debug.Log($"Graphics Memory Size: {SystemInfo.graphicsMemorySize} MB");
            Debug.Log($"Max Texture Size: {SystemInfo.maxTextureSize}");
            Debug.Log($"Shader Level: {SystemInfo.graphicsShaderLevel}");
        }
        
        private static Texture2D CreateTestTexture(int width, int height)
        {
            Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            Color[] pixels = new Color[width * height];
            
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float r = (float)x / width;
                    float g = (float)y / height;
                    float b = 0.5f;
                    pixels[y * width + x] = new Color(r, g, b, 1f);
                }
            }
            
            texture.SetPixels(pixels);
            texture.Apply();
            
            return texture;
        }
    }
}
