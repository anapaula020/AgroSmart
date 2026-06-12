using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[Route("admin/[action]")]
[Authorize(AuthenticationSchemes = "Identity.Application")]
[ApiExplorerSettings(IgnoreApi = true)]
public class AdminPagesController : Controller
{
    public IActionResult Properties() { ViewData["Title"] = "Propriedades"; return View(); }
    public IActionResult Harvests()   { ViewData["Title"] = "Safras";       return View(); }
    public IActionResult Stock()      { ViewData["Title"] = "Estoque";      return View(); }
    public IActionResult Weather()    { ViewData["Title"] = "Clima";        return View(); }
    public IActionResult Alerts()     { ViewData["Title"] = "Alertas";      return View(); }

    public IActionResult Workspaces()  { ViewData["Title"] = "Workspaces";   return View(); }

    [Authorize(AuthenticationSchemes = "Identity.Application", Roles = Api.Roles.Admin)]
    public IActionResult Users()      { ViewData["Title"] = "Usuários";     return View(); }
}
