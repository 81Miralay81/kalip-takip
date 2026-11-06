using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Data.SQLite;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KalipTakip.Sunucu
{
    /// <summary>
    /// SQLite veritabanındaki tabloları (şema + veri) SQL Server'a kopyalar. SQLite dosyasına dokunmaz (yalnız okur).
    /// Bağlantı cümlesi şifreli depoda "SqlServerBaglanti" anahtarında saklanır.
    /// </summary>
    internal static class Aktarim
    {
        public const string Anahtar = "SqlServerBaglanti";
        private static readonly object Kilit = new object();
        public static volatile string Durum = "";
        public static volatile bool Calisiyor;
        public static volatile bool Hata;
        public static volatile string Rapor = "";

        public static string Baglanti() { return Gizli.Al(Anahtar); }

        public static object Dene(string baglanti)
        {
            SqlConnectionStringBuilder b = new SqlConnectionStringBuilder(baglanti);
            string dbAd = b.InitialCatalog;
            b.ConnectTimeout = Math.Min(b.ConnectTimeout, 15);
            using (SqlConnection c = new SqlConnection(b.ConnectionString))
            {
                c.Open();
                string surum = c.ServerVersion;
                bool varMi = true;
                if (!string.IsNullOrEmpty(dbAd)) { }
                return new { tamam = true, surum, veritabani = dbAd, var = varMi };
            }
        }

        // Veritabanı yoksa oluşturur (master'a bağlanıp). Yetki yoksa anlaşılır hata verir.
        private static void VeritabaniHazirla(string baglanti)
        {
            SqlConnectionStringBuilder b = new SqlConnectionStringBuilder(baglanti);
            string ad = b.InitialCatalog;
            if (string.IsNullOrEmpty(ad)) throw new InvalidOperationException("Bağlantı cümlesinde Initial Catalog (veritabanı adı) olmalı.");
            b.InitialCatalog = "master";
            using (SqlConnection c = new SqlConnection(b.ConnectionString))
            {
                c.Open();
                using (SqlCommand k = new SqlCommand("IF DB_ID(@d) IS NULL EXEC('CREATE DATABASE ' + @q)", c))
                {
                    k.Parameters.AddWithValue("@d", ad);
                    k.Parameters.AddWithValue("@q", "[" + ad.Replace("]", "]]") + "]");
                    k.ExecuteNonQuery();
                }
            }
        }

        public static void Baslat(string baglanti, string mod, bool uzerineYaz, string yoneticiAd, string yoneticiParola)
        {
            lock (Kilit)
            {
                if (Calisiyor) throw new InvalidOperationException("Bir aktarım zaten çalışıyor.");
                Calisiyor = true; Hata = false; Durum = "Başlıyor..."; Rapor = "";
            }
            Task.Run(() =>
            {
                try { Rapor = Calistir(baglanti, mod, uzerineYaz, yoneticiAd, yoneticiParola, null); Durum = "Bitti."; }
                catch (Exception ex) { Hata = true; Durum = "HATA: " + ex.Message; HataGunlugu.Yaz(ex); }
                finally { Calisiyor = false; }
            });
        }

        private sealed class Kol { public string Ad, Tip; public bool NotNull, Pk, Otomatik; public string Varsayilan; public int PkSira; }

        private static string Q(string ad) { return "[" + ad.Replace("]", "]]") + "]"; }

        /// <summary>mod: "tam" = şema + veri, "bos" = yalnız şema (+ yönetici). yerelBaglanti: sınama için kaynak SQLite.</summary>
        public static string Calistir(string baglanti, string mod, bool uzerineYaz, string yoneticiAd, string yoneticiParola, string yerelBaglanti)
        {
            VeritabaniHazirla(baglanti);
            StringBuilder rapor = new StringBuilder();
            using (SQLiteConnection src = yerelBaglanti == null ? Db.Ac() : new SQLiteConnection(yerelBaglanti).OpenAndReturn())
            using (SqlConnection hdf = new SqlConnection(baglanti))
            {
                hdf.Open();
                List<string> tablolar = new List<string>();
                using (SQLiteCommand c = new SQLiteCommand("SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%' ORDER BY name", src))
                using (SQLiteDataReader r = c.ExecuteReader()) while (r.Read()) tablolar.Add(r.GetString(0));

                foreach (string t in tablolar)
                {
                    Durum = "Tablo hazırlanıyor: " + t;
                    bool var = Skaler(hdf, "SELECT COUNT(*) FROM sys.tables WHERE name=@n", t) != 0;
                    if (var)
                    {
                        if (!uzerineYaz) throw new InvalidOperationException("Hedefte '" + t + "' tablosu zaten var. 'Var olanların üzerine yaz' seçeneğini işaretleyin ya da boş bir veritabanı kullanın.");
                        Calis(hdf, "DROP TABLE " + Q(t));
                    }
                }
                // üzerine yazarken bağımlılık sorunu olmasın diye önce hepsi silindi; şimdi şemayı kur
                foreach (string t in tablolar)
                {
                    List<Kol> kolonlar = Kolonlar(src, t);
                    Calis(hdf, TabloSql(t, kolonlar));
                    foreach (string ix in IndeksSql(src, t, kolonlar)) { try { Calis(hdf, ix); } catch (Exception ex) { rapor.AppendLine("Dizin atlandı (" + t + "): " + ex.Message); } }
                }
                if (mod == "tam")
                {
                    foreach (string t in tablolar)
                    {
                        long n = Veri(src, hdf, t, Kolonlar(src, t));
                        long kaynakN = Say(src, t);
                        rapor.AppendLine(t + ": " + n + " satır" + (n == kaynakN ? " ✓" : " (KAYNAK " + kaynakN + " – UYUŞMAZLIK!)"));
                        if (n != kaynakN) throw new InvalidOperationException(t + " tablosunda satır sayısı uyuşmadı (kaynak " + kaynakN + ", hedef " + n + ").");
                    }
                }
                else
                {
                    foreach (string t in tablolar) rapor.AppendLine(t + ": şema kuruldu (boş)");
                    if (!string.IsNullOrEmpty(yoneticiAd) && tablolar.Contains("Kullanici"))
                    {
                        using (SqlCommand k = new SqlCommand("INSERT INTO Kullanici (KullaniciAdi, ParolaHash, IsAdmin, Aktif) VALUES (@a,@p,1,1)", hdf))
                        {
                            k.Parameters.AddWithValue("@a", yoneticiAd);
                            k.Parameters.AddWithValue("@p", Guvenlik.Hashle(yoneticiParola));
                            k.ExecuteNonQuery();
                        }
                        rapor.AppendLine("Yönetici oluşturuldu: " + yoneticiAd);
                    }
                }
            }
            return rapor.ToString();
        }

        private static int Skaler(SqlConnection c, string sql, string p)
        {
            using (SqlCommand k = new SqlCommand(sql, c)) { k.Parameters.AddWithValue("@n", p); return Convert.ToInt32(k.ExecuteScalar()); }
        }
        private static void Calis(SqlConnection c, string sql) { using (SqlCommand k = new SqlCommand(sql, c)) { k.CommandTimeout = 300; k.ExecuteNonQuery(); } }
        private static long Say(SQLiteConnection c, string t) { using (SQLiteCommand k = new SQLiteCommand("SELECT COUNT(*) FROM \"" + t + "\"", c)) return Convert.ToInt64(k.ExecuteScalar()); }

        private static List<Kol> Kolonlar(SQLiteConnection src, string t)
        {
            List<Kol> l = new List<Kol>();
            using (SQLiteCommand c = new SQLiteCommand("PRAGMA table_info(\"" + t + "\")", src))
            using (SQLiteDataReader r = c.ExecuteReader())
                while (r.Read())
                    l.Add(new Kol
                    {
                        Ad = r.GetString(1), Tip = (r.IsDBNull(2) ? "" : r.GetString(2)).ToUpperInvariant(),
                        NotNull = r.GetInt32(3) == 1, Varsayilan = r.IsDBNull(4) ? null : r.GetValue(4).ToString(), PkSira = r.GetInt32(5), Pk = r.GetInt32(5) > 0
                    });
            // AUTOINCREMENT: tek INTEGER PK
            string sql = null;
            using (SQLiteCommand c = new SQLiteCommand("SELECT sql FROM sqlite_master WHERE type='table' AND name=@n", src))
            { c.Parameters.AddWithValue("@n", t); sql = Convert.ToString(c.ExecuteScalar()) ?? ""; }
            Kol[] pk = l.Where(x => x.Pk).ToArray();
            if (pk.Length == 1 && pk[0].Tip.StartsWith("INT") && sql.ToUpperInvariant().Contains("AUTOINCREMENT")) pk[0].Otomatik = true;
            return l;
        }

        private static HashSet<string> IndeksliKolonlar(SQLiteConnection src, string t)
        {
            HashSet<string> h = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            List<string> ixler = new List<string>();
            using (SQLiteCommand c = new SQLiteCommand("PRAGMA index_list(\"" + t + "\")", src))
            using (SQLiteDataReader r = c.ExecuteReader()) while (r.Read()) ixler.Add(r.GetString(1));
            foreach (string ix in ixler)
                using (SQLiteCommand c = new SQLiteCommand("PRAGMA index_info(\"" + ix + "\")", src))
                using (SQLiteDataReader r = c.ExecuteReader()) while (r.Read()) if (!r.IsDBNull(2)) h.Add(r.GetString(2));
            return h;
        }

        private static string SqlTipi(Kol k, bool dizinli)
        {
            string t = k.Tip;
            if (t.Contains("INT")) return "BIGINT";
            if (t.Contains("REAL") || t.Contains("FLOA") || t.Contains("DOUB")) return "FLOAT";
            if (t.Contains("BOOL") || t == "BIT") return "BIT";
            if (t.Contains("DATE") || t.Contains("TIME")) return "DATETIME2";
            if (t.Contains("BLOB")) return "VARBINARY(MAX)";
            if (t.Contains("DEC") || t.Contains("NUM")) return "FLOAT";
            return dizinli || k.Pk ? "NVARCHAR(450)" : "NVARCHAR(MAX)";
        }

        private static string Varsayilan(Kol k, string sqlTip)
        {
            if (k.Varsayilan == null) return "";
            string v = k.Varsayilan.Trim();
            if (v.Equals("CURRENT_TIMESTAMP", StringComparison.OrdinalIgnoreCase)) return " DEFAULT SYSDATETIME()";
            if (sqlTip == "BIT") return " DEFAULT " + (v.Trim('\'') == "1" || v.Equals("true", StringComparison.OrdinalIgnoreCase) ? "1" : "0");
            double d;
            if (sqlTip == "BIGINT" || sqlTip == "FLOAT") { return double.TryParse(v.Trim('\''), NumberStyles.Float, CultureInfo.InvariantCulture, out d) ? " DEFAULT " + v.Trim('\'') : ""; }
            if (sqlTip.StartsWith("NVARCHAR")) return v.StartsWith("'") ? " DEFAULT N" + v : "";
            return "";
        }

        private static string TabloSql(string t, List<Kol> kolonlar)
        {
            HashSet<string> dz = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            StringBuilder sb = new StringBuilder("CREATE TABLE " + Q(t) + " (");
            for (int i = 0; i < kolonlar.Count; i++)
            {
                Kol k = kolonlar[i];
                string tip = SqlTipi(k, false);
                sb.Append(i > 0 ? ", " : "").Append(Q(k.Ad)).Append(' ').Append(tip);
                if (k.Otomatik) sb.Append(" IDENTITY(1,1)");
                if (k.NotNull || k.Pk) sb.Append(" NOT NULL");
                sb.Append(Varsayilan(k, tip));
            }
            Kol[] pk = kolonlar.Where(x => x.Pk).OrderBy(x => x.PkSira).ToArray();
            if (pk.Length > 0) sb.Append(", CONSTRAINT ").Append(Q("PK_" + t)).Append(" PRIMARY KEY (").Append(string.Join(", ", pk.Select(x => Q(x.Ad)))).Append(')');
            sb.Append(')');
            return sb.ToString();
        }

        // Dizinli TEXT sütunları NVARCHAR(450) olmalı: tabloyu kurduktan sonra ALTER ile daraltılır (boş tabloda güvenli).
        private static IEnumerable<string> IndeksSql(SQLiteConnection src, string t, List<Kol> kolonlar)
        {
            HashSet<string> dz = IndeksliKolonlar(src, t);
            foreach (Kol k in kolonlar.Where(x => dz.Contains(x.Ad) && !x.Pk && SqlTipi(x, false) == "NVARCHAR(MAX)"))
                yield return "ALTER TABLE " + Q(t) + " ALTER COLUMN " + Q(k.Ad) + " NVARCHAR(450)" + (k.NotNull ? " NOT NULL" : " NULL");
            List<string[]> ixler = new List<string[]>();
            using (SQLiteCommand c = new SQLiteCommand("PRAGMA index_list(\"" + t + "\")", src))
            using (SQLiteDataReader r = c.ExecuteReader())
                while (r.Read())
                {
                    string origin = r.FieldCount > 3 && !r.IsDBNull(3) ? r.GetString(3) : "c";
                    if (origin == "pk") continue;
                    ixler.Add(new[] { r.GetString(1), r.GetInt32(2).ToString() });
                }
            foreach (string[] ix in ixler)
            {
                List<string> cols = new List<string>();
                using (SQLiteCommand c = new SQLiteCommand("PRAGMA index_info(\"" + ix[0] + "\")", src))
                using (SQLiteDataReader r = c.ExecuteReader()) while (r.Read()) if (!r.IsDBNull(2)) cols.Add(Q(r.GetString(2)));
                if (cols.Count == 0) continue;
                string ad = ix[0].StartsWith("sqlite_autoindex") ? "UX_" + t + "_" + string.Join("_", cols.Select(x => x.Trim('[', ']'))) : ix[0];
                yield return "CREATE " + (ix[1] == "1" ? "UNIQUE " : "") + "INDEX " + Q(ad) + " ON " + Q(t) + " (" + string.Join(", ", cols) + ")";
            }
        }

        private static long Veri(SQLiteConnection src, SqlConnection hdf, string t, List<Kol> kolonlar)
        {
            long toplam = 0;
            using (SQLiteCommand c = new SQLiteCommand("SELECT * FROM \"" + t + "\"", src))
            using (SQLiteDataReader r = c.ExecuteReader())
            using (SqlBulkCopy bc = new SqlBulkCopy(hdf, SqlBulkCopyOptions.KeepIdentity | SqlBulkCopyOptions.TableLock, null))
            {
                bc.DestinationTableName = Q(t); bc.BulkCopyTimeout = 0; bc.BatchSize = 5000;
                DataTable dt = new DataTable();
                string[] tipler = new string[kolonlar.Count];
                for (int i = 0; i < kolonlar.Count; i++)
                {
                    tipler[i] = SqlTipi(kolonlar[i], true);
                    Type ct = tipler[i] == "BIGINT" ? typeof(long) : tipler[i] == "FLOAT" ? typeof(double) : tipler[i] == "BIT" ? typeof(bool)
                        : tipler[i] == "DATETIME2" ? typeof(DateTime) : tipler[i] == "VARBINARY(MAX)" ? typeof(byte[]) : typeof(string);
                    dt.Columns.Add(kolonlar[i].Ad, ct);
                    bc.ColumnMappings.Add(kolonlar[i].Ad, kolonlar[i].Ad);
                }
                while (r.Read())
                {
                    object[] row = new object[kolonlar.Count];
                    for (int i = 0; i < row.Length; i++) row[i] = Cevir(r.GetValue(i), tipler[i]);
                    dt.Rows.Add(row);
                    toplam++;
                    if (dt.Rows.Count >= 5000) { bc.WriteToServer(dt); dt.Clear(); Durum = t + ": " + toplam + " satır aktarıldı..."; }
                }
                if (dt.Rows.Count > 0) bc.WriteToServer(dt);
            }
            return toplam;
        }

        private static object Cevir(object v, string tip)
        {
            if (v == null || v is DBNull) return DBNull.Value;
            switch (tip)
            {
                case "BIGINT": return Convert.ToInt64(v, CultureInfo.InvariantCulture);
                case "FLOAT": return Convert.ToDouble(v, CultureInfo.InvariantCulture);
                case "BIT": return Convert.ToInt64(v, CultureInfo.InvariantCulture) != 0;
                case "DATETIME2":
                    {
                        if (v is DateTime) return v;
                        DateTime d;
                        return DateTime.TryParse(Convert.ToString(v, CultureInfo.InvariantCulture), CultureInfo.InvariantCulture, DateTimeStyles.None, out d) ? (object)d : DBNull.Value;
                    }
                case "VARBINARY(MAX)": return v is byte[] ? v : Encoding.UTF8.GetBytes(Convert.ToString(v, CultureInfo.InvariantCulture));
                default: return Convert.ToString(v, CultureInfo.InvariantCulture);
            }
        }
    }
}
