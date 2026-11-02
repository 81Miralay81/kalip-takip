using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace KalipTakip.Arayuz
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            CultureInfo tr = Kultur.Tr;
            Thread.CurrentThread.CurrentCulture = tr;
            Thread.CurrentThread.CurrentUICulture = tr;
            CultureInfo.DefaultThreadCurrentCulture = tr;
            CultureInfo.DefaultThreadCurrentUICulture = tr;

            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += BeklenmeyenHata;

            try
            {
                Baslat();
            }
            catch (Exception ex)
            {
                HataGunlugu.Yaz(ex);
                Mesajlar.Hata("Uygulama başlatılamadı.\r\n\r\n" + Db.AnlasilirMesaj(ex));
            }
        }

        private static void Baslat()
        {
            string mesaj = null;
            VeritabaniDurumu durum = Db.DurumBul(ref mesaj);
            if (durum == VeritabaniDurumu.Hata)
            {
                Mesajlar.Hata("Veritabanı açılamadı:\r\n\r\n" + mesaj +
                              "\r\n\r\nDosya yolu KalipTakip.exe.config içindeki VeritabaniYolu ayarından değiştirilebilir.");
                return;
            }
            if (durum != VeritabaniDurumu.Hazir)
            {
                using (frmSetup kurulum = new frmSetup())
                {
                    if (kurulum.ShowDialog() != DialogResult.OK) return;
                }
            }

            // Eski veritabanını yeni sürüme yükselt (Talep tablosu, yeni yetkiler); bir kez çalışır
            Sema.Yukselt();

            // Günde bir kez otomatik yedek (arka planda)
            Task.Run(() => Db.GunlukYedek());

            using (frmLogin giris = new frmLogin())
            {
                if (giris.ShowDialog() != DialogResult.OK) return;
                Oturum.Aktif = giris.GirisYapan;
            }

            Application.Run(new frmPortal());
        }

        private static void BeklenmeyenHata(object sender, ThreadExceptionEventArgs e)
        {
            HataGunlugu.Yaz(e.Exception);
            Mesajlar.Hata("Beklenmeyen bir hata oluştu:\r\n\r\n" + Db.AnlasilirMesaj(e.Exception));
        }
    }
}
