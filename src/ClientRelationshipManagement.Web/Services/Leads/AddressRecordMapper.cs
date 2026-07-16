using cCoder.ClientRelationshipManagement.Platform.Models.Entities;

namespace ClientRelationshipManagement.Web.Services.Leads;

public static class AddressRecordMapper
{
    public static Address CreateFromText(string addressText, string sourceSystem, string createdBy, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(addressText))
            return null;

        Address address = new()
        {
            Id = Guid.NewGuid(),
            SourceSystem = Normalize(sourceSystem),
            CreatedBy = createdBy,
            LastUpdatedBy = createdBy,
            CreatedOn = now,
            LastUpdated = now
        };

        ApplyText(address, addressText, createdBy, now);
        return address;
    }

    public static void ApplyText(Address address, string addressText, string updatedBy, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(address);

        string[] parts = (addressText ?? string.Empty)
            .Split([',', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        address.Line1 = parts.ElementAtOrDefault(0);
        address.Line2 = parts.ElementAtOrDefault(1);
        address.TownOrCity = parts.ElementAtOrDefault(2);
        address.StateOrProvince = parts.ElementAtOrDefault(3);
        address.ZipOrPostalCode = parts.ElementAtOrDefault(4);
        address.CountryId = parts.Length > 5 ? string.Join(", ", parts.Skip(5)) : address.CountryId;
        address.LastUpdatedBy = updatedBy;
        address.LastUpdated = now;
    }

    public static string Format(Address address)
    {
        if (address is null)
            return string.Empty;

        return string.Join(", ", new[]
        {
            address.PoBox,
            address.Line1,
            address.Line2,
            address.TownOrCity,
            address.StateOrProvince,
            address.ZipOrPostalCode,
            address.CountryId
        }.Where(value => !string.IsNullOrWhiteSpace(value)));
    }

    static string Normalize(string value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
