using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Taller.Data;

namespace Taller.Controllers;

public class HomeController : Controller
{
    private readonly AppDbContext _context;
    public HomeController(AppDbContext context) { _context = context; }

    public async Task<IActionResult> Index(int page = 1)
    {
        int pageSize = 20; // Cantidad de autos por página
        
        // Contamos el total para saber cuántas páginas hay
        var totalVehiculos = await _context.Vehiculos.CountAsync();
        
        var vehiculos = await _context.Vehiculos
            .Include(v => v.Reparaciones)
            .OrderByDescending(v => v.Patente) // Los "últimos" (según patente)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
    
        // Pasamos los datos de paginación a la vista
        ViewBag.CurrentPage = page;
        ViewBag.TotalPages = (int)Math.Ceiling(totalVehiculos / (double)pageSize);
        ViewBag.TotalCount = totalVehiculos;
        ViewBag.ShowMigrationTools = totalVehiculos == 0;
            
        return View(vehiculos);
    }

    [HttpGet]
    public async Task<IActionResult> BuscarAjax(string query) {
        var vQuery = _context.Vehiculos.Include(v => v.Reparaciones).AsQueryable();
        if (!string.IsNullOrWhiteSpace(query)) {
            var q = query.ToUpper().Trim();
            vQuery = vQuery.Where(v => v.Patente.Contains(q) || v.Marca.Contains(q) || v.Modelo.Contains(q));
        }

        var data = await vQuery.Take(20).Select(v => new {
            patente = v.Patente,
            patenteFormateada = v.Patente.Length == 7 ? v.Patente.Substring(0, 2) + " " + v.Patente.Substring(2, 3) + " " + v.Patente.Substring(5, 2) :
                               (v.Patente.Length == 6 ? v.Patente.Substring(0, 3) + " " + v.Patente.Substring(3, 3) : v.Patente),
            marca = v.Marca,
            modelo = v.Modelo,
            reparacionesCount = v.Reparaciones.Count
        }).ToListAsync();

        return Json(data);
    }
}