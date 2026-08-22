using System;
using System.Security.Cryptography;
using System.Text;

namespace Nabrh.Bootstrapper.Services
{
    /// <summary>
    /// Offline installation-protection license gate.
    ///
    /// HONEST SCOPE (AGENTS.md §13.1/§13.7): this is a client-side format+checksum gate that stops
    /// casual/unlicensed installs. It is NOT a cryptographic authorization boundary — the durable
    /// control is a server-issued, RSA-signed license file verified against the embedded public key
    /// (plan §10.B) plus Authenticode. The checksum here is a self-contained seam so the wizard can
    /// enforce a real accept/reject offline; swap Validate() for a signature check when the licensing
    /// backend exists, without touching the wizard.
    ///
    /// Key shape: 5 groups of 5 Crockford-base32 chars — GGGGG-GGGGG-GGGGG-GGGGG-CCCCC — where the
    /// final group is an in-house checksum over the first 20 characters.
    /// </summary>
    public static class LicenseValidator
    {
        // Crockford base32 (excludes I, L, O, U to avoid transcription errors).
        private const string Alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";
        private const string ChecksumSalt = "ERPUI-GOV-2026";

        public sealed class Result
        {
            public bool IsValid { get; }
            public string Message { get; }
            public Result(bool isValid, string message) { IsValid = isValid; Message = message; }
        }

        public static Result Validate(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return new Result(false, "الرجاء إدخال مفتاح الترخيص.");

            string norm = Normalize(raw!);
            if (norm.Length != 25)
                return new Result(false, "صيغة المفتاح غير صحيحة (يجب أن يتكوّن من ٢٥ خانة).");

            foreach (char c in norm)
            {
                if (Alphabet.IndexOf(c) < 0)
                    return new Result(false, "يحتوي المفتاح على رموز غير صالحة.");
            }

            string payload = norm.Substring(0, 20);
            string check = norm.Substring(20, 5);
            if (!string.Equals(check, Checksum(payload), StringComparison.Ordinal))
                return new Result(false, "مفتاح الترخيص غير صالح أو غير مطابق لهذا الإصدار.");

            return new Result(true, "تم التحقّق من مفتاح الترخيص بنجاح.");
        }

        /// <summary>Normalizes user input: uppercases, strips dashes/spaces, folds I/L→1 and O→0.</summary>
        public static string Normalize(string raw)
        {
            var sb = new StringBuilder(25);
            foreach (char ch in raw.Trim().ToUpperInvariant())
            {
                char c = ch;
                if (c == '-' || c == ' ') continue;
                if (c == 'I' || c == 'L') c = '1';
                else if (c == 'O') c = '0';
                else if (c == 'U') c = 'V';
                sb.Append(c);
            }
            return sb.ToString();
        }

        /// <summary>Formats a normalized 25-char key as GGGGG-GGGGG-GGGGG-GGGGG-CCCCC.</summary>
        public static string Format(string norm)
        {
            if (norm.Length != 25) return norm;
            return $"{norm.Substring(0, 5)}-{norm.Substring(5, 5)}-{norm.Substring(10, 5)}-{norm.Substring(15, 5)}-{norm.Substring(20, 5)}";
        }

        /// <summary>
        /// Produces a valid key from a 20-char base32 payload. For licensing tooling and tests only —
        /// production keys are issued and signed server-side.
        /// </summary>
        public static string Generate(string payload20)
        {
            if (payload20 == null || payload20.Length != 20)
                throw new ArgumentException("Payload must be exactly 20 base32 characters.", nameof(payload20));
            return Format(payload20.ToUpperInvariant() + Checksum(payload20.ToUpperInvariant()));
        }

        private static string Checksum(string payload)
        {
            using (var sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(ChecksumSalt + "|" + payload));
                var sb = new StringBuilder(5);
                for (int i = 0; i < 5; i++)
                    sb.Append(Alphabet[hash[i] % Alphabet.Length]);
                return sb.ToString();
            }
        }
    }
}

