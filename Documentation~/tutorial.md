# noema チュートリアル

### 1. 初期設定 — 対象アセンブリの宣言

noema は「どのアセンブリの View を ID 供給源にするか」だけをプロジェクトから受け取る。未設定のままツリーを構築すると例外になる (設定ミスの即時検出)。

```csharp
// ランタイム (シーンロード前に一度)
public static class NoemaBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Init()
    {
        NoemaConfig.ProjectAssemblyPrefix = "MyGame";
        // 設定画面など別アセンブリに切り出した自作 UI も ID の供給源にするなら追加する
        NoemaConfig.AdditionalAssemblyPrefixes = new[] { "MyCompany.Settings" };
    }
}

// EditMode テスト (アセンブリ全体で一度)
[SetUpFixture]
public sealed class NoemaTestSetup
{
    private IDisposable _prefixScope;

    [OneTimeSetUp]
    public void OneTimeSetUp() => _prefixScope = NoemaConfig.PushProjectAssemblyPrefix("MyGame");

    [OneTimeTearDown]
    public void OneTimeTearDown() => _prefixScope?.Dispose();
}
```

`ProjectAssemblyPrefix` はグローバルな可変 static なので、直接代入すると同一 EditMode 実行に載る他アセンブリのテスト (UPM パッケージ同梱のテスト等) へ値が漏れる。`PushProjectAssemblyPrefix` は戻り値の `IDisposable` を Dispose した時点で元の値へ戻る。**戻り値は必ず保持して Dispose すること** (捨てると直接代入と同じ恒久上書きになる)。

### 2. ツリーを観る

```csharp
foreach (var node in UiTreeBuilder.Build())
    Debug.Log(node); // Role \t Id \t Text \t visible= \t interactable=
```

ID は View クラスの `[SerializeField]` フィールド名から `TitleView/startButton` のように決まる。GameObject 名やヒエラルキーパスに依存しないため、シーン構成を変えてもテストが壊れない。

### 3. 探して、クリックする

```csharp
var node = UiQuery.FindById("TitleView/startButton");
var result = UiPointer.Click(node);
Assert.IsTrue(result.Success, result.Message);
```

`UiPointer.Click` は座標を直接叩かず EventSystem の Raycast を通す。**他 UI に遮蔽されている / `raycastTarget` が切れている / `interactable` が false** のボタンはクリックが失敗になり、「人間には押せないのにテストは通る」偽陽性を防ぐ。

テキストや role からも探せる (ローカライズ・画像ボタン化を見据え、ID 検索を第一選択にする):

```csharp
UiQuery.FindByRole(UiRole.Button, "はじめから");
UiQuery.FindAll(UiRole.Slider);
```

### 4. 動的生成 UI — `[UiNodeSource]`

実行時に生成する UI は SerializeField に載らないため、供給源フィールドを明示宣言する:

```csharp
public sealed class ReportView : MonoBehaviour
{
    // ID は "ReportView/keywordButtons[emotion]" のようにキー付きで安定する
    [UiNodeSource] private readonly Dictionary<string, GameObject> _keywordButtons = new();
}
```

Button や Text 等の役割を持たない要素でも、View のフィールドが名指しで握っていれば `Element` ノードとして表示状態を観測できる (送り待ちの ▽ 等)。

読む範囲は `[SerializeField]` と `[UiNodeSource]` の 2 つだけ (暗黙の型推定はしない)。`Dictionary<string, GameObject/Component>` はキーが、List/配列は index が ID になる。

コレクションの要素が View 自身のとき、その View のフィールドの ID は親の要素 ID から合成される。同じ型の View が並ぶ手札のような UI でも ID が衝突しない:

```csharp
public sealed class HandView : MonoBehaviour
{
    [UiNodeSource] private readonly List<CardView> _cards = new();   // "HandView/cards[2]"
}

public sealed class CardView : MonoBehaviour
{
    [SerializeField] private Button button;                         // "HandView/cards[2]/button"
}
```

### 5. 操作 API

