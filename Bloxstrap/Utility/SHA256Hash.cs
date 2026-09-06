using System.Security.Cryptography;

namespace Bloxstrap.Utility
{
    public static class SHA256Hash
    {
        public static string FromFile(string filename)
        {
            using SHA256 sha256 = SHA256.Create();
            using FileStream stream = File.OpenRead(filename);
            byte[] hash = sha256.ComputeHash(stream);
            return Stringify(hash);
        }

        public static string FromBytes(byte[] data)
        {
            using SHA256 sha256 = SHA256.Create();
            return Stringify(sha256.ComputeHash(data));
        }

        public static string Stringify(byte[] hash)
        {
            return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
        }

        /// <summary>
        /// Extracts a SHA-256 hash from a release body string.
        /// Looks for a line matching "SHA256: &lt;hex hash&gt;".
        /// Returns null if not found.
        /// </summary>
        public static string? ExtractFromReleaseBody(string body)
        {
            if (string.IsNullOrEmpty(body))
                return null;

            foreach (string line in body.Split('\n'))
            {
                string trimmed = line.Trim();
                if (trimmed.StartsWith("SHA256:", StringComparison.OrdinalIgnoreCase))
                {
                    string hash = trimmed["SHA256:".Length..].Trim();
                    if (hash.Length == 64 && System.Text.RegularExpressions.Regex.IsMatch(hash, @"^[0-9a-fA-F]{64}$"))
                        return hash.ToLowerInvariant();
                }
            }

            return null;
        }
    }
}
