using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using Guna.UI2.WinForms;

namespace KalipTakip.Arayuz
{
    /// <summary>Portalda görünen bir form/modül kartı.</summary>
    public class PortalModulu
    {
        public string Ad;
        public string Aciklama;
        public Func<Bitmap> Simge;           // kartta çizilen simge (PortalSimge.*)
        public Color Renk1, Renk2;
        public bool SadeceYonetici;
        public string Izin;                  // portalı görmek için gereken yetki anahtarı (null = herkes)
        public Action<Form> Ac;              // null ise "yakında" kartı

        public PortalModulu(string ad, string aciklama, Func<Bitmap> simge, Color r1, Color r2, Action<Form> ac, bool sadeceYonetici = false, string izin = null)
        {
            Ad = ad; Aciklama = aciklama; Simge = simge; Renk1 = r1; Renk2 = r2; Ac = ac; SadeceYonetici = sadeceYonetici; Izin = izin;
        }
    }

    /// <summary>
    /// Giriş sonrası açılan portal: kullanıcının açabileceği formlar kart olarak listelenir.
    /// YENİ FORM EKLEMEK İÇİN: aşağıdaki ModulleriOlustur() listesine tek satır eklemek yeterlidir.
    /// </summary>
    [DesignerCategory("Code")]
    public class frmPortal : TabanForm
    {
        private FlowLayoutPanel _akis;

        public frmPortal()
            : base("Kalıp Takip Portalı", "Açmak istediğiniz formu seçin", new Size(1100, 700), true)
        {
            MinimumSize = new Size(820, 560);
            SuspendLayout();

            string ad = Oturum.Aktif.KullaniciAdi + (Oturum.Aktif.IsAdmin ? "  (Yönetici)" : (Oturum.Aktif.Misafir ? "  (salt okunur)" : ""));
            Label lblKullanici = Tema.Etiket("Kullanıcı: " + ad, Tema.Normal, Color.FromArgb(222, 226, 250));
            lblKullanici.Margin = new Padding(0, 8, 18, 0);
            Guna2Button btnCikis = Tema.BantButonu("Çıkış", 84);
            btnCikis.FillColor = Color.FromArgb(150, 40, 80);
            btnCikis.HoverState.FillColor = Tema.Kirmizi;
            btnCikis.Margin = new Padding(0);
            btnCikis.Click += delegate { Close(); };
            UstSag.Controls.Add(lblKullanici);
            UstSag.Controls.Add(btnCikis);

            Label hosgeldin = Tema.Etiket("Hoş geldiniz, " + Oturum.Aktif.KullaniciAdi, new Font("Segoe UI Semibold", 20F), Color.White);
            hosgeldin.Location = new Point(34, 22);
            Label alt = Tema.Etiket("Çalışmak istediğiniz formu seçin.", Tema.Normal, Tema.YaziSoluk);
            alt.Location = new Point(37, 66);

            _akis = new FlowLayoutPanel();
            _akis.Dock = DockStyle.Fill;
            _akis.AutoScroll = true;
            _akis.BackColor = Color.Transparent;
            _akis.Padding = new Padding(26, 16, 10, 10);

            Panel ust = new Panel { Dock = DockStyle.Top, Height = 104, BackColor = Color.Transparent };
            ust.Controls.Add(hosgeldin);
            ust.Controls.Add(alt);

            Govde.Controls.Add(_akis);
            Govde.Controls.Add(ust);
            SurukleBagla(ust);
            SurukleBagla(hosgeldin);
            SurukleBagla(alt);

            foreach (PortalModulu m in ModulleriOlustur())
            {
                if (m.SadeceYonetici && !Oturum.Aktif.IsAdmin) continue;
                if (m.Izin != null && !Oturum.Aktif.GorebilirMi(m.Izin)) continue;
                _akis.Controls.Add(KartYap(m));
            }
            ResumeLayout(false);
        }

