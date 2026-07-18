using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Void2610.Noema
{
    /// <summary>
    /// ロード済みシーンの uGUI 階層を走査してセマンティックツリー (UiNode のフラットリスト) を構築する
    /// 常駐せず呼び出し時点のスナップショットを返すオンデマンド方式
    /// </summary>
    public static class UiTreeBuilder
    {
        // CanvasGroup.alpha がこれ以下なら不可視とみなす
        private const float VISIBLE_ALPHA_THRESHOLD = 0.01f;

        public static IReadOnlyList<UiNode> Build()
        {
            var nodes = new List<UiNode>();
            var idMap = UiViewIdMap.Build();
            foreach (var canvas in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            {
                // 入れ子 Canvas は root 側の走査で拾われるため root のみ起点にする
                if (!canvas.isActiveAndEnabled || canvas != canvas.rootCanvas) continue;
                Walk(canvas.transform, canvas, idMap, nodes);
            }
            return nodes;
        }

        private static void Walk(Transform transform, Canvas rootCanvas, IReadOnlyDictionary<GameObject, string> idMap, List<UiNode> nodes)
        {
            // disabled な入れ子 Canvas の配下は描画されないため Visible=True で報告しない
            if (transform.TryGetComponent<Canvas>(out var nested) && nested != rootCanvas && !nested.enabled) return;
            var node = TryCreateNode(transform.gameObject, rootCanvas, idMap);
            if (node != null) nodes.Add(node);
            for (var i = 0; i < transform.childCount; i++) Walk(transform.GetChild(i), rootCanvas, idMap, nodes);
        }

        // 対象 GameObject の role を判定してノード化する。UI 要素でなければ null
        private static UiNode TryCreateNode(GameObject go, Canvas rootCanvas, IReadOnlyDictionary<GameObject, string> idMap)
        {
            if (go.TryGetComponent<Button>(out var button)) return Create(UiRole.Button, go, rootCanvas, idMap, LabelOf(go), button, button.IsInteractable());
            if (go.TryGetComponent<Toggle>(out var toggle)) return Create(UiRole.Checkbox, go, rootCanvas, idMap, LabelOf(go), toggle, toggle.IsInteractable());
            if (go.TryGetComponent<TMP_InputField>(out var tmpInput)) return Create(UiRole.Textbox, go, rootCanvas, idMap, tmpInput.text, tmpInput, tmpInput.IsInteractable());
            if (go.TryGetComponent<Slider>(out var slider)) return Create(UiRole.Slider, go, rootCanvas, idMap, slider.value.ToString(System.Globalization.CultureInfo.InvariantCulture), slider, slider.IsInteractable());
            if (go.TryGetComponent<TMP_Dropdown>(out var dropdown)) return Create(UiRole.Combobox, go, rootCanvas, idMap, dropdown.captionText != null ? dropdown.captionText.text : "", dropdown, dropdown.IsInteractable());
            if (go.TryGetComponent<ScrollRect>(out var scroll)) return Create(UiRole.ScrollArea, go, rootCanvas, idMap, "", scroll, scroll.enabled);
            // 標準 Selectable でないカスタムクリック要素 (カード等)。プロジェクトの実装のみ対象にしてライブラリ内部を拾わない
            if (TryGetProjectClickHandler(go, out var clickable)) return Create(UiRole.Clickable, go, rootCanvas, idMap, LabelOf(go), clickable, clickable is Behaviour { isActiveAndEnabled: true });
            // カスタムドラッグ要素 (D&D チップ等)。クリックハンドラを持つ要素は Clickable 優先で上に拾われる
            if (TryGetProjectHandler<UnityEngine.EventSystems.IBeginDragHandler>(go, out var draggable)) return Create(UiRole.Draggable, go, rootCanvas, idMap, LabelOf(go), draggable, draggable is Behaviour { isActiveAndEnabled: true });
            // ドロップ受け要素 (D&D の受け皿)。UiPointer.Drag の to 引数として引けるようノード化する
            if (TryGetProjectHandler<UnityEngine.EventSystems.IDropHandler>(go, out var dropTarget)) return Create(UiRole.DropTarget, go, rootCanvas, idMap, LabelOf(go), dropTarget, dropTarget is Behaviour { isActiveAndEnabled: true });
            // 対話要素の内側のラベルはノード化しない (ボタンの Text 側に集約される)
            if (go.TryGetComponent<TMP_Text>(out var text) && go.GetComponentInParent<Selectable>() == null) return Create(UiRole.Text, go, rootCanvas, idMap, text.text, text, false);
            return null;
        }

        private static bool TryGetProjectClickHandler(GameObject go, out Component clickable) =>
            TryGetProjectHandler<UnityEngine.EventSystems.IPointerClickHandler>(go, out clickable);

        private static bool TryGetProjectHandler<T>(GameObject go, out Component found) where T : class
        {
            found = null;
            foreach (var handler in go.GetComponents<T>())
            {
                if (handler is not Component component) continue;
                if (!UiViewIdMap.IsProjectAssembly(component.GetType())) continue;
                found = component;
                return true;
            }
            return false;
        }

        private static UiNode Create(UiRole role, GameObject go, Canvas rootCanvas, IReadOnlyDictionary<GameObject, string> idMap, string text, Component target, bool interactable)
        {
            return new(role, ResolveId(go, idMap), text, IsVisible(go), interactable, ScreenBoundsOf(go, rootCanvas), target);
        }

        // 子孫の TMP_Text からラベルを拾う (Button/Toggle 用)
        private static string LabelOf(GameObject go)
        {
            var label = go.GetComponentInChildren<TMP_Text>(true);
            return label != null ? label.text : "";
        }

        // View フィールド由来 ID を最優先し、View に握られていない要素のみ階層パスへフォールバックする
        private static string ResolveId(GameObject go, IReadOnlyDictionary<GameObject, string> idMap)
        {
            return idMap.TryGetValue(go, out var id) ? id : HierarchyPathOf(go.transform);
        }

        private static string HierarchyPathOf(Transform transform)
        {
            var sb = new StringBuilder(transform.name);
            for (var t = transform.parent; t != null; t = t.parent) sb.Insert(0, '/').Insert(0, t.name);
            return sb.ToString();
        }

        // activeInHierarchy に加えて祖先 CanvasGroup のフェード状態まで見る
        private static bool IsVisible(GameObject go)
        {
            if (!go.activeInHierarchy) return false;
            foreach (var group in go.GetComponentsInParent<CanvasGroup>())
            {
                if (group.alpha <= VISIBLE_ALPHA_THRESHOLD) return false;
                if (group.ignoreParentGroups) break;
            }
            return true;
        }

        // RectTransform の 4 隅をスクリーン座標へ射影した外接矩形
        private static Rect ScreenBoundsOf(GameObject go, Canvas rootCanvas)
        {
            if (go.transform is not RectTransform rect) return Rect.zero;
            // ScreenSpaceCamera/WorldSpace で worldCamera 未設定の場合、GraphicRaycaster の eventCamera と同様に Camera.main へ寄せる
            var camera = rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay
                ? null
                : rootCanvas.worldCamera != null ? rootCanvas.worldCamera : Camera.main;
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            var min = new Vector2(float.MaxValue, float.MaxValue);
            var max = new Vector2(float.MinValue, float.MinValue);
            foreach (var corner in corners)
            {
                var screen = RectTransformUtility.WorldToScreenPoint(camera, corner);
                min = Vector2.Min(min, screen);
                max = Vector2.Max(max, screen);
            }
            return new Rect(min, max - min);
        }
    }
}
