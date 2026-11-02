using System;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using Guna.UI2.WinForms;

namespace KalipTakip.Arayuz
{
    /// <summary>
    /// Kalıp Listesi portalı: her kalıp tek satır. Satıra tıklayınca altta o kalıbın talep geçmişi,
    /// statü/lokasyon hareketleri ve Kalıp Takip (işlem/tashih) kayıtları açılır.
    /// Dışarıdan Excel/CSV ile toplu yükleme ve elle kalıp ekleme/düzenleme yapılabilir.
    /// </summary>
    [DesignerCategory("Code")]
    public class frmKalipListesi : TabanForm
    {
        private Guna2TextBox txtAra;
        private Guna2ComboBox cmbMod;
        private Label lblSayi, lblDetay;
        private Guna2DataGridView gridListe, gridTalep, gridHareket, gridTakip;
        private Guna2Button btnSekme1, btnSekme2, btnSekme3;
        private Color _sekmePasif, _sekmeAktif = Color.FromArgb(124, 58, 237);
        private DataTable _tablo;
        private readonly Timer _aramaZaman = new Timer();
        private bool _yukleniyor;
        private readonly ToolTip _ipucu = new ToolTip();

        private const int Limit = 1500;

        public frmKalipListesi()
            : base("Kalıp Listesi", "Her kalıp tek satır; satıra tıklayınca tüm geçmişi açılır", new Size(1500, 880), true)
        {
            SuspendLayout();
            MinimumSize = new Size(1240, 760);
            StartPosition = FormStartPosition.CenterScreen;

            Guna2Button btnGeri = Tema.BantButonu("Portala Dön", 118);
            btnGeri.FillColor = Color.FromArgb(150, 40, 80);
            btnGeri.HoverState.FillColor = Tema.Kirmizi;
            btnGeri.Margin = new Padding(0);
            btnGeri.Click += delegate { Close(); };
            UstSag.Controls.Add(btnGeri);

            TableLayoutPanel kok = new TableLayoutPanel();
            kok.Dock = DockStyle.Fill;
            kok.ColumnCount = 1;
            kok.RowCount = 4;
            kok.BackColor = Color.Transparent;
            kok.Padding = new Padding(14, 12, 14, 14);
            kok.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            kok.RowStyles.Add(new RowStyle(SizeType.Absolute, 70F));
            kok.RowStyles.Add(new RowStyle(SizeType.Percent, 56F));
            kok.RowStyles.Add(new RowStyle(SizeType.Absolute, 46F));
            kok.RowStyles.Add(new RowStyle(SizeType.Percent, 44F));

            kok.Controls.Add(FiltreKur(), 0, 0);
            kok.Controls.Add(ListeKur(), 0, 1);
            kok.Controls.Add(SekmeSeridiKur(), 0, 2);
            kok.Controls.Add(DetayKur(), 0, 3);
            Govde.Controls.Add(kok);
            ResumeLayout(false);

            _aramaZaman.Interval = 350;
            _aramaZaman.Tick += delegate { _aramaZaman.Stop(); Yenile(); };
            Shown += delegate { Yenile(); txtAra.Focus(); };
        }

        // ------------------------------------------------------------ yerleşim

