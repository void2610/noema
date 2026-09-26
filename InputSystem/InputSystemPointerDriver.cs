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

        private static InputSystemPointerDriver _installed;

        private readonly List<InputDevice> _disabledMice = new();
        private Mouse _mouse;

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
            InputSystem.QueueStateEvent(mouse, new MouseState { position = screenPosition });
            await WaitFramesAsync(cancellationToken);
        }

        public async Awaitable ClickAsync(Vector2 screenPosition, CancellationToken cancellationToken)
        {
            await MoveAsync(screenPosition, cancellationToken);
            InputSystem.QueueStateEvent(_mouse, new MouseState { position = screenPosition }.WithButton(MouseButton.Left));
            await WaitFramesAsync(cancellationToken);
            InputSystem.QueueStateEvent(_mouse, new MouseState { position = screenPosition });
            await WaitFramesAsync(cancellationToken);
        }

        public void Release()
        {
            foreach (var device in _disabledMice)
            {
                if (device.added) InputSystem.EnableDevice(device);
            }
            _disabledMice.Clear();
        }

        private Mouse Acquire()
        {
            if (_mouse == null || !_mouse.added) _mouse = InputSystem.AddDevice<Mouse>(DEVICE_NAME);
            foreach (var device in InputSystem.devices)
            {
                if (device is not Mouse || device == _mouse || !device.enabled) continue;
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
