using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using Guna.UI2.WinForms;

namespace KalipTakip.Arayuz
{
    /// <summary>
    /// Tüm pencerelerin tabanı: çerçevesiz, yuvarlak köşeli, gölgeli; üst banttan tutup sürüklerken yarı saydam olur.
    /// Türetilen form içeriğini <see cref="Govde"/> paneline ekler.
    /// </summary>
    [DesignerCategory("Code")]
    public class TabanForm : Form
    {
        private readonly Guna2BorderlessForm _cerceve = new Guna2BorderlessForm();
        protected readonly Guna2GradientPanel UstBant = new Guna2GradientPanel();
        protected readonly Panel Govde = new Panel();
        protected readonly Label LblBaslik;
        protected readonly Label LblAlt;
        protected readonly FlowLayoutPanel UstSag = new FlowLayoutPanel();
        private readonly Guna2Panel _logo;
        private readonly Guna2ControlBox _kapat = new Guna2ControlBox();
        private readonly Guna2ControlBox _buyut = new Guna2ControlBox();
        private readonly Guna2ControlBox _kucult = new Guna2ControlBox();
        private readonly Timer _belirme = new Timer();
        private readonly bool _boyutlanabilir;
        private const int KoseYaricapi = 18;
        private const int BantYuksekligi = 66;

        public TabanForm(string baslik, string alt, Size istemci, bool boyutlanabilir)
        {
            _boyutlanabilir = boyutlanabilir;
            SuspendLayout();
            AutoScaleMode = AutoScaleMode.None;
            Font = Tema.Normal;
            BackColor = Tema.Zemin;
            ForeColor = Tema.Yazi;
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = istemci;
            Text = baslik;
            DoubleBuffered = true;
            Opacity = 0;
            if (Tema.Ikon != null) Icon = Tema.Ikon;

            // Çerçevesiz pencere: yuvarlak köşe + gölge (+ isteğe bağlı kenardan boyutlandırma)
            _cerceve.ContainerControl = this;
            _cerceve.BorderRadius = KoseYaricapi;
            _cerceve.HasFormShadow = true;
            _cerceve.ShadowColor = Color.Black;
            _cerceve.DragForm = false;
            _cerceve.ResizeForm = boyutlanabilir;

            // Üst bant
            UstBant.Dock = DockStyle.Top;
            UstBant.Height = BantYuksekligi;
            UstBant.FillColor = Tema.BantSol;
            UstBant.FillColor2 = Tema.BantSag;
            UstBant.GradientMode = LinearGradientMode.Horizontal;
            UstBant.BackColor = Color.Transparent;

            _logo = LogoPlaka.Olustur(190, 46);
            _logo.Location = new Point(14, 10);

            LblBaslik = Tema.Etiket(baslik, Tema.Baslik, Color.White);
            LblBaslik.Location = new Point(222, 8);
            LblAlt = Tema.Etiket(alt ?? "", Tema.Kucuk, Color.FromArgb(214, 206, 248));
            LblAlt.Location = new Point(224, 40);

            UstSag.AutoSize = true;
            UstSag.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            UstSag.WrapContents = false;
            UstSag.FlowDirection = FlowDirection.LeftToRight;
            UstSag.BackColor = Color.Transparent;
            UstSag.Anchor = AnchorStyles.Top | AnchorStyles.Right;

            KutuHazirla(_kucult, Guna.UI2.WinForms.Enums.ControlBoxType.MinimizeBox);
            KutuHazirla(_buyut, Guna.UI2.WinForms.Enums.ControlBoxType.MaximizeBox);
            KutuHazirla(_kapat, Guna.UI2.WinForms.Enums.ControlBoxType.CloseBox);
            _kapat.HoverState.FillColor = Tema.Kirmizi;
            _kapat.HoverState.IconColor = Color.White;

            UstBant.Controls.Add(_logo);
            UstBant.Controls.Add(LblBaslik);
            UstBant.Controls.Add(LblAlt);
            UstBant.Controls.Add(UstSag);
            UstBant.Controls.Add(_kucult);
            UstBant.Controls.Add(_buyut);
            UstBant.Controls.Add(_kapat);

            Govde.Dock = DockStyle.Fill;
            Govde.BackColor = Color.Transparent;
            Controls.Add(Govde);
            Controls.Add(UstBant);

            UstBant.SizeChanged += delegate { BantYerlestir(); };
            UstSag.SizeChanged += delegate { BantYerlestir(); };

            // Üst banttan (ve üzerindeki yazı/logodan) tutup sürükleyince pencere yarı saydamlaşır
            SurukleBagla(UstBant);
            SurukleBagla(LblBaslik);
            SurukleBagla(LblAlt);
            SurukleBagla(_logo);

            _belirme.Interval = 15;
            _belirme.Tick += BelirmeAdimi;

            ResumeLayout(false);
            BantYerlestir();
        }