        private Control FiltreKur()
        {
            Guna2Panel kart = Tema.KartPaneli();
            kart.Dock = DockStyle.Fill;
            kart.Margin = new Padding(0, 0, 0, 8);
            FlowLayoutPanel akis = new FlowLayoutPanel();
            akis.Dock = DockStyle.Fill;
            akis.WrapContents = false;
            akis.BackColor = Color.Transparent;
            akis.Padding = new Padding(14, 12, 14, 0);

            cmbMod = Tema.Acilir(170);
            cmbMod.Items.AddRange(new object[] { "Kalıp ile ara", "Raf ile ara" });
            cmbMod.SelectedIndex = 0;
            cmbMod.Margin = new Padding(0, 0, 10, 0);
            cmbMod.SelectedIndexChanged += delegate
            {
                txtAra.PlaceholderText = cmbMod.SelectedIndex == 1
                    ? "Raf adını yazın: o rafta bulunan tüm kalıplar listelenir..."
                    : "Kalıp kodu veya adı yazın: yalnız o kalıp listelenir...";
                Yenile();
            };
            txtAra = Tema.Kutu(100);
            txtAra.Width = 380;
            txtAra.Margin = new Padding(0, 0, 10, 0);
            txtAra.PlaceholderText = "Kalıp kodu veya adı yazın: yalnız o kalıp listelenir...";
            txtAra.IconLeft = Simge.Ara(Tema.YaziSoluk);
            txtAra.IconLeftSize = new Size(18, 18);
            txtAra.TextChanged += delegate { _aramaZaman.Stop(); _aramaZaman.Start(); };

            lblSayi = Tema.Etiket("", Tema.Normal, Tema.YaziSoluk);
            lblSayi.AutoSize = false;
            lblSayi.Size = new Size(230, 36);
            lblSayi.TextAlign = ContentAlignment.MiddleLeft;
            lblSayi.Margin = new Padding(0, 0, 10, 0);

            bool yazabilir = Oturum.Aktif.IzinVar(Izin.KalipListeEkle);
            Guna2GradientButton btnYeni = Tema.Buton("+  Yeni Kalıp", Tema.Yesil, Color.FromArgb(16, 185, 129), 140);
            Guna2GradientButton btnDuzenle = Tema.Buton("Düzenle", Tema.Mavi, Tema.Mor, 110);
            Guna2GradientButton btnYukle = Tema.Buton("Excel / CSV'den Yükle", Tema.Turkuaz, Tema.Mavi, 200);
            Guna2GradientButton btnExcel = Tema.Buton("Excel'e Aktar", Tema.Gri1, Tema.Gri2, 130);
            btnYeni.Enabled = btnDuzenle.Enabled = btnYukle.Enabled = yazabilir;
            btnExcel.Enabled = Oturum.Aktif.IzinVar(Izin.KalipListeExcel);
            foreach (Guna2GradientButton b in new Guna2GradientButton[] { btnYeni, btnDuzenle, btnYukle, btnExcel }) b.Margin = new Padding(0, 0, 8, 0);
            _ipucu.SetToolTip(btnYukle, "İlk satır başlık: Kalıp Kodu (zorunlu), Kalıp Adı, Kalıp Türü, Tepe Kodu, Lokasyon, Sahip Bölüm, Notlar");
            btnYeni.Click += delegate { TanimAc(false); };
            btnDuzenle.Click += delegate { TanimAc(true); };
            btnYukle.Click += delegate { DosyadanYukle(); };
            btnExcel.Click += delegate { ListeExcel(); };

            akis.Controls.AddRange(new Control[] { cmbMod, txtAra, lblSayi, btnYeni, btnDuzenle, btnYukle, btnExcel });
            kart.Controls.Add(akis);
            return kart;
        }

        private Control ListeKur()
        {
            Guna2Panel kart = Tema.KartPaneli();
            kart.Dock = DockStyle.Fill;
            kart.Margin = new Padding(0, 0, 0, 8);
            kart.Padding = new Padding(10);
            gridListe = Tema.Tablo();
            gridListe.Dock = DockStyle.Fill;
            gridListe.AutoGenerateColumns = false;
            foreach (KolonTanimi k in KalipListeRepository.Kolonlar) KolonEkle(gridListe, k.Alan, k.Baslik, k.Genislik, k.Tip, null);
            gridListe.SelectionChanged += delegate { Secildi(); };
            gridListe.CellDoubleClick += delegate (object s, DataGridViewCellEventArgs e) { if (e.RowIndex >= 0 && Oturum.Aktif.IzinVar(Izin.KalipListeEkle)) TanimAc(true); };
            gridListe.CellFormatting += GridBicim;
            kart.Controls.Add(gridListe);
            return kart;
        }

