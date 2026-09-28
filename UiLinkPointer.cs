using System.Threading;
using TMPro;
using UnityEngine;

namespace Void2610.Noema
{
    /// <summary>
    /// TMP テキスト内の &lt;link&gt; を実入力で押す。本文中のキーワードのように、文字列の一部だけがクリック対象の UI 向け。
    /// 押す前に、その座標で TMP のリンク判定 (TMP_TextUtilities.FindIntersectingLink) が当該リンクを返すことを確かめる
    /// </summary>
    public static class UiLinkPointer
    {
        public static async Awaitable<UiPointer.ClickResult> ClickLinkAsync(UiNode node, string linkId, CancellationToken cancellationToken = default)
        {
            if (node == null) return new UiPointer.ClickResult(false, "node not found");
            if (!node.Visible) return new UiPointer.ClickResult(false, $"'{node.Id}' is not visible");
            // 行自体がクリック要素なら、フェード中など入力を止めている間は押さない (背後の要素を誤って押さないため)
            if (node.Role == UiRole.Clickable && !node.Interactable) return new UiPointer.ClickResult(false, $"'{node.Id}' is not interactable");
            var text = node.GameObject.GetComponentInChildren<TMP_Text>();
            if (text == null) return new UiPointer.ClickResult(false, $"'{node.Id}' に TMP テキストが無い");
            if (!TryGetLinkPoint(text, linkId, out var point, out var camera, out var failure)) return new UiPointer.ClickResult(false, failure);

            var label = $"{node.Id}#{linkId}";
            // テキスト自身 (の親子) がクリックを受けるなら EventSystem で正確に押す。手前の別要素が受ける場合 (全画面の送りボタンが
            // デバイスのポインタ位置でリンクを判定する等) だけ、その要素が読む位置を揃えるため仮想マウスで押す
            if (UiWorldPointer.Driver == null || UiPointer.IsClickHandledBy(point, node.GameObject)) return UiPointer.ClickAtScreenPoint(point, label);
            try
            {
                await UiWorldPointer.Driver.ClickAsync(point, cancellationToken);
            }
            finally
            {
                UiWorldPointer.Driver.Release();
            }
            return new UiPointer.ClickResult(true, label);
        }

        /// <summary>
        /// ID が prefix で始まる表示中ノードのうち、linkId のリンクを含む最初のテキストのリンクを押す。
        /// バックログのように行が実行時に並び、何行目にあるかをテスト側で決められない一覧向け
        /// </summary>
        public static async Awaitable<UiPointer.ClickResult> ClickLinkWithinAsync(string prefix, string linkId, CancellationToken cancellationToken = default)
        {
            var target = FindLinkNode(prefix, linkId, clickableOnly: true) ?? FindLinkNode(prefix, linkId, clickableOnly: false);
            if (target == null) return new UiPointer.ClickResult(false, $"no visible node under '{prefix}' contains link '{linkId}'");
            var result = await ClickLinkAsync(target, linkId, cancellationToken);
            // 押した行の ID は実行時に決まるため、呼び出し側が比較できるよう成功時は prefix で表す
            return result.Success ? new UiPointer.ClickResult(true, $"{prefix}#{linkId}") : result;
        }

        // 行 (クリック要素) を優先して探す。行の子のテキストノードを先に拾うと、行の入力停止 (フェード中等) の判定が効かないため
        private static UiNode FindLinkNode(string prefix, string linkId, bool clickableOnly)
        {
            foreach (var node in UiQuery.FindByIdPrefix(prefix))
            {
                if (!node.Visible || (clickableOnly && node.Role != UiRole.Clickable)) continue;
                var text = node.GameObject.GetComponentInChildren<TMP_Text>();
                if (text != null && ContainsLink(text, linkId)) return node;
            }
            return null;
        }

        private static bool ContainsLink(TMP_Text text, string linkId)
        {
            text.ForceMeshUpdate();
            var info = text.textInfo;
            for (var i = 0; i < info.linkCount; i++)
            {
                if (info.linkInfo[i].GetLinkID() == linkId) return true;
            }
            return false;
        }

        // リンクの先頭の可視文字の中心を、リンク判定が当たることを確かめたうえでスクリーン座標として返す
        private static bool TryGetLinkPoint(TMP_Text text, string linkId, out Vector2 point, out Camera camera, out string failure)
        {
            point = default;
            camera = null;
            failure = null;
            text.ForceMeshUpdate();
            var info = text.textInfo;
            var linkIndex = -1;
            for (var i = 0; i < info.linkCount; i++)
            {
                if (info.linkInfo[i].GetLinkID() != linkId) continue;
                linkIndex = i;
                break;
            }
            if (linkIndex < 0)
            {
                failure = $"link '{linkId}' not found in '{text.name}'";
                return false;
            }

            var link = info.linkInfo[linkIndex];
            var charIndex = -1;
            for (var i = link.linkTextfirstCharacterIndex; i < link.linkTextfirstCharacterIndex + link.linkTextLength; i++)
            {
                if (i >= info.characterCount || !info.characterInfo[i].isVisible) continue;
                charIndex = i;
                break;
            }
            if (charIndex < 0)
            {
                failure = $"link '{linkId}' has no visible character";
                return false;
            }

            var ch = info.characterInfo[charIndex];
            var localCenter = (ch.bottomLeft + ch.topRight) * 0.5f;
            var canvas = text.canvas != null ? text.canvas.rootCanvas : null;
            camera = canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera != null ? canvas.worldCamera : Camera.main;
            point = RectTransformUtility.WorldToScreenPoint(camera, text.transform.TransformPoint(localCenter));
            if (TMP_TextUtilities.FindIntersectingLink(text, point, camera) != linkIndex)
            {
                failure = $"link '{linkId}' is not reachable at {point}";
                return false;
            }
            return true;
        }
    }
}
