using System;
using System.Linq;
using System.ComponentModel;
using System.Configuration;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using Guna.UI2.WinForms;

namespace KalipTakip.Arayuz
{
    /// <summary>
    /// Giriş ekranı: çerçevesiz, yuvarlak köşeli, canlı degrade zeminli.
    /// Pencereyi boş yerinden tutup sürükleyince yarı saydamlaşır. 3 hatalı denemede kapanır.
    /// </summary>
    [DesignerCategory("Code")]
    public class frmLogin : TabanForm
    {
        private const int AzamiDeneme = 3;
        private readonly Guna2TextBox _txtAd;
        private readonly Guna2TextBox _txtParola;
        private readonly Guna2GradientButton _btnGiris;
        private readonly Label _lblHata;
        private int _deneme;

        public Kullanici GirisYapan { get; private set; }

        public frmLogin() : base("Kalıp Takip Sistemi - Giriş", "", new Size(900, 540), false)
        {
            SuspendLayout();
            UstBant.Visible = false;

            // Sol taraf: logo plakası + başlık
            Guna2Panel plaka = LogoPlaka.Olustur(360, 112);
            plaka.BorderRadius = 22;
            plaka.Padding = new Padding(24, 16, 24, 16);
            plaka.Location = new Point(56, 108);

            Label marka = Tema.Etiket("Kalıp Takip Sistemi", new Font("Segoe UI Semibold", 27F), Color.White);
            marka.Location = new Point(52, 246);

            Guna2GradientPanel cizgi = new Guna2GradientPanel();
            cizgi.FillColor = Tema.MarkaYesil;
            cizgi.FillColor2 = Tema.Turkuaz;
            cizgi.BorderRadius = 3;
            cizgi.BackColor = Color.Transparent;
            cizgi.SetBounds(58, 312, 90, 6);

            Label slogan = Tema.Etiket("Kalıphane kayıt, bakım ve süre takibi", new Font("Segoe UI", 12F), Color.FromArgb(210, 204, 246));
            slogan.Location = new Point(56, 330);

            Label oz1 = Tema.Etiket("●  Hızlı arama ve filtreleme", Tema.Normal, Color.FromArgb(170, 220, 255));
            oz1.Location = new Point(58, 388);
            Label oz2 = Tema.Etiket("●  Tashih ve süre takibi", Tema.Normal, Color.FromArgb(190, 250, 200));
            oz2.Location = new Point(58, 414);
            Label oz3 = Tema.Etiket("●  Anlık istatistik ve raporlar", Tema.Normal, Color.FromArgb(255, 214, 160));
            oz3.Location = new Point(58, 440);

            // Sağ taraf: giriş kartı
            Guna2Panel kart = new Guna2Panel();
            kart.FillColor = Color.FromArgb(19, 23, 46);
            kart.BorderColor = Color.FromArgb(80, 88, 150);
            kart.BorderThickness = 1;
            kart.BorderRadius = 26;
            kart.BackColor = Color.Transparent;
            kart.SetBounds(500, 52, 354, 436);

            Label baslik = Tema.Etiket("Hoş geldiniz", new Font("Segoe UI Semibold", 21F), Color.White);
            baslik.Location = new Point(30, 28);
            Label alt = Tema.Etiket("Devam etmek için giriş yapın", Tema.Normal, Tema.YaziSoluk);
            alt.Location = new Point(33, 72);

            // Düzen tablosu: kutular satır içinde Dock=Fill olduğundan taşma/kayma olmaz
            TableLayoutPanel duzen = new TableLayoutPanel();
            duzen.SetBounds(28, 108, 298, 300);
            duzen.BackColor = Color.Transparent;
            duzen.ColumnCount = 1;
            duzen.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            float[] satirlar = { 24F, 54F, 10F, 24F, 54F, 46F, 56F, 30F };
            duzen.RowCount = satirlar.Length;
            foreach (float h in satirlar) duzen.RowStyles.Add(new RowStyle(SizeType.Absolute, h));

            Label l1 = Tema.Etiket("Kullanıcı adı", Tema.Kucuk, Tema.YaziSoluk);
            l1.AutoSize = false;
            l1.Dock = DockStyle.Fill;
            l1.TextAlign = ContentAlignment.BottomLeft;
            _txtAd = Tema.Kutu(50);
            _txtAd.Dock = DockStyle.Fill;
            _txtAd.Margin = new Padding(0);
            _txtAd.BorderRadius = 14;
            _txtAd.Font = new Font("Segoe UI", 11F);
            _txtAd.CharacterCasing = CharacterCasing.Upper;
            _txtAd.IconLeft = Simge.Kisi(Tema.YaziSoluk);
            _txtAd.IconLeftSize = new Size(18, 18);
            _txtAd.PlaceholderText = "Kullanıcı adınız";

            Label l2 = Tema.Etiket("Parola", Tema.Kucuk, Tema.YaziSoluk);
            l2.AutoSize = false;
            l2.Dock = DockStyle.Fill;
            l2.TextAlign = ContentAlignment.BottomLeft;
            _txtParola = Tema.Kutu(100);
            _txtParola.Dock = DockStyle.Fill;
            _txtParola.Margin = new Padding(0);
            _txtParola.BorderRadius = 14;
            _txtParola.Font = new Font("Segoe UI", 11F);
            _txtParola.UseSystemPasswordChar = true;
            _txtParola.IconLeft = Simge.Kilit(Tema.YaziSoluk);
            _txtParola.IconLeftSize = new Size(18, 18);
            _txtParola.PlaceholderText = "Parolanız";

            _lblHata = Tema.Etiket("", new Font("Segoe UI Semibold", 9.5F), Color.FromArgb(255, 120, 120));
            _lblHata.AutoSize = false;
            _lblHata.Dock = DockStyle.Fill;
            _lblHata.TextAlign = ContentAlignment.MiddleLeft;

            _btnGiris = Tema.Buton("Giriş Yap", Tema.Mavi, Tema.Mor, 294, 52);
            _btnGiris.Font = new Font("Segoe UI Semibold", 12F);
            _btnGiris.BorderRadius = 16;
            _btnGiris.Dock = DockStyle.Fill;
            _btnGiris.Margin = new Padding(0, 2, 0, 2);
            _btnGiris.Click += GirisTiklandi;

            duzen.Controls.Add(l1, 0, 0);
            duzen.Controls.Add(_txtAd, 0, 1);
            duzen.Controls.Add(l2, 0, 3);
            duzen.Controls.Add(_txtParola, 0, 4);
            duzen.Controls.Add(_lblHata, 0, 5);
            duzen.Controls.Add(_btnGiris, 0, 6);
            kart.Controls.Add(duzen);
            kart.Controls.Add(baslik);
            kart.Controls.Add(alt);

            bool misafirAcik = string.Equals(ConfigurationManager.AppSettings["MisafirGirisi"], "true", StringComparison.OrdinalIgnoreCase);
            if (misafirAcik)
            {
                LinkLabel lnk = new LinkLabel();
                lnk.Text = "Misafir olarak gir (salt okunur)";
                lnk.Font = Tema.Normal;
                lnk.AutoSize = true;
                lnk.BackColor = Color.Transparent;
                lnk.LinkColor = Color.FromArgb(140, 180, 255);
                lnk.ActiveLinkColor = Color.White;
                lnk.Location = new Point(32, 404);
                lnk.LinkClicked += delegate { MisafirGir(); };
                kart.Controls.Add(lnk);
            }

            Guna2ControlBox kapat = new Guna2ControlBox();
            kapat.ControlBoxType = Guna.UI2.WinForms.Enums.ControlBoxType.CloseBox;
            kapat.FillColor = Color.Transparent;
            kapat.IconColor = Color.White;
            kapat.BackColor = Color.Transparent;
            kapat.Size = new Size(46, 40);
            kapat.Location = new Point(ClientSize.Width - 54, 8);
            kapat.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            kapat.HoverState.FillColor = Tema.Kirmizi;
            kapat.HoverState.IconColor = Color.White;

            Govde.Controls.Add(kapat);
            Govde.Controls.Add(plaka);
            Govde.Controls.Add(marka);
            Govde.Controls.Add(cizgi);
            Govde.Controls.Add(slogan);
            Govde.Controls.Add(oz1);
            Govde.Controls.Add(oz2);
            Govde.Controls.Add(oz3);
            Govde.Controls.Add(kart);

            // Boş yerlerden tutup sürükleme (yarı saydam)
            SurukleBagla(this);
            SurukleBagla(Govde);
            SurukleBagla(plaka);
            SurukleBagla(marka);
            SurukleBagla(slogan);
            SurukleBagla(kart);

            ResumeLayout(false);
            Shown += delegate { _txtAd.Focus(); };
        }

