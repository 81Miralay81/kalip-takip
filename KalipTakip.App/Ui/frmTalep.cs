using System;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Guna.UI2.WinForms;

namespace KalipTakip.Arayuz
{
    /// <summary>
    /// Talep portalı: "Talep" sekmesi (kalıp kodu/barkod ile sorgula, talep aç/güncelle) ve
    /// "Talepler" sekmesi (tüm talepleri filtreleyip listele, Excel'e aktar).
    /// </summary>
    [DesignerCategory("Code")]
    public class frmTalep : TabanForm
    {
        // Sekmeler
        private Panel pnlTalep, pnlListe;
        private Guna2Button btnSekmeTalep, btnSekmeListe;
        private Color _sekmePasif, _sekmeAktif = Color.FromArgb(124, 58, 237);
        private bool _listeYuklendi;

        // Talep sekmesi
        private Guna2TextBox txtTalepNoAra;
        private Label lblSagBaslik;
        private Guna2DataGridView gridSag;
        private DataTable _tabloSag;

        // Kalıp kodu yazdıkça açılan öneri penceresi
        private ToolStripDropDown _oneriPopup;
        private ListBox _oneriListe;
        private readonly System.Collections.Generic.List<string> _oneriKodlar = new System.Collections.Generic.List<string>();
        private readonly Timer _oneriZaman = new Timer();

        // Talep formu
        private Label lblMod, lblMeta, lblKalipBilgi;
        private Guna2TextBox txtTalepNo, txtTalepEden, txtAciklama, txtSonuc;
        // Kalıp satırları
        private Guna2TextBox txtSatirKod, txtSatirAciklama;
        private OneriKutusu txtSatirLok, txtSahip;
        private Guna2ComboBox cmbSatirStatu;
        private Guna2DataGridView gridSatir;
        private DataTable _satirlar;
        private bool _satirYukleniyor;
        private bool DurumYetkisi { get { return Oturum.Aktif.IzinVar(Izin.TalepKalipDurum); } }
        private OneriKutusu cmbBolum;
        private Guna2ComboBox cmbOncelik, cmbStatu;
        private Guna2DateTimePicker dtTalep, dtTermin, dtTamam;
        private Guna2GradientButton btnAc, btnGuncelle, btnSil;
        private Talep _mevcut;
        private bool _yukleniyor;

        // Talepler sekmesi
        private Guna2TextBox txtAra;
        private Guna2ComboBox cmbStatuF, cmbBolumF, cmbLimit;
        private Guna2DateTimePicker dtBas, dtBit;
        private Guna2DataGridView gridListe;
        private DataTable _tabloListe;
        private Label lblListeSayi;

        private readonly ToolTip _ipucu = new ToolTip();

        public frmTalep()
            : base("Talep", "Kalıp talepleri: aç, takip et, listele", new Size(1500, 880), true)
        {
            SuspendLayout();
            MinimumSize = new Size(1240, 760);
            StartPosition = FormStartPosition.CenterScreen;
            KeyPreview = true;

            UstBandiKur();

            pnlTalep = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent };
            pnlListe = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent, Visible = false };
            Govde.Controls.Add(pnlTalep);
            Govde.Controls.Add(pnlListe);
            TalepSekmesiKur();
            ListeSekmesiKur();

