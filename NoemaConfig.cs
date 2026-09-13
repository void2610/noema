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
        /// 接頭辞を一時的に差し替え、Dispose で元の値へ戻すスコープ。
        /// テストのように「自分のアセンブリだけを走査させたいが、他のテストが設定した値を壊したくない」場面で使う
        /// (グローバルな可変 static なので、直接代入すると同一実行内の他テストへ漏れる)
        /// </summary>
        public static System.IDisposable OverrideAssemblyPrefix(string prefix) => new AssemblyPrefixScope(prefix);

        private sealed class AssemblyPrefixScope : System.IDisposable
        {
            private readonly string _previous;
            private bool _disposed;

            public AssemblyPrefixScope(string prefix)
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
