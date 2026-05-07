using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using System;
using System.Data;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using System.Windows.Forms;
using Taller.Data;
using Taller.Infrastructure;

namespace Taller.Controllers;

public class BackupController : Controller
{
    private readonly AppDbContext _context;

    public BackupController(AppDbContext context)
    {
        _context = context;
    }

    public IActionResult Index()
    {
        var configured = AppPaths.GetConfiguredBackupDirectory();
        bool rutaDisponible;
        string rutaActual;
        List<BackupInfo> backups;

        if (!string.IsNullOrWhiteSpace(configured))
        {
            rutaActual = configured;
            rutaDisponible = Directory.Exists(configured);
            backups = rutaDisponible ? ObtenerArchivosDbDisponibles(configured) : new List<BackupInfo>();
        }
        else
        {
            rutaActual = AppPaths.GetDefaultBackupDirectory();
            Directory.CreateDirectory(rutaActual);
            rutaDisponible = true;
            backups = ObtenerArchivosDbDisponibles(rutaActual);
        }

        var model = new BackupIndexViewModel
        {
            RutaActual = rutaActual,
            RutaDisponible = rutaDisponible,
            Backups = backups
        };

        return View(model);
    }

    [HttpPost]
    public IActionResult SeleccionarRuta()
    {
        try
        {
            // Intentamos detectar unidades extraíbles conectadas y listas.
            var removable = DriveInfo.GetDrives()
                .Where(d => d.DriveType == DriveType.Removable && d.IsReady)
                .Select(d => d.RootDirectory.FullName.TrimEnd(Path.DirectorySeparatorChar))
                .Distinct()
                .ToList();

            if (removable.Count == 0)
            {
                // Si no hay pendrives detectados, intentamos abrir el selector nativo como fallback.
                var ruta = SeleccionarCarpetaNativa();
                if (string.IsNullOrWhiteSpace(ruta))
                {
                    return Json(new { exito = false, mensaje = "No se detectaron pendrives y no se seleccionó carpeta." });
                }

                AppPaths.SetConfiguredBackupDirectory(ruta);
                return Json(new { exito = true, ruta });
            }

            if (removable.Count == 1)
            {
                var candidata = Path.Combine(removable[0], "AutoSys_Backups");
                AppPaths.SetConfiguredBackupDirectory(candidata);
                return Json(new { exito = true, ruta = candidata });
            }

            // Si hay múltiples unidades removibles, devolvemos la lista para que el cliente elija.
            return Json(new { exito = true, multiples = true, drives = removable });
        }
        catch (Exception ex)
        {
            return Json(new { exito = false, mensaje = ex.Message });
        }
    }

