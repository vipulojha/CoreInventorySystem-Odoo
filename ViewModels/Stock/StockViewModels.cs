using System.ComponentModel.DataAnnotations;
using CoreInventory.ViewModels.Shared;

namespace CoreInventory.ViewModels.Stock;

public sealed class ProductFormViewModel
{
    public long? Id { get; set; }

    [Required]
    [StringLength(32)]
    public string Sku { get; set; } = string.Empty;

    [Required]
    [StringLength(120)]
    public string Name { get; set; } = string.Empty;

    [Range(typeof(decimal), "0", "999999999")]
    [Display(Name = "Unit Cost")]
    public decimal UnitCost { get; set; }
}

public sealed class ProductCatalogItemViewModel
{
    public long Id { get; set; }

    public string Sku { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public decimal UnitCost { get; set; }

    public string LastUpdated { get; set; } = string.Empty;
}

public sealed class StockAdjustmentFormViewModel
{
    [Range(1, long.MaxValue)]
    [Display(Name = "Product")]
    public long ProductId { get; set; }

    [Range(1, long.MaxValue)]
    [Display(Name = "Warehouse")]
    public long WarehouseId { get; set; }

    [Range(1, long.MaxValue)]
    [Display(Name = "Location")]
    public long LocationId { get; set; }

    [Range(-999999, 999999)]
    [Display(Name = "Quantity Delta")]
    public int QuantityDelta { get; set; }

    [StringLength(240)]
    public string Note { get; set; } = string.Empty;
}

public sealed class StockBalanceRowViewModel
{
    public string ProductName { get; set; } = string.Empty;

    public string Sku { get; set; } = string.Empty;

    public decimal UnitCost { get; set; }

    public string WarehouseCode { get; set; } = string.Empty;

    public string LocationCode { get; set; } = string.Empty;

    public int OnHand { get; set; }

    public int FreeToUse { get; set; }

    public string UpdatedAt { get; set; } = string.Empty;

    public bool IsLowStock { get; set; }
}

public sealed class StockIndexViewModel
{
    public string Search { get; set; } = string.Empty;

    public ProductFormViewModel ProductForm { get; set; } = new();

    public StockAdjustmentFormViewModel AdjustmentForm { get; set; } = new();

    public IReadOnlyList<ProductCatalogItemViewModel> Products { get; set; } = [];

    public IReadOnlyList<StockBalanceRowViewModel> Balances { get; set; } = [];

    public List<SelectOptionViewModel> ProductOptions { get; set; } = [];

    public List<SelectOptionViewModel> WarehouseOptions { get; set; } = [];

    public List<SelectOptionViewModel> LocationOptions { get; set; } = [];
}
