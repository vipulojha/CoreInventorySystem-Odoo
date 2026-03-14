using CoreInventory.ViewModels.Operations;
using CoreInventory.ViewModels.Stock;

namespace CoreInventory.ViewModels.Dashboard;

public sealed class DashboardViewModel
{
    public int ReceiptOpenCount { get; set; }

    public int ReceiptLateCount { get; set; }

    public int DeliveryOpenCount { get; set; }

    public int DeliveryWaitingCount { get; set; }

    public int TotalOperationCount { get; set; }

    public int LowStockCount { get; set; }

    public IReadOnlyList<OperationListItemViewModel> UpcomingOperations { get; set; } = [];

    public IReadOnlyList<StockBalanceRowViewModel> LowStockItems { get; set; } = [];
}
