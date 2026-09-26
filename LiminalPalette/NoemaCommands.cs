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

        [LiminalCommand("Ui/Probe", Description = "いまクリックしたら届くか (表示・interactable・遮蔽)。届くなら \"ok\"、届かなければ理由")]
        public static string Probe(string id)
        {
            var node = UiQuery.FindById(id);
            if (node == null) return UiQuery.DescribeNotFound(id);
            var result = UiPointer.Probe(node);
            return result.Success ? "ok" : result.Message;
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

        [LiminalCommand("Ui/VisualAssert", Description = "UI カメラの描画をベースライン PNG と比較する。一致で \"OK\"、初回はベースラインを作って非 OK を返す")]
        public static string VisualAssert(string name, float threshold = UiVisualRegression.DEFAULT_THRESHOLD, string masks = "") =>
            UiVisualRegression.Assert(name, threshold, UiVisualRegression.ParseMasks(masks));

        [LiminalCommand("Ui/VisualUpdateBaseline", Description = "UI カメラの描画でベースライン PNG を上書きする (意図した見た目の変更時)")]
        public static string VisualUpdateBaseline(string name) => UiVisualRegression.UpdateBaseline(name);
    }
}
