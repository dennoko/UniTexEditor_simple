# UniTexEditor_simple — テクスチャ編集（非破壊）拡張

日本語ドキュメント

## 概要
`UniTexEditor_simple` は Unity Editor 拡張で、テクスチャに対する非破壊的な色調補正・合成・マスキング・UV対応ブラー等を高速に行うことを目的とします。処理は基本的に GPU（Compute Shader）で行い、高速な編集ワークフローを提供します。

## 主要機能
- 色調補正
  - HDR を用いた色合成（加算 / 乗算 / オーバーレイ 等）
  - 色相（Hue Shift）
  - 彩度（Saturation）
  - 明度（Brightness/Value）
  - ガンマ補正（Gamma）
  - **トーンカーブ調整（RGB / R / G / B 個別）** ← NEW!
- 制御・合成
  - マスク指定（グレースケール）：適用範囲、強度、反転
  - テクスチャ合成（複数ブレンドモード：標準、乗算、加算、スクリーン、オーバーレイ 等）
  - 合成範囲のマスク制御
- UV アイランドに基づく特殊機能
  - UV アイランドの境界を越えないブラー処理（指定メッシュの UV 情報を使用）
- ワークフロー
  - 非破壊編集（レイヤ/プリセット）
  - 結果は新しいテクスチャとして出力、またはオプションで既存テクスチャを上書き可能
  - **レスポンシブなプレビュー表示（正方形維持、チェッカーボード背景）** ← NEW!

## 技術的前提（推奨）
- Unity 2020.3 LTS 以降を推奨
- GPU（DirectX 11 / Metal 等）で Compute Shader をサポートする環境
- エディタ実行環境は Windows / macOS を想定（モバイルは制限あり）

## クイックスタート
1. 本フォルダを Unity プロジェクトの `Assets/Editor/UniTexEditor_simple` に配置します。
2. Unity を開き、メニューの `Window > UniTex Editor` からエディタウィンドウを起動します。
3. テクスチャ、マスク、メッシュ（UV ブラーを使う場合）を指定してパラメータを調整します。
4. 「Apply（非破壊レイヤに追加）」または「Overwrite（上書き）」を選択します。

### 初回セットアップの確認
- `Window > UniTex Editor > Check System Info` でCompute Shader対応を確認
- `Window > UniTex Editor > Run Simple Test` で動作テストを実行
- テスト成功時は `Assets/UniTexEditor_TestResult.png` が生成されます

### 詳細な使い方
`SAMPLES.md` を参照してください。具体的なワークフローとサンプルを記載しています。

## ファイル構成
```
UniTexEditor_simple/
├── README.md              # 本ファイル
├── ARCHITECTURE.md        # 設計ドキュメント
├── SAMPLES.md            # サンプルとチュートリアル
├── CHANGELOG.md          # 変更履歴
├── LICENSE               # MITライセンス
├── Scripts/              # C# スクリプト
│   ├── UniTexEditor.asmdef
│   ├── ProcessingNode.cs
│   ├── TextureProcessor.cs
│   ├── UVIslandUtility.cs
│   ├── UVIslandBlurNode.cs
│   ├── UniTexEditorWindow.cs
│   └── UniTexEditorTests.cs
├── Resources/            # Compute Shaders
    ├── ColorCorrection.compute
    ├── Blend.compute
    ├── ToneCurve.compute
    └── UVIslandBlur.compute
```

## 最新の変更（v0.2.3）
- ✅ **パラメータ調整時の黒画面を修正**: Compute Shaderのマスクエラーを解決
- ✅ **RenderTextureFormat修正**: RGBA32→ARGB32に統一
- ✅ **安定性向上**: すべてのノードでダミーマスク対応

## 以前の変更（v0.2.2）
- ✅ **プレビュー表示の修正**: 真っ黒になる問題を解決、ノードなしでもソーステクスチャを表示
- ✅ **デバッグ強化**: プレビュー生成状況をConsoleで確認可能
- ✅ **メモリ管理改善**: ウィンドウクローズ時の確実なクリーンアップ

## 開発ロードマップ（概要）
1. ドキュメントとアーキテクチャ設計（完了）
2. 最小限の Editor ウィンドウと Compute Shader プロトタイプ
3. 合成・マスク機能の追加
4. UV アイランド対応ブラーの実装
5. テストと最適化、サンプル追加

## ライセンス
- 初期は MIT ライセンスを想定（必要なら変更してください）。

## 連絡・フィードバック
不具合や改善要望は Issues に記載してください。
