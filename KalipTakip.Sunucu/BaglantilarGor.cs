using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Data.SQLite;
using System.Globalization;
using System.Linq;

namespace KalipTakip.Sunucu
{
    /// <summary>IFS tablolarını izleme ekranının sunucu tarafı: çekilmiş yerel kopya (arama + sayfalama) ve IFS'teki canlı yazma tablosu.</summary>
    internal static partial class Vk
    {
        public sealed class VeriSayfa
        {
            public List<string> Sutunlar = new List<string>();
            public List<List<string>> Satirlar = new List<List<string>>();
            public long Toplam;
            public string Not;
        }

        private static string Gor(object v)
        {
            if (v == null || v is DBNull) return null;
            return Metne(v);
        }

        /// <summary>Çekilmiş yerel kopya: IFS_&lt;ad&gt;. q: tüm sütunlarda arama; silinenler: kaynakta kalmayanlar da gelsin.</summary>
        public static VeriSayfa YerelVeri(VkTanim t, string q, bool silinenler, int sayfa, int adet)
        {
            string tablo = TabloAdi(t);
            VeriSayfa o = new VeriSayfa();
            using (SQLiteConnection y = Db.Ac())
            {
                long var;
                using (SQLiteCommand c = new SQLiteCommand("SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name=@n", y)) { c.Parameters.AddWithValue("@n", tablo); var = Convert.ToInt64(c.ExecuteScalar()); }
                if (var == 0) { o.Not = "Bu kaynaktan henüz veri çekilmedi. Bağlantılar sayfasında \"Çek\" düğmesine basın."; return o; }
                List<string> kol = new List<string>();
                using (SQLiteCommand c = new SQLiteCommand("PRAGMA table_info(\"" + tablo + "\")", y))
                using (SQLiteDataReader r = c.ExecuteReader()) while (r.Read()) kol.Add(r.GetString(1));
                List<string> veriKol = kol.Where(k => !k.StartsWith("_")).ToList();
                List<string> secKol = new List<string>(veriKol); secKol.Add("_guncelleme"); secKol.Add("_silindi");
                o.Sutunlar = new List<string>(veriKol); o.Sutunlar.Add("Son güncelleme"); o.Sutunlar.Add("Durum");
                string where = silinenler ? "1=1" : "_silindi = 0";
                List<string> prm = new List<string>();
                if (!string.IsNullOrWhiteSpace(q))
                {
                    foreach (string w in q.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).Take(5))
                    {
                        where += " AND (" + string.Join(" OR ", veriKol.Select(k => "CAST(\"" + k + "\" AS TEXT) LIKE @q" + prm.Count)) + ")";
                        prm.Add("%" + w.Replace("%", "") + "%");
                    }
                }
                using (SQLiteCommand c = new SQLiteCommand("SELECT COUNT(*) FROM \"" + tablo + "\" WHERE " + where, y))
                { for (int i = 0; i < prm.Count; i++) c.Parameters.AddWithValue("@q" + i, prm[i]); o.Toplam = Convert.ToInt64(c.ExecuteScalar()); }
                if (adet < 1) adet = 100; if (adet > 500) adet = 500; if (sayfa < 1) sayfa = 1;
                using (SQLiteCommand c = new SQLiteCommand("SELECT " + string.Join(", ", secKol.Select(k => "\"" + k + "\"")) + " FROM \"" + tablo + "\" WHERE " + where + " ORDER BY rowid LIMIT " + adet + " OFFSET " + ((sayfa - 1) * adet), y))
                {
                    for (int i = 0; i < prm.Count; i++) c.Parameters.AddWithValue("@q" + i, prm[i]);
                    using (SQLiteDataReader r = c.ExecuteReader())
                        while (r.Read())
                        {
                            List<string> s = new List<string>();
                            for (int i = 0; i < veriKol.Count + 1; i++) s.Add(Gor(r.GetValue(i)));
                            s.Add(r.GetInt64(veriKol.Count + 1) == 1 ? "Kaynakta yok" : "");
                            o.Satirlar.Add(s);
                        }
                }
            }
            return o;
        }

        private static string YazBag(VkTanim t) { return string.IsNullOrWhiteSpace(t.yazBaglanti) ? t.baglanti : t.yazBaglanti; }

        /// <summary>IFS'teki yazma tablosunun canlı hâli (ilk N satır). Yalnız SELECT çalıştırılır.</summary>
        public static VeriSayfa CanliVeri(VkTanim t, int adet)
        {
            if (string.IsNullOrWhiteSpace(t.yazTablo)) throw new InvalidOperationException("Bu kaynakta yazma tablosu tanımlı değil.");
            if (adet < 1 || adet > 1000) adet = 300;
            VeriSayfa o = new VeriSayfa();
            using (DbConnection c = Baglan(t.tur, YazBag(t)))
            {
                c.Open();
                using (DbCommand k = c.CreateCommand())
                {
                    k.CommandText = "SELECT * FROM " + t.yazTablo; k.CommandTimeout = 60;
                    using (DbDataReader r = k.ExecuteReader())
                    {
                        for (int i = 0; i < r.FieldCount; i++) o.Sutunlar.Add(r.GetName(i));
                        while (o.Satirlar.Count < adet && r.Read())
                        {
                            List<string> s = new List<string>();
                            for (int i = 0; i < r.FieldCount; i++) s.Add(Gor(r.GetValue(i)));
                            o.Satirlar.Add(s);
                        }
                        o.Toplam = o.Satirlar.Count;
                        if (o.Satirlar.Count >= adet) o.Not = "İlk " + adet + " satır gösteriliyor.";
                        try { k.Cancel(); } catch { }
                    }
                }
            }
            return o;
        }

        /// <summary>Bizdeki kalıp kodlarından IFS yazma tablosunun anahtar sütununda bulunmayanlar.</summary>
        public static object Eksikler(VkTanim t)
        {
            if (string.IsNullOrWhiteSpace(t.yazTablo) || string.IsNullOrWhiteSpace(t.yazAnahtar)) throw new InvalidOperationException("Bu kaynakta yazma tablosu / anahtar sütun tanımlı değil.");
            HashSet<string> ifs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            using (DbConnection c = Baglan(t.tur, YazBag(t)))
            {
                c.Open();
                using (DbCommand k = c.CreateCommand())
                {
                    k.CommandText = "SELECT " + t.yazAnahtar + " FROM " + t.yazTablo; k.CommandTimeout = 120;
                    using (DbDataReader r = k.ExecuteReader()) while (r.Read()) { string v = Gor(r.GetValue(0)); if (v != null) ifs.Add(v.Trim()); }
                }
            }
            List<string> eksik = new List<string>(); int bizde = 0;
            using (SQLiteConnection y = Db.Ac())
            using (SQLiteCommand c = new SQLiteCommand("SELECT KalipKodu FROM KalipTanim ORDER BY KalipKodu", y))
            using (SQLiteDataReader r = c.ExecuteReader())
                while (r.Read()) { bizde++; string kod = Convert.ToString(r.GetValue(0)).Trim(); if (!ifs.Contains(kod) && eksik.Count < 2000) eksik.Add(kod); }
            return new { bizde, ifste = ifs.Count, eksikSayi = eksik.Count, eksik };
        }
    }
}
