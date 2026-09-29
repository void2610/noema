using NUnit.Framework;

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
    }
}
