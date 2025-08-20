using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Enfermeria_app.Models;
using System.Linq;
using System.Threading.Tasks;

namespace Enfermeria_app.Controllers.Api
{
    [ApiController]
    [Route("api/[controller]")]
    public class HorariosApiController : ControllerBase
    {
        private readonly EnfermeriaContext _context;

        public HorariosApiController(EnfermeriaContext context)
        {
            _context = context;
        }

        // GET api/horariosapi/libres
        [HttpGet("libres")]
        public async Task<IActionResult> GetHorariosLibres()
        {
            var horarios = await _context.EnfHorarios
                .Where(h => h.Estado == "Libre")
                .OrderBy(h => h.Fecha)
                .ThenBy(h => h.Hora)
                .Select(h => new {
                    id = h.Id,
                    fecha = h.Fecha.ToString("yyyy-MM-dd"),
                    hora = h.Hora,
                    estado = h.Estado
                })
                .ToListAsync();

            return Ok(horarios);
        }
    }
}
