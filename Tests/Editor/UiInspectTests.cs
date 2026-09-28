using NUnit.Framework;
using UnityEngine;

namespace Void2610.Noema.Tests
{
    /// <summary>UiInspect の要素数の数え方を検証する</summary>
    public sealed class UiInspectTests
    {
        private static UiNode Node(string id, bool visible = true) => new(UiRole.Element, id, "", visible, false, Rect.zero, null);

        [Test]
        public void コレクション直下の表示中要素だけを数える()
        {
            var nodes = new[]
            {
                Node("ListView/items[0]"),
                Node("ListView/items[1]"),
                Node("ListView/items[key]"),
                Node("ListView/items[2]", visible: false),
                Node("ListView/items[0]/label"),
                Node("ListView/itemsHeader"),
                Node("OtherView/items[0]"),
            };

            Assert.That(UiInspect.CountItems("ListView/items", nodes), Is.EqualTo(3));
        }
    }
}
