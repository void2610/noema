using System.Text.RegularExpressions;

namespace Void2610.Noema
{
    /// <summary>
    /// 表示テキストの正規化。アサートはローカライズ済みの見た目の文字で書きたいため、TMP のタグを落とす
    /// </summary>
    public static class UiText
    {
        // <sprite name=coin> や <color=#fff> のような TMP タグ。"<" を含む素の文字列を壊さないよう、タグ名で始まるものだけを対象にする
        private static readonly Regex TagPattern = new(@"</?[a-zA-Z#][^<>]*>", RegexOptions.Compiled);

        public static string StripTags(string text) => string.IsNullOrEmpty(text) ? "" : TagPattern.Replace(text, "");
    }
}
