using System.Threading;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace Void2610.Noema
{
    /// <summary>
    /// 仮想ゲームパッドと仮想キーボードを追加し、十字キー・ボタン・キーの状態イベントを Input System へ流して実入力と同じ経路でゲームへ届けるドライバ。
    /// 実ゲームパッドは止めない (十字キーは離していれば 0 で、押した仮想側の値が勝つため)
    /// </summary>
    public sealed class InputSystemNavigationDriver : IUiNavigationDriver, IUiPadButtonDriver, IUiKeyDriver
    {
        private const string DEVICE_NAME = "NoemaVirtualGamepad";
        private const string KEYBOARD_DEVICE_NAME = "NoemaVirtualKeyboard";
        // 状態イベントは次の Input System 更新で処理され、それを読む Update / アクションのコールバックはさらに後になる
        private const int FRAMES_PER_STEP = 2;

        private static InputSystemNavigationDriver _installed;

        private Gamepad _gamepad;
        private Keyboard _keyboard;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Install()
        {
            // Domain Reload 無効時は前回の Play の購読が残るため外してから付け直す
            if (_installed != null) Application.quitting -= _installed.Dispose;
            _installed = new InputSystemNavigationDriver();
            UiNavigation.Driver = _installed;
            Application.quitting += _installed.Dispose;
        }

        public Awaitable PressAsync(UiNavigateDirection direction, CancellationToken cancellationToken) => PressAsync(ToButton(direction), cancellationToken);

        public Awaitable PressButtonAsync(UiPadButton button, CancellationToken cancellationToken) => PressAsync(ToButton(button), cancellationToken);

        // 数値の文字列 ("27" 等) は Key の値として解釈されてしまうため受け付けない
        public bool IsKnownKey(string key) => !char.IsDigit(key[0]) && System.Enum.TryParse<Key>(key, true, out var parsed) && parsed != Key.None && System.Enum.IsDefined(typeof(Key), parsed);

        public async Awaitable PressKeyAsync(string key, CancellationToken cancellationToken)
        {
            var keyboard = AcquireKeyboard();
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(System.Enum.Parse<Key>(key, true)));
            await WaitFramesAsync(cancellationToken);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            await WaitFramesAsync(cancellationToken);
        }

        private async Awaitable PressAsync(GamepadButton button, CancellationToken cancellationToken)
        {
            var gamepad = Acquire();
            InputSystem.QueueStateEvent(gamepad, new GamepadState().WithButton(button));
            await WaitFramesAsync(cancellationToken);
            // 離しを届けないと、次の押下が同じ向きのときに新しい押下として扱われない
            InputSystem.QueueStateEvent(gamepad, new GamepadState());
            await WaitFramesAsync(cancellationToken);
        }

        public void Release() => GameInputRouting.Release(this);

        private Gamepad Acquire()
        {
            GameInputRouting.Acquire(this);
            if (_gamepad == null || !_gamepad.added) _gamepad = InputSystem.AddDevice<Gamepad>(DEVICE_NAME);
            // Gamepad.current で修飾ボタン (LB など) を見るゲームが、仮想側の状態を読むようにする
            _gamepad.MakeCurrent();
            return _gamepad;
        }

        private Keyboard AcquireKeyboard()
        {
            GameInputRouting.Acquire(this);
            if (_keyboard == null || !_keyboard.added) _keyboard = InputSystem.AddDevice<Keyboard>(KEYBOARD_DEVICE_NAME);
            // Keyboard.current でキーを見るゲームが、仮想側の状態を読むようにする
            _keyboard.MakeCurrent();
            return _keyboard;
        }

        private void Dispose()
        {
            Release();
            if (_gamepad != null && _gamepad.added) InputSystem.RemoveDevice(_gamepad);
            _gamepad = null;
            if (_keyboard != null && _keyboard.added) InputSystem.RemoveDevice(_keyboard);
            _keyboard = null;
        }

        private static GamepadButton ToButton(UiNavigateDirection direction) => direction switch
        {
            UiNavigateDirection.Up => GamepadButton.DpadUp,
            UiNavigateDirection.Down => GamepadButton.DpadDown,
            UiNavigateDirection.Left => GamepadButton.DpadLeft,
            _ => GamepadButton.DpadRight,
        };

        private static GamepadButton ToButton(UiPadButton button) => button switch
        {
            UiPadButton.South => GamepadButton.South,
            UiPadButton.East => GamepadButton.East,
            UiPadButton.West => GamepadButton.West,
            UiPadButton.North => GamepadButton.North,
            UiPadButton.LeftShoulder => GamepadButton.LeftShoulder,
            UiPadButton.RightShoulder => GamepadButton.RightShoulder,
            UiPadButton.Start => GamepadButton.Start,
            _ => GamepadButton.Select,
        };

        private static async Awaitable WaitFramesAsync(CancellationToken cancellationToken)
        {
            for (var i = 0; i < FRAMES_PER_STEP; i++) await Awaitable.NextFrameAsync(cancellationToken);
        }
    }
}
