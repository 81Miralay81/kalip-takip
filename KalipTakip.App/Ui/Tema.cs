using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using Guna.UI2.WinForms;

namespace KalipTakip.Arayuz
{
    /// <summary>Uygulamanın canlı, koyu teması: renkler, yazı tipleri ve Guna kontrol üreticileri.</summary>
    public static class Tema
    {
        // ---- Zeminler
        public static readonly Color Zemin = Color.FromArgb(11, 13, 28);
        public static readonly Color ZeminUst = Color.FromArgb(30, 20, 84);
        public static readonly Color Kart = Color.FromArgb(21, 25, 49);
        public static readonly Color KartKenar = Color.FromArgb(52, 58, 102);
        public static readonly Color KartAcik = Color.FromArgb(33, 39, 74);
        public static readonly Color SatirAlt = Color.FromArgb(27, 32, 62);

        // ---- Yazılar
        public static readonly Color Yazi = Color.FromArgb(238, 241, 252);
        public static readonly Color YaziSoluk = Color.FromArgb(150, 160, 200);

        // ---- Giriş alanları
        public static readonly Color GirdiDolgu = Color.FromArgb(30, 35, 68);
        public static readonly Color GirdiKenar = Color.FromArgb(62, 70, 120);
        public static readonly Color GirdiSalt = Color.FromArgb(22, 26, 52);

        // ---- Canlı vurgular
        public static readonly Color Mavi = Color.FromArgb(59, 130, 246);
        public static readonly Color Mor = Color.FromArgb(139, 92, 246);
        public static readonly Color Pembe = Color.FromArgb(236, 72, 153);
        public static readonly Color Turuncu = Color.FromArgb(249, 115, 22);
        public static readonly Color Yesil = Color.FromArgb(34, 197, 94);
        public static readonly Color Turkuaz = Color.FromArgb(6, 182, 212);
        public static readonly Color Sari = Color.FromArgb(250, 204, 21);
        public static readonly Color Kirmizi = Color.FromArgb(239, 68, 68);
        public static readonly Color MarkaYesil = Color.FromArgb(126, 182, 45);
        public static readonly Color Gri1 = Color.FromArgb(71, 85, 120);
        public static readonly Color Gri2 = Color.FromArgb(100, 116, 150);

        public static readonly Color BantSol = Color.FromArgb(40, 22, 120);
        public static readonly Color BantSag = Color.FromArgb(96, 38, 160);

        public static readonly Font Normal = new Font("Segoe UI", 9.5F);
        public static readonly Font Kalin = new Font("Segoe UI Semibold", 9.5F);
        public static readonly Font Baslik = new Font("Segoe UI Semibold", 15F);
        public static readonly Font Orta = new Font("Segoe UI Semibold", 12F);
        public static readonly Font Kucuk = new Font("Segoe UI", 8.5F);
        public static readonly Font Buyuk = new Font("Segoe UI Semibold", 24F);

        public static Color Acik(Color c, int miktar)
        {
            return Color.FromArgb(c.A,
                Math.Max(0, Math.Min(255, c.R + miktar)),
                Math.Max(0, Math.Min(255, c.G + miktar)),
                Math.Max(0, Math.Min(255, c.B + miktar)));
        }

        // ---------------------------------------------------------------- MARKA
        private static Image _logo;
        private static bool _logoArandi;
        private static Icon _ikon;
        private static bool _ikonArandi;

        public static Image Logo
        {
            get
            {
                if (!_logoArandi)
                {
                    _logoArandi = true;
                    try
                    {
                        using (Stream s = Assembly.GetExecutingAssembly().GetManifestResourceStream("logo.png"))
                        {
                            if (s != null)
                            {
                                using (Image img = Image.FromStream(s)) { _logo = new Bitmap(img); }
                            }
                        }
                    }
                    catch { _logo = null; }
                }
                return _logo;
            }
        }