```csharp
UiActions.SetSliderValue(node, 0.5f);   // onValueChanged 発火
UiActions.SetText(node, "name");        // InputField / TMP_InputField
UiActions.SetChecked(node, true);       // 目標状態と違う時だけ実クリック (遮蔽検証を継承)
UiActions.SelectOption(node, 2);        // TMP_Dropdown
UiActions.Scroll(node, 0f, 1f);         // ScrollRect 正規化位置 (y=1 が上端)
```

戻り値はすべて `Success` / `Message` を持つ。失敗理由 (`not interactable` 等) をそのままアサートメッセージに使える。

ポインタ以外の操作と観測:

```csharp
UiPointer.Hover(node);                  // Raycast の最前面から祖先へ enter を送る (ツールチップ等)
UiPointer.Unhover();
UiPointer.Submit(node);                 // 選択してから submit (ゲームパッド決定相当)
UiPointer.Probe(node);                  // クリックせずに、いま届くかだけを判定
node.Focused;                           // EventSystem の選択中要素か
node.PlainText;                         // リッチテキストタグ (<sprite> 等) を除いた表示文字
UiQuery.DescribeNotFound(id);           // 近い ID を候補に挙げた失敗メッセージ
UiInspect.SpriteName(node);             // Image / SpriteRenderer のスプライト名 (台紙の色差分等)
UiInspect.ColorHex(node);               // Graphic の色 "RRGGBBAA" (ランプの点灯色等)
UiInspect.CountItems("SaveView/slots"); // コレクション直下の表示中要素数 (要素の子は数えない)
await UiStability.IsStableAsync(id, 5); // 矩形と実効 alpha が 5 フレーム動かなければ true (演出の静止待ち)
await UiLinkPointer.ClickLinkAsync(node, "keyword"); // TMP の <link="keyword"> を実入力で押す
```

**テストのためだけの getter を View に足す前に、ここで読めないかを確かめる。** 表示・文字・画像・色・件数・静止はノードから読めるので、
View に `IsVisible` / `CurrentText` / `ShownCount` / `IsSettled` のような検証用 public を足す必要はない。
入場演出中などの入力停止は `CanvasGroup` (`interactable` / `blocksRaycasts`) で表すと、カスタムクリック要素も `Interactable=false` になり、
クリック成功待ちのポーリングがそのまま入力解禁の待ちになる (View に `IsReady` のようなフラグを足さずに済む)。

### 5.5 ワールド要素 — `IUiNodeProvider`

タイルマップのマスのように uGUI でない操作対象は、プロバイダがノードとして供給する。操作は EventSystem を通らず、
Input System に仮想マウスを追加して状態イベントを流す (`UiWorldPointer`)。ゲームが読む入力アクションの配線まで含めて検証される:

```csharp
public sealed class MapNodeProvider : MonoBehaviour, IUiNodeProvider
{
    private void OnEnable() => UiNodeProviders.Register(this);
    private void OnDisable() => UiNodeProviders.Unregister(this);

    public void Collect(List<UiNode> nodes)
    {
        foreach (var tile in _tiles)
            nodes.Add(new UiNode(UiRole.Clickable, $"Map/tile[{tile.x},{tile.y}]", "", visible: true, interactable: true, ScreenRectOf(tile), this, this));
    }

    // その座標を実入力したとき本当にこのマスが拾われるか (手前の地形に遮られていないか) を返す
    public string Probe(UiNode node, Vector2 screenPosition) => PickTile(screenPosition) == TileOf(node) ? null : "occluded";
}

await UiWorldPointer.ClickAsync(UiQuery.FindById("Map/tile[3,4]"));
```

操作中は実マウスを無効化する (位置が Value 型アクションの場合、複数デバイスでは値の大きい側が勝つため)。`HoverAsync` は乗せたままにするので、外すときは `UiWorldPointer.Unhover()` を呼ぶ。

### 6. ビジュアル回帰

```csharp
var message = UiVisualRegression.Assert("TitleScreen"); // threshold 省略時 0.5%
Assert.AreEqual("OK", message, message);
```

