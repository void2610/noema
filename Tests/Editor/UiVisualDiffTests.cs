using System;
using NUnit.Framework;
using UnityEngine;

namespace Void2610.Noema.Tests
{
    /// <summary>UiVisualDiff (ピクセル差分の純ロジック) の境界と例外を検証する</summary>
    public sealed class UiVisualDiffTests
    {
        private static Color32 Gray(byte v) => new(v, v, v, 255);

        [Test]
        public void 同一配列は差分ゼロ()
        {
            var pixels = new[] { Gray(10), Gray(200) };
            var result = UiVisualDiff.Compare(pixels, (Color32[])pixels.Clone());
            Assert.That(result.DiffPixels, Is.Zero);
            Assert.That(result.DiffRatio, Is.Zero);
        }

        [Test]
        public void チャンネル差8は許容し9は差分になる()
        {
            var baseline = new[] { Gray(100), Gray(100) };
            var withinTolerance = new[] { Gray(108), Gray(100) };
            var beyondTolerance = new[] { Gray(109), Gray(100) };
            Assert.That(UiVisualDiff.Compare(baseline, withinTolerance).DiffPixels, Is.Zero);
            Assert.That(UiVisualDiff.Compare(baseline, beyondTolerance).DiffPixels, Is.EqualTo(1));
        }

        [Test]
        public void DiffRatioは差分ピクセル数を総数で割った値になる()
        {
            var baseline = new[] { Gray(0), Gray(0), Gray(0), Gray(0) };
            var actual = new[] { Gray(255), Gray(0), Gray(0), Gray(0) };
            Assert.That(UiVisualDiff.Compare(baseline, actual).DiffRatio, Is.EqualTo(0.25f));
        }

        [Test]
        public void 差分テクスチャは相違を赤_一致を減光でマークする()
        {
            var baseline = new[] { Gray(0), Gray(200) };
            var actual = new[] { Gray(255), Gray(200) };
            var diff = new Color32[2];
            UiVisualDiff.Compare(baseline, actual, diff);
            Assert.That(diff[0], Is.EqualTo(new Color32(255, 0, 0, 255)));
            Assert.That(diff[1], Is.EqualTo(new Color32(50, 50, 50, 255)));
        }

        [Test]
        public void 配列長不一致は明示例外になる()
        {
            var two = new[] { Gray(0), Gray(0) };
            var three = new[] { Gray(0), Gray(0), Gray(0) };
            Assert.Throws<ArgumentException>(() => UiVisualDiff.Compare(two, three));
            Assert.Throws<ArgumentException>(() => UiVisualDiff.Compare(two, two, three));
        }
    }
}