        public static Icon Ikon
        {
            get
            {
                if (!_ikonArandi)
                {
                    _ikonArandi = true;
                    try
                    {
                        using (Stream s = Assembly.GetExecutingAssembly().GetManifestResourceStream("uygulama.ico"))
                        {
                            if (s != null) _ikon = new Icon(s);
                        }
                    }
                    catch { _ikon = null; }
                }
                return _ikon;
            }
        }

        // ---------------------------------------------------------------- ÜRETİCİLER
        public static Label Etiket(string metin, Font yaziTipi, Color renk)
        {
            return new Label { Text = metin, Font = yaziTipi, ForeColor = renk, AutoSize = true, BackColor = Color.Transparent };
        }

        /// <summary>Yuvarlak köşeli, kenarlıklı kart paneli.</summary>
        public static Guna2Panel KartPaneli(int yaricap = 16)
        {
            Guna2Panel p = new Guna2Panel();
            p.FillColor = Kart;
            p.BorderColor = KartKenar;
            p.BorderThickness = 1;
            p.BorderRadius = yaricap;
            p.BackColor = Color.Transparent;
            return p;
        }

        /// <summary>Degrade dolgulu, yuvarlak köşeli renkli düğme.</summary>
        public static Guna2GradientButton Buton(string metin, Color c1, Color c2, int genislik = 120, int yukseklik = 38)
        {
            Guna2GradientButton b = new Guna2GradientButton();
            b.Text = metin;
            b.Size = new Size(genislik, yukseklik);
            b.BorderRadius = 10;
            b.FillColor = c1;
            b.FillColor2 = c2;
            b.GradientMode = LinearGradientMode.Horizontal;
            b.Font = Kalin;
            b.ForeColor = Color.White;
            b.Cursor = Cursors.Hand;
            b.Animated = true;
            b.Margin = new Padding(0, 0, 8, 8);
            b.BackColor = Color.Transparent;
            b.HoverState.FillColor = Acik(c1, 28);
            b.HoverState.FillColor2 = Acik(c2, 28);
            b.DisabledState.FillColor = Color.FromArgb(52, 56, 84);
            b.DisabledState.FillColor2 = Color.FromArgb(52, 56, 84);
            b.DisabledState.ForeColor = Color.FromArgb(130, 138, 170);
            return b;
        }

        /// <summary>Üst banttaki yarı saydam görünümlü küçük düğme.</summary>
        public static Guna2Button BantButonu(string metin, int genislik)
        {
            Guna2Button b = new Guna2Button();
            b.Text = metin;
            b.Size = new Size(genislik, 34);
            b.BorderRadius = 10;
            b.FillColor = Color.FromArgb(70, 50, 150);
            b.ForeColor = Color.White;
            b.Font = Kalin;
            b.Cursor = Cursors.Hand;
            b.Animated = true;
            b.Margin = new Padding(0, 0, 8, 0);
            b.BackColor = Color.Transparent;
            b.HoverState.FillColor = Color.FromArgb(104, 80, 206);
            return b;
        }

        public static Guna2TextBox Kutu(int maksimum = 0)
        {
            Guna2TextBox t = new Guna2TextBox();
            KutuStili(t);
            if (maksimum > 0) t.MaxLength = maksimum;
            return t;
        }

        public static void KutuStili(Guna2TextBox t)
        {
            t.BorderRadius = 9;
            t.FillColor = GirdiDolgu;
            t.BorderColor = GirdiKenar;
            t.ForeColor = Yazi;
            t.Font = Normal;
            t.Height = 36;
            t.PlaceholderForeColor = YaziSoluk;
            t.FocusedState.BorderColor = Mavi;
            t.FocusedState.FillColor = Color.FromArgb(36, 42, 80);
            t.HoverState.BorderColor = Color.FromArgb(104, 116, 190);
            t.BackColor = Color.Transparent;
        }

        public static void SaltOkunurStili(Guna2TextBox t)
        {
            t.ReadOnly = true;
            t.TabStop = false;
            t.FillColor = GirdiSalt;
            t.BorderColor = Color.FromArgb(46, 52, 92);
            t.ForeColor = Color.FromArgb(190, 200, 240);
            t.Cursor = Cursors.Default;
        }

