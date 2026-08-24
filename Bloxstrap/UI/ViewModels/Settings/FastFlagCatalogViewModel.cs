using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;

using CommunityToolkit.Mvvm.Input;

using Bloxstrap.Enums;

namespace Bloxstrap.UI.ViewModels.Settings
{
    public class CatalogItemViewModel : NotifyPropertyChangedViewModel
    {
        private readonly FastFlagCatalogEntry _entry;

        public CatalogItemViewModel(FastFlagCatalogEntry entry)
        {
            _entry = entry;
            _valueText = App.FastFlags.GetValue(entry.FlagName) ?? "";
        }

        public string FlagName => _entry.FlagName;

        public string CategoryLabel => FastFlagCatalog.GetCategoryLabel(_entry.Category);

        public FastFlagCategory Category => _entry.Category;

        public string Description => FastFlagCatalog.GetDescription(_entry);

        public bool IsPresent => !String.IsNullOrEmpty(App.FastFlags.GetValue(FlagName));

        // a flag is "enabled" in the catalog when it exists in the configuration
        public bool IsEnabled
        {
            get => IsPresent;
            set
            {
                if (value)
                    App.FastFlags.SetValue(FlagName, String.IsNullOrEmpty(_valueText) ? "True" : _valueText);
                else
                    App.FastFlags.SetValue(FlagName, null);

                RefreshState();
            }
        }

        private string _valueText = "";
        public string ValueText
        {
            get => _valueText;
            set
            {
                _valueText = value;
                OnPropertyChanged(nameof(ValueText));
                OnPropertyChanged(nameof(IsDirty));
            }
        }

        /// <summary>
        /// The value box differs from the stored value - the Apply button becomes available.
        /// </summary>
        public bool IsDirty
        {
            get
            {
                string? stored = App.FastFlags.GetValue(FlagName);
                return IsPresent && !String.IsNullOrEmpty(_valueText) && stored != _valueText;
            }
        }

        public void RefreshState()
        {
            _valueText = App.FastFlags.GetValue(FlagName) ?? "";
            OnPropertyChanged(nameof(ValueText));
            OnPropertyChanged(nameof(IsPresent));
            OnPropertyChanged(nameof(IsEnabled));
            OnPropertyChanged(nameof(IsDirty));
            OnPropertyChanged(nameof(ModifiedBadgeVisibility));
        }

        public Visibility ModifiedBadgeVisibility => IsPresent ? Visibility.Visible : Visibility.Collapsed;
    }

    public class FastFlagCatalogViewModel : NotifyPropertyChangedViewModel
    {
        public ObservableCollection<CatalogItemViewModel> Items { get; } = new();

        private string _searchText = "";
        public string SearchText
        {
            get => _searchText;
            set
            {
                _searchText = value;
                OnPropertyChanged(nameof(SearchText));
                ApplyFilter();
            }
        }

        private static readonly Dictionary<string, FastFlagCategory?> CategoryMap = BuildCategories();

        public IReadOnlyList<string> CategoryLabels { get; } = CategoryMap.Keys.ToList();

        private static Dictionary<string, FastFlagCategory?> BuildCategories()
        {
            var categories = new Dictionary<string, FastFlagCategory?>
            {
                { Strings.Catalog_AllCategories, null }
            };

            foreach (FastFlagCategory category in Enum.GetValues(typeof(FastFlagCategory)))
                categories[FastFlagCatalog.GetCategoryLabel(category)] = category;

            return categories;
        }

        private string _selectedCategoryLabel = Strings.Catalog_AllCategories;
        public string SelectedCategoryLabel
        {
            get => _selectedCategoryLabel;
            set
            {
                _selectedCategoryLabel = value;
                OnPropertyChanged(nameof(SelectedCategoryLabel));
                ApplyFilter();
            }
        }

        public Visibility NoResultsVisibility => Items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        private string _statusText = "";
        public string StatusText
        {
            get => _statusText;
            set
            {
                _statusText = value;
                OnPropertyChanged(nameof(StatusText));
            }
        }

        public ICommand ResetFlagCommand => new RelayCommand<object?>(parameter =>
        {
            if (parameter is not CatalogItemViewModel item)
                return;

            App.FastFlags.SetValue(item.FlagName, null);
            item.RefreshState();
        });

        public ICommand ApplyValueCommand => new RelayCommand<object?>(parameter =>
        {
            if (parameter is not CatalogItemViewModel item || String.IsNullOrEmpty(item.ValueText))
                return;

            App.FastFlags.SetValue(item.FlagName, item.ValueText);
            item.RefreshState();
        });

