using Enfermeria_app.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Enfermeria.Models
{
    public class EnfPersonasController : Controller
    {
        private readonly EnfermeriaContext _context;

        public EnfPersonasController(EnfermeriaContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index(string searchString)
        {
            var personas = _context.EnfPersonas.Where(p => p.Activo);

            if (!string.IsNullOrEmpty(searchString))
            {
                personas = personas.Where(p => p.Nombre.Contains(searchString));
            }

            var lista = await personas
            .OrderByDescending(p => p.Id) // Ordena del más reciente al más antiguo
            .ToListAsync();
            
            ViewData["CurrentFilter"] = searchString;
            return View(lista);
        }

        public async Task<IActionResult> Inactivos()
        {
            var inactivos = await _context.EnfPersonas
                .Where(p => !p.Activo)
                .ToListAsync();

            return View(inactivos);
        }

        [HttpPost]
        public async Task<IActionResult> Reactivar(int id)
        {
            var persona = await _context.EnfPersonas.FindAsync(id);
            if (persona != null)
            {
                persona.Activo = true;
                _context.Update(persona);
                await _context.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Inactivos));
        }

        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var enfPersona = await _context.EnfPersonas
                .FirstOrDefaultAsync(m => m.Id == id);

            if (enfPersona == null) return NotFound();

            return View(enfPersona);
        }

        [HttpGet]
        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(EnfPersona model)
        {
            if (!ModelState.IsValid)
                return View(model);

            if (_context.EnfPersonas.Any(p => p.Cedula == model.Cedula))
            {
                ViewBag.Error = "La cédula ya está registrada.";
                return View(model);
            }

            if (_context.EnfPersonas.Any(p => p.Nombre == model.Nombre))
            {
                ViewBag.Error = "El nombre ya está registrado.";
                return View(model);
            }

            if (_context.EnfPersonas.Any(p => p.Email == model.Email))
            {
                ViewBag.Error = "El correo ya está registrado.";
                return View(model);
            }

            if (_context.EnfPersonas.Any(p => p.Usuario == model.Usuario))
            {
                ViewBag.Error = "El usuario ya existe.";
                return View(model);
            }

            _context.EnfPersonas.Add(model);
            await _context.SaveChangesAsync();

            ViewBag.Mensaje = "Registro completado";
            return View(); // No redirige, se queda en el formulario
        }


        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var enfPersona = await _context.EnfPersonas.FindAsync(id);
            if (enfPersona == null) return NotFound();

            return View(enfPersona);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Cedula,Nombre,Telefono,Email,Usuario,Password,Departamento,Tipo,Seccion,FechaNacimiento,Sexo,Activo")] EnfPersona enfPersona)
        {
            if (id != enfPersona.Id) return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(enfPersona);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!_context.EnfPersonas.Any(e => e.Id == enfPersona.Id))
                        return NotFound();
                    else
                        throw;
                }
                return RedirectToAction(nameof(Index));
            }
            return View(enfPersona);
        }

        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var enfPersona = await _context.EnfPersonas
                .FirstOrDefaultAsync(m => m.Id == id);

            if (enfPersona == null) return NotFound();

            return View(enfPersona);
        }



        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var enfPersona = await _context.EnfPersonas.FindAsync(id);
            if (enfPersona != null)
            {
                enfPersona.Activo = false;
                _context.Update(enfPersona);
                await _context.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Index));
        }
    }
}