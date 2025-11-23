using Enfermeria_app.Models;
using Enfermeria_app.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using System.Globalization;

namespace Enfermeria_app.Controllers
{
    [Authorize(Policy = "GestionHorarios")] // 🔐 SOLO CONSULTORIO PUEDE ENTRAR
    public class GestionDisponibilidadController : Controller
    {
        private readonly EnfermeriaContext _context;

        public GestionDisponibilidadController(EnfermeriaContext context)
        {
            _context = context;
        }

        // ============================
        // VALIDACIÓN DE PERMISOS
        // ============================
        private bool UsuarioEsConsultorio(string usuarioActual)
        {
            if (string.IsNullOrWhiteSpace(usuarioActual))
                return false;

            var persona = _context.EnfPersonas
                .AsNoTracking()
                .FirstOrDefault(p => p.Usuario == usuarioActual && p.Activo);

            if (persona == null)
                return false;

            return persona.Tipo.Trim().ToLower() == "consultorio";
        }

        // ============================
        // INDEX (VISTA PRINCIPAL)
        // ============================
        public async Task<IActionResult> Index(DateTime? fechaSeleccionada)
        {
            string? usuarioActual = HttpContext.Session.GetString("Usuario");

            if (!UsuarioEsConsultorio(usuarioActual))
                return RedirectToAction("AccesoDenegado", "Home");

            DateTime fechaPicker = fechaSeleccionada?.Date ?? DateTime.Today;
            DateOnly fechaParaDb = DateOnly.FromDateTime(fechaPicker);

            var viewModel = new GestionDiaViewModel
            {
                FechaSeleccionada = fechaPicker,
                HorasDelDia = new List<HoraViewModel>()
            };

            // No permitir sábados ni domingos
            if (fechaParaDb.DayOfWeek == DayOfWeek.Saturday ||
                fechaParaDb.DayOfWeek == DayOfWeek.Sunday)
            {
                viewModel.EsFinDeSemana = true;
                return View(viewModel);
            }

            // Generar horarios si no existen
            bool existenHorarios = await _context.EnfHorarios.AnyAsync(h => h.Fecha == fechaParaDb);

            if (!existenHorarios)
            {
                var nuevosHorarios = new List<EnfHorario>();

                // 7:00 AM – 5:00 PM cada 30 minutos
                for (var hora = new TimeOnly(7, 0); hora < new TimeOnly(17, 0); hora = hora.AddMinutes(30))
                {
                    nuevosHorarios.Add(new EnfHorario
                    {
                        Fecha = fechaParaDb,
                        Hora = hora,
                        Estado = "Activo",
                        UsuarioCreacion = usuarioActual ?? "Sistema"
                    });
                }

                await _context.EnfHorarios.AddRangeAsync(nuevosHorarios);
                await _context.SaveChangesAsync();

                var nuevasCitas = new List<EnfCita>();

                foreach (var horario in nuevosHorarios)
                {
                    // Cada horario tiene 2 cupos
                    for (int i = 0; i < 2; i++)
                    {
                        nuevasCitas.Add(new EnfCita
                        {
                            IdHorario = horario.Id,
                            IdPersona = null,
                            Estado = "Creada",
                            UsuarioCreacion = usuarioActual ?? "Sistema"
                        });
                    }
                }

                await _context.EnfCitas.AddRangeAsync(nuevasCitas);
                await _context.SaveChangesAsync();
            }

            var horariosDelDia = await _context.EnfHorarios
                .Where(h => h.Fecha == fechaParaDb)
                .Include(h => h.EnfCita)
                .OrderBy(h => h.Hora)
                .ToListAsync();

            foreach (var horario in horariosDelDia)
            {
                int citasActivas = horario.EnfCita.Count(c => c.Estado == "Creada");

                viewModel.HorasDelDia.Add(new HoraViewModel
                {
                    IdHorario = horario.Id,
                    Hora = horario.Hora.ToString("h:mm tt", CultureInfo.InvariantCulture),
                    CantidadCitasProgramadas = citasActivas,
                    Estado = citasActivas > 0 ? "Activo" : "Cancelado"
                });
            }

            viewModel.TotalCitasActivas = viewModel.HorasDelDia.Sum(h => h.CantidadCitasProgramadas);
            return View(viewModel);
        }

        // ============================
        // OBTENER HORARIOS DEL DÍA
        // ============================
        [HttpGet]
        public async Task<IActionResult> ObtenerHorarios(DateTime fecha)
        {
            var fechaDb = DateOnly.FromDateTime(fecha.Date);

            var horarios = await _context.EnfHorarios
                .Include(h => h.EnfCita)
                .Where(h => h.Fecha == fechaDb)
                .Select(h => new
                {
                    idHorario = h.Id,
                    hora = h.Hora.ToString(),
                    disponibles = h.EnfCita.Count(c => c.Estado == "Creada")
                })
                .ToListAsync();

            return Json(horarios);
        }

        // ============================
        // GUARDAR CAMBIOS EN CUPOS
        // ============================
        [HttpPost]
        public async Task<IActionResult> GuardarCambios([FromBody] List<GuardarCitaDto> datosCitas)
        {
            if (datosCitas == null || !datosCitas.Any())
                return Json(new { success = false, message = "No se recibieron datos." });

            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                foreach (var dato in datosCitas)
                {
                    var citasActuales = await _context.EnfCitas
                        .Where(c => c.IdHorario == dato.HorarioId && c.Estado == "Creada")
                        .ToListAsync();

                    int cantidadActual = citasActuales.Count;
                    int cantidadNueva = dato.Cantidad;

                    if (cantidadNueva > cantidadActual)
                    {
                        for (int i = 0; i < cantidadNueva - cantidadActual; i++)
                        {
                            _context.EnfCitas.Add(new EnfCita
                            {
                                IdHorario = dato.HorarioId,
                                Estado = "Creada",
                                UsuarioCreacion = "Sistema"
                            });
                        }
                    }
                    else if (cantidadNueva < cantidadActual)
                    {
                        var eliminar = citasActuales.Take(cantidadActual - cantidadNueva);
                        _context.EnfCitas.RemoveRange(eliminar);
                    }
                }

                await _context.SaveChangesAsync();

                // Obtener la fecha modificada
                var fechaGuardada = await _context.EnfHorarios
                    .Where(h => h.Id == datosCitas.First().HorarioId)
                    .Select(h => h.Fecha)
                    .FirstAsync();

                var nuevoTotal = await _context.EnfCitas
                    .CountAsync(c => c.IdHorarioNavigation.Fecha == fechaGuardada &&
                                     c.Estado == "Creada");

                await transaction.CommitAsync();

                return Json(new { success = true, nuevoTotalCitas = nuevoTotal });
            }
            catch
            {
                await transaction.RollbackAsync();
                return Json(new { success = false, message = "Error al guardar los datos." });
            }
        }
    }
}
