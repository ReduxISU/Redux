using System.Text;

namespace API.Interfaces.Tools;

/// <summary>
/// Makes a value that may come from a request safe to write into a log entry: line breaks and other control characters
/// are escaped, so the value cannot start a forged log line, and it is cut to a length, so it cannot flood the log.
/// </summary>
public static class LogValue {
    /// <summary>How many characters of a value <see cref="Safe"/> keeps when no other length is given.</summary>
    public const int DefaultMaxLength = 200;

    /// <summary>
    /// <paramref name="value"/> with <c>\r</c> and <c>\n</c> written as those two-character escapes, any other control
    /// character as <c>\uXXXX</c>, and everything past <paramref name="maxLength"/> characters replaced by "...".
    /// A null value is written "(null)".
    /// </summary>
    public static string Safe(string? value, int maxLength = DefaultMaxLength) {
        if (value == null)
            return "(null)";
        string text = value.Length > maxLength ? value[..maxLength] + "..." : value;
        // Line breaks with string.Replace, which CodeQL's log-forging check knows as a sanitizer; then the rest.
        text = text.Replace("\r", "\\r").Replace("\n", "\\n");
        if (!text.Any(char.IsControl))
            return text;
        var escaped = new StringBuilder(text.Length + 16);
        foreach (char c in text) {
            if (char.IsControl(c))
                escaped.Append("\\u").Append(((int)c).ToString("x4"));
            else
                escaped.Append(c);
        }
        return escaped.ToString();
    }
}
