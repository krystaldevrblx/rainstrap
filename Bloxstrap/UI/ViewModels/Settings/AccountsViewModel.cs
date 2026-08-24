using System.Collections.ObjectModel;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows;
using System.IO;

using CommunityToolkit.Mvvm.Input;

using Bloxstrap.Models;
using Bloxstrap.UI.Elements.Dialogs;

namespace Bloxstrap.UI.ViewModels.Settings
{
    public class AccountCardViewModel : NotifyPropertyChangedViewModel
    {
        public string Id { get; set; } = "";
        public string Title { get; set; } = "";
        public string Username { get; set; } = "";
        public string AvatarUrl { get; set; } = "";
        public long UserId { get; set; }
        public string LastUsedText { get; set; } = "";

        private bool _isActive = false;
        public bool IsActive
        {
            get => _isActive;
            set
            {
                _isActive = value;
                OnPropertyChanged(nameof(IsActive));
                OnPropertyChanged(nameof(ActiveBadgeVisibility));
                OnPropertyChanged(nameof(SetActiveButtonVisibility));
            }
        }

        public Visibility ActiveBadgeVisibility => IsActive ? Visibility.Visible : Visibility.Collapsed;
        public Visibility SetActiveButtonVisibility => IsActive ? Visibility.Collapsed : Visibility.Visible;

        private bool _isLaunching = false;
        public bool IsLaunching
        {
            get => _isLaunching;
            set
            {
                _isLaunching = value;
                OnPropertyChanged(nameof(IsLaunching));
                OnPropertyChanged(nameof(LaunchButtonText));
            }
        }

        public string LaunchButtonText => IsLaunching ? Strings.Accounts_Launching : Strings.Accounts_Launch;

        public ImageSource? AvatarImage { get; set; }
    }

    public class AccountsViewModel : NotifyPropertyChangedViewModel
    {
        public ObservableCollection<AccountCardViewModel> Accounts { get; } = new();

        private bool _isLoading = false;
        public bool IsLoading
        {
            get => _isLoading;
            set
            {
                _isLoading = value;
                OnPropertyChanged(nameof(IsLoading));
                OnPropertyChanged(nameof(LoadingVisibility));
                OnPropertyChanged(nameof(EmptyStateVisibility));
                OnPropertyChanged(nameof(AddAccountEnabled));
            }
        }

        public Visibility LoadingVisibility => IsLoading ? Visibility.Visible : Visibility.Collapsed;

        public bool LaunchButtonsEnabled => !_launchingInstance;

        public Visibility EmptyStateVisibility =>
            (!_isLoading && Accounts.Count == 0) ? Visibility.Visible : Visibility.Collapsed;

        public bool AddAccountEnabled => !_isLoading && !_addingAccount && App.Settings.Prop.AllowCookieAccess;

        public bool CookieAccessDisabled => !App.Settings.Prop.AllowCookieAccess;

        private Visibility _errorVisibility = Visibility.Collapsed;
        public Visibility ErrorVisibility
        {
            get => _errorVisibility;
            set
            {
                _errorVisibility = value;
                OnPropertyChanged(nameof(ErrorVisibility));
            }
        }

        private string _errorMessage = "";
        public string ErrorMessage
        {
            get => _errorMessage;
            set
            {
                _errorMessage = value;
                OnPropertyChanged(nameof(ErrorMessage));
                ErrorVisibility = String.IsNullOrEmpty(value) ? Visibility.Collapsed : Visibility.Visible;
            }
        }

        private bool _addingAccount = false;

        public ICommand RefreshCommand => new RelayCommand(() => LoadAccounts());

        public ICommand AddAccountCommand => new RelayCommand(async () => await AddCurrentAccountAsync());

        public ICommand ActivateAccountCommand => new AsyncRelayCommand<object?>(parameter =>
        {
            if (parameter is string id)
                SetActive(id);

            return Task.CompletedTask;
        });

        public ICommand RenameAccountCommand => new RelayCommand<object?>(parameter =>
        {
            if (parameter is not string id)
                return;

            var account = App.Accounts.GetAccount(id);
            if (account is null)
                return;

            var dialog = new TextInputDialog(account.AccountDisplayName);
            dialog.ShowDialog();

            if (dialog.Result != MessageBoxResult.OK)
                return;

            App.Accounts.RenameAccount(id, dialog.Value);
            LoadAccounts();
        });

        public ICommand RemoveAccountCommand => new RelayCommand<object?>(parameter =>
        {
            if (parameter is not string id)
                return;

            var account = App.Accounts.GetAccount(id);
            if (account is null)
                return;

            var choice = Frontend.ShowMessageBox(
                String.Format(Strings.Accounts_RemoveConfirmText, account.Username),
                MessageBoxImage.Warning,
                MessageBoxButton.YesNo,
                MessageBoxResult.No
            );

            if (choice != MessageBoxResult.Yes)
                return;

            App.Accounts.RemoveAccount(id);
            LoadAccounts();
        });

        public ICommand LaunchAccountCommand => new AsyncRelayCommand<object?>(async parameter =>
        {
            if (parameter is not string id || _launchingInstance)
                return;

            await LaunchForAccountAsync(id);
        });

