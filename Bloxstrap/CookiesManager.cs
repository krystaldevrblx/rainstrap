using Bloxstrap.RobloxInterfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Bloxstrap
{
    public class CookiesManager
    {
        private CookieState _state = CookieState.Unknown;

        public EventHandler<CookieState>? StateChanged;
        public CookieState State {
            get => _state;
            set {
                _state = value;
                StateChanged?.Invoke(this, value);
            }
        }
        public bool Loaded => Enabled && State == CookieState.Success;
        private bool Enabled => App.Settings.Prop.AllowCookieAccess;

        private string AuthCookie = string.Empty;
        private const string AuthCookieName = ".ROBLOSECURITY";
        private const string SupportedVersion = "1";
        private const string AuthPattern = $@"\t{AuthCookieName}\t(.+?)(;|$)";
        private string CookiesPath => Path.Combine(Paths.Roblox, "LocalStorage", Deployment.IsDefaultRobloxDomain ? "RobloxCookies.dat" : $"{Deployment.RobloxDomain}_RobloxCookies.dat");

        /// <summary>
        /// The authentication token currently held by this manager.
        /// This must never be logged, displayed, or persisted anywhere in plaintext.
        /// </summary>
        public string AuthCookieValue => AuthCookie;

        /// <summary>
        /// Writes an authentication token into the local Roblox client's cookie storage,
        /// so that the next launched Roblox client signs into the corresponding account.
        /// A backup of the previous state is kept, and restored if writing fails.
        /// </summary>
        public bool WriteAuthCookie(string token)
        {
            const string LOG_IDENT = "CookiesManager::WriteAuthCookie";

            if (!Enabled)
            {
                App.Logger.WriteLine(LOG_IDENT, "Cookie access is not enabled");
                return false;
            }

            if (String.IsNullOrEmpty(token))
                throw new ArgumentException("Token must not be empty", nameof(token));

            string backupPath = CookiesPath + ".rainstrap-backup";

            try
            {
                // preserve the rest of the cookie jar so only the auth cookie entry changes
                string rawCookies = "";

                if (File.Exists(CookiesPath))
                {
                    string content = File.ReadAllText(CookiesPath);
                    RobloxCookies cookies = JsonSerializer.Deserialize<RobloxCookies>(content)!;
                    byte[] encryptedData = Convert.FromBase64String(cookies.Cookies);
                    rawCookies = Encoding.UTF8.GetString(ProtectedData.Unprotect(encryptedData, null, DataProtectionScope.CurrentUser));

                    // back up the previous state before modifying anything
                    File.Copy(CookiesPath, backupPath, true);
                }

                string newEntry = $"\t{AuthCookieName}\t{token};";

                if (Regex.IsMatch(rawCookies, AuthPattern))
                    rawCookies = Regex.Replace(rawCookies, AuthPattern, _ => newEntry);
                else
                    rawCookies += newEntry;

                byte[] newData = ProtectedData.Protect(Encoding.UTF8.GetBytes(rawCookies), null, DataProtectionScope.CurrentUser);
                var newCookies = new RobloxCookies
                {
                    Version = SupportedVersion,
                    Cookies = Convert.ToBase64String(newData)
                };

                Directory.CreateDirectory(Path.GetDirectoryName(CookiesPath)!);
                File.WriteAllText(CookiesPath, JsonSerializer.Serialize(newCookies));

                // keep the in-memory token consistent with what is now on disk
                AuthCookie = token;
                State = CookieState.Success;

                App.Logger.WriteLine(LOG_IDENT, "Updated stored authentication state for the next launch");
                return true;
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine(LOG_IDENT, $"Failed to update stored authentication state: {ex.Message}");

                // never leave the client's storage in a broken state
                try
                {
                    if (File.Exists(backupPath))
                    {
                        File.Copy(backupPath, CookiesPath, true);
                        App.Logger.WriteLine(LOG_IDENT, "Restored previous authentication state from backup");
                    }
                }
                catch (Exception restoreEx)
                {
                    App.Logger.WriteLine(LOG_IDENT, $"Failed to restore backup: {restoreEx.Message}");
                }

                return false;
            }
        }

        /// <summary>
        /// Restores the backed-up authentication state, if one exists.
        /// </summary>
        public void RestoreAuthCookieBackup()
        {
            const string LOG_IDENT = "CookiesManager::RestoreAuthCookieBackup";

            string backupPath = CookiesPath + ".rainstrap-backup";

            if (!File.Exists(backupPath))
                return;

            try
            {
                File.Copy(backupPath, CookiesPath, true);
                File.Delete(backupPath);
                App.Logger.WriteLine(LOG_IDENT, "Restored previous authentication state from backup");
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine(LOG_IDENT, $"Failed to restore backup: {ex.Message}");
            }
        }


        public async Task<HttpResponseMessage> AuthRequest(HttpRequestMessage request)
        {
            string? host = request.RequestUri?.Host;

            // basic host validation in case we accidentally send authenticated request somewhere unwanted
            if (host is null)
                throw new ArgumentNullException("Host cannot be null");

            if (
                !host.Equals(Deployment.RobloxDomain, StringComparison.OrdinalIgnoreCase) &&
                !host.EndsWith("." + Deployment.RobloxDomain, StringComparison.OrdinalIgnoreCase)
                )
                throw new HttpRequestException($"Host must end with Roblox domain ({Deployment.RobloxDomain})");

            if (!Enabled)
                throw new NullReferenceException("Cookie access is not enabled");

            request.Headers.Add("Cookie", $".ROBLOSECURITY={AuthCookie}");
            var response = await App.HttpClient.SendAsync(request);

            return response;
        }

        public async Task<HttpResponseMessage> AuthGet(Uri? uri) => await AuthRequest(new HttpRequestMessage { RequestUri = uri, Method = HttpMethod.Get });
        public async Task<HttpResponseMessage> AuthPost(Uri? uri, HttpContent? content) => await AuthRequest(new HttpRequestMessage { RequestUri = uri, Content = content, Method = HttpMethod.Post });

        public async Task<AuthenticatedUser?> GetAuthenticated()
        {
            const string LOG_IDENT = "CookiesManager::GetAuthenticated";
            
            try
            {
                Uri apiUrl = UrlBuilder.BuildApiUrl("users", "v1/users/authenticated");
                HttpResponseMessage response = await AuthGet(apiUrl);
                response.EnsureSuccessStatusCode();

                string content = await response.Content.ReadAsStringAsync();
                AuthenticatedUser user = JsonSerializer.Deserialize<AuthenticatedUser>(content)!;

                return user;
            }
            catch (HttpRequestException ex)
            {
                App.Logger.WriteLine(LOG_IDENT, "Failed to get authenticated user");
                App.Logger.WriteException(LOG_IDENT, ex);
            }

            return null;
        }

        public async Task LoadCookies()
        {
            const string LOG_IDENT = "CookiesManager::LoadCookies";

            // we use the status to infrom user about it in the menu
            if (!Enabled)
            {
                State = CookieState.NotAllowed;
                App.Logger.WriteLine(LOG_IDENT, "Cookie access not allowed");
                return;
            }

            if (!string.IsNullOrEmpty(AuthCookie))
            {
                App.Logger.WriteLine(LOG_IDENT, "Cookie was already loaded!");
                return;
            }

            if (!File.Exists(CookiesPath))
            {
                State = CookieState.NotFound;
                App.Logger.WriteLine(LOG_IDENT, "Cookie file not found");
                return;
            }

            try
            {
                string content = File.ReadAllText(CookiesPath);
                var cookies = JsonSerializer.Deserialize<RobloxCookies>(content)!;

                if (cookies.Version != SupportedVersion)
                    App.Logger.WriteLine(LOG_IDENT, $"Unknown cookie version: {cookies.Version}");

                // here we got the raw bytes data which we have to decrypt with user scope
                // from that we get raw cookies data in roblox's format
                // in our case we will regex it since all we need is auth cookie
                byte[] encryptedData = Convert.FromBase64String(cookies.Cookies);
                byte[] unencryptedData = ProtectedData.Unprotect(encryptedData, null, DataProtectionScope.CurrentUser);

                string rawCookies = Encoding.UTF8.GetString(unencryptedData);
                Match authCookieMatch = Regex.Match(rawCookies, AuthPattern);

                if (!authCookieMatch.Success)
                {
                    State = CookieState.Invalid;
                    App.Logger.WriteLine(LOG_IDENT, "Regex failed for cookies");
                    return;
                }

                string authCookie = authCookieMatch.Groups[1].Value;
                AuthCookie = authCookie; // could use better naming

                // we test the cookie to see if its valid
                AuthenticatedUser? user = await GetAuthenticated();
                if (user is null || user?.Id == 0)
                {
                    State = CookieState.Invalid;
                    App.Logger.WriteLine(LOG_IDENT, "Cookie is invalid");
                    return;
                }

                State = CookieState.Success;
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine(LOG_IDENT, "Failed to load cookie!");
                App.Logger.WriteException(LOG_IDENT, ex); 

                State = CookieState.Failed;
            }

            return;
        }
    }
}
