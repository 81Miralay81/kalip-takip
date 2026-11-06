using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Web.Script.Serialization;

namespace KalipTakip.Sunucu
{
    /// <summary>
    /// Şifreli gizli ayar deposu (gizli.ayar). API anahtarları, veritabanı yolu/bağlantı bilgileri ve IFS parolaları burada tutulur;
    /// yapılandırma (.config) dosyasında düz metin olarak durmaz.
    ///
    /// Şifreleme: veri rastgele 256 bit ana anahtarla AES-CBC + HMAC-SHA256 ile şifrelenir.
    /// Ana anahtar Windows DPAPI (bu Windows kullanıcısı) ile korunur: dosya başka bilgisayara/kullanıcıya kopyalanırsa açılmaz.
    /// Yönetici ayrıca bir "kurtarma parolası" belirler; ana anahtar bu parolayla da sarılıp dosyaya konur.
    /// Bilgisayar/kullanıcı değişirse ya da DPAPI açamazsa dosya kurtarma parolasıyla yeniden açılır.
    /// </summary>
    internal static class Gizli
    {
        private sealed class Zarf
        {
            public int s { get; set; }          // sürüm
            public string dp { get; set; }      // DPAPI ile korunmuş ana anahtar
            public string kt { get; set; }      // kurtarma: PBKDF2 tuzu
            public string kiv { get; set; }     // kurtarma: IV
            public string kk { get; set; }      // kurtarma: parolayla şifrelenmiş ana anahtar
            public string iv { get; set; }      // veri IV
            public string c { get; set; }       // veri (şifreli)
            public string m { get; set; }       // HMAC
        }

        private static readonly object Kilit = new object();
        private static readonly JavaScriptSerializer Js = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
        private static string _dosya;
        private static byte[] _ana;
        private static Dictionary<string, string> _veri = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private static Zarf _zarf;
        private static bool _yuklendi;

        /// <summary>DPAPI açamadı: kurtarma parolası gerekir.</summary>
        public static bool Kilitli { get { Yukle(); return _ana == null; } }
        public static bool KurtarmaVar { get { Yukle(); return _zarf != null && !string.IsNullOrEmpty(_zarf.kk); } }
        public static string Dosya { get { return _dosya ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "gizli.ayar"); } }