- ベースラインは `Tests/VisualBaselines/<name>@1920x1080.png`。**初回実行は "baseline created" を返す** — これは成功ではないので、画像を目視確認してコミットし、再実行で比較を通すこと
- UI カメラ (ScreenSpaceCamera Canvas の worldCamera) を固定 1920x1080 の RenderTexture へ手動描画するため、Game View の解像度・フレーム末イベント・batchmode の描画有無に依存しない
- 差分時は `outputs/ui-visual/` に actual / diff PNG を吐く。CI ではこのディレクトリをアーティファクト収集する
- 意図的な見た目変更は `UiVisualRegression.UpdateBaseline(name)` で上書きする
- 毎回変わる領域 (時計・パーティクル) は `masks` (正規化矩形、左下原点) で比較から外せる
- `AssertWhenStableAsync` (ブリッジの `Ui/VisualAssert`) は実時間で一定間隔ごとに撮り直し、連続 2 枚が一致してから比較する。実時間で動く Animator や Selectable の色遷移のように、フレーム数では完了時点が決まらない演出を待つため
- 保存先と解像度は `NoemaConfig.VisualBaselineDirectory` / `VisualCaptureWidth` 等で変えられる

### 7. テストランナーとの繋ぎ方

noema 自体はテストフレームワーク非依存の観測・操作 API 集で、常駐もフックもしない。
[LiminalPalette](https://github.com/void2610/liminal-palette) が入っているプロジェクトでは、同梱のブリッジ (`NoemaCommands`) が自動で有効になり、次のコマンドが使える:

| コマンド | 戻り値 |
|---|---|
| `Ui/Click` `Ui/Hover` `Ui/Submit` `Ui/Drag` | `clicked: <id>` / `failed: <理由>` |
| `Ui/ClickWithin` `Ui/IdWithin` | ID の接頭辞と表示文字で一覧の 1 件を選ぶ (カード名で選ぶ等) |
| `Ui/DeviceClick` | 見えている点を仮想マウスで押す (入力アクションを購読する UI 向け) |
| `Ui/Navigate` | ゲームパッドの十字キーを仮想ゲームパッドで押して離す (`direction`, `times`)。`navigated: <direction> x<times>`。動いた先は `Ui/Focused` で観測する |
| `Ui/Press` | ゲームパッドのボタン (`South` `East` `West` `North` `LeftShoulder` `RightShoulder` `Start` `Select`) を仮想ゲームパッドで押して離す (`button`, `times`)。`pressed: <button> x<times>` |
| `Ui/Probe` | `ok` / 届かない理由 |
| `Ui/Exists` `Ui/Visible` `Ui/Interactable` | `true` / `false` |
| `Ui/Text` | リッチテキストタグを除いた表示文字 |
| `Ui/Focused` | フォーカス中のノードの ID |
| `Ui/Count` | ID が prefix で始まる表示中のノード数 |
| `Ui/CountItems` | コレクション直下 (`View/field[key]`) の表示中要素数 |
| `Ui/TextContains` | 表示文字が部分文字列を含むか (`true` / `false`) |
| `Ui/Sprite` `Ui/Color` | スプライト名 / 色 `RRGGBBAA` |
| `Ui/Stable` | 矩形と実効 alpha が指定フレーム数動かなければ `true` |
| `Ui/ClickLink` `Ui/ClickLinkWithin` | TMP 内の `<link>` を実入力で押す (Within は prefix 配下からリンクを含む行を探す)。`clicked: <id>#<linkId>` |
| `Ui/Tree` | ツリーのダンプ (ID の確認用) |
| `Ui/VisualAssert` `Ui/VisualUpdateBaseline` | `OK` / 差分の詳細 |

ノードが見つからないときは、近い ID を候補に添えた `node '<id>' not found (候補: ...)` を返す。

演出やロードで UI が未操作可能な瞬間があるため、シナリオ側は「クリックが受理されるまでポーリング」する形にすると flaky にならない (単発実行 + 固定待ちは避ける)。
