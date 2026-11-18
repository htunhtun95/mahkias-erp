namespace Mahkias.Api.Helpers
{
    public static class ImportCellText
    {
        public static string Normalize(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            var text = value.Trim();
            return text.Length == 0 || IsHyphen(text) ? null : text;
        }

        private static bool IsHyphen(string text)
        {
            foreach (var character in text)
            {
                if (character is not ('-' or '\u2010' or '\u2011' or '\u2012' or '\u2013' or '\u2014' or '\u2212'))
                {
                    return false;
                }
            }

            return text.Length > 0;
        }
    }
}