        private Control SekmeSeridiKur()
        {
            FlowLayoutPanel akis = new FlowLayoutPanel();
            akis.Dock = DockStyle.Fill;
            akis.WrapContents = false;
            akis.BackColor = Color.Transparent;
            akis.Padding = new Padding(2, 4, 0, 0);
            btnSekme1 = Tema.BantButonu("Talep Geçmişi", 150);
            btnSekme2 = Tema.BantButonu("Statü / Lokasyon Hareketleri", 230);
            btnSekme3 = Tema.BantButonu("Kalıp Takip Kayıtları (Tashih)", 240);
            _sekmePasif = btnSekme1.FillColor;
            btnSekme1.Click += delegate { SekmeAc(1); };
            btnSekme2.Click += delegate { SekmeAc(2); };
            btnSekme3.Click += delegate { SekmeAc(3); };
            lblDetay = Tema.Etiket("", Tema.Normal, Tema.YaziSoluk);
            lblDetay.AutoSize = false;
            lblDetay.Size = new Size(420, 34);
            lblDetay.TextAlign = ContentAlignment.MiddleLeft;
            lblDetay.Margin = new Padding(14, 0, 0, 0);
            akis.Controls.AddRange(new Control[] { btnSekme1, btnSekme2, btnSekme3, lblDetay });
            return akis;
        }

        private Control DetayKur()
        {
            Guna2Panel kart = Tema.KartPaneli();
            kart.Dock = DockStyle.Fill;
            kart.Margin = new Padding(0);
            kart.Padding = new Padding(10);

            gridTalep = Tema.Tablo();
            gridTalep.AutoGenerateColumns = false;
            KolonEkle(gridTalep, "TalepNo", "Talep No", 110, KolonTipi.Metin, null);
            KolonEkle(gridTalep, "TalepSira", "Kaçıncı Talep", 85, KolonTipi.Tam, null);
            KolonEkle(gridTalep, "TalepTarihi", "Talep Tarihi", 95, KolonTipi.Tarih, null);
            KolonEkle(gridTalep, "Bolum", "İlgili Bölüm", 120, KolonTipi.Metin, null);
            KolonEkle(gridTalep, "TalepStatu", "Talep Statü", 140, KolonTipi.Metin, null);
            KolonEkle(gridTalep, "KalipStatu", "Kalıp Statü", 120, KolonTipi.Metin, null);
            KolonEkle(gridTalep, "Lokasyon", "Lokasyon", 110, KolonTipi.Metin, null);
            KolonEkle(gridTalep, "TashihSayisi", "Tashih", 60, KolonTipi.Tam, null);
            KolonEkle(gridTalep, "Oncelik", "Öncelik", 80, KolonTipi.Metin, null);
            KolonEkle(gridTalep, "TalepEden", "Talep Eden", 100, KolonTipi.Metin, null);
            KolonEkle(gridTalep, "TerminTarihi", "Termin", 95, KolonTipi.Tarih, null);
            KolonEkle(gridTalep, "Aciklama", "Kalıba Özel Açıklama", 320, KolonTipi.Metin, null);

            gridHareket = Tema.Tablo();
            gridHareket.AutoGenerateColumns = false;
            KolonEkle(gridHareket, "Tarih", "Tarih / Saat", 130, KolonTipi.Tarih, "dd.MM.yyyy HH:mm");
            KolonEkle(gridHareket, "TalepNo", "Talep No", 110, KolonTipi.Metin, null);
            KolonEkle(gridHareket, "EskiStatu", "Eski Statü", 130, KolonTipi.Metin, null);
            KolonEkle(gridHareket, "YeniStatu", "Yeni Statü", 130, KolonTipi.Metin, null);
            KolonEkle(gridHareket, "EskiLokasyon", "Eski Lokasyon", 130, KolonTipi.Metin, null);
            KolonEkle(gridHareket, "YeniLokasyon", "Yeni Lokasyon", 130, KolonTipi.Metin, null);
            KolonEkle(gridHareket, "Kullanici", "İşlemi Yapan", 130, KolonTipi.Metin, null);

            gridTakip = Tema.Tablo();
            gridTakip.AutoGenerateColumns = false;
            KolonEkle(gridTakip, "GelisTarihi", "Geliş Tarihi", 100, KolonTipi.Tarih, null);
            KolonEkle(gridTakip, "BitisTarihi", "Bitiş Tarihi", 100, KolonTipi.Tarih, null);
            KolonEkle(gridTakip, "TashihSayi", "Tashih No", 80, KolonTipi.Tam, null);
            KolonEkle(gridTakip, "Hafta", "Hafta", 80, KolonTipi.Metin, null);
            KolonEkle(gridTakip, "Tur", "Tür", 110, KolonTipi.Metin, null);
            KolonEkle(gridTakip, "ParcaAdi", "Parça Adı", 240, KolonTipi.Metin, null);
            KolonEkle(gridTakip, "KalipTuru", "Kalıp Türü", 120, KolonTipi.Metin, null);
            KolonEkle(gridTakip, "ToplamZaman", "Toplam Zaman", 110, KolonTipi.Ondalik, "N2");

            foreach (Guna2DataGridView g in new Guna2DataGridView[] { gridTalep, gridHareket, gridTakip })
            {
                g.Dock = DockStyle.Fill;
                g.Visible = false;
                kart.Controls.Add(g);
            }
            gridTalep.CellFormatting += GridBicim;
            SekmeAc(1);
            return kart;
        }

