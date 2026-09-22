using System.Text.RegularExpressions;

namespace Mustask.Domain.Identity;

/// <summary>
/// Код права: <c>&lt;модуль&gt;.&lt;ресурс&gt;.&lt;действие&gt;</c>, строчные
/// латинские буквы, разделитель — точка (правило именования каталога
/// прав).
/// </summary>
public static partial class PermissionCode
{
    [GeneratedRegex(@"^[a-z]+(\.[a-z]+){2}$", RegexOptions.ExplicitCapture)]
    private static partial Regex Pattern { get; }

    public static bool IsWellFormed(string code) => !string.IsNullOrEmpty(code) && Pattern.IsMatch(code);
}
