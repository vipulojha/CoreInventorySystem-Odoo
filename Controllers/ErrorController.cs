using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace CoreInventory.Controllers;

[AllowAnonymous]
[Route("error")]
public sealed class ErrorController : Controller
{
    [HttpGet("")]
    [HttpGet("{statusCode:int?}")]
    public IActionResult Index(int? statusCode = null)
    {
        var exceptionFeature = HttpContext.Features.Get<IExceptionHandlerPathFeature>();

        ViewData["StatusCode"] = statusCode ?? 500;
        ViewData["Message"] = statusCode switch
        {
            404 => "The page you requested could not be found.",
            403 => "You do not have access to this page.",
            _ when exceptionFeature is not null => "Something failed while processing the request.",
            _ => "Something went wrong."
        };

        return View();
    }
}
