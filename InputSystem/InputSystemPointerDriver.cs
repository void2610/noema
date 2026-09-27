using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace Void2610.Noema
{
    /// <summary>
    /// 仮想マウスを追加し、状態イベントを Input System へ流して実入力と同じ経路でゲームへ届けるドライバ。
    /// 操作中は実マウスを無効化する (位置が Value 型アクションの場合、複数デバイスでは値の大きい側が勝ち、仮想側の位置が無視されうるため)
    /// </summary>
    public sealed class InputSystemPointerDriver : IUiPointerDriver
    {
        private const string DEVICE_NAME = "NoemaVirtualMouse";
        // 状態イベントは次の Input System 更新で処理され、それを読む Update / アクションのコールバックはさらに後になる
        private const int FRAMES_PER_STEP = 2;
        private const float SAME_POSITION_EPSILON = 0.5f;

        private static InputSystemPointerDriver _installed;

        private readonly List<InputDevice> _disabledMice = new();
        private Mouse _mouse;
        private InputSettings.BackgroundBehavior? _savedBackgroundBehavior;
#if UNITY_EDITOR
        private InputSettings.EditorInputBehaviorInPlayMode? _savedEditorInputBehavior;
#endif

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Install()
        {
            // Domain Reload 無効時は前回の Play の購読が残るため外してから付け直す
            if (_installed != null) Application.quitting -= _installed.Dispose;
            _installed = new InputSystemPointerDriver();
            UiWorldPointer.Driver = _installed;
            // Play を抜けたら実マウスを戻し、仮想マウスを消す (Editor で人の操作を奪ったままにしない)
            Application.quitting += _installed.Dispose;
        }

        public async Awaitable MoveAsync(Vector2 screenPosition, CancellationToken cancellationToken)
        {
            var mouse = Acquire();
            // 位置の値が変わらないとアクションの performed が出ず、ゲームは直前に実マウスが残した位置を持ち続けるため、同じ位置へは一度ずらしてから戻す
            if (Vector2.Distance(mouse.position.ReadValue(), screenPosition) < SAME_POSITION_EPSILON)
            {
                InputSystem.QueueStateEvent(mouse, new MouseState { position = screenPosition + Vector2.right });
                await Awaitable.NextFrameAsync(cancellationToken);
            }
            InputSystem.QueueStateEvent(mouse, new MouseState { position = screenPosition });
            await WaitFramesAsync(cancellationToken);
        }

        public async Awaitable ClickAsync(Vector2 screenPosition, CancellationToken cancellationToken)
        {
            await MoveAsync(screenPosition, cancellationToken);
            await PressAsync(screenPosition, cancellationToken);
        }

        public async Awaitable PressAsync(Vector2 screenPosition, CancellationToken cancellationToken)
        {
            Acquire();
            InputSystem.QueueStateEvent(_mouse, new MouseState { position = screenPosition }.WithButton(MouseButton.Left));
            await WaitFramesAsync(cancellationToken);
            InputSystem.QueueStateEvent(_mouse, new MouseState { position = screenPosition });
            await WaitFramesAsync(cancellationToken);
        }

        public void Release()
        {
#if UNITY_EDITOR
            if (_savedEditorInputBehavior.HasValue) InputSystem.settings.editorInputBehaviorInPlayMode = _savedEditorInputBehavior.Value;
            _savedEditorInputBehavior = null;
#endif
            if (_savedBackgroundBehavior.HasValue) InputSystem.settings.backgroundBehavior = _savedBackgroundBehavior.Value;
            _savedBackgroundBehavior = null;
            foreach (var device in _disabledMice)
            {
                if (device.added) InputSystem.EnableDevice(device);
            }
            _disabledMice.Clear();
        }

        private Mouse Acquire()
        {
#if UNITY_EDITOR
            // 既定ではポインタ入力は Game View にフォーカスがあるときだけゲームへ届く。
            // Game View の無い batchmode (CI) やフォーカスの外れた Editor でも届くよう、操作中だけ全入力をゲームへ回す
            if (!_savedEditorInputBehavior.HasValue)
            {
                _savedEditorInputBehavior = InputSystem.settings.editorInputBehaviorInPlayMode;
                InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            }
#endif
            // アプリがフォーカスを持たないとき (batchmode 含む) にデバイスを止めて入力を捨てる既定の振る舞いを、操作中だけ無効にする
            if (!_savedBackgroundBehavior.HasValue)
            {
                _savedBackgroundBehavior = InputSystem.settings.backgroundBehavior;
                InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            }
            if (_mouse == null || !_mouse.added) _mouse = InputSystem.AddDevice<Mouse>(DEVICE_NAME);
            foreach (var device in InputSystem.devices)
            {
                if (device is not Mouse || device == _mouse || !device.enabled) continue;
                // 無効化だけでは Value 型アクションに実マウスの位置の大きさが残り、仮想側より大きいと仮想側が無視されるため、位置ごと 0 に戻してから止める
                InputSystem.ResetDevice(device, alsoResetDontResetControls: true);
                InputSystem.DisableDevice(device);
                _disabledMice.Add(device);
            }
            _mouse.MakeCurrent();
            return _mouse;
        }

        private void Dispose()
        {
            Release();
            if (_mouse != null && _mouse.added) InputSystem.RemoveDevice(_mouse);
            _mouse = null;
        }

        private static async Awaitable WaitFramesAsync(CancellationToken cancellationToken)
        {
            for (var i = 0; i < FRAMES_PER_STEP; i++) await Awaitable.NextFrameAsync(cancellationToken);
        }
    }
}