            ResumeLayout(false);
            Shown += delegate { Baslangic(); };
        }

        // =========================================================== ÜST BANT / SEKMELER

        private void UstBandiKur()
        {
            btnSekmeTalep = Tema.BantButonu("Talep", 100);
            btnSekmeListe = Tema.BantButonu("Talepler", 110);
            btnSekmeListe.Visible = Oturum.Aktif.IsAdmin || Oturum.Aktif.Misafir || Oturum.Aktif.IzinVar(Izin.TalepListe);
            Guna2Button btnGeri = Tema.BantButonu("Portala Dön", 118);
            btnGeri.FillColor = Color.FromArgb(150, 40, 80);
            btnGeri.HoverState.FillColor = Tema.Kirmizi;
            btnGeri.Margin = new Padding(0);
            _sekmePasif = btnSekmeTalep.FillColor;

            btnSekmeTalep.Click += delegate { SekmeAc(true); };
            btnSekmeListe.Click += delegate { SekmeAc(false); };
            btnGeri.Click += delegate { Close(); };

            UstSag.Controls.Add(btnSekmeTalep);
            UstSag.Controls.Add(btnSekmeListe);
            UstSag.Controls.Add(btnGeri);
            SekmeRenkleri(true);
        }

        private void SekmeRenkleri(bool talep)
        {
            btnSekmeTalep.FillColor = talep ? _sekmeAktif : _sekmePasif;
            btnSekmeListe.FillColor = talep ? _sekmePasif : _sekmeAktif;
        }

        private void SekmeAc(bool talep)
        {
            pnlTalep.Visible = talep;
            pnlListe.Visible = !talep;
            SekmeRenkleri(talep);
            if (talep) txtSatirKod.Focus();
            else
            {
                if (!_listeYuklendi) ListeYenile(false);
                txtAra.Focus();
            }
        }

        private void Baslangic()
        {
            OneriKur();
            FormuTemizle();
            SagYenile();
            txtSatirKod.Focus();
        }

        // =========================================================== TALEP SEKMESİ

        private void TalepSekmesiKur()
        {
            TableLayoutPanel kok = new TableLayoutPanel();
            kok.Dock = DockStyle.Fill;
            kok.ColumnCount = 2;
            kok.RowCount = 1;
            kok.BackColor = Color.Transparent;
            kok.Padding = new Padding(14, 12, 14, 14);
            kok.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            kok.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 430F));
            kok.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            kok.Controls.Add(SolKolonKur(), 0, 0);
            kok.Controls.Add(SagKartKur(), 1, 0);
            pnlTalep.Controls.Add(kok);
        }

        private Control SolKolonKur()
        {
            Control form = FormKartiKur();
            form.Margin = new Padding(0, 0, 12, 0);
            return form;
        }

        private Control SagKartKur()
        {
            Guna2Panel kart = Tema.KartPaneli();
            kart.Dock = DockStyle.Fill;
            kart.Margin = new Padding(0);
            kart.Padding = new Padding(10);

            Panel ust = new Panel { Dock = DockStyle.Top, Height = 92, BackColor = Color.Transparent };
            lblSagBaslik = Tema.Etiket("Talepler", Tema.Orta, Tema.Yazi);
            lblSagBaslik.Location = new Point(8, 4);
            txtTalepNoAra = Tema.Kutu(60);
            txtTalepNoAra.SetBounds(6, 42, 270, 38);
            txtTalepNoAra.PlaceholderText = "Talep no ara...";
            Guna2GradientButton btnAra = Tema.Buton("Ara", Tema.Mavi, Tema.Mor, 90, 38);
            btnAra.Location = new Point(286, 42);
            btnAra.Click += delegate { SagYenile(); };
            ust.Controls.Add(lblSagBaslik);
            ust.Controls.Add(txtTalepNoAra);
            ust.Controls.Add(btnAra);

            gridSag = Tema.Tablo();
            gridSag.Dock = DockStyle.Fill;
            KolonEkle(gridSag, "TalepNo", "Talep No", 115, KolonTipi.Metin);
            KolonEkle(gridSag, "Bolum", "Talep Bölüm", 120, KolonTipi.Metin);
            KolonEkle(gridSag, "Statu", "Talep Statü", 100, KolonTipi.Metin);
            KolonEkle(gridSag, "TalepTarihi", "Talep Tarih", 100, KolonTipi.Tarih);
            gridSag.SelectionChanged += delegate { GridSecildi(gridSag); };
            gridSag.CellFormatting += GridBicim;

            kart.Controls.Add(gridSag);
            kart.Controls.Add(ust);
            return kart;
        }

        private Control FormKartiKur()
        {
            Guna2Panel kart = Tema.KartPaneli();
            kart.Dock = DockStyle.Fill;
            kart.Margin = new Padding(0);
            kart.Padding = new Padding(14, 8, 14, 8);

            TableLayoutPanel kok = new TableLayoutPanel();
            kok.Dock = DockStyle.Fill;
            kok.ColumnCount = 1;
            kok.RowCount = 7;
            kok.BackColor = Color.Transparent;
            kok.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            kok.RowStyles.Add(new RowStyle(SizeType.Absolute, 52F));    // başlık
            kok.RowStyles.Add(new RowStyle(SizeType.Absolute, 68F));    // talep no / bölüm / öncelik / talep eden
            kok.RowStyles.Add(new RowStyle(SizeType.Absolute, 68F));    // tarihler / statü
            kok.RowStyles.Add(new RowStyle(SizeType.Absolute, 74F));    // kalıp arama
            kok.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));    // kalıp listesi
            kok.RowStyles.Add(new RowStyle(SizeType.Absolute, 92F));    // seçili kalıp bilgileri
            kok.RowStyles.Add(new RowStyle(SizeType.Absolute, 56F));    // düğmeler

            Panel ust = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent };
            lblMod = Tema.Etiket("Yeni talep", Tema.Orta, Tema.Yazi);
            lblMod.Location = new Point(2, 0);
            lblMeta = Tema.Etiket("", Tema.Kucuk, Tema.YaziSoluk);
            lblMeta.Location = new Point(3, 28);
            ust.Controls.Add(lblMod);
            ust.Controls.Add(lblMeta);

            txtTalepNo = Tema.Kutu();
            Tema.SaltOkunurStili(txtTalepNo);
            cmbBolum = new OneriKutusu { MaxLength = 60 };
            cmbOncelik = Tema.Acilir(100);
            cmbOncelik.Items.AddRange(TalepRepository.Oncelikler);
            cmbStatu = Tema.Acilir(100);
            cmbStatu.Items.AddRange(TalepRepository.Statuler);
            dtTalep = Tema.Tarih();
            dtTermin = Tema.Tarih();
            dtTamam = Tema.Tarih();
            dtTamam.Enabled = false;
            txtTalepEden = Tema.Kutu(60);
            txtAciklama = Tema.Kutu(1000);
            txtAciklama.Multiline = true;
            txtAciklama.Height = 66;
            txtSonuc = Tema.Kutu(1000);
            txtSonuc.Multiline = true;
            txtSonuc.Height = 66;

            // Kalıp satırları
            txtSatirKod = Tema.Kutu(60);
            txtSatirKod.Dock = DockStyle.Fill;
            txtSatirKod.Margin = new Padding(0, 0, 8, 0);
            txtSatirKod.PlaceholderText = "Kalıp kodu / adı yazın ya da barkod okutun, Enter";
            txtSatirKod.IconLeft = Simge.Ara(Tema.YaziSoluk);
            txtSatirKod.IconLeftSize = new Size(18, 18);
            Guna2GradientButton btnSatirEkle = Tema.Buton("Kalıbı Ekle", Tema.Yesil, Color.FromArgb(16, 185, 129), 130, 38);
            Guna2GradientButton btnSatirCikar = Tema.Buton("Seçiliyi Çıkar", Tema.Kirmizi, Color.FromArgb(190, 24, 93), 140, 38);
            btnSatirEkle.Margin = new Padding(0, 0, 8, 0);
            btnSatirCikar.Margin = new Padding(0, 0, 12, 0);
            btnSatirEkle.Click += delegate { SatirEkle(txtSatirKod.Text); };
            btnSatirCikar.Click += delegate { SatirCikar(); };
            lblKalipBilgi = Tema.Etiket("", Tema.Kucuk, Tema.YaziSoluk);
            lblKalipBilgi.AutoSize = false;
            lblKalipBilgi.Dock = DockStyle.Fill;
            lblKalipBilgi.TextAlign = ContentAlignment.MiddleLeft;
            TableLayoutPanel satirSatiri = new TableLayoutPanel();
            satirSatiri.Height = 44;
            satirSatiri.ColumnCount = 4;
            satirSatiri.RowCount = 1;
            satirSatiri.BackColor = Color.Transparent;
            satirSatiri.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 38F));
            satirSatiri.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140F));
            satirSatiri.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160F));
            satirSatiri.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 62F));
            satirSatiri.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            satirSatiri.Controls.Add(txtSatirKod, 0, 0);
            satirSatiri.Controls.Add(btnSatirEkle, 1, 0);
            satirSatiri.Controls.Add(btnSatirCikar, 2, 0);
            satirSatiri.Controls.Add(lblKalipBilgi, 3, 0);

            _satirlar = new DataTable();
            _satirlar.Columns.Add("SatirId", typeof(int));
            _satirlar.Columns.Add("KalipKodu", typeof(string));
            _satirlar.Columns.Add("KalipStatu", typeof(string));
            _satirlar.Columns.Add("Lokasyon", typeof(string));
            _satirlar.Columns.Add("Aciklama", typeof(string));
            _satirlar.Columns.Add("TalepSira", typeof(int));
            _satirlar.Columns.Add("TashihSayisi", typeof(int));
            _satirlar.Columns.Add("SahipBolum", typeof(string));
            gridSatir = Tema.Tablo();
            gridSatir.Dock = DockStyle.Fill;
            KolonEkle(gridSatir, "KalipKodu", "Kalıp Kodu", 130, KolonTipi.Metin);
            KolonEkle(gridSatir, "TalepSira", "Kaçıncı Talep", 85, KolonTipi.Tam);
            KolonEkle(gridSatir, "TashihSayisi", "Tashih", 60, KolonTipi.Tam);
            KolonEkle(gridSatir, "KalipStatu", "Kalıp Statü", 120, KolonTipi.Metin);
            KolonEkle(gridSatir, "Lokasyon", "Lokasyon", 120, KolonTipi.Metin);
            KolonEkle(gridSatir, "SahipBolum", "Sahip Bölüm", 110, KolonTipi.Metin);
            KolonEkle(gridSatir, "Aciklama", "Kalıba Özel Açıklama", 300, KolonTipi.Metin);
            gridSatir.DataSource = _satirlar.DefaultView;
            gridSatir.SelectionChanged += delegate { SatirSecildi(); };
            gridSatir.CellFormatting += GridBicim;

            txtSatirAciklama = Tema.Kutu(1000);
            txtSatirAciklama.Multiline = true;
            txtSatirAciklama.Height = 66;
            txtSatirAciklama.PlaceholderText = "Bu kalıp için açıklama (kalıpta kalır)";
            cmbSatirStatu = Tema.Acilir(100);
            cmbSatirStatu.Items.AddRange(TalepKurallari.KalipStatuleri);
            txtSatirLok = new OneriKutusu { MaxLength = 60 };
            txtSahip = new OneriKutusu { MaxLength = 60 };
            try
            {
                cmbBolum.SecenekleriYukle(LokasyonRepository.Bolumler());
                txtSahip.SecenekleriYukle(LokasyonRepository.Bolumler());
                txtSatirLok.SecenekleriYukle(LokasyonRepository.Adlar());
            }
            catch { }
            txtSahip.TextChanged += delegate { SatirDegeriYaz("SahipBolum", txtSahip.Text); };
            cmbBolum.TextChanged += delegate { YeniSatirLokasyonlariniYenile(); };
            if (!string.IsNullOrWhiteSpace(Oturum.Aktif.Bolum) && !Oturum.Aktif.IsAdmin) cmbBolum.Enabled = false;
            txtSatirAciklama.TextChanged += delegate { SatirDegeriYaz("Aciklama", txtSatirAciklama.Text); };
            txtSatirLok.TextChanged += delegate { SatirDegeriYaz("Lokasyon", txtSatirLok.Text); };
            cmbSatirStatu.SelectedIndexChanged += delegate { SatirStatuDegisti(); };
            cmbSatirStatu.Enabled = DurumYetkisi;
            txtSatirLok.Enabled = DurumYetkisi;
            txtSahip.Enabled = DurumYetkisi;
            _ipucu.SetToolTip(cmbSatirStatu, "Kalıp statüsünü değiştirir; lokasyon otomatik güncellenir (yetki gerekir).");
            _ipucu.SetToolTip(txtSatirLok, "Otomatik lokasyonu elle değiştirebilirsiniz (ör. raf yeri) (yetki gerekir).");

            Panel gridPanel = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent, Margin = new Padding(0, 0, 0, 4) };
            Label gridEtiket = new Label { Text = "Talepteki kalıplar", Dock = DockStyle.Top, Height = 22, Font = Tema.Kucuk, ForeColor = Color.FromArgb(190, 198, 232), BackColor = Color.Transparent };
            gridPanel.Controls.Add(gridSatir);
            gridPanel.Controls.Add(gridEtiket);

            TableLayoutPanel sira1 = Sutunlar(25, 25, 25, 25);
            sira1.Controls.Add(Alan("Talep No (otomatik)", txtTalepNo), 0, 0);
            sira1.Controls.Add(Alan("İlgili Bölüm *", cmbBolum), 1, 0);
            sira1.Controls.Add(Alan("Öncelik", cmbOncelik), 2, 0);
            sira1.Controls.Add(Alan("Talep Eden", txtTalepEden), 3, 0);
            TableLayoutPanel sira2 = Sutunlar(25, 25, 25, 25);
            sira2.Controls.Add(Alan("Talep Tarihi", dtTalep), 0, 0);
            sira2.Controls.Add(Alan("Termin Tarihi", dtTermin), 1, 0);
            sira2.Controls.Add(Alan("Talep Statüsü (otomatik)", cmbStatu), 2, 0);
            sira2.Controls.Add(Alan("Kapanış Tarihi (otomatik)", dtTamam), 3, 0);
            Control kalipAra = Alan("Kalıp ekle  (yazdıkça liste açılır; barkod okutup Enter)", satirSatiri, 74);
            TableLayoutPanel sira3 = Sutunlar(40, 20, 20, 20);
            sira3.Controls.Add(Alan("Seçili kalıp için açıklama (kalıpta kalır)", txtSatirAciklama, 92), 0, 0);
            sira3.Controls.Add(Alan("Seçili kalıp statüsü", cmbSatirStatu), 1, 0);
            sira3.Controls.Add(Alan("Seçili kalıp lokasyonu", txtSatirLok), 2, 0);
            sira3.Controls.Add(Alan("Kalıp sahibi bölüm", txtSahip), 3, 0);

            FlowLayoutPanel dugmeler = new FlowLayoutPanel();
            dugmeler.Dock = DockStyle.Fill;
            dugmeler.BackColor = Color.Transparent;
            dugmeler.Padding = new Padding(0, 6, 0, 0);
            btnAc = Tema.Buton("Talep Aç", Tema.Yesil, Color.FromArgb(16, 185, 129), 150, 42);
            btnGuncelle = Tema.Buton("Güncelle", Tema.Mavi, Tema.Mor, 150, 42);
            Guna2GradientButton btnBos = Tema.Buton("Boş Talep", Tema.Gri1, Tema.Gri2, 130, 42);
            btnSil = Tema.Buton("Sil", Tema.Kirmizi, Color.FromArgb(190, 24, 93), 110, 42);
            _ipucu.SetToolTip(btnAc, "Formdaki bilgilerle yeni bir talep açar");
            _ipucu.SetToolTip(btnGuncelle, "Seçili talebin üzerine yazar (Ctrl+S)");
            _ipucu.SetToolTip(btnBos, "Formu boşaltır (Ctrl+N)");
            btnAc.Enabled = Oturum.Aktif.IzinVar(Izin.TalepEkle);
            btnGuncelle.Enabled = Oturum.Aktif.IzinVar(Izin.TalepGuncelle);
            btnSil.Enabled = Oturum.Aktif.IzinVar(Izin.TalepSil);
            btnAc.Click += delegate { TalepAc(); };
            btnGuncelle.Click += delegate { TalepGuncelle(); };
            btnBos.Click += delegate { BosTalep(); };
            btnSil.Click += delegate { TalepSil(); };
            dugmeler.Controls.AddRange(new Control[] { btnAc, btnGuncelle, btnBos, btnSil });

            kok.Controls.Add(ust, 0, 0);
            kok.Controls.Add(sira1, 0, 1);
            kok.Controls.Add(sira2, 0, 2);
            kok.Controls.Add(kalipAra, 0, 3);
            kok.Controls.Add(gridPanel, 0, 4);
            kok.Controls.Add(sira3, 0, 5);
            kok.Controls.Add(dugmeler, 0, 6);
            kart.Controls.Add(kok);
            return kart;
        }

        private static TableLayoutPanel Sutunlar(params float[] yuzde)
        {
            TableLayoutPanel t = new TableLayoutPanel();
            t.Dock = DockStyle.Fill;
            t.ColumnCount = yuzde.Length;
            t.RowCount = 1;
            t.BackColor = Color.Transparent;
            t.Margin = new Padding(0);
            foreach (float y in yuzde) t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, y));
            t.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            return t;
        }

        private static Panel Alan(string etiket, Control kontrol, int yukseklik = 66)
        {
            Panel p = new Panel();
            p.Dock = DockStyle.Fill;
            p.Height = yukseklik;
            p.Margin = new Padding(0, 0, 10, 4);
            p.BackColor = Color.Transparent;
            Label l = new Label();
            l.Text = etiket;
            l.Dock = DockStyle.Top;
            l.Height = 22;
            l.Font = Tema.Kucuk;
            l.ForeColor = Color.FromArgb(190, 198, 232);
            l.BackColor = Color.Transparent;
            kontrol.Dock = DockStyle.Top;
            p.Controls.Add(kontrol);
            p.Controls.Add(l);
            return p;
        }

        private static void Yerlestir(TableLayoutPanel t, int satir, int sutun, int genislik, Control c)
        {
            while (t.RowCount <= satir)
            {
                t.RowCount = t.RowCount + 1;
                t.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            }
            t.Controls.Add(c, sutun, satir);
            if (genislik > 1) t.SetColumnSpan(c, genislik);
        }

        private static void KolonEkle(DataGridView g, string alan, string baslik, int genislik, KolonTipi tip)
        {
            DataGridViewTextBoxColumn c = new DataGridViewTextBoxColumn();
            c.DataPropertyName = alan;
            c.Name = "c" + alan;
            c.HeaderText = baslik;
            c.AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
            c.Width = genislik + 12;
            c.MinimumWidth = 50;
            c.SortMode = DataGridViewColumnSortMode.Automatic;
            if (tip == KolonTipi.Tarih) c.DefaultCellStyle.Format = "dd.MM.yyyy";
            if (tip == KolonTipi.Tam) c.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            g.Columns.Add(c);
        }

        /// <summary>Statü ve öncelik renkli gösterilir.</summary>
        private void GridBicim(object sender, DataGridViewCellFormattingEventArgs e)
        {
            DataGridView g = sender as DataGridView;
            if (g == null || e.RowIndex < 0 || e.ColumnIndex < 0) return;
            string alan = g.Columns[e.ColumnIndex].DataPropertyName;
            string v = Convert.ToString(e.Value ?? "");
            if (alan == "Statu" || alan == "KalipStatu")
            {
                switch (v)
                {
                    case "Açık": case "Planlandı": e.CellStyle.ForeColor = Color.FromArgb(96, 165, 250); break;
                    case "Sevkediliyor": e.CellStyle.ForeColor = Color.FromArgb(251, 146, 60); break;
                    case "Kısmi Teslim Alındı": case "İade Edildi": e.CellStyle.ForeColor = Color.FromArgb(253, 224, 71); break;
                    case "Teslim Alındı": e.CellStyle.ForeColor = Color.FromArgb(45, 212, 191); break;
                    case "İşleniyor": e.CellStyle.ForeColor = Color.FromArgb(192, 132, 252); break;
                    case "Tamamlandı": case "Kapandı": e.CellStyle.ForeColor = Color.FromArgb(74, 222, 128); break;
                    case "Hurdaya Ayrıldı": e.CellStyle.ForeColor = Color.FromArgb(248, 113, 113); break;
                    case "İptal": e.CellStyle.ForeColor = Color.FromArgb(148, 163, 184); break;
                }
                e.CellStyle.Font = Tema.Kalin;
            }
            else if (alan == "Oncelik")
            {
                if (v == "Çok Acil") { e.CellStyle.ForeColor = Color.FromArgb(248, 113, 113); e.CellStyle.Font = Tema.Kalin; }
                else if (v == "Acil") { e.CellStyle.ForeColor = Color.FromArgb(251, 146, 60); e.CellStyle.Font = Tema.Kalin; }
            }
            else if (alan == "TalepNo")
            {
                e.CellStyle.ForeColor = Color.FromArgb(165, 180, 252);
            }
        }

        // =========================================================== ARAMA / LİSTELER

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (_oneriPopup != null && _oneriPopup.Visible && ReferenceEquals(OdaktakiGunaKutusu(), txtSatirKod))
            {
                if (keyData == Keys.Down) { OneriGit(1); return true; }
                if (keyData == Keys.Up) { OneriGit(-1); return true; }
                if (keyData == Keys.Escape) { OneriKapat(); return true; }
            }
            if (keyData == Keys.Enter)
            {
                Guna2TextBox t = OdaktakiGunaKutusu();
                if (t != null)
                {
                    if (ReferenceEquals(t, txtSatirKod)) { OneriEnter(); return true; }
                    if (ReferenceEquals(t, txtTalepNoAra)) { SagYenile(); return true; }
                    if (ReferenceEquals(t, txtAra)) { ListeYenile(true); return true; }
                }
            }
            switch (keyData)
            {
                case Keys.Control | Keys.S:
                    if (pnlTalep.Visible) { if (_mevcut != null) TalepGuncelle(); else TalepAc(); }
                    return true;
                case Keys.Control | Keys.N:
                    if (pnlTalep.Visible) BosTalep();
                    return true;
                case Keys.F5:
                    if (pnlTalep.Visible) SagYenile();
                    else ListeYenile(true);
                    return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        // Durum / bilgi satırı: formun başlığındaki küçük metin
        private void Durum(string metin) { lblKalipBilgi.Text = metin; lblKalipBilgi.ForeColor = Tema.YaziSoluk; }

        private void Bildir(BildirimTuru tur, string baslik, string metin)
        {
            Bildirim.Goster(this, tur, baslik, metin);
        }

        private void OzetYaz(KalipOzeti oz, string girilen)
        {
            if (oz.Bulundu)
            {
                lblKalipBilgi.Text = oz.KalipKodu + (oz.ParcaAdi.Length > 0 ? "  \u2022  " + oz.ParcaAdi : "") +
                    "   |   Sahip: " + (string.IsNullOrEmpty(oz.SahipBolum) ? "-" : oz.SahipBolum) +
                    "  \u2022  Lokasyon: " + (string.IsNullOrEmpty(oz.Lokasyon) ? "-" : oz.Lokasyon) +
                    "  \u2022  " + oz.TalepAdedi.ToString(Kultur.Tr) + " talepte yer aldı  \u2022  Tashih: " + oz.TashihSayisi.ToString(Kultur.Tr);
                lblKalipBilgi.ForeColor = Tema.YaziSoluk;
            }
            else
            {
                lblKalipBilgi.Text = girilen + "  \u2022  Kalıp Takip kaydında bulunamadı";
                lblKalipBilgi.ForeColor = Color.FromArgb(253, 224, 71);
            }
        }

        // ---- yazdıkça açılan kalıp öneri penceresi

        private void OneriKur()
        {
            _oneriListe = new ListBox();
            _oneriListe.BorderStyle = BorderStyle.None;
            _oneriListe.BackColor = Color.FromArgb(30, 35, 68);
            _oneriListe.ForeColor = Tema.Yazi;
            _oneriListe.Font = Tema.Normal;
            _oneriListe.ItemHeight = 24;
            _oneriListe.IntegralHeight = false;
            _oneriListe.Width = 640;
            _oneriListe.MouseDown += delegate (object s, MouseEventArgs e)
            {
                int i = _oneriListe.IndexFromPoint(e.Location);
                if (i >= 0 && i < _oneriKodlar.Count) OneriSec(_oneriKodlar[i]);
            };
            ToolStripControlHost host = new ToolStripControlHost(_oneriListe);
            host.Margin = Padding.Empty;
            host.Padding = Padding.Empty;
            _oneriPopup = new ToolStripDropDown();
            _oneriPopup.AutoClose = false;
            _oneriPopup.Padding = Padding.Empty;
            _oneriPopup.BackColor = Color.FromArgb(30, 35, 68);
            _oneriPopup.Items.Add(host);

            _oneriZaman.Interval = 250;
            _oneriZaman.Tick += delegate { _oneriZaman.Stop(); OneriGuncelle(); };
            txtSatirKod.TextChanged += delegate { _oneriZaman.Stop(); _oneriZaman.Start(); };
            txtSatirKod.LostFocus += delegate
            {
                BeginInvoke((MethodInvoker)delegate
                {
                    if (_oneriListe == null || _oneriListe.Focused) return;
                    if (_oneriListe.ClientRectangle.Contains(_oneriListe.PointToClient(Cursor.Position))) return;
                    OneriKapat();
                });
            };
        }

        private void OneriGuncelle()
        {
            string m = txtSatirKod.Text.Trim();
            if (m.Length < 2 || !txtSatirKod.Focused) { OneriKapat(); return; }
            DataTable dt;
            try { dt = TalepRepository.KalipAra(m, 15); }
            catch (Exception ex) { HataGunlugu.Yaz(ex); OneriKapat(); return; }
            _oneriKodlar.Clear();
            _oneriListe.Items.Clear();
            foreach (DataRow r in dt.Rows)
            {
                string kod = Convert.ToString(r["ParcaKodu"]);
                string ad = r["ParcaAdi"] == DBNull.Value ? "" : Convert.ToString(r["ParcaAdi"]);
                string lok = r["Lokasyon"] == DBNull.Value ? "" : Convert.ToString(r["Lokasyon"]);
                string sahip = r["SahipBolum"] == DBNull.Value ? "" : Convert.ToString(r["SahipBolum"]);
                string satir = kod + (ad.Length > 0 ? "   \u2022   " + ad : "");
                if (lok.Length > 0) satir += "   \u2022   " + lok;
                if (sahip.Length > 0) satir += "   (" + sahip + ")";
                _oneriKodlar.Add(kod);
                _oneriListe.Items.Add(satir);
            }
            if (_oneriKodlar.Count == 0) { OneriKapat(); return; }
            _oneriListe.SelectedIndex = 0;
            _oneriListe.Height = Math.Min(_oneriKodlar.Count, 10) * _oneriListe.ItemHeight + 4;
            _oneriPopup.Size = new Size(_oneriListe.Width + 2, _oneriListe.Height + 2);
            if (!_oneriPopup.Visible) _oneriPopup.Show(txtSatirKod, new Point(0, txtSatirKod.Height + 2));
        }

        private void OneriKapat()
        {
            _oneriZaman.Stop();
            if (_oneriPopup != null && _oneriPopup.Visible) _oneriPopup.Close();
        }

        private void OneriGit(int yon)
        {
            if (_oneriListe.Items.Count == 0) return;
            int i = Math.Max(0, Math.Min(_oneriListe.Items.Count - 1, _oneriListe.SelectedIndex + yon));
            _oneriListe.SelectedIndex = i;
        }

        private void OneriSec(string kod)
        {
            OneriKapat();
            txtSatirKod.Text = kod;
            SatirEkle(kod);
        }

        /// <summary>Enter: öneri açıksa seçili (ya da tam eşleşen) kalıp, değilse yazılan kod eklenir (barkod okuyucu).</summary>
        private void OneriEnter()
        {
            string yazilan = txtSatirKod.Text.Trim();
            if (_oneriPopup != null && _oneriPopup.Visible && _oneriKodlar.Count > 0)
            {
                string kod = null;
                foreach (string k in _oneriKodlar)
                    if (string.Equals(k, yazilan, StringComparison.OrdinalIgnoreCase)) { kod = k; break; }
                if (kod == null) kod = _oneriKodlar[Math.Max(0, _oneriListe.SelectedIndex)];
                OneriSec(kod);
                return;
            }
            OneriKapat();
            SatirEkle(yazilan);
        }

        /// <summary>Sağdaki liste: talep no aramasına göre; boşsa son 50.</summary>
        private void SagYenile()
        {
            string aranan = txtTalepNoAra.Text.Trim();
            try
            {
                using (new Bekleme())
                {
                    _tabloSag = TalepRepository.Ara(aranan, "", "", null, null, aranan.Length > 0 ? 300 : 50);
                    _tabloSag.PrimaryKey = new DataColumn[] { _tabloSag.Columns["Id"] };
                }
            }
            catch (Exception ex)
            {
                HataGunlugu.Yaz(ex);
                Mesajlar.Hata("Talepler okunamadı:\r\n\r\n" + Db.AnlasilirMesaj(ex));
                return;
            }
            _yukleniyor = true;
            try
            {
                gridSag.DataSource = _tabloSag.DefaultView;
                gridSag.ClearSelection();
            }
            finally { _yukleniyor = false; }
            lblSagBaslik.Text = aranan.Length > 0
                ? "Arama: " + _tabloSag.Rows.Count.ToString(Kultur.Tr) + " talep"
                : "Son " + _tabloSag.Rows.Count.ToString(Kultur.Tr) + " talep";
        }

        private void GridSecildi(DataGridView g)
        {
            if (_yukleniyor || g.SelectedRows.Count == 0) return;
            DataRowView rv = g.SelectedRows[0].DataBoundItem as DataRowView;
            if (rv == null) return;
            // Aynı talebi diğer listede de seçili göstermeye çalışmadan, doğrudan forma yükle
            TalepGoster(Convert.ToInt32(rv.Row["Id"]));
        }

        /// <summary>Talebi veritabanından okuyup forma yükler (listeleri yenilemez).</summary>
        private void TalepGoster(int id)
        {
            DataTable dt = TalepRepository.Satir(id);
            if (dt.Rows.Count == 0) { FormuTemizle(); return; }
            FormaYukle(Talep.SatirdanOku(dt.Rows[0]));
        }

        // =========================================================== FORM

        private static void TarihYaz(Guna2DateTimePicker dt, DateTime? v)
        {
            if (v.HasValue) { dt.Value = v.Value; dt.Checked = true; }
            else dt.Checked = false;
        }

        private static DateTime? TarihOku(Guna2DateTimePicker dt)
        {
            return dt.Checked ? (DateTime?)dt.Value.Date : null;
        }

        private static void Sec(Guna2ComboBox c, string v)
        {
            int i = c.FindStringExact(v ?? "");
            c.SelectedIndex = i >= 0 ? i : 0;
        }

        private void ModGuncelle()
        {
            if (_mevcut == null)
            {
                lblMod.Text = "Yeni talep";
                lblMeta.Text = "Bilgileri doldurup 'Talep Aç'a basın.";
            }
            else
            {
                lblMod.Text = "Talep " + _mevcut.TalepNo + " düzenleniyor";
                string meta = "";
                if (_mevcut.SonDegisiklik.HasValue) meta = "Son değişiklik: " + _mevcut.SonDegisiklik.Value.ToString("dd.MM.yyyy HH:mm", Kultur.Tr);
                if (!string.IsNullOrEmpty(_mevcut.SonKullanici)) meta += "  \u2022  " + _mevcut.SonKullanici;
                lblMeta.Text = meta;
            }
        }

        private void FormaYukle(Talep t)
        {
            _mevcut = t;
            txtTalepNo.Text = t.TalepNo ?? "";
            cmbBolum.Text = t.Bolum ?? "";
            Sec(cmbOncelik, t.Oncelik);
            Sec(cmbStatu, t.Statu);
            TarihYaz(dtTalep, t.TalepTarihi);
            TarihYaz(dtTermin, t.TerminTarihi);
            TarihYaz(dtTamam, t.TamamlanmaTarihi);
            txtTalepEden.Text = t.TalepEden ?? "";
            txtAciklama.Text = t.Aciklama ?? "";
            txtSonuc.Text = t.Sonuc ?? "";
            SatirlariYukle(t.Id);
            ModGuncelle();
        }

        private void SatirlariYukle(int talepId)
        {
            _satirYukleniyor = true;
            try
            {
                _satirlar.Rows.Clear();
                DataTable dt = TalepRepository.SatirlariGetir(talepId);
                foreach (DataRow r in dt.Rows)
                {
                    DataRow n = _satirlar.NewRow();
                    n["SatirId"] = Convert.ToInt32(r["SatirId"]);
                    n["KalipKodu"] = Convert.ToString(r["KalipKodu"]);
                    n["KalipStatu"] = r["KalipStatu"] == DBNull.Value ? "" : Convert.ToString(r["KalipStatu"]);
                    n["Lokasyon"] = r["Lokasyon"] == DBNull.Value ? "" : Convert.ToString(r["Lokasyon"]);
                    n["Aciklama"] = r["Aciklama"] == DBNull.Value ? "" : Convert.ToString(r["Aciklama"]);
                    n["TalepSira"] = Convert.ToInt32(r["TalepSira"]);
                    n["TashihSayisi"] = Convert.ToInt32(r["TashihSayisi"]);
                    n["SahipBolum"] = r["SahipBolum"] == DBNull.Value ? "" : Convert.ToString(r["SahipBolum"]);
                    _satirlar.Rows.Add(n);
                }
                gridSatir.ClearSelection();
            }
            finally { _satirYukleniyor = false; }
            if (gridSatir.Rows.Count > 0) gridSatir.Rows[0].Selected = true;
            SatirSecildi();
        }

        private void FormuTemizle()
        {
            _mevcut = null;
            txtTalepNo.Text = "";
            cmbBolum.Text = Oturum.Aktif.Bolum ?? "";
            cmbOncelik.SelectedIndex = 0;
            cmbStatu.SelectedIndex = 0;
            TarihYaz(dtTalep, DateTime.Today);
            dtTermin.Checked = false;
            dtTamam.Checked = false;
            txtTalepEden.Text = Oturum.Aktif.KullaniciAdi ?? "";
            txtAciklama.Text = "";
            txtSonuc.Text = "";
            txtSatirKod.Text = "";
            _satirYukleniyor = true;
            try { _satirlar.Rows.Clear(); }
            finally { _satirYukleniyor = false; }
            SatirSecildi();
            ModGuncelle();
        }

        private void BosTalep()
        {
            _yukleniyor = true;
            try { gridSag.ClearSelection(); }
            finally { _yukleniyor = false; }
            FormuTemizle();
            cmbBolum.Focus();
            Durum("Boş talep formu hazır. Kalıpları okutun ya da yazın.");
        }

        // ---- kalıp satırları

        private DataRow SeciliSatir()
        {
            if (gridSatir.SelectedRows.Count == 0) return null;
            DataRowView rv = gridSatir.SelectedRows[0].DataBoundItem as DataRowView;
            return rv == null ? null : rv.Row;
        }

        private void SatirSecildi()
        {
            if (_satirYukleniyor) return;
            DataRow r = SeciliSatir();
            _satirYukleniyor = true;
            try
            {
                bool var = r != null;
                txtSatirAciklama.Enabled = var;
                cmbSatirStatu.Enabled = var && DurumYetkisi;
                txtSatirLok.Enabled = var && DurumYetkisi;
                txtSahip.Enabled = var && DurumYetkisi;
                txtSahip.Text = var ? Convert.ToString(r["SahipBolum"]) : "";
                txtSatirAciklama.Text = var ? Convert.ToString(r["Aciklama"]) : "";
                txtSatirLok.Text = var ? Convert.ToString(r["Lokasyon"]) : "";
                cmbSatirStatu.SelectedIndex = var ? Math.Max(0, cmbSatirStatu.FindStringExact(Convert.ToString(r["KalipStatu"]))) : -1;
            }
            finally { _satirYukleniyor = false; }
        }

        /// <summary>Bölüm değişince, henüz kaydedilmemiş 'Planlandı' satırların otomatik lokasyonu güncellenir.</summary>
        private void YeniSatirLokasyonlariniYenile()
        {
            if (_satirlar == null) return;
            foreach (DataRow r in _satirlar.Rows)
            {
                if (Convert.ToInt32(r["SatirId"]) == 0 && Convert.ToString(r["KalipStatu"]) == TalepKurallari.Planlandi)
                    r["Lokasyon"] = TalepKurallari.LokasyonBul(TalepKurallari.Planlandi, cmbBolum.Text);
            }
            DataRow s = SeciliSatir();
            if (s != null && Convert.ToInt32(s["SatirId"]) == 0)
            {
                _satirYukleniyor = true;
                try { txtSatirLok.Text = Convert.ToString(s["Lokasyon"]); }
                finally { _satirYukleniyor = false; }
            }
        }

        private void SatirDegeriYaz(string alan, string deger)
        {
            if (_satirYukleniyor) return;
            DataRow r = SeciliSatir();
            if (r != null) r[alan] = deger ?? "";
        }

        /// <summary>Statü değişince lokasyon kurala göre otomatik atanır (sonra elle değiştirilebilir).</summary>
        private void SatirStatuDegisti()
        {
            if (_satirYukleniyor || cmbSatirStatu.SelectedItem == null) return;
            DataRow r = SeciliSatir();
            if (r == null) return;
            string statu = Convert.ToString(cmbSatirStatu.SelectedItem);
            string lok = TalepKurallari.LokasyonBul(statu, cmbBolum.Text);
            r["KalipStatu"] = statu;
            r["Lokasyon"] = lok;
            _satirYukleniyor = true;
            try { txtSatirLok.Text = lok; }
            finally { _satirYukleniyor = false; }
        }

        private void SatirEkle(string girilen)
        {
            string kod = (girilen ?? "").Trim();
            OneriKapat();
            if (kod.Length == 0) return;
            if (string.IsNullOrWhiteSpace(cmbBolum.Text))
            {
                Mesajlar.Uyari("Önce 'İlgili Bölüm'ü seçin; kalıbın bölümüne uygun olup olmadığı buna göre kontrol edilir.");
                cmbBolum.Focus();
                return;
            }
            if (_mevcut != null && !Oturum.Aktif.IzinVar(Izin.TalepGuncelle)) { Yetki(Izin.TalepGuncelle); return; }
            if (_mevcut == null && !Oturum.Aktif.IzinVar(Izin.TalepEkle)) { Yetki(Izin.TalepEkle); return; }
            KalipOzeti oz;
            try { oz = TalepRepository.KalipOzet(kod); }
            catch (Exception ex)
            {
                HataGunlugu.Yaz(ex);
                Mesajlar.Hata("Kalıp okunamadı:\r\n\r\n" + Db.AnlasilirMesaj(ex));
                return;
            }
            string tam = oz.Bulundu ? oz.KalipKodu : kod;
            string engel;
            try { engel = TalepRepository.EklemeEngeli(tam, _mevcut != null ? _mevcut.Id : 0, cmbBolum.Text); }
            catch (Exception ex)
            {
                HataGunlugu.Yaz(ex);
                Mesajlar.Hata("Kalıp kontrol edilemedi:\r\n\r\n" + Db.AnlasilirMesaj(ex));
                return;
            }
            if (engel != null)
            {
                OzetYaz(oz, kod);
                Mesajlar.Uyari(engel);
                txtSatirKod.SelectAll();
                return;
            }
            foreach (DataRow r in _satirlar.Rows)
            {
                if (string.Equals(TrMetin.Katla(Convert.ToString(r["KalipKodu"])), TrMetin.Katla(tam), StringComparison.Ordinal))
                {
                    Bildir(BildirimTuru.Uyari, "Zaten listede", tam + " bu talepte var.");
                    txtSatirKod.SelectAll();
                    return;
                }
            }
            if (!oz.Bulundu && !Mesajlar.Soru("'" + kod + "' kodu Kalıp Takip kayıtlarında bulunamadı.\r\nYine de talebe eklensin mi?")) return;

            DataRow n = _satirlar.NewRow();
            n["SatirId"] = 0;
            n["KalipKodu"] = tam;
            n["KalipStatu"] = TalepKurallari.Planlandi;
            n["Lokasyon"] = TalepKurallari.LokasyonBul(TalepKurallari.Planlandi, cmbBolum.Text);
            n["Aciklama"] = "";
            n["TalepSira"] = oz.TalepAdedi + 1;
            n["TashihSayisi"] = oz.TashihSayisi;
            n["SahipBolum"] = string.IsNullOrEmpty(oz.SahipBolum) ? "" : oz.SahipBolum;
            _satirlar.Rows.Add(n);
            OzetYaz(oz, kod);
            txtSatirKod.Text = "";
            gridSatir.ClearSelection();
            gridSatir.Rows[gridSatir.Rows.Count - 1].Selected = true;
            gridSatir.FirstDisplayedScrollingRowIndex = gridSatir.Rows.Count - 1;
            SatirSecildi();
            txtSatirAciklama.Focus();
        }

        private void SatirCikar()
        {
            DataRow r = SeciliSatir();
            if (r == null) { Mesajlar.Bilgi("Önce çıkarılacak kalıbı listeden seçin."); return; }
            if (Convert.ToInt32(r["SatirId"]) > 0)
            {
                if (!Mesajlar.Soru(Convert.ToString(r["KalipKodu"]) + " talepten çıkarılsın mı? (Kaydedince kalıp satırı ve açıklaması silinir.)")) return;
            }
            _satirlar.Rows.Remove(r);
            SatirSecildi();
        }

        private System.Collections.Generic.List<TalepKalip> SatirlariOku()
        {
            System.Collections.Generic.List<TalepKalip> liste = new System.Collections.Generic.List<TalepKalip>();
            foreach (DataRow r in _satirlar.Rows)
            {
                TalepKalip k = new TalepKalip();
                k.Id = Convert.ToInt32(r["SatirId"]);
                k.KalipKodu = Convert.ToString(r["KalipKodu"]);
                k.Aciklama = Convert.ToString(r["Aciklama"]);
                k.KalipStatu = Convert.ToString(r["KalipStatu"]);
                k.Lokasyon = Convert.ToString(r["Lokasyon"]);
                k.SahipBolum = Convert.ToString(r["SahipBolum"]);
                // Yetkisiz kullanıcı yeni satırda lokasyonu belirleyemez: kural (talep eden bölüm) uygulanır.
                if (k.Id == 0 && !DurumYetkisi) { k.KalipStatu = TalepKurallari.Planlandi; k.Lokasyon = ""; }
                liste.Add(k);
            }
            return liste;
        }

        private Talep FormdanOku()
        {
            if (_satirlar.Rows.Count == 0)
            {
                Mesajlar.Uyari("Talepte en az bir kalıp olmalı. Kalıp kodunu yazın ya da okutun.");
                txtSatirKod.Focus();
                return null;
            }
            if (string.IsNullOrWhiteSpace(cmbBolum.Text))
            {
                Mesajlar.Uyari("İlgili bölümü yazın ya da listeden seçin.");
                cmbBolum.Focus();
                return null;
            }
            Talep t = new Talep();
            t.TalepNo = _mevcut != null ? _mevcut.TalepNo : null;
            t.Bolum = cmbBolum.Text.Trim();
            t.Oncelik = Convert.ToString(cmbOncelik.SelectedItem ?? "Normal");
            t.Statu = Convert.ToString(cmbStatu.SelectedItem ?? "Açık");
            t.TalepTarihi = TarihOku(dtTalep) ?? DateTime.Today;
            t.TerminTarihi = TarihOku(dtTermin);
            t.TamamlanmaTarihi = TarihOku(dtTamam);
            t.TalepEden = txtTalepEden.Text.Trim();
            t.Aciklama = txtAciklama.Text.Trim();
            t.Sonuc = txtSonuc.Text.Trim();
            if (t.TerminTarihi.HasValue && t.TerminTarihi.Value < t.TalepTarihi.Value)
            {
                if (!Mesajlar.Soru("Termin tarihi, talep tarihinden önce görünüyor. Yine de kaydedilsin mi?")) return null;
            }
            return t;
        }

        private bool Yetki(string anahtar)
        {
            if (Oturum.Aktif.IzinVar(anahtar)) return true;
            Mesajlar.Uyari("Bu işlem için yetkiniz yok.");
            return false;
        }

        /// <summary>İşlem sonrası: listeleri tazeler, talebi forma getirir, kod kutusunu temizleyip imleci oraya koyar.</summary>
        private void IslemSonrasi(int id)
        {
            SagYenile();
            TalepGoster(id);
            if (_listeYuklendi) ListeYenile(false);
            txtSatirKod.Focus();
        }

        private void TalepAc()
        {
            if (!Yetki(Izin.TalepEkle)) return;
            if (_mevcut != null &&
                !Mesajlar.Soru("Seçili talep (" + _mevcut.TalepNo + ") zaten kayıtlı.\r\nAynı bilgilerle YENİ bir talep açılsın mı?"))
                return;
            Talep t = FormdanOku();
            if (t == null) return;
            t.TalepNo = null;
            try
            {
                int id;
                using (new Bekleme())
                {
                    id = TalepRepository.Ekle(t, SatirlariOku(), Oturum.Aktif.KullaniciAdi);
                }
                IslemSonrasi(id);
                string no = _mevcut != null ? _mevcut.TalepNo : "";
                Durum("Talep açıldı: " + no);
                Bildir(BildirimTuru.Basari, "Talep açıldı", no + "  \u2022  " + _satirlar.Rows.Count.ToString(Kultur.Tr) + " kalıp  \u2022  " + t.Bolum);
            }
            catch (Exception ex)
            {
                HataGunlugu.Yaz(ex);
                Mesajlar.Hata("Talep açılamadı:\r\n\r\n" + Db.AnlasilirMesaj(ex));
            }
        }

        private void TalepGuncelle()
        {
            if (_mevcut == null)
            {
                Mesajlar.Bilgi("Güncellemek için önce listeden bir talep seçin.\r\nYeni talep için 'Talep Aç'ı kullanın.");
                return;
            }
            if (!Yetki(Izin.TalepGuncelle)) return;
            Talep t = FormdanOku();
            if (t == null) return;
            t.Id = _mevcut.Id;
            t.TalepNo = _mevcut.TalepNo;
            t.Surum = _mevcut.Surum;
            try
            {
                bool tamam;
                using (new Bekleme())
                {
                    tamam = TalepRepository.Guncelle(t, SatirlariOku(), Oturum.Aktif.KullaniciAdi);
                }
                if (!tamam)
                {
                    Mesajlar.Uyari("Bu talep, siz açtıktan sonra başka bir kullanıcı tarafından değiştirilmiş ya da silinmiş.\r\n\r\n" +
                                   "Hiçbir şey kaydedilmedi. Liste yenileniyor; güncel halini kontrol edip tekrar deneyin.");
                    SagYenile();
                    TalepGoster(t.Id);
                    return;
                }
                IslemSonrasi(t.Id);
                Durum("Talep güncellendi: " + t.TalepNo);
                Bildir(BildirimTuru.Basari, "Talep güncellendi", t.TalepNo + " yeni bilgilerle kaydedildi.");
            }
            catch (Exception ex)
            {
                HataGunlugu.Yaz(ex);
                Mesajlar.Hata("Talep güncellenemedi:\r\n\r\n" + Db.AnlasilirMesaj(ex));
            }
        }

        private void TalepSil()
        {
            if (_mevcut == null)
            {
                Mesajlar.Bilgi("Önce listeden silinecek talebi seçin.");
                return;
            }
            if (!Yetki(Izin.TalepSil)) return;
            string no = _mevcut.TalepNo;
            if (!Mesajlar.Soru(no + " numaralı talep silinecek. Emin misiniz?")) return;
            try
            {
                using (new Bekleme())
                {
                    TalepRepository.Sil(_mevcut.Id);
                }
                SagYenile();
                FormuTemizle();
                if (_listeYuklendi) ListeYenile(false);
                Bildir(BildirimTuru.Basari, "Talep silindi", no + " silindi.");
                txtSatirKod.Focus();
            }
            catch (Exception ex)
            {
                HataGunlugu.Yaz(ex);
                Mesajlar.Hata("Talep silinemedi:\r\n\r\n" + Db.AnlasilirMesaj(ex));
            }
        }

        // =========================================================== TALEPLER SEKMESİ

        private void ListeSekmesiKur()
        {
            TableLayoutPanel kok = new TableLayoutPanel();
            kok.Dock = DockStyle.Fill;
            kok.ColumnCount = 1;
            kok.RowCount = 3;
            kok.BackColor = Color.Transparent;
            kok.Padding = new Padding(14, 12, 14, 12);
            kok.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            kok.RowStyles.Add(new RowStyle(SizeType.Absolute, 128F));
            kok.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            kok.RowStyles.Add(new RowStyle(SizeType.Absolute, 40F));

            // Filtreler
            Guna2Panel filtre = Tema.KartPaneli();
            filtre.Dock = DockStyle.Fill;
            filtre.Margin = new Padding(0, 0, 0, 10);
            FlowLayoutPanel akis = new FlowLayoutPanel();
            akis.Dock = DockStyle.Fill;
            akis.WrapContents = true;
            akis.BackColor = Color.Transparent;
            akis.Padding = new Padding(16, 12, 16, 0);

            txtAra = Tema.Kutu(100);
            txtAra.Width = 340;
            txtAra.Margin = new Padding(0, 0, 10, 8);
            txtAra.PlaceholderText = "Talep no, kalıp kodu, bölüm, açıklama...";
            txtAra.IconLeft = Simge.Ara(Tema.YaziSoluk);
            txtAra.IconLeftSize = new Size(18, 18);

            cmbStatuF = Tema.Acilir(140);
            cmbStatuF.Margin = new Padding(0, 0, 10, 8);
            cmbStatuF.Items.Add("Tüm statüler");
            cmbStatuF.Items.AddRange(TalepRepository.Statuler);
            cmbStatuF.SelectedIndex = 0;

            cmbBolumF = Tema.Acilir(180);
            cmbBolumF.Margin = new Padding(0, 0, 10, 8);

            Label lt = Tema.Etiket("Talep tarihi:", Tema.Kalin, Tema.Yazi);
            lt.Margin = new Padding(6, 9, 6, 0);
            dtBas = Tema.Tarih();
            dtBas.Width = 150;
            dtBas.Margin = new Padding(0, 0, 6, 8);
            dtBit = Tema.Tarih();
            dtBit.Width = 150;
            dtBit.Margin = new Padding(0, 0, 10, 8);

            cmbLimit = Tema.Acilir(130);
            cmbLimit.Margin = new Padding(0, 0, 10, 8);
            cmbLimit.Items.AddRange(new object[] { "Son 500", "Son 2.000", "Tümü" });
            cmbLimit.SelectedIndex = 0;

            Guna2GradientButton btnListele = Tema.Buton("Listele", Tema.Mavi, Tema.Mor, 100);
            Guna2GradientButton btnTemizle = Tema.Buton("Filtreyi Temizle", Tema.Gri1, Tema.Gri2, 140);
            Guna2GradientButton btnExcel = Tema.Buton("Excel'e Aktar", Tema.Yesil, Color.FromArgb(16, 185, 129), 130);
            btnExcel.Enabled = Oturum.Aktif.IzinVar(Izin.TalepExcel);
            btnListele.Click += delegate { ListeYenile(true); };
            btnTemizle.Click += delegate { FiltreTemizle(); };
            btnExcel.Click += delegate { ListeExcel(); };

            akis.Controls.AddRange(new Control[] { txtAra, cmbStatuF, cmbBolumF, lt, dtBas, dtBit, cmbLimit, btnListele, btnTemizle, btnExcel });
            filtre.Controls.Add(akis);

            // Liste
            Guna2Panel gridKart = Tema.KartPaneli();
            gridKart.Dock = DockStyle.Fill;
            gridKart.Margin = new Padding(0);
            gridKart.Padding = new Padding(10);
            gridListe = Tema.Tablo();
            gridListe.Dock = DockStyle.Fill;
            foreach (KolonTanimi k in TalepRepository.Kolonlar) KolonEkle(gridListe, k.Alan, k.Baslik, k.Genislik, k.Tip);
            gridListe.CellFormatting += GridBicim;
            gridListe.CellDoubleClick += delegate (object s, DataGridViewCellEventArgs e)
            {
                if (e.RowIndex < 0) return;
                DataRowView rv = gridListe.Rows[e.RowIndex].DataBoundItem as DataRowView;
                if (rv == null) return;
                int id = Convert.ToInt32(rv["Id"]);
                SekmeAc(true);
                TalepGoster(id);
            };
            gridKart.Controls.Add(gridListe);

            // Alt çubuk
            Guna2Panel durum = Tema.KartPaneli(12);
            durum.Dock = DockStyle.Fill;
            durum.Margin = new Padding(0, 10, 0, 0);
            lblListeSayi = Tema.Etiket("", Tema.Kalin, Tema.Yazi);
            lblListeSayi.Location = new Point(16, 10);
            Label ip = Tema.Etiket("Bir talebi düzenlemek için çift tıklayın.", Tema.Kucuk, Tema.YaziSoluk);
            ip.AutoSize = false;
            ip.Dock = DockStyle.Right;
            ip.Width = 330;
            ip.TextAlign = ContentAlignment.MiddleRight;
            durum.Controls.Add(lblListeSayi);
            durum.Controls.Add(ip);

            kok.Controls.Add(filtre, 0, 0);
            kok.Controls.Add(gridKart, 0, 1);
            kok.Controls.Add(durum, 0, 2);
            pnlListe.Controls.Add(kok);
        }

        private void BolumFiltresiYukle()
        {
            cmbBolumF.Items.Clear();
            cmbBolumF.Items.Add("Tüm bölümler");
            foreach (string b in TalepRepository.BolumListesi()) cmbBolumF.Items.Add(b);
            cmbBolumF.SelectedIndex = 0;
        }

        private void FiltreTemizle()
        {
            txtAra.Text = "";
            cmbStatuF.SelectedIndex = 0;
            if (cmbBolumF.Items.Count > 0) cmbBolumF.SelectedIndex = 0;
            dtBas.Checked = false;
            dtBit.Checked = false;
            ListeYenile(true);
        }

        private int ListeLimit()
        {
            switch (cmbLimit.SelectedIndex)
            {
                case 0: return 500;
                case 1: return 2000;
                default: return 0;
            }
        }

        private void ListeYenile(bool bildir)
        {
            if (!_listeYuklendi)
            {
                BolumFiltresiYukle();
                _listeYuklendi = true;
            }
            string statu = cmbStatuF.SelectedIndex > 0 ? Convert.ToString(cmbStatuF.SelectedItem) : "";
            string bolum = cmbBolumF.SelectedIndex > 0 ? Convert.ToString(cmbBolumF.SelectedItem) : "";
            DateTime? bas = dtBas.Checked ? (DateTime?)dtBas.Value.Date : null;
            DateTime? bit = dtBit.Checked ? (DateTime?)dtBit.Value.Date : null;
            if (bas.HasValue && bit.HasValue && bas.Value > bit.Value)
            {
                Mesajlar.Uyari("Başlangıç tarihi bitiş tarihinden sonra olamaz.");
                return;
            }
            try
            {
                using (new Bekleme())
                {
                    _tabloListe = TalepRepository.Ara(txtAra.Text, statu, bolum, bas, bit, ListeLimit());
                }
            }
            catch (Exception ex)
            {
                HataGunlugu.Yaz(ex);
                Mesajlar.Hata("Talepler okunamadı:\r\n\r\n" + Db.AnlasilirMesaj(ex));
                return;
            }
            gridListe.DataSource = _tabloListe.DefaultView;
            gridListe.ClearSelection();
            int n = _tabloListe.Rows.Count;
            int limit = ListeLimit();
            lblListeSayi.Text = "Talep: " + n.ToString("N0", Kultur.Tr) +
                                (limit > 0 && n >= limit ? "  (en yeni " + limit.ToString("N0", Kultur.Tr) + " ile sınırlı)" : "");
            if (bildir) Bildir(BildirimTuru.Bilgi, "Liste yenilendi", n.ToString("N0", Kultur.Tr) + " talep listelendi.");
        }

        private void ListeExcel()
        {
            if (!Yetki(Izin.TalepExcel)) return;
            DataView gorunum = gridListe.DataSource as DataView;
            if (gorunum == null || gorunum.Count == 0)
            {
                Mesajlar.Bilgi("Aktarılacak talep yok.");
                return;
            }
            using (SaveFileDialog dlg = new SaveFileDialog())
            {
                dlg.Filter = "Excel Çalışma Kitabı (*.xlsx)|*.xlsx";
                dlg.FileName = "TalepListesi_" + DateTime.Now.ToString("yyyyMMdd_HHmm", Kultur.Inv) + ".xlsx";
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    using (new Bekleme())
                    {
                        XlsxYazici.Yaz(dlg.FileName, gorunum, TalepRepository.Kolonlar);
                    }
                }
                catch (Exception ex)
                {
                    HataGunlugu.Yaz(ex);
                    Mesajlar.Hata("Excel dosyası yazılamadı:\r\n\r\n" + ex.Message);
                    return;
                }
                Bildir(BildirimTuru.Basari, "Excel dosyası oluşturuldu", gorunum.Count.ToString("N0", Kultur.Tr) + " talep kaydedildi.");
                if (Mesajlar.Soru("Liste Excel'e aktarıldı. Dosya şimdi açılsın mı?"))
                {
                    try { Process.Start(dlg.FileName); } catch { }
                }
            }
        }
    }
}
