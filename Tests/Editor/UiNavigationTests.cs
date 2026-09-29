using System.Threading;
using NUnit.Framework;
using UnityEngine;

namespace Void2610.Noema.Tests
{
    /// <summary>UiNavigation の引数の解釈と、デバイスへ流す前の失敗を検証する</summary>
    public sealed class UiNavigationTests
    {
        private IUiNavigationDriver _savedDriver;

        [SetUp]
        public void SetUp()
        {
            _savedDriver = UiNavigation.Driver;
            UiNavigation.Driver = null;
        }

        [TearDown]
        public void TearDown() => UiNavigation.Driver = _savedDriver;

        [TestCase("Up", UiNavigateDirection.Up)]
        [TestCase("down", UiNavigateDirection.Down)]
        [TestCase(" LEFT ", UiNavigateDirection.Left)]
        [TestCase("Right", UiNavigateDirection.Right)]
        public void 向きは大文字小文字と前後の空白を問わず解釈する(string text, UiNavigateDirection expected)
        {
            Assert.That(UiNavigation.TryParse(text, out var parsed), Is.True);
            Assert.That(parsed, Is.EqualTo(expected));
        }

        [TestCase("")]
        [TestCase("Forward")]
        [TestCase("2")]
        [TestCase("-1")]
        [TestCase(null)]
        public void 向きでない文字列は受け付けない(string text)
        {
            Assert.That(UiNavigation.TryParse(text, out _), Is.False);
        }

        [Test]
        public void 向きが不正なら押さずに失敗を返す()
        {
            var result = UiNavigation.NavigateAsync("Forward").GetAwaiter().GetResult();

            Assert.That(result.Success, Is.False);
            StringAssert.Contains("Forward", result.Message);
        }

        [Test]
        public void 回数が1未満なら押さずに失敗を返す()
        {
            var result = UiNavigation.NavigateAsync("Up", 0).GetAwaiter().GetResult();

            Assert.That(result.Success, Is.False);
        }

        [Test]
        public void ドライバが無ければ失敗を返す()
        {
            var result = UiNavigation.NavigateAsync("Up").GetAwaiter().GetResult();

            Assert.That(result.Success, Is.False);
            StringAssert.Contains("IUiNavigationDriver", result.Message);
        }

        [TestCase("South", UiPadButton.South)]
        [TestCase("east", UiPadButton.East)]
        [TestCase(" leftshoulder ", UiPadButton.LeftShoulder)]
        [TestCase("Start", UiPadButton.Start)]
        public void ボタン名は大文字小文字と前後の空白を問わず解釈する(string text, UiPadButton expected)
        {
            Assert.That(UiNavigation.TryParseButton(text, out var parsed), Is.True);
            Assert.That(parsed, Is.EqualTo(expected));
        }

        [TestCase("")]
        [TestCase("A")]
        [TestCase("0")]
        [TestCase("-1")]
        [TestCase(null)]
        public void ボタン名でない文字列は受け付けない(string text)
        {
            Assert.That(UiNavigation.TryParseButton(text, out _), Is.False);
        }

        [Test]
        public void ボタン名が不正なら押さずに失敗を返す()
        {
            var result = UiNavigation.PressAsync("A").GetAwaiter().GetResult();

            Assert.That(result.Success, Is.False);
            StringAssert.Contains("South", result.Message);
        }

        [Test]
        public void ボタンの回数が1未満なら押さずに失敗を返す()
        {
            var result = UiNavigation.PressAsync("South", 0).GetAwaiter().GetResult();

            Assert.That(result.Success, Is.False);
        }

        [Test]
        public void ボタンを押せるドライバが無ければ失敗を返す()
        {
            var result = UiNavigation.PressAsync("South").GetAwaiter().GetResult();

            Assert.That(result.Success, Is.False);
            StringAssert.Contains("IUiPadButtonDriver", result.Message);
        }

        [Test]
        public void キーの回数が1未満なら押さずに失敗を返す()
        {
            var result = UiNavigation.PressKeyAsync("Escape", 0).GetAwaiter().GetResult();

            Assert.That(result.Success, Is.False);
        }

        [Test]
        public void キーを押せるドライバが無ければ失敗を返す()
        {
            var result = UiNavigation.PressKeyAsync("Escape").GetAwaiter().GetResult();

            Assert.That(result.Success, Is.False);
            StringAssert.Contains("IUiKeyDriver", result.Message);
        }

        [Test]
        public void ドライバが解釈できないキー名なら押さずに失敗を返す()
        {
            var driver = new RecordingKeyDriver();
            UiNavigation.Driver = driver;

            var result = UiNavigation.PressKeyAsync("NoSuchKey").GetAwaiter().GetResult();

            Assert.That(result.Success, Is.False);
            StringAssert.Contains("NoSuchKey", result.Message);
            Assert.That(driver.Pressed, Is.Zero);
        }

        private sealed class RecordingKeyDriver : IUiNavigationDriver, IUiKeyDriver
        {
            public int Pressed;

            public Awaitable PressAsync(UiNavigateDirection direction, CancellationToken cancellationToken) => throw new System.NotSupportedException();

            public void Release()
            {
            }

            public bool IsKnownKey(string key) => key == "Escape";

            public Awaitable PressKeyAsync(string key, CancellationToken cancellationToken)
            {
                Pressed++;
                throw new System.NotSupportedException();
            }
        }
    }
}
