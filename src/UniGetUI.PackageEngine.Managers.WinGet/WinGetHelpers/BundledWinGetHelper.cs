using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using UniGetUI.Core.Data;
using UniGetUI.Core.Logging;
using UniGetUI.Core.SettingsEngine;
using UniGetUI.Core.Tools;
using UniGetUI.PackageEngine.Classes.Manager;
using UniGetUI.PackageEngine.Enums;
using UniGetUI.PackageEngine.Interfaces;
using UniGetUI.PackageEngine.ManagerClasses.Classes;
using UniGetUI.PackageEngine.PackageClasses;

namespace UniGetUI.PackageEngine.Managers.WingetManager;

internal sealed class BundledWinGetHelper : IWinGetManagerHelper
{
    public IEnumerable<Package> GetAvailableUpdates_UnSafe(WinGet Manager)
    {
        if (Settings.Get("ForceLegacyBundledWinGet"))
        {
            return BundledWinGetLegacyMethods.GetAvailableUpdates_UnSafe(Manager);
        }

        List<Package> Packages = [];

        Process p = new()
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = "/C " + Manager.PowerShellPath + " " + Manager.PowerShellPromptArgs,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = true,
                CreateNoWindow = true,
                StandardOutputEncoding = CodePagesEncodingProvider.Instance.GetEncoding(CoreData.CODE_PAGE),
                StandardInputEncoding = new UTF8Encoding(false),
                StandardErrorEncoding = CodePagesEncodingProvider.Instance.GetEncoding(CoreData.CODE_PAGE),
            }
        };

        IProcessTaskLogger logger = Manager.TaskLogger.CreateNew(LoggableTaskType.ListUpdates, p);

        p.Start();

        string command = """
                         Write-Output (Get-Module -Name Microsoft.WinGet.Client).Version
                         Import-Module Microsoft.WinGet.Client
                         function Print-WinGetPackage {
                             param (
                                 [Parameter(Mandatory,ValueFromPipelineByPropertyName)] [string] $Name,
                                 [Parameter(Mandatory,ValueFromPipelineByPropertyName)] [string] $Id,
                                 [Parameter(Mandatory,ValueFromPipelineByPropertyName)] [string] $InstalledVersion,
                                 [Parameter(ValueFromPipelineByPropertyName)] [string[]] $AvailableVersions,
                                 [Parameter(Mandatory,ValueFromPipelineByPropertyName)] [bool] $IsUpdateAvailable,
                                 [Parameter(ValueFromPipelineByPropertyName)] [string] $Source
                             )
                             process {
                                 if ($IsUpdateAvailable)
                                 {
                                     Write-Output("#" + $Name + "`t" + $Id + "`t" + $InstalledVersion + "`t" + $AvailableVersions[0] + "`t" + $Source)
                                 }
                             }
                         }

                         Get-WinGetPackage | Print-WinGetPackage

                         exit

                         """;

        p.StandardInput.Write(command);
        p.StandardInput.Close();
        logger.AddToStdIn(command);

        string? line;
        while ((line = p.StandardOutput.ReadLine()) is not null)
        {
            logger.AddToStdOut(line);
            if (!line.StartsWith("#"))
            {
                continue; // The PowerShell script appends a '#' to the beginning of each line to identify the output
            }

            string[] elements = line.Split('\t');
            if (elements.Length < 5)
            {
                continue;
            }

            IManagerSource source = Manager.GetSourceOrDefault(elements[4]);

            Packages.Add(new Package(elements[0][1..], elements[1], elements[2], elements[3], source, Manager));
        }

        logger.AddToStdErr(p.StandardError.ReadToEnd());
        p.WaitForExit();
        logger.Close(p.ExitCode);

        if (Packages.Count > 0)
        {
            return Packages;
        }

        Logger.Warn("WinGet updates returned zero packages, attempting legacy...");
        return BundledWinGetLegacyMethods.GetAvailableUpdates_UnSafe(Manager);
    }

    public IEnumerable<Package> GetInstalledPackages_UnSafe(WinGet Manager)
    {
        if (Settings.Get("ForceLegacyBundledWinGet"))
        {
            return BundledWinGetLegacyMethods.GetInstalledPackages_UnSafe(Manager);
        }

        List<Package> Packages = [];
        Process p = new()
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = "/C " + Manager.PowerShellPath + " " + Manager.PowerShellPromptArgs,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = true,
                CreateNoWindow = true,
                StandardOutputEncoding = CodePagesEncodingProvider.Instance.GetEncoding(CoreData.CODE_PAGE),
                StandardInputEncoding = new UTF8Encoding(false),
                StandardErrorEncoding = CodePagesEncodingProvider.Instance.GetEncoding(CoreData.CODE_PAGE),
            }
        };

        IProcessTaskLogger logger = Manager.TaskLogger.CreateNew(LoggableTaskType.ListInstalledPackages, p);
        p.Start();

        string command = """
                         Write-Output (Get-Module -Name Microsoft.WinGet.Client).Version
                         Import-Module Microsoft.WinGet.Client
                         function Print-WinGetPackage {
                             param (
                                 [Parameter(Mandatory,ValueFromPipelineByPropertyName)] [string] $Name,
                                 [Parameter(Mandatory,ValueFromPipelineByPropertyName)] [string] $Id,
                                 [Parameter(Mandatory,ValueFromPipelineByPropertyName)] [string] $InstalledVersion,
                                 [Parameter(ValueFromPipelineByPropertyName)] [string[]] $AvailableVersions,
                                 [Parameter(Mandatory,ValueFromPipelineByPropertyName)] [bool] $IsUpdateAvailable,
                                 [Parameter(ValueFromPipelineByPropertyName)] [string] $Source
                             )
                             process {
                                 Write-Output("#" + $Name + "`t" + $Id + "`t" + $InstalledVersion + "`t" + $Source)
                             }
                         }

                         Get-WinGetPackage | Print-WinGetPackage


                         exit

                         """;

        p.StandardInput.Write(command);
        p.StandardInput.Close();
        logger.AddToStdIn(command);

        string? line;
        while ((line = p.StandardOutput.ReadLine()) is not null)
        {
            logger.AddToStdOut(line);
            if (!line.StartsWith("#"))
            {
                continue; // The PowerShell script appends a '#' to the beginning of each line to identify the output
            }

            string[] elements = line.Split('\t');
            if (elements.Length < 4)
            {
                continue;
            }

            IManagerSource source;
            if (elements[3] != "")
            {
                source = Manager.GetSourceOrDefault(elements[3]);
            }
            else
            {
                source = Manager.GetLocalSource(elements[1]);
            }

            Packages.Add(new Package(elements[0][1..], elements[1], elements[2], source, Manager));
        }

        logger.AddToStdErr(p.StandardError.ReadToEnd());
        p.WaitForExit();
        logger.Close(p.ExitCode);

        if (Packages.Count > 0)
        {
            return Packages;
        }

        Logger.Warn("WinGet installed packages returned zero packages, attempting legacy...");
        return BundledWinGetLegacyMethods.GetInstalledPackages_UnSafe(Manager);
    }

    public IEnumerable<Package> FindPackages_UnSafe(WinGet Manager, string query)
    {
        if (Settings.Get("ForceLegacyBundledWinGet"))
        {
            return BundledWinGetLegacyMethods.FindPackages_UnSafe(Manager, query);
        }

        List<Package> Packages = [];

        Process p = new()
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = "/C " + Manager.PowerShellPath + " " + Manager.PowerShellPromptArgs,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = true,
                CreateNoWindow = true,
                StandardOutputEncoding = CodePagesEncodingProvider.Instance.GetEncoding(CoreData.CODE_PAGE),
                StandardInputEncoding = new UTF8Encoding(false),
                StandardErrorEncoding = CodePagesEncodingProvider.Instance.GetEncoding(CoreData.CODE_PAGE),
            }
        };

        p.Start();

        IProcessTaskLogger logger = Manager.TaskLogger.CreateNew(LoggableTaskType.FindPackages, p);

        string command = """
                         Write-Output (Get-Module -Name Microsoft.WinGet.Client).Version
                         Import-Module Microsoft.WinGet.Client
                         function Print-WinGetPackage {
                             param (
                                 [Parameter(Mandatory,ValueFromPipelineByPropertyName)] [string] $Name,
                                 [Parameter(Mandatory,ValueFromPipelineByPropertyName)] [string] $Id,
                                 [Parameter(ValueFromPipelineByPropertyName)] [string[]] $AvailableVersions,
                                 [Parameter(ValueFromPipelineByPropertyName)] [string] $Source
                             )
                             process {
                                 Write-Output(""#"" + $Name + ""`t"" + $Id + ""`t"" + $AvailableVersions[0] + ""`t"" + $Source)
                             }
                         }

                         Find-WinGetPackage -Query {query} | Print-WinGetPackage

                         exit


                         """;

        p.StandardInput.Write(command);
        p.StandardInput.Close();
        logger.AddToStdIn(command);

        string? line;
        while ((line = p.StandardOutput.ReadLine()) is not null)
        {
            logger.AddToStdOut(line);
            if (!line.StartsWith("#"))
            {
                continue; // The PowerShell script appends a '#' to the beginning of each line to identify the output
            }

            string[] elements = line.Split('\t');
            if (elements.Length < 4)
            {
                continue;
            }

            IManagerSource source = Manager.GetSourceOrDefault(elements[3]);

            Packages.Add(new Package(elements[0][1..], elements[1], elements[2], source, Manager));
        }

        logger.AddToStdErr(p.StandardError.ReadToEnd());
        p.WaitForExit();
        logger.Close(p.ExitCode);

        if (Packages.Count > 0)
        {
            return Packages;
        }

        Logger.Warn("WinGet package fetching returned zero packages, attempting legacy...");
        return BundledWinGetLegacyMethods.FindPackages_UnSafe(Manager, query);

    }

    public void GetPackageDetails_UnSafe(WinGet Manager, IPackageDetails details)
    {
        if (details.Package.Source.Name == "winget")
        {
            details.ManifestUrl = new Uri("https://github.com/microsoft/winget-pkgs/tree/master/manifests/"
                                          + details.Package.Id[0].ToString().ToLower() + "/"
                                          + details.Package.Id.Split('.')[0] + "/"
                                          + string.Join("/",
                                              details.Package.Id.Contains('.')
                                                  ? details.Package.Id.Split('.')[1..]
                                                  : details.Package.Id.Split('.'))
            );
        }
        else if (details.Package.Source.Name == "msstore")
        {
            details.ManifestUrl = new Uri("https://apps.microsoft.com/detail/" + details.Package.Id);
        }

        // Get the output for the best matching locale
        Process process = new();
        string packageIdentifier = "--id " + details.Package.Id + " --exact";

        List<string> output = [];
        bool LocaleFound = true;
        ProcessStartInfo startInfo = new()
        {
            FileName = Manager.WinGetBundledPath,
            Arguments = Manager.Properties.ExecutableCallArgs + " show " + packageIdentifier +
                        " --disable-interactivity --accept-source-agreements --locale " +
                        System.Globalization.CultureInfo.CurrentCulture.ToString(),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = System.Text.Encoding.UTF8
        };
        process.StartInfo = startInfo;
        process.Start();

        string? _line;
        while ((_line = process.StandardOutput.ReadLine()) is not null)
        {
            if (_line.Trim() != "")
            {
                output.Add(_line);
                if (_line.Contains("The value provided for the `locale` argument is invalid") ||
                    _line.Contains("No applicable installer found; see Logger.Logs for more details."))
                {
                    LocaleFound = false;
                    break;
                }
            }
        }

        // Load fallback english locale
        if (!LocaleFound)
        {
            output.Clear();
            Logger.Info("Winget could not found culture data for package Id=" + details.Package.Id + " and Culture=" +
                        System.Globalization.CultureInfo.CurrentCulture.ToString() + ". Trying to get data for en-US");
            process = new Process();
            LocaleFound = true;
            startInfo = new()
            {
                FileName = Manager.WinGetBundledPath,
                Arguments = Manager.Properties.ExecutableCallArgs + " show " + packageIdentifier +
                            " --disable-interactivity --accept-source-agreements --locale en-US",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = System.Text.Encoding.UTF8
            };
            process.StartInfo = startInfo;
            process.Start();

            while ((_line = process.StandardOutput.ReadLine()) is not null)
            {
                if (_line.Trim() != "")
                {
                    output.Add(_line);
                    if (_line.Contains("The value provided for the `locale` argument is invalid") ||
                        _line.Contains("No applicable installer found; see Logger.Logs for more details."))
                    {
                        LocaleFound = false;
                        break;
                    }
                }
            }
        }

        // Load default locale
        if (!LocaleFound)
        {
            output.Clear();
            Logger.Info("Winget could not found culture data for package Id=" + details.Package.Id +
                        " and Culture=en-US. Loading default");
            process = new Process();
            startInfo = new()
            {
                FileName = Manager.WinGetBundledPath,
                Arguments = Manager.Properties.ExecutableCallArgs + " show " + packageIdentifier +
                            " --disable-interactivity --accept-source-agreements",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = System.Text.Encoding.UTF8
            };
            process.StartInfo = startInfo;
            process.Start();

            while ((_line = process.StandardOutput.ReadLine()) is not null)
            {
                if (_line.Trim() != "")
                {
                    output.Add(_line);
                }
            }
        }

        // Parse the output
        bool IsLoadingDescription = false;
        bool IsLoadingReleaseNotes = false;
        bool IsLoadingTags = false;
        foreach (string __line in output)
        {
            try
            {
                string line = __line.TrimEnd();
                if (line == "")
                {
                    continue;
                }

                // Check if a multiline field is being loaded
                if (line.StartsWith(" ") && IsLoadingDescription)
                {
                    details.Description += "\n" + line.Trim();
                }
                else if (line.StartsWith(" ") && IsLoadingReleaseNotes)
                {
                    details.ReleaseNotes += "\n" + line.Trim();
                }
                else if (line.StartsWith(" ") && IsLoadingTags)
                {
                    details.Tags = details.Tags.Append(line.Trim()).ToArray();
                }

                // Stop loading multiline fields
                else if (IsLoadingDescription)
                {
                    IsLoadingDescription = false;
                }
                else if (IsLoadingReleaseNotes)
                {
                    IsLoadingReleaseNotes = false;
                }
                else if (IsLoadingTags)
                {
                    IsLoadingTags = false;
                }

                // Check for single-line fields
                if (line.Contains("Publisher:"))
                {
                    details.Publisher = line.Split(":")[1].Trim();
                }
                else if (line.Contains("Author:"))
                {
                    details.Author = line.Split(":")[1].Trim();
                }
                else if (line.Contains("Homepage:"))
                {
                    details.HomepageUrl = new Uri(line.Replace("Homepage:", "").Trim());
                }
                else if (line.Contains("License:"))
                {
                    details.License = line.Split(":")[1].Trim();
                }
                else if (line.Contains("License Url:"))
                {
                    details.LicenseUrl = new Uri(line.Replace("License Url:", "").Trim());
                }
                else if (line.Contains("Installer SHA256:"))
                {
                    details.InstallerHash = line.Split(":")[1].Trim();
                }
                else if (line.Contains("Installer Url:"))
                {
                    details.InstallerUrl = new Uri(line.Replace("Installer Url:", "").Trim());
                    details.InstallerSize = CoreTools.GetFileSize(details.InstallerUrl);
                }
                else if (line.Contains("Release Date:"))
                {
                    details.UpdateDate = line.Split(":")[1].Trim();
                }
                else if (line.Contains("Release Notes Url:"))
                {
                    details.ReleaseNotesUrl = new Uri(line.Replace("Release Notes Url:", "").Trim());
                }
                else if (line.Contains("Installer Type:"))
                {
                    details.InstallerType = line.Split(":")[1].Trim();
                }
                else if (line.Contains("Description:"))
                {
                    details.Description = line.Split(":")[1].Trim();
                    IsLoadingDescription = true;
                }
                else if (line.Contains("ReleaseNotes"))
                {
                    details.ReleaseNotes = line.Split(":")[1].Trim();
                    IsLoadingReleaseNotes = true;
                }
                else if (line.Contains("Tags"))
                {
                    details.Tags = [];
                    IsLoadingTags = true;
                }
            }
            catch (Exception e)
            {
                Logger.Warn("Error occurred while parsing line value=\"" + _line + "\"");
                Logger.Warn(e.Message);
            }
        }

        return;
    }

    public IEnumerable<string> GetInstallableVersions_Unsafe(WinGet Manager, IPackage package)
    {
        Process p = new()
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = Manager.WinGetBundledPath,
                Arguments = Manager.Properties.ExecutableCallArgs + " show --id " + package.Id +
                            " --exact --versions --accept-source-agreements",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = true,
                CreateNoWindow = true,
                StandardOutputEncoding = System.Text.Encoding.UTF8
            }
        };

        IProcessTaskLogger logger = Manager.TaskLogger.CreateNew(LoggableTaskType.LoadPackageVersions, p);

        p.Start();

        string? line;
        List<string> versions = [];
        bool DashesPassed = false;
        while ((line = p.StandardOutput.ReadLine()) is not null)
        {
            logger.AddToStdOut(line);
            if (!DashesPassed)
            {
                if (line.Contains("---"))
                {
                    DashesPassed = true;
                }
            }
            else
            {
                versions.Add(line.Trim());
            }
        }

        logger.AddToStdErr(p.StandardError.ReadToEnd());
        p.WaitForExit();
        logger.Close(p.ExitCode);
        return versions;
    }

    public IEnumerable<IManagerSource> GetSources_UnSafe(WinGet Manager)
    {
        List<IManagerSource> sources = [];

        Process p = new()
        {
            StartInfo = new()
            {
                FileName = Manager.Status.ExecutablePath,
                Arguments = Manager.Properties.ExecutableCallArgs + " source list",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8
            }
        };

        p.Start();

        IProcessTaskLogger logger = Manager.TaskLogger.CreateNew(LoggableTaskType.FindPackages, p);

        bool dashesPassed = false;
        string? line;
        while ((line = p.StandardOutput.ReadLine()) is not null)
        {
            logger.AddToStdOut(line);
            try
            {
                if (string.IsNullOrEmpty(line))
                {
                    continue;
                }

                if (!dashesPassed)
                {
                    if (line.Contains("---"))
                    {
                        dashesPassed = true;
                    }
                }
                else
                {
                    string[] parts = Regex.Replace(line.Trim(), " {2,}", " ").Split(' ');
                    if (parts.Length > 1)
                    {
                        sources.Add(new ManagerSource(Manager, parts[0].Trim(), new Uri(parts[1].Trim())));
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Warn(e);
            }
        }

        logger.AddToStdErr(p.StandardError.ReadToEnd());
        p.WaitForExit();
        logger.Close(p.ExitCode);
        return sources;

    }
}
