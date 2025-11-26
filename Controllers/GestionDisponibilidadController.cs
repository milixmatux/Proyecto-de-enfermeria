using Enfermeria_app.Models;
using Enfermeria_app.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using System.Globalization;

namespace Enfermeria_app.Controllers
{
    [Authorize]
    public class GestionDisponibilidadController : Controller
    {
        private readonly EnfermeriaContext _context;

        public GestionDisponibilidadController(EnfermeriaContext context)
        {
            _context = context;
        }

        // =====================================================
        //  🔐 VALIDACIÓN CENTRALIZADA DE PERMISOS
        // =====================================================
        private bool TienePermisoGestionHorario(string usuarioActual)
        {
            if (string.IsNullOrWhiteSpace(usuarioActual))
                return false;

            string usuarioNormalizado = usuarioActual.Trim().ToLower();

            var persona = _context.EnfPersonas
                .AsNoTracking()
                .FirstOrDefault(p => p.Usuario.Trim().ToLower() == usuarioNormalizado);

            if (persona == null || !persona.Activo)
                return false;

            string tipo = persona.Tipo?.Trim().ToLower() ?? "";

            //  SOLO estos pueden administrar horarios:
            //  ✔ consultorio
            //  ✔ administrativo
            return tipo == "consultorio" || tipo == "administrativo";
        }

        // =====================================================
        //  📅 INDEX: Mostramos horario del día seleccionado
        // =====================================================
        public async Task<IActionResult> Index(DateTime? fechaSeleccionada)
        {
            string? usuarioActual = HttpContext.Session.GetString("Usuario");

            if (string.IsNullOrEmpty(usuarioActual) || !TienePermisoGestionHorario(usuarioActual))
                return RedirectToAction("AccesoDenegado", "Home");

            DateTime fechaPicker = fechaSeleccionada?.Date ?? DateTime.Today;
            DateOnly fechaParaDb = DateOnly.FromDateTime(fechaPicker);

            var viewModel = new GestionDiaViewModel
            {
                FechaSeleccionada = fechaPicker,
                HorasDelDia = new List<HoraViewModel>()
            };

            if (fechaParaDb.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
            {
                viewModel.EsFinDeSemana = true;
                return View(viewModel);
            }

            // ¿Ya existen horarios para este día?
            bool existenHorarios = await _context.EnfHorarios.AnyAsync(h => h.Fecha == fechaParaDb);

            if (!existenHorarios)
            {
                var nuevosHorarios = new List<EnfHorario>();

                for (var hora = new TimeOnly(7, 0); hora < new TimeOnly(17, 0); hora = hora.AddMinutes(30))
                {
                    nuevosHorarios.Add(new EnfHorario
                    {
                        Fecha = fechaParaDb,
                        Hora = hora,
                        Estado = "Activo",
                        UsuarioCreacion = "Sistema"
                    });
                }

                await _context.EnfHorarios.AddRangeAsync(nuevosHorarios);
                await _context.SaveChangesAsync();

                var nuevasCitas = new List<EnfCita>();
                foreach (var horario in nuevosHorarios)
                {
                    for (int i = 0; i < 2; i++)
                    {
                        nuevasCitas.Add(new EnfCita
                        {
                            IdHorario = horario.Id,
                            Estado = "Creada",
                            UsuarioCreacion = "Sistema"
                        });
                    }
                }

                await _context.EnfCitas.AddRangeAsync(nuevasCitas);
                await _context.SaveChangesAsync();
            }

            // Cargar horarios con citas
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

        // =====================================================
        //  AJAX - Obtener horarios de un día
        // =====================================================
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

        // =====================================================
        //  📝 Guardar cambios de capacidad por bloque horario
        // =====================================================
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
                        // Agregar cupos
                        for (int i = 0; i < (cantidadNueva - cantidadActual); i++)
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
                        // Quitar cupos sobrantes
                        var paraEliminar = citasActuales.Take(cantidadActual - cantidadNueva).ToList();
                        _context.EnfCitas.RemoveRange(paraEliminar);
                    }
                }

                await _context.SaveChangesAsync();

                // Recalcular total del día actualizado
                var fecha = await _context.EnfHorarios
                    .Where(h => h.Id == datosCitas.First().HorarioId)
                    .Select(h => h.Fecha)
                    .FirstAsync();

                var total = await _context.EnfCitas
                    .CountAsync(c => c.IdHorarioNavigation.Fecha == fecha && c.Estado == "Creada");

                await transaction.CommitAsync();

                return Json(new { success = true, nuevoTotalCitas = total });
            }
            catch
            {
                await transaction.RollbackAsync();
                return Json(new { success = false, message = "Error al guardar los cambios." });
            }
        }
    }
}
