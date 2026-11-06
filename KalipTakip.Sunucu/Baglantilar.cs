using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Data.SQLite;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace KalipTakip.Sunucu
{
    /// <summary>
    /// Dış veri kaynakları (IFS tabloları): bağlantı tanımları şifreli depoda (Gizli) tutulur.
    /// Her kaynak için bir SELECT sorgusu çalıştırılır; sonuç yerel veritabanında IFS_&lt;ad&gt; tablosuna yazılır.
    /// Değişmeyen satırlar tekrar yazılmaz (satır özeti/hash karşılaştırılır); bir hücre bile değiştiyse o satır güncellenir.
    /// Kaynaktan kaybolan satırlar silinmez, "_silindi = 1" işaretlenir.
    /// </summary>
    internal sealed class VkEsle { public string hedef { get; set; } public string kaynak { get; set; } }

    internal sealed class VkTanim
    {
        public string id { get; set; }
        public string ad { get; set; }
        public string tur { get; set; }          // sqlserver | oracle
        public string baglanti { get; set; }     // bağlantı cümlesi (parola içerir; panelde gösterilmez)
        public string sorgu { get; set; }        // SELECT ...
        public string anahtar { get; set; }      // sorgu sonucunda satırı tanıtan sütun(lar), virgülle
        public string hedef { get; set; }        // yerel tablo adı (IFS_ öneki eklenir)
        public int aralikDk { get; set; }        // 0 = yalnız elle
        public bool aktif { get; set; }
        public bool silinenleriIsaretle { get; set; }
        public string sonCalisma { get; set; }
        public string sonSonuc { get; set; }
        // IFS'e yazma (gönderim): kalıp başına tek satır, anahtara göre güncelle / yoksa ekle
        public bool yazAktif { get; set; }
        public string yazBaglanti { get; set; }  // boşsa baglanti kullanılır
        public string yazTablo { get; set; }
        public string yazAnahtar { get; set; }   // hedef tablodaki anahtar sütun (KalipKodu buraya yazılır)
        public List<VkEsle> yazEslesme { get; set; }
        public string sonYazma { get; set; }
        public string sonYazSonuc { get; set; }
    }

    internal sealed class VkSonuc
    {
        public int Yeni, Degisen, Ayni, Silinen, Kopya;
        public long Ms;
        public string Ozet()
        {
            return Yeni + " yeni, " + Degisen + " güncellendi, " + Ayni + " aynı, " + Silinen + " kaynakta kalmadı" + (Kopya > 0 ? ", " + Kopya + " tekrarlı anahtar" : "") + " (" + (Ms / 1000.0).ToString("0.0", CultureInfo.InvariantCulture) + " sn)";
        }
    }

    internal static partial class Vk
    {
        private const string Anahtar = "vk.liste";
        private static readonly object Kilit = new object();
        private static readonly object CalismaKilidi = new object();
        private static readonly System.Web.Script.Serialization.JavaScriptSerializer Js = new System.Web.Script.Serialization.JavaScriptSerializer { MaxJsonLength = int.MaxValue };
        private static Timer _zamanlayici;

        // ------------------------------------------------------------ tanımlar

        public static List<VkTanim> Liste()
        {
            lock (Kilit)
            {
                string j = Gizli.Al(Anahtar);
                if (string.IsNullOrEmpty(j)) return new List<VkTanim>();
                return Js.Deserialize<List<VkTanim>>(j) ?? new List<VkTanim>();
            }
        }

        private static void Yaz(List<VkTanim> l) { lock (Kilit) Gizli.Yaz(Anahtar, Js.Serialize(l)); }

        public static VkTanim Kaydet(VkTanim t, bool baglantiDegisti)
        {
            lock (Kilit)
            {
                List<VkTanim> l = Liste();
                VkTanim eski = string.IsNullOrEmpty(t.id) ? null : l.FirstOrDefault(x => x.id == t.id);
                Dogrula(t, eski, baglantiDegisti);
                if (eski == null) { t.id = Guid.NewGuid().ToString("N").Substring(0, 8); l.Add(t); }
                else
                {
                    if (!baglantiDegisti) t.baglanti = eski.baglanti;
                    if (string.IsNullOrWhiteSpace(t.yazBaglanti)) t.yazBaglanti = eski.yazBaglanti;
                    t.sonCalisma = eski.sonCalisma; t.sonSonuc = eski.sonSonuc; t.sonYazma = eski.sonYazma; t.sonYazSonuc = eski.sonYazSonuc;
                    l[l.IndexOf(eski)] = t;
                }
                Yaz(l);
                return t;
            }
        }

        public static void Sil(string id)
        {
            lock (Kilit) { List<VkTanim> l = Liste(); l.RemoveAll(x => x.id == id); Yaz(l); }
        }

        private static void Dogrula(VkTanim t, VkTanim eski, bool baglantiDegisti)
        {
            if (string.IsNullOrWhiteSpace(t.ad)) throw new ArgumentException("Bağlantıya bir ad verin.");
            if (t.tur != "sqlserver" && t.tur != "oracle") throw new ArgumentException("Tür SQL Server ya da Oracle olmalı.");
            if ((eski == null || baglantiDegisti) && string.IsNullOrWhiteSpace(t.baglanti)) throw new ArgumentException("Bağlantı cümlesi boş olamaz.");
            bool oku = !string.IsNullOrWhiteSpace(t.sorgu);
            if (!oku && string.IsNullOrWhiteSpace(t.yazTablo)) throw new ArgumentException("Okuma sorgusu (SELECT) ya da yazılacak IFS tablosu girin.");
            if (oku)
            {
                t.sorgu = SorguTemizle(t.sorgu);
                if (string.IsNullOrWhiteSpace(t.anahtar)) throw new ArgumentException("Anahtar sütun(lar) belirtin (satırı tek başına tanıtan sütun, ör. CONTRACT,PART_NO).");
                string h = (t.hedef ?? "").Trim();
                if (h.StartsWith("IFS_", StringComparison.OrdinalIgnoreCase)) h = h.Substring(4);
                if (!Regex.IsMatch(h, "^[A-Za-z][A-Za-z0-9_]{0,40}$")) throw new ArgumentException("Hedef tablo adı harfle başlamalı; yalnız harf, rakam ve alt çizgi (en çok 40 karakter).");
                t.hedef = h;
            }
            else { t.sorgu = ""; t.anahtar = t.anahtar ?? ""; t.hedef = t.hedef ?? ""; }
            DogrulaYaz(t);
            if (t.aralikDk < 0) t.aralikDk = 0;
            if (t.aralikDk > 0 && t.aralikDk < 1) t.aralikDk = 1;
        }

        // Yalnız okuma: sorgu SELECT/WITH ile başlamalı, değiştiren komut içermemeli (kaynağa yazma bu sürümde yoktur).
        public static string SorguTemizle(string sorgu)
        {
            string s = (sorgu ?? "").Trim().TrimEnd(';').Trim();
            if (s.Length == 0) throw new ArgumentException("Sorgu (SELECT) boş olamaz.");
            string u = s.ToUpperInvariant();
            if (!(u.StartsWith("SELECT") || u.StartsWith("WITH"))) throw new ArgumentException("Sorgu SELECT ile başlamalı.");
            if (s.Contains(";")) throw new ArgumentException("Tek bir SELECT sorgusu yazın (noktalı virgül ile birden çok komut olmaz).");
            if (Regex.IsMatch(u, @"\b(INSERT|UPDATE|DELETE|DROP|ALTER|CREATE|TRUNCATE|MERGE|EXEC|EXECUTE|GRANT|REVOKE)\b"))
                throw new ArgumentException("Sorgu yalnız okuma yapabilir (INSERT/UPDATE/DELETE vb. kullanılamaz).");
            return s;
        }

        public static DbConnection Baglan(string tur, string baglanti)
        {
            if (tur == "oracle") return new Oracle.ManagedDataAccess.Client.OracleConnection(baglanti);
            return new System.Data.SqlClient.SqlConnection(baglanti);
        }

        // ------------------------------------------------------------ deneme

        public static object Dene(string tur, string baglanti, string sorgu)
        {
            using (DbConnection c = Baglan(tur, baglanti))
            {
                c.Open();
                string surum = c.ServerVersion;
                if (string.IsNullOrWhiteSpace(sorgu)) return new { tamam = true, surum, sutunlar = new string[0], satirlar = new object[0] };
                sorgu = SorguTemizle(sorgu);
                using (DbCommand cmd = c.CreateCommand())
                {
                    cmd.CommandText = sorgu; cmd.CommandTimeout = 60;
                    using (DbDataReader rd = cmd.ExecuteReader())
                    {
                        List<string> sut = new List<string>();
                        for (int i = 0; i < rd.FieldCount; i++) sut.Add(rd.GetName(i));
                        List<List<string>> sat = new List<List<string>>();
                        while (sat.Count < 5 && rd.Read())
                        {
                            List<string> r = new List<string>();
                            for (int i = 0; i < rd.FieldCount; i++) r.Add(Metne(rd.GetValue(i)));
                            sat.Add(r);
                        }
                        try { cmd.Cancel(); } catch { }
                        return new { tamam = true, surum, sutunlar = sut, satirlar = sat };
                    }
                }
            }
        }

        // ------------------------------------------------------------ çekme

        private static string Metne(object v)
        {
            if (v == null || v is DBNull) return "";
            if (v is DateTime) return ((DateTime)v).ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
            if (v is bool) return (bool)v ? "1" : "0";
            if (v is byte[]) return Convert.ToBase64String((byte[])v);
            if (v is IFormattable) return ((IFormattable)v).ToString(null, CultureInfo.InvariantCulture);
            return Convert.ToString(v, CultureInfo.InvariantCulture);
        }

        private static object Deger(object v)
        {
            if (v == null || v is DBNull) return DBNull.Value;
            if (v is DateTime) return ((DateTime)v).ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
            if (v is decimal) return Convert.ToDouble((decimal)v);
            if (v is bool) return (bool)v ? 1 : 0;
            if (v is byte[]) return Convert.ToBase64String((byte[])v);
            if (v is Guid) return v.ToString();
            if (v is TimeSpan) return v.ToString();
            return v;
        }

        private static string Hash(string s)
        {
            using (SHA1 h = SHA1.Create()) return BitConverter.ToString(h.ComputeHash(Encoding.UTF8.GetBytes(s))).Replace("-", "");
        }

        private static string Kimlik(string ad)
        {
            string t = Regex.Replace(ad ?? "", "[^A-Za-z0-9_]", "_");
            if (t.Length == 0 || !char.IsLetter(t[0])) t = "C_" + t;
            return t;
        }

        public static string TabloAdi(VkTanim t) { return "IFS_" + t.hedef; }

        /// <summary>Kaynağı yoklar; yeni ve değişen satırları yerel tabloya yazar. Aynı anda tek çekme çalışır.</summary>
        public static VkSonuc Cek(VkTanim t, string yerelBaglanti = null)
        {
            if (string.IsNullOrWhiteSpace(t.sorgu)) return new VkSonuc();
            lock (CalismaKilidi)
            {
                Stopwatch sw = Stopwatch.StartNew();
                VkSonuc s = new VkSonuc();
                string tablo = TabloAdi(t);
                string[] anahtarKol = t.anahtar.Split(',').Select(x => x.Trim()).Where(x => x.Length > 0).ToArray();
                using (DbConnection kaynak = Baglan(t.tur, t.baglanti))
                using (SQLiteConnection yerel = yerelBaglanti == null ? Db.Ac() : new SQLiteConnection(yerelBaglanti).OpenAndReturn())
                {
                    kaynak.Open();
                    using (DbCommand cmd = kaynak.CreateCommand())
                    {
                        cmd.CommandText = SorguTemizle(t.sorgu); cmd.CommandTimeout = 600;
                        using (DbDataReader rd = cmd.ExecuteReader())
                        {
                            int n = rd.FieldCount;
                            string[] adlar = new string[n]; string[] tipler = new string[n];
                            HashSet<string> kullanilan = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "_anahtar", "_hash", "_guncelleme", "_silindi" };
                            for (int i = 0; i < n; i++)
                            {
                                string a = Kimlik(rd.GetName(i));
                                while (!kullanilan.Add(a)) a += "_";
                                adlar[i] = a;
                                Type ft = rd.GetFieldType(i);
                                tipler[i] = (ft == typeof(int) || ft == typeof(long) || ft == typeof(short) || ft == typeof(byte) || ft == typeof(bool)) ? "INTEGER"
                                          : (ft == typeof(double) || ft == typeof(float) || ft == typeof(decimal)) ? "REAL" : "TEXT";
                            }
                            int[] anIdx = anahtarKol.Select(k =>
                            {
                                int ix = -1;
                                for (int i = 0; i < n; i++) if (string.Equals(rd.GetName(i), k, StringComparison.OrdinalIgnoreCase) || string.Equals(adlar[i], Kimlik(k), StringComparison.OrdinalIgnoreCase)) { ix = i; break; }
                                if (ix < 0) throw new InvalidOperationException("Anahtar sütun sorgu sonucunda yok: " + k);
                                return ix;
                            }).ToArray();

                            TabloHazirla(yerel, tablo, adlar, tipler);
                            Dictionary<string, string> mevcut = new Dictionary<string, string>();
                            Dictionary<string, long> mevcutSilindi = new Dictionary<string, long>();
                            using (SQLiteCommand c = new SQLiteCommand("SELECT _anahtar, _hash, _silindi FROM \"" + tablo + "\"", yerel))
                            using (SQLiteDataReader r = c.ExecuteReader())
                                while (r.Read()) { mevcut[r.GetString(0)] = r.IsDBNull(1) ? "" : r.GetString(1); mevcutSilindi[r.GetString(0)] = r.GetInt64(2); }

                            string kolListe = string.Join(", ", adlar.Select(a => "\"" + a + "\""));
                            string ins = "INSERT INTO \"" + tablo + "\" (_anahtar, _hash, _guncelleme, _silindi, " + kolListe + ") VALUES (@k, @h, @g, 0, " + string.Join(", ", adlar.Select((a, i) => "@v" + i)) + ")";
                            string upd = "UPDATE \"" + tablo + "\" SET _hash=@h, _guncelleme=@g, _silindi=0, " + string.Join(", ", adlar.Select((a, i) => "\"" + a + "\"=@v" + i)) + " WHERE _anahtar=@k";
                            HashSet<string> gorulen = new HashSet<string>();
                            SQLiteTransaction tx = yerel.BeginTransaction();
                            int bekleyen = 0;
                            string simdi = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
                            try
                            {
                                while (rd.Read())
                                {
                                    object[] v = new object[n];
                                    rd.GetValues(v);
                                    string anahtar = string.Join("|", anIdx.Select(i => Metne(v[i])));
                                    if (anahtar.Replace("|", "").Length == 0) continue;
                                    if (!gorulen.Add(anahtar)) { s.Kopya++; continue; }
                                    StringBuilder sb = new StringBuilder();
                                    for (int i = 0; i < n; i++) sb.Append(v[i] == null || v[i] is DBNull ? "\u0000" : Metne(v[i])).Append('\u001f');
                                    string h = Hash(sb.ToString());
                                    string eskiHash;
                                    bool var = mevcut.TryGetValue(anahtar, out eskiHash);
                                    if (var && eskiHash == h && mevcutSilindi[anahtar] == 0) { s.Ayni++; continue; }
                                    using (SQLiteCommand w = new SQLiteCommand(var ? upd : ins, yerel, tx))
                                    {
                                        w.Parameters.AddWithValue("@k", anahtar);
                                        w.Parameters.AddWithValue("@h", h);
                                        w.Parameters.AddWithValue("@g", simdi);
                                        for (int i = 0; i < n; i++) w.Parameters.AddWithValue("@v" + i, Deger(v[i]));
                                        w.ExecuteNonQuery();
                                    }
                                    if (var) s.Degisen++; else s.Yeni++;
                                    if (++bekleyen >= 2000) { tx.Commit(); tx.Dispose(); tx = yerel.BeginTransaction(); bekleyen = 0; }
                                }
                                if (t.silinenleriIsaretle && gorulen.Count > 0)
                                {
                                    foreach (KeyValuePair<string, string> m in mevcut)
                                    {
                                        if (gorulen.Contains(m.Key) || mevcutSilindi[m.Key] == 1) continue;
                                        using (SQLiteCommand w = new SQLiteCommand("UPDATE \"" + tablo + "\" SET _silindi=1, _guncelleme=@g WHERE _anahtar=@k", yerel, tx))
                                        {
                                            w.Parameters.AddWithValue("@g", simdi); w.Parameters.AddWithValue("@k", m.Key);
                                            w.ExecuteNonQuery();
                                        }
                                        s.Silinen++;
                                    }
                                }
                                tx.Commit();
                            }
                            catch { try { tx.Rollback(); } catch { } throw; }
                            finally { tx.Dispose(); }
                        }
                    }
                    GunlukYaz(yerel, t, s, null);
                }
                s.Ms = sw.ElapsedMilliseconds;
                return s;
            }
        }

        private static void TabloHazirla(SQLiteConnection yerel, string tablo, string[] adlar, string[] tipler)
        {
            using (SQLiteCommand c = new SQLiteCommand("CREATE TABLE IF NOT EXISTS \"" + tablo + "\" (_anahtar TEXT PRIMARY KEY NOT NULL, _hash TEXT, _guncelleme TEXT, _silindi INTEGER NOT NULL DEFAULT 0, " +
                string.Join(", ", adlar.Select((a, i) => "\"" + a + "\" " + tipler[i])) + ")", yerel)) c.ExecuteNonQuery();
            HashSet<string> var = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            using (SQLiteCommand c = new SQLiteCommand("PRAGMA table_info(\"" + tablo + "\")", yerel))
            using (SQLiteDataReader r = c.ExecuteReader()) while (r.Read()) var.Add(r.GetString(1));
            for (int i = 0; i < adlar.Length; i++)
                if (!var.Contains(adlar[i]))
                    using (SQLiteCommand c = new SQLiteCommand("ALTER TABLE \"" + tablo + "\" ADD COLUMN \"" + adlar[i] + "\" " + tipler[i], yerel)) c.ExecuteNonQuery();
        }

        private static void GunlukTablosu(SQLiteConnection y)
        {
            using (SQLiteCommand c = new SQLiteCommand("CREATE TABLE IF NOT EXISTS IfsGunluk (Id INTEGER PRIMARY KEY AUTOINCREMENT, Tarih TEXT, KaynakId TEXT, Ad TEXT, Yeni INTEGER, Degisen INTEGER, Ayni INTEGER, Silinen INTEGER, Sure INTEGER, Hata TEXT)", y)) c.ExecuteNonQuery();
        }

        public static void GunlukYaz(SQLiteConnection y, VkTanim t, VkSonuc s, string hata)
        {
            GunlukTablosu(y);
            using (SQLiteCommand c = new SQLiteCommand("INSERT INTO IfsGunluk (Tarih, KaynakId, Ad, Yeni, Degisen, Ayni, Silinen, Sure, Hata) VALUES (@t,@i,@a,@y,@d,@n,@s,@m,@h)", y))
            {
                c.Parameters.AddWithValue("@t", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
                c.Parameters.AddWithValue("@i", t.id ?? ""); c.Parameters.AddWithValue("@a", t.ad ?? "");
                c.Parameters.AddWithValue("@y", s == null ? 0 : s.Yeni); c.Parameters.AddWithValue("@d", s == null ? 0 : s.Degisen);
                c.Parameters.AddWithValue("@n", s == null ? 0 : s.Ayni); c.Parameters.AddWithValue("@s", s == null ? 0 : s.Silinen);
                c.Parameters.AddWithValue("@m", s == null ? 0 : s.Ms);
                c.Parameters.AddWithValue("@h", (object)hata ?? DBNull.Value);
                c.ExecuteNonQuery();
            }
            // günlük şişmesin: son 2000 kayıt
            using (SQLiteCommand c = new SQLiteCommand("DELETE FROM IfsGunluk WHERE Id <= (SELECT MAX(Id) FROM IfsGunluk) - 2000", y)) c.ExecuteNonQuery();
        }

        public static DataTable Gunluk(int adet)
        {
            using (SQLiteConnection y = Db.Ac())
            {
                GunlukTablosu(y);
                return Db.Tablo("SELECT Id, Tarih, Ad, Yeni, Degisen, Ayni, Silinen, Sure, Hata FROM IfsGunluk ORDER BY Id DESC LIMIT " + adet.ToString(CultureInfo.InvariantCulture));
            }
        }

        /// <summary>Tanımı çalıştırır, sonucu tanıma ve günlüğe yazar; hata olursa tanıma hata yazılır ve fırlatılmaz.</summary>
        public static string CekVeKaydet(VkTanim t)
        {
            string ozet;
            try { ozet = Cek(t).Ozet(); }
            catch (Exception ex)
            {
                ozet = "HATA: " + ex.Message;
                try { using (SQLiteConnection y = Db.Ac()) GunlukYaz(y, t, null, ex.Message); } catch { }
            }
            lock (Kilit)
            {
                List<VkTanim> l = Liste();
                VkTanim g = l.FirstOrDefault(x => x.id == t.id);
                if (g != null) { g.sonCalisma = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture); g.sonSonuc = ozet; Yaz(l); }
            }
            return ozet;
        }

        // ------------------------------------------------------------ otomatik yoklama

        public static void Baslat()
        {
            _zamanlayici = new Timer(_ => Yokla(), null, TimeSpan.FromSeconds(45), TimeSpan.FromSeconds(30));
        }

        private static int _yoklaniyor;

        private static void Yokla()
        {
            if (Interlocked.Exchange(ref _yoklaniyor, 1) == 1) return;
            try
            {
                foreach (VkTanim t in Liste().Where(x => x.aktif && x.aralikDk > 0 && !string.IsNullOrWhiteSpace(x.sorgu)))
                {
                    DateTime son;
                    if (DateTime.TryParse(t.sonCalisma, CultureInfo.InvariantCulture, DateTimeStyles.None, out son) && (DateTime.Now - son).TotalMinutes < t.aralikDk) continue;
                    CekVeKaydet(t);
                }
            }
            catch { }
            try { YazYokla(); } catch { }
            finally { Interlocked.Exchange(ref _yoklaniyor, 0); }
        }
    }
}
