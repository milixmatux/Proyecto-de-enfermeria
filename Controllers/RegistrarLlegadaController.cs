using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Enfermeria_app.Models;

namespace Enfermeria_app.Controllers
{
    public class RegistrarLlegadaController : Controller
    {
        private readonly EnfermeriaContext _context;

        public RegistrarLlegadaController(EnfermeriaContext context)
        {
            _context = context;
        }

        bool EsAsistente() => User?.Claims?.Any(c => c.Type == "TipoUsuario" && c.Value == "Asistente") == true;

        string NormalizarCR(string? tel)
        {
            if (string.IsNullOrWhiteSpace(tel)) return "";
            var digits = new string(tel.Where(char.IsDigit).ToArray());
            if (digits.StartsWith("506")) digits = digits.Substring(3);
            if (digits.StartsWith("0")) digits = digits.TrimStart('0');
            return $"+506{digits}";
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            if (!EsAsistente()) return RedirectToAction("AccesoDenegado", "Cuenta");

            var hoy = DateOnly.FromDateTime(DateTime.Today);

            var citas = await _context.EnfCitas
                .Include(c => c.IdPersonaNavigation)
                .Include(c => c.IdHorarioNavigation)
                .Where(c => c.IdHorarioNavigation != null && c.IdHorarioNavigation.Fecha == hoy)
                .OrderBy(c => c.IdHorarioNavigation!.Hora)
                .AsNoTracking()
                .ToListAsync();

            var profesores = await _context.EnfPersonas
                .Where(p => p.Tipo == "Profesor" && p.Activo && !string.IsNullOrWhiteSpace(p.Telefono))
                .OrderBy(p => p.Nombre)
                .Select(p => new { p.Id, p.Nombre, p.Telefono })
                .AsNoTracking()
                .ToListAsync();

            ViewBag.Profesores = profesores;
            return View("Index", citas);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> LlegadaAjax(int id, string? mensaje, int idProfesor)
        {
            if (!EsAsistente()) return Json(new { ok = false, msg = "Acceso denegado." });

            var cita = await _context.EnfCitas
                .Include(c => c.IdPersonaNavigation)
                .Include(c => c.IdHorarioNavigation)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (cita == null) return Json(new { ok = false, msg = "Cita no encontrada." });

            var profesor = await _context.EnfPersonas.FirstOrDefaultAsync(p => p.Id == idProfesor && p.Tipo == "Profesor");
            if (profesor == null || string.IsNullOrWhiteSpace(profesor.Telefono))
                return Json(new { ok = false, msg = "Profesor inválido o sin teléfono." });

            if (cita.HoraLlegada != null)
                return Json(new { ok = true, yaRegistrado = true, hora = cita.HoraLlegada?.ToString("HH:mm"), waUrl = "" });

            var ahora = TimeOnly.FromDateTime(DateTime.Now);
            cita.HoraLlegada = ahora;
            cita.MensajeLlegada = mensaje ?? "";
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

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SalidaAjax(int id, string mensaje, int idProfesor)
        {
            if (!EsAsistente()) return Json(new { ok = false, msg = "Acceso denegado." });

            var cita = await _context.EnfCitas
                .Include(c => c.IdPersonaNavigation)
                .Include(c => c.IdHorarioNavigation)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (cita == null) return Json(new { ok = false, msg = "Cita no encontrada." });

            var profesor = await _context.EnfPersonas.FirstOrDefaultAsync(p => p.Id == idProfesor && p.Tipo == "Profesor");
            if (profesor == null || string.IsNullOrWhiteSpace(profesor.Telefono))
                return Json(new { ok = false, msg = "Profesor inválido o sin teléfono." });

            if (string.IsNullOrWhiteSpace(mensaje))
                return Json(new { ok = false, msg = "El diagnóstico es obligatorio." });

            var llegadaDb = await _context.EnfCitas
    .Where(c => c.Id == id)
    .Select(c => new { c.HoraLlegada }) // Wrap the nullable value type in an anonymous object
    .AsNoTracking() // Now `AsNoTracking` is valid because the query returns a reference type (anonymous object)
    .FirstOrDefaultAsync();

            if (llegadaDb == null || llegadaDb.HoraLlegada == null) // Access the `HoraLlegada` property from the anonymous object
            {
                var fix = TimeOnly.FromDateTime(DateTime.Now);
                cita.HoraLlegada = fix;
                if (string.IsNullOrWhiteSpace(cita.MensajeLlegada)) cita.MensajeLlegada = "(auto)";
            }

            if (cita.HoraSalida != null)
                return Json(new { ok = true, yaRegistrado = true, hora = cita.HoraSalida?.ToString("HH:mm"), waUrl = "" });

            var ahora = TimeOnly.FromDateTime(DateTime.Now);
            cita.HoraSalida = ahora;
            cita.MensajeSalida = mensaje;
            _context.EnfCitas.Update(cita);
            await _context.SaveChangesAsync();

            var tel = NormalizarCR(profesor.Telefono);
            var nom = cita.IdPersonaNavigation?.Nombre ?? "estudiante";
            var sec = cita.IdPersonaNavigation?.Seccion ?? "";
            var texto = Uri.EscapeDataString($"{nom}, estudiante de {sec}, ha salido de la enfermería a las {ahora:HH\\:mm}. Diagnóstico: {mensaje}");
            var url = $"https://wa.me/{tel}?text={texto}";

            return Json(new { ok = true, hora = ahora.ToString("HH:mm"), waUrl = url });
        }
    }
}
