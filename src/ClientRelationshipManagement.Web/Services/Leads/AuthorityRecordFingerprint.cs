using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace ClientRelationshipManagement.Web.Services.Leads;

public static class AuthorityRecordFingerprint
{
    const char FieldSeparator = '\u001f';

    public static string ComputeHex(params object[] values)
    {
        string canonicalRecord = string.Join(FieldSeparator, values.Select(Canonicalize));
        byte[] digest = SHA256.HashData(Encoding.UTF8.GetBytes(canonicalRecord));
        return Convert.ToHexString(digest);
    }

    static string Canonicalize(object value) => value switch
    {
        null => string.Empty,
        DBNull _ => string.Empty,
        DateTime date => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        DateTimeOffset date => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture) ?? string.Empty,
        _ => value.ToString()?.Trim() ?? string.Empty
    };
}
