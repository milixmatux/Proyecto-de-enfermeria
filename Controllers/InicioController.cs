using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Enfermeria_app.Controllers
{
    [Authorize]
    public class InicioController : Controller
    {
        public IActionResult Inicio()
        {
            return View(); // Buscará Views/Inicio/Inicio.cshtml
        }
    }
}