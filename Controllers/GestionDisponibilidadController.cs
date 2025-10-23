using Enfermeria_app.Models;
using Enfermeria_app.Models; // <-- ¡VERIFICA TU NAMESPACE!
using Enfermeria_app.ViewModels;
using Enfermeria_app.ViewModels; // <-- ¡VERIFICA TU NAMESPACE!
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;



namespace Enfermeria_app.Controllers // <-- ¡VERIFICA TU NAMESPACE!
{
    [Authorize]
    public class GestionDisponibilidadController : Controller
    {
        private readonly EnfermeriaContext _context;

        public GestionDisponibilidadController(EnfermeriaContext context)
        {
            _context = context;
        }
        private bool TienePermisoGestionHorario(string usuarioActual)
        {
            if (string.IsNullOrWhiteSpace(usuarioActual))
                return false;

            // Normaliza el nombre de usuario
            string usuarioNormalizado = usuarioActual.Trim().ToLower();

            // Busca en la base de datos la persona que tiene ese usuario
            var persona = _context.EnfPersonas
                .AsNoTracking()
                .FirstOrDefault(p => p.Usuario.Trim().ToLower() == usuarioNormalizado);

            if (persona == null || !persona.Activo)
                return false;

            // Normaliza tipo de usuario (por si hay espacios o mayúsculas)
            string tipo = persona.Tipo?.Trim().ToLower() ?? "";

            // Permitir solo doctor y asistente
            return tipo == "doctor" || tipo == "asistente";
        }


        public async Task<IActionResult> Index(DateTime? fechaSeleccionada)
        {
            string? usuarioActual = HttpContext.Session.GetString("Usuario");

            if (string.IsNullOrEmpty(usuarioActual) || !TienePermisoGestionHorario(usuarioActual))
            {
                return RedirectToAction("AccesoDenegado", "Home");
            }
            // --- FIN DE RESTRICCIÓN ---


            DateTime fechaPicker = fechaSeleccionada?.Date ?? DateTime.Today;
            DateOnly fechaParaDb = DateOnly.FromDateTime(fechaPicker);

            var viewModel = new GestionDiaViewModel
            {
                FechaSeleccionada = fechaPicker,
                HorasDelDia = new List<HoraViewModel>()
            };

            if (fechaParaDb.DayOfWeek == DayOfWeek.Saturday || fechaParaDb.DayOfWeek == DayOfWeek.Sunday)
            {
                viewModel.EsFinDeSemana = true;
                return View(viewModel);
            }

            bool existenHorarios = await _context.EnfHorarios.AnyAsync(h => h.Fecha == fechaParaDb);

            if (!existenHorarios)
            {
                var nuevosHorarios = new List<EnfHorario>();
                for (int hora = 7; hora <= 17; hora++)
                {
                    nuevosHorarios.Add(new EnfHorario
                    {
                        Fecha = fechaParaDb,
                        Hora = new TimeOnly(hora, 0, 0),
                        Estado = "Activo",
                        UsuarioCreacion = "Sistema"
                    });
                }
                await _context.EnfHorarios.AddRangeAsync(nuevosHorarios);
                await _context.SaveChangesAsync();

                var nuevasCitas = new List<EnfCita>();
                foreach (var horarioGuardado in nuevosHorarios)
                {
                    for (int i = 0; i < 2; i++)
                    {
                        nuevasCitas.Add(new EnfCita
                        {
                            IdHorario = horarioGuardado.Id,
                            IdPersona = null,
                            Estado = "Creada",
                            UsuarioCreacion = "Sistema"
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

            // --- LÍNEA CLAVE PARA EL CONTADOR ---
            // Esta línea calcula el total y lo asigna al modelo que va a la vista.
            viewModel.TotalCitasActivas = viewModel.HorasDelDia.Sum(h => h.CantidadCitasProgramadas);

            return View(viewModel);
        }

        [HttpGet]
        public async Task<IActionResult> ObtenerHorarios(DateTime fecha)
        {
            var fechaDb = DateOnly.FromDateTime(fecha.Date);
            var horarios = await _context.EnfHorarios
                .Include(h => h.EnfCita)
                .Where(h => h.Fecha == fechaDb)
                .Select(h => new {
                    idHorario = h.Id,
                    hora = h.Hora.ToString(),
                    disponibles = h.EnfCita.Count(c => c.Estado == "Creada")
                })
                .ToListAsync();

            return Json(horarios);
        }

        [HttpPost]
        public async Task<IActionResult> GuardarCambios([FromBody] List<GuardarCitaDto> datosCitas)
        {
            if (datosCitas == null || !datosCitas.Any())
            {
                return Json(new { success = false, message = "No se recibieron datos." });
            }

            using (var transaction = await _context.Database.BeginTransactionAsync())
            {
                try
                {
                    // --- INICIO DE LA LÓGICA DE GUARDADO (ESTA PARTE ES IGUAL) ---
                    foreach (var dato in datosCitas)
                    {
                        var citasActuales = await _context.EnfCitas
                            .Where(c => c.IdHorario == dato.HorarioId && c.Estado == "Creada")
                            .ToListAsync();

                        int cantidadEnBd = citasActuales.Count;
                        int cantidadDeseada = dato.Cantidad;

                        if (cantidadDeseada > cantidadEnBd)
                        {
                            int citasParaAgregar = cantidadDeseada - cantidadEnBd;
                            for (int i = 0; i < citasParaAgregar; i++)
                            {
                                _context.EnfCitas.Add(new EnfCita { IdHorario = dato.HorarioId, IdPersona = null, Estado = "Creada", UsuarioCreacion = "Sistema" });
                            }
                        }
                        else if (cantidadDeseada < cantidadEnBd)
                        {
                            int citasParaQuitar = cantidadEnBd - cantidadDeseada;
                            var citasARemover = citasActuales.Take(citasParaQuitar);
                            _context.EnfCitas.RemoveRange(citasARemover);
                        }
                    }
                    await _context.SaveChangesAsync();
                    // --- FIN DE LA LÓGICA DE GUARDADO ---

                    // --- CAMBIO CLAVE AQUÍ ---
                    // 1. Calculamos el nuevo total de citas para la fecha que acabamos de modificar.
                    var fechaGuardada = await _context.EnfHorarios
                        .Where(h => h.Id == datosCitas.First().HorarioId)
                        .Select(h => h.Fecha)
                        .FirstAsync();

                    var nuevoTotal = await _context.EnfCitas
                        .CountAsync(c => c.IdHorarioNavigation.Fecha == fechaGuardada && c.Estado == "Creada");

                    // 2. Confirmamos la transacción
                    await transaction.CommitAsync();

                    // 3. Devolvemos el éxito Y el nuevo total calculado.
                    return Json(new { success = true, nuevoTotalCitas = nuevoTotal });
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    System.Diagnostics.Debug.WriteLine(ex);
                    return Json(new { success = false, message = "Ocurrió un error al guardar los datos." });
                }
            }
        }
    }
}