        private void KutuHazirla(Guna2ControlBox k, Guna.UI2.WinForms.Enums.ControlBoxType tur)
        {
            k.ControlBoxType = tur;
            k.FillColor = Color.Transparent;
            k.IconColor = Color.White;
            k.Size = new Size(46, 40);
            k.BackColor = Color.Transparent;
            k.HoverState.FillColor = Color.FromArgb(255, 110, 80, 200);
            k.HoverState.IconColor = Color.White;
            k.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        }

        /// <summary>Verilen kontrolden tutulup sürüklenince formu taşır; sürüklerken yarı saydamlaşır.</summary>
        protected void SurukleBagla(Control hedef)
        {
            Guna2DragControl d = new Guna2DragControl();
            d.TargetControl = hedef;
            d.UseTransparentDrag = true;
            d.TransparentWhileDrag = true;
            d.DragStartTransparencyValue = 0.72;
            d.DragEndTransparencyValue = 1.0;
        }

        /// <summary>Üst bandın sağ tarafını (ek düğmeler + pencere düğmeleri) yerleştirir.</summary>
        private void BantYerlestir()
        {
            int w = UstBant.ClientSize.Width;
            int sag = w - 8;
            bool tam = _boyutlanabilir;
            _buyut.Visible = tam;
            _kucult.Visible = tam;
            Control[] kutular = tam ? new Control[] { _kapat, _buyut, _kucult } : new Control[] { _kapat };
            foreach (Control k in kutular)
            {
                sag -= k.Width;
                k.Location = new Point(sag, 4);
            }
            UstSag.Location = new Point(Math.Max(LblBaslik.Right + 20, sag - UstSag.Width - 12), (BantYuksekligi - UstSag.Height) / 2);
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            try { MaximizedBounds = Screen.FromControl(this).WorkingArea; } catch { }
        }

        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);
            try { _cerceve.BorderRadius = WindowState == FormWindowState.Maximized ? 0 : KoseYaricapi; } catch { }
        }

        // ------------------------------------------------ yumuşak belirme (açılışta)
        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            _belirme.Start();
        }

        private void BelirmeAdimi(object sender, EventArgs e)
        {
            double o = Opacity + 0.12;
            if (o >= 1.0) { Opacity = 1.0; _belirme.Stop(); }
            else Opacity = o;
        }

        // ------------------------------------------------ zemin: degrade + yumuşak daireler
        protected override void OnPaintBackground(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            Rectangle r = ClientRectangle;
            if (r.Width < 4 || r.Height < 4) return;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (LinearGradientBrush br = new LinearGradientBrush(r, Color.FromArgb(24, 16, 70), Tema.Zemin, 62F))
            {
                g.FillRectangle(br, r);
            }
            using (SolidBrush b1 = new SolidBrush(Color.FromArgb(34, 126, 182, 45)))
            {
                g.FillEllipse(b1, -160, r.Height - 300, 460, 460);
            }
            using (SolidBrush b2 = new SolidBrush(Color.FromArgb(36, 59, 130, 246)))
            {
                g.FillEllipse(b2, r.Width - 380, -190, 520, 520);
            }
            using (SolidBrush b3 = new SolidBrush(Color.FromArgb(22, 236, 72, 153)))
            {
                g.FillEllipse(b3, r.Width - 260, r.Height - 240, 360, 360);
            }
        }

        // ------------------------------------------------ yardımcılar (alt formlar kullanır)

        /// <summary>Odaktaki en iç kontrolü bulur (UserControl içindeki gerçek TextBox dahil).</summary>
        protected Control OdaktakiKontrol()
        {
            Control k = ActiveControl;
            ContainerControl cc = k as ContainerControl;
            while (cc != null && cc.ActiveControl != null)
            {
                k = cc.ActiveControl;
                cc = k as ContainerControl;
            }
            return k;
        }

        /// <summary>Odak bir Guna2TextBox'un iç metin kutusundaysa, o Guna2TextBox'u döner.</summary>
        protected Guna2TextBox OdaktakiGunaKutusu()
        {
            Control k = OdaktakiKontrol();
            while (k != null)
            {
                Guna2TextBox t = k as Guna2TextBox;
                if (t != null) return t;
                k = k.Parent;
            }
            return null;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) { _belirme.Dispose(); }
            base.Dispose(disposing);
        }
    }
}
