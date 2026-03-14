namespace CoreInventory.ViewModels.History;

public sealed class StockMoveRowViewModel
{
    public string EventAt { get; set; } = string.Empty;

    public string Reference { get; set; } = string.Empty;

    public string ProductName { get; set; } = string.Empty;

    public string Sku { get; set; } = string.Empty;

    public string MoveKind { get; set; } = string.Empty;

    public string FromLocationLabel { get; set; } = "-";

    public string ToLocationLabel { get; set; } = "-";

    public int Quantity { get; set; }

    public string PerformedBy { get; set; } = "-";

    public string Note { get; set; } = string.Empty;
}

public sealed class HistoryIndexViewModel
{
    public string Search { get; set; } = string.Empty;

    public IReadOnlyList<StockMoveRowViewModel> Moves { get; set; } = [];
}
