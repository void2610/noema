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

        /// <summary>
        /// from ノードを掴んで to ノードへ実 Raycast 経路でドラッグ&ドロップする。
        /// beginDrag → drag (中間 3 点) → drop (到達点の Raycast 先) → endDrag を EventSystem と同じ順で発火する。
        /// ドラッグ中に from が blocksRaycasts を切る実装 (掴み表現) を想定し、drop 先は到達点の再 Raycast で解決する
        /// </summary>
        public static ClickResult Drag(UiNode from, UiNode to)
        {
            if (from == null) return new ClickResult(false, "drag source not found");
            if (to == null) return new ClickResult(false, "drop target not found");
            if (EventSystem.current == null) return new ClickResult(false, "no EventSystem");

            var startPosition = from.ScreenBounds.center;
            var endPosition = to.ScreenBounds.center;
            var canvas = from.Target.GetComponentInParent<Canvas>();
            if (canvas == null) return new ClickResult(false, $"no parent Canvas for '{from.Id}'");
            var pixelRect = canvas.rootCanvas.pixelRect;
            if (!pixelRect.Contains(startPosition)) return new ClickResult(false, $"source off screen at {startPosition} (canvas={pixelRect.size})");
            if (!pixelRect.Contains(endPosition)) return new ClickResult(false, $"target off screen at {endPosition} (canvas={pixelRect.size})");

            var eventData = new PointerEventData(EventSystem.current)
            {
                position = startPosition,
                pressPosition = startPosition,
                button = PointerEventData.InputButton.Left,
            };
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(eventData, hits);
            if (hits.Count == 0) return new ClickResult(false, $"no raycast hit at {startPosition} (Raycast Target 切れの可能性)");

            var topHit = hits[0];
            var dragHandler = ExecuteEvents.GetEventHandler<IBeginDragHandler>(topHit.gameObject);
            if (dragHandler == null) return new ClickResult(false, $"no drag handler at '{HierarchyName(topHit.gameObject)}'");
            var related = dragHandler == from.GameObject
                || dragHandler.transform.IsChildOf(from.GameObject.transform)
                || from.GameObject.transform.IsChildOf(dragHandler.transform);
            if (!related) return new ClickResult(false, $"occluded by '{HierarchyName(dragHandler)}'");

            eventData.pointerPressRaycast = topHit;
            eventData.pointerCurrentRaycast = topHit;
            eventData.pointerDrag = dragHandler;
            ExecuteEvents.ExecuteHierarchy(topHit.gameObject, eventData, ExecuteEvents.pointerDownHandler);
            ExecuteEvents.Execute(dragHandler, eventData, ExecuteEvents.beginDragHandler);

            // 中間 3 点 + 終点で drag を発火 (1 点だけだと途中経過依存の実装を検出できない)
            for (var step = 1; step <= 4; step++)
            {
                eventData.position = Vector2.Lerp(startPosition, endPosition, step / 4f);
                ExecuteEvents.Execute(dragHandler, eventData, ExecuteEvents.dragHandler);
            }

            // 到達点で再 Raycast し、実 EventSystem と同じく「ポインタ直下の要素」へ drop を届ける
            hits.Clear();
            EventSystem.current.RaycastAll(eventData, hits);
            var dropExecuted = false;
            if (hits.Count > 0)
            {
                eventData.pointerCurrentRaycast = hits[0];
                var dropHandler = ExecuteEvents.GetEventHandler<IDropHandler>(hits[0].gameObject);
                if (dropHandler != null)
                {
                    ExecuteEvents.Execute(dropHandler, eventData, ExecuteEvents.dropHandler);
                    dropExecuted = true;
                }
            }
            ExecuteEvents.ExecuteHierarchy(topHit.gameObject, eventData, ExecuteEvents.pointerUpHandler);
            ExecuteEvents.Execute(dragHandler, eventData, ExecuteEvents.endDragHandler);

            return dropExecuted
                ? new ClickResult(true, $"{from.Id} -> {to.Id}")
                : new ClickResult(false, $"no drop handler under {endPosition} ('{to.Id}' が IDropHandler を持たないか遮蔽されている)");
        }

        private static string HierarchyName(GameObject go)
        {
            return go.transform.parent != null ? $"{go.transform.parent.name}/{go.name}" : go.name;
        }
    }
}
