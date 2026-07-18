using System;

namespace Void2610.Noema
{
    /// <summary>
    /// 実行時フィールドをセマンティック UI ID の供給源として明示宣言するマーカー
    /// UiViewIdMap は [SerializeField] と本アトリビュートが付いたフィールドだけを読む (暗黙の型推定はしない)。
    /// Dictionary&lt;string, GameObject/Component&gt; はキーが、List/配列は index が ID になる。
    /// UiTest 本体は開発ビルド限定のため、属性だけを常時コンパイルの本 asmdef に置き View 側を製品ビルドでも壊さない
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class UiNodeSourceAttribute : Attribute
    {
    }
}
