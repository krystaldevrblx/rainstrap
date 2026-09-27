using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;

using CommunityToolkit.Mvvm.Input;

using Bloxstrap.Models;

namespace Bloxstrap.UI.ViewModels.Settings
{
    /// <summary>
    /// One preset row: the curated definition plus its current applied state, derived
    /// from the live FastFlag values rather than tracked separately.
    /// </summary>
    public class FastFlagPresetItem : NotifyPropertyChangedViewModel
    {
        public FastFlagPresetItem(FastFlagPreset preset)
        {
            Preset = preset;

            Flags = preset.Flags
                .Select(kv => new FastFlagPresetFlag(kv.Key, kv.Value))
                .ToList();

            Conflicting = FastFlagPresetCatalogue.ConflictingWith(preset);

            // Rainstrap's own controls (MSAA, FRM quality, DPI scaling, the renderer
            // dropdown) already own some of these flags, so moving those controls would
            // quietly undo part of the preset. Name them so that is not a surprise.
            SharedWithExistingControls = FastFlagManager.PresetFlags
                .Where(pair => preset.Flags.ContainsKey(pair.Value))
                .Select(pair => pair.Key)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToList();
        }

        public FastFlagPreset Preset { get; }

        public string Id => Preset.Id;
        public string Name => Preset.Name;
        public string Description => Preset.Description;
        public int FlagCount => Preset.FlagCount;

        public string FlagsCountText => string.Format(Strings.FastFlagPresets_FlagsCount, FlagCount);

        public string CategoryName =>
            Strings.ResourceManager.GetString($"FastFlagPresets_Category_{Preset.Category}")
            ?? Preset.Category.ToString();

        public IReadOnlyList<FastFlagPresetFlag> Flags { get; }

        public bool HasWarning => !string.IsNullOrEmpty(Preset.Warning);
        public string Warning => Preset.Warning ?? "";

        public bool IsRisky => Preset.Risk != FastFlagPresetRisk.None;

        /// <summary>
        /// Presets that would set at least one shared flag to a different value.
        /// Shown up front so overlapping presets never silently overwrite each other.
        /// </summary>
        public IReadOnlyList<FastFlagPreset> Conflicting { get; }

        /// <summary>Names of the existing FastFlags page controls that own shared flags.</summary>
        public IReadOnlyList<string> SharedWithExistingControls { get; }

        public bool HasConflicts => Conflicting.Count > 0 || SharedWithExistingControls.Count > 0;

        public string ConflictText
        {
            get
            {
                if (!HasConflicts)
                    return "";

                var parts = new List<string>();

                if (Conflicting.Count > 0)
                    parts.Add(string.Format(
                        Strings.FastFlagPresets_ConflictNamesFormat,
                        string.Join(", ", Conflicting.Select(c => c.Name))));

                if (SharedWithExistingControls.Count > 0)
                    parts.Add(string.Format(
                        Strings.FastFlagPresets_SharedWithControlsFormat,
                        string.Join(", ", SharedWithExistingControls)));

                return string.Join(Environment.NewLine, parts);
            }
        }

        #region Applied state

        /// <summary>Every flag in the preset currently holds the preset's value.</summary>
        public bool IsApplied => Preset.Flags.All(kv =>
            string.Equals(App.FastFlags.GetValue(kv.Key), kv.Value, StringComparison.OrdinalIgnoreCase));

        /// <summary>Some, but not all, of the preset's flags are set.</summary>
        public bool IsPartiallyApplied => !IsApplied && Preset.Flags.Any(kv =>
        {
            string? current = App.FastFlags.GetValue(kv.Key);
            return current is not null && string.Equals(current, kv.Value, StringComparison.OrdinalIgnoreCase);
        });

        public bool IsTouched => IsApplied || IsPartiallyApplied;

        public string StateText => IsApplied
            ? Strings.FastFlagPresets_AppliedBadge
            : IsPartiallyApplied
                ? Strings.FastFlagPresets_PartiallyApplied
                : Strings.FastFlagPresets_NotApplied;

        public string ApplyButtonText => IsApplied
            ? Strings.FastFlagPresets_Reapply
            : Strings.FastFlagPresets_Apply;

        /// <summary>Recomputes the applied state. Called after any flag change.</summary>
        public void RefreshState()
        {
            OnPropertyChanged(nameof(IsApplied));
            OnPropertyChanged(nameof(IsPartiallyApplied));
            OnPropertyChanged(nameof(IsTouched));
            OnPropertyChanged(nameof(StateText));
            OnPropertyChanged(nameof(ApplyButtonText));
        }

        #endregion
    }

    public record FastFlagPresetFlag(string Name, string Value);

