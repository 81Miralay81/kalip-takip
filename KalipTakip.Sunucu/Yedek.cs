using System;
using System.IO;
using System.Linq;
using System.Data.SQLite;
using System.Threading;

namespace KalipTakip.Sunucu
{
    /// <summary>
    /// Günlük otomatik yedek: veritabanının tarihli bir kopyası, veritabanı dosyasının yanındaki "Yedek" klasörüne alınır
    /// (KalipTakip_2026-11-02.db). Son 30 yedek saklanır. Yedek, SQLite'ın kendi yedekleme yöntemiyle alındığı için
    /// sunucu çalışırken de tutarlıdır.
    /// </summary>
    internal static class Yedek
    {
        private const int SaklanacakAdet = 30;
        private static Timer _zamanlayici;

        public static void Baslat()
        {
            // açılışta bir kez, sonra saatte bir kontrol: bugünün yedeği yoksa alınır
            _zamanlayici = new Timer(_ => Dene(), null, TimeSpan.FromSeconds(20), TimeSpan.FromHours(1));
        }

        private static void Dene()
        {
            try
            {
                string kaynak = Db.Yol;
                if (!File.Exists(kaynak)) return;
                string klasor = Path.Combine(Path.GetDirectoryName(kaynak) ?? ".", "Yedek");
                Directory.CreateDirectory(klasor);
                string hedef = Path.Combine(klasor, "KalipTakip_" + DateTime.Now.ToString("yyyy-MM-dd") + ".db");
                if (File.Exists(hedef)) return;
                string gecici = hedef + ".tmp";
                if (File.Exists(gecici)) File.Delete(gecici);
                using (SQLiteConnection kaynakBag = Db.Ac())
                using (SQLiteConnection hedefBag = new SQLiteConnection("Data Source=" + gecici + ";Version=3;Pooling=False;"))
                {
                    hedefBag.Open();
                    kaynakBag.BackupDatabase(hedefBag, "main", "main", -1, null, 0);
                }
                File.Move(gecici, hedef);
                foreach (string eski in Directory.GetFiles(klasor, "KalipTakip_*.db").OrderByDescending(x => x).Skip(SaklanacakAdet))
                {
                    try { File.Delete(eski); } catch { }
                }
                Console.WriteLine("[yedek] " + hedef);
            }
            catch (Exception ex) { HataGunlugu.Yaz(ex); }
        }
    }
}