        private static void SabitBoyut(Control c, int x, int y, int w, int h)
        {
            c.Anchor = AnchorStyles.Top | AnchorStyles.Left;
            c.MinimumSize = new Size(w, h);
            c.MaximumSize = new Size(w, h);
            c.SetBounds(x, y, w, h);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (Pen ring = new Pen(Color.FromArgb(40, 255, 255, 255), 1.5F))
            {
                g.DrawEllipse(ring, 20, 20, 280, 280);
                g.DrawEllipse(ring, 330, Height - 200, 140, 140);
            }
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Enter)
            {
                GirisTiklandi(this, EventArgs.Empty);
                return true;
            }
            if (keyData == Keys.Escape)
            {
                DialogResult = DialogResult.Cancel;
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        private void MisafirGir()
        {
            Kullanici k = new Kullanici();
            k.KullaniciAdi = "MİSAFİR";
            k.Misafir = true;
            GirisYapan = k;
            DialogResult = DialogResult.OK;
        }

        private void GirisTiklandi(object sender, EventArgs e)
        {
            _lblHata.Text = "";
            if (_txtAd.Text.Trim().Length == 0 || _txtParola.Text.Length == 0)
            {
                _lblHata.Text = "Kullanıcı adı ve parola gerekli.";
                return;
            }

            Kullanici k = null;
            GirisSonucu sonuc;
            try
            {
                using (new Bekleme())
                {
                    sonuc = KullaniciRepository.GirisDene(_txtAd.Text, _txtParola.Text, ref k);
                }
            }
            catch (Exception ex)
            {
                HataGunlugu.Yaz(ex);
                Mesajlar.Hata("Giriş yapılamadı:\r\n\r\n" + Db.AnlasilirMesaj(ex));
                return;
            }

            if (sonuc == GirisSonucu.Basarili && k != null)
            {
                string cihazMesaj = "";
                bool yeniIstek = false;
                CihazSonuc cs;
                try { cs = CihazKontrol(k, out cihazMesaj, out yeniIstek); }
                catch (Exception ex)
                {
                    HataGunlugu.Yaz(ex);
                    Mesajlar.Hata("Cihaz kontrolü yapılamadı:\r\n\r\n" + Db.AnlasilirMesaj(ex));
                    return;
                }
                if (cs != CihazSonuc.Izinli)
                {
                    _txtParola.Text = "";
                    Mesajlar.Uyari(cihazMesaj);
                    return;
                }
                GirisYapan = k;
                DialogResult = DialogResult.OK;
                return;
            }

            _deneme++;
            _txtParola.Text = "";
            if (_deneme >= AzamiDeneme)
            {
                Mesajlar.Uyari("3 kez hatalı giriş yapıldı. Uygulama kapatılıyor.");
                DialogResult = DialogResult.Cancel;
            }
            else
            {
                string neden;
                switch (sonuc)
                {
                    case GirisSonucu.KullaniciYok: neden = "Böyle bir kullanıcı yok."; break;
                    case GirisSonucu.Pasif: neden = "Bu kullanıcı pasif, giriş yapamaz."; break;
                    default: neden = "Yanlış şifre girdiniz."; break;
                }
                _lblHata.Text = neden + " (Kalan hak: " + (AzamiDeneme - _deneme).ToString(Kultur.Inv) + ")";
                if (sonuc == GirisSonucu.KullaniciYok) { _txtAd.Focus(); _txtAd.SelectAll(); }
                else _txtParola.Focus();
            }
        }
    
        // Cihaz yetkilendirmesi: Windows makine kimliği (MachineGuid) cihaz kodu olarak kullanılır; MAC bilgi amaçlıdır.
        private static CihazSonuc CihazKontrol(Kullanici k, out string mesaj, out bool yeniIstek)
        {
            string kod = "";
            try
            {
                using (Microsoft.Win32.RegistryKey rk = Microsoft.Win32.RegistryKey.OpenBaseKey(Microsoft.Win32.RegistryHive.LocalMachine, Microsoft.Win32.RegistryView.Registry64)
                    .OpenSubKey(@"SOFTWARE\Microsoft\Cryptography"))
                    if (rk != null) kod = Convert.ToString(rk.GetValue("MachineGuid"));
            }
            catch { }
            if (string.IsNullOrEmpty(kod)) kod = "exe-" + Environment.MachineName + "-" + Environment.UserDomainName;
            kod = "exe|" + kod;
            string mac = "", ip = "";
            try
            {
                foreach (System.Net.NetworkInformation.NetworkInterface ni in System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (ni.OperationalStatus != System.Net.NetworkInformation.OperationalStatus.Up || ni.NetworkInterfaceType == System.Net.NetworkInformation.NetworkInterfaceType.Loopback) continue;
                    string m = ni.GetPhysicalAddress().ToString();
                    if (m.Length < 12) continue;
                    mac = string.Join(":", Enumerable.Range(0, 6).Select(i => m.Substring(i * 2, 2)).ToArray());
                    foreach (System.Net.NetworkInformation.UnicastIPAddressInformation ua in ni.GetIPProperties().UnicastAddresses)
                        if (ua.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork) { ip = ua.Address.ToString(); break; }
                    break;
                }
            }
            catch { }
            string bilgi = "Exe; Windows " + Environment.OSVersion.Version + "; makine " + Environment.MachineName + "; kullanıcı " + Environment.UserName;
            mesaj = "";
            yeniIstek = false;
            return CihazRepository.Kontrol(k.Id, kod, Environment.MachineName, mac, ip, bilgi, "Exe", ref mesaj, ref yeniIstek);
        }
}
}
