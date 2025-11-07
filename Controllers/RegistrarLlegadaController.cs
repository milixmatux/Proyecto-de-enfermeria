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
    [Authorize]
    public class RegistrarLlegadaController : Controller
    {
        private readonly EnfermeriaContext _context;

        public RegistrarLlegadaController(EnfermeriaContext context)
        {
            _context = context;
        }

        // ✅ Verifica si el usuario es Asistente o Doctor
        bool EsPersonalAutorizado()
        {
            var tipo = User?.Claims?.FirstOrDefault(c => c.Type == "TipoUsuario")?.Value;
            return tipo == "Asistente" || tipo == "Doctor";
        }

        // ✅ Normaliza teléfono CR
        string NormalizarCR(string? tel)
        {
            if (string.IsNullOrWhiteSpace(tel)) return "";
            var digits = new string(tel.Where(char.IsDigit).ToArray());
            if (digits.StartsWith("506")) digits = digits.Substring(3);
            if (digits.StartsWith("0")) digits = digits.TrimStart('0');
            return $"+506{digits}";
        }

        // ✅ Convierte fechas seguras
        DateOnly? ParseDate(string? v)
        {
            if (string.IsNullOrWhiteSpace(v)) return null;
            if (DateOnly.TryParse(v, out var d)) return d;
            if (DateOnly.TryParseExact(v, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out d)) return d;
            if (DateOnly.TryParseExact(v, "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out d)) return d;
            return null;
        }

        // ✅ INDEX: muestra solo citas registradas y permite filtrar por rango
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

        // ✅ AJAX - Registrar llegada
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

            var profesor = await _context.EnfPersonas.FirstOrDefaultAsync(p => p.Id == idProfesor && p.Tipo == "Profesor");
            if (profesor == null || string.IsNullOrWhiteSpace(profesor.Telefono))
                return Json(new { ok = false, msg = "Profesor inválido o sin teléfono." });

            if (cita.HoraLlegada != null)
                return Json(new { ok = true, yaRegistrado = true, hora = cita.HoraLlegada?.ToString("HH:mm"), waUrl = "" });

            var ahora = TimeOnly.FromDateTime(DateTime.Now);
            cita.HoraLlegada = ahora;
            cita.MensajeLlegada = mensaje ?? "";
            cita.Estado = "Llegada"; // 👈 Actualizamos el estado
            _context.EnfCitas.Update(cita);
            await _context.SaveChangesAsync();

            var tel = NormalizarCR(profesor.Telefono);
            var obs = string.IsNullOrWhiteSpace(mensaje) ? "ninguna" : mensaje;
            var nom = cita.IdPersonaNavigation?.Nombre ?? "estudiante";
            var sec = cita.IdPersonaNavigation?.Seccion ?? "";
            var texto = Uri.EscapeDataString($"{nom}, estudiante de {sec}, ha llegado a la enfermería a las {ahora:HH\\:mm}. Observaciones: {obs}");
            var url = $"https://wa.me/{tel}?text={texto}";

            return Json(new { ok = true, hora = ahora.ToString("HH:mm"), waUrl = url });
        }

        // ✅ AJAX - Registrar salida
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

            var profesor = await _context.EnfPersonas.FirstOrDefaultAsync(p => p.Id == idProfesor && p.Tipo == "Profesor");
            if (profesor == null || string.IsNullOrWhiteSpace(profesor.Telefono))
                return Json(new { ok = false, msg = "Profesor inválido o sin teléfono." });

            if (string.IsNullOrWhiteSpace(mensaje))
                return Json(new { ok = false, msg = "El motivo es obligatorio." });

            // si no tiene hora de llegada, se registra automáticamente
            if (cita.HoraLlegada == null)
            {
                cita.HoraLlegada = TimeOnly.FromDateTime(DateTime.Now);
                if (string.IsNullOrWhiteSpace(cita.MensajeLlegada))
                    cita.MensajeLlegada = "(auto)";
            }

            if (cita.HoraSalida != null)
                return Json(new { ok = true, yaRegistrado = true, hora = cita.HoraSalida?.ToString("HH:mm"), waUrl = "" });

            var ahora = TimeOnly.FromDateTime(DateTime.Now);
            cita.HoraSalida = ahora;
            cita.MensajeSalida = mensaje;
            cita.Estado = "Completada"; // 👈 Actualizamos el estado
            _context.EnfCitas.Update(cita);
            await _context.SaveChangesAsync();

            var tel = NormalizarCR(profesor.Telefono);
            var nom = cita.IdPersonaNavigation?.Nombre ?? "estudiante";
            var sec = cita.IdPersonaNavigation?.Seccion ?? "";
            var texto = Uri.EscapeDataString($"{nom}, estudiante de {sec}, ha salido de la enfermería a las {ahora:HH\\:mm}. Motivo: {mensaje}");
            var url = $"https://wa.me/{tel}?text={texto}";

            return Json(new { ok = true, hora = ahora.ToString("HH:mm"), waUrl = url });
        }

    }
}
