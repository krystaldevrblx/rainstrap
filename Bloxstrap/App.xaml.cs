using System.Reflection;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Shell;
using System.Windows.Threading;

using Microsoft.Win32;

namespace Bloxstrap
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
#if QA_BUILD
        public const string ProjectName = "Rainstrap-QA";
#else
        public const string ProjectName = "Rainstrap";
#endif
        public const string ProjectOwner = "krystaldevrblx";
        public const string ProjectRepository = "krystaldevrblx/rainstrap";
        public const string ProjectDownloadLink = "https://github.com/krystaldevrblx/rainstrap/releases";
        public const string ProjectHelpLink = "https://github.com/bloxstraplabs/bloxstrap/wiki";
        public const string ProjectSupportLink = "https://github.com/krystaldevrblx/rainstrap/issues/new";
        public const string ProjectRemoteDataLink = "https://config.fishstrap.app/v1/Data.json";

        public const string RobloxPlayerAppName = "RobloxPlayerBeta.exe";
        public const string RobloxStudioAppName = "RobloxStudioBeta.exe";

        // simple shorthand for extremely frequently used and long string - this goes under HKCU
        public const string UninstallKey = $@"Software\Microsoft\Windows\CurrentVersion\Uninstall\{ProjectName}";

        public const string ApisKey = $"Software\\{ProjectName}";

        public static LaunchSettings LaunchSettings { get; private set; } = null!;

        public static BuildMetadataAttribute BuildMetadata = Assembly.GetExecutingAssembly().GetCustomAttribute<BuildMetadataAttribute>()!;

        public static string Version = Assembly.GetExecutingAssembly().GetName().Version!.ToString()[..^2];

        public static Bootstrapper? Bootstrapper { get; set; } = null!;

        public static bool IsActionBuild => !String.IsNullOrEmpty(BuildMetadata.CommitRef);

        public static bool IsProductionBuild => IsActionBuild && BuildMetadata.CommitRef.StartsWith("tag", StringComparison.Ordinal);

        public static bool IsStudioVisible => !String.IsNullOrEmpty(App.RobloxState.Prop.Studio.VersionGuid);

        public static readonly MD5 MD5Provider = MD5.Create();

        public static readonly Logger Logger = new();

        public static readonly Dictionary<string, BaseTask> PendingSettingTasks = new();

        public static readonly JsonManager<Settings> Settings = new();

        public static readonly JsonManager<State> State = new();

        public static readonly JsonManager<RobloxState> RobloxState = new();

        public static readonly RemoteDataManager RemoteData = new();

        public static readonly FastFlagManager FastFlags = new();

        public static readonly GlobalSettingsManager GlobalSettings = new();

        public static readonly CookiesManager Cookies = new();

        public static readonly AccountsManager Accounts = new();

        // RainHub ecosystem link (optional; offline-first)
        public static readonly JsonManager<Models.Persistable.RainHubLink> RainHubLink = new();

        // Plugin system
        public static Plugins.PluginManager? PluginManager { get; set; }

        public static readonly Integrations.RainHub.RainHubLinkManager RainHubManager = Integrations.RainHub.RainHubLinkManager.Instance;

        public static readonly HttpClient HttpClient = new(
            new HttpClientLoggingHandler(
                new HttpClientHandler { AutomaticDecompression = DecompressionMethods.All }
            )
        );

        private static bool _showingExceptionDialog = false;

        public static void Terminate(ErrorCode exitCode = ErrorCode.ERROR_SUCCESS)
        {
            int exitCodeNum = (int)exitCode;

            Logger.WriteLine("App::Terminate", $"Terminating with exit code {exitCodeNum} ({exitCode})");

            Environment.Exit(exitCodeNum);
        }

        public static void SoftTerminate(ErrorCode exitCode = ErrorCode.ERROR_SUCCESS)
        {
            int exitCodeNum = (int)exitCode;

            Logger.WriteLine("App::SoftTerminate", $"Terminating with exit code {exitCodeNum} ({exitCode})");

            Current.Dispatcher.Invoke(() => Current.Shutdown(exitCodeNum));
        }

        void GlobalExceptionHandler(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            e.Handled = true;

            Logger.WriteLine("App::GlobalExceptionHandler", "An exception occurred");

            FinalizeExceptionHandling(e.Exception);
        }

        public static void FinalizeExceptionHandling(AggregateException ex)
        {
            foreach (var innerEx in ex.InnerExceptions)
                Logger.WriteException("App::FinalizeExceptionHandling", innerEx);

            FinalizeExceptionHandling(ex.GetBaseException(), false);
        }

        public static void FinalizeExceptionHandling(Exception ex, bool log = true)
        {
            if (log)
                Logger.WriteException("App::FinalizeExceptionHandling", ex);

            if (_showingExceptionDialog)
                return;

            _showingExceptionDialog = true;

            SendLog();

            if (Bootstrapper?.Dialog != null)
            {
                if (Bootstrapper.Dialog.TaskbarProgressValue == 0)
                    Bootstrapper.Dialog.TaskbarProgressValue = 1; // make sure it's visible

                Bootstrapper.Dialog.TaskbarProgressState = TaskbarItemProgressState.Error;
            }

            Frontend.ShowExceptionDialog(ex);

            Terminate(ErrorCode.ERROR_INSTALL_FAILURE);
        }

        public static async Task<GithubRelease?> GetLatestRelease()
        {
            const string LOG_IDENT = "App::GetLatestRelease";

            try
            {
                Uri githubReleasesUrl = new($"https://api.github.com/repos/{ProjectRepository}/releases/latest");
                var releaseInfo = await Http.GetJson<GithubRelease>(githubReleasesUrl);

                if (releaseInfo is null || releaseInfo.Assets is null)
                {
                    Logger.WriteLine(LOG_IDENT, "Encountered invalid data");
                    return null;
                }

                return releaseInfo;
            }
            catch (Exception ex)
            {
                Logger.WriteException(LOG_IDENT, ex);
            }

            return null;
        }

        public static void SendLog()
        {

        }

        private static async Task CheckForRainstrapUpdate()
        {
            const string LOG_IDENT = "App::CheckForRainstrapUpdate";

            try
            {
                var (updateAvailable, latestVersion) = await Utility.VersionChecker.CheckForUpdateAsync();

                if (updateAvailable)
                {
                    Logger.WriteLine(LOG_IDENT, $"Update available: {latestVersion}");

                    Current.Dispatcher.Invoke(() =>
                    {
                        UI.Frontend.ShowBalloonTip(
                            $"{ProjectName} Update Available",
                            $"A new version ({latestVersion}) is available.\nOpen Settings > Updates to download it.",
                            System.Windows.Forms.ToolTipIcon.Info,
                            10
                        );
                    });
                }
                else
                {
                    Logger.WriteLine(LOG_IDENT, "No updates available");
                }
            }
            catch (Exception ex)
            {
                Logger.WriteException(LOG_IDENT, ex);
            }
        }

        public static void AssertWindowsOSVersion()
        {
            const string LOG_IDENT = "App::AssertWindowsOSVersion";

            int major = Environment.OSVersion.Version.Major;
            if (major < 10) // Windows 10 and newer only
            {
                Logger.WriteLine(LOG_IDENT, $"Detected unsupported Windows version ({Environment.OSVersion.Version}).");

                if (!LaunchSettings.QuietFlag.Active)
                    Frontend.ShowMessageBox(Strings.App_OSDeprecation_Win7_81, MessageBoxImage.Error);

                Terminate(ErrorCode.ERROR_INVALID_FUNCTION);
            }
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            const string LOG_IDENT = "App::OnStartup";

            Locale.Initialize();

            base.OnStartup(e);

            Logger.WriteLine(LOG_IDENT, $"Starting {ProjectName} v{Version}");

            string userAgent = $"{ProjectName}/{Version}";

            if (IsActionBuild)
            {
                Logger.WriteLine(LOG_IDENT, $"Compiled {BuildMetadata.Timestamp.ToFriendlyString()} from commit {BuildMetadata.CommitHash} ({BuildMetadata.CommitRef})");

                if (IsProductionBuild)
                    userAgent += $" (Production)";
                else
                    userAgent += $" (Artifact {BuildMetadata.CommitHash}, {BuildMetadata.CommitRef})";
            }
            else
            {
                Logger.WriteLine(LOG_IDENT, $"Compiled {BuildMetadata.Timestamp.ToFriendlyString()} from {BuildMetadata.Machine}");

#if QA_BUILD
                userAgent += " (QA)";
#else
                userAgent += $" (Build {Convert.ToBase64String(Encoding.UTF8.GetBytes(BuildMetadata.Machine))})";
#endif
            }

            Logger.WriteLine(LOG_IDENT, $"OSVersion: {Environment.OSVersion}");
            Logger.WriteLine(LOG_IDENT, $"Loaded from {Paths.Process}");
            Logger.WriteLine(LOG_IDENT, $"Temp path is {Paths.Temp}");
            Logger.WriteLine(LOG_IDENT, $"WindowsStartMenu path is {Paths.WindowsStartMenu}");

            // To customize application configuration such as set high DPI settings or default font,
            // see https://aka.ms/applicationconfiguration.
            ApplicationConfiguration.Initialize();

            HttpClient.Timeout = TimeSpan.FromSeconds(30);
            HttpClient.DefaultRequestHeaders.Add("User-Agent", userAgent);

            LaunchSettings = new LaunchSettings(e.Args);

            // installation check begins here
            using var uninstallKey = Registry.CurrentUser.OpenSubKey(UninstallKey);
            string? installLocation = null;
            bool fixInstallLocation = false;

            if (uninstallKey?.GetValue("InstallLocation") is string value)
            {
                if (Directory.Exists(value))
                {
                    installLocation = value;
                }
                else
                {
                    // check if user profile folder has been renamed
                    var match = Regex.Match(value, @"^[a-zA-Z]:\\Users\\([^\\]+)", RegexOptions.IgnoreCase);

                    if (match.Success)
                    {
                        string newLocation = value.Replace(match.Value, Paths.UserProfile, StringComparison.InvariantCultureIgnoreCase);

                        if (Directory.Exists(newLocation))
                        {
                            installLocation = newLocation;
                            fixInstallLocation = true;
                        }
                    }
                }
            }

            // silently change install location if we detect a portable run
            if (installLocation is null && Directory.GetParent(Paths.Process)?.FullName is string processDir)
            {
                var files = Directory.GetFiles(processDir).Select(x => Path.GetFileName(x)).ToArray();

                // check if settings.json and state.json are the only files in the folder
                if (files.Length <= 3 && files.Contains("Settings.json") && files.Contains("State.json"))
                {
                    installLocation = processDir;
                    fixInstallLocation = true;
                }
            }

            if (fixInstallLocation && installLocation is not null)
            {
                var installer = new Installer
                {
                    InstallLocation = installLocation,
                    IsImplicitInstall = true
                };

                if (installer.CheckInstallLocation())
                {
                    Logger.WriteLine(LOG_IDENT, $"Changing install location to '{installLocation}'");
                    installer.DoInstall();
                }
                else
                {
                    // force reinstall
                    installLocation = null;
                }
            }

            if (installLocation is null)
            {
                Logger.Initialize(true);
                AssertWindowsOSVersion();
                Logger.WriteLine(LOG_IDENT, "Not installed, launching the installer");
                AssertWindowsOSVersion(); // prevent new installs from unsupported operating systems
                LaunchHandler.LaunchInstaller();
            }
            else
            {
                Paths.Initialize(installLocation);

                // ensure executable is in the install directory
                if (Paths.Process != Paths.Application && !File.Exists(Paths.Application))
                    File.Copy(Paths.Process, Paths.Application);

                Logger.Initialize(LaunchSettings.UninstallFlag.Active);

                if (!Logger.Initialized && !Logger.NoWriteMode)
                {
                    Logger.WriteLine(LOG_IDENT, "Possible duplicate launch detected, terminating.");
                    Terminate();
                }

                Settings.Load();
                State.Load();
                RobloxState.Load();
                FastFlags.Load();
                Accounts.Load();
                GlobalSettings.Load();
                RainHubLink.Load();

                // Initialize plugin system
                PluginManager = new Plugins.PluginManager(installLocation);
                PluginManager.DiscoverPlugins();

                // Ensure builtin plugin manifests are in the discovered list
                foreach (var manifest in new[] {
                    new Plugins.PluginManifest { Id = "rainstrap.multiinstance", Name = "Multi-Instance", Version = "1.0.0", ApiVersion = "1.0", Author = "Rainstrap", Description = "Run multiple Roblox instances at the same time.", IsOfficial = true, Verified = true, Permissions = new() { "processManagement" } },
                    new Plugins.PluginManifest { Id = "rainstrap.clips", Name = "Clips", Version = "1.0.0", ApiVersion = "1.0", Author = "Rainstrap", Description = "Capture Roblox gameplay clips using a rolling replay buffer.", IsOfficial = true, Verified = true, Permissions = new() { "screenCapture", "filesystem" } },
                    new Plugins.PluginManifest { Id = "rainstrap.accountmanager", Name = "Account Manager", Version = "1.0.0", ApiVersion = "1.0", Author = "Rainstrap", Description = "Manage multiple Roblox accounts and switch between them.", IsOfficial = true, Verified = true, Permissions = new() { "credentialStorage", "cookieAccess" } }
                })
                {
                    if (!PluginManager.Manifests.ContainsKey(manifest.Id))
                    {
                        PluginManager.Manifests[manifest.Id] = manifest;
                    }
                }

                // Load persisted enabled states, then enable any newly discovered plugins
                PluginManager.LoadPluginStates();

                // Apply any pending enable/disable/uninstall changes from previous session
                PluginManager.ApplyPendingChanges();

                PluginManager.EnableNewPlugins();

                // Register builtin plugins (initializes only those that are enabled)
                PluginManager.RegisterBuiltinPlugin(new Plugins.MultiInstancePlugin(), () => new Plugins.MultiInstancePlugin());
                PluginManager.RegisterBuiltinPlugin(new Plugins.ClipsPlugin(), () => new Plugins.ClipsPlugin());
                PluginManager.RegisterBuiltinPlugin(new Plugins.AccountManagerPlugin(), () => new Plugins.AccountManagerPlugin());

                // Initialize builtin plugins now so they are active before the Settings window
                // exists. This ensures plugin-owned functionality works during direct Roblox launch.
                PluginManager.InitializeBuiltinPlugins();

                if (Settings.Prop.AllowCookieAccess)
                    Task.Run(Cookies.LoadCookies);

                if (!Locale.SupportedLocales.ContainsKey(Settings.Prop.Locale))
                {
                    Settings.Prop.Locale = "nil";
                    Settings.Save();
                }

                Locale.Set(Settings.Prop.Locale);

                if (!LaunchSettings.BypassUpdateCheck)
                    Installer.HandleUpgrade();

                Task.Run(App.RemoteData.LoadData); // ok

                // RainHub sync is best-effort and fully optional — it starts in the
                // background, never blocks launching, and no-ops when unlinked.
                App.RainHubManager.Start();

                // Check for Rainstrap updates in the background
                Task.Run(CheckForRainstrapUpdate);

                WindowsRegistry.RegisterApis(); // we want to register those early on
                                                // so we wont have any issues with bloxshade

                LaunchHandler.ProcessLaunchArgs();
            }

            // you must *explicitly* call terminate when everything is done, it won't be called implicitly
        }
    }
}
