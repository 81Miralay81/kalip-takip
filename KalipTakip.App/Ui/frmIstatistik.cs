using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Guna.UI2.WinForms;
using DV = System.Windows.Forms.DataVisualization.Charting;

namespace KalipTakip.Arayuz
{
    /// <summary>İstatistik: kalıp türüne göre YENİ / İLAVE / TASHİH adetleri (ana ekrandaki liste ya da tarih aralığı).</summary>
    [DesignerCategory("Code")]
    public class frmIstatistik : TabanForm
    {
        private readonly DataView _liste;
        private Guna2RadioButton rbListe, rbTarih;
        private Guna2DateTimePicker dtBas, dtBit;
        private Label lblKaynak;
        private KpiKarti kpiYeni, kpiIlave, kpiTashih, kpiToplam;
        private Guna2DataGridView grid;
        private DV.Chart grafik;

        private const string YeniMetni = "YENİ";
        private const string IlaveMetni = "İLAVE";
        private const string TashihMetni = "TASHİH";

        public frmIstatistik(DataView liste)
            : base("İstatistik", "Kalıp türüne göre yeni / ilave / tashih adetleri", new Size(1260, 780), true)
        {
            _liste = liste;
            MinimumSize = new Size(1000, 660);
            StartPosition = FormStartPosition.CenterParent;
            SuspendLayout();

            TableLayoutPanel kok = new TableLayoutPanel();
            kok.Dock = DockStyle.Fill;
            kok.ColumnCount = 1;
            kok.RowCount = 4;
            kok.BackColor = Color.Transparent;
            kok.Padding = new Padding(14, 12, 14, 14);
            kok.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            kok.RowStyles.Add(new RowStyle(SizeType.Absolute, 66F));
            kok.RowStyles.Add(new RowStyle(SizeType.Absolute, 34F));
            kok.RowStyles.Add(new RowStyle(SizeType.Absolute, 112F));
            kok.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            // Kaynak seçimi
            Guna2Panel filtreKart = Tema.KartPaneli();
            filtreKart.Dock = DockStyle.Fill;
            filtreKart.Margin = new Padding(0, 0, 0, 6);
            FlowLayoutPanel filtre = new FlowLayoutPanel();
            filtre.Dock = DockStyle.Fill;
            filtre.WrapContents = false;
            filtre.BackColor = Color.Transparent;
            filtre.Padding = new Padding(16, 14, 16, 0);

            rbListe = Tema.Secenek("Ana ekrandaki liste");
            rbListe.Checked = true;
            rbListe.Margin = new Padding(0, 6, 24, 0);
            rbTarih = Tema.Secenek("Tarih aralığı (tüm kayıtlar, bitiş tarihine göre):");
            rbTarih.Margin = new Padding(0, 6, 12, 0);

            dtBas = Tema.Tarih();
            dtBas.Width = 150;
            dtBas.Margin = new Padding(0, 1, 8, 0);
            Label tire = Tema.Etiket("–", Tema.Kalin, Tema.Yazi);
            tire.Margin = new Padding(0, 8, 8, 0);
            dtBit = Tema.Tarih();
            dtBit.Width = 150;
            dtBit.Margin = new Padding(0, 1, 16, 0);
            Guna2GradientButton btnRapor = Tema.Buton("Raporla", Tema.Mavi, Tema.Mor, 110);
            filtre.Controls.AddRange(new Control[] { rbListe, rbTarih, dtBas, tire, dtBit, btnRapor });
            filtreKart.Controls.Add(filtre);

            lblKaynak = Tema.Etiket("", Tema.Kucuk, Tema.YaziSoluk);
            lblKaynak.AutoSize = false;
            lblKaynak.Dock = DockStyle.Fill;
            lblKaynak.TextAlign = ContentAlignment.MiddleLeft;
            lblKaynak.Padding = new Padding(6, 0, 0, 0);

            // Sayı kartları
            TableLayoutPanel kartlar = new TableLayoutPanel();
            kartlar.Dock = DockStyle.Fill;
            kartlar.ColumnCount = 4;
            kartlar.RowCount = 1;
            kartlar.BackColor = Color.Transparent;
            kartlar.Padding = new Padding(0, 4, 0, 8);
            for (int i = 0; i < 4; i++) kartlar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
            kartlar.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            kpiYeni = new KpiKarti("YENİ", Tema.Mavi, Tema.Mor);
            kpiIlave = new KpiKarti("İLAVE", Tema.Yesil, Color.FromArgb(13, 148, 136));
            kpiTashih = new KpiKarti("TASHİH", Tema.Turuncu, Tema.Pembe);
            kpiToplam = new KpiKarti("TOPLAM", Color.FromArgb(71, 85, 120), Color.FromArgb(30, 41, 90));
            kpiToplam.Margin = new Padding(0);
            kartlar.Controls.Add(kpiYeni, 0, 0);
            kartlar.Controls.Add(kpiIlave, 1, 0);
            kartlar.Controls.Add(kpiTashih, 2, 0);
            kartlar.Controls.Add(kpiToplam, 3, 0);

            // Tablo + grafik
            TableLayoutPanel icerik = new TableLayoutPanel();
            icerik.Dock = DockStyle.Fill;
            icerik.ColumnCount = 2;
            icerik.RowCount = 1;
            icerik.BackColor = Color.Transparent;
            icerik.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 48F));
            icerik.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 52F));
            icerik.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            Guna2Panel solKart = Tema.KartPaneli();
            solKart.Dock = DockStyle.Fill;
            solKart.Margin = new Padding(0, 0, 12, 0);
            solKart.Padding = new Padding(10);
            grid = Tema.Tablo();
            grid.Dock = DockStyle.Fill;
            KolonEkle("KalipTuru", "Kalıp Türü", 220, false);
            KolonEkle("Yeni", "Yeni", 70, true);
            KolonEkle("Ilave", "İlave", 70, true);
            KolonEkle("Tashih", "Tashih", 80, true);
            KolonEkle("Diger", "Diğer", 70, true);
            KolonEkle("Toplam", "Toplam", 90, true);
            solKart.Controls.Add(grid);

            Guna2Panel sagKart = Tema.KartPaneli();
            sagKart.Dock = DockStyle.Fill;
            sagKart.Margin = new Padding(0);
            sagKart.Padding = new Padding(10);
            grafik = new DV.Chart();
            grafik.Dock = DockStyle.Fill;
            grafik.MinimumSize = new Size(120, 120);   // "Height must be > 0" hatasını önler
            grafik.BackColor = Tema.Kart;
            sagKart.Controls.Add(grafik);

            icerik.Controls.Add(solKart, 0, 0);
            icerik.Controls.Add(sagKart, 1, 0);

            kok.Controls.Add(filtreKart, 0, 0);
            kok.Controls.Add(lblKaynak, 0, 1);
            kok.Controls.Add(kartlar, 0, 2);
            kok.Controls.Add(icerik, 0, 3);
            Govde.Controls.Add(kok);

            btnRapor.Click += delegate { Raporla(); };
            rbListe.CheckedChanged += delegate { KaynakDegisti(); };
            rbTarih.CheckedChanged += delegate { KaynakDegisti(); };
            Shown += delegate { Raporla(); };
            KaynakDegisti();
            ResumeLayout(false);
        }

        private void KaynakDegisti()
        {
            bool tarihModu = rbTarih.Checked;
            dtBas.Enabled = tarihModu;
            dtBit.Enabled = tarihModu;
            if (IsHandleCreated) Raporla();
        }

        private void KolonEkle(string alan, string baslik, int genislik, bool sagaYasli)
        {
            DataGridViewTextBoxColumn c = new DataGridViewTextBoxColumn();
            c.DataPropertyName = alan;
            c.Name = "c" + alan;
            c.HeaderText = baslik;
            c.Width = genislik;
            c.SortMode = DataGridViewColumnSortMode.NotSortable;
            if (sagaYasli)
            {
                c.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
                c.DefaultCellStyle.Format = "N0";
            }
            else
            {
                c.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            }
            grid.Columns.Add(c);
        }

        /// <summary>Ana ekrandaki görünümden (KalipTuru, Tur, Adet) tablosu üretir.</summary>
        private static DataTable ListedenTablo(DataView v)
        {
            Dictionary<string, int> sayac = new Dictionary<string, int>();
            const char ayirac = '\u0001';
            if (v != null)
            {
                foreach (DataRowView rv in v)
                {
                    string kalip = rv["KalipTuru"] == DBNull.Value ? "" : Convert.ToString(rv["KalipTuru"]);
                    string tur = rv["Tur"] == DBNull.Value ? "" : Convert.ToString(rv["Tur"]);
                    string anahtar = kalip + ayirac + tur;
                    int n;
                    sayac.TryGetValue(anahtar, out n);
                    sayac[anahtar] = n + 1;
                }
            }
            DataTable dt = new DataTable();
            dt.Columns.Add("KalipTuru", typeof(string));
            dt.Columns.Add("Tur", typeof(string));
            dt.Columns.Add("Adet", typeof(int));
            foreach (KeyValuePair<string, int> kv in sayac)
            {
                string[] parca = kv.Key.Split(ayirac);
                object kalip = parca[0].Length == 0 ? (object)DBNull.Value : parca[0];
                object tur = parca[1].Length == 0 ? (object)DBNull.Value : parca[1];
                dt.Rows.Add(new object[] { kalip, tur, kv.Value });
            }
            return dt;
        }

        private void Raporla()
        {
            DataTable ham;
            try
            {
                if (rbListe.Checked)
                {
                    ham = ListedenTablo(_liste);
                    int adet = _liste == null ? 0 : _liste.Count;
                    lblKaynak.Text = "Kaynak: ana ekrandaki liste (" + adet.ToString("N0", Kultur.Tr) + " kayıt). " +
                                     "Ana ekranda arama yaptıysanız ya da 'Göster' sınırı varsa yalnızca listedeki kayıtlar sayılır; tüm kayıtlar için 'Tarih aralığı'nı kullanın.";
                }
                else
                {
                    DateTime? bas = dtBas.Checked ? (DateTime?)dtBas.Value.Date : null;
                    DateTime? bit = dtBit.Checked ? (DateTime?)dtBit.Value.Date : null;
                    if (bas.HasValue && bit.HasValue && bas.Value > bit.Value)
                    {
                        Mesajlar.Uyari("Başlangıç tarihi bitiş tarihinden sonra olamaz.");
                        return;
                    }
                    using (new Bekleme())
                    {
                        ham = KalipRepository.Istatistik(bas, bit);
                    }
                    string aralik = (bas.HasValue || bit.HasValue)
                        ? (bas.HasValue ? bas.Value.ToString("dd.MM.yyyy", Kultur.Tr) : "…") + " – " +
                          (bit.HasValue ? bit.Value.ToString("dd.MM.yyyy", Kultur.Tr) : "…")
                        : "tüm tarihler";
                    lblKaynak.Text = "Kaynak: tüm kayıtlar, bitiş tarihi aralığı: " + aralik +
                                     "  (tarih kutuları işaretli değilse sınır uygulanmaz)";
                }
            }
            catch (Exception ex)
            {
                HataGunlugu.Yaz(ex);
                Mesajlar.Hata("İstatistik alınamadı:\r\n\r\n" + Db.AnlasilirMesaj(ex));
                return;
            }

            // Kalıp türü -> [yeni, ilave, tashih, diğer]
            Dictionary<string, int[]> sozluk = new Dictionary<string, int[]>();
            foreach (DataRow r in ham.Rows)
            {
                string kalip = r["KalipTuru"] == DBNull.Value ? "(Belirtilmemiş)" : Convert.ToString(r["KalipTuru"]).Trim();
                if (kalip.Length == 0) kalip = "(Belirtilmemiş)";
                string tur = r["Tur"] == DBNull.Value ? "" : Convert.ToString(r["Tur"]).Trim().ToUpper(Kultur.Tr);
                int adet = Convert.ToInt32(r["Adet"]);
                int[] dizi;
                if (!sozluk.TryGetValue(kalip, out dizi))
                {
                    dizi = new int[4];
                    sozluk[kalip] = dizi;
                }
                if (tur == YeniMetni) dizi[0] += adet;
                else if (tur == IlaveMetni) dizi[1] += adet;
                else if (tur == TashihMetni) dizi[2] += adet;
                else dizi[3] += adet;
            }

            List<KeyValuePair<string, int[]>> siralanmis = sozluk.OrderByDescending(kv => kv.Value.Sum()).ToList();

            DataTable sonuc = new DataTable();
            sonuc.Columns.Add("KalipTuru", typeof(string));
            foreach (string ad in new[] { "Yeni", "Ilave", "Tashih", "Diger", "Toplam" }) sonuc.Columns.Add(ad, typeof(int));
            int[] t = new int[4];
            foreach (KeyValuePair<string, int[]> kv in siralanmis)
            {
                sonuc.Rows.Add(kv.Key, kv.Value[0], kv.Value[1], kv.Value[2], kv.Value[3], kv.Value.Sum());
                for (int i = 0; i < 4; i++) t[i] += kv.Value[i];
            }
            int genel = t.Sum();
            sonuc.Rows.Add("TOPLAM", t[0], t[1], t[2], t[3], genel);
            grid.DataSource = sonuc;
            if (grid.Rows.Count > 0)
            {
                DataGridViewRow son = grid.Rows[grid.Rows.Count - 1];
                son.DefaultCellStyle.Font = Tema.Kalin;
                son.DefaultCellStyle.BackColor = Color.FromArgb(52, 38, 126);
                son.DefaultCellStyle.ForeColor = Color.FromArgb(253, 224, 71);
            }
            grid.ClearSelection();

            kpiYeni.Deger = t[0].ToString("N0", Kultur.Tr);
            kpiIlave.Deger = t[1].ToString("N0", Kultur.Tr);
            kpiTashih.Deger = t[2].ToString("N0", Kultur.Tr);
            kpiToplam.Deger = genel.ToString("N0", Kultur.Tr);

            GrafikCiz(siralanmis);
        }

        private void GrafikCiz(List<KeyValuePair<string, int[]>> veri)
        {
            grafik.Series.Clear();
            grafik.ChartAreas.Clear();
            grafik.Legends.Clear();
            grafik.Titles.Clear();
            grafik.BackColor = Tema.Kart;

            Color cizgi = Color.FromArgb(52, 58, 102);
            DV.ChartArea alan = new DV.ChartArea("a");
            alan.BackColor = Tema.Kart;
            alan.AxisX.MajorGrid.Enabled = false;
            alan.AxisX.Interval = 1;
            alan.AxisX.LineColor = cizgi;
            alan.AxisX.MajorTickMark.LineColor = cizgi;
            alan.AxisX.LabelStyle.Font = Tema.Kucuk;
            alan.AxisX.LabelStyle.ForeColor = Tema.Yazi;
            alan.AxisX.LabelStyle.Angle = -25;
            alan.AxisY.MajorGrid.LineColor = cizgi;
            alan.AxisY.LineColor = cizgi;
            alan.AxisY.MajorTickMark.LineColor = cizgi;
            alan.AxisY.LabelStyle.Font = Tema.Kucuk;
            alan.AxisY.LabelStyle.ForeColor = Tema.Yazi;
            alan.AxisY.LabelStyle.Format = "N0";
            grafik.ChartAreas.Add(alan);

            DV.Legend lejant = new DV.Legend("l");
            lejant.Docking = DV.Docking.Bottom;
            lejant.Font = Tema.Normal;
            lejant.ForeColor = Tema.Yazi;
            lejant.BackColor = Tema.Kart;
            grafik.Legends.Add(lejant);

            grafik.Titles.Add(new DV.Title("Kalıp türüne göre adet", DV.Docking.Top, Tema.Orta, Tema.Yazi));

            string[] adlar = { "Yeni", "İlave", "Tashih", "Diğer" };
            Color[] renkler = { Tema.Mavi, Tema.Yesil, Tema.Turuncu, Color.FromArgb(148, 163, 184) };
            for (int i = 0; i < 4; i++)
            {
                DV.Series s = new DV.Series(adlar[i]);
                s.ChartType = DV.SeriesChartType.StackedColumn;
                s.ChartArea = "a";
                s.Legend = "l";
                s.Color = renkler[i];
                foreach (KeyValuePair<string, int[]> kv in veri)
                {
                    string etiket = kv.Key.Replace("\"", "");
                    if (etiket.Length > 22) etiket = etiket.Substring(0, 22) + "…";
                    s.Points.AddXY(etiket, kv.Value[i]);
                }
                grafik.Series.Add(s);
            }
        }
    }
}
