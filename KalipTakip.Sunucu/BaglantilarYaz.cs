using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Data.SQLite;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace KalipTakip.Sunucu
{
    /// <summary>
    /// IFS'e yazma (gönderim): her kalıp için tek satır. Kalıp Listesi / Talep değişince, satırın özeti (hash) son gönderilenden farklıysa
    /// hedef tabloda anahtara göre UPDATE, satır yoksa INSERT yapılır. Özetler yerel IfsGiden tablosunda tutulur.
    /// Güvenlik: yalnızca yönetici tanımlar; "yazAktif" açık olmalı; otomatik gönderim ancak ilk temel (baseline) alındıktan sonra çalışır.
    /// </summary>
    internal static partial class Vk
    {
        // Kaynak alan adı → tür (metin | sayi | tarih)
        public static readonly Dictionary<string, string> YazAlanlari = new Dictionary<string, string>
        {
            { "KalipKodu", "metin" }, { "KalipAdi", "metin" }, { "KalipTuru", "metin" }, { "TepeKodu", "metin" }, { "Notlar", "metin" },
            { "Lokasyon", "metin" }, { "SahipBolum", "metin" }, { "Raf", "metin" },
            { "KalipStatu", "metin" }, { "TashihSayisi", "sayi" },
            { "SonTalepNo", "metin" }, { "SonTalepStatu", "metin" }, { "SonTalepBolum", "metin" }, { "SonTalepTarihi", "tarih" },
            { "SonDegisiklik", "tarih" }, { "Guncelleme", "tarih" }
        };

        private const string SnapshotSql =
            "SELECT kt.Anahtar AS Anahtar, kt.KalipKodu AS KalipKodu, kt.KalipAdi AS KalipAdi, kt.KalipTuru AS KalipTuru, kt.TepeKodu AS TepeKodu, kt.Notlar AS Notlar, " +
            "kl.Lokasyon AS Lokasyon, kl.SahipBolum AS SahipBolum, kl.Raf AS Raf, " +
            "(SELECT k.KalipStatu FROM TalepKalip k WHERE k.Anahtar = kt.Anahtar ORDER BY k.Id DESC LIMIT 1) AS KalipStatu, " +
            "(SELECT COALESCE(SUM(k.TashihSayisi),0) FROM TalepKalip k WHERE k.Anahtar = kt.Anahtar) AS TashihSayisi, " +
            "(SELECT t.TalepNo FROM TalepKalip k JOIN Talep t ON t.Id = k.TalepId WHERE k.Anahtar = kt.Anahtar ORDER BY k.Id DESC LIMIT 1) AS SonTalepNo, " +
            "(SELECT t.Statu FROM TalepKalip k JOIN Talep t ON t.Id = k.TalepId WHERE k.Anahtar = kt.Anahtar ORDER BY k.Id DESC LIMIT 1) AS SonTalepStatu, " +
            "(SELECT t.Bolum FROM TalepKalip k JOIN Talep t ON t.Id = k.TalepId WHERE k.Anahtar = kt.Anahtar ORDER BY k.Id DESC LIMIT 1) AS SonTalepBolum, " +
            "(SELECT t.TalepTarihi FROM TalepKalip k JOIN Talep t ON t.Id = k.TalepId WHERE k.Anahtar = kt.Anahtar ORDER BY k.Id DESC LIMIT 1) AS SonTalepTarihi, " +
            "MAX(COALESCE(kt.SonDegisiklik,''), COALESCE(kl.SonDegisiklik,''), COALESCE((SELECT MAX(k.SonDegisiklik) FROM TalepKalip k WHERE k.Anahtar = kt.Anahtar),'')) AS SonDegisiklik " +
            "FROM KalipTanim kt LEFT JOIN KalipLokasyon kl ON kl.Anahtar = kt.Anahtar";

        private static readonly Regex Kimlikler = new Regex("^[A-Za-z][A-Za-z0-9_]{0,60}$", RegexOptions.Compiled);
        private static readonly Regex TabloAd = new Regex("^[A-Za-z][A-Za-z0-9_]{0,60}(\\.[A-Za-z][A-Za-z0-9_]{0,60})?$", RegexOptions.Compiled);

        private static void DogrulaYaz(VkTanim t)
        {
            if (!t.yazAktif && string.IsNullOrWhiteSpace(t.yazTablo)) { t.yazEslesme = t.yazEslesme ?? new List<VkEsle>(); return; }
            t.yazTablo = (t.yazTablo ?? "").Trim(); t.yazAnahtar = (t.yazAnahtar ?? "").Trim();
            if (!TabloAd.IsMatch(t.yazTablo)) throw new ArgumentException("Yazılacak IFS tablosu adı geçersiz (örn. KALIP_ARA veya SEMA.KALIP_ARA).");
            if (!Kimlikler.IsMatch(t.yazAnahtar)) throw new ArgumentException("Hedef tablodaki anahtar sütun adı geçersiz (KalipKodu bu sütuna yazılır).");
            List<VkEsle> l = (t.yazEslesme ?? new List<VkEsle>()).Where(e => e != null && !string.IsNullOrWhiteSpace(e.hedef) && !string.IsNullOrWhiteSpace(e.kaynak)).ToList();
            foreach (VkEsle e in l)
            {
                e.hedef = e.hedef.Trim(); e.kaynak = e.kaynak.Trim();
                if (!Kimlikler.IsMatch(e.hedef)) throw new ArgumentException("Hedef sütun adı geçersiz: " + e.hedef);
                if (!YazAlanlari.ContainsKey(e.kaynak)) throw new ArgumentException("Bilinmeyen kaynak alan: " + e.kaynak);
            }
            if (l.Select(e => e.hedef.ToUpperInvariant()).Distinct().Count() != l.Count) throw new ArgumentException("Aynı hedef sütun iki kez eşleştirilmiş.");
            if (l.Any(e => string.Equals(e.hedef, t.yazAnahtar, StringComparison.OrdinalIgnoreCase))) throw new ArgumentException("Anahtar sütun eşleştirme listesinde ayrıca yer almamalı (KalipKodu otomatik yazılır).");
            if (t.yazAktif && l.Count == 0) throw new ArgumentException("Yazmayı açmak için en az bir sütun eşleştirin.");
            t.yazEslesme = l;
        }

        private static void IfsGidenTablosu(SQLiteConnection y)
        {
            using (SQLiteCommand c = new SQLiteCommand("CREATE TABLE IF NOT EXISTS IfsGiden (KaynakId TEXT NOT NULL, Anahtar TEXT NOT NULL, Hash TEXT, Tarih TEXT, PRIMARY KEY (KaynakId, Anahtar))", y)) c.ExecuteNonQuery();
        }

        public static int TemelSayisi(string id, string yerelBaglanti = null)
        {
            using (SQLiteConnection y = yerelBaglanti == null ? Db.Ac() : new SQLiteConnection(yerelBaglanti).OpenAndReturn())
            {
                IfsGidenTablosu(y);
                using (SQLiteCommand c = new SQLiteCommand("SELECT COUNT(*) FROM IfsGiden WHERE KaynakId=@i", y)) { c.Parameters.AddWithValue("@i", id); return Convert.ToInt32(c.ExecuteScalar()); }
            }
        }

        private static object YazDeger(object v, string tur)
        {
            if (v == null || v is DBNull) return DBNull.Value;
            string s = Convert.ToString(v, CultureInfo.InvariantCulture);
            if (tur == "sayi") { double d; return double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out d) ? (object)d : DBNull.Value; }
            if (tur == "tarih")
            {
                if (string.IsNullOrWhiteSpace(s)) return DBNull.Value;
                DateTime dt; return DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out dt) ? (object)dt : DBNull.Value;
            }
            return s;
        }

        private static string YazHash(List<VkEsle> esle, Dictionary<string, object> satir)
        {
            StringBuilder sb = new StringBuilder();
            foreach (VkEsle e in esle.OrderBy(x => x.hedef))
            {
                if (e.kaynak == "Guncelleme") continue;   // her seferinde değişir; hash'e girmez
                object v = satir[e.kaynak];
                sb.Append(e.kaynak).Append('=').Append(v == null || v is DBNull ? "\u0000" : Convert.ToString(v, CultureInfo.InvariantCulture)).Append('\u001f');
            }
            sb.Append("KalipKodu=").Append(Convert.ToString(satir["KalipKodu"], CultureInfo.InvariantCulture));
            using (SHA1 h = SHA1.Create()) return BitConverter.ToString(h.ComputeHash(Encoding.UTF8.GetBytes(sb.ToString()))).Replace("-", "");
        }

        private static DbCommand Komut(DbConnection c, string tur, string sql, List<KeyValuePair<string, object>> p)
        {
            DbCommand k = c.CreateCommand();
            string on = tur == "oracle" ? ":" : "@";
            k.CommandText = sql.Replace("@", on);
            if (k is Oracle.ManagedDataAccess.Client.OracleCommand) ((Oracle.ManagedDataAccess.Client.OracleCommand)k).BindByName = true;
            foreach (KeyValuePair<string, object> kv in p)
            {
                DbParameter dp = k.CreateParameter(); dp.ParameterName = kv.Key; dp.Value = kv.Value ?? DBNull.Value; k.Parameters.Add(dp);
            }
            k.CommandTimeout = 60;
            return k;
        }

        /// <summary>mod: "degisen" (otomatik; temel gerekli) | "tum" (hepsini gönder) | "temel" (göndermeden şimdiki durumu temel al).</summary>
        public static VkSonuc Gonder(VkTanim t, string mod, string yerelBaglanti = null)
        {
            if (!t.yazAktif && mod != "temel") throw new InvalidOperationException("Bu kaynakta IFS'e yazma kapalı.");
            lock (YazKilidi)
            {
                VkSonuc s = new VkSonuc();
                System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();
                List<VkEsle> esle = t.yazEslesme ?? new List<VkEsle>();
                using (SQLiteConnection y = yerelBaglanti == null ? Db.Ac() : new SQLiteConnection(yerelBaglanti).OpenAndReturn())
                {
                    IfsGidenTablosu(y);
                    Dictionary<string, string> eskiHash = new Dictionary<string, string>();
                    using (SQLiteCommand c = new SQLiteCommand("SELECT Anahtar, Hash FROM IfsGiden WHERE KaynakId=@i", y))
                    {
                        c.Parameters.AddWithValue("@i", t.id);
                        using (SQLiteDataReader r = c.ExecuteReader()) while (r.Read()) eskiHash[r.GetString(0)] = r.IsDBNull(1) ? "" : r.GetString(1);
                    }
                    if (mod == "degisen" && eskiHash.Count == 0) return s;   // temel alınmadan otomatik gönderim yapılmaz

                    List<Dictionary<string, object>> satirlar = new List<Dictionary<string, object>>();
                    using (SQLiteCommand c = new SQLiteCommand(SnapshotSql, y))
                    using (SQLiteDataReader r = c.ExecuteReader())
                        while (r.Read())
                        {
                            Dictionary<string, object> d = new Dictionary<string, object>();
                            for (int i = 0; i < r.FieldCount; i++) d[r.GetName(i)] = r.IsDBNull(i) ? null : r.GetValue(i);
                            satirlar.Add(d);
                        }

                    string simdi = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
                    DbConnection hedef = null;
                    SQLiteTransaction tx = y.BeginTransaction();
                    int ardisikHata = 0; string sonHata = null;
                    try
                    {
                        if (mod != "temel") { hedef = Baglan(t.tur, string.IsNullOrWhiteSpace(t.yazBaglanti) ? t.baglanti : t.yazBaglanti); hedef.Open(); }
                        foreach (Dictionary<string, object> sat in satirlar)
                        {
                            string anahtar = Convert.ToString(sat["Anahtar"], CultureInfo.InvariantCulture);
                            string kod = Convert.ToString(sat["KalipKodu"], CultureInfo.InvariantCulture);
                            if (string.IsNullOrWhiteSpace(anahtar) || string.IsNullOrWhiteSpace(kod)) continue;
                            string h = YazHash(esle, sat);
                            string eski;
                            bool var = eskiHash.TryGetValue(anahtar, out eski);
                            if (mod != "tum" && var && eski == h) { s.Ayni++; continue; }
                            if (mod != "temel")
                            {
                                try
                                {
                                    List<KeyValuePair<string, object>> p = new List<KeyValuePair<string, object>>();
                                    p.Add(new KeyValuePair<string, object>("k", kod));
                                    for (int i = 0; i < esle.Count; i++)
                                    {
                                        string tur = YazAlanlari[esle[i].kaynak];
                                        object v = esle[i].kaynak == "Guncelleme" ? (object)DateTime.Now : YazDeger(sat[esle[i].kaynak], tur);
                                        p.Add(new KeyValuePair<string, object>("v" + i, v));
                                    }
                                    string set = string.Join(", ", esle.Select((e, i) => e.hedef + " = @v" + i));
                                    int n;
                                    using (DbCommand u = Komut(hedef, t.tur, "UPDATE " + t.yazTablo + " SET " + set + " WHERE " + t.yazAnahtar + " = @k", p)) n = u.ExecuteNonQuery();
                                    if (n == 0)
                                    {
                                        string kolonlar = t.yazAnahtar + ", " + string.Join(", ", esle.Select(e => e.hedef));
                                        string degerler = "@k, " + string.Join(", ", esle.Select((e, i) => "@v" + i));
                                        using (DbCommand ins = Komut(hedef, t.tur, "INSERT INTO " + t.yazTablo + " (" + kolonlar + ") VALUES (" + degerler + ")", p)) ins.ExecuteNonQuery();
                                        s.Yeni++;
                                    }
                                    else s.Degisen++;
                                    ardisikHata = 0;
                                }
                                catch (Exception ex)
                                {
                                    s.Kopya++; sonHata = kod + ": " + ex.Message; ardisikHata++;
                                    if (ardisikHata >= 5) throw new InvalidOperationException("Üst üste 5 satır yazılamadı, durduruldu. Son hata: " + sonHata);
                                    continue;   // bu satırın özeti kaydedilmez; bir sonraki turda yeniden denenir
                                }
                            }
                            else s.Yeni++;
                            using (SQLiteCommand w = new SQLiteCommand("INSERT OR REPLACE INTO IfsGiden (KaynakId, Anahtar, Hash, Tarih) VALUES (@i,@a,@h,@t)", y, tx))
                            { w.Parameters.AddWithValue("@i", t.id); w.Parameters.AddWithValue("@a", anahtar); w.Parameters.AddWithValue("@h", h); w.Parameters.AddWithValue("@t", simdi); w.ExecuteNonQuery(); }
                        }
                        tx.Commit();
                    }
                    catch { try { tx.Commit(); } catch { } throw; }   // başarılı gönderilenlerin özeti korunsun
                    finally { tx.Dispose(); if (hedef != null) hedef.Dispose(); }
                    s.Ms = sw.ElapsedMilliseconds;
                    if (mod != "temel" && (s.Yeni + s.Degisen + s.Kopya > 0))
                    {
                        VkTanim kayit = new VkTanim { id = t.id, ad = t.ad + " (gönderim)" };
                        GunlukYaz(y, kayit, s, s.Kopya > 0 ? s.Kopya + " satır yazılamadı; " + sonHata : null);
                    }
                }
                return s;
            }
        }

        private static readonly object YazKilidi = new object();

        public static string YazOzet(VkSonuc s, string mod)
        {
            if (mod == "temel") return s.Yeni + " kalıbın şimdiki durumu temel alındı (IFS'e bir şey yazılmadı)";
            return s.Yeni + " eklendi, " + s.Degisen + " güncellendi, " + s.Ayni + " aynı" + (s.Kopya > 0 ? ", " + s.Kopya + " HATALI" : "") + " (" + (s.Ms / 1000.0).ToString("0.0", CultureInfo.InvariantCulture) + " sn)";
        }

        public static string GonderVeKaydet(VkTanim t, string mod)
        {
            string ozet;
            try { ozet = YazOzet(Gonder(t, mod), mod); }
            catch (Exception ex)
            {
                ozet = "HATA: " + ex.Message;
                try { using (SQLiteConnection y = Db.Ac()) GunlukYaz(y, new VkTanim { id = t.id, ad = t.ad + " (gönderim)" }, null, ex.Message); } catch { }
            }
            lock (Kilit)
            {
                List<VkTanim> l = Liste();
                VkTanim g = l.FirstOrDefault(x => x.id == t.id);
                if (g != null && (mod != "degisen" || ozet.StartsWith("HATA") || !ozet.StartsWith("0 eklendi, 0 güncellendi")))
                { g.sonYazma = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture); g.sonYazSonuc = ozet; Yaz(l); }
            }
            return ozet;
        }

        private static void YazYokla()
        {
            foreach (VkTanim t in Liste().Where(x => x.yazAktif)) GonderVeKaydet(t, "degisen");
        }
    }
}
