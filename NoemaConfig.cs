namespace Void2610.Noema
{
    /// <summary>
    /// noema の利用側設定。ID 解決とカスタムクリック判定の対象を、利用側プロジェクトのアセンブリに限定するための接頭辞
    /// (ライブラリ/エンジン内部の serialized フィールドで ID が非決定化するのを防ぐ)。起動時に必ず設定する
    /// </summary>
    public static class NoemaConfig
    {
        public static string ProjectAssemblyPrefix = "";
    }
}
