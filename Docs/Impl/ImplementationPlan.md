# 実装・リファクタリング計画

## 1. 現状の課題分析

### 1.1. コード構成と可読性
- **クラスの混在**: `ProcessingNode.cs` に `ProcessingNode`（基底）、`ColorCorrectionNode`、`BlendNode` が混在しており、他のノード（`SharpenNode.cs` 等）と構成が一貫していません。
- **フラットなディレクトリ**: すべてのスクリプトが `Scripts/` 直下にあり、機能ごとの分類（Editor, Core, Nodes, Utils）がされていません。
- **肥大化した EditorWindow**: `UniTexEditorWindow.cs` が 900 行を超えており、UI 描画コードと内部ロジック（プレビュー生成、保存処理）が混在しています。

### 1.2. 設計・拡張性
- **ノード生成のハードコード**: `UniTexEditorWindow` 内で各ノードのインスタンス化とパラメータ設定が直接記述されており、新しいノードタイプを追加する際に修正箇所が多くなります。
- **リソース管理**: 各ノードの `Process` メソッド内で `Resources.Load` が毎回呼ばれる構造になっており、パフォーマンスとエラーハンドリングの観点で改善の余地があります。

## 2. リファクタリング計画

### 2.1. フェーズ 1: ディレクトリ構造の整理 (整理・統廃合)
スクリプトを機能別にサブフォルダへ移動し、見通しを良くします。

```
Scripts/
├── Editor/             # EditorWindow 関連
│   └── UniTexEditorWindow.cs
├── Core/               # コアロジック
│   └── TextureProcessor.cs
├── Nodes/              # 処理ノード
│   ├── ProcessingNode.cs       (基底クラスのみ)
│   ├── ColorCorrectionNode.cs  (独立ファイル化)
│   ├── BlendNode.cs            (独立ファイル化)
│   ├── SharpenNode.cs
│   ├── ToneCurveNode.cs
│   └── UVIslandBlurNode.cs
└── Utils/              # ユーティリティ
    ├── UVIslandUtility.cs
    └── UVBoundaryMaskUtility.cs
```

### 2.2. フェーズ 2: クラス分離と責務の明確化
1.  **`ProcessingNode.cs` の分割**:
    - `ColorCorrectionNode` と `BlendNode` をそれぞれ独立したファイルに分割します。
2.  **`UniTexEditorWindow` の分割 (Partial Class 活用)**:
    - `UniTexEditorWindow.cs`: ライフサイクル、メインエントリ
    - `UniTexEditorWindow.UI.cs`: `OnGUI` などの描画ロジック
    - `UniTexEditorWindow.Logic.cs`: プレビュー更新、保存処理などのロジック

### 2.3. フェーズ 3: アーキテクチャ改善 (中期的)
1.  **NodeFactory の導入**:
    - ノードの生成ロジックをファクトリクラスに委譲し、EditorWindow からノードの具象クラスへの依存を減らします。
2.  **リソース管理の最適化**:
    - Compute Shader のロードを単一の `ResourceManager` クラス、または `TextureProcessor` の初期化時に集約し、エラーチェックを早期に行えるようにします。

## 3. 実装ロードマップ

### Step 1: ドキュメント整備 (完了)
- 構成の把握とドキュメント化 (`Docs/ProjectOverview.md`)
- 改善計画の策定 (本ファイル)

### Step 2: ファイル構成の整理
1. `Scripts` フォルダ内にサブフォルダ (`Editor`, `Core`, `Nodes`, `Utils`) を作成。
2. 既存のスクリプトを適切なフォルダに移動。
3. `UniTexEditor.asmdef` の更新 (必要であれば)。

### Step 3: コードのリファクタリング
1. `ProcessingNode.cs` から具象クラスを抽出。
2. `UniTexEditorWindow` を Partial Class に分割して整理。

これらのリファクタリングにより、コードベースの保守性が向上し、今後の機能追加（例：ノードの並べ替え、カスタムノードの追加）が容易になります。
