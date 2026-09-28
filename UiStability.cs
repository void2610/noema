using System.Threading;
using UnityEngine;

namespace Void2610.Noema
{
    /// <summary>
    /// 演出の静止待ち。ノードの画面上の矩形と実効 alpha が連続フレームで変わらなくなったかを確かめる。
    /// 利用側が入場演出・スライド・フェードの完了フラグを View に足さずに待てるようにする
    /// </summary>
    public static class UiStability
    {
        // 1 フレーム間の差をこれ以下なら静止とみなす (ピクセル / alpha)
        private const float POSITION_EPSILON = 0.5f;
        private const float ALPHA_EPSILON = 0.005f;

        /// <summary>id のノードが frames フレーム連続で動かなければ true。見つからない / 非表示 / 途中で動けば false</summary>
        public static async Awaitable<bool> IsStableAsync(string id, int frames, CancellationToken cancellationToken = default)
        {
            var node = UiQuery.FindById(id);
            if (node == null || !node.Visible) return false;
            var bounds = node.ScreenBounds;
            var alpha = UiInspect.EffectiveAlpha(node.GameObject);
            for (var i = 0; i < frames; i++)
            {
                await Awaitable.NextFrameAsync(cancellationToken);
                var current = UiQuery.FindById(id);
                if (current == null || !current.Visible) return false;
                var currentAlpha = UiInspect.EffectiveAlpha(current.GameObject);
                if (!Approximately(bounds, current.ScreenBounds) || Mathf.Abs(alpha - currentAlpha) > ALPHA_EPSILON) return false;
                bounds = current.ScreenBounds;
                alpha = currentAlpha;
            }
            return true;
        }

        private static bool Approximately(Rect a, Rect b) =>
            Vector2.Distance(a.min, b.min) <= POSITION_EPSILON && Vector2.Distance(a.max, b.max) <= POSITION_EPSILON;
    }
}
