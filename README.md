# noema

Unity uGUI のセマンティック UI テストライブラリ。UI を座標やピクセルでなく「意味 (role / 安定 ID / 状態)」として観測・操作・検証する。

名前は現象学の *noema* (意識に意味として与えられた対象) に由来。

## 機能

- **セマンティックツリー**: uGUI/TMP 階層を role (Button/Checkbox/Textbox/Slider/Combobox/ScrollArea/Text/Clickable) と安定 ID でスナップショット化 (`UiTreeBuilder`)
- **安定 ID**: View の `[SerializeField]` フィールド逆引き (`ViewType/fieldName`)。動的生成 UI は `[UiNodeSource]` 宣言で `field[key]` 形式 (`UiViewIdMap`)
- **実 Raycast クリック**: EventSystem の Raycast を通すため、遮蔽・Raycast Target 切れ・interactable 切れをテスト失敗として検出 (`UiPointer`)
- **操作**: Slider/Textbox/Toggle/Dropdown/ScrollRect への意味的操作 (`UiActions`)
- **ビジュアル回帰**: UI カメラの固定解像度 RenderTexture 描画によるベースライン PNG 比較。batchmode CI 対応・Game View 解像度非依存 (`UiVisualRegression`)

## インストール

Package Manager の "Add package from git URL"、または `Packages/manifest.json` に追加する:

```json
"com.void2610.noema": "https://github.com/void2610/noema.git"
```

- 本体 asmdef (`Void2610.Noema`) は `UNITY_EDITOR || DEVELOPMENT_BUILD || NOEMA_FORCE_ENABLE` の開発ビルド限定
- `Void2610.Noema.Abstractions` (`[UiNodeSource]` のみ) は常時コンパイルで、View 側の宣言が製品ビルドを壊さない

## チュートリアル

インストールから E2E 連携までの手順は [Documentation~/tutorial.md](./Documentation~/tutorial.md) を参照。

## 制約

- 対象は uGUI + TextMeshPro (UI Toolkit / IMGUI は対象外)
- ビジュアル回帰は ScreenSpaceCamera の Canvas が対象 (ScreenSpaceOverlay / UITK は写らない)
- ID が重複した場合 (同型 View の複数インスタンス等) は最初のノードが勝つ
