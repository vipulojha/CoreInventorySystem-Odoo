namespace CoreInventory.ViewModels.Shared;

public sealed class SelectOptionViewModel
{
    public long Value { get; set; }

    public string Label { get; set; } = string.Empty;

    public string Group { get; set; } = string.Empty;
}
