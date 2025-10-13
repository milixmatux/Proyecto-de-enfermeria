using Enfermeria_app.Models;
using Enfermeria_app.Models;     // Asegúrate de cambiar a tu namespace real
using Enfermeria_app.ViewModels;        // Asegúrate de cambiar a tu namespace real
using Microsoft.AspNetCore.Mvc;
using System;
using System.Linq;
using Microsoft.AspNetCore.Authorization;



[Authorize]
public class AgendamientoController : Controller
{
    private readonly EnfermeriaContext _context;

    public AgendamientoController(EnfermeriaContext context)
    {
        _context = context;
    }

    // GET: /Agendamiento/Sacar
    public IActionResult Sacar()
    {
        // Consulta: solo horarios activos o disponibles
        var horarios = _context.EnfHorarios
            .Where(h => h.Estado == "DISPONIBLE") // Ajusta si tu campo Estado tiene otro nombre o valores
            .OrderBy(h => h.Fecha)                // Ordena por fecha
            .ThenBy(h => h.FechaCreacion)            // Y luego por hora
            .ToList();

        // Pasa la lista a la vista
        return View(horarios);
    }

    // POST: /Agendamiento/Reservar
    [HttpPost]
    public IActionResult Reservar(int id)
    {
        var horario = _context.EnfHorarios.FirstOrDefault(h => h.Id == id);
        if (horario == null)
        {
            return NotFound();
        }

        // Marca el horario como reservado
        horario.Estado = "RESERVADO";
        horario.UsuarioModificacion = "admin"; // Cambia por usuario real logueado
        horario.FechaModificacion = DateTime.Now;

        _context.Update(horario);
        _context.SaveChanges();

        TempData["Mensaje"] = "Horario reservado correctamente.";
        return RedirectToAction("Sacar");
    }
}
