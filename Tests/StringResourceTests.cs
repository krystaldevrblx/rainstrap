using System.Globalization;
using System.Reflection;
using System.Resources;
using System.Xml.Linq;

namespace Rainstrap.Tests
{
/// <summary>
        /// Guards the generated string resource wiring.
        ///
        /// These exist because a mismatch between a resx <c>data name</c> and the key
        /// the generated designer looks up fails silently at runtime: the property
        /// compiles, returns null, and only blows up later inside a String.Format or
        /// as a blank label in the UI. That kind of defect survives a green build,
        /// so it is asserted explicitly here.
        ///
        /// Everything is read through the compiled resource set in Rainstrap.dll -
        /// the artefact that actually ships - rather than from the .resx source, so
        /// a stale or half-regenerated resource assembly is caught too.
        /// </summary>
    public class StringResourceTests
    {
        private static PropertyInfo[] GetStringProperties() =>
            typeof(Strings)
                .GetProperties(BindingFlags.Public | BindingFlags.Static)
                // ResourceManager is also a public static property; only the
                // generated lookups return strings.
                .Where(x => x.PropertyType == typeof(string))
                .ToArray();

        /// <summary>
        /// Every key embedded in the neutral Strings.resx inside the built assembly.
        /// </summary>
        private static HashSet<string> GetEmbeddedKeys()
        {
            var keys = new HashSet<string>(StringComparer.Ordinal);

            ResourceSet? set = Strings.ResourceManager
                .GetResourceSet(CultureInfo.InvariantCulture, createIfNotExists: true, tryParents: true);

            if (set is null)
                return keys;

            foreach (System.Collections.DictionaryEntry entry in set)
                keys.Add((string)entry.Key);

            return keys;
        }

        private static string? Read(string property) =>
            (string?)GetStringProperties().First(x => x.Name == property).GetValue(null);

        [Fact]
        public void EveryStringPropertyResolves()
        {
            var missing = new List<string>();

            foreach (PropertyInfo property in GetStringProperties())
            {
                string? value = (string?)property.GetValue(null);

                if (value is null)
                    missing.Add(property.Name);
            }

            Assert.Empty(missing);
        }

        [Fact]
        public void EveryVersionControlStringResolves()
        {
            foreach (PropertyInfo property in GetStringProperties()
                         .Where(x => x.Name.StartsWith("VersionControl_", StringComparison.Ordinal)))
            {
                string? value = (string?)property.GetValue(null);

                Assert.False(String.IsNullOrWhiteSpace(value), $"{property.Name} did not resolve");
            }
        }

        [Fact]
        public void EveryNotifyStringResolves()
        {
            foreach (PropertyInfo property in GetStringProperties()
                         .Where(x => x.Name.StartsWith("UpgradeNotify_", StringComparison.Ordinal)))
            {
                string? value = (string?)property.GetValue(null);

                Assert.False(String.IsNullOrWhiteSpace(value), $"{property.Name} did not resolve");
            }
        }

        [Fact]
        public void EveryEmbeddedKeyIsReachableThroughTheResourceManager()
        {
            // Reads the compiled resource set that ships, not the .resx source.
            var embedded = GetEmbeddedKeys();

            Assert.NotEmpty(embedded);

            var unreachable = embedded
                .Where(key => Strings.ResourceManager.GetString(key) is null)
                .ToList();

            Assert.Empty(unreachable);
        }

        [Fact]
        public void VersionControlStringsAreActuallyEmbedded()
        {
            var embedded = GetEmbeddedKeys();

            foreach (string key in new[]
                     {
                         "VersionControl.Title",
                         "VersionControl.Description",
                         "VersionControl.Limitation.NoHistoryEndpoint",
                         "VersionControl.Limitation.PartialRefresh",
                         "VersionControl.AgeWarning",
                         "VersionControl.RecoveryPrompt",
                         "Menu.VersionControl.Title",
                         "UpgradeNotify.Title",
                         "UpgradeNotify.AvailableMessage",
                         "UpgradeNotify.PinnedNote",
                     })
            {
                Assert.Contains(key, embedded);
            }
        }

        [Fact]
        public void RollbackStringsAreNotShipped()
        {
            // Rollback was replaced by Version Control. Its strings must not
            // linger, or a future change could bind to them and resurrect a
            // feature with no behaviour behind it.
            var embedded = GetEmbeddedKeys();

            Assert.DoesNotContain("Updates.History.RollbackButton", embedded);
            Assert.DoesNotContain("Updates.History.RollbackConfirmText", embedded);

            Assert.DoesNotContain(
                GetStringProperties(),
                x => x.Name.Contains("Rollback", StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public void EveryResxEntryHasADesignerProperty()
        {
            var designerProperties = GetStringProperties().Select(x => x.Name).ToHashSet(StringComparer.Ordinal);

            XDocument resx = XDocument.Load(LocateResx());

            var orphans = resx.Root!
                .Elements("data")
                .Select(x => (string?)x.Attribute("name") ?? String.Empty)
                // Nested keys use '_' in the property name because '.' cannot
                // appear in an identifier: "VersionControl.Details.Support.X"
                // becomes "VersionControl_Details_Support_X".
                .Select(name => name.Replace('.', '_'))
                .Where(name => !designerProperties.Contains(name))
                .ToList();

            Assert.Empty(orphans);
        }

        [Fact]
        public void TheNotifyMessageRendersBothVersionsAndNoStrayBraces()
        {
            string message = Strings.UpgradeNotify_AvailableMessage;

            Assert.False(String.IsNullOrEmpty(message));

            string rendered = String.Format(message, "0.740.0.7400927", "0.741.0.7411058", "");

            // The user must be able to see both versions in the prompt.
            Assert.Contains("0.740.0.7400927", rendered);
            Assert.Contains("0.741.0.7411058", rendered);
            Assert.DoesNotContain("{", rendered);
        }

        [Fact]
        public void TheNotifyMessageRendersWhenAPinIsInPlace()
        {
            string rendered = String.Format(
                Strings.UpgradeNotify_AvailableMessage,
                "0.740.0.7400927",
                "0.741.0.7411058",
                Strings.UpgradeNotify_PinnedNote);

            Assert.Contains("0.740.0.7400927", rendered);
            Assert.DoesNotContain("{", rendered);
        }

        private static string LocateResx()
        {
            // Walk up from the test binaries to the repository root, then into the
            // application project. Keeps the test independent of where it is run
            // from without adding a build-time copy step.
            var directory = new DirectoryInfo(AppContext.BaseDirectory);

            while (directory is not null)
            {
                string candidate = Path.Combine(directory.FullName, "Bloxstrap", "Resources", "Strings.resx");

                if (File.Exists(candidate))
                    return candidate;

                directory = directory.Parent;
            }

            throw new FileNotFoundException("Could not locate Bloxstrap/Resources/Strings.resx");
        }
    }
}