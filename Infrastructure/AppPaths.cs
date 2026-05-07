using System;
using System.IO;

namespace Taller.Infrastructure;

public static class AppPaths
{
    public static string GetOrCreateDataDirectory()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "AutoSys"
        );

        Directory.CreateDirectory(dir);
        return dir;
    }

    public static string GetDbPath()
    {
        return Path.Combine(GetOrCreateDataDirectory(), "taller.db");
    }

    public static string GetBackupMarkerPath()
    {
        return Path.Combine(GetOrCreateDataDirectory(), "ultimo_backup.txt");
    }

    public static string GetBackupPathConfigPath()
    {
        return Path.Combine(GetOrCreateDataDirectory(), "backup_path.txt");
    }

    public static string GetMigrationPathConfigPath()
    {
        return Path.Combine(GetOrCreateDataDirectory(), "migration_path.txt");
    }

    public static string GetDefaultBackupDirectory()
    {
        return Path.Combine(GetOrCreateDataDirectory(), "Backups");
    }

    public static string? GetConfiguredBackupDirectory()
    {
        var configPath = GetBackupPathConfigPath();
        if (!File.Exists(configPath)) return null;

        var path = File.ReadAllText(configPath).Trim();
        return string.IsNullOrWhiteSpace(path) ? null : path;
    }

    public static void SetConfiguredBackupDirectory(string backupDirectory)
    {
        if (string.IsNullOrWhiteSpace(backupDirectory))
        {
            throw new ArgumentException("La ruta de backup no puede estar vacía.", nameof(backupDirectory));
        }

        Directory.CreateDirectory(backupDirectory);
        File.WriteAllText(GetBackupPathConfigPath(), backupDirectory.Trim());
    }

    public static string GetBackupDirectory()
    {
        var configured = GetConfiguredBackupDirectory();
        if (!string.IsNullOrWhiteSpace(configured) && Directory.Exists(configured))
        {
            Directory.CreateDirectory(configured);
            return configured;
        }

        foreach (var drive in DriveInfo.GetDrives())
        {
            try
            {
                if (drive.DriveType == DriveType.Removable && drive.IsReady)
                {
                    var candidata = Path.Combine(drive.RootDirectory.FullName, "AutoSys_Backups");
                    Directory.CreateDirectory(candidata);
                    return candidata;
                }
            }
            catch
            {
                // Ignorar unidades inaccesibles.
            }
        }

        var defaultDirectory = GetDefaultBackupDirectory();
        Directory.CreateDirectory(defaultDirectory);
        return defaultDirectory;
    }

    public static string? GetConfiguredMigrationDirectory()
    {
        var configPath = GetMigrationPathConfigPath();
        if (!File.Exists(configPath)) return null;

        var path = File.ReadAllText(configPath).Trim();
        return string.IsNullOrWhiteSpace(path) ? null : path;
    }

    public static void SetConfiguredMigrationDirectory(string migrationDirectory)
    {
        if (string.IsNullOrWhiteSpace(migrationDirectory))
        {
            throw new ArgumentException("La ruta de migración no puede estar vacía.", nameof(migrationDirectory));
        }

        Directory.CreateDirectory(migrationDirectory);
        File.WriteAllText(GetMigrationPathConfigPath(), migrationDirectory.Trim());
    }

    public static string GetMigrationDirectory()
    {
        var configured = GetConfiguredMigrationDirectory();
        if (!string.IsNullOrWhiteSpace(configured) && Directory.Exists(configured))
        {
            return configured;
        }

        foreach (var drive in DriveInfo.GetDrives())
        {
            try
            {
                if (drive.DriveType == DriveType.Removable && drive.IsReady)
                {
                    var candidata = Path.Combine(drive.RootDirectory.FullName, "Fichas");
                    if (Directory.Exists(candidata))
                    {
                        return candidata;
                    }
                }
            }
            catch
            {
                // Ignorar unidades inaccesibles.
            }
        }

        return @"E:\Fichas";
    }
}