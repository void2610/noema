using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UI;

namespace Void2610.Noema
{
    /// <summary>
    /// ノードの見た目 (画像・色・実効 alpha) と、コレクションの要素数を読む観測 API。
    /// 利用側が「テストで読みたいだけ」の getter を View に足さずに済むようにする
    /// </summary>
    public static class UiInspect
    {
        /// <summary>ノードの Image / SpriteRenderer のスプライト名。画像を持たなければ空文字</summary>
        public static string SpriteName(UiNode node)
        {
            if (node == null) return "";
            if (node.GameObject.TryGetComponent<Image>(out var image)) return image.sprite != null ? image.sprite.name : "";
            if (node.GameObject.TryGetComponent<SpriteRenderer>(out var sr)) return sr.sprite != null ? sr.sprite.name : "";
            return "";
        }

        /// <summary>ノードの Graphic / SpriteRenderer の色を "RRGGBBAA" で返す。色を持たなければ空文字</summary>
        public static string ColorHex(UiNode node)
        {
            if (node == null) return "";
            if (node.GameObject.TryGetComponent<Graphic>(out var graphic)) return ColorUtility.ToHtmlStringRGBA(graphic.color);
            if (node.GameObject.TryGetComponent<SpriteRenderer>(out var sr)) return ColorUtility.ToHtmlStringRGBA(sr.color);
            return "";
        }

        /// <summary>祖先 CanvasGroup の alpha を掛け合わせた、画面に出る不透明度 (Graphic の色 alpha も含む)</summary>
        public static float EffectiveAlpha(GameObject go)
        {
            var alpha = go.TryGetComponent<Graphic>(out var graphic) ? graphic.color.a : 1f;
            foreach (var group in go.GetComponentsInParent<CanvasGroup>())
            {
                alpha *= group.alpha;
                if (group.ignoreParentGroups) break;
            }
            return alpha;
        }

        /// <summary>
        /// コレクションフィールド "View/field" の直下の要素 ("View/field[key]") のうち表示中の数。
        /// 要素 View の子 ("View/field[0]/label") は数えない
        /// </summary>
        public static int CountItems(string collectionId) => CountItems(collectionId, UiTreeBuilder.Build());

        public static int CountItems(string collectionId, IEnumerable<UiNode> nodes)
        {
            var pattern = new Regex("^" + Regex.Escape(collectionId) + @"\[[^\[\]/]*\]$");
            var count = 0;
            foreach (var node in nodes)
            {
                if (node.Visible && pattern.IsMatch(node.Id)) count++;
            }
            return count;
        }
    }
}