    [HttpPost]
    public IActionResult GuardarRuta(string ruta)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(ruta))
            {
                return Json(new { exito = false, mensaje = "La ruta no puede estar vacía." });
            }

            AppPaths.SetConfiguredBackupDirectory(ruta.Trim());
            return Json(new { exito = true, ruta = ruta.Trim() });
        }
        catch (Exception ex)
        {
            return Json(new { exito = false, mensaje = ex.Message });
        }
    }

    [HttpPost]
    public IActionResult CrearBackupManual()
    {
        try
        {
            string dbPath = AppPaths.GetDbPath();
            if (!System.IO.File.Exists(dbPath))
            {
                return Json(new { exito = false, mensaje = "No se encontró la base de datos actual." });
            }

            var configured = AppPaths.GetConfiguredBackupDirectory();
            if (string.IsNullOrWhiteSpace(configured))
            {
                return Json(new { exito = false, mensaje = "No hay una ruta de backup configurada. Elegí una carpeta." });
            }

            if (!Directory.Exists(configured))
            {
                return Json(new { exito = false, mensaje = "La ruta configurada no está disponible. Conectá el pendrive." });
            }

            var root = Path.GetPathRoot(configured);
            if (string.IsNullOrWhiteSpace(root))
            {
                return Json(new { exito = false, mensaje = "No se pudo determinar la unidad del backup." });
            }

            var drive = new DriveInfo(root);
            if (drive.DriveType != DriveType.Removable || !drive.IsReady)
            {
                return Json(new { exito = false, mensaje = "La ruta configurada no es un pendrive conectado." });
            }

            string backupFolder = configured;
            Directory.CreateDirectory(backupFolder);

            string fecha = DateTime.Now.ToString("dd-MM-yyyy_HH-mm-ss");
            string destino = Path.Combine(backupFolder, $"taller_backup_{fecha}.db");

            using (var source = new SqliteConnection($"Data Source={dbPath}"))
            using (var destination = new SqliteConnection($"Data Source={destino}"))
            {
                source.Open();
                destination.Open();
                source.BackupDatabase(destination);
            }
            System.IO.File.SetLastWriteTime(destino, DateTime.Now);

            return Json(new { exito = true, mensaje = "Backup creado correctamente.", nombreArchivo = Path.GetFileName(destino) });
        }
        catch (Exception ex)
        {
            return Json(new { exito = false, mensaje = $"Error: {ex.Message}" });
        }
    }

    [HttpGet]
    public IActionResult Comparar(string nombreArchivo)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(nombreArchivo))
            {
                return Json(new { exito = false, mensaje = "Debes indicar un archivo de backup válido." });
            }

            var configured = AppPaths.GetConfiguredBackupDirectory();
            if (string.IsNullOrWhiteSpace(configured) || !Directory.Exists(configured))
            {
                return Json(new { exito = false, mensaje = "La ruta configurada no está disponible." });
            }

            var backups = ObtenerArchivosDbDisponibles(configured);
            var backup = backups.FirstOrDefault(b => b.NombreArchivo == nombreArchivo);
            if (backup == null || string.IsNullOrWhiteSpace(backup.Ruta))
            {
                return Json(new { exito = false, mensaje = "Backup no encontrado." });
            }

            string currentPath = AppPaths.GetDbPath();
            if (!System.IO.File.Exists(currentPath))
            {
                return Json(new { exito = false, mensaje = "No se encontró la base de datos actual." });
            }

            const int maxDetalles = 200;
            var result = CompararBackups(currentPath, backup.Ruta, maxDetalles);

            return Json(new
            {
                exito = true,
                resumen = result.Resumen,
                vehiculosSoloBackup = result.VehiculosSoloBackup,
                vehiculosSoloActual = result.VehiculosSoloActual,
                reparacionesPorVehiculo = result.ReparacionesPorVehiculo,
                reparacionesSoloBackup = result.ReparacionesSoloBackup,
                reparacionesSoloActual = result.ReparacionesSoloActual,
                limites = new { maxDetalles }
            });
        }
        catch (Exception ex)
        {
            return Json(new { exito = false, mensaje = $"Error: {ex.Message}" });
        }
    }

    private static string? SeleccionarCarpetaNativa()
    {
        string? rutaSeleccionada = null;
        using var evento = new ManualResetEvent(false);

        var hilo = new Thread(() =>
        {
            try
            {
                try
                {
                    using var dialog = new FolderBrowserDialog
                    {
                        Description = "Seleccioná la carpeta donde guardar los backups",
                        UseDescriptionForTitle = true,
                        ShowNewFolderButton = true,
                        SelectedPath = AppPaths.GetBackupDirectory()
                    };

                    if (dialog.ShowDialog() == DialogResult.OK)
                    {
                        rutaSeleccionada = dialog.SelectedPath;
                        return;
                    }
                }
                catch
                {
                }

                try
                {
                    using var openDialog = new OpenFileDialog
                    {
                        Title = "Seleccioná la carpeta donde guardar los backups",
                        CheckFileExists = false,
                        CheckPathExists = true,
                        ValidateNames = false,
                        FileName = "Seleccionar carpeta",
                        InitialDirectory = AppPaths.GetBackupDirectory()
                    };

                    if (openDialog.ShowDialog() == DialogResult.OK)
                    {
                        rutaSeleccionada = Path.GetDirectoryName(openDialog.FileName);
                    }
                }
                catch
                {
                }
            }
            finally
            {
                evento.Set();
            }
        });

        hilo.SetApartmentState(ApartmentState.STA);
        hilo.IsBackground = true;
        hilo.Start();
        evento.WaitOne();

        return rutaSeleccionada;
    }

    private static List<BackupInfo> ObtenerArchivosDbDisponibles(string backupFolder)
    {
        var lista = new List<BackupInfo>();

        try
        {
            if (!Directory.Exists(backupFolder)) return lista;

            var archivos = new DirectoryInfo(backupFolder)
                .GetFiles("*.db")
                .OrderByDescending(f => f.LastWriteTime);

            foreach (var archivo in archivos)
            {
                lista.Add(new BackupInfo
                {
                    NombreArchivo = archivo.Name,
                    Ruta = archivo.FullName,
                    Fecha = archivo.LastWriteTime,
                    TamanioMB = Math.Round((double)archivo.Length / (1024 * 1024), 2),
                    Ubicacion = backupFolder
                });
            }
        }
        catch { }

        return lista;
    }

    private static BackupCompareResult CompararBackups(string currentPath, string backupPath, int maxDetalles)
    {
        var resumen = new BackupCompareSummary();
        var vehiculosSoloBackup = new List<string>();
        var vehiculosSoloActual = new List<string>();
        var reparacionesPorVehiculo = new List<ReparacionDiff>();
        var reparacionesSoloBackup = new List<ReparacionDetalle>();
        var reparacionesSoloActual = new List<ReparacionDetalle>();

        var currentBuilder = new SqliteConnectionStringBuilder
        {
            DataSource = currentPath,
            Mode = SqliteOpenMode.ReadOnly,
            Cache = SqliteCacheMode.Shared
        };

        using var conn = new SqliteConnection(currentBuilder.ToString());
        conn.Open();

        using (var attach = conn.CreateCommand())
        {
            attach.CommandText = "ATTACH DATABASE $backup AS b;";
            attach.Parameters.AddWithValue("$backup", backupPath);
            attach.ExecuteNonQuery();
        }

        try
        {
            resumen.VehiculosActual = ExecuteScalarInt(conn, "SELECT COUNT(*) FROM Vehiculos;");
            resumen.VehiculosBackup = ExecuteScalarInt(conn, "SELECT COUNT(*) FROM b.Vehiculos;");
            resumen.ReparacionesActual = ExecuteScalarInt(conn, "SELECT COUNT(*) FROM Reparaciones;");
            resumen.ReparacionesBackup = ExecuteScalarInt(conn, "SELECT COUNT(*) FROM b.Reparaciones;");

            vehiculosSoloBackup = ExecuteStringList(conn,
                "SELECT Patente FROM b.Vehiculos EXCEPT SELECT Patente FROM Vehiculos ORDER BY Patente;");
            vehiculosSoloActual = ExecuteStringList(conn,
                "SELECT Patente FROM Vehiculos EXCEPT SELECT Patente FROM b.Vehiculos ORDER BY Patente;");

            var actualCounts = ExecuteReparacionCounts(conn,
                "SELECT Patente, COUNT(*) FROM Reparaciones GROUP BY Patente;");
            var backupCounts = ExecuteReparacionCounts(conn,
                "SELECT Patente, COUNT(*) FROM b.Reparaciones GROUP BY Patente;");

            var patentes = new HashSet<string>(actualCounts.Keys, StringComparer.OrdinalIgnoreCase);
            patentes.UnionWith(backupCounts.Keys);

            foreach (var patente in patentes)
            {
                actualCounts.TryGetValue(patente, out int actual);
                backupCounts.TryGetValue(patente, out int backup);
                if (actual != backup)
                {
                    reparacionesPorVehiculo.Add(new ReparacionDiff
                    {
                        Patente = patente,
                        Actual = actual,
                        Backup = backup,
                        Diferencia = backup - actual
                    });
                }
            }

            reparacionesPorVehiculo = reparacionesPorVehiculo
                .OrderByDescending(r => Math.Abs(r.Diferencia))
                .ThenBy(r => r.Patente, StringComparer.OrdinalIgnoreCase)
                .ToList();

            reparacionesSoloBackup = ExecuteReparaciones(conn,
                "SELECT Patente, Fecha, Kilometraje, Detalle FROM (" +
                "SELECT Patente, Fecha, Kilometraje, Detalle FROM b.Reparaciones " +
                "EXCEPT SELECT Patente, Fecha, Kilometraje, Detalle FROM Reparaciones" +
                ") ORDER BY Patente, Fecha LIMIT $limit;",
                maxDetalles);

            reparacionesSoloActual = ExecuteReparaciones(conn,
                "SELECT Patente, Fecha, Kilometraje, Detalle FROM (" +
                "SELECT Patente, Fecha, Kilometraje, Detalle FROM Reparaciones " +
                "EXCEPT SELECT Patente, Fecha, Kilometraje, Detalle FROM b.Reparaciones" +
                ") ORDER BY Patente, Fecha LIMIT $limit;",
                maxDetalles);
        }
        finally
        {
            using var detach = conn.CreateCommand();
            detach.CommandText = "DETACH DATABASE b;";
            detach.ExecuteNonQuery();
        }

        return new BackupCompareResult
        {
            Resumen = resumen,
            VehiculosSoloBackup = vehiculosSoloBackup,
            VehiculosSoloActual = vehiculosSoloActual,
            ReparacionesPorVehiculo = reparacionesPorVehiculo,
            ReparacionesSoloBackup = reparacionesSoloBackup,
            ReparacionesSoloActual = reparacionesSoloActual
        };
    }

    private static int ExecuteScalarInt(SqliteConnection conn, string sql)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    private static List<string> ExecuteStringList(SqliteConnection conn, string sql)
    {
        var items = new List<string>();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            items.Add(reader.GetString(0));
        }
        return items;
    }

    private static Dictionary<string, int> ExecuteReparacionCounts(SqliteConnection conn, string sql)
    {
        var items = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var patente = reader.GetString(0);
            var count = reader.GetInt32(1);
            items[patente] = count;
        }
        return items;
    }

    private static List<ReparacionDetalle> ExecuteReparaciones(SqliteConnection conn, string sql, int limit)
    {
        var items = new List<ReparacionDetalle>();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("$limit", limit);
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            items.Add(new ReparacionDetalle
            {
                Patente = reader.GetString(0),
                Fecha = reader.GetValue(1)?.ToString() ?? string.Empty,
                Kilometraje = reader.GetInt32(2),
                Detalle = reader.GetValue(3)?.ToString() ?? string.Empty
            });
        }
        return items;
    }

    [HttpPost]
    public IActionResult Restaurar(string nombreArchivo)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(nombreArchivo))
            {
                return Json(new { exito = false, mensaje = "Debes indicar un archivo de backup válido." });
            }

            var backupFolder = AppPaths.GetBackupDirectory();
            var backups = ObtenerArchivosDbDisponibles(backupFolder);
            var backup = backups.FirstOrDefault(b => b.NombreArchivo == nombreArchivo);

            if (backup == null || string.IsNullOrWhiteSpace(backup.Ruta))
                return Json(new { exito = false, mensaje = "Backup no encontrado" });

            string dbPath = AppPaths.GetDbPath();
            string backupPath = backup.Ruta;

            // 1. Crear backup del actual antes de restaurar
            if (System.IO.File.Exists(dbPath))
            {
                string backupSeguridad = $"{dbPath}.backup_{DateTime.Now:yyyyMMdd_HHmmss}";
                System.IO.File.Copy(dbPath, backupSeguridad, true);
                if (System.IO.File.Exists(dbPath + "-wal")) System.IO.File.Copy(dbPath + "-wal", backupSeguridad + "-wal", true);
                if (System.IO.File.Exists(dbPath + "-shm")) System.IO.File.Copy(dbPath + "-shm", backupSeguridad + "-shm", true);
            }

            // 2. Asegurar que no haya conexiones activas al archivo actual
            _context.ChangeTracker.Clear();
            var dbConnection = _context.Database.GetDbConnection();
            if (dbConnection.State != ConnectionState.Closed)
            {
                dbConnection.Close();
            }
            SqliteConnection.ClearAllPools();

            // Borrar archivos temporales de WAL si existen
            if (System.IO.File.Exists(dbPath + "-wal")) System.IO.File.Delete(dbPath + "-wal");
            if (System.IO.File.Exists(dbPath + "-shm")) System.IO.File.Delete(dbPath + "-shm");

            // 3. Copiar el backup seleccionado
            System.IO.File.Copy(backupPath, dbPath, true);

            // 4. Limpiar pools otra vez para forzar relectura
            SqliteConnection.ClearAllPools();

            // 5. Ahora la app debería reabrir la conexión con el archivo restaurado
            TempData["Success"] = $"✅ Backup restaurado correctamente desde {backup.Fecha:dd/MM/yyyy HH:mm}";

            return Json(new { exito = true, mensaje = "Backup restaurado. Refresca la página." });
        }
        catch (Exception ex)
        {
            return Json(new { exito = false, mensaje = $"Error: {ex.Message}" });
        }
    }

    public class BackupInfo
    {
        public string? NombreArchivo { get; set; }
        public string? Ruta { get; set; }
        public DateTime Fecha { get; set; }
        public double TamanioMB { get; set; }
        public string? Ubicacion { get; set; }
    }

    public class BackupIndexViewModel
    {
        public string RutaActual { get; set; } = string.Empty;
        public bool RutaDisponible { get; set; }
        public List<BackupInfo> Backups { get; set; } = new();
    }

    private sealed class BackupCompareSummary
    {
        public int VehiculosActual { get; set; }
        public int VehiculosBackup { get; set; }
        public int ReparacionesActual { get; set; }
        public int ReparacionesBackup { get; set; }
    }

    private sealed class ReparacionDiff
    {
        public string Patente { get; set; } = string.Empty;
        public int Actual { get; set; }
        public int Backup { get; set; }
        public int Diferencia { get; set; }
    }

    private sealed class ReparacionDetalle
    {
        public string Patente { get; set; } = string.Empty;
        public string Fecha { get; set; } = string.Empty;
        public int Kilometraje { get; set; }
        public string Detalle { get; set; } = string.Empty;
    }

    private sealed class BackupCompareResult
    {
        public BackupCompareSummary Resumen { get; set; } = new();
        public List<string> VehiculosSoloBackup { get; set; } = new();
        public List<string> VehiculosSoloActual { get; set; } = new();
        public List<ReparacionDiff> ReparacionesPorVehiculo { get; set; } = new();
        public List<ReparacionDetalle> ReparacionesSoloBackup { get; set; } = new();
        public List<ReparacionDetalle> ReparacionesSoloActual { get; set; } = new();
    }
}
