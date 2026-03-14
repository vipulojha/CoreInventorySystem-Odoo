using CoreInventory.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoreInventory.Controllers;

[Authorize]
[Route("history")]
public sealed class HistoryController : Controller
{
    private readonly InventoryService _inventoryService;

    public HistoryController(InventoryService inventoryService)
    {
        _inventoryService = inventoryService;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(string? q, CancellationToken cancellationToken)
    {
        var model = await _inventoryService.GetHistoryAsync(q, cancellationToken);
        return View(model);
    }
}
