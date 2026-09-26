using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace Void2610.Noema.Tests
{
    public sealed class UiViewIdCompositionTests
    {
        private string _savedPrefix;
        private string[] _savedAdditional;
        private Canvas _canvas;

        [SetUp]
        public void SetUp()
        {
            _savedPrefix = NoemaConfig.ProjectAssemblyPrefix;
            _savedAdditional = NoemaConfig.AdditionalAssemblyPrefixes;
            NoemaConfig.ProjectAssemblyPrefix = "";
            NoemaConfig.AdditionalAssemblyPrefixes = new[] { "Void2610.Noema.Tests" };
            // Test Runner が EditMode テスト用に用意した使い捨てシーンへ作る
            _canvas = new GameObject("Canvas", typeof(Canvas)).GetComponent<Canvas>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_canvas.gameObject);
            NoemaConfig.ProjectAssemblyPrefix = _savedPrefix;
            NoemaConfig.AdditionalAssemblyPrefixes = _savedAdditional;
        }

        [Test]
        public void SerializeFieldのボタンは型名とフィールド名のIDになる()
        {
            var parent = CreateView<TestParentView>("Parent", _canvas.transform);
            parent.closeButton = CreateButton("Close", parent.transform);

            Assert.That(UiQuery.FindById("TestParentView/closeButton")?.GameObject, Is.EqualTo(parent.closeButton.gameObject));
        }

        [Test]
        public void コレクション要素のViewは親の要素IDでフィールドIDを合成する()
        {
            var parent = CreateView<TestParentView>("Parent", _canvas.transform);
            for (var i = 0; i < 2; i++)
            {
                var child = CreateView<TestChildView>($"Child{i}", parent.transform);
                child.actionButton = CreateButton("Action", child.transform);
                parent.children.Add(child);
            }

            Assert.That(UiQuery.FindById("TestParentView/children[0]/actionButton")?.GameObject, Is.EqualTo(parent.children[0].actionButton.gameObject));
            Assert.That(UiQuery.FindById("TestParentView/children[1]/actionButton")?.GameObject, Is.EqualTo(parent.children[1].actionButton.gameObject));
        }

        [Test]
        public void UiNodeSourceの辞書はキーをIDに使う()
        {
            var view = CreateView<TestRuntimeView>("Runtime", _canvas.transform);
            var button = CreateButton("Yes", view.transform);
            view.Add("yes", button);

            Assert.That(UiQuery.FindById("TestRuntimeView/options[yes]")?.GameObject, Is.EqualTo(button.gameObject));
        }

        [Test]
        public void FindByIdの高速経路はツリー全体の構築と同じノードを返す()
        {
            var parent = CreateView<TestParentView>("Parent", _canvas.transform);
            parent.closeButton = CreateButton("Close", parent.transform);
            parent.closeButton.interactable = false;

            var fast = UiQuery.FindById("TestParentView/closeButton");
            var expected = UiTreeBuilder.Build().First(n => n.Id == "TestParentView/closeButton");

            Assert.That(fast.ToString(), Is.EqualTo(expected.ToString()));
        }

        [Test]
        public void 接頭辞に一致しないアセンブリのViewはIDの供給源にしない()
        {
            NoemaConfig.AdditionalAssemblyPrefixes = new[] { "Other" };
            NoemaConfig.ProjectAssemblyPrefix = "Other";
            var parent = CreateView<TestParentView>("Parent", _canvas.transform);
            parent.closeButton = CreateButton("Close", parent.transform);

            Assert.That(UiQuery.FindById("TestParentView/closeButton"), Is.Null);
        }

        [Test]
        public void 要素を配下に持つViewの参照は無関係なViewのserialized参照より優先される()
        {
            var parent = CreateView<TestParentView>("Parent", _canvas.transform);
            parent.closeButton = CreateButton("Close", parent.transform);
            // 選択中の要素を追うデバッグ用 View のように、配下に持たない要素を指す参照
            var tracker = CreateView<TestChildView>("Tracker", _canvas.transform);
            tracker.actionButton = parent.closeButton;

            Assert.That(UiQuery.FindById("TestParentView/closeButton")?.GameObject, Is.EqualTo(parent.closeButton.gameObject));
        }

        [Test]
        public void 同じ型のViewが複数あれば単一フィールドで握る親のIDで合成する()
        {
            var owner = CreateView<TestPairView>("Pair", _canvas.transform);
            owner.left = CreateView<TestChildView>("Left", owner.transform);
            owner.left.actionButton = CreateButton("Action", owner.left.transform);
            owner.right = CreateView<TestChildView>("Right", owner.transform);
            owner.right.actionButton = CreateButton("Action", owner.right.transform);

            Assert.That(UiQuery.FindById("TestPairView/left/actionButton")?.GameObject, Is.EqualTo(owner.left.actionButton.gameObject));
            Assert.That(UiQuery.FindById("TestPairView/right/actionButton")?.GameObject, Is.EqualTo(owner.right.actionButton.gameObject));
        }

        [Test]
        public void 型が衝突しない単一フィールドのViewは型名のままのIDになる()
        {
            var owner = CreateView<TestPairView>("Pair", _canvas.transform);
            owner.left = CreateView<TestChildView>("Left", owner.transform);
            owner.left.actionButton = CreateButton("Action", owner.left.transform);

            Assert.That(UiQuery.FindById("TestChildView/actionButton")?.GameObject, Is.EqualTo(owner.left.actionButton.gameObject));
        }

        [Test]
        public void 自作Button派生の基底フィールドはIDの供給源にしない()
        {
            var go = new GameObject("Derived", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(_canvas.transform, false);
            var button = go.AddComponent<TestButton>();
            button.targetGraphic = go.GetComponent<Image>();

            Assert.That(UiTreeBuilder.Build().Any(n => n.Id.Contains("m_TargetGraphic")), Is.False);
        }

        [Test]
        public void 接頭辞が未設定ならツリー構築で例外()
        {
            NoemaConfig.AdditionalAssemblyPrefixes = System.Array.Empty<string>();

            Assert.Throws<System.InvalidOperationException>(() => UiTreeBuilder.Build());
        }

        [Test]
        public void 見つからないIDには近いIDを候補に挙げる()
        {
            var parent = CreateView<TestParentView>("Parent", _canvas.transform);
            parent.closeButton = CreateButton("Close", parent.transform);

            Assert.That(UiQuery.DescribeNotFound("TestParentView/closeButon"), Does.Contain("TestParentView/closeButton"));
        }

        private static T CreateView<T>(string name, Transform parent) where T : MonoBehaviour
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go.AddComponent<T>();
        }

        private static Button CreateButton(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            return go.GetComponent<Button>();
        }
    }
}