        /// <summary>Portaldaki formlar. Yeni form eklemek için buraya bir satır ekleyin.</summary>
        /// <summary>Mobil sunucuyu (KalipTakipSunucu.exe, bu programla aynı klasörde) gerekirse başlatır ve web arama sayfasını tarayıcıda açar.</summary>
        private static void WebAramaAc(string sayfa = "")
        {
            const int port = 8080;
            try
            {
                if (!PortAcikMi(port))
                {
                    string exe = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "KalipTakipSunucu.exe");
                    if (!System.IO.File.Exists(exe))
                    {
                        Mesajlar.Uyari("KalipTakipSunucu.exe bu programın klasöründe bulunamadı.\r\n\r\nKalipTakip.Sunucu projesini derleyip çıkan dosyaları (KalipTakipSunucu.exe ve .config) KalipTakip.exe'nin yanına kopyalayın.");
                        return;
                    }
                    System.Diagnostics.ProcessStartInfo psi = new System.Diagnostics.ProcessStartInfo(exe);
                    psi.WorkingDirectory = AppDomain.CurrentDomain.BaseDirectory;
                    psi.WindowStyle = System.Diagnostics.ProcessWindowStyle.Minimized;
                    psi.UseShellExecute = true;
                    System.Diagnostics.Process.Start(psi);
                    for (int i = 0; i < 20 && !PortAcikMi(port); i++) System.Threading.Thread.Sleep(250);
                }
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("http://localhost:" + port + "/" + sayfa) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                HataGunlugu.Yaz(ex);
                Mesajlar.Hata("Web uygulaması açılamadı:\r\n\r\n" + ex.Message);
            }
        }

        private static bool PortAcikMi(int port)
        {
            try
            {
                using (System.Net.Sockets.TcpClient c = new System.Net.Sockets.TcpClient())
                {
                    IAsyncResult r = c.BeginConnect("127.0.0.1", port, null, null);
                    bool tamam = r.AsyncWaitHandle.WaitOne(300);
                    if (tamam) { c.EndConnect(r); return true; }
                    return false;
                }
            }
            catch { return false; }
        }

        private List<PortalModulu> ModulleriOlustur()
        {
            List<PortalModulu> l = new List<PortalModulu>();
            l.Add(new PortalModulu("Kalıp Takip", "Kalıp kayıt, bakım ve süre takibi; istatistik ve Excel çıktısı",
                PortalSimge.Disli, Tema.Mavi, Tema.Mor, delegate (Form sahip) { using (frmMain f = new frmMain()) f.ShowDialog(); }, false, KalipTakip.Izin.KalipGor));
            l.Add(new PortalModulu("Kalıp Listesi", "Tüm kalıplar tek satırda; geçmiş, tashih ve talepler; Excel'den yükleme",
                PortalSimge.Disli, Tema.Turkuaz, Tema.Mavi, delegate (Form sahip) { using (frmKalipListesi f = new frmKalipListesi()) f.ShowDialog(); }, false, KalipTakip.Izin.KalipListeGor));
            l.Add(new PortalModulu("Web Uygulaması", "Tüm modüller tarayıcıda açılır (Kalıp Takip, Talep, Kalıp Listesi, Raflar, Kullanıcılar...); telefonla aynı adres",
                PortalSimge.Disli, Tema.Mor, Tema.Mavi, delegate (Form sahip) { WebAramaAc(); }, false, KalipTakip.Izin.KalipListeGor));
            l.Add(new PortalModulu("Talep", "Talep akışı artık web uygulamasında (gönder, teslim al, bildirim); tarayıcıda açılır",
                PortalSimge.Talep, Tema.Turuncu, Tema.Pembe, delegate (Form sahip) { WebAramaAc("#/talep"); }, false, KalipTakip.Izin.TalepGor));
            l.Add(new PortalModulu("Kullanıcılar", "Kullanıcı ekle, parola ve yetkileri ayarla",
                PortalSimge.Kullanicilar, Tema.Yesil, Color.FromArgb(13, 148, 136), delegate (Form sahip) { using (frmKullanicilar f = new frmKullanicilar()) f.ShowDialog(); }, true));
            l.Add(new PortalModulu("Raflar", "Raf adlarını tanımla; kalıplar bu raflara yerleştirilir",
                PortalSimge.Disli, Tema.Turkuaz, Tema.Yesil, delegate (Form sahip) { using (frmRaflar f = new frmRaflar()) f.ShowDialog(); }, false, KalipTakip.Izin.KalipListeEkle));
            l.Add(new PortalModulu("Bölümler ve Lokasyonlar", "Bölümleri, lokasyonları ve ana lokasyonları tanımla",
                PortalSimge.Disli, Tema.Mavi, Tema.Turkuaz, delegate (Form sahip) { using (frmLokasyonlar f = new frmLokasyonlar()) f.ShowDialog(); }, true));
            // Örnek (ileride): l.Add(new PortalModulu("Stok", "Stok girişi / çıkışı", PortalSimge.Arti, Tema.Turuncu, Tema.Pembe, delegate (Form sahip) { using (frmStok f = new frmStok()) f.ShowDialog(); }));
            l.Add(new PortalModulu("Yeni formlar", "Yeni formlar burada görünecek", PortalSimge.Arti, Tema.Gri1, Tema.Gri2, null));
            return l;
        }

