using Enfermeria_app.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Enfermeria_app.Controllers
{
    [Authorize]
    public class EnfPersonasController : Controller
    {
        private readonly EnfermeriaContext _context;

        public EnfPersonasController(EnfermeriaContext context)
        {
            _context = context;
        }

        // 🧪 Helper: Determinar tipo del usuario actual
        private string TipoUsuario =>
            User?.Claims?.FirstOrDefault(c => c.Type == "TipoUsuario")?.Value ?? "";

        private bool EsAdmin => TipoUsuario == "Administrativo";
        private bool EsConsultorio => TipoUsuario == "Consultorio";


        // 📋 LISTADO DE PERSONAS ACTIVAS
        [Authorize(Policy = "AdminFullAccess")] // Consultorio + Administrativo
        public async Task<IActionResult> Index(string searchString)
        {
            var personas = _context.EnfPersonas.Where(p => p.Activo);

            if (!string.IsNullOrEmpty(searchString))
            {
                personas = personas.Where(p =>
                    p.Nombre.Contains(searchString) ||
                    p.Cedula.Contains(searchString));
            }

            var lista = await personas
                .OrderByDescending(p => p.Id)
                .ToListAsync();

            ViewData["CurrentFilter"] = searchString;
            return View(lista);
        }


        // 🔍 FILTRO EN TIEMPO REAL (AJAX)
        [Authorize(Policy = "AdminFullAccess")] // Consultorio + Administrativo
        [HttpGet]
        public async Task<IActionResult> Buscar(string term)
        {
            var personas = _context.EnfPersonas.Where(p => p.Activo);

            if (!string.IsNullOrEmpty(term))
            {
                personas = personas.Where(p =>
                    p.Nombre.Contains(term) ||
                    p.Cedula.Contains(term));
            }

            var lista = await personas.OrderByDescending(p => p.Id).ToListAsync();
            return PartialView("_PersonasFilas", lista);
        }


        // 👥 VER PERSONAS INACTIVAS
        [Authorize(Policy = "AdminFullAccess")] // Consultorio + Administrativo
        public async Task<IActionResult> Inactivos()
        {
            var inactivos = await _context.EnfPersonas
                .Where(p => !p.Activo)
                .OrderBy(p => p.Nombre)
                .ToListAsync();

            return View(inactivos);
        }


        // 🔄 REACTIVAR PERSONA
        [Authorize(Policy = "Administrativo")] // SOLO administrativo
        [HttpPost]
        public async Task<IActionResult> Reactivar(int id)
        {
            var persona = await _context.EnfPersonas.FindAsync(id);
            if (persona != null)
            {
                persona.Activo = true;
                _context.Update(persona);
                await _context.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Inactivos));
        }


        // 👁️ DETALLES
        [Authorize(Policy = "AdminFullAccess")] // Consultorio + Administrativo
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var enfPersona = await _context.EnfPersonas
                .FirstOrDefaultAsync(m => m.Id == id);

            if (enfPersona == null) return NotFound();

            return View(enfPersona);
        }


        // ➕ CREAR PERSONA
        [Authorize(Policy = "Administrativo")] // SOLO administrativo
        [HttpGet]
        public IActionResult Create()
        {
            return View();
        }

        [Authorize(Policy = "Administrativo")] // SOLO administrativo
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(EnfPersona model)
        {
            if (!ModelState.IsValid)
                return View(model);

            if (_context.EnfPersonas.Any(p => p.Cedula == model.Cedula))
            {
                ViewBag.Error = "La cédula ya está registrada.";
                return View(model);
            }

            if (_context.EnfPersonas.Any(p => p.Email == model.Email))
            {
                ViewBag.Error = "El correo ya está registrado.";
                return View(model);
            }

            if (_context.EnfPersonas.Any(p => p.Usuario == model.Usuario))
            {
                ViewBag.Error = "El usuario ya existe.";
                return View(model);
            }

            _context.EnfPersonas.Add(model);
            await _context.SaveChangesAsync();

            TempData["Mensaje"] = "✅ Persona registrada correctamente.";
            return RedirectToAction(nameof(Index));
        }


        // ✏️ EDITAR PERSONA
        [Authorize(Policy = "Administrativo")] // SOLO administrativo
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var enfPersona = await _context.EnfPersonas.FindAsync(id);
            if (enfPersona == null) return NotFound();

            return View(enfPersona);
        }

        [Authorize(Policy = "Administrativo")] // SOLO administrativo
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, EnfPersona enfPersona)
        {
            if (id != enfPersona.Id) return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(enfPersona);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!_context.EnfPersonas.Any(e => e.Id == enfPersona.Id))
                        return NotFound();
                    else
                        throw;
                }

                TempData["Mensaje"] = "✅ Cambios guardados correctamente.";
                return RedirectToAction(nameof(Index));
            }
            return View(enfPersona);
        }


        // ❌ DESACTIVAR PERSONA
        [Authorize(Policy = "Administrativo")] // SOLO administrativo
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var enfPersona = await _context.EnfPersonas
                .FirstOrDefaultAsync(m => m.Id == id);

            if (enfPersona == null) return NotFound();

            return View(enfPersona);
        }

        [Authorize(Policy = "Administrativo")] // SOLO administrativo
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

            TempData["Mensaje"] = "⚠️ Persona desactivada correctamente.";
            return RedirectToAction(nameof(Index));
        }


        // 🚨 CREAR CITA DE EMERGENCIA
        [Authorize(Policy = "Consultorio")] // SOLO consultorio, NO administrativo 
        [HttpPost]
        public async Task<IActionResult> CitaEmergencia(int id)
        {
            var persona = await _context.EnfPersonas.FindAsync(id);
            if (persona == null)
                return Json(new { success = false, message = "Persona no encontrada." });

            var ahora = DateTime.Now;
            var fechaHoy = DateOnly.FromDateTime(ahora);
            var horaActual = TimeOnly.FromDateTime(ahora);

            var horario = await _context.EnfHorarios
                .FirstOrDefaultAsync(h => h.Fecha == fechaHoy && h.Hora == horaActual);

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

            var cita = new EnfCita
            {
                IdPersona = persona.Id,
                IdHorario = horario.Id,
                FechaCreacion = ahora,
                Estado = "Creada",
                HoraLlegada = horaActual,
                UsuarioCreacion = "Emergencia"
            };

            _context.EnfCitas.Add(cita);
            await _context.SaveChangesAsync();

            return Json(new
            {
                success = true,
                message = $"✅ Cita de emergencia creada para {persona.Nombre} a las {horaActual:HH:mm}."
            });
        }
    }
}
