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
}