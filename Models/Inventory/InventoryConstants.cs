namespace CoreInventory.Models.Inventory;

public static class OperationTypes
{
    public const string Receipt = "Receipt";
    public const string Delivery = "Delivery";
    public const string Adjustment = "Adjustment";

    public static readonly string[] All = [Receipt, Delivery, Adjustment];

    public static string Normalize(string? value)
    {
        return value?.Trim().ToLowerInvariant() switch
        {
            "receipt" => Receipt,
            "delivery" => Delivery,
            "adjustment" => Adjustment,
            _ => Receipt
        };
    }

    public static string ToReferenceCode(string value)
    {
        return Normalize(value) switch
        {
            Delivery => "OUT",
            Adjustment => "ADJ",
            _ => "IN"
        };
    }
}

public static class OperationStatuses
{
    public const string Draft = "Draft";
    public const string Waiting = "Waiting";
    public const string Ready = "Ready";
    public const string Done = "Done";
    public const string Cancelled = "Cancelled";

    public static readonly string[] Board = [Draft, Waiting, Ready, Done];
}
