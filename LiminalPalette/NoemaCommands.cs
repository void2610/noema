using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Void2610.LiminalPalette;

namespace Void2610.Noema
{
    /// <summary>
    /// noema の観測・操作を LiminalPalette のコマンドとして公開するブリッジ。
    /// 戻り値はシナリオの文字列比較 (AssertCommandReturns / AssertCommandEventually) でそのまま使える形に揃える。
    /// bool は "true" / "false"、ノードが見つからないときは近い ID の候補を添えた "not found" を返す
    /// </summary>
    public static class NoemaCommands
    {
        private const string TRUE = "true";
        private const string FALSE = "false";

        [LiminalCommand("Ui/Click", Description = "ID のノードを実入力経路でクリックする。uGUI は EventSystem の Raycast、ワールド要素は仮想マウス。成功で \"clicked: <id>\"")]
        public static async Task<string> Click(string id)
        {
            var node = UiQuery.FindById(id);
            if (node == null) return UiQuery.DescribeNotFound(id);
            var result = node.IsWorld ? await UiWorldPointer.ClickAsync(node) : UiPointer.Click(node);
            return result.ToString();
        }

        [LiminalCommand("Ui/ClickText", Description = "表示文字 (部分一致) で見つけたボタンを実 Raycast でクリックする。ID の無い要素向け。成功で \"clicked: <id>\"")]
        public static string ClickText(string text)
        {
            var node = UiQuery.FindByRole(UiRole.Button, text);
            return node == null ? $"button with text '{text}' not found" : UiPointer.Click(node).ToString();
        }

        [LiminalCommand("Ui/DeviceClick", Description = "ID のノードの見えている点を入力デバイス (仮想マウス) で押す。EventSystem ではなく入力アクションを購読する UI 向け。成功で \"clicked: <id>\"")]
        public static async Task<string> DeviceClick(string id)
        {
            var node = UiQuery.FindById(id);
            if (node == null) return UiQuery.DescribeNotFound(id);
            return (await UiWorldPointer.DeviceClickAsync(node)).ToString();
        }

        [LiminalCommand("Ui/DeviceHover", Description = "ID のノードの上へ入力デバイス (仮想マウス) を動かして乗せたままにする。押せる要素は実 Raycast で届く点へ、届く点が無ければ矩形の中心へ動かす。ポインタの位置をデバイスから読むホバー選択の操作用。外すのは Ui/Unhover。成功で \"hovered: <id>\"")]
        public static async Task<string> DeviceHover(string id)
        {
            var node = UiQuery.FindById(id);
            if (node == null) return UiQuery.DescribeNotFound(id);
            var result = await UiWorldPointer.DeviceHoverAsync(node);
            return result.Success ? $"hovered: {result.Message}" : $"failed: {result.Message}";
        }

        [LiminalCommand("Ui/Navigate", Description = "ゲームパッドの十字キーを direction (Up / Down / Left / Right) へ times 回押して離す (仮想ゲームパッド)。EventSystem ではなく入力デバイスへ流すので、ゲーム側のナビゲーションの配線まで通る。成功で \"navigated: <direction> x<times>\"。動いた先は Ui/Focused で観測する")]
        public static async Task<string> Navigate(string direction, int times = 1)
        {
            var result = await UiNavigation.NavigateAsync(direction, times);
            return result.Success ? $"navigated: {result.Message}" : $"failed: {result.Message}";
        }

        [LiminalCommand("Ui/Press", Description = "ゲームパッドのボタン button (South / East / West / North / LeftShoulder / RightShoulder / Start / Select) を times 回押して離す (仮想ゲームパッド)。決定 (South) や Cancel (East) を、選択中の要素や入力アクションへ実入力の経路で届ける。成功で \"pressed: <button> x<times>\"")]
        public static async Task<string> Press(string button, int times = 1)
        {
            var result = await UiNavigation.PressAsync(button, times);
            return result.Success ? $"pressed: {result.Message}" : $"failed: {result.Message}";
        }

