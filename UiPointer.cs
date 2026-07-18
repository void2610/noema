using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Void2610.Noema
{
    /// <summary>
    /// セマンティックノードへの実入力経路での操作
    /// ノード中心のスクリーン座標から EventSystem の Raycast を通すため、
    /// Raycast Target 切れ・他 UI による遮蔽・画面外配置はテスト失敗として検出される
    /// (対象 GameObject へ直接イベントを送る方式では検出できない崩れを拾うのが狙い)
    /// </summary>
    public static class UiPointer
    {
        public readonly struct ClickResult
        {
            public bool Success { get; }
            public string Message { get; }

            public ClickResult(bool success, string message)
            {
                Success = success;
                Message = message;
            }

            public override string ToString() => Success ? $"clicked: {Message}" : $"failed: {Message}";
        }

        public static ClickResult Click(UiNode node)
        {
            if (node == null) return new ClickResult(false, "node not found");
            if (EventSystem.current == null) return new ClickResult(false, "no EventSystem");
            // 無効化ボタンへのクリックは Unity 側で無視されるため、成功待ちポーリングが入力解禁ゲートになるよう明示的に失敗させる (非 Selectable は祖先ハンドラ経路のため対象外)
            if (node.Target is UnityEngine.UI.Selectable selectable && !selectable.IsInteractable()) return new ClickResult(false, $"'{node.Id}' is not interactable");

            var position = node.ScreenBounds.center;
            // Editor 非フォーカス時の Screen.width は Game View と一致しないため、判定基準は所属 Canvas の描画矩形にする
            var canvas = node.Target.GetComponentInParent<Canvas>();
            if (canvas == null) return new ClickResult(false, $"no parent Canvas for '{node.Id}'");
            var pixelRect = canvas.rootCanvas.pixelRect;
            if (!pixelRect.Contains(position)) return new ClickResult(false, $"off screen at {position} (canvas={pixelRect.size})");

            var eventData = new PointerEventData(EventSystem.current)
            {
                position = position,
                button = PointerEventData.InputButton.Left,
            };
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(eventData, hits);
            if (hits.Count == 0) return new ClickResult(false, $"no raycast hit at {position} (Raycast Target 切れの可能性)");

            // 祖先ハンドラを遮蔽扱いしない (ラベル等の子ノードを指しても親の Selectable が受けるのは正当なクリック)
            var topHit = hits[0];
            var handler = ExecuteEvents.GetEventHandler<IPointerClickHandler>(topHit.gameObject);
            if (handler == null) return new ClickResult(false, $"blocked by non-clickable '{HierarchyName(topHit.gameObject)}'");
            var related = handler == node.GameObject
                || handler.transform.IsChildOf(node.GameObject.transform)
                || node.GameObject.transform.IsChildOf(handler.transform);
            if (!related) return new ClickResult(false, $"occluded by '{HierarchyName(handler)}'");

            eventData.pointerPressRaycast = topHit;
            eventData.pointerCurrentRaycast = topHit;
            eventData.pointerPress = ExecuteEvents.ExecuteHierarchy(topHit.gameObject, eventData, ExecuteEvents.pointerDownHandler);
            ExecuteEvents.ExecuteHierarchy(topHit.gameObject, eventData, ExecuteEvents.pointerUpHandler);
            ExecuteEvents.Execute(handler, eventData, ExecuteEvents.pointerClickHandler);
            return new ClickResult(true, node.Id);
        }

        private static string HierarchyName(GameObject go)
        {
            return go.transform.parent != null ? $"{go.transform.parent.name}/{go.name}" : go.name;
        }
    }
}
