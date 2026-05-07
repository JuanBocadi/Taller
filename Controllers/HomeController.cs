using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Taller.Data;

namespace Taller.Controllers;

public class HomeController : Controller
{
    private readonly AppDbContext _context;
    public HomeController(AppDbContext context) { _context = context; }

    public async Task<IActionResult> Index(int page = 1, string? buscar = null)
    {
        const int pageSize = 20;

        buscar = string.IsNullOrWhiteSpace(buscar) ? null : buscar.Trim();
        ViewBag.SearchQuery = buscar ?? "";

        var totalGlobales = await _context.Vehiculos.CountAsync();
        ViewBag.ShowMigrationTools = totalGlobales == 0 && string.IsNullOrWhiteSpace(buscar);

        var query = _context.Vehiculos.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(buscar))
        {
            var q = buscar.ToUpper();
            query = query.Where(v => v.Patente.Contains(q) || v.Marca.Contains(q) || v.Modelo.Contains(q));
        }

        var totalFiltrados = await query.CountAsync();
        if (page < 1) page = 1;

        var totalPages = Math.Max(1, (int)Math.Ceiling(totalFiltrados / (double)pageSize));
        if (page > totalPages) page = totalPages;

        ViewBag.CurrentPage = page;
        ViewBag.TotalPages = totalPages;
        ViewBag.TotalCount = totalFiltrados;

        var vehiculos = await query
            .Include(v => v.Reparaciones)
            .OrderByDescending(v => v.Patente)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return View(vehiculos);
    }
}