    /// <summary>
    /// FastFlag Presets: curated, built-in collections of flags.
    ///
    /// There is deliberately no upload, download, rating or sharing here. Presets ship
    /// with Rainstrap and are applied through the existing
    /// <see cref="FastFlagManager"/>, so they go through exactly the same path as flags
    /// typed by hand in the editor - no special access to Roblox's allowlist, and no
    /// memory manipulation or injection.
    /// </summary>
    public class FastFlagPresetsViewModel : NotifyPropertyChangedViewModel
    {
        public FastFlagPresetsViewModel()
        {
            ApplyCommand = new RelayCommand<FastFlagPresetItem>(Apply);
            RemoveCommand = new RelayCommand<FastFlagPresetItem>(Remove);
            OpenFastFlagEditorCommand = new RelayCommand(() => OpenFlagEditorEvent?.Invoke(this, EventArgs.Empty));
        }

        public ObservableCollection<FastFlagPresetItem> Presets { get; } = new();

        public ICommand ApplyCommand { get; }
        public ICommand RemoveCommand { get; }
        public ICommand OpenFastFlagEditorCommand { get; }

        /// <summary>Raised so the page can navigate to the full flag editor.</summary>
        public event EventHandler? OpenFlagEditorEvent;

        /// <summary>Raised after flags change so the containing page can repaint.</summary>
        public event EventHandler? RequestPageReloadEvent;

        private string _messageTitle = "";
        public string MessageTitle
        {
            get => _messageTitle;
            private set
            {
                _messageTitle = value;
                OnPropertyChanged(nameof(MessageTitle));
            }
        }

        private string _message = "";
        public string Message
        {
            get => _message;
            private set
            {
                _message = value;
                OnPropertyChanged(nameof(Message));
                OnPropertyChanged(nameof(HasMessage));
            }
        }

        public bool HasMessage => !string.IsNullOrEmpty(_message);

        public bool HasPresets => Presets.Count > 0;

        public void LoadPresets()
        {
            Presets.Clear();

            foreach (var preset in FastFlagPresetCatalogue.All)
                Presets.Add(new FastFlagPresetItem(preset));

            OnPropertyChanged(nameof(HasPresets));
        }

        public void RefreshStates()
        {
            foreach (var item in Presets)
                item.RefreshState();
        }

        private void Apply(FastFlagPresetItem? item)
        {
            if (item is null)
                return;

            // Show every caveat at once: system risk, and flags owned by another preset
            // or by one of Rainstrap's own controls that the apply would override.
            var notes = new List<string>();

            if (item.IsRisky && !string.IsNullOrEmpty(item.Warning))
                notes.Add(item.Warning);

            if (item.HasConflicts)
                notes.Add(item.ConflictText);

            if (notes.Count > 0)
            {
                string message = string.Format(Strings.FastFlagPresets_BeforeApplyingFormat, item.Name)
                    + Environment.NewLine + Environment.NewLine
                    + string.Join(Environment.NewLine + Environment.NewLine, notes);

                var confirmed = Frontend.ShowMessageBox(
                    message, MessageBoxImage.Warning, MessageBoxButton.YesNo);

                if (confirmed != MessageBoxResult.Yes)
                    return;
            }

            foreach (var (flag, value) in item.Preset.Flags)
                App.FastFlags.SetValue(flag, value);
            MessageTitle = Strings.FastFlagPresets_AppliedTitle;
            Message = string.Format(Strings.FastFlagPresets_AppliedMessage, item.FlagCount);

            RefreshStates();
            RequestPageReloadEvent?.Invoke(this, EventArgs.Empty);
        }

        private void Remove(FastFlagPresetItem? item)
        {
            if (item is null || !item.IsTouched)
                return;

            // Removing clears every flag the preset owns, including the ones Rainstrap's
            // own controls share. Say so up front, since that also resets those controls.
            string message = string.Format(
                Strings.FastFlagPresets_RemoveConfirm_Message, item.Name, item.FlagCount);

            if (item.SharedWithExistingControls.Count > 0)
            {
                message += Environment.NewLine + Environment.NewLine + string.Format(
                    Strings.FastFlagPresets_ResetSharedWithControlsFormat,
                    string.Join(", ", item.SharedWithExistingControls));
            }

            var confirmed = Frontend.ShowMessageBox(
                message, MessageBoxImage.Warning, MessageBoxButton.YesNo);

            if (confirmed != MessageBoxResult.Yes)
                return;

            // Clearing a value hands the flag back to Roblox's own default, which is
            // what "remove" means here. Flags the preset never set are left alone.
            foreach (var flag in item.Preset.Flags.Keys)
                App.FastFlags.SetValue(flag, null);

            MessageTitle = Strings.FastFlagPresets_RemovedTitle;
            Message = string.Format(Strings.FastFlagPresets_RemovedMessage, item.FlagCount);

            RefreshStates();
            RequestPageReloadEvent?.Invoke(this, EventArgs.Empty);
        }
    }
}
