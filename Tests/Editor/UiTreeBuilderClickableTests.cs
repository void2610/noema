using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Void2610.Noema.Tests
{
    /// <summary>Clickable role (IPointerClickHandler 直実装) のツリー化を検証する</summary>
    public sealed class UiTreeBuilderClickableTests
    {
        // カード等を模したカスタムクリック要素。NoemaTestSetup が prefix を本アセンブリへ向けるので対象になる
        private sealed class FakeCard : MonoBehaviour, IPointerClickHandler
        {
            public void OnPointerClick(PointerEventData eventData) { }
        }

        private readonly List<GameObject> _created = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _created) Object.DestroyImmediate(go);
            _created.Clear();
        }

        private GameObject NewUnderCanvas(out GameObject canvasGo)
        {
            canvasGo = new GameObject("Canvas", typeof(Canvas));
            _created.Add(canvasGo);
            var go = new GameObject("child", typeof(RectTransform));
            go.transform.SetParent(canvasGo.transform, false);
            return go;
        }

        [Test]
        public void カスタムクリック要素はClickable_roleでツリー化される()
        {
            var go = NewUnderCanvas(out _);
            go.AddComponent<FakeCard>();

            var node = UiTreeBuilder.Build().FirstOrDefault(n => n.GameObject == go);
            Assert.That(node, Is.Not.Null);
            Assert.That(node.Role, Is.EqualTo(UiRole.Clickable));
        }

        [Test]
        public void Buttonが同居する場合はButton_roleが優先される()
        {
            var go = NewUnderCanvas(out _);
            go.AddComponent<Button>();
            go.AddComponent<FakeCard>();

            var node = UiTreeBuilder.Build().FirstOrDefault(n => n.GameObject == go);
            Assert.That(node.Role, Is.EqualTo(UiRole.Button));
        }
    }
}
