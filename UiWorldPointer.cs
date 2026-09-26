using System.Threading;
using UnityEngine;

namespace Void2610.Noema
{
    /// <summary>
    /// ワールド要素を実入力デバイス経由で操作するドライバ。デバイス層 (Input System 等) の実装を差し込む
    /// </summary>
    public interface IUiPointerDriver
    {
        /// <summary>ポインタを screenPosition へ動かし、ゲーム側が位置を読むまで待つ。他のポインタは操作が終わるまで止める</summary>
        Awaitable MoveAsync(Vector2 screenPosition, CancellationToken cancellationToken);

        /// <summary>screenPosition へ動かして左ボタンを押して離し、ゲーム側が押下を読むまで待つ</summary>
        Awaitable ClickAsync(Vector2 screenPosition, CancellationToken cancellationToken);

        /// <summary>ポインタを screenPosition に置いたまま左ボタンを押して離し、ゲーム側が押下を読むまで待つ (MoveAsync の後に使う)</summary>
        Awaitable PressAsync(Vector2 screenPosition, CancellationToken cancellationToken);

        /// <summary>止めていた他のポインタを戻し、操作用のポインタを片付ける</summary>
        void Release();
    }

    /// <summary>
    /// <see cref="IUiNodeProvider"/> が供給したワールド要素への操作。EventSystem ではなく入力デバイスへ入力を流すため、
    /// ゲームが読む入力アクション (クリック・ポインタ位置) の配線まで含めて検証される
    /// </summary>
    public static class UiWorldPointer
    {
        /// <summary>デバイス層の実装。noema の Input System 連携 asmdef が起動時に設定する</summary>
        public static IUiPointerDriver Driver;

        public static UiPointer.ClickResult Probe(UiNode node)
        {
            if (node == null) return new UiPointer.ClickResult(false, "node not found");
            if (!node.IsWorld) return UiPointer.Probe(node);
            if (!node.Visible) return new UiPointer.ClickResult(false, $"'{node.Id}' is not visible");
            if (!node.Interactable) return new UiPointer.ClickResult(false, $"'{node.Id}' is not interactable");
            // 画面外かどうかは描画カメラを知っているプロバイダが判定する (Editor 非フォーカス時の Screen サイズは Game View と一致しない)
            var reason = node.Provider.Probe(node, node.ScreenBounds.center);
            return reason == null ? new UiPointer.ClickResult(true, node.Id) : new UiPointer.ClickResult(false, reason);
        }

        // ポインタを動かしている間に要素が動いたとみなす距離 (ピクセル)
        private const float SETTLED_DISTANCE = 1f;
        // 動く要素を追いかけ直す上限回数
        private const int MAX_FOLLOW_ATTEMPTS = 10;

        public static async Awaitable<UiPointer.ClickResult> ClickAsync(UiNode node, CancellationToken cancellationToken = default)
        {
            if (node != null && !node.IsWorld) return UiPointer.Click(node);
            var probe = Probe(node);
            if (!probe.Success) return probe;
            if (Driver == null) return new UiPointer.ClickResult(false, "IUiPointerDriver が未設定 (Input System 連携が無効)");
            try
            {
                // ポインタを動かす数フレームの間にカメラの追従等で要素が動くと、押した瞬間には別の要素を押してしまう。
                // 動かした後に位置を取り直し、止まっていてそこで届くことを確かめてから押す
                var current = node;
                for (var attempt = 0; attempt < MAX_FOLLOW_ATTEMPTS; attempt++)
                {
                    var point = current.ScreenBounds.center;
                    await Driver.MoveAsync(point, cancellationToken);
                    var refreshed = UiQuery.FindById(current.Id);
                    if (refreshed == null) return new UiPointer.ClickResult(false, $"'{current.Id}' disappeared while moving the pointer");
                    var settled = (refreshed.ScreenBounds.center - point).sqrMagnitude <= SETTLED_DISTANCE * SETTLED_DISTANCE;
                    probe = Probe(refreshed);
                    if (settled && probe.Success)
                    {
                        await Driver.PressAsync(point, cancellationToken);
                        return new UiPointer.ClickResult(true, node.Id);
                    }
                    if (!probe.Success && settled) return probe;
                    current = refreshed;
                }
                return new UiPointer.ClickResult(false, $"'{node.Id}' kept moving on screen ({MAX_FOLLOW_ATTEMPTS} attempts)");
            }
            finally
            {
                Driver.Release();
            }
        }

