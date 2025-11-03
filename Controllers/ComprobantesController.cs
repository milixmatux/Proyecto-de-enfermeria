using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Enfermeria_app.Models;
using Enfermeria_app.Services;

namespace Enfermeria_app.Controllers
{
    [Authorize]
    public class ComprobantesController : Controller
    {
        private readonly EnfermeriaContext _context;
        private readonly ComprobantePdfService _pdf;

        public ComprobantesController(EnfermeriaContext context)
        {
            _context = context;
            _pdf = new ComprobantePdfService(); // ✅ ahora sin parámetros
        }

        bool EsPersonalAutorizado()
        {
            var tipo = User?.Claims?.FirstOrDefault(c => c.Type == "TipoUsuario")?.Value;
            return tipo == "Asistente" || tipo == "Doctor";
        }
        string? Usuario() => User?.Identity?.Name;

        DateOnly? ParseDate(string? v)
        {
            if (string.IsNullOrWhiteSpace(v)) return null;
            if (DateOnly.TryParse(v, out var d)) return d;
            if (DateOnly.TryParseExact(v, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out d)) return d;
            if (DateOnly.TryParseExact(v, "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out d)) return d;
            return null;
        }

        [HttpGet]
        public async Task<IActionResult> Index(string? desde = null, string? hasta = null)
        {
            var d1 = ParseDate(desde) ?? DateOnly.FromDateTime(DateTime.Today);
            var d2 = ParseDate(hasta) ?? DateOnly.FromDateTime(DateTime.Today);

            var q = _context.EnfCitas
                .Include(c => c.IdPersonaNavigation)
                .Include(c => c.IdHorarioNavigation)
                .AsQueryable();

            if (!EsPersonalAutorizado())
            {
                var u = Usuario();
                var persona = await _context.EnfPersonas.FirstOrDefaultAsync(p => p.Usuario == u);
                if (persona != null)
                    q = q.Where(c => c.IdPersona == persona.Id);
                else
                    q = q.Where(c => false);
            }


            q = q.Where(c => c.IdHorarioNavigation != null &&
                 c.IdHorarioNavigation.Fecha >= d1 &&
                 c.IdHorarioNavigation.Fecha <= d2 &&
                 c.IdPersonaNavigation != null &&                // tiene persona asignada
                 !string.IsNullOrEmpty(c.IdPersonaNavigation.Nombre) && // el nombre no está vacío
                 (c.Estado != null && c.Estado != "" && c.Estado != "Pendiente")); // y tiene estado válido

            var data = await q.OrderBy(c => c.IdHorarioNavigation!.Fecha)
                              .ThenBy(c => c.IdHorarioNavigation!.Hora)
                              .AsNoTracking()
                              .ToListAsync();

            ViewBag.Desde = d1.ToDateTime(TimeOnly.MinValue).ToString("yyyy-MM-dd");
            ViewBag.Hasta = d2.ToDateTime(TimeOnly.MinValue).ToString("yyyy-MM-dd");
            return View(data);
        }

        [HttpGet]
        public async Task<IActionResult> Descargar(int id)
        {
            var q = _context.EnfCitas
                .Include(c => c.IdPersonaNavigation)
                .Include(c => c.IdHorarioNavigation)
                .Where(c => c.Id == id);

            if (!EsPersonalAutorizado())
            {
                var u = Usuario();
                var persona = await _context.EnfPersonas.FirstOrDefaultAsync(p => p.Usuario == u);
                if (persona != null) q = q.Where(c => c.IdPersona == persona.Id);
                else return Forbid();
            }

            var cita = await q.FirstOrDefaultAsync();
            if (cita == null) return NotFound();

            var pdf = _pdf.ComprobanteCita(cita); // ✅ ahora existe este método
            var fileName = $"Comprobante_{cita.Id}_{DateTime.Now:yyyyMMddHHmm}.pdf";
            return File(pdf, "application/pdf", fileName);
        }

        [HttpGet]
        public async Task<IActionResult> DescargarRango(string? desde = null, string? hasta = null)
        {
            var d1 = ParseDate(desde) ?? DateOnly.FromDateTime(DateTime.Today);
            var d2 = ParseDate(hasta) ?? DateOnly.FromDateTime(DateTime.Today);

            var q = _context.EnfCitas
                .Include(c => c.IdPersonaNavigation)
                .Include(c => c.IdHorarioNavigation)
                .Where(c => c.IdHorarioNavigation != null &&
                            c.IdHorarioNavigation.Fecha >= d1 &&
                            c.IdHorarioNavigation.Fecha <= d2);

            if (!EsPersonalAutorizado())
            {
                var u = Usuario();
                var persona = await _context.EnfPersonas.FirstOrDefaultAsync(p => p.Usuario == u);
                if (persona != null) q = q.Where(c => c.IdPersona == persona.Id);
                else return Forbid();
            }

            var citas = await q.OrderBy(c => c.IdHorarioNavigation!.Fecha)
                               .ThenBy(c => c.IdHorarioNavigation!.Hora)
                               .AsNoTracking()
                               .ToListAsync();

            var pdf = _pdf.ComprobantesRango(citas, d1, d2); // ✅ ahora recibe citas + fechas
            var fileName = $"Comprobantes_{d1:yyyyMMdd}_{d2:yyyyMMdd}.pdf";
            return File(pdf, "application/pdf", fileName);
        }
    }
}
