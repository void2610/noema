using System;
using System.Collections.Generic;
using System.Linq;

namespace Void2610.Noema
{
    /// <summary>
    /// セマンティックツリーに対する Playwright 風セレクタ
    /// 毎回 <see cref="UiTreeBuilder.Build"/> でスナップショットを取り直す (状態のキャッシュはしない)
    /// </summary>
    public static class UiQuery
    {
        public static UiNode FindById(string id) => UiTreeBuilder.Build().FirstOrDefault(n => n.Id == id);

        /// <summary>text は部分一致 (null なら role のみで絞る)</summary>
        public static IReadOnlyList<UiNode> FindAll(UiRole? role = null, string text = null) =>
            UiTreeBuilder.Build()
                .Where(n => (role == null || n.Role == role) &&
                            (text == null || n.Text.Contains(text, StringComparison.Ordinal)))
                .ToList();

        // 不可視の同名要素 (閉じた画面のボタン等) を拾って Click が誤失敗しないよう、可視ノードを優先する
        public static UiNode FindByRole(UiRole role, string text = null)
        {
            var matches = FindAll(role, text);
            return matches.FirstOrDefault(n => n.Visible) ?? matches.FirstOrDefault();
        }
    }
}
