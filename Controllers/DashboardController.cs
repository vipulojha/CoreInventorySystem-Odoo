using CoreInventory.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoreInventory.Controllers;

[Authorize]
[Route("")]
[Route("dashboard")]
public sealed class DashboardController : Controller
{
    private readonly InventoryService _inventoryService;

    public DashboardController(InventoryService inventoryService)
    {
        _inventoryService = inventoryService;
    }

    [HttpGet("")]
    [HttpGet("index")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var model = await _inventoryService.GetDashboardAsync(cancellationToken);
        return View(model);
    }
}
