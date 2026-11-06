using System;
using System.Data.SqlClient;
using System.Data.SQLite;
using System.IO;

namespace KalipTakip.Sunucu
{
    /// <summary>CI kendi kendine sınama: KalipTakipSunucu.exe --sinama "&lt;SQL Server bağlantı cümlesi (master/LocalDB)&gt;"</summary>
    internal static class Sinama
    {
        private static void Esit(object beklenen, object gercek, string ad)
        {
            if (!Equals(beklenen, gercek)) throw new Exception("SINAMA HATASI: " + ad + " beklenen=" + beklenen + " gercek=" + gercek);
            Console.WriteLine("  tamam: " + ad + " = " + gercek);
        }
        private static void Sql(SqlConnection c, string s) { using (SqlCommand k = new SqlCommand(s, c)) k.ExecuteNonQuery(); }
        private static object Skaler(SqlConnection c, string s) { using (SqlCommand k = new SqlCommand(s, c)) return k.ExecuteScalar(); }

        public static int Calistir(string sunucuBag)
        {
            string tmp = Path.Combine(Path.GetTempPath(), "kt_sinama_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tmp);
            try
            {
                Console.WriteLine("1) Gizli depo");
                Gizli.DosyaAyarla(Path.Combine(tmp, "gizli.ayar"));
                Gizli.Yaz("GeminiAnahtar", "SUPER-GIZLI-DEGER-123");
                Esit("SUPER-GIZLI-DEGER-123", Gizli.Al("GeminiAnahtar"), "yaz/oku");
                Esit(false, File.ReadAllText(Gizli.Dosya).Contains("SUPER-GIZLI"), "dosyada düz metin yok");
                Gizli.KurtarmaParolasiBelirle("kurtarma-1234");
                Esit(true, Gizli.KurtarmaIleAc("kurtarma-1234"), "kurtarma parolası doğru");
                Esit(false, Gizli.KurtarmaIleAc("yanlis-parola"), "kurtarma parolası yanlış");

                Console.WriteLine("2) SQLite -> SQL Server aktarımı");
                string lite = "Data Source=" + Path.Combine(tmp, "t.db") + ";Version=3;";
                using (SQLiteConnection c = new SQLiteConnection(lite).OpenAndReturn())
                {
                    foreach (string s in new[] {
                        "CREATE TABLE Parca (Id INTEGER PRIMARY KEY AUTOINCREMENT NOT NULL, Kod TEXT NOT NULL, Ad TEXT, Miktar REAL, Tarih DATE, Aktif BOOLEAN NOT NULL DEFAULT 1)",
                        "CREATE INDEX ix_Parca_Kod ON Parca (Kod)",
                        "CREATE TABLE Kullanici (Id INTEGER PRIMARY KEY AUTOINCREMENT NOT NULL, KullaniciAdi TEXT NOT NULL, ParolaHash TEXT NOT NULL, IsAdmin BOOLEAN NOT NULL DEFAULT 0, Aktif BOOLEAN NOT NULL DEFAULT 1)" })
                        using (SQLiteCommand k = new SQLiteCommand(s, c)) k.ExecuteNonQuery();
                    using (SQLiteTransaction tx = c.BeginTransaction())
                    {
                        for (int i = 1; i <= 12000; i++)
                            using (SQLiteCommand k = new SQLiteCommand("INSERT INTO Parca (Kod, Ad, Miktar, Tarih) VALUES (@a,@b,@c,@d)", c, tx))
                            { k.Parameters.AddWithValue("@a", "K" + i); k.Parameters.AddWithValue("@b", "Parça çğışöü " + i); k.Parameters.AddWithValue("@c", i * 1.5); k.Parameters.AddWithValue("@d", "2026-01-15"); k.ExecuteNonQuery(); }
                        tx.Commit();
                    }
                }
                SqlConnectionStringBuilder b = new SqlConnectionStringBuilder(sunucuBag) { InitialCatalog = "KtSinama" };
                string hedefBag = b.ConnectionString;
                string rapor = Aktarim.Calistir(hedefBag, "tam", true, null, null, lite);
                Console.WriteLine(rapor);
                using (SqlConnection c = new SqlConnection(hedefBag))
                {
                    c.Open();
                    Esit(12000, Convert.ToInt32(Skaler(c, "SELECT COUNT(*) FROM Parca")), "aktarılan satır");
                    Esit("Parça çğışöü 777", Convert.ToString(Skaler(c, "SELECT Ad FROM Parca WHERE Kod='K777'")), "Türkçe karakter");
                    Esit(1165.5, Convert.ToDouble(Skaler(c, "SELECT Miktar FROM Parca WHERE Kod='K777'")), "sayı değeri");
                    Esit(1, Convert.ToInt32(Skaler(c, "SELECT COUNT(*) FROM sys.indexes WHERE name='ix_Parca_Kod'")), "dizin kuruldu");
                    Sql(c, "INSERT INTO Parca (Kod, Ad) VALUES ('YENI','otomatik numara denemesi')");
                    Esit(12001, Convert.ToInt32(Skaler(c, "SELECT MAX(Id) FROM Parca")), "IDENTITY devam ediyor");
                }
                Esit("Bos", Aktarim.Calistir(hedefBag, "bos", true, "YON", "parola1234", lite) != null ? "Bos" : "", "boş kurulum çalıştı");
                using (SqlConnection c = new SqlConnection(hedefBag))
                {
                    c.Open();
                    Esit(0, Convert.ToInt32(Skaler(c, "SELECT COUNT(*) FROM Parca")), "boş kurulumda veri yok");
                    Esit("YON", Convert.ToString(Skaler(c, "SELECT KullaniciAdi FROM Kullanici")), "yönetici oluştu");
                }

                Console.WriteLine("3) IFS kaynağı: yeni / aynı / değişen / silinen");
                using (SqlConnection c = new SqlConnection(hedefBag))
                {
                    c.Open();
                    Sql(c, "IF OBJECT_ID('IfsKaynak') IS NOT NULL DROP TABLE IfsKaynak");
                    Sql(c, "CREATE TABLE IfsKaynak (Sirket NVARCHAR(10), ParcaNo NVARCHAR(30), Aciklama NVARCHAR(100), Fiyat DECIMAL(18,4), Tarih DATETIME)");
                    for (int i = 1; i <= 50; i++) Sql(c, "INSERT INTO IfsKaynak VALUES ('TNK','P" + i + "','Açıklama " + i + "'," + i + ".25, '2026-02-01')");
                    VkTanim t = new VkTanim { id = "x1", ad = "Sınama", tur = "sqlserver", baglanti = hedefBag, sorgu = "SELECT * FROM IfsKaynak", anahtar = "Sirket,ParcaNo", hedef = "Sinama", silinenleriIsaretle = true };
                    string yerel = lite;
                    VkSonuc s1 = Vk.Cek(t, yerel); Esit(50, s1.Yeni, "ilk çekme: yeni");
                    VkSonuc s2 = Vk.Cek(t, yerel); Esit(50, s2.Ayni, "ikinci çekme: aynı"); Esit(0, s2.Yeni + s2.Degisen, "ikinci çekme: yeni/değişen yok");
                    Sql(c, "UPDATE IfsKaynak SET Fiyat=99.5 WHERE ParcaNo='P7'");
                    Sql(c, "DELETE FROM IfsKaynak WHERE ParcaNo='P9'");
                    VkSonuc s3 = Vk.Cek(t, yerel); Esit(1, s3.Degisen, "bir hücre değişti: güncellendi"); Esit(1, s3.Silinen, "kaynaktan silinen işaretlendi"); Esit(48, s3.Ayni, "diğerleri aynı");
                    using (SQLiteConnection y = new SQLiteConnection(yerel).OpenAndReturn())
                    using (SQLiteCommand k = new SQLiteCommand("SELECT Fiyat FROM IFS_Sinama WHERE ParcaNo='P7'", y))
                        Esit(99.5, Convert.ToDouble(k.ExecuteScalar()), "yerel tabloda yeni fiyat");
                    Sql(c, "ALTER TABLE IfsKaynak ADD Yeni NVARCHAR(10) NULL");
                    VkSonuc s4 = Vk.Cek(t, yerel); Esit(true, s4.Degisen == 49, "yeni sütun eklenince tüm satırlar güncellendi");
                }
                Console.WriteLine("4) IFS'e yazma (kalıp başına tek satır)");
                using (SQLiteConnection y = new SQLiteConnection(lite).OpenAndReturn())
                {
                    foreach (string q in new[] {
                        "CREATE TABLE KalipTanim (Anahtar TEXT PRIMARY KEY, KalipKodu TEXT, KalipAdi TEXT, KalipTuru TEXT, TepeKodu TEXT, Notlar TEXT, SonDegisiklik TEXT)",
                        "CREATE TABLE KalipLokasyon (Anahtar TEXT PRIMARY KEY, KalipKodu TEXT, Lokasyon TEXT, SahipBolum TEXT, Raf TEXT, SonDegisiklik TEXT)",
                        "CREATE TABLE Talep (Id INTEGER PRIMARY KEY, TalepNo TEXT, Statu TEXT, Bolum TEXT, TalepTarihi TEXT)",
                        "CREATE TABLE TalepKalip (Id INTEGER PRIMARY KEY, TalepId INTEGER, Anahtar TEXT, KalipStatu TEXT, TashihSayisi INTEGER, SonDegisiklik TEXT)",
                        "INSERT INTO KalipTanim (Anahtar, KalipKodu, KalipAdi) VALUES ('A1','A-1','Kalıp bir'),('A2','A-2','Kalıp iki'),('A3','A-3','Kalıp üç')",
                        "INSERT INTO KalipLokasyon (Anahtar, KalipKodu, Raf) VALUES ('A1','A-1','R1')",
                        "INSERT INTO Talep VALUES (1,'T-1','Açık','CNC','2026-11-01')",
                        "INSERT INTO TalepKalip VALUES (1,1,'A1','Planlandı',2,'2026-11-01 10:00:00')" })
                        using (SQLiteCommand k = new SQLiteCommand(q, y)) k.ExecuteNonQuery();
                }
                using (SqlConnection c = new SqlConnection(hedefBag))
                {
                    c.Open();
                    Sql(c, "IF OBJECT_ID('KalipAra') IS NOT NULL DROP TABLE KalipAra");
                    Sql(c, "CREATE TABLE KalipAra (KOD NVARCHAR(30), AD NVARCHAR(100), RAF NVARCHAR(30), STATU NVARCHAR(30), TASHIH FLOAT, GUNC DATETIME)");
                    VkTanim w = new VkTanim { id = "w1", ad = "Yazma", tur = "sqlserver", baglanti = hedefBag, yazAktif = true, yazTablo = "KalipAra", yazAnahtar = "KOD",
                        yazEslesme = new System.Collections.Generic.List<VkEsle> {
                            new VkEsle { hedef = "AD", kaynak = "KalipAdi" }, new VkEsle { hedef = "RAF", kaynak = "Raf" }, new VkEsle { hedef = "STATU", kaynak = "SonTalepStatu" },
                            new VkEsle { hedef = "TASHIH", kaynak = "TashihSayisi" }, new VkEsle { hedef = "GUNC", kaynak = "Guncelleme" } } };
                    Esit(0, Vk.Gonder(w, "degisen", lite).Yeni, "temel yokken otomatik gönderim yapılmaz");
                    Esit(3, Vk.Gonder(w, "temel", lite).Yeni, "temel alındı");
                    Esit(0, Convert.ToInt32(Skaler(c, "SELECT COUNT(*) FROM KalipAra")), "temel alırken IFS'e yazılmadı");
                    VkSonuc g1 = Vk.Gonder(w, "degisen", lite); Esit(3, g1.Ayni, "değişiklik yok: hepsi aynı"); Esit(0, g1.Yeni + g1.Degisen, "değişiklik yok: gönderim yok");
                    using (SQLiteConnection y = new SQLiteConnection(lite).OpenAndReturn())
                    using (SQLiteCommand k = new SQLiteCommand("UPDATE KalipLokasyon SET Raf='R9' WHERE Anahtar='A1'", y)) k.ExecuteNonQuery();
                    VkSonuc g2 = Vk.Gonder(w, "degisen", lite); Esit(1, g2.Yeni, "raf değişti: satır IFS'te yoktu, eklendi"); Esit(2, g2.Ayni, "öbürleri aynı");
                    Esit("R9", Convert.ToString(Skaler(c, "SELECT RAF FROM KalipAra WHERE KOD='A-1'")), "IFS tablosunda yeni raf");
                    Esit("Açık", Convert.ToString(Skaler(c, "SELECT STATU FROM KalipAra WHERE KOD='A-1'")), "son talep statüsü");
                    Esit(2.0, Convert.ToDouble(Skaler(c, "SELECT TASHIH FROM KalipAra WHERE KOD='A-1'")), "tashih sayısı");
                    using (SQLiteConnection y = new SQLiteConnection(lite).OpenAndReturn())
                    using (SQLiteCommand k = new SQLiteCommand("UPDATE KalipLokasyon SET Raf='R10' WHERE Anahtar='A1'", y)) k.ExecuteNonQuery();
                    VkSonuc g3 = Vk.Gonder(w, "degisen", lite); Esit(1, g3.Degisen, "ikinci değişiklik: UPDATE");
                    Esit("R10", Convert.ToString(Skaler(c, "SELECT RAF FROM KalipAra WHERE KOD='A-1'")), "IFS tablosu güncellendi");
                    Esit(1, Convert.ToInt32(Skaler(c, "SELECT COUNT(*) FROM KalipAra")), "kalıp başına tek satır");
                    Vk.Gonder(w, "tum", lite);
                    Esit(3, Convert.ToInt32(Skaler(c, "SELECT COUNT(*) FROM KalipAra")), "hepsini gönder: 3 satır");
                }
                Console.WriteLine("SINAMA TAMAM");
                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                return 1;
            }
            finally { try { SQLiteConnection.ClearAllPools(); Directory.Delete(tmp, true); } catch { } }
        }
    }
}
