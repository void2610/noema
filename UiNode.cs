using UnityEngine;
using UnityEngine.EventSystems;

namespace Void2610.Noema
{
    /// <summary>
    /// セマンティックツリーの 1 ノード。構築時点のスナップショットであり、フレームを跨ぐ保持は想定しない
    /// </summary>
    public sealed class UiNode
    {
        public UiRole Role { get; }
        /// <summary>View フィールド由来 ID ("TitleView/startButton")、View 管理外は Transform 階層パス</summary>
        public string Id { get; }
        /// <summary>表示テキスト (ボタンはラベル、入力欄は現在値)。TMP のリッチテキストタグを含む生の文字列</summary>
        public string Text { get; }
        /// <summary>Text からリッチテキストタグを除いた、画面に文字として出る部分</summary>
        public string PlainText => UiText.StripTags(Text);
        public bool Visible { get; }
        public bool Interactable { get; }
        /// <summary>スクリーン座標の外接矩形 (左下原点、ピクセル)</summary>
        public Rect ScreenBounds { get; }
        /// <summary>role を決定したコンポーネント (クリック対象の解決に使う)。ワールド要素では供給元の View</summary>
        public Component Target { get; }
        /// <summary>uGUI 外 (ワールド空間) の要素を供給したプロバイダ。uGUI 要素なら null</summary>
        public IUiNodeProvider Provider { get; }

        public GameObject GameObject => Target.gameObject;

        public bool IsWorld => Provider != null;

        /// <summary>EventSystem の選択中要素 (ゲームパッド / キーボードのフォーカス) か</summary>
        public bool Focused => !IsWorld && EventSystem.current != null && EventSystem.current.currentSelectedGameObject == GameObject;

        public UiNode(UiRole role, string id, string text, bool visible, bool interactable, Rect screenBounds, Component target)
            : this(role, id, text, visible, interactable, screenBounds, target, null)
        {
        }

        public UiNode(UiRole role, string id, string text, bool visible, bool interactable, Rect screenBounds, Component target, IUiNodeProvider provider)
        {
            Role = role;
            Id = id;
            Text = text ?? "";
            Visible = visible;
            Interactable = interactable;
            ScreenBounds = screenBounds;
            Target = target;
            Provider = provider;
        }

        public override string ToString() => $"{Role}\t{Id}\t{Text}\tvisible={Visible}\tinteractable={Interactable}";
    }
}
