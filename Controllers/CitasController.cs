using Enfermeria_app.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;

namespace Enfermeria_app.Controllers
{
    public class CitasController : Controller
    {
        public IActionResult Publicar() => View();
        public IActionResult CheckInOut() => View();
        public IActionResult Cancelar() => View();

        [Authorize(Policy = "EstudianteFuncionario")]
        [HttpGet]
        public async Task<IActionResult> Sacar(DateOnly? fecha = null)
        {
            var username = User.Identity?.Name;
            var persona = await _context.EnfPersonas.FirstOrDefaultAsync(p => p.Usuario == username);
            if (persona == null) return RedirectToAction("Login", "Cuenta");

            var hoy = DateOnly.FromDateTime(DateTime.Today);
            var dia = persona.Tipo == "Estudiante" ? hoy : (fecha ?? hoy);

            var horarios = await _context.EnfHorarios
                .Where(h => h.Fecha == dia)
                .Select(h => new
                {
                    Horario = h,
                    Cupos = h.EnfCita.Count(c => c.Estado == "Creada" && c.IdPersona == null)
                })
                .Where(x => x.Cupos > 0)
                .OrderBy(x => x.Horario.Hora)
                .Select(x => x.Horario)
                .AsNoTracking()
                .ToListAsync();

            ViewBag.Fecha = dia;
            ViewBag.Horarios = horarios;
            ViewBag.TipoUsuario = persona.Tipo;
            ViewBag.FechaSoloHoy = persona.Tipo == "Estudiante";
            return View();
        }

