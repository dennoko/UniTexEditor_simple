# 改善・バグ修正計画

## 1. UV アイランドブラー機能の削除
ユーザビリティの観点から、UV アイランドブラー機能をオミットします。

### 1.1. 削除対象ファイル
以下のファイルは本機能専用のため削除します。
- `Scripts/Nodes/UVIslandBlurNode.cs`
- `Scripts/Utils/UVIslandUtility.cs`
- `Scripts/Utils/UVBoundaryMaskUtility.cs`
- `Resources/UVIslandBlur.compute`

### 1.2. 変更が必要なファイル
UI およびロジックから本機能の参照を削除します。
- `Scripts/Editor/UniTexEditorWindow.cs`: 定義されているフィールド (`showUVBlur`, `blurRadius` 等) を削除。
- `Scripts/Editor/UniTexEditorWindow.GUI.cs`: UV アイランドブラーの UI セクションを削除。
- `Scripts/Editor/UniTexEditorWindow.Logic.cs`: `UpdatePreview` および `ApplyAndSave` 内のノード構築ロジックを削除。

## 2. トーンカーブのプレビュー消失バグ修正

### 2.1. 原因分析
`ToneCurveNode.cs` において、各カーブ（RGB, R, G, B）の使用フラグが `false` の場合、対応する `ComputeBuffer` が作成もバインドもされません。
多くのグラフィックス API (特に DirectX 11/12) では、シェーダー内で宣言された `StructuredBuffer` がブランチで使用されない場合でも、リソースがバインドされていないと描画エラー（黒画面）や不安定な動作を引き起こすことがあります。

### 2.2. 修正計画
`ToneCurveNode.cs` を修正し、カーブの使用有無にかかわらず、常に有効なバッファを作成してシェーダーにバインドするように変更します。
- `useRGBCurve` 等のフラグにかかわらず、すべての `ComputeBuffer` (`rgbBuffer` 等) を初期化する。
- 常に `SetBuffer` を行う。
- 使用しないカーブにはデフォルト（Linear / Identity）のデータをセットしておくことで、万が一フラグの不整合があっても安全に動作させる。

## 3. 検証計画

### 3.1. ビルド検証
- コンパイルエラーが発生しないことを確認。
- 削除したファイルへの参照が残っていないか確認。

### 3.2. 動作検証
- **UV削除**: エディタウィンドウを起動し、エラーが出ないこと、UI から該当項目が消えていることを確認。
- **トーンカーブ**: 
    - トーンカーブを有効にした直後にプレビューが表示されること。
    - 各チェックボックス (RGB, R, G, B) を切り替えてもプレビューが消えないこと。
    - パラメータリセットを行っても正常に動作すること。
