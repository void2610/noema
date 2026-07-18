using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Void2610.Noema
{
    /// <summary>
    /// クリック以外のセマンティックノード操作 (値設定 / 選択 / スクロール)
    /// 各コンポーネントの公開 API 経由で変更するため onValueChanged 等のイベントは実操作と同様に発火する
    /// (ポインタドラッグの物理的な再現はしない。遮蔽検証が必要な操作は UiPointer.Click 側が担う)
    /// </summary>
    public static class UiActions
    {
        public readonly struct Result
        {
            public bool Success { get; }
            public string Message { get; }

            public Result(bool success, string message)
            {
                Success = success;
                Message = message;
            }

            public override string ToString() => Success ? $"ok: {Message}" : $"failed: {Message}";
        }

        public static Result SetSliderValue(UiNode node, float value)
        {
            if (!TryGetInteractable<Slider>(node, UiRole.Slider, out var slider, out var failure)) return failure;
            slider.value = value;
            return new Result(true, $"{node.Id} = {slider.value.ToString(CultureInfo.InvariantCulture)}");
        }

        public static Result SetText(UiNode node, string text)
        {
            if (node == null) return new Result(false, "node not found");
            if (node.Role != UiRole.Textbox) return new Result(false, $"'{node.Id}' は textbox ではない ({node.Role})");
            if (!node.Interactable) return new Result(false, $"'{node.Id}' is not interactable");
            switch (node.Target)
            {
                case TMP_InputField tmp:
                    tmp.text = text;
                    return new Result(true, $"{node.Id} = \"{tmp.text}\"");
                case InputField legacy:
                    legacy.text = text;
                    return new Result(true, $"{node.Id} = \"{legacy.text}\"");
                default:
                    return new Result(false, $"'{node.Id}' の Target が入力欄ではない");
            }
        }

        /// <summary>目標状態と異なる場合のみ実 Raycast クリックでトグルする (遮蔽検証込み)</summary>
        public static Result SetChecked(UiNode node, bool isOn)
        {
            if (!TryGetInteractable<Toggle>(node, UiRole.Checkbox, out var toggle, out var failure)) return failure;
            if (toggle.isOn == isOn) return new Result(true, $"{node.Id} は既に {isOn}");
            var click = UiPointer.Click(node);
            if (!click.Success) return new Result(false, click.Message);
            return toggle.isOn == isOn
                ? new Result(true, $"{node.Id} = {toggle.isOn}")
                : new Result(false, $"クリック後も {node.Id} = {toggle.isOn} (期待 {isOn})");
        }

        public static Result SelectOption(UiNode node, int index)
        {
            if (!TryGetInteractable<TMP_Dropdown>(node, UiRole.Combobox, out var dropdown, out var failure)) return failure;
            if (index < 0 || index >= dropdown.options.Count) return new Result(false, $"index {index} が範囲外 (options={dropdown.options.Count})");
            dropdown.value = index;
            dropdown.RefreshShownValue();
            return new Result(true, $"{node.Id} = [{index}] {dropdown.options[index].text}");
        }

        /// <summary>正規化位置 (0-1、y は 1 が上端) へスクロールする</summary>
        public static Result Scroll(UiNode node, float normalizedX, float normalizedY)
        {
            if (!TryGetInteractable<ScrollRect>(node, UiRole.ScrollArea, out var scroll, out var failure)) return failure;
            scroll.normalizedPosition = new Vector2(Mathf.Clamp01(normalizedX), Mathf.Clamp01(normalizedY));
            // Vector2.ToString はカルチャ/バージョンで表記が揺れるため、シナリオの文字列比較用に明示フォーマットする
            var pos = scroll.normalizedPosition;
            return new Result(true, string.Format(CultureInfo.InvariantCulture, "{0} = ({1:0.00}, {2:0.00})", node.Id, pos.x, pos.y));
        }

        private static bool TryGetInteractable<T>(UiNode node, UiRole expectedRole, out T component, out Result failure) where T : Component
        {
            component = null;
            if (node == null)
            {
                failure = new Result(false, "node not found");
                return false;
            }
            if (node.Role != expectedRole)
            {
                failure = new Result(false, $"'{node.Id}' は {expectedRole} ではない ({node.Role})");
                return false;
            }
            if (!node.Interactable)
            {
                failure = new Result(false, $"'{node.Id}' is not interactable");
                return false;
            }
            component = node.Target as T;
            if (component == null)
            {
                failure = new Result(false, $"'{node.Id}' の Target が {typeof(T).Name} ではない");
                return false;
            }
            failure = default;
            return true;
        }
    }
}
