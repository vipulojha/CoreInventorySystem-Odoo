using CoreInventory.Extensions;
using CoreInventory.Models.Inventory;
using CoreInventory.Services;
using CoreInventory.ViewModels.Operations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoreInventory.Controllers;

[Authorize]
[Route("operations")]
public sealed class OperationsController : Controller
{
    private readonly InventoryService _inventoryService;

    public OperationsController(InventoryService inventoryService)
    {
        _inventoryService = inventoryService;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(string? type, string? q, string? view, CancellationToken cancellationToken)
    {
        var model = await _inventoryService.GetOperationsAsync(type, q, view, cancellationToken);
        return View(model);
    }

    [HttpGet("new")]
    public async Task<IActionResult> Create(string? type, CancellationToken cancellationToken)
    {
        var model = await _inventoryService.GetOperationEditorAsync(null, type, cancellationToken);
        return View("Editor", model);
    }

    [HttpGet("{id:long}")]
    public async Task<IActionResult> Edit(long id, CancellationToken cancellationToken)
    {
        var model = await _inventoryService.GetOperationEditorAsync(id, null, cancellationToken);
        return View("Editor", model);
    }

    [HttpPost("save")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(OperationEditorViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            var fallback = await _inventoryService.GetOperationEditorAsync(model.Id, model.Type, cancellationToken);
            HydrateEditorFallback(fallback, model);
            return View("Editor", fallback);
        }

        try
        {
            var operationId = await _inventoryService.SaveOperationAsync(model, User.GetUserId(), cancellationToken);
            TempData["Success"] = $"{OperationTypes.Normalize(model.Type)} saved successfully.";
            return RedirectToAction(nameof(Edit), new { id = operationId });
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            var fallback = await _inventoryService.GetOperationEditorAsync(model.Id, model.Type, cancellationToken);
            HydrateEditorFallback(fallback, model);
            return View("Editor", fallback);
        }
    }

    [HttpPost("{id:long}/transition")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Transition(long id, string action, CancellationToken cancellationToken)
    {
        try
        {
            TempData["Success"] = await _inventoryService.TransitionOperationAsync(id, action, User.GetUserId(), cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Edit), new { id });
    }

    [HttpGet("{id:long}/print")]
    public async Task<IActionResult> Print(long id, CancellationToken cancellationToken)
    {
        var model = await _inventoryService.GetOperationPrintAsync(id, cancellationToken);
        return View(model);
    }

    private static void HydrateEditorFallback(OperationEditorViewModel target, OperationEditorViewModel source)
    {
        target.Id = source.Id;
        target.Type = source.Type;
        target.Status = source.Status;
        target.Reference = source.Reference;
        target.WarehouseId = source.WarehouseId;
        target.FromLocationId = source.FromLocationId;
        target.ToLocationId = source.ToLocationId;
        target.ContactName = source.ContactName;
        target.DeliveryAddress = source.DeliveryAddress;
        target.ScheduleDate = source.ScheduleDate;
        target.ResponsibleUserId = source.ResponsibleUserId;
        target.Notes = source.Notes;
        target.Lines = source.Lines;
    }
}
