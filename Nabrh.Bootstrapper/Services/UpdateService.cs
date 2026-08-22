using System;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Nabrh.Bootstrapper.Services
{
    public sealed class UpdateManifest
    {
        public Version Version { get; }
        public Uri BundleUrl { get; }
        public long SizeBytes { get; }
        public string Sha256 { get; }
        public string CoreMd5 { get; }
        public string SignatureBase64 { get; }

        public UpdateManifest(Version version, Uri bundleUrl, long sizeBytes, string sha256, string coreMd5, string signatureBase64)
        {
            Version = version;
            BundleUrl = bundleUrl;
            SizeBytes = sizeBytes;
            Sha256 = sha256;
            CoreMd5 = coreMd5;
            SignatureBase64 = signatureBase64;
        }
    }

    public static class UpdateService
    {
        public static async Task ApplyUpdateAsync(UpdateManifest manifest, byte[] rsaPublicKeyDer, CancellationToken ct)
        {
            if (manifest == null) throw new ArgumentNullException(nameof(manifest));
            if (rsaPublicKeyDer == null) throw new ArgumentNullException(nameof(rsaPublicKeyDer));

            // 1. Verify Feed Signature against Embedded Public Key
            byte[] sha256Bytes = Encoding.UTF8.GetBytes(manifest.Sha256);
            byte[] sigBytes = Convert.FromBase64String(manifest.SignatureBase64);

            bool isSigValid;
            using (var rsa = RSA.Create())
            {
                rsa.ImportSubjectPublicKeyInfo(rsaPublicKeyDer, out _);
                isSigValid = rsa.VerifyData(sha256Bytes, sigBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            }

            if (!isSigValid)
            {
                throw new InvalidOperationException("حزمة التحديث غير موثوقة (توقيع التحديث غير صالح).");
            }

            // 2. Download Bundle to Local Temp
            string tempBundlePath = Path.Combine(Path.GetTempPath(), "ERPUI.Update.exe");
            using (var http = new HttpClient())
            {
                byte[] bundleBytes = await http.GetByteArrayAsync(manifest.BundleUrl);
                File.WriteAllBytes(tempBundlePath, bundleBytes);
            }

            // 3. Verify Download SHA-256
            byte[] hash;
            using (var sha256 = SHA256.Create())
            {
                hash = sha256.ComputeHash(File.ReadAllBytes(tempBundlePath));
            }
            string hashHex = BitConverter.ToString(hash).Replace("-", "").ToUpperInvariant();

            if (!hashHex.Equals(manifest.Sha256.Replace("-", "").ToUpperInvariant(), StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("ملف التحديث تالف (عدم تطابق مجموع SHA-256).");
            }

            // 4. Hand-off execution to new installer bundle
            System.Diagnostics.Process.Start(tempBundlePath, "/update");
            Environment.Exit(0);
        }
    }
}