        private static void KolonEkle(DataGridView g, string alan, string baslik, int genislik, KolonTipi tip, string bicim)
        {
            DataGridViewTextBoxColumn c = new DataGridViewTextBoxColumn();
            c.DataPropertyName = alan;
            c.Name = "c" + alan;
            c.HeaderText = baslik;
            c.AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
            c.Width = genislik + 12;
            c.MinimumWidth = 50;
            c.SortMode = DataGridViewColumnSortMode.Automatic;
            if (bicim != null) c.DefaultCellStyle.Format = bicim;
            else if (tip == KolonTipi.Tarih) c.DefaultCellStyle.Format = "dd.MM.yyyy";
            if (tip == KolonTipi.Tam || tip == KolonTipi.Ondalik) c.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            g.Columns.Add(c);
        }

        private void GridBicim(object sender, DataGridViewCellFormattingEventArgs e)
        {
            DataGridView g = sender as DataGridView;
            if (g == null || e.RowIndex < 0 || e.ColumnIndex < 0) return;
            string alan = g.Columns[e.ColumnIndex].DataPropertyName;
            string v = Convert.ToString(e.Value ?? "");
            if (alan == "KalipKodu") { e.CellStyle.ForeColor = Color.FromArgb(165, 180, 252); e.CellStyle.Font = Tema.Kalin; }
            else if (alan == "TalepNo") e.CellStyle.ForeColor = Color.FromArgb(165, 180, 252);
            else if (alan == "SonKalipStatu" || alan == "KalipStatu" || alan == "TalepStatu")
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
            }
        }

        private void SekmeAc(int n)
        {
            if (btnSekme1 == null) return;
            btnSekme1.FillColor = n == 1 ? _sekmeAktif : _sekmePasif;
            btnSekme2.FillColor = n == 2 ? _sekmeAktif : _sekmePasif;
            btnSekme3.FillColor = n == 3 ? _sekmeAktif : _sekmePasif;
            if (gridTalep != null) gridTalep.Visible = n == 1;
            if (gridHareket != null) gridHareket.Visible = n == 2;
            if (gridTakip != null) gridTakip.Visible = n == 3;
        }

        // ------------------------------------------------------------ veri

