namespace Void2610.Noema
{
    /// <summary>
    /// セマンティックツリー上の UI 要素の役割
    /// uGUI コンポーネント型から <see cref="UiTreeBuilder"/> が導出する
    /// </summary>
    public enum UiRole
    {
        /// <summary>Button (クリック可能)</summary>
        Button,
        /// <summary>Toggle (チェック状態を持つ)</summary>
        Checkbox,
        /// <summary>InputField / TMP_InputField (テキスト入力)</summary>
        Textbox,
        /// <summary>Slider (数値入力)</summary>
        Slider,
        /// <summary>Dropdown / TMP_Dropdown (選択リスト)</summary>
        Combobox,
        /// <summary>ScrollRect (スクロール領域)</summary>
        ScrollArea,
        /// <summary>TMP_Text / Text (観測のみ)</summary>
        Text,
        /// <summary>IPointerClickHandler 直実装のカスタムクリック要素 (標準 Selectable 以外)</summary>
        Clickable,
    }
}