        public static Guna2ComboBox Acilir(int genislik = 160)
        {
            Guna2ComboBox c = new Guna2ComboBox();
            c.DropDownStyle = ComboBoxStyle.DropDownList;
            c.BorderRadius = 9;
            c.FillColor = GirdiDolgu;
            c.BorderColor = GirdiKenar;
            c.ForeColor = Yazi;
            c.Font = Normal;
            c.Height = 36;
            c.Width = genislik;
            c.BackColor = Color.Transparent;
            c.FocusedState.BorderColor = Mavi;
            c.HoverState.BorderColor = Color.FromArgb(104, 116, 190);
            c.ItemsAppearance.BackColor = Color.FromArgb(30, 35, 68);
            c.ItemsAppearance.ForeColor = Yazi;
            c.ItemsAppearance.SelectedBackColor = Color.FromArgb(79, 70, 229);
            c.ItemsAppearance.SelectedForeColor = Color.White;
            return c;
        }

        public static Guna2DateTimePicker Tarih()
        {
            Guna2DateTimePicker d = new Guna2DateTimePicker();
            d.Format = DateTimePickerFormat.Custom;
            d.CustomFormat = "dd.MM.yyyy";
            d.ShowCheckBox = true;
            d.Checked = false;
            d.BorderRadius = 9;
            d.FillColor = GirdiDolgu;
            d.BorderColor = GirdiKenar;
            d.ForeColor = Yazi;
            d.Font = Normal;
            d.Height = 36;
            d.BackColor = Color.Transparent;
            d.FocusedColor = Mavi;
            return d;
        }

        public static Guna2CheckBox Onay(string metin)
        {
            Guna2CheckBox c = new Guna2CheckBox();
            c.Text = metin;
            c.ForeColor = Yazi;
            c.Font = Normal;
            c.AutoSize = true;
            c.BackColor = Color.Transparent;
            c.CheckedState.FillColor = Mavi;
            c.CheckedState.BorderColor = Mavi;
            c.CheckedState.BorderRadius = 4;
            c.UncheckedState.FillColor = GirdiDolgu;
            c.UncheckedState.BorderColor = GirdiKenar;
            c.UncheckedState.BorderRadius = 4;
            return c;
        }

        public static Guna2RadioButton Secenek(string metin)
        {
            Guna2RadioButton r = new Guna2RadioButton();
            r.Text = metin;
            r.ForeColor = Yazi;
            r.Font = Kalin;
            r.AutoSize = true;
            r.BackColor = Color.Transparent;
            r.CheckedState.FillColor = Mavi;
            r.CheckedState.BorderColor = Mavi;
            r.CheckedState.InnerColor = Color.White;
            r.UncheckedState.FillColor = GirdiDolgu;
            r.UncheckedState.BorderColor = GirdiKenar;
            return r;
        }

        /// <summary>Koyu, zengin tablo.</summary>
        public static Guna2DataGridView Tablo()
        {
            Guna2DataGridView g = new Guna2DataGridView();
            g.Theme = Guna.UI2.WinForms.Enums.DataGridViewPresetThemes.Dark;
            TabloStili(g);
            g.HandleCreated += delegate
            {
                // Guna kendi temasını tutamaç oluşunca uygular; bizim stilimiz bunun ardından yeniden yazılır.
                g.BeginInvoke(new Action(delegate { TabloStili(g); }));
            };
            return g;
        }