        private bool _launchingInstance = false;

        public AccountsViewModel()
        {
            LoadAccounts();
        }

        public void LoadAccounts()
        {
            ErrorMessage = "";
            Accounts.Clear();

            foreach (SavedAccount account in App.Accounts.Prop.Items)
            {
                var card = new AccountCardViewModel
                {
                    Id = account.Id,
                    UserId = account.UserId,
                    Username = account.Username,
                    AvatarUrl = account.AvatarUrl,
                    Title = String.IsNullOrWhiteSpace(account.AccountDisplayName) ? account.Username : account.AccountDisplayName,
                    LastUsedText = account.LastUsedAt is null ? "" : String.Format(Strings.Accounts_LastUsed, account.LastUsedAt.Value.ToLocalTime().ToString("g")),
                    IsActive = App.Accounts.Prop.ActiveAccountId == account.Id
                };

                Accounts.Add(card);
                _ = LoadAvatarAsync(card, account);
            }

            OnPropertyChanged(nameof(EmptyStateVisibility));
            OnPropertyChanged(nameof(CookieAccessDisabled));
            OnPropertyChanged(nameof(AddAccountEnabled));
        }

        private async Task LoadAvatarAsync(AccountCardViewModel card, SavedAccount account)
        {
            if (String.IsNullOrEmpty(account.AvatarUrl))
                return;

            try
            {
                // cache avatars on disk so the list loads instantly after the first time
                string cacheDir = Path.Combine(Paths.Base, "AccountAvatars");
                Directory.CreateDirectory(cacheDir);
                string cacheFile = Path.Combine(cacheDir, $"{account.UserId}.png");

                byte[] data;

                if (File.Exists(cacheFile) && File.GetLastWriteTimeUtc(cacheFile).AddDays(3) > DateTime.UtcNow)
                {
                    data = await File.ReadAllBytesAsync(cacheFile);
                }
                else
                {
                    data = await App.HttpClient.GetByteArrayAsync(account.AvatarUrl);
                    await File.WriteAllBytesAsync(cacheFile, data);
                }

                var image = new BitmapImage();
                image.BeginInit();
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.StreamSource = new MemoryStream(data);
                image.EndInit();
                image.Freeze();

                card.AvatarImage = image;
                card.OnPropertyChanged(nameof(card.AvatarImage));
            }
            catch
            {
                // avatar is decorative - ignore failures
            }
        }

        private async Task AddCurrentAccountAsync()
        {
            if (_addingAccount)
                return;

            _addingAccount = true;
            ErrorMessage = "";
            OnPropertyChanged(nameof(AddAccountEnabled));

            try
            {
                IsLoading = true;
                (SavedAccount Account, bool AlreadyExisted) = await App.Accounts.AddCurrentAccountAsync();

                LoadAccounts();

                if (AlreadyExisted)
                    Frontend.ShowMessageBox(
                        String.Format(Strings.Accounts_AlreadySaved, Account.Username),
                        MessageBoxImage.Information
                    );
            }
            catch (Exception ex)
            {
                ErrorMessage = $"{Strings.Accounts_CaptureFailed}\n{ex.Message}";
            }
            finally
            {
                _addingAccount = false;
                IsLoading = false;
                OnPropertyChanged(nameof(AddAccountEnabled));
            }
        }

        private void SetActive(string id)
        {
            App.Accounts.SetActiveAccount(App.Accounts.Prop.ActiveAccountId == id ? null : id);
            LoadAccounts();
        }

        private async Task LaunchForAccountAsync(string id)
        {
            if (_launchingInstance)
                return;

            var card = Accounts.FirstOrDefault(x => x.Id == id);
            if (card is null)
                return;

            // prevent accidental duplicate launches
            if (Utilities.IsRobloxRunning() && !App.Settings.Prop.MultiInstanceLaunching)
            {
                var choice = Frontend.ShowMessageBox(
                    Strings.MultiInstance_ConfirmParallel,
                    MessageBoxImage.Warning,
                    MessageBoxButton.YesNo,
                    MessageBoxResult.No
                );

                if (choice != MessageBoxResult.Yes)
                    return;
            }

            _launchingInstance = true;
            OnPropertyChanged(nameof(LaunchButtonsEnabled));
            card.IsLaunching = true;

            try
            {
                // make this the active account so the launch applies it
                App.Accounts.SetActiveAccount(id);

                using var process = Process.Start(new ProcessStartInfo
                {
                    FileName = Paths.Process,
                    Arguments = $"-player -account {id}",
                    UseShellExecute = false
                });

                if (process is null)
                    Frontend.ShowMessageBox(Strings.Accounts_LaunchFailed, MessageBoxImage.Error);

                await Task.Delay(1500); // brief cooldown so the button cannot be double clicked
            }
            catch (Exception ex)
            {
                Frontend.ShowMessageBox($"{Strings.Accounts_LaunchFailed}\n{ex.Message}", MessageBoxImage.Error);
            }
            finally
            {
                _launchingInstance = false;
                OnPropertyChanged(nameof(LaunchButtonsEnabled));
                card.IsLaunching = false;
                LoadAccounts();
            }
        }
    }
}
