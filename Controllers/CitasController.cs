using Enfermeria_app.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;

namespace Enfermeria_app.Controllers
{
    
    public class CitasController : Controller
    {
        public IActionResult Publicar()
        {
            return View();
        }

        public IActionResult CheckInOut()
        {
            return View();
        }

        public IActionResult Cancelar()
        {
            return View();
        }

        public IActionResult Sacar()
        {
            return View();
        }

     
            private readonly EnfermeriaContext _context;

            public CitasController(EnfermeriaContext context)
            {
                _context = context;
            }

            public IActionResult Emergencia()
        {
            return View();
        }

        [Authorize]
        
            // Vista para estudiantes y funcionarios
            public async Task<IActionResult> Historial()
        {
            var username = User.Identity?.Name;

            if (string.IsNullOrEmpty(username))
            {
                return RedirectToAction("Login", "Cuenta");
            }

            var persona = await _context.EnfPersonas.FirstOrDefaultAsync(p => p.Usuario == username);

            if (persona == null)
            {
                TempData["Error"] = "No se encontró información del usuario.";
                return RedirectToAction("Login", "Cuenta");
            }

            var citas = await _context.EnfCitas
                .Where(c => c.IdPersona == persona.Id)
                .Include(c => c.IdHorarioNavigation)
                .OrderByDescending(c => c.FechaCreacion)
                .ToListAsync();

            return View("Historial", citas);
        }
        

        // Vista para profesores con filtro
        public async Task<IActionResult> Estudiante_Historial(string filtro = null)
        {
            var username = User.Identity?.Name;
            var persona = await _context.EnfPersonas.FirstOrDefaultAsync(p => p.Usuario == username);

            

            var citas = _context.EnfCitas
                .Include(c => c.IdPersonaNavigation)
                .Include(c => c.IdHorarioNavigation)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(filtro))
            {
                citas = citas.Where(c =>
                    (c.IdPersonaNavigation.Nombre.Contains(filtro) ||
                    c.IdPersonaNavigation.Cedula.Contains(filtro)) &&
                    c.IdPersonaNavigation.Tipo == "Estudiante");
            }
            else
            {
                citas = citas.Take(0);
            }

            return View("Estudiante_Historial", await citas.OrderByDescending(c => c.FechaCreacion).ToListAsync());
        }

    }
}