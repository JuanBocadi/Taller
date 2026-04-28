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
            // Borramos primero las reparaciones (hijos) para evitar errores de clave foránea
            var todasLasReparaciones = await _context.Reparaciones.ToListAsync();
            _context.Reparaciones.RemoveRange(todasLasReparaciones);
            await _context.SaveChangesAsync();
    
            // Ahora que los vehículos están "sueltos", los borramos
            var todosLosVehiculos = await _context.Vehiculos.ToListAsync();
            _context.Vehiculos.RemoveRange(todosLosVehiculos);
            await _context.SaveChangesAsync();
    
            return Ok("Base de datos limpia y lista. Ya podés correr la migración masiva.");
        }
        catch (Exception ex)
        {
            return BadRequest($"Error al limpiar: {ex.Message}");
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

    // ESTA ES LA FUNCIÓN PARA LOS 6700 ARCHIVOS LOCALES EN E:\Fichas
    [HttpGet]
    public async Task<IActionResult> MigrarTodoElDisco()
    {
        string rutaRaiz = @"E:\Fichas"; 

        if (!Directory.Exists(rutaRaiz))
        {
            return BadRequest($"La ruta {rutaRaiz} no es accesible.");
        }

        int vCreados = 0;
        int rCreadas = 0;
        int archivosProcesados = 0;

        var todosLosArchivos = Directory.GetFiles(rutaRaiz, "*.xls", SearchOption.AllDirectories);

        _context.ChangeTracker.AutoDetectChangesEnabled = false;

        foreach (var ruta in todosLosArchivos)
        {
            try
            {
                using var stream = System.IO.File.Open(ruta, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var reader = ExcelReaderFactory.CreateReader(stream);
                var result = reader.AsDataSet();
                if (result.Tables.Count == 0) continue;
                var tabla = result.Tables[0];

                string patente = tabla.Rows[0][1]?.ToString()?.Replace(" ", "").ToUpper().Trim() ?? "";
                if (string.IsNullOrEmpty(patente)) continue;

                string datoVehiculo = tabla.Rows[0][4]?.ToString()?.Trim().ToUpper() ?? "";
                string marca = ""; string modelo = "";
                var partes = datoVehiculo.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
                if (partes.Length == 2) { marca = partes[0]; modelo = partes[1]; }
                else { modelo = datoVehiculo; marca = ""; }

                var vehiculo = await _context.Vehiculos.FirstOrDefaultAsync(v => v.Patente == patente);
                if (vehiculo == null)
                {
                    vehiculo = new Vehiculo { Patente = patente, Marca = marca, Modelo = modelo };
                    _context.Vehiculos.Add(vehiculo);
                    vCreados++;
                }

                int filaInicio = 0;
                for (int i = 0; i < tabla.Rows.Count; i++)
                {
                    if (tabla.Rows[i][0]?.ToString()?.ToUpper().Contains("FECHA") == true)
                    {
                        filaInicio = i + 1;
                        break;
                    }
                }

                if (filaInicio > 0)
                {
                    for (int i = filaInicio; i < tabla.Rows.Count; i++)
                    {
                        var fila = tabla.Rows[i];
                        string detalle = fila[2]?.ToString()?.Trim() ?? "";
                        if (string.IsNullOrWhiteSpace(detalle)) continue;

                        DateTime fecha = DateTime.Now;
                        if (fila[0] is double n) fecha = DateTime.FromOADate(n);
                        else DateTime.TryParse(fila[0]?.ToString(), out fecha);

                        int km = 0;
                        if (fila[1] is double k) km = (int)k;
                        else int.TryParse(fila[1]?.ToString(), out km);

                        _context.Reparaciones.Add(new Reparacion { Patente = patente, Fecha = fecha, Kilometraje = km, Detalle = detalle });
                        rCreadas++;
                    }
                }

                archivosProcesados++;
                if (archivosProcesados % 200 == 0) await _context.SaveChangesAsync();
            }
            catch { continue; }
        }

        _context.ChangeTracker.AutoDetectChangesEnabled = true;
        await _context.SaveChangesAsync();

        return Ok($"¡Migración Completada! Archivos: {archivosProcesados}, Autos: {vCreados}, Reparaciones: {rCreadas}");
    }
}