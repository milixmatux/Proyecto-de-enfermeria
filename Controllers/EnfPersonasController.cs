using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Enfermeria_app.Models;

namespace Enfermeria_app.Controllers
{
    public class EnfPersonasController : Controller
    {
        private readonly EnfermeriaContext _context;

        public EnfPersonasController(EnfermeriaContext context)
        {
            _context = context;
        }

        // ✅ Acción principal (ya existe la vista Index.cshtml)
        public async Task<IActionResult> Index(string? searchString)
        {
            var personas = from p in _context.EnfPersonas
                           where p.Activo == true
                           select p;

            if (!string.IsNullOrEmpty(searchString))
            {
                personas = personas.Where(p =>
                    p.Nombre.Contains(searchString) ||
                    p.Cedula.Contains(searchString));
            }

            var lista = await personas.OrderBy(p => p.Nombre).ToListAsync();
            ViewData["CurrentFilter"] = searchString;

            return View(lista);
        }

        // ✅ Acción para buscar en tiempo real
        [HttpGet]
        public async Task<IActionResult> Buscar(string term)
        {
            var personas = from p in _context.EnfPersonas
                           where p.Activo == true
                           select p;

            if (!string.IsNullOrEmpty(term))
            {
                personas = personas.Where(p =>
                    p.Nombre.Contains(term) ||
                    p.Cedula.Contains(term));
            }

            var lista = await personas.OrderBy(p => p.Nombre).ToListAsync();

            // Renderiza solo las filas (partial view)
            return PartialView("_PersonasFilas", lista);
        }
    
    [HttpGet]
        public async Task<IActionResult> CitaEmergencia(int id)
        {
            var persona = await _context.EnfPersonas.FindAsync(id);
            if (persona == null)
                return NotFound();

            // Crear nueva cita de emergencia
            var cita = new EnfCita
            {
                IdPersona = persona.Id,
                FechaCreacion = DateTime.Now,
                Estado = "Emergencia",
                IdHorario = null, // si tu modelo lo requiere, puedes crear uno especial o dejarlo null
                HoraLlegada = DateTime.Now,
                HoraSalida = null
            };

            _context.EnfCita.Add(cita);
            await _context.SaveChangesAsync();

            TempData["Mensaje"] = $"Se ha generado una cita de emergencia para {persona.Nombre} a las {DateTime.Now:hh\\:mm tt}.";
            return RedirectToAction(nameof(Index));
        }
}
    }
