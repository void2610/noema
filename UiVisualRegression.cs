using System.IO;
using UnityEngine;

namespace Void2610.Noema
{
    /// <summary>
    /// スクリーンショットのベースライン比較によるビジュアル回帰検証
    /// キャプチャは UI カメラを固定解像度の RenderTexture へ手動描画する方式で、フレーム末イベントに依存しないため
    /// batchmode CI でも実行でき、マシンの Game View 解像度にも左右されない (ScreenSpaceCamera の Canvas は RT サイズへレイアウトされる)。
    /// 制約: ScreenSpaceOverlay の Canvas と UI Toolkit はカメラ描画に乗らないため写らない
    /// </summary>
    public static class UiVisualRegression
    {
        private const string BASELINE_DIR = "Tests/VisualBaselines";
        private const string ARTIFACT_DIR = "outputs/ui-visual";
        private const int CAPTURE_WIDTH = 1920;
        private const int CAPTURE_HEIGHT = 1080;
        public const float DEFAULT_THRESHOLD = 0.005f;

        public static string Assert(string name, float threshold = DEFAULT_THRESHOLD)
        {
            ValidateName(name);
            var actual = Capture();
            try
            {
                var baselinePath = BaselinePath(name);
                // 新規作成を OK にすると「一度も比較されないまま緑」が起こるため、意図的に非 OK を返してコミットを促す
                if (!File.Exists(baselinePath))
                {
                    WritePng(baselinePath, actual);
                    return $"baseline created: {baselinePath} (コミットして再実行する)";
                }

                var baseline = LoadPng(baselinePath);
                try
                {
                    var result = CompareAndArtifact(name, baseline, actual, threshold, out var detail);
                    return result ? "OK" : detail;
                }
                finally
                {
                    Object.Destroy(baseline);
                }
            }
            finally
            {
                Object.Destroy(actual);
            }
        }

        public static string UpdateBaseline(string name)
        {
            ValidateName(name);
            var actual = Capture();
            try
            {
                var path = BaselinePath(name);
                WritePng(path, actual);
                return $"updated {path}";
            }
            finally
            {
                Object.Destroy(actual);
            }
        }

        private static bool CompareAndArtifact(string name, Texture2D baseline, Texture2D actual, float threshold, out string detail)
        {
            var baselinePixels = baseline.GetPixels32();
            var actualPixels = actual.GetPixels32();
            var diffPixels = new Color32[actualPixels.Length];
            var result = UiVisualDiff.Compare(baselinePixels, actualPixels, diffPixels);
            if (result.DiffRatio <= threshold)
            {
                detail = null;
                return true;
            }
            var actualPath = ArtifactPath(name, "actual");
            var diffPath = ArtifactPath(name, "diff");
            WritePng(actualPath, actual);
            WriteDiffPng(diffPath, diffPixels, actual.width, actual.height);
            detail = $"diff {result.DiffRatio:P2} > {threshold:P2} (baseline={BaselinePath(name)} actual={actualPath} diff={diffPath})";
            return false;
        }

        // UI を描画しているカメラ (ScreenSpaceCamera Canvas の worldCamera) を固定解像度 RT へ手動描画する
        private static Texture2D Capture()
        {
            var camera = FindUiCamera();
            if (camera == null) throw new System.InvalidOperationException("UI カメラが見つからない (ScreenSpaceCamera の Canvas と worldCamera が必要)");
            var rt = RenderTexture.GetTemporary(CAPTURE_WIDTH, CAPTURE_HEIGHT, 24);
            var prevTarget = camera.targetTexture;
            var prevActive = RenderTexture.active;
            var prevRect = camera.rect;
            try
            {
                // レターボックス調整済みの camera.rect は実スクリーン基準のため、RT へは全面で描く (CI の Screen サイズに影響されない)
                camera.rect = new Rect(0f, 0f, 1f, 1f);
                camera.targetTexture = rt;
                camera.Render();
                RenderTexture.active = rt;
                var texture = new Texture2D(CAPTURE_WIDTH, CAPTURE_HEIGHT, TextureFormat.RGBA32, false);
                texture.ReadPixels(new Rect(0, 0, CAPTURE_WIDTH, CAPTURE_HEIGHT), 0, 0);
                texture.Apply();
                return texture;
            }
            finally
            {
                camera.rect = prevRect;
                camera.targetTexture = prevTarget;
                RenderTexture.active = prevActive;
                RenderTexture.ReleaseTemporary(rt);
            }
        }

        private static Camera FindUiCamera()
        {
            foreach (var canvas in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            {
                if (canvas.isActiveAndEnabled && canvas.renderMode == RenderMode.ScreenSpaceCamera && canvas.worldCamera != null) return canvas.worldCamera;
            }
            // フォールバックしない: Camera.main で撮るとワールド描画混じりの誤ったベースラインを作りやすい
            return null;
        }

        private static string BaselinePath(string name)
        {
            return Path.Combine(BASELINE_DIR, $"{name}@{CAPTURE_WIDTH}x{CAPTURE_HEIGHT}.png");
        }

        private static string ArtifactPath(string name, string kind)
        {
            return Path.Combine(ARTIFACT_DIR, $"{name}-{kind}.png");
        }

        // name はコマンド引数として外部から渡るため、パス区切りや ../ による意図外パスへの書き込みを拒否する
        private static void ValidateName(string name)
        {
            if (string.IsNullOrEmpty(name) || !System.Text.RegularExpressions.Regex.IsMatch(name, "^[A-Za-z0-9_-]+$")) throw new System.ArgumentException($"name はファイル名安全な文字 (A-Za-z0-9_-) のみ許可: '{name}'");
        }

        private static void WritePng(string path, Texture2D texture)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, texture.EncodeToPNG());
        }

        private static void WriteDiffPng(string path, Color32[] pixels, int width, int height)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            try
            {
                texture.SetPixels32(pixels);
                texture.Apply();
                WritePng(path, texture);
            }
            finally
            {
                Object.Destroy(texture);
            }
        }

        private static Texture2D LoadPng(string path)
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!texture.LoadImage(File.ReadAllBytes(path)))
            {
                Object.Destroy(texture);
                throw new System.InvalidOperationException($"baseline PNG の読込に失敗 (破損の可能性): {path}");
            }
            if (texture.width != CAPTURE_WIDTH || texture.height != CAPTURE_HEIGHT)
            {
                var actualSize = $"{texture.width}x{texture.height}";
                Object.Destroy(texture);
                throw new System.InvalidOperationException($"baseline PNG のサイズが不正: {path} 実サイズ={actualSize} (期待 {CAPTURE_WIDTH}x{CAPTURE_HEIGHT})");
            }
            return texture;
        }
    }
}
