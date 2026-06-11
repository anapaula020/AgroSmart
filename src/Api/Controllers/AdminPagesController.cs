using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[Route("admin/[action]")]
public class AdminPagesController : Controller
{
    public IActionResult Properties() { ViewData["Title"] = "Propriedades"; return View(); }
    public IActionResult Harvests()   { ViewData["Title"] = "Safras";       return View(); }
    public IActionResult Stock()      { ViewData["Title"] = "Estoque";      return View(); }
    public IActionResult Weather()    { ViewData["Title"] = "Clima";        return View(); }
    public IActionResult Alerts()     { ViewData["Title"] = "Alertas";      return View(); }
}
