using Bloxstrap;
using Bloxstrap.Enums;
using Bloxstrap.Models.Persistable;
using Bloxstrap.RobloxInterfaces;

namespace Rainstrap.Tests
{
    /// <summary>
    /// Redirects Rainstrap's state and filesystem at a throwaway directory.
    ///
    /// Everything destructive in these tests runs inside that directory, so the
    /// developer's real Roblox installation is never read or written. Paths is a
    /// static singleton by design in the application, so the only safe way to test
    /// the file-level behaviour is to repoint it before each test and clean up
    /// afterwards.
    /// </summary>
    public sealed class TempInstall : IDisposable
    {
        private readonly string _root;

        /// <summary>Versions directory for this test.</summary>
        public string VersionsRoot { get; }

        /// <summary>Directory a test can drop a fake install into.</summary>
        public string Versions => VersionsRoot;

        public TempInstall()
        {
            _root = Path.Combine(Path.GetTempPath(), "RainstrapTests", Guid.NewGuid().ToString("N"));

            Directory.CreateDirectory(_root);

            Paths.Initialize(_root);

            VersionsRoot = Paths.Versions;

            Directory.CreateDirectory(VersionsRoot);

            // Fresh, empty state for every test so nothing leaks between them.
            App.Settings.Prop = new Settings();
            App.State.Prop = new State();
            App.RobloxState.Prop = new RobloxState();

            // The bootstrapper's static entry points read launch flags without
            // owning them. In the application OnStartup always sets these first;
            // here it has to be done explicitly, with no flags set.
            App.LaunchSettings = new LaunchSettings(Array.Empty<string>());
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(_root))
                    Directory.Delete(_root, true);
            }
            catch (IOException)
            {
                // A leftover temp directory is not worth failing a test over.
            }

            App.Settings.Prop = new Settings();
            App.State.Prop = new State();
            App.RobloxState.Prop = new RobloxState();
        }

        /// <summary>
        /// Creates a version folder containing a file that genuinely passes
        /// installation validation.
        ///
        /// A real RobloxPlayerBeta.exe is a signed ~100MB binary that is not
        /// available to a test, so this copies a real managed assembly from the
        /// test output instead. It matters that it is a genuine PE with a genuine
        /// version resource: a synthetic blob of zeroes would be rejected by
        /// validation, which would mean every "installed version survives" test
        /// passed for the wrong reason - because the folder looked broken rather
        /// than because it was protected.
        /// </summary>
        public string CreateInstall(string versionGuid, string executableName = "RobloxPlayerBeta.exe", long? sizeBytes = null)
        {
            string dir = Path.Combine(VersionsRoot, versionGuid);

            Directory.CreateDirectory(dir);

            string executable = Path.Combine(dir, executableName);

            if (sizeBytes is null)
            {
                File.Copy(FindRealAssembly(), executable, overwrite: true);

                return executable;
            }

            // Explicitly-sized (and therefore deliberately invalid) file, for the
            // truncated-download cases.
            using (var stream = new FileStream(executable, FileMode.Create, FileAccess.Write))
                stream.SetLength(sizeBytes.Value);

            return executable;
        }

        /// <summary>
        /// A real assembly that carries a version resource and passes validation.
        /// </summary>
        private static string FindRealAssembly()
        {
            string directory = AppContext.BaseDirectory;

            foreach (string name in new[] { "Rainstrap.dll", "Wpf.Ui.dll", "Newtonsoft.Json.dll" })
            {
                string candidate = Path.Combine(directory, name);

                if (File.Exists(candidate))
                    return candidate;
            }

            throw new FileNotFoundException(
                $"No assembly with a version resource was found next to the tests ({directory})");
        }

        /// <summary>
        /// Creates a version folder with no executable at all - the shape an
        /// interrupted download leaves behind.
        /// </summary>
        public string CreateIncompleteInstall(string versionGuid)
        {
            string dir = Path.Combine(VersionsRoot, versionGuid);

            Directory.CreateDirectory(dir);

            File.WriteAllText(Path.Combine(dir, "partial.tmp"), "not a client");

            return dir;
        }

        /// <summary>Marks a version as the installed one in recorded state.</summary>
        public void SetInstalled(string versionGuid)
            => App.RobloxState.Prop.Player.VersionGuid = versionGuid;

        /// <summary>Pins a version through the real selection API.</summary>
        public void Select(string versionGuid, bool pinned = true)
            => VersionControl.SelectVersion(versionGuid, pinned);

        public string Channel => Deployment.Channel;
    }
}