        [Authorize(Policy = "EstudianteFuncionario")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Sacar(int horarioId)
        {
            var username = User.Identity?.Name;
            var persona = await _context.EnfPersonas.FirstOrDefaultAsync(p => p.Usuario == username);
            if (persona == null) return RedirectToAction("Login", "Cuenta");

            var horario = await _context.EnfHorarios
                .Include(h => h.EnfCita)
                .FirstOrDefaultAsync(h => h.Id == horarioId);
            if (horario == null)
            {
                TempData["Error"] = "El horario no existe.";
                return RedirectToAction(nameof(Sacar));
            }

            var hoy = DateOnly.FromDateTime(DateTime.Today);

            if (persona.Tipo == "Estudiante")
            {
                if (horario.Fecha != hoy)
                {
                    TempData["Error"] = "Como estudiante, solo puedes sacar citas para hoy.";
                    return RedirectToAction(nameof(Sacar), new { fecha = hoy });
                }

                var yaTieneCitaHoy = await _context.EnfCitas
                    .Include(c => c.IdHorarioNavigation)
                    .AnyAsync(c => c.IdPersona == persona.Id
                                   && c.IdHorarioNavigation.Fecha == hoy
                                   && c.Estado == "Creada");
                if (yaTieneCitaHoy)
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

        [Authorize(Policy = "Profesor")]
        [HttpGet]
        public async Task<IActionResult> Profesor(DateOnly? fecha = null)
        {
            var username = User.Identity?.Name;
            var persona = await _context.EnfPersonas.FirstOrDefaultAsync(p => p.Usuario == username);
            if (persona == null) return RedirectToAction("Login", "Cuenta");

            var hoy = DateOnly.FromDateTime(DateTime.Today);
            var dia = fecha ?? hoy;

            var horarios = await _context.EnfHorarios
                .Where(h => h.Fecha == dia)
                .Select(h => new
                {
                    Horario = h,
                    Cupos = h.EnfCita.Count(c => c.Estado == "Creada" && c.IdPersona == null)
                })
                .Where(x => x.Cupos > 0)
                .OrderBy(x => x.Horario.Hora)
                .Select(x => x.Horario)
                .AsNoTracking()
                .ToListAsync();

            ViewBag.Fecha = dia;
            ViewBag.Horarios = horarios;
            ViewBag.TipoUsuario = "Profesor";
            ViewBag.FechaSoloHoy = false;
            return View("Sacar");
        }

        [Authorize(Policy = "Profesor")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Profesor(int horarioId)
        {
            var username = User.Identity?.Name;
            var persona = await _context.EnfPersonas.FirstOrDefaultAsync(p => p.Usuario == username);
            if (persona == null) return RedirectToAction("Login", "Cuenta");

            var horario = await _context.EnfHorarios
                .Include(h => h.EnfCita)
                .FirstOrDefaultAsync(h => h.Id == horarioId);
            if (horario == null)
            {
                TempData["Error"] = "El horario no existe.";
                return RedirectToAction(nameof(Profesor));
            }

            var cupo = horario.EnfCita.FirstOrDefault(c => c.Estado == "Creada" && c.IdPersona == null);
            if (cupo == null)
            {
                TempData["Error"] = "El horario ya no tiene cupos disponibles.";
                return RedirectToAction(nameof(Profesor), new { fecha = horario.Fecha });
            }

            cupo.IdPersona = persona.Id;
            cupo.FechaCreacion = DateTime.Now;
            cupo.UsuarioCreacion = username;

            await _context.SaveChangesAsync();

            TempData["Mensaje"] = "Cita creada correctamente.";
            return RedirectToAction(nameof(Profesor), new { fecha = horario.Fecha });
        }

        [Authorize(Policy = "Profesor")]
        [HttpGet]
        public IActionResult Emergencia() => View();

        [Authorize(Policy = "Profesor")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Emergencia(string cedula)
        {
            var user = User.Identity?.Name;

            var persona = await _context.EnfPersonas.FirstOrDefaultAsync(p => p.Cedula == cedula);
            if (persona == null)
            {
                if (EsAjax()) return Json(new { ok = false, msg = "No se encontró la persona." });
                TempData["Error"] = "No se encontró la persona por cédula.";
                return View();
            }

            var hoy = DateOnly.FromDateTime(DateTime.Today);
            var ahora = TimeOnly.FromDateTime(DateTime.Now);

            var horario = await _context.EnfHorarios
                .Where(h => h.Fecha == hoy && h.Hora >= ahora)
                .OrderBy(h => h.Hora)
                .Include(h => h.EnfCita)
                .FirstOrDefaultAsync(h => h.EnfCita.Any(c => c.Estado == "Creada" && c.IdPersona == null));

            if (horario == null)
            {
                horario = await _context.EnfHorarios
                    .Where(h => h.Fecha == hoy)
                    .OrderBy(h => h.Hora)
                    .Include(h => h.EnfCita)
                    .FirstOrDefaultAsync(h => h.EnfCita.Any(c => c.Estado == "Creada" && c.IdPersona == null));
            }

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
                    UsuarioCreacion = user
                };
                _context.EnfHorarios.Add(horario);
                await _context.SaveChangesAsync();

                var cupoNuevo = new EnfCita
                {
                    IdHorario = horario.Id,
                    IdPersona = null,
                    Estado = "Creada",
                    FechaCreacion = DateTime.Now,
                    UsuarioCreacion = user
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
                if (EsAjax()) return Json(new { ok = false, msg = "Bloque no disponible." });
                TempData["Error"] = "El bloque no está disponible.";
                return View();
            }

            cupo.IdPersona = persona.Id;
            cupo.FechaCreacion = DateTime.Now;
            cupo.UsuarioCreacion = user;

            await _context.SaveChangesAsync();

            if (EsAjax()) return Json(new { ok = true, msg = $"Cita de emergencia creada para {persona.Nombre}.", close = true });
            TempData["Mensaje"] = "Cita de emergencia creada.";
            return View();
        }

        [Authorize(Policy = "Profesor")]
        [HttpGet]
        [Produces("application/json")]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public async Task<IActionResult> BuscarEstudiantes(string q)
        {
            q = (q ?? string.Empty).Trim();
            if (q.Length < 1) return Ok(Array.Empty<object>());

            var like = $"%{q}%";

            var resultados = await _context.EnfPersonas
                .Where(p =>
                    (p.Tipo == "Estudiante" ||
                     p.Tipo == "ESTUDIANTE" ||
                     p.Tipo == "estudiante" ||
                     p.Tipo.StartsWith("Estudiante") ||
                     p.Tipo.StartsWith("ESTUDIANTE")) &&
                    (
                        (p.Nombre != null && EF.Functions.Like(p.Nombre, like)) ||
                        (p.Cedula != null && EF.Functions.Like(p.Cedula, like))
                    )
                )
                .OrderBy(p => p.Nombre)
                .Select(p => new { cedula = p.Cedula, nombre = p.Nombre })
                .AsNoTracking()
                .Take(10)
                .ToListAsync();

            return Ok(resultados);
        }

        [Authorize]
        public async Task<IActionResult> Historial()
        {
            var username = User.Identity?.Name;
            if (string.IsNullOrEmpty(username)) return RedirectToAction("Login", "Cuenta");

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

        bool EsAjax() => Request.Headers.TryGetValue("X-Requested-With", out var v) && v == "XMLHttpRequest";

        private readonly EnfermeriaContext _context;
        public CitasController(EnfermeriaContext context) { _context = context; }
    }
}
