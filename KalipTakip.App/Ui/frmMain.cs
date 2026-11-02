using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;
using Guna.UI2.WinForms;

namespace KalipTakip.Arayuz
{
    /// <summary>Ana ekran: zengin kayıt listesi + düzenleme paneli (eski "kalip" userform'unun karşılığı).</summary>
    [DesignerCategory("Code")]
    public class frmMain : TabanForm
    {
        private DataTable _tablo;
        private Kalip _mevcut;               // null = yeni kayıt giriliyor
        private bool _yukleniyor;
        private int _limit;

        // Arama
        private Guna2ComboBox cmbAlan;
        private Guna2ComboBox cmbLimit;
        private Guna2TextBox txtAra;
        private Guna2DataGridView grid;

        // Düzenleme alanları
        private Guna2TextBox txtParcaKodu, txtTepeKodu, txtParcaAdi, txtTashih, txtHafta, txtDovme, txtCnc, txtAciklama, txtToplam;
        private OneriKutusu cmbKalipTuru, cmbTur;
        private Guna2DateTimePicker dtGelis, dtBitis, dtIslem;
        private Guna2TextBox[] sureKutulari;
        private readonly HashSet<Control> _sayiKutulari = new HashSet<Control>();

        private Label lblMod, lblMeta;
        private Label lblSayi, lblOrt, lblSecili, lblMesaj;
        private readonly ToolTip _ipucu = new ToolTip();

        private static readonly string[] SureEtiketleri =
        {
            "İşlem Süresi", "Ayar", "Bakım", "Eleman Eksikliği", "Elektrik", "Kalıp Arıza", "Takım", "Arıza"
        };

        public frmMain()
            : base("Kalıp Takip Sistemi", "Kalıphane kayıt, bakım ve süre takibi", new Size(1500, 860), true)
        {
            SuspendLayout();
            MinimumSize = new Size(1180, 720);
            KeyPreview = true;

            UstBandiKur();

            TableLayoutPanel kok = new TableLayoutPanel();
            kok.Dock = DockStyle.Fill;
            kok.ColumnCount = 1;
            kok.RowCount = 3;
            kok.BackColor = Color.Transparent;
            kok.Padding = new Padding(14, 12, 14, 12);
            kok.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            kok.RowStyles.Add(new RowStyle(SizeType.Absolute, 78F));
            kok.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            kok.RowStyles.Add(new RowStyle(SizeType.Absolute, 40F));
            kok.Controls.Add(AramaKartiKur(), 0, 0);
            kok.Controls.Add(IcerikKur(), 0, 1);
            kok.Controls.Add(DurumKur(), 0, 2);
            Govde.Controls.Add(kok);

            ListeleriYukle();
            FormuTemizle();
            ResumeLayout(false);
            Shown += delegate { Yenile(0, false); };
        }

        // =========================================================== ARAYÜZ KURULUMU

        private void UstBandiKur()
        {
            string ad = Oturum.Aktif.KullaniciAdi + (Oturum.Aktif.IsAdmin ? "  (Yönetici)" : (Oturum.Aktif.Misafir ? "  (salt okunur)" : ""));
            Label lblKullanici = Tema.Etiket("Kullanıcı: " + ad, Tema.Normal, Color.FromArgb(222, 226, 250));
            lblKullanici.Margin = new Padding(0, 8, 18, 0);

            Guna2Button btnIst = Tema.BantButonu("İstatistik", 104);
            Guna2Button btnCikis = Tema.BantButonu("Portala Dön", 118);
            btnCikis.FillColor = Color.FromArgb(150, 40, 80);
            btnCikis.HoverState.FillColor = Tema.Kirmizi;
            btnCikis.Margin = new Padding(0);

            btnIst.Click += delegate { IstatistikAc(); };
            btnCikis.Click += delegate { Close(); };

            UstSag.Controls.Add(lblKullanici);
            UstSag.Controls.Add(btnIst);
            UstSag.Controls.Add(btnCikis);
        }

        private Control AramaKartiKur()
        {
            Guna2Panel kart = Tema.KartPaneli();
            kart.Dock = DockStyle.Fill;
            kart.Margin = new Padding(0, 0, 0, 10);

            FlowLayoutPanel akis = new FlowLayoutPanel();
            akis.Dock = DockStyle.Fill;
            akis.WrapContents = false;
            akis.BackColor = Color.Transparent;
            akis.Padding = new Padding(16, 14, 16, 0);

            Label et = Tema.Etiket("Ara:", Tema.Kalin, Tema.Yazi);
            et.Margin = new Padding(0, 9, 8, 0);

            cmbAlan = Tema.Acilir(210);
            cmbAlan.Margin = new Padding(0, 1, 10, 0);
            cmbAlan.DisplayMember = "Key";
            cmbAlan.ValueMember = "Value";
            cmbAlan.DataSource = KalipRepository.AramaAlanlari.ToList();

            txtAra = Tema.Kutu(100);
            txtAra.Width = 320;
            txtAra.Margin = new Padding(0, 1, 10, 0);
            txtAra.PlaceholderText = "Aranacak metin...";
            txtAra.IconLeft = Simge.Ara(Tema.YaziSoluk);
            txtAra.IconLeftSize = new Size(18, 18);

            Guna2GradientButton btnAra = Tema.Buton("Ara", Tema.Mavi, Tema.Mor, 90);
            Guna2GradientButton btnFiltre = Tema.Buton("Filtreyi Temizle", Tema.Gri1, Tema.Gri2, 140);
            Guna2GradientButton btnYenile = Tema.Buton("Yenile (F5)", Tema.Turkuaz, Tema.Mavi, 116);

            Label lblGoster = Tema.Etiket("Göster:", Tema.Kalin, Tema.Yazi);
            lblGoster.Margin = new Padding(14, 9, 8, 0);
            cmbLimit = Tema.Acilir(124);
            cmbLimit.Margin = new Padding(0, 1, 0, 0);
            cmbLimit.Items.AddRange(new object[] { "Son 500", "Son 2.000", "Son 10.000", "Tümü" });
            cmbLimit.SelectedIndex = 1;
            _ipucu.SetToolTip(cmbLimit, "Listede en fazla kaç kayıt gösterileceği (en yeniler). Arama her zaman tüm kayıtlarda yapılır; daha az kayıt = daha hızlı.");
            cmbLimit.SelectionChangeCommitted += delegate { Yenile(SeciliId(), true); };

            Guna2GradientButton btnExcel = Tema.Buton("Excel'e Aktar", Tema.Yesil, Color.FromArgb(16, 185, 129), 132);
            btnExcel.Margin = new Padding(22, 0, 0, 8);

            btnAra.Click += delegate { Yenile(SeciliId(), true); };
            btnFiltre.Click += delegate
            {
                txtAra.Text = "";
                cmbAlan.SelectedIndex = 0;
                Yenile(SeciliId(), true);
            };
            btnYenile.Click += delegate { Yenile(SeciliId(), true); };
            btnExcel.Click += delegate { ExceleAktar(); };

            akis.Controls.AddRange(new Control[] { et, cmbAlan, txtAra, btnAra, btnFiltre, btnYenile, lblGoster, cmbLimit, btnExcel });
            kart.Controls.Add(akis);
            return kart;
        }

