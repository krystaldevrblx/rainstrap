namespace Bloxstrap.Models.Persistable
{
    /// <summary>
    /// Persisted Account Manager metadata. Contains no secrets.
    /// </summary>
    public class Accounts
    {
        public List<SavedAccount> Items { get; set; } = new();

        public string? ActiveAccountId { get; set; } = null;
    }
}
