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
    public class ComprobantesController : Controller
    {
        private readonly EnfermeriaContext _context;

        public ComprobantesController(EnfermeriaContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> Index(DateTime? desde, DateTime? hasta, string? nombre)
        {
            // 🔹 Si no se envían fechas, usar la de hoy por defecto
            var hoy = DateOnly.FromDateTime(DateTime.Today);
            var fechaDesde = desde.HasValue ? DateOnly.FromDateTime(desde.Value) : hoy;
            var fechaHasta = hasta.HasValue ? DateOnly.FromDateTime(hasta.Value) : hoy;

            var query = _context.EnfCitas
                .Include(c => c.IdPersonaNavigation)
                .Include(c => c.IdHorarioNavigation)
                .AsQueryable();

            // 🔹 Solo citas reservadas (tienen persona asignada)
            query = query.Where(c => c.IdPersonaNavigation != null && c.IdPersonaNavigation.Nombre != null);

            // 🔹 Filtrar por rango de fechas
            query = query.Where(c => c.IdHorarioNavigation != null &&
                                     c.IdHorarioNavigation.Fecha >= fechaDesde &&
                                     c.IdHorarioNavigation.Fecha <= fechaHasta);

            // 🔹 Filtro por nombre o cédula
            if (!string.IsNullOrWhiteSpace(nombre))
            {
                nombre = nombre.Trim().ToLower();
                query = query.Where(c =>
                    (c.IdPersonaNavigation!.Nombre.ToLower().Contains(nombre)) ||
                    (c.IdPersonaNavigation!.Cedula.ToLower().Contains(nombre)));
            }

            var citas = await query
                .OrderByDescending(c => c.IdHorarioNavigation!.Fecha)
                .ThenByDescending(c => c.IdHorarioNavigation!.Hora)
                .AsNoTracking()
                .ToListAsync();

            ViewBag.Desde = fechaDesde.ToDateTime(TimeOnly.MinValue).ToString("yyyy-MM-dd");
            ViewBag.Hasta = fechaHasta.ToDateTime(TimeOnly.MinValue).ToString("yyyy-MM-dd");
            ViewBag.Nombre = nombre ?? "";

            // 🔹 Si la solicitud viene por AJAX (filtro dinámico)
            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                return PartialView("_TablaComprobantes", citas);

            return View(citas);
        }
    }
}
