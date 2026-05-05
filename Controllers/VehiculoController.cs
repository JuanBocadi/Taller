using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Taller.Data;
using Taller.Models;

namespace Taller.Controllers;

public class VehiculosController : Controller
{
    private readonly AppDbContext _context;

    public VehiculosController(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Details(string id)
    {
        if (id == null) return NotFound();

        var vehiculo = await _context.Vehiculos
            .Include(v => v.Reparaciones)
            .FirstOrDefaultAsync(m => m.Patente == id.ToUpper());

        if (vehiculo == null) return NotFound();

        // Ordenamos las reparaciones por fecha descendente
        vehiculo.Reparaciones = vehiculo.Reparaciones.OrderByDescending(r => r.Fecha).ToList();

        return View(vehiculo);
    }

    public IActionResult Create(string patente)
    {
        var vehiculo = new Vehiculo { Patente = patente?.ToUpper() ?? "" };
        return View(vehiculo);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("Patente,Marca,Modelo,EsMaquinaria")] Vehiculo vehiculo)
    {
        // 1. Limpiamos la patente
        vehiculo.Patente = vehiculo.Patente.Replace(" ", "").ToUpper().Trim();
    
        // 2. Validación Manual: Si NO es maquinaria, obligamos formato estándar
        if (!vehiculo.EsMaquinaria)
        {
            var regexVieja = new System.Text.RegularExpressions.Regex(@"^[A-Z]{3}[0-9]{3}$");
            var regexNueva = new System.Text.RegularExpressions.Regex(@"^[A-Z]{2}[0-9]{3}[A-Z]{2}$");
    
            if (!regexVieja.IsMatch(vehiculo.Patente) && !regexNueva.IsMatch(vehiculo.Patente))
            {
                ModelState.AddModelError("Patente", "Formato inválido. Si es un vehículo especial (montacargas, etc.), active la opción correspondiente.");
            }
        }
    
        if (ModelState.IsValid)
        {
            try 
            {
                var existe = await _context.Vehiculos.AnyAsync(v => v.Patente == vehiculo.Patente);
                if (existe)
                {
                    ModelState.AddModelError("Patente", "Esta identificación ya está registrada.");
                    return View(vehiculo);
                }
    
                _context.Add(vehiculo);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Vehículo registrado correctamente.";
                return RedirectToAction(nameof(Index), "Home");
            }
            catch (Exception)
            {
                ModelState.AddModelError("", "Ocurrió un error al guardar.");
            }
        }
        return View(vehiculo);
    }
    // POST: Vehiculos/Delete/AAJ445
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(string id)
    {
        var vehiculo = await _context.Vehiculos.FirstOrDefaultAsync(v => v.Patente == id);
        if (vehiculo != null)
        {
            _context.Vehiculos.Remove(vehiculo);
            await _context.SaveChangesAsync();
            TempData["Success"] = $"El vehículo patente {id} y todo su historial fueron eliminados.";
        }
        return RedirectToAction("Index", "Home");
    }

}