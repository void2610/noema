namespace Void2610.Noema
{
    /// <summary>
    /// noema の利用側設定。ID 解決とカスタムクリック判定の対象を、利用側プロジェクトのアセンブリに限定するための接頭辞
    /// (ライブラリ/エンジン内部の serialized フィールドで ID が非決定化するのを防ぐ)。起動時に必ず設定する
    /// </summary>
    public static class NoemaConfig
    {
        public static string ProjectAssemblyPrefix = "";

        /// <summary>
        /// ProjectAssemblyPrefix 以外に対象へ含めるアセンブリ接頭辞 (設定画面など別アセンブリに切り出した自作 UI 用)。
        /// 配列ごと代入する (Domain Reload 無効時に追記が重複しないよう、追加 API は持たない)
        /// </summary>
        public static string[] AdditionalAssemblyPrefixes = System.Array.Empty<string>();

        /// <summary>ビジュアル回帰のベースライン保存先 (プロジェクトルートからの相対パス)</summary>
        public static string VisualBaselineDirectory = "Tests/VisualBaselines";

        /// <summary>ビジュアル回帰で差分が出たときの actual / diff PNG の出力先</summary>
        public static string VisualArtifactDirectory = "outputs/ui-visual";

        /// <summary>ビジュアル回帰のキャプチャ解像度。ベースラインのファイル名に含まれるため、変えると別ベースライン扱いになる</summary>
        public static int VisualCaptureWidth = 1920;

        public static int VisualCaptureHeight = 1080;

        /// <summary>
        /// <see cref="ProjectAssemblyPrefix"/> を一時的に差し替え、Dispose で直前の値へ戻すスコープを開く。
        /// テストのように「自分のアセンブリだけを走査させたいが、他のテストが設定した値を壊したくない」場面で使う
        /// (グローバルな可変 static なので、直接代入すると同一実行内の他テストへ漏れる)。
        /// 「自分のアセンブリだけ」を満たすため、スコープ中は <see cref="AdditionalAssemblyPrefixes"/> も空にする
        /// </summary>
        /// <remarks>
        /// <b>戻り値は必ず保持して Dispose すること</b>。捨てると直接代入と同じ「恒久上書き」になり元の値へ戻らない
        /// (C# は戻り値の破棄を警告しないので、ここだけは呼び出し側の責任になる)。
        /// NUnit の <c>[SetUpFixture]</c> のように <c>using</c> 文が書けない場所では、フィールドへ退避して
        /// <c>[OneTimeTearDown]</c> で Dispose する。復元は Dispose の呼び出しに依存しており、
        /// スコープを抜ければ必ず戻ることが構造的に保証されているわけではない。
        /// 入れ子にした場合は取得と逆順 (LIFO) で Dispose すること。順序を違えると古い値が復活する。
        /// なお、利用側のテストアセンブリ名は任意でライブラリ側から列挙できないため、
        /// internal + InternalsVisibleTo では代替できず public として公開している
        /// </remarks>
        public static System.IDisposable PushProjectAssemblyPrefix(string prefix) => new ProjectAssemblyPrefixScope(prefix);

        internal static bool HasProjectAssemblyPrefix =>
            !string.IsNullOrEmpty(ProjectAssemblyPrefix) || AdditionalAssemblyPrefixes is { Length: > 0 };

        internal static bool IsProjectAssemblyName(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            if (!string.IsNullOrEmpty(ProjectAssemblyPrefix) && name.StartsWith(ProjectAssemblyPrefix, System.StringComparison.Ordinal)) return true;
            if (AdditionalAssemblyPrefixes == null) return false;
            foreach (var prefix in AdditionalAssemblyPrefixes)
            {
                if (!string.IsNullOrEmpty(prefix) && name.StartsWith(prefix, System.StringComparison.Ordinal)) return true;
            }
            return false;
        }

        private sealed class ProjectAssemblyPrefixScope : System.IDisposable
        {
            private readonly string _previous;
            private readonly string[] _previousAdditional;
            private bool _disposed;

            public ProjectAssemblyPrefixScope(string prefix)
            {
                _previous = ProjectAssemblyPrefix;
                _previousAdditional = AdditionalAssemblyPrefixes;
                ProjectAssemblyPrefix = prefix;
                AdditionalAssemblyPrefixes = System.Array.Empty<string>();
            }

            // 二重 Dispose で「復元済みの値をさらに巻き戻す」のを防ぐ
            public void Dispose()
            {
                if (_disposed) return;
                _disposed = true;
                ProjectAssemblyPrefix = _previous;
                AdditionalAssemblyPrefixes = _previousAdditional;
            }
        }
    }
}
