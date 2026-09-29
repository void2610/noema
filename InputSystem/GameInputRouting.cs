using System.Collections.Generic;
using UnityEngine.InputSystem;

namespace Void2610.Noema
{
    /// <summary>
    /// 仮想デバイスの入力をゲームへ届けるため、操作中だけ Input System の設定を変える。
    /// 仮想マウスと仮想ゲームパッドが同時に使っても、最後の利用者が離したときに元の設定へ戻す
    /// </summary>
    internal static class GameInputRouting
    {
        private static readonly HashSet<object> Owners = new();
        private static InputSettings.BackgroundBehavior _savedBackgroundBehavior;
#if UNITY_EDITOR
        private static InputSettings.EditorInputBehaviorInPlayMode _savedEditorInputBehavior;
#endif

        /// <summary>owner の操作が続く間、入力をゲームへ回す。同じ owner で何度呼んでもよい</summary>
        public static void Acquire(object owner)
        {
            if (!Owners.Add(owner) || Owners.Count > 1) return;
#if UNITY_EDITOR
            // 既定の入力は Game View にフォーカスがあるときだけ届くため、batchmode (CI) や非フォーカスの Editor でも届くよう全入力を回す
            _savedEditorInputBehavior = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
#endif
            // アプリがフォーカスを持たないとき (batchmode 含む) にデバイスを止めて入力を捨てる既定の振る舞いを、操作中だけ無効にする
            _savedBackgroundBehavior = InputSystem.settings.backgroundBehavior;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        }

        /// <summary>owner の操作を終える。誰も使わなくなったら設定を戻す</summary>
        public static void Release(object owner)
        {
            if (!Owners.Remove(owner) || Owners.Count > 0) return;
#if UNITY_EDITOR
            InputSystem.settings.editorInputBehaviorInPlayMode = _savedEditorInputBehavior;
#endif
            InputSystem.settings.backgroundBehavior = _savedBackgroundBehavior;
        }
    }
}
