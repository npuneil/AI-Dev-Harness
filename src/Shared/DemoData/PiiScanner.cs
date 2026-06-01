using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace LocalAiDemos.Shared.DemoData;

/// <summary>
/// Cheap regex-based redactor for SSN / email / phone / common demo names.
/// Always run before any cloud egress in the Hybrid harness.
/// </summary>
public static class PiiScanner
{
    private static readonly Regex _ssn = new(@"\b\d{3}-\d{2}-\d{4}\b", RegexOptions.Compiled);
    private static readonly Regex _email = new(@"\b[\w\.\-]+@[\w\.\-]+\.[A-Za-z]{2,}\b", RegexOptions.Compiled);
    private static readonly Regex _phone = new(
        @"\b(\+?\d{1,2}[\s\-\.]?)?(\(?\d{3}\)?[\s\-\.]?)\d{3}[\s\-\.]?\d{4}\b",
        RegexOptions.Compiled);

    // Curated demo names — extend in the consuming app if the demo data adds more.
    private static readonly Regex _names = new(
        @"\b(Sarah\s+Chen|Eleanor\s+R\.|Mateo|Sophia)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static string Redact(string input)
    {
        if (string.IsNullOrEmpty(input)) return input;
        var s = _ssn.Replace(input, "[REDACTED SSN]");
        s = _email.Replace(s, "[REDACTED EMAIL]");
        s = _phone.Replace(s, "[REDACTED PHONE]");
        s = _names.Replace(s, "[REDACTED NAME]");
        return s;
    }

    public static IEnumerable<string> Findings(string input)
    {
        if (string.IsNullOrEmpty(input)) yield break;
        foreach (Match m in _ssn.Matches(input)) yield return $"SSN: {m.Value}";
        foreach (Match m in _email.Matches(input)) yield return $"Email: {m.Value}";
        foreach (Match m in _phone.Matches(input)) yield return $"Phone: {m.Value}";
        foreach (Match m in _names.Matches(input)) yield return $"Name: {m.Value}";
    }
}
