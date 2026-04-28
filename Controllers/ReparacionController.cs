using Microsoft.AspNetCore.Mvc;
using Taller.Data;
using Taller.Models;

namespace Taller.Controllers;

public class ReparacionesController : Controller
{
    private readonly AppDbContext _context;

    public ReparacionesController(AppDbContext context)
    {
        _context = context;
    }

    public IActionResult Create(string patente)
    {
        if (string.IsNullOrEmpty(patente)) return RedirectToAction("Index", "Home");

        var reparacion = new Reparacion 
        { 
            Patente = patente.ToUpper(),
            Fecha = DateTime.Now
        };
        
        return View(reparacion);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("Patente,Fecha,Kilometraje,Detalle")] Reparacion reparacion)
    {
        if (ModelState.IsValid)
        {
            _context.Add(reparacion);
            await _context.SaveChangesAsync();
            
            TempData["Success"] = "Reparación registrada con éxito.";
            return RedirectToAction("Details", "Vehiculos", new { id = reparacion.Patente });
        }
        return View(reparacion);
    }

    // GET: Cargar la vista de Edición
    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null) return NotFound();
        var reparacion = await _context.Reparaciones.FindAsync(id);
        if (reparacion == null) return NotFound();
        return View(reparacion);
    }

    // POST: Guardar los cambios de la Edición
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, [Bind("Id,Patente,Fecha,Kilometraje,Detalle")] Reparacion reparacion)
    {
        if (id != reparacion.Id) return NotFound();

        if (ModelState.IsValid)
        {
            _context.Update(reparacion);
            await _context.SaveChangesAsync();
            TempData["Success"] = "Trabajo actualizado correctamente.";
            return RedirectToAction("Details", "Vehiculos", new { id = reparacion.Patente });
        }
        return View(reparacion);
    }

    // POST: Borrar un trabajo
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var reparacion = await _context.Reparaciones.FindAsync(id);
        if (reparacion != null)
        {
            var patente = reparacion.Patente;
            _context.Reparaciones.Remove(reparacion);
            await _context.SaveChangesAsync();
            TempData["Success"] = "Reparación eliminada del historial.";
            return RedirectToAction("Details", "Vehiculos", new { id = patente });
        }
        return RedirectToAction("Index", "Home");
    }
}