        private Control IcerikKur()
        {
            TableLayoutPanel t = new TableLayoutPanel();
            t.Dock = DockStyle.Fill;
            t.ColumnCount = 2;
            t.RowCount = 1;
            t.BackColor = Color.Transparent;
            t.Margin = new Padding(0);
            t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            t.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 590F));
            t.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            t.Controls.Add(GridKartiKur(), 0, 0);
            t.Controls.Add(FormKartiKur(), 1, 0);
            return t;
        }

        private Control GridKartiKur()
        {
            Guna2Panel kart = Tema.KartPaneli();
            kart.Dock = DockStyle.Fill;
            kart.Margin = new Padding(0, 0, 12, 0);
            kart.Padding = new Padding(10);

            grid = Tema.Tablo();
            grid.Dock = DockStyle.Fill;
            foreach (KolonTanimi k in KalipRepository.Kolonlar)
            {
                DataGridViewTextBoxColumn c = new DataGridViewTextBoxColumn();
                c.DataPropertyName = k.Alan;
                c.Name = "c" + k.Alan;
                c.HeaderText = k.Baslik;
                c.AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
                c.Width = k.Genislik + 14;
                c.MinimumWidth = 50;
                c.Resizable = DataGridViewTriState.True;
                c.SortMode = DataGridViewColumnSortMode.Automatic;
                switch (k.Tip)
                {
                    case KolonTipi.Tarih:
                        c.DefaultCellStyle.Format = "dd.MM.yyyy";
                        break;
                    case KolonTipi.Ondalik:
                        c.DefaultCellStyle.Format = "0.####";
                        c.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
                        break;
                    case KolonTipi.Tam:
                        c.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
                        break;
                }
                grid.Columns.Add(c);
            }
            grid.SelectionChanged += GridSecimDegisti;
            grid.CellFormatting += GridHucreBicimi;
            kart.Controls.Add(grid);
            return kart;
        }

        /// <summary>Listeyi canlandırır: tasarım modeli (YENİ/İLAVE/TASHİH) renkli, Id ve toplam zaman kalın.</summary>
        private void GridHucreBicimi(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
            string alan = grid.Columns[e.ColumnIndex].DataPropertyName;
            if (alan == "Tur")
            {
                string v = Convert.ToString(e.Value ?? "").Trim().ToUpper(Kultur.Tr);
                if (v == "YENİ") e.CellStyle.ForeColor = Color.FromArgb(96, 165, 250);
                else if (v == "İLAVE") e.CellStyle.ForeColor = Color.FromArgb(74, 222, 128);
                else if (v == "TASHİH") e.CellStyle.ForeColor = Color.FromArgb(251, 146, 60);
                else if (v.Length > 0) e.CellStyle.ForeColor = Color.FromArgb(216, 180, 254);
                e.CellStyle.Font = Tema.Kalin;
            }
            else if (alan == "Id")
            {
                e.CellStyle.ForeColor = Tema.YaziSoluk;
            }
            else if (alan == "ToplamZaman" && e.Value != null && e.Value != DBNull.Value)
            {
                e.CellStyle.ForeColor = Color.FromArgb(253, 224, 71);
                e.CellStyle.Font = Tema.Kalin;
            }
            else if (alan == "TashihSayi" && e.Value != null && e.Value != DBNull.Value)
            {
                long n;
                if (long.TryParse(Convert.ToString(e.Value, Kultur.Inv), out n) && n > 0)
                {
                    e.CellStyle.ForeColor = Color.FromArgb(251, 146, 60);
                    e.CellStyle.Font = Tema.Kalin;
                }
            }
        }

        private Control FormKartiKur()
        {
            Guna2Panel kart = Tema.KartPaneli();
            kart.Dock = DockStyle.Fill;
            kart.Margin = new Padding(0);
            kart.Padding = new Padding(14, 10, 14, 10);

            TableLayoutPanel kok = new TableLayoutPanel();
            kok.Dock = DockStyle.Fill;
            kok.ColumnCount = 1;
            kok.RowCount = 4;
            kok.BackColor = Color.Transparent;
            kok.Margin = new Padding(0);
            kok.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            kok.RowStyles.Add(new RowStyle(SizeType.Absolute, 58F));
            kok.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            kok.RowStyles.Add(new RowStyle(SizeType.Absolute, 62F));
            kok.RowStyles.Add(new RowStyle(SizeType.Absolute, 112F));

            // Başlık
            Panel ust = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent };
            lblMod = Tema.Etiket("Yeni kayıt", Tema.Orta, Tema.Yazi);
            lblMod.Location = new Point(2, 2);
            lblMeta = Tema.Etiket("", Tema.Kucuk, Tema.YaziSoluk);
            lblMeta.Location = new Point(3, 32);
            ust.Controls.Add(lblMod);
            ust.Controls.Add(lblMeta);

            // Alanlar
            Panel kaydirma = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Color.Transparent };

            TableLayoutPanel f = new TableLayoutPanel();
            f.Dock = DockStyle.Top;
            f.AutoSize = true;
            f.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            f.ColumnCount = 2;
            f.BackColor = Color.Transparent;
            f.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            f.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            f.Padding = new Padding(0, 0, 0, 12);

            txtParcaKodu = Tema.Kutu(50);
            cmbKalipTuru = new OneriKutusu { MaxLength = 60 };
            txtTepeKodu = Tema.Kutu(100);
            txtParcaAdi = Tema.Kutu(1000);
            txtParcaAdi.Multiline = true;
            txtParcaAdi.Height = 56;
            dtGelis = Tema.Tarih();
            txtTashih = SayiKutusu();
            dtBitis = Tema.Tarih();
            txtHafta = Tema.Kutu();
            Tema.SaltOkunurStili(txtHafta);
            cmbTur = new OneriKutusu { MaxLength = 30 };
            txtDovme = SayiKutusu();
            txtCnc = SayiKutusu();
            txtAciklama = Tema.Kutu(220);
            txtToplam = Tema.Kutu();
            Tema.SaltOkunurStili(txtToplam);
            txtToplam.Font = new Font("Segoe UI Semibold", 11F);
            txtToplam.ForeColor = Color.FromArgb(253, 224, 71);

            sureKutulari = new Guna2TextBox[8];
            for (int i = 0; i < 8; i++)
            {
                sureKutulari[i] = SayiKutusu();
                sureKutulari[i].TextChanged += delegate { ToplamGuncelle(); };
            }

            dtBitis.ValueChanged += delegate { HaftaGuncelle(); };
            dtBitis.CheckedChanged += delegate { HaftaGuncelle(); };

            // Bitiş tarihi + "Tarih" düğmesi (işlem tarihini bitişe yazar)
            Guna2GradientButton btnTarih = Tema.Buton("Tarih", Tema.Turkuaz, Tema.Mavi, 70, 36);
            btnTarih.Dock = DockStyle.Right;
            btnTarih.Margin = new Padding(0);
            _ipucu.SetToolTip(btnTarih, "İşlem tarihini Bitiş Tarihi alanına yazar");
            btnTarih.Click += delegate { TarihDugmesi(); };
            dtBitis.Dock = DockStyle.Fill;
            Panel bitisHucre = new Panel { Height = 36, BackColor = Color.Transparent };
            bitisHucre.Controls.Add(dtBitis);
            bitisHucre.Controls.Add(btnTarih);

            SatirEkle(f, Alan("Parça Kodu *", txtParcaKodu), Alan("Teklif Referans", txtTepeKodu));
            SatirEkle(f, Alan("Parçanın Adı", txtParcaAdi, 86));
            SatirEkle(f, Alan("Kalıp Türü", cmbKalipTuru), Alan("Kalıp Tasarım Modeli (Yeni / İlave / Tashih)", cmbTur));
            SatirEkle(f, Alan("Geliş Tarihi", dtGelis), Alan("Bitiş Tarihi", bitisHucre));
            SatirEkle(f, Alan("Tashih Sayısı", txtTashih), Alan("Hafta (otomatik)", txtHafta));
            SatirEkle(f, Alan("Dövme Adeti", txtDovme), Alan("CNC No", txtCnc));
            SatirEkle(f, Alan("Açıklama", txtAciklama));

            // Süreler bölümü: farklı, canlı renkli ayrı panel
            Guna2GradientPanel sureKart = new Guna2GradientPanel();
            sureKart.Dock = DockStyle.Top;
            sureKart.Height = 392;
            sureKart.FillColor = Color.FromArgb(88, 36, 176);
            sureKart.FillColor2 = Color.FromArgb(30, 58, 150);
            sureKart.GradientMode = LinearGradientMode.ForwardDiagonal;
            sureKart.BorderRadius = 16;
            sureKart.BackColor = Color.Transparent;
            sureKart.Padding = new Padding(16, 10, 8, 8);

            TableLayoutPanel fSure = new TableLayoutPanel();
            fSure.Dock = DockStyle.Top;
            fSure.AutoSize = true;
            fSure.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            fSure.ColumnCount = 2;
            fSure.BackColor = Color.Transparent;
            fSure.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            fSure.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));

            Label ayrac = Tema.Etiket("SÜRELER  (dakika)", Tema.Kalin, Color.FromArgb(253, 224, 71));
            ayrac.Margin = new Padding(0, 4, 0, 8);
            SatirEkle(fSure, ayrac);
            SatirEkle(fSure, Alan(SureEtiketleri[0], sureKutulari[0]), Alan(SureEtiketleri[1], sureKutulari[1]));
            SatirEkle(fSure, Alan(SureEtiketleri[2], sureKutulari[2]), Alan(SureEtiketleri[3], sureKutulari[3]));
            SatirEkle(fSure, Alan(SureEtiketleri[4], sureKutulari[4]), Alan(SureEtiketleri[5], sureKutulari[5]));
            SatirEkle(fSure, Alan(SureEtiketleri[6], sureKutulari[6]), Alan(SureEtiketleri[7], sureKutulari[7]));
            SatirEkle(fSure, Alan("Toplam Zaman (otomatik)", txtToplam));
            sureKart.Controls.Add(fSure);

            kaydirma.Controls.Add(sureKart);
            kaydirma.Controls.Add(f);

            // İşlem tarihi şeridi (eski TextBox21): tashih geliş tarihi ve "Tarih" düğmesi bunu kullanır
            Guna2Panel serit = new Guna2Panel();
            serit.Dock = DockStyle.Fill;
            serit.FillColor = Tema.KartAcik;
            serit.BorderColor = Color.FromArgb(70, 80, 140);
            serit.BorderThickness = 1;
            serit.BorderRadius = 12;
            serit.BackColor = Color.Transparent;
            serit.Margin = new Padding(0, 6, 0, 6);
            Label lblIslem = Tema.Etiket("İŞLEM TARİHİ", Tema.Kalin, Color.FromArgb(250, 204, 21));
            lblIslem.Location = new Point(14, 15);
            dtIslem = Tema.Tarih();
            dtIslem.SetBounds(124, 8, 150, 36);
            dtIslem.Value = DateTime.Today;
            dtIslem.Checked = true;
            Label lblIpucu = Tema.Etiket("Tashih geliş tarihi ve 'Tarih' düğmesi\r\nbu tarihi kullanır.", Tema.Kucuk, Tema.YaziSoluk);
            lblIpucu.Location = new Point(286, 9);
            serit.Controls.Add(lblIslem);
            serit.Controls.Add(dtIslem);
            serit.Controls.Add(lblIpucu);

            // Düğmeler
            FlowLayoutPanel dugmeler = new FlowLayoutPanel();
            dugmeler.Dock = DockStyle.Fill;
            dugmeler.WrapContents = true;
            dugmeler.BackColor = Color.Transparent;
            dugmeler.Padding = new Padding(0, 6, 0, 0);

            Guna2GradientButton btnGuncelle = Tema.Buton("Güncelle", Tema.Mavi, Tema.Mor, 172, 42);
            Guna2GradientButton btnYeniKaydet = Tema.Buton("Yeni Kaydet", Tema.Yesil, Color.FromArgb(16, 185, 129), 172, 42);
            Guna2GradientButton btnSil = Tema.Buton("Sil", Tema.Kirmizi, Color.FromArgb(190, 24, 93), 172, 42);
            Guna2GradientButton btnGuncelleTashih = Tema.Buton("Güncelle + Tashih Kaydı", Tema.Turuncu, Tema.Pembe, 252, 42);
            Guna2GradientButton btnTemizle = Tema.Buton("Temizle", Tema.Turkuaz, Tema.Mavi, 126, 42);
            Guna2GradientButton btnBos = Tema.Buton("Boş Form", Tema.Gri1, Tema.Gri2, 126, 42);
            _ipucu.SetToolTip(btnGuncelle, "Seçili kaydın üzerine yazar (Ctrl+S)");
            _ipucu.SetToolTip(btnYeniKaydet, "Formdaki bilgileri yeni bir satır olarak ekler");
            _ipucu.SetToolTip(btnGuncelleTashih, "Seçili kaydı günceller, ardından aynı parçadan yeni bir TASHİH kaydı açar:\r\ngeliş tarihi = işlem tarihi, tashih sayısı +1, süreler boş.");
            _ipucu.SetToolTip(btnTemizle, "Süreleri, CNC, dövme adeti ve açıklamayı temizler; geliş tarihini işlem tarihi yapar;\r\ntashih sayısını 1 artırır. Kaydedilmez.\r\n'Yeni Kaydet' yeni satır ekler, 'Güncelle' seçili kaydın üzerine yazar.");
            _ipucu.SetToolTip(btnBos, "Formu tamamen boşaltır (Ctrl+N)");
            btnGuncelle.Click += delegate { GuncelleTiklandi(); };
            btnYeniKaydet.Click += delegate { YeniKaydetTiklandi(); };
            btnGuncelleTashih.Click += delegate { GuncelleVeTashihTiklandi(); };
            btnTemizle.Click += delegate { TemizleTiklandi(); };
            btnBos.Click += delegate { Yeni(); };
            btnSil.Click += delegate { SilTiklandi(); };
            dugmeler.Controls.AddRange(new Control[] { btnGuncelle, btnYeniKaydet, btnSil, btnGuncelleTashih, btnTemizle, btnBos });

            kok.Controls.Add(ust, 0, 0);
            kok.Controls.Add(kaydirma, 0, 1);
            kok.Controls.Add(serit, 0, 2);
            kok.Controls.Add(dugmeler, 0, 3);
            kart.Controls.Add(kok);
            return kart;
        }

        private Control DurumKur()
        {
            Guna2Panel p = Tema.KartPaneli(12);
            p.Dock = DockStyle.Fill;
            p.Margin = new Padding(0, 10, 0, 0);

            TableLayoutPanel t = new TableLayoutPanel();
            t.Dock = DockStyle.Fill;
            t.ColumnCount = 4;
            t.RowCount = 1;
            t.BackColor = Color.Transparent;
            t.Padding = new Padding(14, 0, 14, 0);
            t.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            t.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            t.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            t.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            lblSayi = Tema.Etiket("", Tema.Kalin, Tema.Yazi);
            lblOrt = Tema.Etiket("", Tema.Kucuk, Tema.YaziSoluk);
            lblSecili = Tema.Etiket("", Tema.Kucuk, Color.FromArgb(147, 197, 253));
            lblMesaj = Tema.Etiket("", Tema.Kucuk, Tema.YaziSoluk);
            lblMesaj.AutoSize = false;
            lblMesaj.Dock = DockStyle.Fill;
            lblMesaj.TextAlign = ContentAlignment.MiddleRight;
            foreach (Label l in new Label[] { lblSayi, lblOrt, lblSecili })
            {
                l.Anchor = AnchorStyles.Left;
                l.Margin = new Padding(0, 0, 22, 0);
            }
            t.Controls.Add(lblSayi, 0, 0);
            t.Controls.Add(lblOrt, 1, 0);
            t.Controls.Add(lblSecili, 2, 0);
            t.Controls.Add(lblMesaj, 3, 0);
            p.Controls.Add(t);
            return p;
        }

        /// <summary>Sayı alanı: 30.5 / 30,5 / 575+525 yazılabilir; Enter hesaplar ve aynı kutuda kalır.</summary>
        private Guna2TextBox SayiKutusu()
        {
            Guna2TextBox t = Tema.Kutu(24);
            _sayiKutulari.Add(t);
            Guna2TextBox kutu = t;
            t.Leave += delegate { SayiAlani.KutudanCikildi(kutu); };
            return t;
        }

        /// <summary>Etiket + alan çifti (etiket üstte).</summary>
        private static Panel Alan(string etiket, Control kontrol, int yukseklik = 66)
        {
            Panel p = new Panel();
            p.Dock = DockStyle.Fill;
            p.Height = yukseklik;
            p.Margin = new Padding(0, 0, 12, 4);
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

        private static void SatirEkle(TableLayoutPanel t, Control sol, Control sag = null)
        {
            int r = t.RowCount;
            t.RowCount = r + 1;
            t.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            t.Controls.Add(sol, 0, r);
            if (sag == null) t.SetColumnSpan(sol, 2);
            else t.Controls.Add(sag, 1, r);
        }

        private void ListeleriYukle()
        {
            try
            {
                cmbKalipTuru.SecenekleriYukle(ListeRepository.Getir(ListeRepository.GrupKalipTuru));
                cmbTur.SecenekleriYukle(ListeRepository.Getir(ListeRepository.GrupTur));
            }
            catch (Exception ex)
            {
                HataGunlugu.Yaz(ex);
            }
        }

        // =========================================================== OLAYLAR

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Enter)
            {
                Guna2TextBox t = OdaktakiGunaKutusu();
                if (t != null)
                {
                    if (_sayiKutulari.Contains(t))
                    {
                        // Aynı kutuda kal: sonucu yaz, imleç sona; "/60" eklenip tekrar Enter basılabilir
                        SayiAlani.EnterBasildi(t);
                        return true;
                    }
                    if (ReferenceEquals(t, txtAra))
                    {
                        Yenile(SeciliId(), true);
                        return true;
                    }
                }
            }
            switch (keyData)
            {
                case Keys.Control | Keys.S:
                    Kaydet();
                    return true;
                case Keys.Control | Keys.N:
                    Yeni();
                    return true;
                case Keys.Control | Keys.F:
                    AramayaGit();
                    return true;
                case Keys.F5:
                    Yenile(SeciliId(), true);
                    return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        private void AramayaGit()
        {
            txtAra.Focus();
            txtAra.SelectionStart = 0;
            txtAra.SelectionLength = txtAra.Text.Length;
        }

        private void GridSecimDegisti(object sender, EventArgs e)
        {
            if (_yukleniyor) return;
            if (grid.SelectedRows.Count == 0) return;
            DataRowView rv = grid.SelectedRows[0].DataBoundItem as DataRowView;
            if (rv == null) return;
            FormaYukle(Kalip.SatirdanOku(rv.Row));
        }

        private int SeciliId() { return _mevcut != null ? _mevcut.Id : 0; }

        private bool Yetki(string anahtar)
        {
            if (Oturum.Aktif.IzinVar(anahtar)) return true;
            Mesajlar.Uyari("Bu işlem için yetkiniz yok.");
            return false;
        }

        private void Bildir(BildirimTuru tur, string baslik, string metin)
        {
            Bildirim.Goster(this, tur, baslik, metin);
        }

        private void Durum(string metin) { lblMesaj.Text = metin; }

        // =========================================================== LİSTE

        private void Yenile(int seciliId, bool bildirimGoster = false)
        {
            string alan = cmbAlan.SelectedValue == null ? "" : Convert.ToString(cmbAlan.SelectedValue);
            _limit = LimitDegeri();
            try
            {
                using (new Bekleme())
                {
                    _tablo = KalipRepository.Ara(alan, txtAra.Text, _limit);
                    _tablo.PrimaryKey = new DataColumn[] { _tablo.Columns["Id"] };
                }
            }
            catch (Exception ex)
            {
                HataGunlugu.Yaz(ex);
                Mesajlar.Hata("Liste okunamadı:\r\n\r\n" + Db.AnlasilirMesaj(ex));
                return;
            }

            _yukleniyor = true;
            try
            {
                grid.DataSource = _tablo.DefaultView;
                grid.ClearSelection();
            }
            finally
            {
                _yukleniyor = false;
            }

            DataView goruntu = _tablo.DefaultView;
            SayaclariYaz();

            if (bildirimGoster)
            {
                string aranan = txtAra.Text.Trim();
                string ek = "";
                if (_limit > 0 && goruntu.Count >= _limit)
                    ek = "\r\nListe en yeni " + _limit.ToString("N0", Kultur.Tr) + " kayıtla sınırlı ('Göster' kutusundan değiştirebilirsiniz).";
                if (aranan.Length > 0)
                    Bildir(BildirimTuru.Bilgi, "Arama tamamlandı", "'" + aranan + "' aramasıyla " + goruntu.Count.ToString("N0", Kultur.Tr) + " kayıt bulundu." + ek);
                else
                    Bildir(BildirimTuru.Bilgi, "Liste yenilendi", goruntu.Count.ToString("N0", Kultur.Tr) + " kayıt listelendi." + ek);
            }
            if (seciliId > 0 && SatiriSec(seciliId)) return;
            FormuTemizle();
            if (seciliId > 0) Durum("Kayıt mevcut filtreye uymuyor; listede görünmüyor.");
        }

        private int LimitDegeri()
        {
            switch (cmbLimit.SelectedIndex)
            {
                case 0: return 500;
                case 1: return 2000;
                case 2: return 10000;
                default: return 0;
            }
        }

        /// <summary>Kayıt sayısı ve ortalama dövme adedi (alt çubuk).</summary>
        private void SayaclariYaz()
        {
            DataView goruntu = _tablo.DefaultView;
            bool sinirli = _limit > 0 && goruntu.Count >= _limit;
            lblSayi.Text = "Kayıt: " + goruntu.Count.ToString("N0", Kultur.Tr) +
                           (sinirli ? "  (en yeni " + _limit.ToString("N0", Kultur.Tr) + " ile sınırlı)" : "");
            object ort = _tablo.Compute("AVG(DovmeAdeti)", "");
            lblOrt.Text = "Ort. dövme adeti: " + (ort == null || ort == DBNull.Value ? "-" : Math.Round(Convert.ToDouble(ort), 1).ToString("0.#", Kultur.Tr));
        }

        /// <summary>Tek kaydı veritabanından okuyup listedeki satırı yerinde günceller (yoksa en üste ekler, silinmişse kaldırır).</summary>
        private void SatiriYerlestir(int id)
        {
            if (_tablo == null) return;
            DataTable dt = KalipRepository.Satir(id);
            DataRow hedef = _tablo.Rows.Find(Convert.ToInt64(id));
            if (dt.Rows.Count == 0)
            {
                if (hedef != null)
                {
                    hedef.Delete();
                    _tablo.AcceptChanges();
                }
                return;
            }
            if (hedef == null)
            {
                DataRow yeni = _tablo.NewRow();
                yeni.ItemArray = dt.Rows[0].ItemArray;
                _tablo.Rows.InsertAt(yeni, 0);
            }
            else
            {
                hedef.ItemArray = dt.Rows[0].ItemArray;
            }
            _tablo.AcceptChanges();
        }

        /// <summary>Kaydı tazeler, listede seçer, formu güncel bilgiyle doldurur; istenirse arama kutusunu temizleyip imleci oraya koyar.</summary>
        private void KaydiGoster(int id, bool aramayiTemizle)
        {
            try
            {
                SatiriYerlestir(id);
            }
            catch (Exception ex)
            {
                HataGunlugu.Yaz(ex);
                Yenile(id);
                return;
            }
            DataRow r = _tablo.Rows.Find(Convert.ToInt64(id));
            if (r == null)
            {
                FormuTemizle();
            }
            else
            {
                SatiriSec(id);
                FormaYukle(Kalip.SatirdanOku(r));
            }
            SayaclariYaz();
            if (aramayiTemizle)
            {
                txtAra.Text = "";
                txtAra.Focus();
            }
        }

        private void SatirSil(int id)
        {
            DataRow r = _tablo.Rows.Find(Convert.ToInt64(id));
            _yukleniyor = true;
            try
            {
                if (r != null)
                {
                    r.Delete();
                    _tablo.AcceptChanges();
                }
                grid.ClearSelection();
            }
            finally
            {
                _yukleniyor = false;
            }
            FormuTemizle();
            SayaclariYaz();
        }

        /// <summary>Id'si verilen satırı listede seçer (seçilince form otomatik dolar). Bulunamazsa false.</summary>
        private bool SatiriSec(int id)
        {
            DataView v = _tablo.DefaultView;
            for (int i = 0; i < v.Count; i++)
            {
                if (Convert.ToInt32(v[i]["Id"]) == id)
                {
                    grid.CurrentCell = grid.Rows[i].Cells[0];
                    grid.Rows[i].Selected = true;
                    try { grid.FirstDisplayedScrollingRowIndex = Math.Max(0, i - 4); } catch { }
                    return true;
                }
            }
            return false;
        }

        // =========================================================== FORM

        private void ModGuncelle()
        {
            if (_mevcut == null)
            {
                lblMod.Text = "Yeni kayıt";
                lblMeta.Text = "Bilgileri doldurup 'Yeni Kaydet'e basın.";
                lblSecili.Text = "";
            }
            else
            {
                lblMod.Text = "Kayıt #" + _mevcut.Id.ToString(Kultur.Inv) + " düzenleniyor";
                string meta = "";
                if (_mevcut.SonDegisiklik.HasValue) meta = "Son değişiklik: " + _mevcut.SonDegisiklik.Value.ToString("dd.MM.yyyy HH:mm", Kultur.Tr);
                if (!string.IsNullOrEmpty(_mevcut.SonKullanici)) meta += "  •  " + _mevcut.SonKullanici;
                lblMeta.Text = meta;
                lblSecili.Text = "Seçili: #" + _mevcut.Id.ToString(Kultur.Inv) + "  " + (_mevcut.ParcaKodu ?? "");
            }
        }

        private static void TarihYaz(Guna2DateTimePicker dt, DateTime? v)
        {
            if (v.HasValue)
            {
                dt.Value = v.Value;
                dt.Checked = true;
            }
            else
            {
                dt.Checked = false;
            }
        }

        private static DateTime? TarihOku(Guna2DateTimePicker dt)
        {
            if (dt.Checked) return dt.Value.Date;
            return null;
        }

        private void FormaYukle(Kalip k)
        {
            _mevcut = k;
            txtParcaKodu.Text = k.ParcaKodu ?? "";
            cmbKalipTuru.Text = k.KalipTuru ?? "";
            txtTepeKodu.Text = k.TepeKodu ?? "";
            txtParcaAdi.Text = k.ParcaAdi ?? "";
            TarihYaz(dtGelis, k.GelisTarihi);
            SayiAlani.Yaz(txtTashih, k.TashihSayi);
            TarihYaz(dtBitis, k.BitisTarihi);
            cmbTur.Text = k.Tur ?? "";
            SayiAlani.Yaz(txtDovme, k.DovmeAdeti);
            SayiAlani.Yaz(txtCnc, k.CncNo);
            txtAciklama.Text = k.Aciklama ?? "";
            SayiAlani.Yaz(sureKutulari[0], k.IslemSuresi);
            SayiAlani.Yaz(sureKutulari[1], k.Ayar);
            SayiAlani.Yaz(sureKutulari[2], k.Bakim);
            SayiAlani.Yaz(sureKutulari[3], k.ElemanEksikligi);
            SayiAlani.Yaz(sureKutulari[4], k.Elektrik);
            SayiAlani.Yaz(sureKutulari[5], k.KalipAriza);
            SayiAlani.Yaz(sureKutulari[6], k.Takim);
            SayiAlani.Yaz(sureKutulari[7], k.Ariza);
            HaftaGuncelle();
            ToplamGuncelle();
            ModGuncelle();
        }

        private void FormuTemizle()
        {
            _mevcut = null;
            txtParcaKodu.Text = "";
            cmbKalipTuru.Text = "";
            txtTepeKodu.Text = "";
            txtParcaAdi.Text = "";
            dtGelis.Checked = false;
            txtTashih.Text = "";
            dtBitis.Checked = false;
            cmbTur.Text = "";
            txtDovme.Text = "";
            txtCnc.Text = "";
            txtAciklama.Text = "";
            foreach (Guna2TextBox s in sureKutulari) s.Text = "";
            txtHafta.Text = "";
            txtToplam.Text = "";
            ModGuncelle();
        }

        private void HaftaGuncelle()
        {
            txtHafta.Text = dtBitis.Checked ? TarihYardimci.HaftaHesapla(dtBitis.Value.Date) : "";
        }

        private void ToplamGuncelle()
        {
            double? toplam = ToplamHesapla();
            txtToplam.Text = toplam.HasValue ? Sayi.Yaz2(toplam.Value) : "";
        }

        private double? ToplamHesapla()
        {
            double? t = null;
            foreach (Guna2TextBox s in sureKutulari)
            {
                double? d = SayiAlani.Deger(s);
                if (d.HasValue) t = (t ?? 0.0) + d.Value;
            }
            return t.HasValue ? (double?)Math.Round(t.Value, 2, MidpointRounding.AwayFromZero) : null;
        }

        private bool SayiAl(Guna2TextBox kutu, string ad, out double? sonuc, bool tamSayi = false)
        {
            sonuc = null;
            if (!SayiAlani.Gecerli(kutu))
            {
                Mesajlar.Uyari(ad + " alanında geçerli bir sayı yazın (ör. 12 ya da 12,5).");
                kutu.Focus();
                return false;
            }
            sonuc = SayiAlani.Deger(kutu);
            if (tamSayi && sonuc.HasValue)
            {
                if (sonuc.Value < 0 || Math.Abs(sonuc.Value - Math.Round(sonuc.Value)) > 0.000001)
                {
                    Mesajlar.Uyari(ad + " alanına 0 veya daha büyük bir tam sayı yazın.");
                    kutu.Focus();
                    return false;
                }
            }
            return true;
        }

        /// <summary>Formdaki değerleri doğrulayıp Kalip nesnesine çevirir; sorun varsa null.</summary>
        private Kalip FormdanOku()
        {
            if (string.IsNullOrWhiteSpace(txtParcaKodu.Text))
            {
                Mesajlar.Uyari("Parça kodu boş olamaz.");
                txtParcaKodu.Focus();
                return null;
            }

            Kalip k = new Kalip();
            k.ParcaKodu = txtParcaKodu.Text.Trim();
            k.KalipTuru = cmbKalipTuru.Text.Trim();
            k.TepeKodu = txtTepeKodu.Text.Trim();
            k.ParcaAdi = txtParcaAdi.Text.Trim();
            k.GelisTarihi = TarihOku(dtGelis);
            k.BitisTarihi = TarihOku(dtBitis);
            k.Tur = cmbTur.Text.Trim();
            k.Aciklama = txtAciklama.Text.Trim();

            double? d;
            if (!SayiAl(txtTashih, "Tashih sayısı", out d, true)) return null;
            k.TashihSayi = d.HasValue ? (int?)(int)Math.Round(d.Value) : null;
            if (!SayiAl(txtCnc, "CNC No", out d, true)) return null;
            k.CncNo = d.HasValue ? (int?)(int)Math.Round(d.Value) : null;
            if (!SayiAl(txtDovme, "Dövme adeti", out d)) return null;
            k.DovmeAdeti = d;

            double?[] sureler = new double?[8];
            for (int i = 0; i < 8; i++)
            {
                if (!SayiAl(sureKutulari[i], SureEtiketleri[i], out d)) return null;
                sureler[i] = d;
            }
            k.IslemSuresi = sureler[0];
            k.Ayar = sureler[1];
            k.Bakim = sureler[2];
            k.ElemanEksikligi = sureler[3];
            k.Elektrik = sureler[4];
            k.KalipAriza = sureler[5];
            k.Takim = sureler[6];
            k.Ariza = sureler[7];
            k.ToplamZaman = ToplamHesapla();
            k.Hafta = TarihYardimci.HaftaHesapla(k.BitisTarihi);

            if (k.GelisTarihi.HasValue && k.BitisTarihi.HasValue && k.BitisTarihi.Value < k.GelisTarihi.Value)
            {
                if (!Mesajlar.Soru("Bitiş tarihi, geliş tarihinden önce görünüyor. Yine de kaydedilsin mi?")) return null;
            }
            return k;
        }

        // =========================================================== İŞLEMLER

        private void Yeni()
        {
            _yukleniyor = true;
            try { grid.ClearSelection(); }
            finally { _yukleniyor = false; }
            FormuTemizle();
            txtParcaKodu.Focus();
            Durum("Yeni kayıt hazır.");
            Bildir(BildirimTuru.Bilgi, "Boş form hazır", "Bilgileri girip 'Yeni Kaydet'e basın.");
        }

        /// <summary>Ctrl+S: kayıt seçiliyse günceller, değilse yeni kayıt olarak ekler.</summary>
        private void Kaydet()
        {
            if (_mevcut != null) GuncelleTiklandi();
            else YeniKaydetTiklandi();
        }

        /// <summary>GÜNCELLE: seçili kaydın üzerine yazar.</summary>
        private void GuncelleTiklandi()
        {
            if (_mevcut == null)
            {
                Mesajlar.Bilgi("Güncellemek için önce listeden bir kayıt seçin.\r\nYeni satır eklemek için 'Yeni Kaydet'i kullanın.");
                return;
            }
            if (!Yetki(Izin.Guncelle)) return;
            Kalip k = FormdanOku();
            if (k == null) return;
            k.Id = _mevcut.Id;
            k.Surum = _mevcut.Surum;
            try
            {
                bool basarili;
                using (new Bekleme())
                {
                    basarili = KalipRepository.Guncelle(k, Oturum.Aktif.KullaniciAdi);
                }
                if (basarili)
                {
                    KaydiGoster(k.Id, true);
                    Durum("Kayıt güncellendi (#" + k.Id.ToString(Kultur.Inv) + ").");
                    Bildir(BildirimTuru.Basari, "Kayıt güncellendi", "#" + k.Id.ToString(Kultur.Inv) + "  " + k.ParcaKodu + " yeni bilgilerle kaydedildi.");
                }
                else
                {
                    CakismaUyarisi();
                    KaydiGoster(k.Id, false);
                }
            }
            catch (Exception ex)
            {
                HataGunlugu.Yaz(ex);
                Mesajlar.Hata("Kayıt güncellenemedi:\r\n\r\n" + Db.AnlasilirMesaj(ex));
            }
        }

        private void CakismaUyarisi()
        {
            Mesajlar.Uyari("Bu kayıt, siz açtıktan sonra başka bir kullanıcı tarafından değiştirilmiş ya da silinmiş.\r\n\r\n" +
                           "Hiçbir şey kaydedilmedi. Liste yenileniyor; güncel halini kontrol edip tekrar deneyin.");
        }

        /// <summary>YENİ KAYDET: formdaki bilgileri her zaman yeni bir satır olarak ekler.</summary>
        private void YeniKaydetTiklandi()
        {
            if (!Yetki(Izin.Ekle)) return;
            Kalip k = FormdanOku();
            if (k == null) return;
            try
            {
                int yeniId;
                using (new Bekleme())
                {
                    yeniId = KalipRepository.Ekle(k, Oturum.Aktif.KullaniciAdi);
                }
                KaydiGoster(yeniId, true);
                Durum("Yeni kayıt eklendi (#" + yeniId.ToString(Kultur.Inv) + ").");
                Bildir(BildirimTuru.Basari, "Yeni kayıt eklendi", "#" + yeniId.ToString(Kultur.Inv) + "  " + k.ParcaKodu + " listeye eklendi.");
            }
            catch (Exception ex)
            {
                HataGunlugu.Yaz(ex);
                Mesajlar.Hata("Kayıt eklenemedi:\r\n\r\n" + Db.AnlasilirMesaj(ex));
            }
        }

        private void SilTiklandi()
        {
            if (_mevcut == null)
            {
                Mesajlar.Bilgi("Önce listeden silinecek kaydı seçin.");
                return;
            }
            if (!Yetki(Izin.Sil)) return;
            int id = _mevcut.Id;
            string parca = _mevcut.ParcaKodu;
            if (!Mesajlar.Soru("#" + id.ToString(Kultur.Inv) + " numaralı kayıt (" + parca + ") silinecek. Emin misiniz?")) return;
            try
            {
                bool silindi;
                using (new Bekleme())
                {
                    silindi = KalipRepository.Sil(id);
                }
                SatirSil(id);
                Durum(silindi ? "Kayıt silindi." : "Kayıt zaten silinmiş.");
                if (silindi) Bildir(BildirimTuru.Basari, "Kayıt silindi", "#" + id.ToString(Kultur.Inv) + "  " + parca + " silindi.");
                else Bildir(BildirimTuru.Uyari, "Kayıt bulunamadı", "#" + id.ToString(Kultur.Inv) + " numaralı kayıt zaten silinmiş.");
            }
            catch (Exception ex)
            {
                HataGunlugu.Yaz(ex);
                Mesajlar.Hata("Kayıt silinemedi:\r\n\r\n" + Db.AnlasilirMesaj(ex));
            }
        }

        /// <summary>
        /// GÜNCELLE + TASHİH KAYDI (eski "Güncelle Kaydet"): seçili kaydı günceller, ardından aynı parçadan
        /// yeni bir TASHİH kaydı ekler: geliş tarihi = işlem tarihi, tashih sayısı +1, süreler/CNC/açıklama boş.
        /// </summary>
        private void GuncelleVeTashihTiklandi()
        {
            if (_mevcut == null)
            {
                Mesajlar.Bilgi("Önce listeden kaydı seçin.\r\nBu düğme seçili kaydı günceller, ardından yeni bir tashih kaydı açar.");
                return;
            }
            if (!Yetki(Izin.Tashih)) return;
            if (!dtIslem.Checked)
            {
                Mesajlar.Uyari("Önce 'İşlem Tarihi' kutusunu işaretleyip tarihi girin.\r\nTashih kaydının geliş tarihi olarak kullanılır.");
                dtIslem.Focus();
                return;
            }
            Kalip k = FormdanOku();
            if (k == null) return;
            k.Id = _mevcut.Id;
            k.Surum = _mevcut.Surum;
            string kullanici = Oturum.Aktif.KullaniciAdi;
            bool guncellendi = false;
            try
            {
                bool basarili;
                using (new Bekleme())
                {
                    basarili = KalipRepository.Guncelle(k, kullanici);
                }
                if (!basarili)
                {
                    CakismaUyarisi();
                    KaydiGoster(k.Id, false);
                    return;
                }
                guncellendi = true;

                Kalip y = new Kalip();
                y.ParcaKodu = k.ParcaKodu;
                y.KalipTuru = k.KalipTuru;
                y.TepeKodu = k.TepeKodu;
                y.ParcaAdi = k.ParcaAdi;
                y.GelisTarihi = dtIslem.Value.Date;
                y.TashihSayi = (k.TashihSayi ?? 0) + 1;
                y.Tur = "TASHİH";
                int yeniId;
                using (new Bekleme())
                {
                    yeniId = KalipRepository.Ekle(y, kullanici);
                }
                SatiriYerlestir(k.Id);
                KaydiGoster(yeniId, true);
                Durum("#" + k.Id.ToString(Kultur.Inv) + " güncellendi; tashih kaydı #" + yeniId.ToString(Kultur.Inv) +
                      " açıldı (tashih sayısı " + y.TashihSayi.Value.ToString(Kultur.Inv) + ").");
                Bildir(BildirimTuru.Basari, "Güncellendi ve tashih kaydı açıldı",
                       "#" + k.Id.ToString(Kultur.Inv) + " güncellendi.\r\nYeni tashih kaydı: #" + yeniId.ToString(Kultur.Inv) +
                       " (tashih sayısı " + y.TashihSayi.Value.ToString(Kultur.Inv) + ", geliş " + y.GelisTarihi.Value.ToString("dd.MM.yyyy", Kultur.Tr) + ").");
            }
            catch (Exception ex)
            {
                HataGunlugu.Yaz(ex);
                string ek = "";
                if (guncellendi) ek = "\r\n\r\nNot: Seçili kayıt güncellendi, ancak tashih kaydı eklenemedi (F5 ile listeyi yenileyin).";
                Mesajlar.Hata("İşlem tamamlanamadı:\r\n\r\n" + Db.AnlasilirMesaj(ex) + ek);
            }
        }

        /// <summary>
        /// TEMİZLE (eski CommandButton7): süreleri, CNC, dövme adeti ve açıklamayı temizler, geliş tarihini işlem tarihi yapar,
        /// tashih sayısını 1 artırır. Kaydedilmez. Seçili kayıt (üstteki numara) korunur: 'Güncelle' onun üzerine yazar,
        /// 'Yeni Kaydet' yeni satır ekler (eski sistemdeki gibi).
        /// </summary>
        private void TemizleTiklandi()
        {
            if (!Yetki(Izin.Tashih)) return;
            double? d = SayiAlani.Deger(txtTashih);
            double yeniSayi = (d ?? 0.0) + 1;

            if (dtIslem.Checked) TarihYaz(dtGelis, dtIslem.Value.Date);
            else dtGelis.Checked = false;
            dtBitis.Checked = false;
            SayiAlani.Yaz(txtTashih, yeniSayi);
            txtDovme.Text = "";
            txtCnc.Text = "";
            txtAciklama.Text = "";
            foreach (Guna2TextBox s in sureKutulari) s.Text = "";
            HaftaGuncelle();
            ToplamGuncelle();
            ModGuncelle();
            if (_mevcut != null)
            {
                lblMeta.Text = "Form temizlendi, henüz kaydedilmedi. 'Güncelle' bu kaydın üzerine yazar, 'Yeni Kaydet' yeni satır ekler.";
            }
            txtCnc.Focus();
            Durum("Süreler temizlendi, tashih sayısı artırıldı. Kaydetmek için 'Yeni Kaydet' (ya da kaydın üzerine yazmak için 'Güncelle').");
            Bildir(BildirimTuru.Uyari, "Form temizlendi (kaydedilmedi)",
                   "Süreler temizlendi, tashih sayısı " + Sayi.Yaz(yeniSayi) + " oldu.\r\nKaydetmek için 'Yeni Kaydet' ya da 'Güncelle'ye basın.");
        }

        /// <summary>TARİH düğmesi: işlem tarihini Bitiş Tarihi alanına yazar.</summary>
        private void TarihDugmesi()
        {
            if (!dtIslem.Checked)
            {
                Mesajlar.Uyari("Önce 'İşlem Tarihi' kutusunu işaretleyip tarihi girin.");
                dtIslem.Focus();
                return;
            }
            TarihYaz(dtBitis, dtIslem.Value.Date);
            HaftaGuncelle();
            Durum("Bitiş tarihi işlem tarihine ayarlandı. Kaydetmeyi unutmayın.");
            Bildir(BildirimTuru.Bilgi, "Bitiş tarihi ayarlandı",
                   dtIslem.Value.ToString("dd.MM.yyyy", Kultur.Tr) + " tarihi Bitiş Tarihi'ne yazıldı.\r\nKaydetmek için 'Güncelle'ye basın.");
        }

        private void ExceleAktar()
        {
            if (!Yetki(Izin.Excel)) return;
            DataView gorunum = grid.DataSource as DataView;
            if (gorunum == null || gorunum.Count == 0)
            {
                Mesajlar.Bilgi("Aktarılacak kayıt yok.");
                return;
            }
            using (SaveFileDialog dlg = new SaveFileDialog())
            {
                dlg.Filter = "Excel Çalışma Kitabı (*.xlsx)|*.xlsx";
                dlg.FileName = "KalipListesi_" + DateTime.Now.ToString("yyyyMMdd_HHmm", Kultur.Inv) + ".xlsx";
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    using (new Bekleme())
                    {
                        XlsxYazici.Yaz(dlg.FileName, gorunum, KalipRepository.Kolonlar);
                    }
                }
                catch (Exception ex)
                {
                    HataGunlugu.Yaz(ex);
                    Mesajlar.Hata("Excel dosyası yazılamadı:\r\n\r\n" + ex.Message);
                    return;
                }
                Durum(gorunum.Count.ToString("N0", Kultur.Tr) + " kayıt Excel'e aktarıldı.");
                Bildir(BildirimTuru.Basari, "Excel dosyası oluşturuldu", gorunum.Count.ToString("N0", Kultur.Tr) + " kayıt şuraya kaydedildi:\r\n" + dlg.FileName);
                if (Mesajlar.Soru("Liste Excel'e aktarıldı. Dosya şimdi açılsın mı?"))
                {
                    try { Process.Start(dlg.FileName); } catch { }
                }
            }
        }

        private void IstatistikAc()
        {
            if (!Yetki(Izin.Istatistik)) return;
            using (frmIstatistik f = new frmIstatistik(grid.DataSource as DataView))
            {
                f.ShowDialog(this);
            }
        }
    }
}
