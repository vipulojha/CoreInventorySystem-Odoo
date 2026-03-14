using System.ComponentModel.DataAnnotations;
using CoreInventory.ViewModels.Shared;

namespace CoreInventory.ViewModels.Settings;

public sealed class WarehouseFormViewModel
{
    public long? Id { get; set; }

    [Required]
    [StringLength(10)]
    [Display(Name = "Short Code")]
    public string Code { get; set; } = string.Empty;

    [Required]
    [StringLength(80)]
    public string Name { get; set; } = string.Empty;

    [StringLength(240)]
    public string Address { get; set; } = string.Empty;
}

public sealed class WarehouseRowViewModel
{
    public long Id { get; set; }

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Address { get; set; } = string.Empty;
}

public sealed class LocationFormViewModel
{
    public long? Id { get; set; }

    [Required]
    [Range(1, long.MaxValue)]
    [Display(Name = "Warehouse")]
    public long WarehouseId { get; set; }

    [Required]
    [StringLength(30)]
    [Display(Name = "Short Code")]
    public string Code { get; set; } = string.Empty;

    [Required]
    [StringLength(80)]
    public string Name { get; set; } = string.Empty;

    [StringLength(20)]
    public string Kind { get; set; } = "Stock";
}

public sealed class LocationRowViewModel
{
    public long Id { get; set; }

    public string WarehouseCode { get; set; } = string.Empty;

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Kind { get; set; } = string.Empty;
}

public sealed class SettingsIndexViewModel
{
    public WarehouseFormViewModel WarehouseForm { get; set; } = new();

    public LocationFormViewModel LocationForm { get; set; } = new();

    public IReadOnlyList<WarehouseRowViewModel> Warehouses { get; set; } = [];

    public IReadOnlyList<LocationRowViewModel> Locations { get; set; } = [];

    public List<SelectOptionViewModel> WarehouseOptions { get; set; } = [];
}
