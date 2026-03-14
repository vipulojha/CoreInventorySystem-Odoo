using CoreInventory.Extensions;
using CoreInventory.Services;
using CoreInventory.ViewModels.Stock;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoreInventory.Controllers;

[Authorize]
[Route("stock")]
public sealed class StockController : Controller
{
    private readonly InventoryService _inventoryService;

    public StockController(InventoryService inventoryService)
    {
        _inventoryService = inventoryService;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(string? q, long? productId, CancellationToken cancellationToken)
    {
        var model = await _inventoryService.GetStockIndexAsync(q, productId, cancellationToken);
        return View(model);
    }

    [HttpPost("product")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveProduct(ProductFormViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            var fallback = await _inventoryService.GetStockIndexAsync(null, model.Id, cancellationToken);
            fallback.ProductForm = model;
            return View("Index", fallback);
        }

        try
        {
            await _inventoryService.SaveProductAsync(model, cancellationToken);
            TempData["Success"] = "Product catalog updated.";
            return RedirectToAction(nameof(Index));
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            var fallback = await _inventoryService.GetStockIndexAsync(null, model.Id, cancellationToken);
            fallback.ProductForm = model;
            return View("Index", fallback);
        }
    }

    [HttpPost("adjust")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Adjust(StockAdjustmentFormViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            var fallback = await _inventoryService.GetStockIndexAsync(null, null, cancellationToken);
            fallback.AdjustmentForm = model;
            return View("Index", fallback);
        }

        try
        {
            await _inventoryService.AdjustStockAsync(model, User.GetUserId(), cancellationToken);
            TempData["Success"] = "Stock updated successfully.";
            return RedirectToAction(nameof(Index));
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            var fallback = await _inventoryService.GetStockIndexAsync(null, null, cancellationToken);
            fallback.AdjustmentForm = model;
            return View("Index", fallback);
        }
    }
}
