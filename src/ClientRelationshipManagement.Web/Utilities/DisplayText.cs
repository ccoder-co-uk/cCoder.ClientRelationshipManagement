namespace ClientRelationshipManagement.Web.Utilities;

public static class DisplayText
{
    public static string Humanize(Enum value) =>
        Humanize(value.ToString());

    public static string Humanize(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "Unspecified";

        System.Text.StringBuilder builder = new();

        for (int index = 0; index < value.Length; index++)
        {
            char current = value[index];

            if (index > 0 && char.IsUpper(current) && !char.IsUpper(value[index - 1]))
                builder.Append(' ');

            builder.Append(current);
        }

        return builder.ToString();
    }
}