        public ICommand ApplyPresetCommand => new RelayCommand<object?>(parameter =>
        {
            Dictionary<string, string>? preset = parameter as Dictionary<string, string>;

            if (preset is null)
                return;

            string label =
                preset == FastFlagCatalog.Presets.Performance ? Strings.Catalog_Preset_Performance :
                preset == FastFlagCatalog.Presets.LowLatency ? Strings.Catalog_Preset_LowLatency :
                Strings.Catalog_Preset_UIMinimal;

            foreach (var pair in preset)
                App.FastFlags.SetValue(pair.Key, pair.Value);

            ApplyFilter();
            StatusText = String.Format(Strings.Catalog_PresetApplied, label);
        });

        public ICommand PresetPerformanceCommand => new RelayCommand(() => ApplyNamedPreset(FastFlagCatalog.Presets.Performance, Strings.Catalog_Preset_Performance));
        public ICommand PresetLowLatencyCommand => new RelayCommand(() => ApplyNamedPreset(FastFlagCatalog.Presets.LowLatency, Strings.Catalog_Preset_LowLatency));
        public ICommand PresetUIMinimalCommand => new RelayCommand(() => ApplyNamedPreset(FastFlagCatalog.Presets.UIMinimal, Strings.Catalog_Preset_UIMinimal));

        private void ApplyNamedPreset(Dictionary<string, string> preset, string label)
        {
            foreach (var pair in preset)
                App.FastFlags.SetValue(pair.Key, pair.Value);

            ApplyFilter();
            StatusText = String.Format(Strings.Catalog_PresetApplied, label);
        }

        public ICommand ResetAllCommand => new RelayCommand(() =>
        {
            var choice = Frontend.ShowMessageBox(
                Strings.Catalog_ResetAllConfirm,
                MessageBoxImage.Warning,
                MessageBoxButton.YesNo,
                MessageBoxResult.No
            );

            if (choice != MessageBoxResult.Yes)
                return;

            foreach (var entry in FastFlagCatalog.Entries)
                App.FastFlags.SetValue(entry.FlagName, null);

            ApplyFilter();
        });

        public ICommand ImportCommand => new RelayCommand(() =>
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = $"{Strings.FileTypes_JSONFiles}|*.json"
            };

            if (dialog.ShowDialog() != true)
                return;

            try
            {
                var flags = JsonSerializer.Deserialize<Dictionary<string, object>>(File.ReadAllText(dialog.FileName));

                if (flags is null)
                    throw new InvalidDataException("Deserialization returned null");

                foreach (var pair in flags)
                    App.FastFlags.SetValue(pair.Key, pair.Value);

                ApplyFilter();
                StatusText = String.Format(Strings.Catalog_ImportedCount, flags.Count);
            }
            catch (Exception ex)
            {
                Frontend.ShowMessageBox(String.Format(Strings.Catalog_ImportFailed, ex.Message), MessageBoxImage.Error);
            }
        });

        public ICommand ExportCommand => new RelayCommand(() =>
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = $"{Strings.FileTypes_JSONFiles}|*.json",
                FileName = "RainstrapFastFlags.json"
            };

            if (dialog.ShowDialog() != true)
                return;

            File.WriteAllText(dialog.FileName, JsonSerializer.Serialize(App.FastFlags.Prop, new JsonSerializerOptions { WriteIndented = true }));
            StatusText = Strings.Catalog_ExportDone;
        });

        public FastFlagCatalogViewModel()
        {
            ApplyFilter();
        }

        private void ApplyFilter()
        {
            Items.Clear();

            string search = _searchText.TrimStart();

            FastFlagCategory? categoryFilter =
                SelectedCategoryLabel == Strings.Catalog_AllCategories ? null : CategoryMap.GetValueOrDefault(SelectedCategoryLabel);

            foreach (var entry in FastFlagCatalog.Entries)
            {
                if (categoryFilter is not null && entry.Category != categoryFilter)
                    continue;

                if (
                    !String.IsNullOrEmpty(search) &&
                    !entry.FlagName.Contains(search.Trim(), StringComparison.InvariantCultureIgnoreCase) &&
                    !FastFlagCatalog.GetDescription(entry).Contains(search.Trim(), StringComparison.InvariantCultureIgnoreCase)
                )
                    continue;

                Items.Add(new CatalogItemViewModel(entry));
            }

            OnPropertyChanged(nameof(Items));
            OnPropertyChanged(nameof(NoResultsVisibility));
        }
    }
}
