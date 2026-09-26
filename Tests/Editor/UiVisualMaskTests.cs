using NUnit.Framework;
using UnityEngine;

namespace Void2610.Noema.Tests
{
    public sealed class UiVisualMaskTests
    {
        private static readonly Color32 Black = new(0, 0, 0, 255);
        private static readonly Color32 White = new(255, 255, 255, 255);

        [Test]
        public void ApplyMasks_マスク内の差分は数えない()
        {
            // 4x2 の右半分だけが違う
            var baseline = new[] { Black, Black, Black, Black, Black, Black, Black, Black };
            var actual = new[] { Black, Black, White, White, Black, Black, White, White };

            UiVisualDiff.ApplyMasks(baseline, actual, 4, 2, new[] { new Rect(0.5f, 0f, 0.5f, 1f) });

            Assert.That(UiVisualDiff.Compare(baseline, actual).DiffPixels, Is.EqualTo(0));
        }

        [Test]
        public void ApplyMasks_マスク外の差分は残す()
        {
            var baseline = new[] { Black, Black, Black, Black };
            var actual = new[] { White, Black, Black, White };

            UiVisualDiff.ApplyMasks(baseline, actual, 2, 2, new[] { new Rect(0f, 0f, 0.5f, 0.5f) });

            Assert.That(UiVisualDiff.Compare(baseline, actual).DiffPixels, Is.EqualTo(1));
        }

        [Test]
        public void ParseMasks_セミコロン区切りの矩形を読む()
        {
            var masks = UiVisualRegression.ParseMasks("0,0,0.5,0.25; 0.5,0.5,0.1,0.2");

            Assert.That(masks, Is.EqualTo(new[] { new Rect(0f, 0f, 0.5f, 0.25f), new Rect(0.5f, 0.5f, 0.1f, 0.2f) }));
        }

        [Test]
        public void ParseMasks_空ならnull()
        {
            Assert.That(UiVisualRegression.ParseMasks(""), Is.Null);
        }

        [Test]
        public void ParseMasks_値が4つでなければ例外()
        {
            Assert.Throws<System.ArgumentException>(() => UiVisualRegression.ParseMasks("0,0,1"));
        }
    }
}