        [LiminalCommand("Ui/PadHold", Description = "ゲームパッドのボタン button を押したままにする (仮想ゲームパッド)。押している間の Ui/Navigate と Ui/Press は、このボタンと同時押しとして届く (LB を押しながらの十字キーなど)。離すのは Ui/PadRelease。成功で \"holding: <button>\"")]
        public static async Task<string> PadHold(string button)
        {
            var result = await UiNavigation.HoldButtonAsync(button);
            return result.Success ? $"holding: {result.Message}" : $"failed: {result.Message}";
        }

        [LiminalCommand("Ui/PadRelease", Description = "Ui/PadHold で押したままのボタン button を離す。押していなければ何もしない。成功で \"released: <button>\"")]
        public static async Task<string> PadRelease(string button)
        {
            var result = await UiNavigation.ReleaseButtonAsync(button);
            return result.Success ? $"released: {result.Message}" : $"failed: {result.Message}";
        }

        [LiminalCommand("Ui/PressKey", Description = "キーボードの key (Input System のキー名。Escape / Enter / Tab など) を times 回押して離す (仮想キーボード)。キーボードのデバイスを見て分岐する処理 (ESC でポーズを開く等) まで実入力の経路で届く。成功で \"pressed: <key> x<times>\"")]
        public static async Task<string> PressKey(string key, int times = 1)
        {
            var result = await UiNavigation.PressKeyAsync(key, times);
            return result.Success ? $"pressed: {result.Message}" : $"failed: {result.Message}";
        }

        [LiminalCommand("Ui/ClickWithin", Description = "ID が prefix で始まり表示文字に text を含むノードを実入力経路でクリックする (一覧からカード名等で選ぶ)。成功で \"clicked: <prefix> '<text>'\"")]
        public static async Task<string> ClickWithin(string prefix, string text)
        {
            var node = UiQuery.FindWithin(prefix, text);
            if (node == null) return $"node under '{prefix}' with text '{text}' not found";
            var result = node.IsWorld ? await UiWorldPointer.ClickAsync(node) : UiPointer.Click(node);
            // どの ID が選ばれたかは呼び出し側に分からないため、成功時は入力から決まる文字列で返して期待値比較できるようにする
            return result.Success ? $"clicked: {prefix} '{text}'" : result.ToString();
        }

        [LiminalCommand("Ui/IdWithin", Description = "ID が prefix で始まり表示文字に text を含むノードの ID。無ければ (none)")]
        public static string IdWithin(string prefix, string text) => UiQuery.FindWithin(prefix, text)?.Id ?? "(none)";

        [LiminalCommand("Ui/Probe", Description = "いまクリックしたら届くか (表示・interactable・遮蔽)。届くなら \"ok\"、届かなければ理由")]
        public static string Probe(string id)
        {
            var node = UiQuery.FindById(id);
            if (node == null) return UiQuery.DescribeNotFound(id);
            var result = UiPointer.Probe(node);
            return result.Success ? "ok" : result.Message;
        }

        [LiminalCommand("Ui/Reachable", Description = "いまクリックしたら届くか (\"true\" / \"false\")。Ui/Probe の真偽版で、スクロールでビューポートの外へ出たか・遮られたかを期待値比較で待つのに使う")]
        public static string Reachable(string id)
        {
            var node = UiQuery.FindById(id);
            if (node == null) return UiQuery.DescribeNotFound(id);
            return UiPointer.Probe(node).Success ? TRUE : FALSE;
        }

        [LiminalCommand("Ui/Hover", Description = "ID のノードにポインタを乗せる (ワールド要素は仮想マウスを乗せたままにする)。外すのは Ui/Unhover")]
        public static async Task<string> Hover(string id)
        {
            var node = UiQuery.FindById(id);
            if (node == null) return UiQuery.DescribeNotFound(id);
            var result = node.IsWorld ? await UiWorldPointer.HoverAsync(node) : UiPointer.Hover(node);
            return result.Success ? $"hovered: {result.Message}" : $"failed: {result.Message}";
        }

        [LiminalCommand("Ui/Unhover", Description = "Ui/Hover で乗せたポインタを外す")]
        public static string Unhover() => UiWorldPointer.Unhover().Success ? "unhovered" : "failed";

        [LiminalCommand("Ui/Submit", Description = "ID のノードを選択して決定する (ゲームパッド / キーボードの決定相当)")]
        public static string Submit(string id)
        {
            var node = UiQuery.FindById(id);
            return node == null ? UiQuery.DescribeNotFound(id) : UiPointer.Submit(node).ToString();
        }

