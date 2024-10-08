using System.Diagnostics;
using UniGetUI.Core.Logging;
using UniGetUI.PackageEngine.Enums;
using UniGetUI.PackageEngine.Interfaces;
using UniGetUI.PackageEngine.ManagerClasses.Classes;
using UniGetUI.PackageEngine.PackageClasses;

namespace UniGetUI.PackageEngine.Managers.WingetManager;

internal static class BundledWinGetLegacyMethods
{
    public static IEnumerable<Package> FindPackages_UnSafe(WinGet Manager, string query)
    {
        List<Package> Packages = [];
        Process p = new()
        {
            StartInfo = new()
            {
                FileName = Manager.WinGetBundledPath,
                Arguments = Manager.Properties.ExecutableCallArgs + " search \"" + query +
                            "\"  --accept-source-agreements",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = System.Text.Encoding.UTF8
            }
        };

        IProcessTaskLogger logger = Manager.TaskLogger.CreateNew(LoggableTaskType.FindPackages, p);

        p.Start();

        string OldLine = "";
        int IdIndex = -1;
        int VersionIndex = -1;
        int SourceIndex = -1;
        bool DashesPassed = false;
        string? line;
        while ((line = p.StandardOutput.ReadLine()) is not null)
        {
            logger.AddToStdOut(line);
            if (!DashesPassed && line.Contains("---"))
            {
                string HeaderPrefix = OldLine.Contains("SearchId") ? "Search" : "";
                IdIndex = OldLine.IndexOf(HeaderPrefix + "Id", StringComparison.InvariantCulture);
                VersionIndex = OldLine.IndexOf(HeaderPrefix + "Version", StringComparison.InvariantCulture);
                SourceIndex = OldLine.IndexOf(HeaderPrefix + "Source", StringComparison.InvariantCulture);
                DashesPassed = true;
            }
            else if (DashesPassed && IdIndex > 0 && VersionIndex > 0 && IdIndex < VersionIndex && VersionIndex < line.Length)
            {
                int offset = 0; // Account for non-unicode character length
                while (line[IdIndex - offset - 1] != ' ' || offset > (IdIndex - 5))
                {
                    offset++;
                }

                string name = line[..(IdIndex - offset)].Trim();
                string id = line[(IdIndex - offset)..].Trim().Split(' ')[0];
                string version = line[(VersionIndex - offset)..].Trim().Split(' ')[0];
                IManagerSource source;
                if (SourceIndex == -1 || SourceIndex >= line.Length)
                {
                    source = Manager.DefaultSource;
                }
                else
                {
                    string sourceName = line[(SourceIndex - offset)..].Trim().Split(' ')[0];
                    source = Manager.GetSourceOrDefault(sourceName);
                }
                Packages.Add(new Package(name, id, version, source, Manager));
            }
            OldLine = line;
        }

        logger.AddToStdErr(p.StandardError.ReadToEnd());
        p.WaitForExit();
        logger.Close(p.ExitCode);

        return Packages;
    }

    public static IEnumerable<Package> GetAvailableUpdates_UnSafe(WinGet Manager)
    {
        List<Package> Packages = [];
        Process p = new()
        {
            StartInfo = new()
            {
                FileName = Manager.WinGetBundledPath,
                Arguments = Manager.Properties.ExecutableCallArgs +
                            " update --include-unknown  --accept-source-agreements",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = System.Text.Encoding.UTF8
            }
        };

        IProcessTaskLogger logger = Manager.TaskLogger.CreateNew(LoggableTaskType.ListUpdates, p);

        p.Start();

        string OldLine = "";
        int IdIndex = -1;
        int VersionIndex = -1;
        int NewVersionIndex = -1;
        int SourceIndex = -1;
        bool DashesPassed = false;
        string? line;
        while ((line = p.StandardOutput.ReadLine()) is not null)
        {
            logger.AddToStdOut(line);

            if (line.Contains("have pins"))
            {
                continue;
            }

            if (!DashesPassed && line.Contains("---"))
            {
                string HeaderPrefix = OldLine.Contains("SearchId") ? "Search" : "";
                string HeaderSuffix = OldLine.Contains("SearchId") ? "Header" : "";
                IdIndex = OldLine.IndexOf(HeaderPrefix + "Id", StringComparison.InvariantCulture);
                VersionIndex = OldLine.IndexOf(HeaderPrefix + "Version", StringComparison.InvariantCulture);
                NewVersionIndex = OldLine.IndexOf("Available" + HeaderSuffix, StringComparison.InvariantCulture);
                SourceIndex = OldLine.IndexOf(HeaderPrefix + "Source", StringComparison.InvariantCulture);
                DashesPassed = true;
            }
            else if (line.Trim() == "")
            {
                DashesPassed = false;
            }
            else if (DashesPassed && IdIndex > 0 && VersionIndex > 0 && NewVersionIndex > 0 && IdIndex < VersionIndex && VersionIndex < NewVersionIndex && NewVersionIndex < line.Length)
            {
                int offset = 0; // Account for non-unicode character length
                while (line[IdIndex - offset - 1] != ' ' || offset > (IdIndex - 5))
                {
                    offset++;
                }

                string name = line[..(IdIndex - offset)].Trim();
                string id = line[(IdIndex - offset)..].Trim().Split(' ')[0];
                string version = line[(VersionIndex - offset)..(NewVersionIndex - offset)].Trim();
                string newVersion;
                if (SourceIndex != -1)
                {
                    newVersion = line[(NewVersionIndex - offset)..(SourceIndex - offset)].Trim();
                }
                else
                {
                    newVersion = line[(NewVersionIndex - offset)..].Trim().Split(' ')[0];
                }

                IManagerSource source;
                if (SourceIndex == -1 || SourceIndex >= line.Length)
                {
                    source = Manager.DefaultSource;
                }
                else
                {
                    string sourceName = line[(SourceIndex - offset)..].Trim().Split(' ')[0];
                    source = Manager.GetSourceOrDefault(sourceName);
                }

                Packages.Add(new Package(name, id, version, newVersion, source, Manager));
            }
            OldLine = line;
        }

        logger.AddToStdErr(p.StandardError.ReadToEnd());
        p.WaitForExit();
        logger.Close(p.ExitCode);

        return Packages;
    }

