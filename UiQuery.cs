using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Void2610.Noema
{
    /// <summary>
    /// セマンティックツリーに対する Playwright 風セレクタ
    /// 毎回スナップショットを取り直す (状態のキャッシュはしない)
    /// </summary>
    public static class UiQuery
    {
        /// <summary>
        /// ID で 1 件を探す。View フィールド由来の ID はツリー全体を歩かずに解決し、
        /// 階層パス由来の ID・ワールド要素・ID が重複している場合だけ全体のスナップショットへ落とす
        /// (ポーリングで毎フレーム呼ばれても重くならないようにするため)
        /// </summary>
        public static UiNode FindById(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            var idMap = UiViewIdMap.Build();
            GameObject found = null;
            var duplicated = false;
            foreach (var (go, mappedId) in idMap)
            {
                if (mappedId != id) continue;
                if (found != null)
                {
                    duplicated = true;
                    break;
                }
                found = go;
            }
            if (found != null && !duplicated)
            {
                var node = UiTreeBuilder.TryBuildSingle(found, idMap);
                if (node != null && node.Id == id) return node;
            }
            return UiTreeBuilder.Build().FirstOrDefault(n => n.Id == id);
        }

        /// <summary>text は部分一致 (null なら role のみで絞る)。リッチテキストタグを除いた表示文字で比べる</summary>
        public static IReadOnlyList<UiNode> FindAll(UiRole? role = null, string text = null) =>
            UiTreeBuilder.Build()
                .Where(n => (role == null || n.Role == role) &&
                            (text == null || n.PlainText.Contains(text, StringComparison.Ordinal)))
                .ToList();

        /// <summary>ID が prefix で始まるノード (動的生成の一覧 "View/items[" 等の件数を数える用途)</summary>
        public static IReadOnlyList<UiNode> FindByIdPrefix(string prefix) =>
            UiTreeBuilder.Build().Where(n => n.Id.StartsWith(prefix, StringComparison.Ordinal)).ToList();

        // 不可視の同名要素 (閉じた画面のボタン等) を拾って Click が誤失敗しないよう、可視ノードを優先する
        public static UiNode FindByRole(UiRole role, string text = null)
        {
            var matches = FindAll(role, text);
            return matches.FirstOrDefault(n => n.Visible) ?? matches.FirstOrDefault();
        }

        /// <summary>見つからなかった ID に近い既存 ID を、可視ノードを優先して返す (typo や index ずれの診断用)</summary>
        public static IReadOnlyList<string> SuggestIds(string id, int max = 5)
        {
            if (string.IsNullOrEmpty(id)) return Array.Empty<string>();
            return UiTreeBuilder.Build()
                .GroupBy(n => n.Id)
                .Select(g => (Id: g.Key, Visible: g.Any(n => n.Visible), Distance: Distance(id, g.Key)))
                .OrderBy(x => x.Distance)
                .ThenByDescending(x => x.Visible)
                .ThenBy(x => x.Id, StringComparer.Ordinal)
                .Take(max)
                .Select(x => x.Id)
                .ToList();
        }

        /// <summary>FindById が null のときのメッセージ。候補を添えて「どの ID なら在るのか」を失敗ログだけで分かるようにする</summary>
        public static string DescribeNotFound(string id)
        {
            var suggestions = SuggestIds(id);
            return suggestions.Count == 0
                ? $"node '{id}' not found (ツリーが空)"
                : $"node '{id}' not found (候補: {string.Join(", ", suggestions)})";
        }

        // 編集距離。ID は短いので O(n*m) の素朴な DP で足りる
        private static int Distance(string a, string b)
        {
            var previous = new int[b.Length + 1];
            var current = new int[b.Length + 1];
            for (var j = 0; j <= b.Length; j++) previous[j] = j;
            for (var i = 1; i <= a.Length; i++)
            {
                current[0] = i;
                for (var j = 1; j <= b.Length; j++)
                {
                    var cost = a[i - 1] == b[j - 1] ? 0 : 1;
                    current[j] = Math.Min(Math.Min(current[j - 1] + 1, previous[j] + 1), previous[j - 1] + cost);
                }
                (previous, current) = (current, previous);
            }
            return previous[b.Length];
        }
    }
}