        private void Yenile()
        {
            string seciliAnahtar = SeciliAnahtar();
            try
            {
                using (new Bekleme())
                {
                    _tablo = KalipListeRepository.Ara(txtAra.Text, cmbMod.SelectedIndex, Limit);
                }
            }
            catch (Exception ex)
            {
                HataGunlugu.Yaz(ex);
                Mesajlar.Hata("Kalıp listesi okunamadı:\r\n\r\n" + Db.AnlasilirMesaj(ex));
                return;
            }
            _yukleniyor = true;
            try
            {
                gridListe.DataSource = _tablo.DefaultView;
                gridListe.ClearSelection();
                if (seciliAnahtar != null)
                {
                    foreach (DataGridViewRow r in gridListe.Rows)
                    {
                        DataRowView rv = r.DataBoundItem as DataRowView;
                        if (rv != null && Convert.ToString(rv["Anahtar"]) == seciliAnahtar) { r.Selected = true; gridListe.FirstDisplayedScrollingRowIndex = r.Index; break; }
                    }
                }
            }
            finally { _yukleniyor = false; }
            lblSayi.Text = _tablo.Rows.Count.ToString("N0", Kultur.Tr) + " kalıp" + (_tablo.Rows.Count >= Limit ? " (ilk " + Limit.ToString("N0", Kultur.Tr) + "; aramayı daraltın)" : "");
            DetayYukle(SeciliAnahtar());
        }

        private DataRow SeciliSatir()
        {
            if (gridListe.SelectedRows.Count == 0) return null;
            DataRowView rv = gridListe.SelectedRows[0].DataBoundItem as DataRowView;
            return rv == null ? null : rv.Row;
        }

        private string SeciliAnahtar()
        {
            DataRow r = SeciliSatir();
            return r == null ? null : Convert.ToString(r["Anahtar"]);
        }

        private void Secildi()
        {
            if (_yukleniyor) return;
            DetayYukle(SeciliAnahtar());
        }

        private void DetayYukle(string anahtar)
        {
            if (anahtar == null)
            {
                gridTalep.DataSource = null; gridHareket.DataSource = null; gridTakip.DataSource = null;
                lblDetay.Text = "Geçmişi görmek için bir kalıp seçin.";
                return;
            }
            try
            {
                DataTable t = KalipListeRepository.TalepGecmisi(anahtar);
                DataTable h = KalipListeRepository.Hareketler(anahtar);
                DataTable k = KalipListeRepository.TakipKayitlari(anahtar);
                gridTalep.DataSource = t.DefaultView;
                gridHareket.DataSource = h.DefaultView;
                gridTakip.DataSource = k.DefaultView;
                gridTalep.ClearSelection(); gridHareket.ClearSelection(); gridTakip.ClearSelection();
                DataRow r = SeciliSatir();
                lblDetay.Text = (r != null ? Convert.ToString(r["KalipKodu"]) + "   •   " : "") +
                    t.Rows.Count.ToString(Kultur.Tr) + " talep, " + h.Rows.Count.ToString(Kultur.Tr) + " hareket, " + k.Rows.Count.ToString(Kultur.Tr) + " takip kaydı";
            }
            catch (Exception ex)
            {
                HataGunlugu.Yaz(ex);
                lblDetay.Text = "Geçmiş okunamadı: " + Db.AnlasilirMesaj(ex);
            }
        }

        // ------------------------------------------------------------ işlemler

        private void TanimAc(bool duzenle)
        {
            if (!Oturum.Aktif.IzinVar(Izin.KalipListeEkle)) { Mesajlar.Uyari("Bu işlem için yetkiniz yok."); return; }
            DataRow r = duzenle ? SeciliSatir() : null;
            if (duzenle && r == null) { Mesajlar.Bilgi("Önce listeden bir kalıp seçin."); return; }
            using (frmKalipTanim f = new frmKalipTanim(r))
            {
                if (f.ShowDialog(this) != DialogResult.OK) return;
            }
            Yenile();
            Bildirim.Goster(this, BildirimTuru.Basari, "Kaydedildi", "Kalıp bilgisi güncellendi.");
        }

