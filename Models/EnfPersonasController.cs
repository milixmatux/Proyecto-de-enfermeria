using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace Enfermeria_app.Models
{
    public class EnfPersonasController : Controller
    {
        private readonly EnfermeriaContext _context;

        public EnfPersonasController(EnfermeriaContext context)
        {
            _context = context;
        }

        // GET: EnfPersonas
        public async Task<IActionResult> Index()
        {
            return View(await _context.EnfPersonas.ToListAsync());
        }

        // GET: EnfPersonas/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var enfPersona = await _context.EnfPersonas
                .FirstOrDefaultAsync(m => m.Id == id);
            if (enfPersona == null)
            {
                return NotFound();
            }

            return View(enfPersona);
        }

        // GET: EnfPersonas/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: EnfPersonas/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Id,Cedula,Nombre,Telefono,Email,Usuario,Password,Departamento,Tipo,Seccion,FechaNacimiento,Sexo")] EnfPersona enfPersona)
        {
            if (ModelState.IsValid)
            {
                _context.Add(enfPersona);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(enfPersona);
        }

        // GET: EnfPersonas/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var enfPersona = await _context.EnfPersonas.FindAsync(id);
            if (enfPersona == null)
            {
                return NotFound();
            }
            return View(enfPersona);
        }

        // POST: EnfPersonas/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Cedula,Nombre,Telefono,Email,Usuario,Password,Departamento,Tipo,Seccion,FechaNacimiento,Sexo")] EnfPersona enfPersona)
        {
            if (id != enfPersona.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(enfPersona);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!EnfPersonaExists(enfPersona.Id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(Index));
            }
            return View(enfPersona);
        }

        // GET: EnfPersonas/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var enfPersona = await _context.EnfPersonas
                .FirstOrDefaultAsync(m => m.Id == id);
            if (enfPersona == null)
            {
                return NotFound();
            }

            return View(enfPersona);
        }

        // POST: EnfPersonas/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var enfPersona = await _context.EnfPersonas.FindAsync(id);
            if (enfPersona != null)
            {
                _context.EnfPersonas.Remove(enfPersona);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool EnfPersonaExists(int id)
        {
            return _context.EnfPersonas.Any(e => e.Id == id);
        }
    }
}
