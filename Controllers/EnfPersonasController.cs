using Enfermeria_app.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading.Tasks;

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
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var enfPersona = await _context.EnfPersonas
                .FirstOrDefaultAsync(m => m.Id == id);

            if (enfPersona == null) return NotFound();

            return View(enfPersona);
        }

        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var enfPersona = await _context.EnfPersonas
                .FirstOrDefaultAsync(m => m.Id == id);

            if (enfPersona == null) return NotFound();

            return View(enfPersona);
        }
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var enfPersona = await _context.EnfPersonas.FindAsync(id);
            if (enfPersona != null)
            {
                enfPersona.Activo = false;
                _context.Update(enfPersona);
                await _context.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> CitaEmergencia(int id)
        {
            var persona = await _context.EnfPersonas.FindAsync(id);
            if (persona == null)
                return NotFound();

            // Hora actual
            var ahora = DateTime.Now;
            var fechaHoy = DateOnly.FromDateTime(ahora);
            var horaActual = TimeOnly.FromDateTime(ahora);

            // Buscar si ya hay horario para hoy en esa hora exacta
            var horario = await _context.EnfHorarios
                .FirstOrDefaultAsync(h => h.Fecha == fechaHoy && h.Hora == horaActual);

            // Si no existe, crear uno nuevo
            if (horario == null)
            {
                horario = new EnfHorario
                {
                    Fecha = fechaHoy,
                    Hora = horaActual,
                    Estado = "Activo",
                    UsuarioCreacion = "Emergencia"
                };
                _context.EnfHorarios.Add(horario);
                await _context.SaveChangesAsync();
            }

            // Crear cita con estado "Creada" (válido según tu restricción CHECK)
            var cita = new EnfCita
            {
                IdPersona = persona.Id,
                IdHorario = horario.Id,
                FechaCreacion = ahora,
                Estado = "Creada", // ✅ Valor permitido
                HoraLlegada = TimeOnly.FromDateTime(ahora),
                UsuarioCreacion = "Emergencia" // 👈 indicamos que fue cita de emergencia
            };

            _context.EnfCitas.Add(cita);
            await _context.SaveChangesAsync();

            TempData["Mensaje"] = $"✅ Se ha registrado una cita de emergencia para {persona.Nombre} a las {horaActual}.";

            // En lugar de redirigir, devolvemos un mensaje JSON para mostrarlo sin recargar la página
            return Json(new { success = true, message = $"Cita de emergencia creada para {persona.Nombre}" });
        }



    }
}
