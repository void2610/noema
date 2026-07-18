# noema チュートリアル

### 1. 初期設定 — 対象アセンブリの宣言

noema は「どのアセンブリの View を ID 供給源にするか」だけをプロジェクトから受け取る。未設定のままツリーを構築すると例外になる (設定ミスの即時検出)。

```csharp
// ランタイム (シーンロード前に一度)
public static class NoemaBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Init() => NoemaConfig.ProjectAssemblyPrefix = "MyGame";
}

// EditMode テスト (アセンブリ全体で一度)
[SetUpFixture]
public sealed class NoemaTestSetup
{
    [OneTimeSetUp]
    public void OneTimeSetUp() => NoemaConfig.ProjectAssemblyPrefix = "MyGame";
}
```

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

読む範囲は `[SerializeField]` と `[UiNodeSource]` の 2 つだけ (暗黙の型推定はしない)。`Dictionary<string, GameObject/Component>` はキーが、List/配列は index が ID になる。

### 5. 操作 API

```csharp
UiActions.SetSliderValue(node, 0.5f);   // onValueChanged 発火
UiActions.SetText(node, "name");        // InputField / TMP_InputField
UiActions.SetChecked(node, true);       // 目標状態と違う時だけ実クリック (遮蔽検証を継承)
UiActions.SelectOption(node, 2);        // TMP_Dropdown
UiActions.Scroll(node, 0f, 1f);         // ScrollRect 正規化位置 (y=1 が上端)
```

戻り値はすべて `Success` / `Message` を持つ。失敗理由 (`not interactable` 等) をそのままアサートメッセージに使える。

### 6. ビジュアル回帰

```csharp
var message = UiVisualRegression.Assert("TitleScreen"); // threshold 省略時 0.5%
Assert.AreEqual("OK", message, message);
```

- ベースラインは `Tests/VisualBaselines/<name>@1920x1080.png`。**初回実行は "baseline created" を返す** — これは成功ではないので、画像を目視確認してコミットし、再実行で比較を通すこと
- UI カメラ (ScreenSpaceCamera Canvas の worldCamera) を固定 1920x1080 の RenderTexture へ手動描画するため、Game View の解像度・フレーム末イベント・batchmode の描画有無に依存しない
- 差分時は `outputs/ui-visual/` に actual / diff PNG を吐く。CI ではこのディレクトリをアーティファクト収集する
- 意図的な見た目変更は `UiVisualRegression.UpdateBaseline(name)` で上書きする

### 7. テストランナーとの繋ぎ方

noema 自体はテストフレームワーク非依存の観測・操作 API 集で、常駐もフックもしない。E2E で使う場合は、プロジェクト側のコマンド基盤 (例: [LiminalPalette](https://github.com/void2610/liminal-palette)) に薄いブリッジを 1 枚置く:

```csharp
public sealed class UiDebugCommands
{
    [LiminalCommand("Ui/Click")]
    public string Click(string id) => UiPointer.Click(UiQuery.FindById(id)).ToString();

    [LiminalCommand("Ui/Visible")]
    public string Visible(string id) => UiQuery.FindById(id)?.Visible.ToString() ?? "NotFound";
}
```

演出やロードで UI が未操作可能な瞬間があるため、シナリオ側は「クリックが受理されるまでポーリング」する形にすると flaky にならない (単発実行 + 固定待ちは避ける)。
