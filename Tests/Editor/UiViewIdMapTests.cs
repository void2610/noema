using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace Void2610.Noema.Tests
{
    /// <summary>UiViewIdMap の ID 解決 (Dictionary キー / _ トリム / serialized 優先 / fake-null 耐性) を検証する</summary>
    public sealed class UiViewIdMapTests
    {
        // テスト用 View。NoemaTestSetup が prefix を本アセンブリ (Void2610.Noema.Tests) へ向けるので走査対象になる
        private sealed class FakeView : MonoBehaviour
        {
            [SerializeField] internal Button wiredButton;
            [UiNodeSource] internal readonly Dictionary<string, GameObject> _dynamicButtons = new();
            [UiNodeSource] internal readonly List<GameObject> _rows = new();
            internal readonly List<GameObject> _undeclared = new();
        }

        private sealed class OtherView : MonoBehaviour
        {
            [UiNodeSource] internal readonly Dictionary<string, GameObject> _cache = new();
        }

        // 自分自身の GameObject にあるコンポーネントを持つ View (カード等の実装でよくある形)
        private sealed class SelfRefView : MonoBehaviour
        {
            [SerializeField] internal Image image;
        }

        // 上の View を配列で束ねる親
        private sealed class OwnerView : MonoBehaviour
        {
            [SerializeField] internal SelfRefView[] children;
        }

        private readonly List<GameObject> _created = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _created) Object.DestroyImmediate(go);
            _created.Clear();
        }

        private GameObject New(string name)
        {
            var go = new GameObject(name);
            _created.Add(go);
            return go;
        }

        [Test]
        public void Dictionaryのキーが意味的IDになり_プレフィックスは除去される()
        {
            var view = New("view").AddComponent<FakeView>();
            var keyword = New("keyword");
            view._dynamicButtons["emotion"] = keyword;

            var map = UiViewIdMap.Build();
            Assert.That(map[keyword], Is.EqualTo("FakeView/dynamicButtons[emotion]"));
        }

        [Test]
        public void Listはインデックス付きIDになる()
        {
            var view = New("view").AddComponent<FakeView>();
            var row0 = New("row0");
            var row1 = New("row1");
            view._rows.Add(row0);
            view._rows.Add(row1);

            var map = UiViewIdMap.Build();
            Assert.That(map[row1], Is.EqualTo("FakeView/rows[1]"));
        }

        [Test]
        public void serialized配線は他Viewの実行時フィールドより優先される()
        {
            var button = New("button").AddComponent<Button>();
            var viewA = New("viewA").AddComponent<FakeView>();
            viewA.wiredButton = button;
            var viewB = New("viewB").AddComponent<OtherView>();
            viewB._cache["stale"] = button.gameObject;

            var map = UiViewIdMap.Build();
            Assert.That(map[button.gameObject], Is.EqualTo("FakeView/wiredButton"));
        }

        [Test]
        public void 未宣言の実行時フィールドは読まれない()
        {
            var view = New("view").AddComponent<FakeView>();
            var hidden = New("hidden");
            view._undeclared.Add(hidden);

            var map = UiViewIdMap.Build();
            Assert.That(map.ContainsKey(hidden), Is.False);
        }

        [Test]
        public void 破棄済みオブジェクトを含むDictionaryでも例外にならない()
        {
            var view = New("view").AddComponent<FakeView>();
            var dead = new GameObject("dead");
            view._dynamicButtons["dead"] = dead;
            Object.DestroyImmediate(dead);
            var alive = New("alive");
            view._dynamicButtons["alive"] = alive;

            var map = UiViewIdMap.Build();
            Assert.That(map[alive], Is.EqualTo("FakeView/dynamicButtons[alive]"));
            Assert.That(map.ContainsKey(dead), Is.False);
        }

        [Test]
        public void 自己参照より親からの参照が優先される()
        {
            // 同じ GameObject を親の配列と自分の serialized が指す場合、登録順が FindObjectsByType の
            // 不定順に依存すると実行ごとに ID が入れ替わり、E2E のクリック対象が消える
            var childGo = New("card");
            var child = childGo.AddComponent<SelfRefView>();
            child.image = childGo.AddComponent<Image>();
            var owner = New("owner").AddComponent<OwnerView>();
            owner.children = new[] { child };

            var map = UiViewIdMap.Build();

            Assert.AreEqual("OwnerView/children[0]", map[childGo]);
        }

        [Test]
        public void 競合しない自己参照はそのままIDになる()
        {
            // 親から参照されていない View は、自分の serialized が意味的 ID になる
            var go = New("standalone");
            var view = go.AddComponent<SelfRefView>();
            view.image = go.AddComponent<Image>();

            var map = UiViewIdMap.Build();

            Assert.AreEqual("SelfRefView/image", map[go]);
        }
    }
}