        private Control KartYap(PortalModulu m)
        {
            bool aktif = m.Ac != null;
            Guna2GradientPanel kart = new Guna2GradientPanel();
            kart.Size = new Size(330, 170);
            kart.Margin = new Padding(0, 0, 22, 22);
            kart.BorderRadius = 22;
            kart.FillColor = aktif ? m.Renk1 : Color.FromArgb(48, 56, 96);
            kart.FillColor2 = aktif ? m.Renk2 : Color.FromArgb(34, 40, 74);
            kart.GradientMode = LinearGradientMode.ForwardDiagonal;
            kart.BackColor = Color.Transparent;
            kart.ShadowDecoration.Enabled = true;
            kart.ShadowDecoration.Depth = 14;
            kart.ShadowDecoration.Color = Color.Black;
            kart.Cursor = aktif ? Cursors.Hand : Cursors.Default;

            Label rozet = new Label();
            rozet.Text = "";
            rozet.Image = m.Simge();
            rozet.ImageAlign = ContentAlignment.MiddleCenter;
            rozet.BackColor = Color.FromArgb(60, 255, 255, 255);
            rozet.SetBounds(22, 22, 56, 56);

            Label baslik = new Label();
            baslik.Text = m.Ad;
            baslik.Font = new Font("Segoe UI Semibold", 15F);
            baslik.ForeColor = Color.White;
            baslik.BackColor = Color.Transparent;
            baslik.AutoSize = true;
            baslik.Location = new Point(22, 92);

            Label acik = new Label();
            acik.Text = m.Aciklama;
            acik.Font = Tema.Kucuk;
            acik.ForeColor = Color.FromArgb(225, 228, 250);
            acik.BackColor = Color.Transparent;
            acik.SetBounds(22, 124, 290, 38);

            kart.Controls.Add(rozet);
            kart.Controls.Add(baslik);
            kart.Controls.Add(acik);

            if (aktif)
            {
                Color c1 = m.Renk1, c2 = m.Renk2;
                EventHandler gir = delegate { kart.FillColor = Tema.Acik(c1, 28); kart.FillColor2 = Tema.Acik(c2, 28); };
                EventHandler cik = delegate
                {
                    if (!kart.ClientRectangle.Contains(kart.PointToClient(Cursor.Position)))
                    {
                        kart.FillColor = c1; kart.FillColor2 = c2;
                    }
                };
                EventHandler tikla = delegate { ModulAc(m); };
                foreach (Control c in new Control[] { kart, rozet, baslik, acik })
                {
                    c.MouseEnter += gir;
                    c.MouseLeave += cik;
                    c.Click += tikla;
                    c.Cursor = Cursors.Hand;
                }
            }
            return kart;
        }

