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

    /// <summary>ゲームパッドのボタン (向きは Xbox 配置の位置で表す)</summary>
    public enum UiPadButton
    {
        South,
        East,
        West,
        North,
        LeftShoulder,
        RightShoulder,
        Start,
        Select,
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
    /// ボタンを実入力デバイス経由で押すドライバ。<see cref="IUiNavigationDriver"/> の既存の実装を壊さないよう別のインターフェイスにしている
    /// </summary>
    public interface IUiPadButtonDriver
    {
        /// <summary>button を押して離し、ゲーム側が押下と離しを読むまで待つ</summary>
        Awaitable PressButtonAsync(UiPadButton button, CancellationToken cancellationToken);
    }

    /// <summary>
    /// ボタンを押したままにするドライバ。押している間の十字キーやボタンの押下は、押したままのボタンと重ねて届く
    /// </summary>
    public interface IUiPadHoldDriver
    {
        /// <summary>button を押したままにし、ゲーム側が押下を読むまで待つ</summary>
        Awaitable HoldButtonAsync(UiPadButton button, CancellationToken cancellationToken);

        /// <summary>押したままの button を離し、ゲーム側が離しを読むまで待つ。押していなければ何もしない</summary>
        Awaitable ReleaseButtonAsync(UiPadButton button, CancellationToken cancellationToken);
    }

    /// <summary>
    /// キーボードのキーを実入力デバイス経由で押すドライバ。キー名の解釈はデバイス層が持つ
    /// </summary>
    public interface IUiKeyDriver
    {
        /// <summary>key がデバイス層で解釈できるキー名か</summary>
        bool IsKnownKey(string key);

        /// <summary>key を押して離し、ゲーム側が押下と離しを読むまで待つ</summary>
        Awaitable PressKeyAsync(string key, CancellationToken cancellationToken);
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

        /// <summary>button ("South" / "East" / "West" / "North" / "LeftShoulder" / "RightShoulder" / "Start" / "Select"、大文字小文字は問わない) を times 回押して離す</summary>
        public static async Awaitable<UiPointer.ClickResult> PressAsync(string button, int times = 1, CancellationToken cancellationToken = default)
        {
            if (!TryParseButton(button, out var parsed)) return new UiPointer.ClickResult(false, $"button '{button}' は {string.Join(" / ", Enum.GetNames(typeof(UiPadButton)))} のいずれか");
            if (times < 1) return new UiPointer.ClickResult(false, $"times は 1 以上 (指定: {times})");
            if (Driver is not IUiPadButtonDriver buttonDriver) return new UiPointer.ClickResult(false, "IUiPadButtonDriver が未設定 (Input System 連携が無効)");
            try
            {
                for (var i = 0; i < times; i++) await buttonDriver.PressButtonAsync(parsed, cancellationToken);
            }
            finally
            {
                Driver.Release();
            }
            return new UiPointer.ClickResult(true, $"{parsed} x{times}");
        }

        /// <summary>button を押したままにする。離すのは <see cref="ReleaseButtonAsync"/></summary>
        public static async Awaitable<UiPointer.ClickResult> HoldButtonAsync(string button, CancellationToken cancellationToken = default)
        {
            if (!TryParseButton(button, out var parsed)) return new UiPointer.ClickResult(false, $"button '{button}' は {string.Join(" / ", Enum.GetNames(typeof(UiPadButton)))} のいずれか");
            if (Driver is not IUiPadHoldDriver holdDriver) return new UiPointer.ClickResult(false, "IUiPadHoldDriver が未設定 (Input System 連携が無効)");
            await holdDriver.HoldButtonAsync(parsed, cancellationToken);
            return new UiPointer.ClickResult(true, parsed.ToString());
        }

        /// <summary>押したままの button を離す</summary>
        public static async Awaitable<UiPointer.ClickResult> ReleaseButtonAsync(string button, CancellationToken cancellationToken = default)
        {
            if (!TryParseButton(button, out var parsed)) return new UiPointer.ClickResult(false, $"button '{button}' は {string.Join(" / ", Enum.GetNames(typeof(UiPadButton)))} のいずれか");
            if (Driver is not IUiPadHoldDriver holdDriver) return new UiPointer.ClickResult(false, "IUiPadHoldDriver が未設定 (Input System 連携が無効)");
            await holdDriver.ReleaseButtonAsync(parsed, cancellationToken);
            return new UiPointer.ClickResult(true, parsed.ToString());
        }

        /// <summary>キーボードの key (Input System のキー名。"Escape" / "Enter" / "Tab" など、大文字小文字は問わない) を times 回押して離す</summary>
        public static async Awaitable<UiPointer.ClickResult> PressKeyAsync(string key, int times = 1, CancellationToken cancellationToken = default)
        {
            if (times < 1) return new UiPointer.ClickResult(false, $"times は 1 以上 (指定: {times})");
            if (Driver is not IUiKeyDriver keyDriver) return new UiPointer.ClickResult(false, "IUiKeyDriver が未設定 (Input System 連携が無効)");
            if (string.IsNullOrWhiteSpace(key) || !keyDriver.IsKnownKey(key.Trim())) return new UiPointer.ClickResult(false, $"key '{key}' はキー名ではない");
            var trimmed = key.Trim();
            try
            {
                for (var i = 0; i < times; i++) await keyDriver.PressKeyAsync(trimmed, cancellationToken);
            }
            finally
            {
                Driver.Release();
            }
            return new UiPointer.ClickResult(true, $"{trimmed} x{times}");
        }

        /// <summary>ボタン名の文字列を解釈する。数値の文字列 ("2" 等) は受け付けない</summary>
        public static bool TryParseButton(string button, out UiPadButton parsed) => TryParseEnum(button, out parsed);

        /// <summary>向きの文字列を解釈する。数値の文字列 ("2" 等) は受け付けない</summary>
        public static bool TryParse(string direction, out UiNavigateDirection parsed) => TryParseEnum(direction, out parsed);

        private static bool TryParseEnum<T>(string text, out T parsed) where T : struct, Enum
        {
            parsed = default;
            if (string.IsNullOrWhiteSpace(text) || char.IsDigit(text.Trim()[0])) return false;
            return Enum.TryParse(text.Trim(), true, out parsed) && Enum.IsDefined(typeof(T), parsed);
        }
    }
}