        /// <summary>Sınama için dosya yolu değiştirilir.</summary>
        public static void DosyaAyarla(string yol)
        {
            lock (Kilit) { _dosya = yol; _yuklendi = false; _ana = null; _zarf = null; _veri = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase); }
        }

        // ------------------------------------------------------------ okuma / yazma

        public static string Al(string ad)
        {
            lock (Kilit)
            {
                Yukle();
                string v;
                return _ana != null && _veri.TryGetValue(ad, out v) && !string.IsNullOrEmpty(v) ? v : null;
            }
        }

        public static List<string> Anahtarlar()
        {
            lock (Kilit) { Yukle(); return _ana == null ? new List<string>() : _veri.Keys.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList(); }
        }

        public static void Yaz(string ad, string deger)
        {
            if (string.IsNullOrWhiteSpace(ad)) throw new ArgumentException("Ad boş olamaz.");
            lock (Kilit)
            {
                Yukle();
                if (_ana == null) throw new InvalidOperationException("Gizli ayar dosyası kilitli: önce kurtarma parolasıyla açın.");
                if (string.IsNullOrEmpty(deger)) _veri.Remove(ad.Trim()); else _veri[ad.Trim()] = deger;
                Kaydet();
            }
        }

        public static void Sil(string ad) { Yaz(ad, null); }

        // ------------------------------------------------------------ kurtarma

        public static void KurtarmaParolasiBelirle(string parola)
        {
            if (string.IsNullOrEmpty(parola) || parola.Length < 8) throw new ArgumentException("Kurtarma parolası en az 8 karakter olmalı.");
            lock (Kilit)
            {
                Yukle();
                if (_ana == null) throw new InvalidOperationException("Gizli ayar dosyası kilitli.");
                byte[] tuz = Rastgele(16), iv = Rastgele(16);
                byte[] anahtar = Turet(parola, tuz);
                _zarf.kt = Convert.ToBase64String(tuz);
                _zarf.kiv = Convert.ToBase64String(iv);
                _zarf.kk = Convert.ToBase64String(AesSifrele(_ana, anahtar, iv));
                Kaydet();
            }
        }

        /// <summary>Kilitli dosyayı kurtarma parolasıyla açar ve DPAPI ile yeniden korur. Yanlış parolada false döner.</summary>
        public static bool KurtarmaIleAc(string parola)
        {
            lock (Kilit)
            {
                Yukle();
                if (_zarf == null || string.IsNullOrEmpty(_zarf.kk)) return false;
                try
                {
                    byte[] ana = AesCoz(Convert.FromBase64String(_zarf.kk), Turet(parola ?? "", Convert.FromBase64String(_zarf.kt)), Convert.FromBase64String(_zarf.kiv));
                    Dictionary<string, string> veri = VeriCoz(_zarf, ana);
                    _ana = ana; _veri = veri;
                    Kaydet();
                    return true;
                }
                catch { return false; }
            }
        }

        // ------------------------------------------------------------ iç işler

        private static void Yukle()
        {
            if (_yuklendi) return;
            _yuklendi = true;
            string d = Dosya;
            if (!File.Exists(d))
            {
                _ana = Rastgele(32);
                _veri = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                _zarf = new Zarf { s = 1 };
                Kaydet();
                return;
            }
            _zarf = Js.Deserialize<Zarf>(File.ReadAllText(d, Encoding.UTF8));
            try
            {
                byte[] ana = ProtectedData.Unprotect(Convert.FromBase64String(_zarf.dp), null, DataProtectionScope.CurrentUser);
                _veri = VeriCoz(_zarf, ana);
                _ana = ana;
            }
            catch
            {
                _ana = null;     // kilitli
                _veri = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }
        }

        private static void Kaydet()
        {
            byte[] iv = Rastgele(16);
            byte[] sifreli = AesSifrele(Encoding.UTF8.GetBytes(Js.Serialize(_veri)), Turet2(_ana, "E"), iv);
            _zarf.s = 1;
            _zarf.dp = Convert.ToBase64String(ProtectedData.Protect(_ana, null, DataProtectionScope.CurrentUser));
            _zarf.iv = Convert.ToBase64String(iv);
            _zarf.c = Convert.ToBase64String(sifreli);
            _zarf.m = Convert.ToBase64String(Mac(_ana, iv, sifreli));
            string gecici = Dosya + ".tmp";
            File.WriteAllText(gecici, Js.Serialize(_zarf), Encoding.UTF8);
            if (File.Exists(Dosya)) File.Delete(Dosya);
            File.Move(gecici, Dosya);
        }

        private static Dictionary<string, string> VeriCoz(Zarf z, byte[] ana)
        {
            byte[] iv = Convert.FromBase64String(z.iv), c = Convert.FromBase64String(z.c);
            byte[] mac = Mac(ana, iv, c), beklenen = Convert.FromBase64String(z.m);
            if (!Esit(mac, beklenen)) throw new CryptographicException("Doğrulama başarısız.");
            string json = Encoding.UTF8.GetString(AesCoz(c, Turet2(ana, "E"), iv));
            Dictionary<string, string> d = Js.Deserialize<Dictionary<string, string>>(json) ?? new Dictionary<string, string>();
            return new Dictionary<string, string>(d, StringComparer.OrdinalIgnoreCase);
        }

        private static byte[] Rastgele(int n)
        {
            byte[] b = new byte[n];
            using (RNGCryptoServiceProvider r = new RNGCryptoServiceProvider()) r.GetBytes(b);
            return b;
        }

        private static byte[] Turet(string parola, byte[] tuz)
        {
            using (Rfc2898DeriveBytes k = new Rfc2898DeriveBytes(parola, tuz, 150000)) return k.GetBytes(32);
        }

        private static byte[] Turet2(byte[] ana, string amac)
        {
            using (SHA256 h = SHA256.Create()) return h.ComputeHash(ana.Concat(Encoding.ASCII.GetBytes(amac)).ToArray());
        }

        private static byte[] Mac(byte[] ana, byte[] iv, byte[] c)
        {
            using (HMACSHA256 h = new HMACSHA256(Turet2(ana, "M"))) return h.ComputeHash(iv.Concat(c).ToArray());
        }

        private static bool Esit(byte[] a, byte[] b)
        {
            if (a == null || b == null || a.Length != b.Length) return false;
            int f = 0;
            for (int i = 0; i < a.Length; i++) f |= a[i] ^ b[i];
            return f == 0;
        }

        private static byte[] AesSifrele(byte[] veri, byte[] anahtar, byte[] iv)
        {
            using (Aes a = Aes.Create())
            {
                a.Key = anahtar; a.IV = iv; a.Mode = CipherMode.CBC; a.Padding = PaddingMode.PKCS7;
                using (ICryptoTransform t = a.CreateEncryptor()) return t.TransformFinalBlock(veri, 0, veri.Length);
            }
        }

        private static byte[] AesCoz(byte[] veri, byte[] anahtar, byte[] iv)
        {
            using (Aes a = Aes.Create())
            {
                a.Key = anahtar; a.IV = iv; a.Mode = CipherMode.CBC; a.Padding = PaddingMode.PKCS7;
                using (ICryptoTransform t = a.CreateDecryptor()) return t.TransformFinalBlock(veri, 0, veri.Length);
            }
        }

        // ------------------------------------------------------------ config'ten taşıma

        /// <summary>Gizli sayılan anahtarlar (yapılandırma dosyasında düz metin durmamalı).</summary>
        public static readonly string[] TasinacakAnahtarlar = { "VeritabaniYolu", "GeminiAnahtar", "AnthropicAnahtar" };

        /// <summary>
        /// Yapılandırma dosyasındaki gizli değerleri şifreli depoya alır ve dosyada değerlerini boşaltır.
        /// Dönen liste: taşınan anahtar adları.
        /// </summary>
        public static List<string> ConfigtenTasi()
        {
            List<string> tasinan = new List<string>();
            string yol = AppDomain.CurrentDomain.SetupInformation.ConfigurationFile;
            if (string.IsNullOrEmpty(yol) || !File.Exists(yol)) throw new InvalidOperationException("Yapılandırma dosyası bulunamadı.");
            string metin = File.ReadAllText(yol, Encoding.UTF8);
            foreach (string ad in TasinacakAnahtarlar)
            {
                System.Text.RegularExpressions.Regex r = new System.Text.RegularExpressions.Regex("(<add\\s+key=\"" + System.Text.RegularExpressions.Regex.Escape(ad) + "\"\\s+value=\")([^\"]*)(\")");
                System.Text.RegularExpressions.Match m = r.Match(metin);
                if (!m.Success) continue;
                string deger = System.Net.WebUtility.HtmlDecode(m.Groups[2].Value);
                if (deger.Trim().Length == 0) continue;
                if (Al(ad) == null) Yaz(ad, deger.Trim());
                metin = r.Replace(metin, "${1}${3}", 1);
                tasinan.Add(ad);
            }
            if (tasinan.Count > 0) File.WriteAllText(yol, metin, new UTF8Encoding(false));
            return tasinan;
        }

        /// <summary>Gizli depo değeri; yoksa yapılandırma dosyasındaki değer.</summary>
        public static string AyarAl(string ad)
        {
            string v = Al(ad);
            if (v != null) return v;
            string c = System.Configuration.ConfigurationManager.AppSettings[ad];
            return c == null ? "" : c.Trim();
        }

        /// <summary>Panelde değeri gizlemek için: ilk 2 ve son 2 karakter dışında yıldız.</summary>
        public static string Maske(string deger)
        {
            if (string.IsNullOrEmpty(deger)) return "";
            if (deger.Length <= 6) return new string('•', deger.Length);
            return deger.Substring(0, 2) + new string('•', Math.Min(12, deger.Length - 4)) + deger.Substring(deger.Length - 2);
        }
    }
}