        private void ModulAc(PortalModulu m)
        {
            try
            {
                Hide();
                m.Ac(this);
            }
            catch (Exception ex)
            {
                HataGunlugu.Yaz(ex);
                Mesajlar.Hata("Form açılamadı:\r\n\r\n" + ex.Message);
            }
            finally
            {
                Show();
                Activate();
            }
        }
    }

    /// <summary>Portal kartlarındaki beyaz çizim simgeleri (dosya gerektirmez).</summary>
    public static class PortalSimge
    {
        private static Bitmap Yeni(out Graphics g)
        {
            Bitmap b = new Bitmap(56, 56);
            g = Graphics.FromImage(b);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            return b;
        }

        /// <summary>Çark: kalıp / makine takibi.</summary>
        public static Bitmap Disli()
        {
            Graphics g;
            Bitmap b = Yeni(out g);
            using (g)
            using (Pen kalem = new Pen(Color.White, 3.2F))
            using (SolidBrush beyaz = new SolidBrush(Color.White))
            {
                float c = 28F;
                kalem.StartCap = LineCap.Round; kalem.EndCap = LineCap.Round;
                for (int i = 0; i < 8; i++)
                {
                    double a = Math.PI / 4 * i;
                    float x1 = c + (float)Math.Cos(a) * 15F, y1 = c + (float)Math.Sin(a) * 15F;
                    float x2 = c + (float)Math.Cos(a) * 22F, y2 = c + (float)Math.Sin(a) * 22F;
                    using (Pen diş = new Pen(Color.White, 6F))
                    {
                        g.DrawLine(diş, x1, y1, x2, y2);
                    }
                }
                g.FillEllipse(beyaz, c - 16, c - 16, 32, 32);
                using (SolidBrush delik = new SolidBrush(Color.FromArgb(70, 80, 190)))
                {
                    g.FillEllipse(delik, c - 7, c - 7, 14, 14);
                }
            }
            return b;
        }

        /// <summary>İki kişi: kullanıcılar.</summary>
        public static Bitmap Kullanicilar()
        {
            Graphics g;
            Bitmap b = Yeni(out g);
            using (g)
            using (SolidBrush beyaz = new SolidBrush(Color.White))
            using (SolidBrush soluk = new SolidBrush(Color.FromArgb(190, 255, 255, 255)))
            {
                g.FillEllipse(soluk, 31, 12, 15, 15);
                g.FillPie(soluk, 24, 28, 30, 30, 180, 180);
                g.FillEllipse(beyaz, 13, 13, 17, 17);
                g.FillPie(beyaz, 5, 31, 33, 33, 180, 180);
            }
            return b;
        }

        /// <summary>Evrak panosu: talep.</summary>
        public static Bitmap Talep()
        {
            Graphics g;
            Bitmap b = Yeni(out g);
            using (g)
            using (Pen kalem = new Pen(Color.White, 3.4F))
            using (Pen cizgi = new Pen(Color.FromArgb(220, 255, 255, 255), 3F))
            using (SolidBrush beyaz = new SolidBrush(Color.White))
            {
                kalem.LineJoin = LineJoin.Round;
                cizgi.StartCap = LineCap.Round; cizgi.EndCap = LineCap.Round;
                g.DrawRectangle(kalem, 12, 10, 32, 40);
                g.FillRectangle(beyaz, 20, 6, 16, 9);
                g.DrawLine(cizgi, 19, 25, 37, 25);
                g.DrawLine(cizgi, 19, 33, 37, 33);
                g.DrawLine(cizgi, 19, 41, 30, 41);
            }
            return b;
        }

        /// <summary>Artı: yeni form.</summary>
        public static Bitmap Arti()
        {
            Graphics g;
            Bitmap b = Yeni(out g);
            using (g)
            using (Pen kalem = new Pen(Color.White, 5F))
            {
                kalem.StartCap = LineCap.Round; kalem.EndCap = LineCap.Round;
                g.DrawLine(kalem, 28, 12, 28, 44);
                g.DrawLine(kalem, 12, 28, 44, 28);
            }
            return b;
        }
    }
}
