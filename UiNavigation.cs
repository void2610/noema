using System;
using System.Threading;
using UnityEngine;

namespace Void2610.Noema
{
    /// <summary>十字キーの向き</summary>
    public enum UiNavigateDirection
    {
        Up,
        Down,
        Left,
        Right,
    }

    /// <summary>
    /// 方向入力を実入力デバイス経由で流すドライバ。デバイス層 (Input System 等) の実装を差し込む
    /// </summary>
    public interface IUiNavigationDriver
    {
        /// <summary>direction の十字キーを押して離し、ゲーム側が押下と離しを読むまで待つ。押している時間はリピートが始まるより十分短い</summary>
        Awaitable PressAsync(UiNavigateDirection direction, CancellationToken cancellationToken);

        /// <summary>操作のために変えた入力の設定を戻す</summary>
        void Release();
    }

    /// <summary>
    /// ゲームパッドの十字キーを押す。EventSystem へ Move イベントを直接送らず入力デバイスへ流すため、
    /// ゲーム側のナビゲーション (InputSystemUIInputModule の move や、それを止めて自前で解決するライブラリ) の配線まで含めて検証される。
    /// 選択がどこへ動いたかは <see cref="UiTreeBuilder"/> の Focused (ブリッジの Ui/Focused) で観測する
    /// </summary>
    public static class UiNavigation
    {
        /// <summary>デバイス層の実装。noema の Input System 連携 asmdef が起動時に設定する</summary>
        public static IUiNavigationDriver Driver;

        /// <summary>direction ("Up" / "Down" / "Left" / "Right"、大文字小文字は問わない) へ times 回押して離す</summary>
        public static async Awaitable<UiPointer.ClickResult> NavigateAsync(string direction, int times = 1, CancellationToken cancellationToken = default)
        {
            if (!TryParse(direction, out var parsed)) return new UiPointer.ClickResult(false, $"direction '{direction}' は Up / Down / Left / Right のいずれか");
            if (times < 1) return new UiPointer.ClickResult(false, $"times は 1 以上 (指定: {times})");
            if (Driver == null) return new UiPointer.ClickResult(false, "IUiNavigationDriver が未設定 (Input System 連携が無効)");
            try
            {
                for (var i = 0; i < times; i++) await Driver.PressAsync(parsed, cancellationToken);
            }
            finally
            {
                Driver.Release();
            }
            return new UiPointer.ClickResult(true, $"{parsed} x{times}");
        }

        /// <summary>向きの文字列を解釈する。数値の文字列 ("2" 等) は受け付けない</summary>
        public static bool TryParse(string direction, out UiNavigateDirection parsed)
        {
            parsed = default;
            if (string.IsNullOrWhiteSpace(direction) || char.IsDigit(direction.Trim()[0])) return false;
            return Enum.TryParse(direction.Trim(), true, out parsed) && Enum.IsDefined(typeof(UiNavigateDirection), parsed);
        }
    }
}
