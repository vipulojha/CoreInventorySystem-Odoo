using CoreInventory.Services;
using CoreInventory.ViewModels.Settings;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoreInventory.Controllers;

[Authorize]
[Route("settings")]
public sealed class SettingsController : Controller
{
    private readonly InventoryService _inventoryService;

    public SettingsController(InventoryService inventoryService)
    {
        _inventoryService = inventoryService;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(long? warehouseId, long? locationId, CancellationToken cancellationToken)
    {
        var model = await _inventoryService.GetSettingsAsync(warehouseId, locationId, cancellationToken);
        return View(model);
    }

    [HttpPost("warehouse")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveWarehouse(WarehouseFormViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            var fallback = await _inventoryService.GetSettingsAsync(model.Id, null, cancellationToken);
            fallback.WarehouseForm = model;
            return View("Index", fallback);
        }

        try
        {
            await _inventoryService.SaveWarehouseAsync(model, cancellationToken);
            TempData["Success"] = "Warehouse saved.";
            return RedirectToAction(nameof(Index));
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            var fallback = await _inventoryService.GetSettingsAsync(model.Id, null, cancellationToken);
            fallback.WarehouseForm = model;
            return View("Index", fallback);
        }
    }

    [HttpPost("location")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveLocation(LocationFormViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            var fallback = await _inventoryService.GetSettingsAsync(null, model.Id, cancellationToken);
            fallback.LocationForm = model;
            return View("Index", fallback);
        }

        try
        {
            await _inventoryService.SaveLocationAsync(model, cancellationToken);
            TempData["Success"] = "Location saved.";
            return RedirectToAction(nameof(Index));
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            var fallback = await _inventoryService.GetSettingsAsync(null, model.Id, cancellationToken);
            fallback.LocationForm = model;
            return View("Index", fallback);
        }
    }
}
