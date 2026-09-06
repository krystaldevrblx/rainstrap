namespace Bloxstrap.Plugins
{
    public class AccountManagerPlugin : IPlugin
    {
        public PluginManifest Manifest { get; } = new()
        {
            Id = "rainstrap.accountmanager",
            Name = "Account Manager",
            Version = "1.0.0",
            ApiVersion = "1.0",
            Author = "Rainstrap",
            Description = "Manage multiple Roblox accounts and switch between them.",
            IsOfficial = true,
            Verified = true,
            Permissions = new() { "credentialStorage", "cookieAccess" }
        };

        private IPluginHost? _host;

        public void Initialize(IPluginHost host)
        {
            const string LOG_IDENT = "AccountManagerPlugin::Initialize";

            _host = host;

            App.Accounts.IsAccountManagementActive = true;

            host.RegisterNavigationItem("Accounts", "PeopleList24", "accounts", typeof(UI.Elements.Settings.Pages.AccountsPage));

            host.Logger.WriteLine(LOG_IDENT, "Initialized");
        }

        public void OnShutdown()
        {
            const string LOG_IDENT = "AccountManagerPlugin::OnShutdown";

            _host?.UnregisterNavigationItem("accounts");

            App.Accounts.IsAccountManagementActive = false;

            _host?.Logger.WriteLine(LOG_IDENT, "Shut down");
        }

        public void Dispose()
        {
        }

        public async Task<(SavedAccount Account, bool AlreadyExisted)> AddCurrentAccountAsync(string? displayName = null)
        {
            if (_host is null)
                throw new InvalidOperationException("Plugin not initialized");

            var credCap = _host.RequestCapability(Manifest.Id, PluginCapabilities.CredentialStorage);
            if (!credCap.Granted)
                throw new InvalidOperationException($"Credential storage denied: {credCap.DenialReason}");

            var cookieCap = _host.RequestCapability(Manifest.Id, PluginCapabilities.CookieAccess);
            if (!cookieCap.Granted)
                throw new InvalidOperationException($"Cookie access denied: {cookieCap.DenialReason}");

            return await App.Accounts.AddCurrentAccountAsync(displayName);
        }

        public void RenameAccount(string id, string displayName)
        {
            const string LOG_IDENT = "AccountManagerPlugin::RenameAccount";

            if (_host is null)
                return;

            var credCap = _host.RequestCapability(Manifest.Id, PluginCapabilities.CredentialStorage);
            if (!credCap.Granted)
            {
                _host.Logger.WriteLine(LOG_IDENT, $"Credential storage denied: {credCap.DenialReason}");
                return;
            }

            App.Accounts.RenameAccount(id, displayName);
        }

        public void RemoveAccount(string id)
        {
            const string LOG_IDENT = "AccountManagerPlugin::RemoveAccount";

            if (_host is null)
                return;

            var credCap = _host.RequestCapability(Manifest.Id, PluginCapabilities.CredentialStorage);
            if (!credCap.Granted)
            {
                _host.Logger.WriteLine(LOG_IDENT, $"Credential storage denied: {credCap.DenialReason}");
                return;
            }

            App.Accounts.RemoveAccount(id);
        }

        public void SetActiveAccount(string? id)
        {
            const string LOG_IDENT = "AccountManagerPlugin::SetActiveAccount";

            if (_host is null)
                return;

            var cookieCap = _host.RequestCapability(Manifest.Id, PluginCapabilities.CookieAccess);
            if (!cookieCap.Granted)
            {
                _host.Logger.WriteLine(LOG_IDENT, $"Cookie access denied: {cookieCap.DenialReason}");
                return;
            }

            App.Accounts.SetActiveAccount(id);
        }

        public SavedAccount? GetActiveAccount() => App.Accounts.GetActiveAccount();

        public SavedAccount? GetAccount(string id) => App.Accounts.GetAccount(id);

        public bool HasSecret(string id) => App.Accounts.HasSecret(id);
    }
}