        private void DosyadanYukle()
        {
            if (!Oturum.Aktif.IzinVar(Izin.KalipListeEkle)) { Mesajlar.Uyari("Bu işlem için yetkiniz yok."); return; }
            using (OpenFileDialog dlg = new OpenFileDialog())
            {
                dlg.Title = "Kalıp listesi dosyasını seçin";
                dlg.Filter = "Excel / CSV (*.xlsx;*.xlsm;*.csv)|*.xlsx;*.xlsm;*.csv|Tüm dosyalar (*.*)|*.*";
                dlg.CheckFileExists = true;
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    string ozet;
                    using (new Bekleme())
                    {
                        ozet = KalipListeRepository.DosyadanYukle(dlg.FileName, Oturum.Aktif.KullaniciAdi);
                    }
                    Yenile();
                    Mesajlar.Basari(ozet);
                }
                catch (Exception ex)
                {
                    HataGunlugu.Yaz(ex);
                    Mesajlar.Hata("Dosya yüklenemedi:\r\n\r\n" + ex.Message);
                }
            }
        }

        private void ListeExcel()
        {
            if (!Oturum.Aktif.IzinVar(Izin.KalipListeExcel)) { Mesajlar.Uyari("Bu işlem için yetkiniz yok."); return; }
            DataView gorunum = gridListe.DataSource as DataView;
            if (gorunum == null || gorunum.Count == 0) { Mesajlar.Bilgi("Aktarılacak kalıp yok."); return; }
            using (SaveFileDialog dlg = new SaveFileDialog())
            {
                dlg.Filter = "Excel Çalışma Kitabı (*.xlsx)|*.xlsx";
                dlg.FileName = "KalipListesi_" + DateTime.Now.ToString("yyyyMMdd_HHmm", Kultur.Inv) + ".xlsx";
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    using (new Bekleme()) { XlsxYazici.Yaz(dlg.FileName, gorunum, KalipListeRepository.Kolonlar); }
                }
                catch (Exception ex)
                {
                    HataGunlugu.Yaz(ex);
                    Mesajlar.Hata("Excel dosyası yazılamadı:\r\n\r\n" + ex.Message);
                    return;
                }
                if (Mesajlar.Soru("Liste Excel'e aktarıldı. Dosya şimdi açılsın mı?"))
                {
                    try { Process.Start(dlg.FileName); } catch { }
                }
            }
        }
    }

    /// <summary>Tek kalıp ekleme / düzenleme penceresi.</summary>
    [DesignerCategory("Code")]
    public class frmKalipTanim : TabanForm
    {
        private readonly Guna2TextBox txtKod, txtAd, txtTur, txtTepe, txtNot;
        private readonly OneriKutusu txtLok, txtSahip, txtRaf;
        private readonly bool _duzenle;

        public frmKalipTanim(DataRow r)
            : base(r == null ? "Yeni kalıp" : "Kalıp düzenle", "Kalıp tanımı", new Size(720, 640), true)
        {
            _duzenle = r != null;
            StartPosition = FormStartPosition.CenterParent;
            SuspendLayout();

            txtKod = Tema.Kutu(60);
            txtAd = Tema.Kutu(120);
            txtTur = Tema.Kutu(60);
            txtTepe = Tema.Kutu(60);
            txtLok = new OneriKutusu { MaxLength = 60 };
            txtSahip = new OneriKutusu { MaxLength = 60 };
            txtRaf = new OneriKutusu { MaxLength = 60 };
            txtNot = Tema.Kutu(1000);
            txtNot.Multiline = true;
            txtNot.Height = 90;
            try
            {
                txtLok.SecenekleriYukle(LokasyonRepository.Adlar());
                txtSahip.SecenekleriYukle(LokasyonRepository.Bolumler());
                txtRaf.SecenekleriYukle(KalipListeRepository.Rafler());
            }
            catch { }

            if (r != null)
            {
                txtKod.Text = Deger(r, "KalipKodu");
                txtKod.ReadOnly = true;
                txtAd.Text = Deger(r, "KalipAdi");
                txtTur.Text = Deger(r, "KalipTuru");
                txtTepe.Text = Deger(r, "TepeKodu");
                txtLok.Text = Deger(r, "Lokasyon");
                txtSahip.Text = Deger(r, "SahipBolum");
                txtRaf.Text = Deger(r, "Raf");
                txtNot.Text = Deger(r, "Notlar");
            }

            TableLayoutPanel t = new TableLayoutPanel();
            t.Dock = DockStyle.Fill;
            t.ColumnCount = 2;
            t.BackColor = Color.Transparent;
            t.Padding = new Padding(24, 14, 24, 14);
            t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            t.RowCount = 6;
            for (int i = 0; i < 4; i++) t.RowStyles.Add(new RowStyle(SizeType.Absolute, 70F));
            t.RowStyles.Add(new RowStyle(SizeType.Absolute, 130F));
            t.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            t.Controls.Add(Alan("Kalıp kodu *", txtKod), 0, 0);
            t.Controls.Add(Alan("Kalıp / parça adı", txtAd), 1, 0);
            t.Controls.Add(Alan("Kalıp türü", txtTur), 0, 1);
            t.Controls.Add(Alan("Tepe kodu", txtTepe), 1, 1);
            t.Controls.Add(Alan("Lokasyon", txtLok), 0, 2);
            t.Controls.Add(Alan("Sahip bölüm (ana lokasyonu)", txtSahip), 1, 2);
            t.Controls.Add(Alan("Raf", txtRaf), 0, 3);
            Control not = Alan("Notlar", txtNot, 120);
            t.Controls.Add(not, 0, 4);
            t.SetColumnSpan(not, 2);

            FlowLayoutPanel dugmeler = new FlowLayoutPanel();
            dugmeler.Dock = DockStyle.Fill;
            dugmeler.BackColor = Color.Transparent;
            Guna2GradientButton btnKaydet = Tema.Buton("Kaydet", Tema.Mavi, Tema.Mor, 130, 42);
            Guna2GradientButton btnIptal = Tema.Buton("Vazgeç", Tema.Gri1, Tema.Gri2, 110, 42);
            btnKaydet.Click += delegate { Kaydet(); };
            btnIptal.Click += delegate { DialogResult = DialogResult.Cancel; };
            dugmeler.Controls.AddRange(new Control[] { btnKaydet, btnIptal });
            t.Controls.Add(dugmeler, 0, 5);
            t.SetColumnSpan(dugmeler, 2);

            Govde.Controls.Add(t);
            ResumeLayout(false);
        }

        private static string Deger(DataRow r, string alan)
        {
            if (!r.Table.Columns.Contains(alan) || r[alan] == DBNull.Value) return "";
            return Convert.ToString(r[alan]);
        }

        private static Panel Alan(string etiket, Control k, int yukseklik = 66)
        {
            Panel p = new Panel { Dock = DockStyle.Fill, Height = yukseklik, Margin = new Padding(0, 0, 12, 4), BackColor = Color.Transparent };
            Label l = new Label { Text = etiket, Dock = DockStyle.Top, Height = 22, Font = Tema.Kucuk, ForeColor = Color.FromArgb(190, 198, 232), BackColor = Color.Transparent };
            k.Dock = DockStyle.Top;
            p.Controls.Add(k);
            p.Controls.Add(l);
            return p;
        }

        private void Kaydet()
        {
            string kod = txtKod.Text.Trim();
            if (kod.Length == 0) { Mesajlar.Uyari("Kalıp kodu boş olamaz."); txtKod.Focus(); return; }
            try
            {
                KalipListeRepository.Kaydet(kod, txtAd.Text, txtTur.Text, txtTepe.Text, txtNot.Text, txtLok.Text, txtSahip.Text, txtRaf.Text, Oturum.Aktif.KullaniciAdi, _duzenle);
                DialogResult = DialogResult.OK;
            }
            catch (Exception ex)
            {
                HataGunlugu.Yaz(ex);
                Mesajlar.Hata("Kaydedilemedi:\r\n\r\n" + Db.AnlasilirMesaj(ex));
            }
        }
    }
}
