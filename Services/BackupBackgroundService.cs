using Microsoft.Extensions.Hosting;
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Taller // Cambiá esto si tu namespace es distinto
{
    public class BackupBackgroundService : BackgroundService
    {
        private readonly string dbPath = "taller.db";
        private readonly string configPath = "ultimo_backup.txt";

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Este bucle corre silenciosamente mientras el programa esté abierto
            while (!stoppingToken.IsCancellationRequested)
            {
                if (NecesitaBackup())
                {
                    RealizarBackupSiHayPendrive();
                }

                // Si lo hizo con éxito, o si no encontró el pendrive, 
                // se va a dormir 1 hora y luego vuelve a probar.
                // IMPORTANTE: Cuando cierren el programa y lo vuelvan a abrir mañana,
                // este ciclo arranca de cero y prueba INSTANTÁNEAMENTE al inicio.
                await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
            }
        }

        private bool NecesitaBackup()
        {
            // Si el archivo no existe, significa que es la primera vez en la vida que se corre
            if (!File.Exists(configPath)) return true;

            // Leemos la fecha del archivo .txt
            if (DateTime.TryParse(File.ReadAllText(configPath), out DateTime ultimaFecha))
            {
                // ¿Pasaron 7 días o más?
                return (DateTime.Now - ultimaFecha).TotalDays >= 7;
            }
            
            // Si el archivo de texto se corrompió o alguien lo editó mal, forzamos backup
            return true; 
        }

        private void RealizarBackupSiHayPendrive()
        {
            try
            {
                // Busca todas las unidades conectadas que sean del tipo "Removibles" (Pendrives/Discos USB)
                var pendrive = DriveInfo.GetDrives()
                    .FirstOrDefault(d => d.DriveType == DriveType.Removable && d.IsReady);

                if (pendrive != null)
                {
                    // Crea una carpeta prolija adentro del pendrive si no existe
                    string backupFolder = Path.Combine(pendrive.RootDirectory.FullName, "AutoSys_Backups");
                    if (!Directory.Exists(backupFolder))
                    {
                        Directory.CreateDirectory(backupFolder);
                    }

                    // Genera el nombre con la fecha actual
                    string fecha = DateTime.Now.ToString("dd-MM-yyyy_HH-mm");
                    string destino = Path.Combine(backupFolder, $"taller_backup_{fecha}.db");

                    // Copia la base de datos de la PC al pendrive
                    File.Copy(dbPath, destino, true);

                    // Escribe en el archivito de texto la fecha de hoy, así no vuelve a joder por 7 días
                    File.WriteAllText(configPath, DateTime.Now.ToString());
                }
            }
            catch
            {
                // Si justo desconectaron el pendrive mientras copiaba o hay un error, 
                // no hacemos que el sistema explote. Lo ignora y volverá a probar en la próxima hora.
            }
        }
    }
}