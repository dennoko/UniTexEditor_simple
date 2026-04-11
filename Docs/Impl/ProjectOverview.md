# プロジェクト構成と概要

## 1. 概要
`UniTexEditor_simple` は Unity Editor 内で完結する非破壊テクスチャ編集ツールです。
主に Compute Shader を使用し、GPU アクセラレーションによる高速なプレビューと処理を実現しています。

## 2. ディレクトリ構成

```
UniTexEditor_simple/
├── ARCHITECTURE.md         # システム設計書
├── CHANGELOG.md            # 変更履歴
├── LICENSE                 # ライセンスファイル
├── README.md               # ユーザーマニュアル (簡易)
├── ROADMAP.md              # 開発ロードマップ
├── SAMPLES.md              # サンプル・チュートリアル
├── Docs/                   # (New) ドキュメントフォルダ
│   ├── ProjectOverview.md  # 本ファイル
│   └── ImplementationPlan.md # 実装・リファクタリング計画
├── Resources/              # Compute Shader リソース (動的ロード用)
│   ├── Blend.compute           # テクスチャ合成
│   ├── ColorCorrection.compute # 色調補正
│   ├── ToneCurve.compute       # トーンカーブ
│   └── UVIslandBlur.compute    # UV境界考慮ブラー
└── Scripts/                # C# ソースコード
    ├── UniTexEditor.asmdef     # Assembly Definition
    ├── UniTexEditorWindow.cs   # メイン EditorWindow UI
    ├── TextureProcessor.cs     # 処理パイプライン管理
    ├── ProcessingNode.cs       # ノード基底クラス / 色調補正 / ブレンドノード
    ├── SharpenNode.cs          # シャープネス / ガウシアンブラーノード
    ├── ToneCurveNode.cs        # トーンカーブノード
    ├── UVIslandBlurNode.cs     # UVアイランドブラーノード
    ├── UVIslandUtility.cs      # UV解析・アイランド検出ロジック
    ├── UVBoundaryMaskUtility.cs # UV境界マスク生成ユーティリティ
    └── UniTexEditorTests.cs    # ユニットテスト
```

## 3. 主要クラスの役割

### 3.1. UI / エディタ操作
- **`UniTexEditorWindow`**: 
  - ツールのメインウィンドウ。
  - ユーザー入力の受付、プレビュー表示の更新、最終的な保存処理を担当。
  - 各機能（色調補正、ブレンド、ブラー等）のパラメータを管理し、変更時に `TextureProcessor` にノードを再構築してプレビュー更新を要求する。

### 3.2. コアロジック (パイプライン)
- **`TextureProcessor`**:
  - 非破壊編集パイプラインの中核。
  - `ProcessingNode` のリストを保持し、入力テクスチャから順番に処理を適用して結果を生成する。
  - `Working Texture` (RenderTexture) のライフサイクル管理を行う。
  - Linear/sRGB 色空間の変換ユーティリティも提供。

### 3.3. 処理ノード
- **`ProcessingNode` (abstract)**: 全ての処理ノードの基底クラス。
- **`ColorCorrectionNode`**: HSV調整、ガンマ補正。 (`ProcessingNode.cs` 内)
- **`BlendNode`**: テクスチャ合成（乗算、加算、オーバーレイ等）。 (`ProcessingNode.cs` 内)
- **`SharpenNode`**: アンシャープマスク、ガウシアンブラー。
- **`ToneCurveNode`**: RGB各チャンネルのカーブ調整。
- **`UVIslandBlurNode`**: UVアイランド情報を利用したブラー処理。

### 3.4. ユーティリティ
- **`UVIslandUtility` / `UVBoundaryMaskUtility`**:
  - メッシュのUV情報を解析し、アイランドごとのマスクや境界情報を生成する。
  - UVアイランドブラー機能で使用される。

## 4. 現在のデータフロー
1. **入力**: ユーザーがテクスチャ、マスク、パラメータを設定。
2. **構築**: `UniTexEditorWindow` が `TextureProcessor` に処理ノード (`ProcessingNode` 派生クラス) のリストを登録。
3. **処理**: `TextureProcessor.ProcessAll()` が呼ばれる。
   - `Resources` から Compute Shader をロード。
   - 各ノードが GPU 上で処理を実行（RenderTexture 間で Ping-Pong）。
4. **出力**: 
   - **プレビュー**: 低解像度で処理し、EditorWindow に表示。
   - **保存**: フル解像度で再処理し、PNG として保存（必要に応じて sRGB 変換）。
