# Changelog

## [Unreleased]

### Added
- `UiNavigation.NavigateAsync` / `IUiNavigationDriver` / `InputSystemNavigationDriver`: 仮想ゲームパッドの十字キーを実入力の経路で押す。EventSystem の move を止めて自前でナビゲーションを解決するライブラリ (arinn など) でも、ゲーム側の配線まで含めて検証できる
- LiminalPalette コマンド `Ui/Navigate`
- `UiNavigation.PressAsync` / `IUiPadButtonDriver` / `UiPadButton`: 仮想ゲームパッドのボタン (決定の South、Cancel の East など) を実入力の経路で押す。Graphic を持たない要素 (仮想カーソルのアンカーなど) の決定や、入力アクションを購読する Cancel まで検証できる
- LiminalPalette コマンド `Ui/Press`
- `UiNavigation.PressKeyAsync` / `IUiKeyDriver`: 仮想キーボードのキーを実入力の経路で押す。キーボードのデバイスを見て分岐する処理 (ESC でポーズを開く等) まで検証できる
- LiminalPalette コマンド `Ui/PressKey` `Ui/Alpha` (実効 alpha を小数 2 桁で返し、フェードの途中と完了を観測する)

### Changed
- 仮想マウスと仮想ゲームパッドが入力の設定 (バックグラウンドの扱い・Game View への回し方) を共有し、両方が操作を終えたときに元へ戻すようになった

## [0.3.0] - 2026-09-28

### Added
- `UiInspect`: スプライト名 (`SpriteName`)・色 (`ColorHex`)・実効 alpha (`EffectiveAlpha`)・コレクション直下の要素数 (`CountItems`) の観測
- `UiStability.IsStableAsync`: ノードの矩形と実効 alpha が指定フレーム数変わらないかで演出の静止を待つ
- `UiLinkPointer.ClickLinkAsync` / `ClickLinkWithinAsync`: TMP テキスト内の `<link>` を、位置でリンクに当たることを確かめてから実入力で押す。テキスト (の行) 自身がクリックを受けるなら EventSystem で押し、手前の別要素がデバイスのポインタ位置で判定する構成 (全画面の送りボタン等) のときだけ仮想マウスで押す。行が入力を止めている間 (フェード中等) は失敗を返す
- LiminalPalette コマンド `Ui/CountItems` `Ui/TextContains` `Ui/Sprite` `Ui/Color` `Ui/Stable` `Ui/ClickLink` `Ui/ClickLinkWithin`

### Changed
- Clickable / Draggable / DropTarget の `Interactable` が、祖先 `CanvasGroup` の `interactable` / `blocksRaycasts` を反映するようになった。Clickable へのクリックと Draggable のドラッグは、操作不可なら失敗を返す (成功待ちのポーリングが入力解禁の待ちになる)
- `UiPointer.Drag` は、`OnBeginDrag` が `pointerDrag` を外した (掴めなかった) とき、および指定した受け皿以外に落ちたときに失敗を返す。以前はどちらも成功扱いで、人間には不可能な操作をテストが通していた

## [0.2.0]

### Added
- 同梱の LiminalPalette ブリッジ (`NoemaCommands`)。`Ui/Probe` `Ui/Hover` `Ui/Submit` `Ui/DeviceClick` `Ui/Exists` `Ui/Count` などを含む
- ワールド要素 (`IUiNodeProvider`)、Input System の仮想マウス (`UiWorldPointer` / `InputSystemPointerDriver`)
- コレクション要素 View の ID 合成、`UiQuery.FindWithin` / `DescribeNotFound`、`Element` role、ビジュアル回帰の静止待ちとマスク

### Changed (0.1.0 からの移行)
- 利用側で自前の LP ブリッジを持っていた場合は削除して同梱版へ移る。コマンド名 (`Ui/SetSliderValue` → `Ui/SetSlider`、`Ui/CountByRole` → `Ui/Count` 等)、bool の表記 (`True` → `true`)、`Ui/Text` のタグ除去、`Ui/Drag` の引数名 (`from` / `to`) が変わる
