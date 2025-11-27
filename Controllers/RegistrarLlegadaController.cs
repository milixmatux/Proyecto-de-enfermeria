using Enfermeria_app.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace Enfermeria_app.Controllers
{
    [Authorize(Policy = "Consultorio")]
    public class RegistrarLlegadaController : Controller
    {
        private readonly EnfermeriaContext _context;

        public RegistrarLlegadaController(EnfermeriaContext context)
        {
            _context = context;
        }

        // ============================
        // 🔐 Nuevo método de permisos
        // ============================
        bool EsPersonalAutorizado()
        {
            var tipo = User?.Claims?.FirstOrDefault(c => c.Type == "TipoUsuario")?.Value?.Trim().ToLower();

            // Solo CONSULTORIO puede usar este módulo
            return tipo == "consultorio";
        }

        // ============================
        // Normalizador de teléfono
        // ============================
        string NormalizarCR(string? tel)
        {
            if (string.IsNullOrWhiteSpace(tel)) return "";
            var digits = new string(tel.Where(char.IsDigit).ToArray());
            if (digits.StartsWith("506")) digits = digits.Substring(3);
            if (digits.StartsWith("0")) digits = digits.TrimStart('0');
            return $"+506{digits}";
        }

        DateOnly? ParseDate(string? v)
        {
            if (string.IsNullOrWhiteSpace(v)) return null;
            if (DateOnly.TryParse(v, out var d)) return d;
            if (DateOnly.TryParseExact(v, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out d)) return d;
            if (DateOnly.TryParseExact(v, "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out d)) return d;
            return null;
        }

        // ============================
        // 📌 INDEX
        // ============================
        [HttpGet]
        public async Task<IActionResult> Index(string? desde = null, string? hasta = null)
        {
            if (!EsPersonalAutorizado())
                return RedirectToAction("AccesoDenegado", "Cuenta");

            var d1 = ParseDate(desde) ?? DateOnly.FromDateTime(DateTime.Today);
            var d2 = ParseDate(hasta) ?? DateOnly.FromDateTime(DateTime.Today);

            var citas = await _context.EnfCitas
                .Include(c => c.IdPersonaNavigation)
                .Include(c => c.IdHorarioNavigation)
                .Where(c =>
                    c.IdHorarioNavigation != null &&
                    c.IdHorarioNavigation.Fecha >= d1 &&
                    c.IdHorarioNavigation.Fecha <= d2 &&
                    c.IdPersonaNavigation != null &&
                    !string.IsNullOrWhiteSpace(c.IdPersonaNavigation.Nombre)
                )
                .OrderBy(c => c.IdHorarioNavigation!.Fecha)
                .ThenBy(c => c.IdHorarioNavigation!.Hora)
                .AsNoTracking()
                .ToListAsync();

            var profesores = await _context.EnfPersonas
                .Where(p => p.Tipo == "Profesor" && p.Activo && !string.IsNullOrWhiteSpace(p.Telefono))
                .OrderBy(p => p.Nombre)
                .Select(p => new { p.Id, p.Nombre, p.Telefono })
                .AsNoTracking()
                .ToListAsync();

            ViewBag.Profesores = profesores;
            ViewBag.Desde = d1.ToDateTime(TimeOnly.MinValue).ToString("yyyy-MM-dd");
            ViewBag.Hasta = d2.ToDateTime(TimeOnly.MinValue).ToString("yyyy-MM-dd");

            return View("Index", citas);
        }

        // ============================
        // 📌 Registrar llegada (AJAX)
        // ============================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> LlegadaAjax(int id, string? mensaje, int idProfesor)
        {
            if (!EsPersonalAutorizado())
                return Json(new { ok = false, msg = "Acceso denegado." });

            var cita = await _context.EnfCitas
                .Include(c => c.IdPersonaNavigation)
                .Include(c => c.IdHorarioNavigation)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (cita == null)
                return Json(new { ok = false, msg = "Cita no encontrada." });

            var tipoPaciente = cita.IdPersonaNavigation?.Tipo?.Trim();

            bool pacienteEsEstudiante = tipoPaciente == "Estudiante";

            // ✔ SOLO estudiantes requieren profesor
            EnfPersona? profesor = null;

            if (pacienteEsEstudiante)
            {
                profesor = await _context.EnfPersonas
                    .FirstOrDefaultAsync(p => p.Id == idProfesor && p.Tipo == "Profesor");

                if (profesor == null || string.IsNullOrWhiteSpace(profesor.Telefono))
                    return Json(new { ok = false, msg = "Selecciona un profesor válido." });
            }

            if (cita.HoraLlegada != null)
                return Json(new { ok = true, yaRegistrado = true, hora = cita.HoraLlegada?.ToString("HH:mm") });

            var ahora = TimeOnly.FromDateTime(DateTime.Now);
            cita.HoraLlegada = ahora;
            cita.MensajeLlegada = mensaje ?? "";
            cita.Estado = "Llegada";

            _context.EnfCitas.Update(cita);
            await _context.SaveChangesAsync();

            string waUrl = "";

            // ✔ SOLO estudiantes reciben WhatsApp
            if (pacienteEsEstudiante)
            {
                var tel = NormalizarCR(profesor!.Telefono);
                var texto = Uri.EscapeDataString($"{cita.IdPersonaNavigation?.Nombre} ha llegado a la enfermería a las {ahora:HH:mm}. Observaciones: {(mensaje ?? "ninguna")}");
                waUrl = $"https://wa.me/{tel}?text={texto}";
            }

            return Json(new { ok = true, hora = ahora.ToString("HH:mm"), waUrl });
        }

        // ============================
        // 📌 Registrar salida (AJAX)
        // ============================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SalidaAjax(int id, string mensaje, int idProfesor)
        {
            if (!EsPersonalAutorizado())
                return Json(new { ok = false, msg = "Acceso denegado." });

            var cita = await _context.EnfCitas
                .Include(c => c.IdPersonaNavigation)
                .Include(c => c.IdHorarioNavigation)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (cita == null)
                return Json(new { ok = false, msg = "Cita no encontrada." });

            var tipoPaciente = cita.IdPersonaNavigation?.Tipo?.Trim();
            bool pacienteEsEstudiante = tipoPaciente == "Estudiante";

            EnfPersona? profesor = null;

            // ✔ SOLO estudiantes requieren profesor
            if (pacienteEsEstudiante)
            {
                profesor = await _context.EnfPersonas
                    .FirstOrDefaultAsync(p => p.Id == idProfesor && p.Tipo == "Profesor");

                if (profesor == null || string.IsNullOrWhiteSpace(profesor.Telefono))
                    return Json(new { ok = false, msg = "Selecciona un profesor válido." });
            }

            if (string.IsNullOrWhiteSpace(mensaje))
                return Json(new { ok = false, msg = "El motivo es obligatorio." });

            if (cita.HoraLlegada == null)
            {
                cita.HoraLlegada = TimeOnly.FromDateTime(DateTime.Now);
                cita.MensajeLlegada = "(auto)";
            }

            if (cita.HoraSalida != null)
                return Json(new { ok = true, yaRegistrado = true, hora = cita.HoraSalida?.ToString("HH:mm") });

            var ahora = TimeOnly.FromDateTime(DateTime.Now);
            cita.HoraSalida = ahora;
            cita.MensajeSalida = mensaje;
            cita.Estado = "Completada";

            _context.EnfCitas.Update(cita);
            await _context.SaveChangesAsync();

            string waUrl = "";

            // ✔ SOLO estudiantes reciben WhatsApp
            if (pacienteEsEstudiante)
            {
                var tel = NormalizarCR(profesor!.Telefono);
                var texto = Uri.EscapeDataString($"{cita.IdPersonaNavigation?.Nombre} ha salido de la enfermería a las {ahora:HH:mm}. Motivo: {mensaje}");
                waUrl = $"https://wa.me/{tel}?text={texto}";
            }

            return Json(new { ok = true, hora = ahora.ToString("HH:mm"), waUrl });
        }

    }
}