    public static IEnumerable<Package> GetInstalledPackages_UnSafe(WinGet Manager)
    {
        List<Package> Packages = [];
        Process p = new()
        {
            StartInfo = new()
            {
                FileName = Manager.WinGetBundledPath,
                Arguments = Manager.Properties.ExecutableCallArgs + " list  --accept-source-agreements",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = System.Text.Encoding.UTF8
            }
        };

        IProcessTaskLogger logger = Manager.TaskLogger.CreateNew(LoggableTaskType.ListInstalledPackages, p);

        p.Start();

        string OldLine = "";
        int IdIndex = -1;
        int VersionIndex = -1;
        int SourceIndex = -1;
        int NewVersionIndex = -1;
        bool DashesPassed = false;
        string? line;
        while ((line = p.StandardOutput.ReadLine()) is not null)
        {
            try
            {
                logger.AddToStdOut(line);
                if (!DashesPassed && line.Contains("---"))
                {
                    string HeaderPrefix = OldLine.Contains("SearchId") ? "Search" : "";
                    string HeaderSuffix = OldLine.Contains("SearchId") ? "Header" : "";
                    IdIndex = OldLine.IndexOf(HeaderPrefix + "Id", StringComparison.InvariantCulture);
                    VersionIndex = OldLine.IndexOf(HeaderPrefix + "Version", StringComparison.InvariantCulture);
                    NewVersionIndex = OldLine.IndexOf("Available" + HeaderSuffix, StringComparison.InvariantCulture);
                    SourceIndex = OldLine.IndexOf(HeaderPrefix + "Source", StringComparison.InvariantCulture);
                    DashesPassed = true;
                }
                else if (DashesPassed && IdIndex > 0 && VersionIndex > 0 && IdIndex < VersionIndex && VersionIndex < line.Length)
                {
                    int offset = 0; // Account for non-unicode character length
                    while (((IdIndex - offset) <= line.Length && line[IdIndex - offset - 1] != ' ') || offset > (IdIndex - 5))
                    {
                        offset++;
                    }

                    string name = line[..(IdIndex - offset)].Trim();
                    string id = line[(IdIndex - offset)..].Trim().Split(' ')[0];
                    if (NewVersionIndex == -1 && SourceIndex != -1)
                    {
                        NewVersionIndex = SourceIndex;
                    }
                    else if (NewVersionIndex == -1 && SourceIndex == -1)
                    {
                        NewVersionIndex = line.Length - 1;
                    }

                    string version = line[(VersionIndex - offset)..(NewVersionIndex - offset)].Trim();

                    IManagerSource source;
                    if (SourceIndex == -1 || (SourceIndex - offset) >= line.Length)
                    {
                        source = GetLocalSource(Manager, id); // Load Winget Local Sources
                    }
                    else
                    {
                        string sourceName = line[(SourceIndex - offset)..].Trim().Split(' ')[0].Trim();
                        source = Manager.GetSourceOrDefault(sourceName);
                    }
                    Packages.Add(new Package(name, id, version, source, Manager));
                }
                OldLine = line;
            }
            catch (Exception e)
            {
                Logger.Error(e);
            }
        }

        logger.AddToStdErr(p.StandardError.ReadToEnd());
        p.WaitForExit();
        logger.Close(p.ExitCode);

        return Packages;
    }

    private static IManagerSource GetLocalSource(WinGet Manager, string id)
    {
        try
        {
            // Check if source is android
            bool AndroidValid = true;
            foreach (char c in id)
            {
                if (!"abcdefghijklmnopqrstuvwxyz.…".Contains(c))
                {
                    AndroidValid = false;
                    break;
                }
            }

            if (AndroidValid && id.Count(x => x == '.') >= 2)
            {
                return Manager.AndroidSubsystemSource;
            }

            // Check if source is Steam
            if ((id == "Steam" || id.Contains("Steam App ")) && id.Split("Steam App").Length >= 2 && !id.Split("Steam App")[1].Trim().Any(x => !"1234567890".Contains(x)))
            {
                return Manager.SteamSource;
            }

            // Check if source is Ubisoft Connect
            if (id == "Uplay" || id.Contains("Uplay Install ") && id.Split("Uplay Install").Length >= 2 && !id.Split("Uplay Install")[1].Trim().Any(x => !"1234567890".Contains(x)))
            {
                return Manager.UbisoftConnectSource;
            }

            // Check if source is GOG
            if (id.EndsWith("_is1") && !id.Split("_is1")[0].Any(x => !"1234567890".Contains(x)))
            {
                return Manager.GOGSource;
            }

            // Check if source is Microsoft Store
            if (id.Count(x => x == '_') == 1 && (id.Split('_')[^1].Length == 14 | id.Split('_')[^1].Length == 13 | id.Split('_')[^1].Length <= 13 && id[^1] == '…'))
            {
                return Manager.MicrosoftStoreSource;
            }

            return Manager.LocalPcSource;
        }
        catch (Exception ex)
        {
            Logger.Error(ex);
            return Manager.LocalPcSource;
        }
    }
}
