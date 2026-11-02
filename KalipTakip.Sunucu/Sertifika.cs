using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Reflection;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace KalipTakip.Sunucu
{
    /// <summary>https için kendinden imzalı sertifika. Yönetici hakkı gerekmez; exe'nin yanına sertifika.pfx olarak kaydedilir.</summary>
    internal static class Sertifika
    {
        private const string Parola = "kaliptakip";

        public static X509Certificate2 Al()
        {
            string klasor = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            string dosya = Path.Combine(klasor, "sertifika.pfx");
            if (File.Exists(dosya))
            {
                try
                {
                    X509Certificate2 c = new X509Certificate2(dosya, Parola, X509KeyStorageFlags.Exportable | X509KeyStorageFlags.UserKeySet);
                    if (c.NotAfter > DateTime.Now.AddDays(7)) return c;
                }
                catch { }
            }
            X509Certificate2 yeni = Uret();
            try { File.WriteAllBytes(dosya, yeni.Export(X509ContentType.Pfx, Parola)); } catch { }
            return new X509Certificate2(yeni.Export(X509ContentType.Pfx, Parola), Parola, X509KeyStorageFlags.Exportable | X509KeyStorageFlags.UserKeySet);
        }

        private static X509Certificate2 Uret()
        {
            using (RSA rsa = RSA.Create(2048))
            {
                CertificateRequest istek = new CertificateRequest("CN=KalipTakip", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
                SubjectAlternativeNameBuilder san = new SubjectAlternativeNameBuilder();
                san.AddDnsName("localhost");
                san.AddDnsName(Environment.MachineName);
                san.AddIpAddress(IPAddress.Loopback);
                foreach (NetworkInterface ni in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (ni.OperationalStatus != OperationalStatus.Up) continue;
                    foreach (UnicastIPAddressInformation ua in ni.GetIPProperties().UnicastAddresses)
                        if (ua.Address.AddressFamily == AddressFamily.InterNetwork) san.AddIpAddress(ua.Address);
                }
                istek.CertificateExtensions.Add(san.Build());
                istek.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, false));
                istek.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment, false));
                istek.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(new OidCollection { new Oid("1.3.6.1.5.5.7.3.1") }, false));
                return istek.CreateSelfSigned(DateTimeOffset.Now.AddDays(-1), DateTimeOffset.Now.AddYears(10));
            }
        }
    }
}
