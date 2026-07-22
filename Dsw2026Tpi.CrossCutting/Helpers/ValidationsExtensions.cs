using System.Text.RegularExpressions;

namespace Dsw2026Tpi.CrossCutting.Helpers;

public static class ValidationsExtensions
{
    public const string EmailPattern = @"^[^\s@]+@[^\s@]+\.[^\s@]{2,}$";
    public static bool IsEmailValid(this string? email)
    {
        return !string.IsNullOrWhiteSpace(email) &&
            Regex.IsMatch(email, EmailPattern);
    }

    public const string DniPattern = @"^\d{7,8}$";
    public static bool IsDniValid(this long dni)
    {
        var dniStr = dni.ToString();
        return dniStr.Length >= 7 && dniStr.Length <= 8;
    }
}
