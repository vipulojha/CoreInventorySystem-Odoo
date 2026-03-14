using System.ComponentModel.DataAnnotations;
using CoreInventory.Models.Inventory;
using CoreInventory.ViewModels.Shared;

namespace CoreInventory.ViewModels.Operations;

public sealed class OperationListItemViewModel
{
    public long Id { get; set; }

    public string Reference { get; set; } = string.Empty;

    public string Type { get; set; } = OperationTypes.Receipt;

    public string ContactName { get; set; } = string.Empty;

    public string ScheduleDate { get; set; } = string.Empty;

    public string Status { get; set; } = OperationStatuses.Draft;

    public string WarehouseCode { get; set; } = string.Empty;

    public string FromLocationLabel { get; set; } = "-";

    public string ToLocationLabel { get; set; } = "-";

    public int LineCount { get; set; }

    public bool IsLate { get; set; }

    public bool IsWaiting { get; set; }
}

public sealed class OperationIndexViewModel
{
    public string SelectedType { get; set; } = OperationTypes.Receipt;

    public string Search { get; set; } = string.Empty;

    public string ViewMode { get; set; } = "list";

    public int OpenCount { get; set; }

    public int LateCount { get; set; }

    public int WaitingCount { get; set; }

    public IReadOnlyList<OperationListItemViewModel> Items { get; set; } = [];

    public IReadOnlyDictionary<string, IReadOnlyList<OperationListItemViewModel>> Board { get; set; } =
        new Dictionary<string, IReadOnlyList<OperationListItemViewModel>>();
}

public sealed class OperationActionViewModel
{
    public string Action { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;

    public string Tone { get; set; } = "primary";
}

public sealed class OperationLineInputViewModel
{
    public long? Id { get; set; }

    public string ProductLabel { get; set; } = string.Empty;

    [Range(1, long.MaxValue)]
    [Display(Name = "Product")]
    public long ProductId { get; set; }

    [Range(1, 999999)]
    public int Quantity { get; set; } = 1;

    [Range(typeof(decimal), "0", "999999999")]
    [Display(Name = "Unit Cost")]
    public decimal UnitCost { get; set; }

    public string Note { get; set; } = string.Empty;

    public bool InsufficientStock { get; set; }

    public int AvailableStock { get; set; }
}

public sealed class OperationEditorViewModel
{
    public long? Id { get; set; }

    public string Type { get; set; } = OperationTypes.Receipt;

    public string Status { get; set; } = OperationStatuses.Draft;

    public string Reference { get; set; } = "Generated on save";

    [Range(1, long.MaxValue)]
    [Display(Name = "Warehouse")]
    public long WarehouseId { get; set; }

    [Display(Name = "From Location")]
    public long? FromLocationId { get; set; }

    [Display(Name = "To Location")]
    public long? ToLocationId { get; set; }

    [Required]
    [StringLength(120)]
    public string ContactName { get; set; } = string.Empty;

    [StringLength(240)]
    [Display(Name = "Delivery Address")]
    public string DeliveryAddress { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Schedule Date")]
    public string ScheduleDate { get; set; } = DateTime.Today.ToString("yyyy-MM-dd");

    [Display(Name = "Responsible")]
    public long? ResponsibleUserId { get; set; }

    [StringLength(1000)]
    public string Notes { get; set; } = string.Empty;

    public List<OperationLineInputViewModel> Lines { get; set; } = [new()];

    public List<SelectOptionViewModel> WarehouseOptions { get; set; } = [];

    public List<SelectOptionViewModel> LocationOptions { get; set; } = [];

    public List<SelectOptionViewModel> ProductOptions { get; set; } = [];

    public List<SelectOptionViewModel> UserOptions { get; set; } = [];

    public List<OperationActionViewModel> Actions { get; set; } = [];

    public string ContactLabel => Type == OperationTypes.Receipt ? "Receive From" : "Contact";

    public string FromLocationLabel => Type == OperationTypes.Delivery ? "Ship From" : "From Location";

    public string ToLocationLabel => Type == OperationTypes.Receipt ? "Receive Into" : "To Location";

    public bool ShowFromLocation => Type != OperationTypes.Receipt;

    public bool ShowToLocation => Type != OperationTypes.Delivery;

    public bool ShowDeliveryAddress => Type == OperationTypes.Delivery;

    public string StatusHint =>
        Type == OperationTypes.Delivery
            ? "Draft -> Waiting -> Ready -> Done. Short stock stays in Waiting until stock is available."
            : "Draft -> Ready -> Done. Use To Do to move the operation forward and Validate to complete it.";
}

public sealed class OperationPrintViewModel
{
    public string Reference { get; set; } = string.Empty;

    public string Type { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public string WarehouseName { get; set; } = string.Empty;

    public string ContactName { get; set; } = string.Empty;

    public string DeliveryAddress { get; set; } = string.Empty;

    public string ScheduleDate { get; set; } = string.Empty;

    public string Responsible { get; set; } = string.Empty;

    public string Notes { get; set; } = string.Empty;

    public string FromLocationLabel { get; set; } = "-";

    public string ToLocationLabel { get; set; } = "-";

    public IReadOnlyList<OperationLineInputViewModel> Lines { get; set; } = [];
}
