using UnityEngine;

namespace Void2610.Noema
{
    /// <summary>
    /// 2 枚のスクリーンショットのピクセル差分。UI のレイアウト/描画崩れをベースライン比較で検出するための純ロジック
    /// </summary>
    public static class UiVisualDiff
    {
        // アンチエイリアス/圧縮由来の微小揺れを差分と見なさないためのチャンネル許容誤差
        private const int CHANNEL_TOLERANCE = 8;

        public readonly struct Result
        {
            public int TotalPixels { get; }
            public int DiffPixels { get; }
            public float DiffRatio => TotalPixels == 0 ? 0f : (float)DiffPixels / TotalPixels;

            public Result(int totalPixels, int diffPixels)
            {
                TotalPixels = totalPixels;
                DiffPixels = diffPixels;
            }
        }

        /// <summary>差分ピクセル数を数え、diffTexture が非 null なら相違箇所を赤でマークする (全配列が同サイズ前提)</summary>
        public static Result Compare(Color32[] baseline, Color32[] actual, Color32[] diffTexture = null)
        {
            if (baseline.Length != actual.Length || (diffTexture != null && diffTexture.Length != actual.Length)) throw new System.ArgumentException($"ピクセル数が不一致: baseline={baseline.Length} actual={actual.Length} diff={diffTexture?.Length.ToString() ?? "null"}");
            var diffCount = 0;
            for (var i = 0; i < baseline.Length; i++)
            {
                if (IsSame(baseline[i], actual[i]))
                {
                    if (diffTexture != null) diffTexture[i] = Dim(actual[i]);
                    continue;
                }
                diffCount++;
                if (diffTexture != null) diffTexture[i] = new Color32(255, 0, 0, 255);
            }
            return new Result(baseline.Length, diffCount);
        }

        /// <summary>
        /// masks (正規化矩形、左下原点) の範囲を比較対象から外すため、actual の該当画素を baseline の値で上書きする。
        /// 時計やパーティクルのように毎回変わる領域を除いて、残りの崩れだけを検出するための前処理
        /// </summary>
        public static void ApplyMasks(Color32[] baseline, Color32[] actual, int width, int height, Rect[] masks)
        {
            if (masks == null || masks.Length == 0) return;
            if (baseline.Length != width * height || actual.Length != width * height) throw new System.ArgumentException($"ピクセル数が {width}x{height} と不一致: baseline={baseline.Length} actual={actual.Length}");
            foreach (var mask in masks)
            {
                var xMin = Mathf.Clamp(Mathf.FloorToInt(mask.xMin * width), 0, width);
                var xMax = Mathf.Clamp(Mathf.CeilToInt(mask.xMax * width), 0, width);
                var yMin = Mathf.Clamp(Mathf.FloorToInt(mask.yMin * height), 0, height);
                var yMax = Mathf.Clamp(Mathf.CeilToInt(mask.yMax * height), 0, height);
                for (var y = yMin; y < yMax; y++)
                {
                    for (var x = xMin; x < xMax; x++) actual[y * width + x] = baseline[y * width + x];
                }
            }
        }

        // アルファはスクリーンショットでは常に不透明なので意図的に比較しない
        private static bool IsSame(Color32 a, Color32 b)
        {
            return Mathf.Abs(a.r - b.r) <= CHANNEL_TOLERANCE && Mathf.Abs(a.g - b.g) <= CHANNEL_TOLERANCE && Mathf.Abs(a.b - b.b) <= CHANNEL_TOLERANCE;
        }

        // 一致箇所は暗く沈めて差分の赤を際立たせる
        private static Color32 Dim(Color32 c)
        {
            return new((byte)(c.r / 4), (byte)(c.g / 4), (byte)(c.b / 4), 255);
        }
    }
}
