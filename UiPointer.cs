using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Void2610.Noema
{
    /// <summary>
    /// セマンティックノードへの実入力経路での操作
    /// ノード中心のスクリーン座標から EventSystem の Raycast を通すため、
    /// Raycast Target 切れ・他 UI による遮蔽・画面外配置はテスト失敗として検出される
    /// (対象 GameObject へ直接イベントを送る方式では検出できない崩れを拾うのが狙い)。
    /// ワールド要素 (<see cref="UiNode.IsWorld"/>) は EventSystem を通らないため <see cref="UiWorldPointer"/> で操作する
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

        // Hover で enter を送った要素。Unhover / 次の Hover で exit を送る
        private static readonly List<GameObject> Hovered = new();

        /// <summary>クリックせずに、いまクリックしたら node に届くかだけを判定する (到達可否の観測用)</summary>
        public static ClickResult Probe(UiNode node)
        {
            if (node != null && node.IsWorld) return UiWorldPointer.Probe(node);
            return Resolve<IPointerClickHandler>(node, requireInteractable: true, out _, out _, out _);
        }

        /// <summary>
        /// node のうち実入力でクリックが届く画面上の点を探す (中心で届かなければ見えている部分)。
        /// 入力デバイスから押す操作 (<see cref="UiWorldPointer.DeviceClickAsync"/>) が押す位置を決めるのに使う
        /// </summary>
        public static bool TryFindReachablePoint(UiNode node, out Vector2 point, out ClickResult failure)
        {
            failure = Resolve<IPointerClickHandler>(node, requireInteractable: true, out var eventData, out _, out _);
            point = failure.Success ? eventData.position : default;
            return failure.Success;
        }

        public static ClickResult Click(UiNode node)
        {
            if (node != null && node.IsWorld) return new ClickResult(false, $"'{node.Id}' はワールド要素のため UiWorldPointer.ClickAsync で操作する");
            var resolved = Resolve<IPointerClickHandler>(node, requireInteractable: true, out var eventData, out var topHit, out var handler);
            if (!resolved.Success) return resolved;

            eventData.pointerPressRaycast = topHit;
            eventData.pointerCurrentRaycast = topHit;
            eventData.pointerPress = ExecuteEvents.ExecuteHierarchy(topHit.gameObject, eventData, ExecuteEvents.pointerDownHandler);
            ExecuteEvents.ExecuteHierarchy(topHit.gameObject, eventData, ExecuteEvents.pointerUpHandler);
            ExecuteEvents.Execute(handler, eventData, ExecuteEvents.pointerClickHandler);
            return new ClickResult(true, node.Id);
        }

        /// <summary>point の最前面でクリックを受ける要素が target 自身またはその祖先 / 子孫なら true</summary>
        internal static bool IsClickHandledBy(Vector2 point, GameObject target)
        {
            if (EventSystem.current == null) return false;
            var eventData = new PointerEventData(EventSystem.current) { position = point };
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(eventData, hits);
            if (hits.Count == 0) return false;
            var handler = ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject);
            return handler != null && IsRelated(handler, target);
        }

        /// <summary>
        /// スクリーン座標 point を実 Raycast で押す。押し先は point の最前面にあるクリック要素 (人がそこを押したときと同じ)。
        /// 文字列内のリンクのように、ノード自身ではなく手前の要素がクリックを受けて位置で判定する UI 向け
        /// </summary>
        internal static ClickResult ClickAtScreenPoint(Vector2 point, string label)
        {
            if (EventSystem.current == null) return new ClickResult(false, "no EventSystem");
            var eventData = new PointerEventData(EventSystem.current)
            {
                position = point,
                pressPosition = point,
                button = PointerEventData.InputButton.Left,
            };
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(eventData, hits);
            if (hits.Count == 0) return new ClickResult(false, $"no raycast hit at {point}");
            var topHit = hits[0];
            var handler = ExecuteEvents.GetEventHandler<IPointerClickHandler>(topHit.gameObject);
            if (handler == null) return new ClickResult(false, $"blocked by non-clickable '{HierarchyName(topHit.gameObject)}'");
            eventData.pointerPressRaycast = topHit;
            eventData.pointerCurrentRaycast = topHit;
            eventData.pointerPress = ExecuteEvents.ExecuteHierarchy(topHit.gameObject, eventData, ExecuteEvents.pointerDownHandler);
            ExecuteEvents.ExecuteHierarchy(topHit.gameObject, eventData, ExecuteEvents.pointerUpHandler);
            ExecuteEvents.Execute(handler, eventData, ExecuteEvents.pointerClickHandler);
            return new ClickResult(true, label);
        }

        /// <summary>
        /// node の中心へポインタを乗せる。EventSystem と同じく、Raycast の最前面から祖先へ向かって enter を送る
        /// (カードのホバー拡大とその親の並べ直しのように、祖先側も enter を受ける実装を再現するため)
        /// </summary>
        public static ClickResult Hover(UiNode node)
        {
            if (node != null && node.IsWorld) return new ClickResult(false, $"'{node.Id}' はワールド要素のため UiWorldPointer.HoverAsync で操作する");
            var resolved = Resolve<IPointerEnterHandler>(node, requireInteractable: false, out var eventData, out var topHit, out _);
            if (!resolved.Success) return resolved;

            var chain = new List<GameObject>();
            for (var t = topHit.gameObject.transform; t != null; t = t.parent) chain.Add(t.gameObject);
            // 前回ホバーしていて今回の祖先鎖に無いものへ exit を送る (EventSystem の HandlePointerExitAndEnter と同じ差分)
            foreach (var previous in Hovered)
            {
                if (previous != null && !chain.Contains(previous)) ExecuteEvents.Execute(previous, eventData, ExecuteEvents.pointerExitHandler);
            }
            eventData.pointerEnter = topHit.gameObject;
            eventData.pointerCurrentRaycast = topHit;
            foreach (var go in chain)
            {
                if (Hovered.Contains(go)) continue;
                eventData.hovered.Add(go);
                ExecuteEvents.Execute(go, eventData, ExecuteEvents.pointerEnterHandler);
            }
            Hovered.Clear();
            Hovered.AddRange(chain);
            return new ClickResult(true, node.Id);
        }

        /// <summary>Hover で乗せたポインタを外し、enter を送った要素へ exit を送る</summary>
        public static ClickResult Unhover()
        {
            if (EventSystem.current == null) return new ClickResult(false, "no EventSystem");
            var eventData = new PointerEventData(EventSystem.current);
            foreach (var go in Hovered)
            {
                if (go != null) ExecuteEvents.Execute(go, eventData, ExecuteEvents.pointerExitHandler);
            }
            var count = Hovered.Count;
            Hovered.Clear();
            return new ClickResult(true, $"unhovered {count}");
        }

        /// <summary>
        /// ゲームパッド / キーボードの決定相当。node を選択状態にしてから submit を送る
        /// (Raycast は通さない。フォーカス移動の経路はナビゲーションの責務でポインタの遮蔽とは無関係)
        /// </summary>
        public static ClickResult Submit(UiNode node)
        {
            if (node == null) return new ClickResult(false, "node not found");
            if (node.IsWorld) return new ClickResult(false, $"'{node.Id}' はワールド要素のため Submit できない");
            if (EventSystem.current == null) return new ClickResult(false, "no EventSystem");
            if (!node.Visible) return new ClickResult(false, $"'{node.Id}' is not visible");
            if (node.Target is UnityEngine.UI.Selectable selectable && !selectable.IsInteractable()) return new ClickResult(false, $"'{node.Id}' is not interactable");
            var handler = ExecuteEvents.GetEventHandler<ISubmitHandler>(node.GameObject);
            if (handler == null) return new ClickResult(false, $"'{node.Id}' は submit を受けない");
            EventSystem.current.SetSelectedGameObject(node.GameObject);
            ExecuteEvents.Execute(handler, new BaseEventData(EventSystem.current), ExecuteEvents.submitHandler);
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
            if (from.IsWorld || to.IsWorld) return new ClickResult(false, "ワールド要素のドラッグには未対応");
            if (EventSystem.current == null) return new ClickResult(false, "no EventSystem");
            if (from.Role == UiRole.Draggable && !from.Interactable) return new ClickResult(false, $"'{from.Id}' is not interactable");

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
            if (!IsRelated(dragHandler, from.GameObject)) return new ClickResult(false, $"occluded by '{HierarchyName(dragHandler)}'");

            eventData.pointerPressRaycast = topHit;
            eventData.pointerCurrentRaycast = topHit;
            eventData.pointerDrag = dragHandler;
            ExecuteEvents.ExecuteHierarchy(topHit.gameObject, eventData, ExecuteEvents.pointerDownHandler);
            ExecuteEvents.Execute(dragHandler, eventData, ExecuteEvents.beginDragHandler);
            // 実 EventSystem と同じく、OnBeginDrag が pointerDrag を外したら掴めなかった (取り出せない要素等) として打ち切る
            if (eventData.pointerDrag == null)
            {
                ExecuteEvents.ExecuteHierarchy(topHit.gameObject, eventData, ExecuteEvents.pointerUpHandler);
                return new ClickResult(false, $"drag refused by '{from.Id}' (OnBeginDrag が pointerDrag を解除)");
            }

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
                // 指定した受け皿以外 (手前の別要素) に落ちたら、その要素へは届けても成功扱いにしない
                if (dropHandler != null)
                {
                    ExecuteEvents.Execute(dropHandler, eventData, ExecuteEvents.dropHandler);
                    dropExecuted = IsRelated(dropHandler, to.GameObject);
                }
            }
            ExecuteEvents.ExecuteHierarchy(topHit.gameObject, eventData, ExecuteEvents.pointerUpHandler);
            ExecuteEvents.Execute(dragHandler, eventData, ExecuteEvents.endDragHandler);

            return dropExecuted
                ? new ClickResult(true, $"{from.Id} -> {to.Id}")
                : new ClickResult(false, $"drop did not reach '{to.Id}' at {endPosition} (IDropHandler を持たないか、手前の要素に遮蔽されている)");
        }

        // 矩形の内側を格子状に試す分割数 (中心で届かないときだけ使う)
        private const int SAMPLE_GRID = 5;

        // node に実 Raycast を撃ち、最前面の T ハンドラが node 自身 (またはその祖先 / 子孫) であることを確かめる。
        // まず中心を試し、届かなければ矩形内の格子点を試す (扇状に重なる手札のように、見えている端だけが押せる要素のため)。
        // どこにも届かなければ中心での失敗理由を返す
        private static ClickResult Resolve<T>(UiNode node, bool requireInteractable, out PointerEventData eventData, out RaycastResult topHit, out GameObject handler)
            where T : IEventSystemHandler
        {
            eventData = null;
            topHit = default;
            handler = null;
            if (node == null) return new ClickResult(false, "node not found");
            if (EventSystem.current == null) return new ClickResult(false, "no EventSystem");
            // 無効化ボタンへのクリックは Unity 側で無視されるため、成功待ちポーリングが入力解禁ゲートになるよう明示的に失敗させる (非 Selectable は祖先ハンドラ経路のため対象外)
            if (requireInteractable && node.Target is UnityEngine.UI.Selectable selectable && !selectable.IsInteractable()) return new ClickResult(false, $"'{node.Id}' is not interactable");
            // カスタムクリック要素も CanvasGroup で入力を止めている間は失敗させ、成功待ちのポーリングを入力解禁ゲートにする
            if (requireInteractable && node.Role == UiRole.Clickable && !node.Interactable) return new ClickResult(false, $"'{node.Id}' is not interactable");

            // Editor 非フォーカス時の Screen.width は Game View と一致しないため、判定基準は所属 Canvas の描画矩形にする
            var canvas = node.Target.GetComponentInParent<Canvas>();
            if (canvas == null) return new ClickResult(false, $"no parent Canvas for '{node.Id}'");
            var pixelRect = canvas.rootCanvas.pixelRect;

            var centerResult = TryPoint<T>(node, node.ScreenBounds.center, pixelRect, out eventData, out topHit, out handler);
            if (centerResult.Success) return centerResult;
            var bounds = node.ScreenBounds;
            for (var y = 0; y < SAMPLE_GRID; y++)
            {
                for (var x = 0; x < SAMPLE_GRID; x++)
                {
                    // 縁ちょうどは隣の要素と境界を共有するため、各セルの中心を使う
                    var point = new Vector2(
                        bounds.xMin + bounds.width * (x + 0.5f) / SAMPLE_GRID,
                        bounds.yMin + bounds.height * (y + 0.5f) / SAMPLE_GRID);
                    if (TryPoint<T>(node, point, pixelRect, out eventData, out topHit, out handler).Success) return new ClickResult(true, node.Id);
                }
            }
            eventData = null;
            topHit = default;
            handler = null;
            return centerResult;
        }

        private static ClickResult TryPoint<T>(UiNode node, Vector2 position, Rect pixelRect, out PointerEventData eventData, out RaycastResult topHit, out GameObject handler)
            where T : IEventSystemHandler
        {
            eventData = null;
            topHit = default;
            handler = null;
            if (!pixelRect.Contains(position)) return new ClickResult(false, $"off screen at {position} (canvas={pixelRect.size})");

            eventData = new PointerEventData(EventSystem.current)
            {
                position = position,
                button = PointerEventData.InputButton.Left,
            };
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(eventData, hits);
            if (hits.Count == 0) return new ClickResult(false, $"no raycast hit at {position} (Raycast Target 切れの可能性)");

            // 祖先ハンドラを遮蔽扱いしない (ラベル等の子ノードを指しても親の Selectable が受けるのは正当なクリック)
            topHit = hits[0];
            handler = ExecuteEvents.GetEventHandler<T>(topHit.gameObject);
            if (handler == null) return new ClickResult(false, $"blocked by non-{(typeof(T) == typeof(IPointerClickHandler) ? "clickable" : "hoverable")} '{HierarchyName(topHit.gameObject)}'");
            if (!IsRelated(handler, node.GameObject)) return new ClickResult(false, $"occluded by '{HierarchyName(handler)}'");
            return new ClickResult(true, node.Id);
        }

        private static bool IsRelated(GameObject handler, GameObject target) =>
            handler == target || handler.transform.IsChildOf(target.transform) || target.transform.IsChildOf(handler.transform);

        private static string HierarchyName(GameObject go)
        {
            return go.transform.parent != null ? $"{go.transform.parent.name}/{go.name}" : go.name;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay() => Hovered.Clear();
    }
}
