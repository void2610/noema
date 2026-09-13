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
        /// <see cref="ProjectAssemblyPrefix"/> を一時的に差し替え、Dispose で直前の値へ戻すスコープを開く。
        /// テストのように「自分のアセンブリだけを走査させたいが、他のテストが設定した値を壊したくない」場面で使う
        /// (グローバルな可変 static なので、直接代入すると同一実行内の他テストへ漏れる)
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

        private sealed class ProjectAssemblyPrefixScope : System.IDisposable
        {
            private readonly string _previous;
            private bool _disposed;

            public ProjectAssemblyPrefixScope(string prefix)
            {
                _previous = ProjectAssemblyPrefix;
                ProjectAssemblyPrefix = prefix;
            }

            // 二重 Dispose で「復元済みの値をさらに巻き戻す」のを防ぐ
            public void Dispose()
            {
                if (_disposed) return;
                _disposed = true;
                ProjectAssemblyPrefix = _previous;
            }
        }
    }
}
