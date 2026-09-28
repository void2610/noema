# noema

Unity uGUI のセマンティック UI テストライブラリ。UI を座標やピクセルでなく「意味 (role / 安定 ID / 状態)」として観測・操作・検証する。

名前は現象学の *noema* (意識に意味として与えられた対象) に由来。

## 機能

- **セマンティックツリー**: uGUI/TMP 階層を role (Button/Checkbox/Textbox/Slider/Combobox/ScrollArea/Text/Clickable) と安定 ID でスナップショット化 (`UiTreeBuilder`)
- **安定 ID**: View の `[SerializeField]` フィールド逆引き (`ViewType/fieldName`)。動的生成 UI は `[UiNodeSource]` 宣言で `field[key]` 形式 (`UiViewIdMap`)
- **実 Raycast クリック**: EventSystem の Raycast を通すため、遮蔽・Raycast Target 切れ・interactable 切れをテスト失敗として検出 (`UiPointer`)。中心が他の要素に隠れていれば矩形内の見えている点を探して押す (重なって並ぶ手札等)
- **操作**: Slider/Textbox/Toggle/Dropdown/ScrollRect への意味的操作 (`UiActions`)
- **ホバー / Submit / 到達判定**: ポインタを乗せる (`UiPointer.Hover`)、ゲームパッド決定相当 (`UiPointer.Submit`)、クリックせずに届くかだけを見る (`UiPointer.Probe`)
- **ワールド要素**: タイルマップのマス等を `IUiNodeProvider` でツリーへ供給し、Input System の仮想マウスで実入力経路から操作 (`UiWorldPointer`)
- **ビジュアル回帰**: UI カメラの固定解像度 RenderTexture 描画によるベースライン PNG 比較。batchmode CI 対応・Game View 解像度非依存・マスク領域指定 (`UiVisualRegression`)
- **見た目・件数・静止の観測**: スプライト名 / 色 (`UiInspect`)、コレクションの要素数 (`UiInspect.CountItems`)、演出の静止待ち (`UiStability`)
- **文字列内リンク**: TMP の `<link>` を位置判定込みで実入力クリック (`UiLinkPointer`)
- **LiminalPalette ブリッジ**: LP が入っていれば `Ui/Click` / `Ui/Text` / `Ui/Visible` 等のコマンドが自動で使える (`NoemaCommands`)

## インストール

Package Manager の "Add package from git URL"、または `Packages/manifest.json` に追加する:

```json
"com.void2610.noema": "https://github.com/void2610/noema.git"
```

- 本体 asmdef (`Void2610.Noema`) は `UNITY_EDITOR || DEVELOPMENT_BUILD || NOEMA_FORCE_ENABLE` の開発ビルド限定
- `Void2610.Noema.Abstractions` (`[UiNodeSource]` のみ) は常時コンパイルで、View 側の宣言が製品ビルドを壊さない
- `Void2610.Noema.InputSystem` (仮想マウス) は Input System パッケージ、`Void2610.Noema.LiminalPalette` (コマンドブリッジ) は LiminalPalette パッケージがあるときだけ有効になる

## チュートリアル

インストールから E2E 連携までの手順は [Documentation~/tutorial.md](./Documentation~/tutorial.md) を参照。

## テスト

`Tests/Editor` に EditMode テストを同梱している。git URL 経由で導入した場合、パッケージのテストは利用側の `Packages/manifest.json` に `testables` を書かないとコンパイル・実行されない:

```json
"testables": ["com.void2610.noema"]
```

これを入れると Test Runner の EditMode に `Void2610.Noema.Tests` が現れ、利用側の CI にも一緒に載る。

## 制約

- 対象は uGUI + TextMeshPro (UI Toolkit / IMGUI は対象外)
- ビジュアル回帰は ScreenSpaceCamera の Canvas が対象 (ScreenSpaceOverlay / UITK は写らない)
- ID が重複した場合 (同型 View の複数インスタンスを別々の View が握る等) は最初のノードが勝つ。コレクション要素として握られている View は親の要素 ID で合成されるため重複しない
