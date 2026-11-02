using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace KalipTakip.Arayuz
{
    public enum BildirimTuru { Basari, Bilgi, Uyari, Hata }

    /// <summary>
    /// İşlem sonrası ekranın üst ortasında görünen renkli bildirim ("Kayıt güncellendi" gibi).
    /// Kendiliğinden kaybolur, tıklayınca hemen kapanır; odağı almaz (yazmayı bölmez).
    /// </summary>
    [DesignerCategory("Code")]
    public class Bildirim : Form
    {
        private const int GosterimMs = 3800;
        private static Bildirim _aktif;

        private readonly BildirimTuru _tur;
        private readonly string _baslik;
        private readonly string _metin;
        private readonly float _olcek;
        private readonly Timer _zaman = new Timer();
        private readonly Font _baslikFont = new Font("Segoe UI Semibold", 11F);
        private readonly Font _metinFont = new Font("Segoe UI", 9.5F);
        private readonly Font _glifFont = new Font("Segoe UI Symbol", 13F, FontStyle.Bold);
        private int _bekleme;
        private bool _sonlaniyor;

        public static void Goster(Form sahip, BildirimTuru tur, string baslik, string metin)
        {
            if (sahip == null || sahip.IsDisposed || !sahip.Visible) return;
            if (_aktif != null && !_aktif.IsDisposed) _aktif.Close();
            float olcek = sahip.DeviceDpi / 96F;
            Bildirim b = new Bildirim(tur, baslik, metin, olcek);
            _aktif = b;
            b.Location = sahip.PointToScreen(new Point((sahip.ClientSize.Width - b.Width) / 2, (int)(78 * olcek)));
            b.Show(sahip);
        }

        private Bildirim(BildirimTuru tur, string baslik, string metin, float olcek)
        {
            _tur = tur;
            _baslik = baslik;
            _metin = metin;
            _olcek = olcek;

            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            DoubleBuffered = true;
            BackColor = Color.FromArgb(26, 30, 60);
            Cursor = Cursors.Hand;

            int h = TextRenderer.MeasureText(metin, _metinFont, new Size(Px(330), 0), TextFormatFlags.WordBreak).Height;
            ClientSize = new Size(Px(430), Math.Max(Px(80), Px(48) + h + Px(14)));
            using (GraphicsPath yol = Cizim.Yuvarlak(new Rectangle(0, 0, Width, Height), Px(16)))
            {
                Region = new Region(yol);
            }

            _zaman.Interval = 30;
            _zaman.Tick += ZamanTick;
        }

        private int Px(int v) { return (int)Math.Round(v * _olcek); }

        protected override bool ShowWithoutActivation { get { return true; } }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= 0x8000000 | 0x80;   // WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW
                return cp;
            }
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            _zaman.Start();
        }

        private void ZamanTick(object sender, EventArgs e)
        {
            if (!_sonlaniyor)
            {
                _bekleme += _zaman.Interval;
                if (_bekleme >= GosterimMs) _sonlaniyor = true;
            }
            else if (Opacity > 0.1)
            {
                Opacity -= 0.08;
            }
            else
            {
                _zaman.Stop();
                Close();
            }
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            Close();
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            _zaman.Stop();
            _zaman.Dispose();
            _baslikFont.Dispose();
            _metinFont.Dispose();
            _glifFont.Dispose();
            if (ReferenceEquals(_aktif, this)) _aktif = null;
            base.OnFormClosed(e);
        }

        public static Color TurRengi(BildirimTuru tur)
        {
            switch (tur)
            {
                case BildirimTuru.Basari: return Tema.Yesil;
                case BildirimTuru.Uyari: return Tema.Sari;
                case BildirimTuru.Hata: return Tema.Kirmizi;
                default: return Tema.Mavi;
            }
        }

        public static string Glif(BildirimTuru tur)
        {
            switch (tur)
            {
                case BildirimTuru.Basari: return "✓";
                case BildirimTuru.Uyari: return "!";
                case BildirimTuru.Hata: return "✕";
                default: return "i";
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(BackColor);
            Color renk = TurRengi(_tur);

            using (LinearGradientBrush lb = new LinearGradientBrush(new Rectangle(0, 0, Width, Height),
                Color.FromArgb(34, 38, 78), Color.FromArgb(22, 26, 52), 0F))
            {
                g.FillRectangle(lb, 0, 0, Width, Height);
            }
            using (SolidBrush b = new SolidBrush(renk))
            {
                g.FillRectangle(b, 0, 0, Px(7), Height);
            }

            int d = Px(38);
            Rectangle ikon = new Rectangle(Px(24), (Height - d) / 2, d, d);
            using (SolidBrush b = new SolidBrush(renk)) { g.FillEllipse(b, ikon); }
            Color glifRengi = (_tur == BildirimTuru.Basari || _tur == BildirimTuru.Uyari) ? Color.FromArgb(20, 28, 40) : Color.White;
            TextRenderer.DrawText(g, Glif(_tur), _glifFont, ikon, glifRengi,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);

            TextRenderer.DrawText(g, _baslik, _baslikFont, new Rectangle(Px(76), Px(12), Width - Px(90), Px(26)), Color.White,
                TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
            TextRenderer.DrawText(g, _metin, _metinFont, new Rectangle(Px(76), Px(40), Width - Px(90), Height - Px(48)), Color.FromArgb(204, 210, 236),
                TextFormatFlags.Left | TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);

            using (GraphicsPath yol = Cizim.Yuvarlak(new Rectangle(0, 0, Width - 1, Height - 1), Px(16)))
            using (Pen pn = new Pen(Color.FromArgb(96, 104, 160)))
            {
                g.DrawPath(pn, yol);
            }
        }
    }
}