        /// <summary>
        /// uGUI の要素でも EventSystem へ直接イベントを送らず、見えている点を入力デバイス (仮想マウス) で押す。
        /// ゲームが EventSystem ではなく入力アクション (Click / Submit) を購読している UI (全画面のオーバーレイ等) を操作するため
        /// </summary>
        public static async Awaitable<UiPointer.ClickResult> DeviceClickAsync(UiNode node, CancellationToken cancellationToken = default)
        {
            if (node == null) return new UiPointer.ClickResult(false, "node not found");
            if (node.IsWorld) return await ClickAsync(node, cancellationToken);
            if (!node.Visible) return new UiPointer.ClickResult(false, $"'{node.Id}' is not visible");
            // 押す位置は実 Raycast で届く点にする。クリックハンドラを持たない要素は矩形の中心を押す
            Vector2 point;
            if (node.Role is UiRole.Button or UiRole.Checkbox or UiRole.Clickable)
            {
                if (!UiPointer.TryFindReachablePoint(node, out point, out var failure)) return failure;
            }
            else
            {
                point = node.ScreenBounds.center;
            }
            if (Driver == null) return new UiPointer.ClickResult(false, "IUiPointerDriver が未設定 (Input System 連携が無効)");
            try
            {
                await Driver.ClickAsync(point, cancellationToken);
            }
            finally
            {
                Driver.Release();
            }
            return new UiPointer.ClickResult(true, node.Id);
        }

        /// <summary>ポインタを node に乗せたままにする (ホバー表示の観測用)。外すときは <see cref="Unhover"/></summary>
        public static async Awaitable<UiPointer.ClickResult> HoverAsync(UiNode node, CancellationToken cancellationToken = default)
        {
            if (node != null && !node.IsWorld) return UiPointer.Hover(node);
            if (node == null) return new UiPointer.ClickResult(false, "node not found");
            if (!node.Visible) return new UiPointer.ClickResult(false, $"'{node.Id}' is not visible");
            var reason = node.Provider.Probe(node, node.ScreenBounds.center);
            if (reason != null) return new UiPointer.ClickResult(false, reason);
            if (Driver == null) return new UiPointer.ClickResult(false, "IUiPointerDriver が未設定 (Input System 連携が無効)");
            // クリックと同じく、ポインタを動かしている間に要素が動いたら追いかけ直す
            var current = node;
            for (var attempt = 0; attempt < MAX_FOLLOW_ATTEMPTS; attempt++)
            {
                var point = current.ScreenBounds.center;
                await Driver.MoveAsync(point, cancellationToken);
                var refreshed = UiQuery.FindById(current.Id);
                if (refreshed == null) return new UiPointer.ClickResult(false, $"'{current.Id}' disappeared while moving the pointer");
                if ((refreshed.ScreenBounds.center - point).sqrMagnitude <= SETTLED_DISTANCE * SETTLED_DISTANCE) return new UiPointer.ClickResult(true, node.Id);
                current = refreshed;
            }
            return new UiPointer.ClickResult(false, $"'{node.Id}' kept moving on screen ({MAX_FOLLOW_ATTEMPTS} attempts)");
        }

        /// <summary>uGUI のホバーを外し、ワールド側で止めていた実ポインタも戻す</summary>
        public static UiPointer.ClickResult Unhover()
        {
            Driver?.Release();
            return UiPointer.Unhover();
        }
    }
}
