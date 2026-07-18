using UnityEngine;

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
        /// <summary>表示テキスト (ボタンはラベル、入力欄は現在値)</summary>
        public string Text { get; }
        public bool Visible { get; }
        public bool Interactable { get; }
        /// <summary>スクリーン座標の外接矩形 (左下原点、ピクセル)</summary>
        public Rect ScreenBounds { get; }
        /// <summary>role を決定したコンポーネント (クリック対象の解決に使う)</summary>
        public Component Target { get; }

        public GameObject GameObject => Target.gameObject;

        public UiNode(UiRole role, string id, string text, bool visible, bool interactable, Rect screenBounds, Component target)
        {
            Role = role;
            Id = id;
            Text = text;
            Visible = visible;
            Interactable = interactable;
            ScreenBounds = screenBounds;
            Target = target;
        }

        public override string ToString() => $"{Role}\t{Id}\t{Text}\tvisible={Visible}\tinteractable={Interactable}";
    }
}
