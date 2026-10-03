using System.Text.RegularExpressions;

namespace Rainstrap.Tests
{
    /// <summary>
    /// Guards the icon names used in the XAML.
    ///
    /// An invalid symbol name is not a build error. Wpf.Ui resolves Icon="..."
    /// through a TypeConverterMarkupExtension when the XAML is *loaded*, so a typo
    /// such as Icon="Stack2" compiles cleanly, passes every other test, and then
    /// throws XamlParseException the first time the settings window opens - taking
    /// the entire window down rather than just the one control.
    ///
    /// This was a real crash: the Version Control navigation item shipped with
    /// Icon="Stack2", which does not exist in this version of Wpf.Ui. The valid
    /// names are Stack16/20/24/32.
    /// </summary>
    public class XamlIconTests
    {
        private static readonly Regex IconAttribute =
            new(@"\b(?:Symbol|Icon)\s*=\s*""([^""]+)""", RegexOptions.Compiled);

        /// <summary>
        /// Every symbol name in the Wpf.Ui icon enums.
        ///
        /// Read by reflection rather than by parsing the submodule's source: the
        /// enums are the compiled contract, and a renamed member cannot be used in
        /// XAML regardless of what the source says.
        /// </summary>
        private static HashSet<string> KnownSymbols()
        {
            var symbols = new HashSet<string>(StringComparer.Ordinal);

            foreach (Type type in new[]
                     {
                         typeof(Wpf.Ui.Common.SymbolRegular),
                         typeof(Wpf.Ui.Common.SymbolFilled),
                     })
            {
                foreach (string name in Enum.GetNames(type))
                    symbols.Add(name);
            }

            return symbols;
        }

        private static IEnumerable<(string Symbol, string File, int Line)> EnumerateIconLiterals(string root)
        {
            foreach (string file in Directory.EnumerateFiles(root, "*.xaml", SearchOption.AllDirectories))
            {
                string text = File.ReadAllText(file);

                foreach (Match match in IconAttribute.Matches(text))
                {
                    string symbol = Regex.Unescape(match.Groups[1].Value).Trim();

                    // Not a symbol literal: a pack:// image URI (TitleBar.Icon is an
                    // image, not an enum), a resource key, or a style reference.
                    if (symbol.Contains(':') || symbol.Contains('{'))
                        continue;

                    if (!Regex.IsMatch(symbol, @"^[A-Za-z]\w*$"))
                        continue;

                    int line = text.Substring(0, match.Index).Count(c => c == '\n') + 1;

                    yield return (symbol, Path.GetFileName(file), line);
                }
            }
        }

        [Fact]
        public void EveryIconInTheUiResolvesToAKnownSymbol()
        {
            HashSet<string> known = KnownSymbols();

            Assert.True(known.Count > 1000, "the icon enums could not be read");

            List<string> invalid = EnumerateIconLiterals(LocateUiRoot())
                .Where(x => !known.Contains(x.Symbol))
                .Select(x => $"  {x.File}:{x.Line}  '{x.Symbol}'")
                .ToList();

            Assert.True(
                invalid.Count == 0,
                "These icon names do not exist in Wpf.Ui.Common.SymbolRegular or "
                + "SymbolFilled, and will throw XamlParseException when the window opens:"
                + Environment.NewLine
                + String.Join(Environment.NewLine, invalid));
        }

        [Fact]
        public void TheVersionControlNavigationIconIsValid()
        {
            // Named explicitly because this is the one that broke: the general check
            // above would catch a regression, but this says which control was at
            // fault when it fails. Wpf.Ui has Stack16/20/24/32, not Stack2.
            string mainWindow = File.ReadAllText(
                Path.Combine(LocateUiRoot(), "Elements", "Settings", "MainWindow.xaml"));

            Match match = Regex.Match(mainWindow, @"VersionControlPage}""\s+Icon=""([^""]+)""");

            Assert.True(match.Success, "the Version Control navigation item has no Icon");

            string symbol = match.Groups[1].Value;

            Assert.True(
                KnownSymbols().Contains(symbol),
                $"'{symbol}' is not a valid Wpf.Ui icon; valid 'Stack' symbols are Stack16, Stack20, Stack24 and Stack32");
        }

        /// <summary>
        /// Walks up from the test binaries to the repository root, then into the
        /// application's UI folder, so the check does not depend on the working
        /// directory or need a build-time copy of the XAML.
        /// </summary>
        private static string LocateUiRoot()
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);

            while (directory is not null)
            {
                string candidate = Path.Combine(directory.FullName, "Bloxstrap", "UI");

                if (Directory.Exists(candidate))
                    return candidate;

                directory = directory.Parent;
            }

            throw new DirectoryNotFoundException("Could not locate Bloxstrap/UI");
        }
    }
}