        [LiminalCommand("Ui/Drag", Description = "from のノードを掴んで to のノードへドラッグ&ドロップする")]
        public static string Drag(string from, string to)
        {
            var fromNode = UiQuery.FindById(from);
            if (fromNode == null) return UiQuery.DescribeNotFound(from);
            var toNode = UiQuery.FindById(to);
            if (toNode == null) return UiQuery.DescribeNotFound(to);
            return UiPointer.Drag(fromNode, toNode).ToString();
        }

        [LiminalCommand("Ui/Exists", Description = "ID のノードがツリーに在るか (\"true\" / \"false\")。不可視でも在れば true")]
        public static string Exists(string id) => UiQuery.FindById(id) != null ? TRUE : FALSE;

        [LiminalCommand("Ui/Visible", Description = "ID のノードが表示されているか (\"true\" / \"false\")。ノードが無ければ false")]
        public static string Visible(string id) => UiQuery.FindById(id) is { Visible: true } ? TRUE : FALSE;

        [LiminalCommand("Ui/Interactable", Description = "ID のノードが操作可能か (\"true\" / \"false\")")]
        public static string Interactable(string id)
        {
            var node = UiQuery.FindById(id);
            if (node == null) return UiQuery.DescribeNotFound(id);
            return node.Interactable ? TRUE : FALSE;
        }

        [LiminalCommand("Ui/Text", Description = "ID のノードの表示文字 (リッチテキストタグを除く)")]
        public static string Text(string id)
        {
            var node = UiQuery.FindById(id);
            return node == null ? UiQuery.DescribeNotFound(id) : node.PlainText;
        }

        [LiminalCommand("Ui/Focused", Description = "ゲームパッド / キーボードのフォーカスがあるノードの ID。無ければ (none)")]
        public static string Focused() => UiTreeBuilder.Build().FirstOrDefault(n => n.Focused)?.Id ?? "(none)";

        [LiminalCommand("Ui/Count", Description = "ID が prefix で始まる表示中のノード数 (動的生成の一覧の件数)")]
        public static string Count(string prefix) =>
            UiQuery.FindByIdPrefix(prefix).Count(n => n.Visible).ToString(CultureInfo.InvariantCulture);

        [LiminalCommand("Ui/CountItems", Description = "コレクションフィールド (\"View/field\") の直下の要素 (\"View/field[key]\") のうち表示中の数。要素の子は数えない")]
        public static string CountItems(string collectionId) => UiInspect.CountItems(collectionId).ToString(CultureInfo.InvariantCulture);

        [LiminalCommand("Ui/TextContains", Description = "ID のノードの表示文字 (タグ除去後) が text を含むか (\"true\" / \"false\")")]
        public static string TextContains(string id, string text)
        {
            var node = UiQuery.FindById(id);
            if (node == null) return UiQuery.DescribeNotFound(id);
            return node.PlainText.Contains(text) ? TRUE : FALSE;
        }

        [LiminalCommand("Ui/Sprite", Description = "ID のノードの Image / SpriteRenderer のスプライト名 (無ければ空文字)")]
        public static string Sprite(string id)
        {
            var node = UiQuery.FindById(id);
            return node == null ? UiQuery.DescribeNotFound(id) : UiInspect.SpriteName(node);
        }

        [LiminalCommand("Ui/Color", Description = "ID のノードの Graphic / SpriteRenderer の色 (\"RRGGBBAA\")")]
        public static string Color(string id)
        {
            var node = UiQuery.FindById(id);
            return node == null ? UiQuery.DescribeNotFound(id) : UiInspect.ColorHex(node);
        }

        [LiminalCommand("Ui/Alpha", Description = "ID のノードの実効 alpha (祖先の CanvasGroup と Graphic の色を掛けた値) を小数 2 桁で返す (\"0.00\" 〜 \"1.00\")。フェードの途中と完了を観測する")]
        public static string Alpha(string id)
        {
            var node = UiQuery.FindById(id);
            return node == null ? UiQuery.DescribeNotFound(id) : UiInspect.EffectiveAlpha(node.GameObject).ToString("0.00", CultureInfo.InvariantCulture);
        }

