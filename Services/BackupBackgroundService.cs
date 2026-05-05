using Microsoft.Extensions.Hosting;
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Taller.Infrastructure;

namespace Taller 
{
    public class BackupBackgroundService : BackgroundService
    {
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // AL ARRANCAR: Esperamos unos segundos para no estorbar la pantalla de carga
            await Task.Delay(8000, stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                // 1. ¿Toca hacer backup hoy?
                if (NecesitaBackup())
                {
                    // 2. Si toca, buscamos el pendrive y lo intentamos
                    bool exito = RealizarBackupSoloEnPendrive();

                    if (exito)
                    {
                        // Si se hizo bien, esperamos 1 hora para el próximo chequeo
                        // (aunque el NecesitaBackup dará falso por los próximos 7 días)
                        await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
                    }
                    else
                    {
                        // Si NO se pudo (porque no estaba el pendrive), 
                        // reintentamos más seguido, por ejemplo cada 30 minutos,
                        // para "atrapar" el momento en que lo conecten.
                        await Task.Delay(TimeSpan.FromMinutes(30), stoppingToken);
                    }
                }
                else
                {
                    // Si no toca hacer backup, chequeamos cada 4 horas solo por rutina
                    await Task.Delay(TimeSpan.FromHours(4), stoppingToken);
                }
            }
        }

        private bool NecesitaBackup()
        {
            string configPath = AppPaths.GetBackupMarkerPath();
            
            // Si el archivo no existe, es la primera vez o lo borraron: toca backup.
            if (!File.Exists(configPath)) return true;

            if (DateTime.TryParse(File.ReadAllText(configPath), out DateTime ultimaFecha))
            {
                // ¿Pasaron 7 días o más?
                return (DateTime.Now - ultimaFecha).TotalDays >= 7;
            }
            
            return true; 
        }

        private bool RealizarBackupSoloEnPendrive()
        {
            try
            {
                string dbPath = AppPaths.GetDbPath();
                string configPath = AppPaths.GetBackupMarkerPath();

                if (!File.Exists(dbPath)) return false;

                // Buscamos ÚNICAMENTE unidades que Windows marque como Removibles (Pendrives)
                var pendrive = DriveInfo.GetDrives()
                    .FirstOrDefault(d => d.DriveType == DriveType.Removable && d.IsReady);

                if (pendrive != null)
                {
                    string backupFolder = Path.Combine(pendrive.RootDirectory.FullName, "AutoSys_Backups");
                    if (!Directory.Exists(backupFolder)) Directory.CreateDirectory(backupFolder);

                    string fecha = DateTime.Now.ToString("dd-MM-yyyy_HH-mm");
                    string destino = Path.Combine(backupFolder, $"taller_backup_{fecha}.db");

                    File.Copy(dbPath, destino, true);
                    File.SetLastWriteTime(destino, DateTime.Now); 

                    // SOLO si la copia fue exitosa, actualizamos el archivo de fecha
                    File.WriteAllText(configPath, DateTime.Now.ToString("O"));
                    return true;
                }
                
                return false; // No se encontró pendrive
            }
            catch
            {
                return false; // Error (permisos, desconexión repentina, etc.)
            }
        }
    }
}