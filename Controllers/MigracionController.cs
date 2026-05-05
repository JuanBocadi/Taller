using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ExcelDataReader;
using Taller.Data;
using Taller.Models;
using System.Data;

namespace Taller.Controllers;

public class MigracionController : Controller
{
    private readonly AppDbContext _context;

    public MigracionController(AppDbContext context)
    {
        _context = context;
    }

    public IActionResult Index() => View();

    [HttpGet]
    public async Task<IActionResult> LimpiarBaseDeDatos()
    {
        try 
        {
            await _context.Database.ExecuteSqlRawAsync("DELETE FROM Reparaciones");
            await _context.Database.ExecuteSqlRawAsync("DELETE FROM Vehiculos");
            _context.ChangeTracker.Clear();

            TempData["Success"] = "Base de datos vaciada correctamente.";
            return RedirectToAction("Index", "Home");
        }
        catch (Exception ex)
        {
            TempData["Error"] = $"Error al limpiar: {ex.Message}";
            return RedirectToAction("Index", "Home");
        }
    }

    [HttpPost]
    public async Task<IActionResult> SubirExcels(List<IFormFile> archivosExcel)
    {
        int vCreados = 0; int rCreadas = 0;
        foreach (var file in archivosExcel)
        {
            if (file.Length == 0) continue;
            try
            {
                using var stream = file.OpenReadStream();
                using var reader = ExcelReaderFactory.CreateReader(stream);
                var result = reader.AsDataSet();
                var tabla = result.Tables[0];

                string patente = tabla.Rows[0][1]?.ToString()?.Replace(" ", "").ToUpper().Trim() ?? "";
                if (string.IsNullOrEmpty(patente)) continue;

                string datoVehiculo = tabla.Rows[0][4]?.ToString()?.Trim().ToUpper() ?? "";
                string marca = ""; string modelo = "";
                var partes = datoVehiculo.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
                if (partes.Length == 2) { marca = partes[0]; modelo = partes[1]; }
                else { modelo = datoVehiculo; marca = ""; }

                var vehiculo = await _context.Vehiculos.FirstOrDefaultAsync(v => v.Patente == patente);
                if (vehiculo == null) {
                    vehiculo = new Vehiculo { Patente = patente, Marca = marca, Modelo = modelo };
                    _context.Vehiculos.Add(vehiculo);
                    vCreados++;
                }

                int filaInicio = 0;
                for (int i = 0; i < tabla.Rows.Count; i++) {
                    if (tabla.Rows[i][0]?.ToString()?.ToUpper().Contains("FECHA") == true) { filaInicio = i + 1; break; }
                }

                if (filaInicio > 0) {
                    for (int i = filaInicio; i < tabla.Rows.Count; i++) {
                        var fila = tabla.Rows[i];
                        string detalle = fila[2]?.ToString()?.Trim() ?? "";
                        if (string.IsNullOrEmpty(detalle)) continue;

                        DateTime f = DateTime.Now;
                        if (fila[0] is double n) f = DateTime.FromOADate(n);
                        else DateTime.TryParse(fila[0]?.ToString(), out f);

                        int k = 0; if (fila[1] is double km) k = (int)km; else int.TryParse(fila[1]?.ToString(), out k);

                        _context.Reparaciones.Add(new Reparacion { Patente = patente, Fecha = f, Kilometraje = k, Detalle = detalle });
                        rCreadas++;
                    }
                }
            }
            catch { continue; }
        }
        await _context.SaveChangesAsync();
        TempData["Success"] = $"Importados: {vCreados} vehículos y {rCreadas} trabajos.";
        return RedirectToAction("Index");
    }

    [HttpGet]
    public async Task<IActionResult> MigrarTodoElDisco()
    {
        string rutaRaiz = @"E:\Fichas"; 
        
        if (!Directory.Exists(rutaRaiz))
        {
            return Json(new { Error = $"No se encontró la ruta {rutaRaiz}. Verificá el Pendrive." });
        }

        int vCreados = 0;
        int rCreadas = 0;
        var todosLosArchivos = Directory.GetFiles(rutaRaiz, "*.xls", SearchOption.AllDirectories);
        var mapaPatenteArchivo = new Dictionary<string, string>();
        var repartidos = new List<object>(); 
        var fallidos = new List<object>();

        _context.ChangeTracker.AutoDetectChangesEnabled = false;

        try 
        {
            foreach (var ruta in todosLosArchivos)
            {
                string nombreArchivo = Path.GetFileName(ruta);
                try
                {
                    using var stream = System.IO.File.Open(ruta, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    using var reader = ExcelReaderFactory.CreateReader(stream);
                    var result = reader.AsDataSet();
                    if (result.Tables.Count == 0) continue;
                    
                    var tabla = result.Tables[0];
                    string patente = tabla.Rows[0][1]?.ToString()?.Replace(" ", "").ToUpper().Trim() ?? "";
                    if (string.IsNullOrEmpty(patente)) continue;

                    var vehiculo = await _context.Vehiculos.AsNoTracking().FirstOrDefaultAsync(v => v.Patente == patente);
                    
                    if (vehiculo == null)
                    {
                        string datoVehiculo = tabla.Rows[0][4]?.ToString()?.Trim().ToUpper() ?? "";
                        string marca = ""; string modelo = "";
                        var partes = datoVehiculo.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
                        if (partes.Length == 2) { marca = partes[0]; modelo = partes[1]; }
                        else { modelo = datoVehiculo; marca = ""; }

                        vehiculo = new Vehiculo { Patente = patente, Marca = marca, Modelo = modelo };
                        _context.Vehiculos.Add(vehiculo);
                        vCreados++;
                        await _context.SaveChangesAsync(); 
                        mapaPatenteArchivo[patente] = nombreArchivo;
                    }

                    int filaInicio = 0;
                    for (int i = 0; i < tabla.Rows.Count; i++) {
                        if (tabla.Rows[i][0]?.ToString()?.ToUpper().Contains("FECHA") == true) { filaInicio = i + 1; break; }
                    }

                    if (filaInicio > 0) {
                        for (int i = filaInicio; i < tabla.Rows.Count; i++) {
                            var fila = tabla.Rows[i];
                            string detalle = fila[2]?.ToString()?.Trim() ?? "";
                            if (string.IsNullOrWhiteSpace(detalle)) continue;

                            DateTime f = DateTime.Now;
                            if (fila[0] is double n) f = DateTime.FromOADate(n);
                            else DateTime.TryParse(fila[0]?.ToString(), out f);

                            int k = 0; if (fila[1] is double km) k = (int)km; else int.TryParse(fila[1]?.ToString(), out k);

                            _context.Reparaciones.Add(new Reparacion { Patente = patente, Fecha = f, Kilometraje = k, Detalle = detalle });
                            rCreadas++;
                        }
                    }

                    if ((vCreados + rCreadas) % 200 == 0) {
                        await _context.SaveChangesAsync();
                        _context.ChangeTracker.Clear();
                    }
                }
                catch (Exception ex) {
                    fallidos.Add(new { Archivo = nombreArchivo, Error = ex.Message });
                }
            }

            await _context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            return Json(new { Error = "Error crítico en la migración", Detalle = ex.Message });
        }
        finally 
        {
            _context.ChangeTracker.AutoDetectChangesEnabled = true;
        }

        return Json(new {
            Resumen = new {
                Total = todosLosArchivos.Length,
                NuevosVehiculos = vCreados,
                Reparaciones = rCreadas,
                Errores = fallidos.Count
            },
            DetalleErrores = fallidos
        });
    }
} // Aquí termina la Clase