        [LiminalCommand("Ui/Stable", Description = "ID のノードの位置と実効 alpha が frames フレーム連続で変わらなければ \"true\" (演出の静止待ち)")]
        public static async Task<string> Stable(string id, int frames = 5) => await UiStability.IsStableAsync(id, frames) ? TRUE : FALSE;

        [LiminalCommand("Ui/ClickLink", Description = "ID のテキスト内の <link=linkId> を実入力で押す。成功で \"clicked: <id>#<linkId>\"")]
        public static async Task<string> ClickLink(string id, string linkId)
        {
            var node = UiQuery.FindById(id);
            if (node == null) return UiQuery.DescribeNotFound(id);
            return (await UiLinkPointer.ClickLinkAsync(node, linkId)).ToString();
        }

        [LiminalCommand("Ui/ClickLinkWithin", Description = "ID が prefix で始まる表示中ノードのうち <link=linkId> を含む最初のテキストのリンクを実入力で押す (行が実行時に並ぶ一覧向け)。成功で \"clicked: <prefix>#<linkId>\"")]
        public static async Task<string> ClickLinkWithin(string prefix, string linkId) =>
            (await UiLinkPointer.ClickLinkWithinAsync(prefix, linkId)).ToString();

        [LiminalCommand("Ui/Tree", Description = "セマンティックツリーを 1 行 1 ノードで返す (ID の確認用)。filter は ID の部分一致")]
        public static string Tree(string filter = "", bool visibleOnly = true)
        {
            var sb = new StringBuilder();
            foreach (var node in UiTreeBuilder.Build())
            {
                if (visibleOnly && !node.Visible) continue;
                if (!string.IsNullOrEmpty(filter) && !node.Id.Contains(filter)) continue;
                sb.AppendLine(node.ToString());
            }
            return sb.Length == 0 ? "(empty)" : sb.ToString().TrimEnd();
        }

        [LiminalCommand("Ui/SetText", Description = "入力欄へ文字列を設定する")]
        public static string SetText(string id, string text)
        {
            var node = UiQuery.FindById(id);
            return node == null ? UiQuery.DescribeNotFound(id) : UiActions.SetText(node, text).ToString();
        }

        [LiminalCommand("Ui/SetSlider", Description = "スライダーへ値を設定する")]
        public static string SetSlider(string id, float value)
        {
            var node = UiQuery.FindById(id);
            return node == null ? UiQuery.DescribeNotFound(id) : UiActions.SetSliderValue(node, value).ToString();
        }

        [LiminalCommand("Ui/SetChecked", Description = "トグルを目標状態にする (違うときだけ実クリック)")]
        public static string SetChecked(string id, bool isOn)
        {
            var node = UiQuery.FindById(id);
            return node == null ? UiQuery.DescribeNotFound(id) : UiActions.SetChecked(node, isOn).ToString();
        }

        [LiminalCommand("Ui/SelectOption", Description = "ドロップダウンの index 番目を選ぶ")]
        public static string SelectOption(string id, int index)
        {
            var node = UiQuery.FindById(id);
            return node == null ? UiQuery.DescribeNotFound(id) : UiActions.SelectOption(node, index).ToString();
        }

        [LiminalCommand("Ui/Scroll", Description = "スクロール領域を正規化位置 (0-1、y は 1 が上端) へ動かす")]
        public static string Scroll(string id, float x, float y)
        {
            var node = UiQuery.FindById(id);
            return node == null ? UiQuery.DescribeNotFound(id) : UiActions.Scroll(node, x, y).ToString();
        }

        [LiminalCommand("Ui/VisualAssert", Description = "UI カメラの描画が落ち着くのを待ってからベースライン PNG と比較する。一致で \"OK\"、初回はベースラインを作って非 OK を返す")]
        public static async Task<string> VisualAssert(string name, float threshold = UiVisualRegression.DEFAULT_THRESHOLD, string masks = "") =>
            await UiVisualRegression.AssertWhenStableAsync(name, threshold, UiVisualRegression.ParseMasks(masks));

        [LiminalCommand("Ui/VisualUpdateBaseline", Description = "UI カメラの描画でベースライン PNG を上書きする (意図した見た目の変更時)")]
        public static string VisualUpdateBaseline(string name) => UiVisualRegression.UpdateBaseline(name);
    }
}