        public static void TabloStili(DataGridView g)
        {
            g.BorderStyle = BorderStyle.None;
            g.BackgroundColor = Kart;
            g.GridColor = Color.FromArgb(40, 46, 84);
            g.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            g.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
            g.EnableHeadersVisualStyles = false;
            g.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(52, 38, 126);
            g.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
            g.ColumnHeadersDefaultCellStyle.Font = Kalin;
            g.ColumnHeadersDefaultCellStyle.SelectionBackColor = Color.FromArgb(52, 38, 126);
            g.ColumnHeadersDefaultCellStyle.SelectionForeColor = Color.White;
            g.ColumnHeadersDefaultCellStyle.Padding = new Padding(6, 0, 0, 0);
            g.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            g.ColumnHeadersHeight = 40;
            g.RowHeadersVisible = false;
            g.RowTemplate.Height = 30;
            g.DefaultCellStyle.Font = Normal;
            g.DefaultCellStyle.ForeColor = Yazi;
            g.DefaultCellStyle.BackColor = Kart;
            g.DefaultCellStyle.SelectionBackColor = Color.FromArgb(79, 70, 229);
            g.DefaultCellStyle.SelectionForeColor = Color.White;
            g.DefaultCellStyle.Padding = new Padding(4, 0, 4, 0);
            g.AlternatingRowsDefaultCellStyle.BackColor = SatirAlt;
            g.AlternatingRowsDefaultCellStyle.ForeColor = Yazi;
            g.AlternatingRowsDefaultCellStyle.SelectionBackColor = Color.FromArgb(79, 70, 229);
            g.AlternatingRowsDefaultCellStyle.SelectionForeColor = Color.White;
            g.AllowUserToAddRows = false;
            g.AllowUserToDeleteRows = false;
            g.AllowUserToResizeRows = false;
            g.ReadOnly = true;
            g.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            g.MultiSelect = false;
            g.AutoGenerateColumns = false;
            g.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
            g.ColumnHeadersDefaultCellStyle.WrapMode = DataGridViewTriState.False;
            g.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleLeft;
            g.ScrollBars = ScrollBars.Both;
            g.DefaultCellStyle.WrapMode = DataGridViewTriState.False;
            try
            {
                typeof(DataGridView).InvokeMember("DoubleBuffered",
                    BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.SetProperty, null, g, new object[] { true });
            }
            catch { }
        }
    }

    /// <summary>GDI+ ile çizilen küçük simgeler (yazı tipi ya da dosya gerektirmez).</summary>
    public static class Simge
    {
        private static Bitmap Yeni(Action<Graphics> ciz)
        {
            Bitmap b = new Bitmap(20, 20);
            using (Graphics g = Graphics.FromImage(b))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                ciz(g);
            }
            return b;
        }

        public static Bitmap Kisi(Color c)
        {
            return Yeni(delegate (Graphics g)
            {
                using (SolidBrush br = new SolidBrush(c))
                {
                    g.FillEllipse(br, 6, 2, 8, 8);
                    g.FillEllipse(br, 2, 12, 16, 16);
                }
            });
        }

        public static Bitmap Kilit(Color c)
        {
            return Yeni(delegate (Graphics g)
            {
                using (SolidBrush br = new SolidBrush(c))
                using (Pen p = new Pen(c, 2F))
                {
                    g.DrawArc(p, 6, 2, 8, 10, 180, 180);
                    using (GraphicsPath yol = Cizim.Yuvarlak(new Rectangle(4, 9, 12, 9), 2)) g.FillPath(br, yol);
                }
            });
        }

        public static Bitmap OkAsagi(Color c)
        {
            return Yeni(delegate (Graphics g)
            {
                using (Pen p = new Pen(c, 2F))
                {
                    p.StartCap = LineCap.Round;
                    p.EndCap = LineCap.Round;
                    g.DrawLines(p, new Point[] { new Point(5, 8), new Point(10, 13), new Point(15, 8) });
                }
            });
        }

