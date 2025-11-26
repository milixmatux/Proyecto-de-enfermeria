using Enfermeria_app.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace Enfermeria_app.Controllers
{
    [Authorize]
    public class CitasController : Controller
    {
        private readonly EnfermeriaContext _context;

        public CitasController(EnfermeriaContext context)
        {
            _context = context;
        }

        // ============================================================
        // RUTAS BÁSICAS
        // ============================================================
        public IActionResult Publicar() => View();
        public IActionResult CheckInOut() => View();
        public IActionResult Cancelar() => View();

        // ============================================================
        // Helper de roles / permisos
        // ============================================================
        bool EsEstudiante(string tipo) => string.Equals(tipo, "Estudiante", StringComparison.OrdinalIgnoreCase);
        bool EsFuncionario(string tipo) => string.Equals(tipo, "Funcionario", StringComparison.OrdinalIgnoreCase);
        bool EsProfesor(string tipo) => string.Equals(tipo, "Profesor", StringComparison.OrdinalIgnoreCase);
        bool EsConsultorio(string tipo) => string.Equals(tipo, "Consultorio", StringComparison.OrdinalIgnoreCase);
        bool EsAdministrativo(string tipo) => string.Equals(tipo, "Administrativo", StringComparison.OrdinalIgnoreCase);

        // Quién puede sacar cita normal para sí mismo
        bool PuedeSacarNormalesParaElMismo(string tipo)
            => EsEstudiante(tipo) || EsFuncionario(tipo) || EsProfesor(tipo) || EsConsultorio(tipo) || EsAdministrativo(tipo);

        // Quién puede crear emergencias para terceros
        bool PuedeSacarEmergencias(string tipo)
            => EsProfesor(tipo) || EsConsultorio(tipo) || EsAdministrativo(tipo);

        // ============================================================
        // GET: Sacar (mostrar horarios disponibles)
        // - Estudiantes: sólo hoy
        // - Si la fecha es hoy, se ocultan horarios ya pasados
        // ============================================================
        [Authorize(Policy = "EstudianteFuncionario")] // mantiene compatibilidad; otros perfiles también entran por sesión
        [HttpGet]
        public async Task<IActionResult> Sacar(DateOnly? fecha = null)
        {
            var username = User.Identity?.Name;
            var persona = await _context.EnfPersonas.FirstOrDefaultAsync(p => p.Usuario == username);
            if (persona == null) return RedirectToAction("Login", "Cuenta");

            var tipo = persona.Tipo ?? "";
            var hoy = DateOnly.FromDateTime(DateTime.Today);
            var dia = fecha ?? hoy;

            // Estudiante sólo puede reservar para hoy
            if (EsEstudiante(tipo))
                dia = hoy;

            // Hora actual para filtrar horarios de hoy
            var horaActual = TimeOnly.FromDateTime(DateTime.Now);

            var horariosQuery = _context.EnfHorarios
                .Where(h => h.Fecha == dia)
                .Select(h => new
                {
                    Horario = h,
                    Cupos = h.EnfCita.Count(c => c.Estado == "Creada" && c.IdPersona == null)
                })
                .Where(x => x.Cupos > 0)
                .OrderBy(x => x.Horario.Hora)
                .AsQueryable();

            // Si la fecha es hoy, mostrar solo horarios iguales o mayores a la hora actual
            if (dia == hoy)
                horariosQuery = horariosQuery.Where(x => x.Horario.Hora >= horaActual);

            var horarios = await horariosQuery
                .Select(x => x.Horario)
                .AsNoTracking()
                .ToListAsync();

            ViewBag.Fecha = dia;
            ViewBag.Horarios = horarios;
            ViewBag.TipoUsuario = tipo;
            ViewBag.FechaSoloHoy = EsEstudiante(tipo);

            return View();
        }

        // ============================================================
        // POST: Sacar (reservar para sí mismo)
        // - Valida permisos para reservar para sí mismo
        // - Estudiantes: sólo hoy y 1 cita por día
        // ============================================================
        [Authorize(Policy = "EstudianteFuncionario")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Sacar(int horarioId)
        {
            var username = User.Identity?.Name;
            var persona = await _context.EnfPersonas.FirstOrDefaultAsync(p => p.Usuario == username);
            if (persona == null) return RedirectToAction("Login", "Cuenta");

            var tipo = persona.Tipo ?? "";

            if (!PuedeSacarNormalesParaElMismo(tipo))
                return Unauthorized();

            var horario = await _context.EnfHorarios
                .Include(h => h.EnfCita)
                .FirstOrDefaultAsync(h => h.Id == horarioId);

            if (horario == null)
            {
                TempData["Error"] = "El horario no existe.";
                return RedirectToAction(nameof(Sacar));
            }

            var hoy = DateOnly.FromDateTime(DateTime.Today);

            if (EsEstudiante(tipo) && horario.Fecha != hoy)
            {
                TempData["Error"] = "Como estudiante, solo puedes sacar citas para hoy.";
                return RedirectToAction(nameof(Sacar), new { fecha = hoy });
            }

            if (EsEstudiante(tipo))
            {
                var yaTiene = await _context.EnfCitas
                    .Include(c => c.IdHorarioNavigation)
                    .AnyAsync(c =>
                        c.IdPersona == persona.Id &&
                        c.IdHorarioNavigation.Fecha == hoy &&
                        c.Estado == "Creada");

                if (yaTiene)
                {
                    TempData["Error"] = "Ya tienes una cita para hoy.";
                    return RedirectToAction(nameof(Sacar), new { fecha = hoy });
                }
            }

            var cupo = horario.EnfCita.FirstOrDefault(c => c.Estado == "Creada" && c.IdPersona == null);
            if (cupo == null)
            {
                TempData["Error"] = "El horario ya no tiene cupos disponibles.";
                return RedirectToAction(nameof(Sacar), new { fecha = horario.Fecha });
            }

            cupo.IdPersona = persona.Id;
            cupo.FechaCreacion = DateTime.Now;
            cupo.UsuarioCreacion = username;

            await _context.SaveChangesAsync();

            TempData["Mensaje"] = "Cita creada correctamente.";
            return RedirectToAction(nameof(Sacar), new { fecha = horario.Fecha });
        }

        // ============================================================
        // Emergencia (GET/POST)
        // - Permite crear cita para otra persona (Profesor, Consultorio, Administrativo)
        // - Si no hay bloque disponible se crea el siguiente bloque (como antes)
        // ============================================================
        [HttpGet]
        public IActionResult Emergencia()
        {
            var username = User.Identity?.Name;
            var user = _context.EnfPersonas.FirstOrDefault(p => p.Usuario == username);

            if (user == null || !PuedeSacarEmergencias(user.Tipo))
                return RedirectToAction("AccesoDenegado", "Home");

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Emergencia(string cedula)
        {
            var username = User.Identity?.Name;
            var personaActual = await _context.EnfPersonas.FirstOrDefaultAsync(x => x.Usuario == username);

            if (personaActual == null || !PuedeSacarEmergencias(personaActual.Tipo))
                return Unauthorized();

            var paciente = await _context.EnfPersonas.FirstOrDefaultAsync(p => p.Cedula == cedula);
            if (paciente == null)
            {
                TempData["Error"] = "No se encontró la persona.";
                return View();
            }

            var hoy = DateOnly.FromDateTime(DateTime.Today);
            var ahora = TimeOnly.FromDateTime(DateTime.Now);

            // Buscar horario con cupo desde ahora en adelante
            var horario = await _context.EnfHorarios
                .Where(h => h.Fecha == hoy && h.Hora >= ahora)
                .Include(h => h.EnfCita)
                .OrderBy(h => h.Hora)
                .FirstOrDefaultAsync(h => h.EnfCita.Any(c => c.Estado == "Creada" && c.IdPersona == null));

            // Si no hay, buscar cualquier horario hoy con cupo
            if (horario == null)
            {
                horario = await _context.EnfHorarios
                    .Where(h => h.Fecha == hoy)
                    .Include(h => h.EnfCita)
                    .OrderBy(h => h.Hora)
                    .FirstOrDefaultAsync(h => h.EnfCita.Any(c => c.Estado == "Creada" && c.IdPersona == null));
            }

            // Si aún no existe horario con cupo, crear uno nuevo al próximo múltiplo de 5 min
            if (horario == null)
            {
                var dt = DateTime.Now;
                var add = (5 - (dt.Minute % 5)) % 5;
                var dtNext = dt.AddMinutes(add == 0 ? 5 : add);
                var nextTime = new TimeOnly(dtNext.Hour, dtNext.Minute);

                horario = new EnfHorario
                {
                    Fecha = hoy,
                    Hora = nextTime,
                    Estado = "Activo",
                    FechaCreacion = DateTime.Now,
                    UsuarioCreacion = username
                };

                _context.EnfHorarios.Add(horario);
                await _context.SaveChangesAsync();

                var cupoNuevo = new EnfCita
                {
                    IdHorario = horario.Id,
                    IdPersona = null,
                    Estado = "Creada",
                    FechaCreacion = DateTime.Now,
                    UsuarioCreacion = username
                };
                _context.EnfCitas.Add(cupoNuevo);
                await _context.SaveChangesAsync();

                horario = await _context.EnfHorarios
                    .Include(h => h.EnfCita)
                    .FirstAsync(h => h.Id == horario.Id);
            }

            var cupo = horario.EnfCita.FirstOrDefault(c => c.Estado == "Creada" && c.IdPersona == null);
            if (cupo == null)
            {
                TempData["Error"] = "Bloque no disponible.";
                return View();
            }

            cupo.IdPersona = paciente.Id;
            cupo.FechaCreacion = DateTime.Now;
            cupo.UsuarioCreacion = username;

            await _context.SaveChangesAsync();

            TempData["Mensaje"] = $"Cita de emergencia creada para {paciente.Nombre}.";
            return View();
        }

        // ============================================================
        // BuscarEstudiantes (GET JSON) — utilizado por modal/emergencia
        // - Disponible para Profesor, Consultorio y Administrativo
        // ============================================================
        [HttpGet]
        [Produces("application/json")]
        public async Task<IActionResult> BuscarEstudiantes(string q)
        {
            var username = User.Identity?.Name;
            var usuario = await _context.EnfPersonas.FirstOrDefaultAsync(x => x.Usuario == username);

            if (usuario == null || !PuedeSacarEmergencias(usuario.Tipo))
                return Unauthorized();

            q = (q ?? "").Trim();
            if (q.Length < 1) return Ok(Array.Empty<object>());

            var like = $"%{q}%";

            var results = await _context.EnfPersonas
                .Where(p =>
                    p.Tipo != null &&
                    p.Tipo.Equals("Estudiante", StringComparison.OrdinalIgnoreCase) &&
                    (
                        (!string.IsNullOrEmpty(p.Nombre) && EF.Functions.Like(p.Nombre, like)) ||
                        (!string.IsNullOrEmpty(p.Cedula) && EF.Functions.Like(p.Cedula, like))
                    ))
                .OrderBy(p => p.Nombre)
                .Select(p => new { cedula = p.Cedula, nombre = p.Nombre })
                .Take(10)
                .AsNoTracking()
                .ToListAsync();

            return Ok(results);
        }

        // ============================================================
        // Historial (mi historial de citas)
        // ============================================================
        public async Task<IActionResult> Historial()
        {
            var username = User.Identity?.Name;
            var persona = await _context.EnfPersonas.FirstOrDefaultAsync(p => p.Usuario == username);
            if (persona == null) return RedirectToAction("Login", "Cuenta");

            var citas = await _context.EnfCitas
                .Where(c => c.IdPersona == persona.Id)
                .Include(c => c.IdHorarioNavigation)
                .OrderByDescending(c => c.FechaCreacion)
                .AsNoTracking()
                .ToListAsync();

            return View("Historial", citas);
        }

        // ============================================================
        // Cancelar cita (POST)
        // - Sólo Estudiante/Funcionario pueden cancelar su cita del día si está "Creada"
        // ============================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CancelarCita(int id)
        {
            var username = User.Identity?.Name;
            var persona = await _context.EnfPersonas.FirstOrDefaultAsync(p => p.Usuario == username);
            if (persona == null) return RedirectToAction("Login", "Cuenta");

            if (!EsEstudiante(persona.Tipo) && !EsFuncionario(persona.Tipo))
                return Unauthorized();

            var cita = await _context.EnfCitas
                .Include(c => c.IdHorarioNavigation)
                .FirstOrDefaultAsync(c => c.Id == id && c.IdPersona == persona.Id);

            if (cita == null)
            {
                TempData["Error"] = "No se encontró la cita.";
                return RedirectToAction(nameof(Historial));
            }

            var hoy = DateOnly.FromDateTime(DateTime.Today);
            if (cita.IdHorarioNavigation == null || cita.IdHorarioNavigation.Fecha != hoy || cita.Estado != "Creada")
            {
                TempData["Error"] = "Solo puedes cancelar citas del día actual que estén activas.";
                return RedirectToAction(nameof(Historial));
            }

            cita.Estado = "Cancelada";
            cita.FechaModificacion = DateTime.Now;
            cita.UsuarioModificacion = username;

            await _context.SaveChangesAsync();

            TempData["Mensaje"] = "Tu cita fue cancelada correctamente.";
            return RedirectToAction(nameof(Historial));
        }

        // ============================================================
        // Estudiante_Historial (para PROFESOR y ADMINISTRATIVO)
        // ============================================================
        public async Task<IActionResult> Estudiante_Historial(string filtro = null)
        {
            var username = User.Identity?.Name;
            var persona = await _context.EnfPersonas.FirstOrDefaultAsync(x => x.Usuario == username);

            if (persona == null || (!EsProfesor(persona.Tipo) && !EsAdministrativo(persona.Tipo)))
                return Unauthorized();

            var citas = _context.EnfCitas
                .Include(c => c.IdPersonaNavigation)
                .Include(c => c.IdHorarioNavigation)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(filtro))
            {
                citas = citas.Where(c =>
                    c.IdPersonaNavigation != null &&
                    c.IdPersonaNavigation.Tipo != null &&
                    c.IdPersonaNavigation.Tipo.Equals("Estudiante", StringComparison.OrdinalIgnoreCase) &&
                    (
                        (c.IdPersonaNavigation.Nombre != null && c.IdPersonaNavigation.Nombre.Contains(filtro)) ||
                        (c.IdPersonaNavigation.Cedula != null && c.IdPersonaNavigation.Cedula.Contains(filtro))
                    )
                );
            }
            else
            {
                // Si no hay filtro, devolver vacío (evita listar por defecto)
                citas = citas.Take(0);
            }

            var lista = await citas.OrderByDescending(c => c.FechaCreacion).AsNoTracking().ToListAsync();
            return View("Estudiante_Historial", lista);
        }

        // ============================================================
        // Helper: detectar AJAX
        // ============================================================
        bool EsAjax() =>
            Request.Headers.TryGetValue("X-Requested-With", out var v) &&
            v == "XMLHttpRequest";
    }
}
