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

        public static async Awaitable<UiPointer.ClickResult> ClickAsync(UiNode node, CancellationToken cancellationToken = default)
        {
            if (node != null && !node.IsWorld) return UiPointer.Click(node);
            var probe = Probe(node);
            if (!probe.Success) return probe;
            if (Driver == null) return new UiPointer.ClickResult(false, "IUiPointerDriver が未設定 (Input System 連携が無効)");
            try
            {
                await Driver.ClickAsync(node.ScreenBounds.center, cancellationToken);
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
            await Driver.MoveAsync(node.ScreenBounds.center, cancellationToken);
            return new UiPointer.ClickResult(true, node.Id);
        }

        /// <summary>uGUI のホバーを外し、ワールド側で止めていた実ポインタも戻す</summary>
        public static UiPointer.ClickResult Unhover()
        {
            Driver?.Release();
            return UiPointer.Unhover();
        }
    }
}