        public static Bitmap Ara(Color c)
        {
            return Yeni(delegate (Graphics g)
            {
                using (Pen p = new Pen(c, 2F))
                {
                    p.EndCap = LineCap.Round;
                    g.DrawEllipse(p, 3, 3, 10, 10);
                    g.DrawLine(p, 12, 12, 17, 17);
                }
            });
        }
    }

    public static class Cizim
    {
        public static GraphicsPath Yuvarlak(Rectangle r, int yaricap)
        {
            GraphicsPath p = new GraphicsPath();
            int d = yaricap * 2;
            if (yaricap <= 0 || r.Width < d || r.Height < d)
            {
                p.AddRectangle(r);
                return p;
            }
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }
    }

    /// <summary>Logoyu oranını bozmadan, yüksek kaliteyle çizen kontrol.</summary>
    [DesignerCategory("Code")]
    public class LogoKutusu : Control
    {
        public LogoKutusu(Color arkaPlan)
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = arkaPlan;
            TabStop = false;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(BackColor);
            Image img = Tema.Logo;
            if (img == null || ClientSize.Width < 4 || ClientSize.Height < 4) return;
            float oran = (float)img.Width / img.Height;
            float w = ClientSize.Width;
            float h = w / oran;
            if (h > ClientSize.Height) { h = ClientSize.Height; w = h * oran; }
            float x = (ClientSize.Width - w) / 2F;
            float y = (ClientSize.Height - h) / 2F;
            e.Graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            e.Graphics.SmoothingMode = SmoothingMode.HighQuality;
            e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            e.Graphics.DrawImage(img, x, y, w, h);
        }
    }

    /// <summary>Beyaz, yuvarlak köşeli logo plakası (koyu bantlarda logonun okunması için).</summary>
    public static class LogoPlaka
    {
        public static Guna2Panel Olustur(int genislik, int yukseklik)
        {
            Guna2Panel p = new Guna2Panel();
            p.FillColor = Color.White;
            p.BorderRadius = 12;
            p.BackColor = Color.Transparent;
            p.Size = new Size(genislik, yukseklik);
            p.Padding = new Padding(12, 6, 12, 6);
            LogoKutusu k = new LogoKutusu(Color.White);
            k.Dock = DockStyle.Fill;
            p.Controls.Add(k);
            return p;
        }
    }

    /// <summary>Metin kutusu + sağda açılır liste oku: serbest yazılabilir, listeden de seçilebilir.</summary>
    [DesignerCategory("Code")]
    public class OneriKutusu : Guna2TextBox
    {
        public readonly List<string> Secenekler = new List<string>();
        private readonly ContextMenuStrip _menu = new ContextMenuStrip();

        public OneriKutusu()
        {
            Tema.KutuStili(this);
            IconRight = Simge.OkAsagi(Tema.YaziSoluk);
            IconRightSize = new Size(16, 16);
            IconRightCursor = Cursors.Hand;
            IconRightClick += delegate { MenuAc(); };

            _menu.ShowImageMargin = false;
            _menu.Font = Tema.Normal;
            _menu.Renderer = new ToolStripProfessionalRenderer(new KoyuRenkler());
            _menu.BackColor = Color.FromArgb(30, 35, 68);
            _menu.ForeColor = Tema.Yazi;
        }

        public void SecenekleriYukle(IEnumerable<string> liste)
        {
            Secenekler.Clear();
            Secenekler.AddRange(liste);
            try
            {
                AutoCompleteStringCollection kaynak = new AutoCompleteStringCollection();
                kaynak.AddRange(Secenekler.ToArray());
                AutoCompleteCustomSource = kaynak;
                AutoCompleteSource = AutoCompleteSource.CustomSource;
                AutoCompleteMode = AutoCompleteMode.SuggestAppend;
            }
            catch { }
        }

        private void MenuAc()
        {
            _menu.Items.Clear();
            foreach (string s in Secenekler)
            {
                string deger = s;
                ToolStripMenuItem oge = new ToolStripMenuItem(deger);
                oge.Click += delegate { Text = deger; Focus(); };
                _menu.Items.Add(oge);
            }
            if (_menu.Items.Count == 0) return;
            _menu.MinimumSize = new Size(Width, 0);
            _menu.Show(this, new Point(0, Height + 2));
        }
    }

    /// <summary>Koyu açılır menü renkleri.</summary>
    public class KoyuRenkler : ProfessionalColorTable
    {
        public override Color ToolStripDropDownBackground { get { return Color.FromArgb(30, 35, 68); } }
        public override Color ImageMarginGradientBegin { get { return Color.FromArgb(30, 35, 68); } }
        public override Color ImageMarginGradientMiddle { get { return Color.FromArgb(30, 35, 68); } }
        public override Color ImageMarginGradientEnd { get { return Color.FromArgb(30, 35, 68); } }
        public override Color MenuBorder { get { return Color.FromArgb(62, 70, 120); } }
        public override Color MenuItemBorder { get { return Color.FromArgb(79, 70, 229); } }
        public override Color MenuItemSelected { get { return Color.FromArgb(79, 70, 229); } }
        public override Color MenuItemSelectedGradientBegin { get { return Color.FromArgb(79, 70, 229); } }
        public override Color MenuItemSelectedGradientEnd { get { return Color.FromArgb(124, 58, 237); } }
    }

    /// <summary>İstatistik ekranındaki renkli sayı kartı.</summary>
    [DesignerCategory("Code")]
    public class KpiKarti : Guna2GradientPanel
    {
        private readonly Label _baslik;
        private readonly Label _deger;

        public KpiKarti(string baslik, Color c1, Color c2)
        {
            FillColor = c1;
            FillColor2 = c2;
            GradientMode = LinearGradientMode.ForwardDiagonal;
            BorderRadius = 16;
            BackColor = Color.Transparent;
            Dock = DockStyle.Fill;
            Margin = new Padding(0, 0, 12, 0);
            Padding = new Padding(20, 12, 12, 8);

            _baslik = new Label { Text = baslik, Dock = DockStyle.Top, Height = 26, Font = Tema.Kalin, ForeColor = Color.FromArgb(235, 235, 255), BackColor = Color.Transparent };
            _deger = new Label { Text = "0", Dock = DockStyle.Fill, Font = Tema.Buyuk, ForeColor = Color.White, TextAlign = ContentAlignment.MiddleLeft, BackColor = Color.Transparent };
            Controls.Add(_deger);
            Controls.Add(_baslik);
        }

        public string Deger
        {
            get { return _deger.Text; }
            set { _deger.Text = value; }
        }
    }

    /// <summary>Sayı alanları için ortak yardımcılar: 30.5 ve 30,5 ikisi de ondalık; "30+15,5" Enter ile hesaplanır.</summary>
    public static class SayiAlani
    {
        public static double? Deger(Guna2TextBox t)
        {
            double d = 0;
            if (Sayi.Hesapla(t.Text, ref d)) return d;
            return null;
        }

        public static void Yaz(Guna2TextBox t, double? v)
        {
            t.Text = v.HasValue ? Sayi.Yaz(v.Value) : "";
        }

        public static bool Gecerli(Guna2TextBox t)
        {
            if (t.Text.Trim().Length == 0) return true;
            double d = 0;
            return Sayi.Hesapla(t.Text, ref d);
        }

        /// <summary>Enter: işlemi hesaplar (575+525 -> 1100) ve AYNI kutuda kalır, imleç sona gider ("/60" eklenebilir).</summary>
        public static void EnterBasildi(Guna2TextBox t)
        {
            double d = 0;
            if (t.Text.Trim().Length > 0 && Sayi.Hesapla(t.Text, ref d)) t.Text = Sayi.Yaz2(d);
            t.SelectionStart = t.Text.Length;
            t.SelectionLength = 0;
        }

        /// <summary>Kutudan çıkınca sonucu sade biçimde yazar.</summary>
        public static void KutudanCikildi(Guna2TextBox t)
        {
            double d = 0;
            if (t.Text.Trim().Length > 0 && Sayi.Hesapla(t.Text, ref d)) t.Text = Sayi.Yaz2(d);
        }
    }

    public static class Oturum
    {
        /// <summary>Oturum açmış kullanıcı (tüm formlardan erişilir).</summary>
        public static Kullanici Aktif;
    }
}
