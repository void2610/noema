using NUnit.Framework;

namespace Void2610.Noema.Tests
{
    public sealed class UiTextTests
    {
        [TestCase("コイン<sprite name=coin>10", "コイン10")]
        [TestCase("<color=#ff0000>赤</color>字", "赤字")]
        [TestCase("<b>太字</b>", "太字")]
        [TestCase("1 < 2", "1 < 2")]
        [TestCase("", "")]
        [TestCase(null, "")]
        public void StripTags_TMPタグだけを除く(string text, string expected)
        {
            Assert.That(UiText.StripTags(text), Is.EqualTo(expected));
        }
    }
}
