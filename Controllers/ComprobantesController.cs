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

        // ✅ Método auxiliar para obtener el tipo de usuario actual
        private string ObtenerTipoUsuario()
        {
            return User?.Claims?.FirstOrDefault(c => c.Type == "TipoUsuario")?.Value ?? "";
        }

        // ✅ Método auxiliar para obtener el nombre de usuario actual
        private string ObtenerUsuario()
        {
            return User?.Identity?.Name ?? "";
        }

        [HttpGet]
        public async Task<IActionResult> Index(DateTime? desde, DateTime? hasta, string? nombre)
        {
            var hoy = DateOnly.FromDateTime(DateTime.Today);
            var fechaDesde = desde.HasValue ? DateOnly.FromDateTime(desde.Value) : hoy;
            var fechaHasta = hasta.HasValue ? DateOnly.FromDateTime(hasta.Value) : hoy;

            var tipoUsuario = ObtenerTipoUsuario();
            var usuario = ObtenerUsuario();

            var query = _context.EnfCitas
                .Include(c => c.IdPersonaNavigation)
                .Include(c => c.IdHorarioNavigation)
                .AsQueryable();

            // ✅ Solo citas reservadas (que tengan persona asignada)
            query = query.Where(c => c.IdPersonaNavigation != null);

            // ✅ Filtrar por rango de fechas
            query = query.Where(c =>
                c.IdHorarioNavigation != null &&
                c.IdHorarioNavigation.Fecha >= fechaDesde &&
                c.IdHorarioNavigation.Fecha <= fechaHasta);

            // Obtener persona actual
            var personaActual = await _context.EnfPersonas
                .FirstOrDefaultAsync(p => p.Usuario == usuario);

            // =======================================
            // 🔹 1. ESTUDIANTE / FUNCIONARIO / PROFESOR
            //    → Solo pueden ver sus comprobantes
            // =======================================
            if (tipoUsuario == "Estudiante" ||
                tipoUsuario == "Funcionario" ||
                tipoUsuario == "Profesor")
            {
                if (personaActual != null)
                    query = query.Where(c => c.IdPersona == personaActual.Id);
                else
                    query = query.Where(c => false);
            }
            else
            {
                // =======================================
                // 🔹 2. CONSULTORIO / ADMINISTRATIVO
                //    → Sí pueden buscar por nombre/cédula
                // =======================================
                if (!string.IsNullOrWhiteSpace(nombre))
                {
                    var filtro = nombre.Trim().ToLower();

                    query = query.Where(c =>
                        (c.IdPersonaNavigation!.Nombre.ToLower().Contains(filtro)) ||
                        (c.IdPersonaNavigation!.Cedula.ToLower().Contains(filtro))
                    );
                }
            }


            var citas = await query
                .OrderByDescending(c => c.IdHorarioNavigation!.Fecha)
                .ThenByDescending(c => c.IdHorarioNavigation!.Hora)
                .AsNoTracking()
                .ToListAsync();

            // 🔹 Enviar datos a la vista
            ViewBag.Desde = fechaDesde.ToDateTime(TimeOnly.MinValue).ToString("yyyy-MM-dd");
            ViewBag.Hasta = fechaHasta.ToDateTime(TimeOnly.MinValue).ToString("yyyy-MM-dd");
            ViewBag.Nombre = nombre ?? "";
            ViewBag.TipoUsuario = tipoUsuario;

            // 🔹 Si viene de AJAX (búsqueda en tiempo real)
            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                return PartialView("_TablaComprobantes", citas);

            return View(citas);
        }
    }
}
