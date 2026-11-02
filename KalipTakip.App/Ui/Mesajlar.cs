using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using Guna.UI2.WinForms;

namespace KalipTakip.Arayuz
{
    /// <summary>Tek tip, yuvarlak köşeli, renkli mesaj pencereleri (Windows MessageBox yerine).</summary>
    public static class Mesajlar
    {
        public static void Bilgi(string metin) { Goster(metin, BildirimTuru.Bilgi, false); }
        public static void Uyari(string metin) { Goster(metin, BildirimTuru.Uyari, false); }
        public static void Hata(string metin) { Goster(metin, BildirimTuru.Hata, false); }
        public static void Basari(string metin) { Goster(metin, BildirimTuru.Basari, false); }
        public static bool Soru(string metin) { return Goster(metin, BildirimTuru.Bilgi, true) == DialogResult.Yes; }

        private static DialogResult Goster(string metin, BildirimTuru tur, bool evetHayir)
        {
            using (MesajForm f = new MesajForm(metin, tur, evetHayir))
            {
                Form sahip = Form.ActiveForm;
                return sahip != null && sahip.Visible ? f.ShowDialog(sahip) : f.ShowDialog();
            }
        }
    }

    [DesignerCategory("Code")]
    internal class MesajForm : Form
    {
        private readonly string _metin;
        private readonly BildirimTuru _tur;
        private readonly Font _baslikFont = new Font("Segoe UI Semibold", 12F);
        private readonly Font _glifFont = new Font("Segoe UI Symbol", 17F, FontStyle.Bold);
        private readonly Rectangle _metinAlani;
        private readonly bool _evetHayir;
        private readonly Guna2BorderlessForm _cerceve = new Guna2BorderlessForm();

        public MesajForm(string metin, BildirimTuru tur, bool evetHayir)
        {
            _metin = metin;
            _tur = tur;
            _evetHayir = evetHayir;

            AutoScaleMode = AutoScaleMode.None;
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterParent;
            ShowInTaskbar = false;
            BackColor = Tema.Kart;
            Font = Tema.Normal;
            DoubleBuffered = true;
            if (Tema.Ikon != null) Icon = Tema.Ikon;
            Text = "Kalıp Takip Sistemi";

            const int genislik = 500;
            const int solBosluk = 96;
            Size olcu = TextRenderer.MeasureText(metin, Tema.Normal, new Size(genislik - solBosluk - 28, 0), TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);
            int metinYuksekligi = Math.Max(olcu.Height, 40);
            _metinAlani = new Rectangle(solBosluk, 62, genislik - solBosluk - 28, metinYuksekligi);
            ClientSize = new Size(genislik, 62 + metinYuksekligi + 84);

            _cerceve.ContainerControl = this;
            _cerceve.BorderRadius = 18;
            _cerceve.HasFormShadow = true;
            _cerceve.ShadowColor = Color.Black;
            _cerceve.DragForm = false;

            Guna2DragControl d = new Guna2DragControl();
            d.TargetControl = this;
            d.UseTransparentDrag = true;
            d.TransparentWhileDrag = true;
            d.DragStartTransparencyValue = 0.75;
            d.DragEndTransparencyValue = 1.0;

            int y = ClientSize.Height - 62;
            if (evetHayir)
            {
                Guna2GradientButton evet = Tema.Buton("Evet", Tema.Yesil, Color.FromArgb(16, 185, 129), 120, 40);
                Guna2GradientButton hayir = Tema.Buton("Hayır", Tema.Gri1, Tema.Gri2, 120, 40);
                hayir.Location = new Point(ClientSize.Width - 28 - 120, y);
                evet.Location = new Point(hayir.Left - 12 - 120, y);
                evet.Click += delegate { DialogResult = DialogResult.Yes; };
                hayir.Click += delegate { DialogResult = DialogResult.No; };
                Controls.Add(evet);
                Controls.Add(hayir);
            }
            else
            {
                Guna2GradientButton tamam = Tema.Buton("Tamam", Tema.Mavi, Tema.Mor, 130, 40);
                tamam.Location = new Point(ClientSize.Width - 28 - 130, y);
                tamam.Click += delegate { DialogResult = DialogResult.OK; };
                Controls.Add(tamam);
            }
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Escape)
            {
                DialogResult = _evetHayir ? DialogResult.No : DialogResult.OK;
                return true;
            }
            if (keyData == Keys.Enter && !_evetHayir)
            {
                DialogResult = DialogResult.OK;
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Color renk = Bildirim.TurRengi(_tur);

            using (LinearGradientBrush br = new LinearGradientBrush(new Rectangle(0, 0, Width, 8), renk, Tema.Acik(renk, -60), 0F))
            {
                g.FillRectangle(br, 0, 0, Width, 8);
            }
            Rectangle daire = new Rectangle(28, 62, 48, 48);
            using (SolidBrush b = new SolidBrush(renk)) { g.FillEllipse(b, daire); }
            Color glif = (_tur == BildirimTuru.Basari || _tur == BildirimTuru.Uyari) ? Color.FromArgb(20, 28, 40) : Color.White;
            TextRenderer.DrawText(g, _evetHayir ? "?" : Bildirim.Glif(_tur), _glifFont, daire, glif,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);

            TextRenderer.DrawText(g, "Kalıp Takip Sistemi", _baslikFont, new Rectangle(28, 20, Width - 56, 30), Tema.YaziSoluk,
                TextFormatFlags.Left | TextFormatFlags.NoPrefix);
            TextRenderer.DrawText(g, _metin, Tema.Normal, _metinAlani, Tema.Yazi, TextFormatFlags.Left | TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) { _baslikFont.Dispose(); _glifFont.Dispose(); }
            base.Dispose(disposing);
        }
    }
}
