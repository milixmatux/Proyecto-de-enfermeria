using Enfermeria_app.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;

namespace Enfermeria_app.Controllers
{
    [Authorize] // Se controla adentro quién puede hacer qué
    public class EnfPersonasController : Controller
    {
        private readonly EnfermeriaContext _context;

        public EnfPersonasController(EnfermeriaContext context)
        {
            _context = context;
        }

        // ============================================================
        // 🔐 MÉTODOS DE CONTROL DE PERMISOS
        // ============================================================
        private bool EsAdministrativo()
        {
            return User.HasClaim("TipoUsuario", "Administrativo");
        }

        private bool EsConsultorio()
        {
            return User.HasClaim("TipoUsuario", "Consultorio");
        }

        // ============================================================
        // 📋 LISTADO DE PERSONAS (solo Administrativo y Consultorio)
        // ============================================================
        public async Task<IActionResult> Index(string searchString)
        {
            if (!EsAdministrativo() && !EsConsultorio())
                return RedirectToAction("AccesoDenegado", "Home");

            var personas = _context.EnfPersonas.Where(p => p.Activo);

            if (!string.IsNullOrEmpty(searchString))
            {
                personas = personas.Where(p =>
                    p.Nombre.Contains(searchString) || p.Cedula.Contains(searchString));
            }

            var lista = await personas
                .OrderByDescending(p => p.Id)
                .ToListAsync();

            ViewBag.EsAdmin = EsAdministrativo();   // controla botones
            ViewBag.EsConsultorio = EsConsultorio();

            return View(lista);
        }

        // ============================================================
        // 🔍 FILTRO EN TIEMPO REAL (solo Admin/Consultorio)
        // ============================================================
        [HttpGet]
        public async Task<IActionResult> Buscar(string term)
        {
            if (!EsAdministrativo() && !EsConsultorio())
                return Unauthorized();

            var personas = _context.EnfPersonas.Where(p => p.Activo);

            if (!string.IsNullOrEmpty(term))
            {
                personas = personas.Where(p =>
                    p.Nombre.Contains(term) || p.Cedula.Contains(term));
            }

            var lista = await personas.OrderByDescending(p => p.Id).ToListAsync();

            ViewBag.EsAdmin = EsAdministrativo();
            ViewBag.EsConsultorio = EsConsultorio();

            return PartialView("_PersonasFilas", lista);
        }

        // ============================================================
        // ❌ CRUD COMPLETO — SOLO ADMINISTRATIVO
        // ============================================================

        // 🔁 INACTIVOS
        public async Task<IActionResult> Inactivos()
        {
            if (!EsAdministrativo())
                return RedirectToAction("AccesoDenegado", "Home");

            var inactivos = await _context.EnfPersonas
                .Where(p => !p.Activo)
                .OrderBy(p => p.Nombre)
                .ToListAsync();

            return View(inactivos);
        }

        // ↩️ REACTIVAR
        [HttpPost]
        public async Task<IActionResult> Reactivar(int id)
        {
            if (!EsAdministrativo())
                return RedirectToAction("AccesoDenegado", "Home");

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
        public async Task<IActionResult> Details(int? id)
        {
            if (!EsAdministrativo())
                return RedirectToAction("AccesoDenegado", "Home");

            if (id == null) return NotFound();

            var persona = await _context.EnfPersonas.FirstOrDefaultAsync(p => p.Id == id);
            if (persona == null) return NotFound();

            return View(persona);
        }

        // ➕ CREAR
        [HttpGet]
        public IActionResult Create()
        {
            if (!EsAdministrativo())
                return RedirectToAction("AccesoDenegado", "Home");

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(EnfPersona model)
        {
            if (!EsAdministrativo())
                return RedirectToAction("AccesoDenegado", "Home");

            if (!ModelState.IsValid)
                return View(model);

            // Validaciones
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

            TempData["Mensaje"] = "Persona registrada correctamente.";
            return RedirectToAction(nameof(Index));
        }

        // ✏️ EDITAR
        public async Task<IActionResult> Edit(int? id)
        {
            if (!EsAdministrativo())
                return RedirectToAction("AccesoDenegado", "Home");

            if (id == null) return NotFound();

            var persona = await _context.EnfPersonas.FindAsync(id);
            if (persona == null) return NotFound();

            return View(persona);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, EnfPersona persona)
        {
            if (!EsAdministrativo())
                return RedirectToAction("AccesoDenegado", "Home");

            if (id != persona.Id) return NotFound();

            if (!ModelState.IsValid)
                return View(persona);

            _context.Update(persona);
            await _context.SaveChangesAsync();

            TempData["Mensaje"] = "Cambios guardados correctamente.";
            return RedirectToAction(nameof(Index));
        }

        // ❌ DESACTIVAR
        public async Task<IActionResult> Delete(int? id)
        {
            if (!EsAdministrativo())
                return RedirectToAction("AccesoDenegado", "Home");

            if (id == null) return NotFound();

            var persona = await _context.EnfPersonas.FirstOrDefaultAsync(m => m.Id == id);
            if (persona == null) return NotFound();

            return View(persona);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            if (!EsAdministrativo())
                return RedirectToAction("AccesoDenegado", "Home");

            var persona = await _context.EnfPersonas.FindAsync(id);
            if (persona != null)
            {
                persona.Activo = false;
                _context.Update(persona);
                await _context.SaveChangesAsync();
            }

            TempData["Mensaje"] = "Persona desactivada correctamente.";
            return RedirectToAction(nameof(Index));
        }

        // ====================================================================
        // 🚨 CREAR CITA DE EMERGENCIA (Consultorio + Administrativo)
        // ====================================================================
        [HttpPost]
        public async Task<IActionResult> CitaEmergencia(int id)
        {
            if (!EsAdministrativo() && !EsConsultorio())
                return Json(new { success = false, message = "Permiso denegado." });

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
                    UsuarioCreacion = User.Identity?.Name ?? "Sistema"
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
                UsuarioCreacion = User.Identity?.Name ?? "Sistema"
            };

            _context.EnfCitas.Add(cita);
            await _context.SaveChangesAsync();

            return Json(new
            {
                success = true,
                message = $"Cita de emergencia creada para {persona.Nombre}."
            });
        }
    }
}
