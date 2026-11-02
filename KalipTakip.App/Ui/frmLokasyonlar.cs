using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Windows.Forms;
using Guna.UI2.WinForms;

namespace KalipTakip.Arayuz
{
    /// <summary>
    /// Yönetici ekranı: her satır bir bölümdür (bölüm + ana lokasyonu). Alt lokasyonlar satırdaki düğmeyle
    /// açılan ayrı pencerede girilir. Hiçbir lokasyon bağımsız kalmaz: ya bir bölümün ana lokasyonudur ya da alt lokasyonudur.
    /// </summary>
    [DesignerCategory("Code")]
    public class frmLokasyonlar : TabanForm
    {
        private Guna2DataGridView grid;

        public frmLokasyonlar()
            : base("Bölümler ve Lokasyonlar", "Bölümü ve ana lokasyonunu yazın; alt lokasyonları satırdaki düğmeden ekleyin", new Size(980, 720), true)
        {
            MinimumSize = new Size(820, 560);
            StartPosition = FormStartPosition.CenterParent;
            SuspendLayout();

            TableLayoutPanel kok = new TableLayoutPanel();
            kok.Dock = DockStyle.Fill;
            kok.ColumnCount = 1;
            kok.RowCount = 3;
            kok.BackColor = Color.Transparent;
            kok.Padding = new Padding(14, 12, 14, 14);
            kok.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            kok.RowStyles.Add(new RowStyle(SizeType.Absolute, 74F));
            kok.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            kok.RowStyles.Add(new RowStyle(SizeType.Absolute, 58F));

            Label not = Tema.Etiket(
                "Her bölümün bir ana lokasyonu vardır: talep açılınca kalıp oraya yerleşir, sevk işlemleri oradan yapılır. Alt lokasyonlar (raf, hat vb.) ana lokasyona bağlıdır.\r\n" +
                "Bir kalıp yeni talebe yalnızca sahibi bölümün ana ya da alt lokasyonlarındayken eklenebilir.",
                Tema.Kucuk, Tema.YaziSoluk);
            not.Dock = DockStyle.Fill;
            not.AutoSize = false;

            Guna2Panel kart = Tema.KartPaneli();
            kart.Dock = DockStyle.Fill;
            kart.Margin = new Padding(0, 0, 0, 8);
            kart.Padding = new Padding(10);
            grid = Tema.Tablo();
            grid.Dock = DockStyle.Fill;
            grid.ReadOnly = false;
            grid.AllowUserToAddRows = false;
            grid.AllowUserToDeleteRows = false;
            grid.SelectionMode = DataGridViewSelectionMode.CellSelect;
            grid.MultiSelect = false;
            grid.EditMode = DataGridViewEditMode.EditOnEnter;

            DataGridViewTextBoxColumn cBolum = new DataGridViewTextBoxColumn();
            cBolum.HeaderText = "Bölüm"; cBolum.Name = "cBolum"; cBolum.Width = 260;
            DataGridViewTextBoxColumn cAna = new DataGridViewTextBoxColumn();
            cAna.HeaderText = "Ana lokasyon"; cAna.Name = "cAna"; cAna.Width = 260;
            DataGridViewButtonColumn cAlt = new DataGridViewButtonColumn();
            cAlt.HeaderText = "Alt lokasyonlar"; cAlt.Name = "cAlt"; cAlt.Width = 220;
            cAlt.FlatStyle = FlatStyle.Flat;
            grid.Columns.AddRange(new DataGridViewColumn[] { cBolum, cAna, cAlt });
            grid.EditingControlShowing += delegate (object s, DataGridViewEditingControlShowingEventArgs e)
            {
                TextBox t = e.Control as TextBox;
                if (t != null) t.CharacterCasing = CharacterCasing.Upper;
            };
            grid.CellContentClick += delegate (object s, DataGridViewCellEventArgs e)
            {
                if (e.RowIndex >= 0 && e.ColumnIndex == grid.Columns["cAlt"].Index) AltAc(grid.Rows[e.RowIndex]);
            };
            grid.CellDoubleClick += delegate (object s, DataGridViewCellEventArgs e)
            {
                if (e.RowIndex >= 0 && e.ColumnIndex != grid.Columns["cBolum"].Index && e.ColumnIndex != grid.Columns["cAna"].Index) AltAc(grid.Rows[e.RowIndex]);
            };
            kart.Controls.Add(grid);

            FlowLayoutPanel dugmeler = new FlowLayoutPanel();
            dugmeler.Dock = DockStyle.Fill;
            dugmeler.BackColor = Color.Transparent;
            dugmeler.Padding = new Padding(0, 6, 0, 0);
            Guna2GradientButton btnEkle = Tema.Buton("+  Bölüm Ekle", Tema.Yesil, Color.FromArgb(16, 185, 129), 160, 42);
            Guna2GradientButton btnKaydet = Tema.Buton("Kaydet", Tema.Mavi, Tema.Mor, 130, 42);
            Guna2GradientButton btnSatirSil = Tema.Buton("Bölümü Sil", Tema.Kirmizi, Color.FromArgb(190, 24, 93), 140, 42);
            Guna2GradientButton btnKapat = Tema.Buton("Kapat", Tema.Gri1, Tema.Gri2, 110, 42);
            btnEkle.Click += delegate { SatirEkle(); };
            btnKaydet.Click += delegate { Kaydet(); };
            btnSatirSil.Click += delegate { SatirSil(); };
            btnKapat.Click += delegate { Close(); };
            dugmeler.Controls.AddRange(new Control[] { btnEkle, btnKaydet, btnSatirSil, btnKapat });

            kok.Controls.Add(not, 0, 0);
            kok.Controls.Add(kart, 0, 1);
            kok.Controls.Add(dugmeler, 0, 2);
            Govde.Controls.Add(kok);
            ResumeLayout(false);
            Shown += delegate { Yukle(); };
        }

        private static List<string> Alt(DataGridViewRow r)
        {
            List<string> l = r.Tag as List<string>;
            if (l == null) { l = new List<string>(); r.Tag = l; }
            return l;
        }

        private void AltYaz(DataGridViewRow r)
        {
            r.Cells["cAlt"].Value = "Alt lokasyonlar (" + Alt(r).Count.ToString() + ")";
        }

        private DataGridViewRow SatirEkle()
        {
            int i = grid.Rows.Add("", "", "");
            DataGridViewRow r = grid.Rows[i];
            r.Tag = new List<string>();
            AltYaz(r);
            grid.CurrentCell = r.Cells["cBolum"];
            grid.BeginEdit(true);
            return r;
        }

        private void Yukle()
        {
            try
            {
                grid.Rows.Clear();
                Dictionary<string, DataGridViewRow> bolumler = new Dictionary<string, DataGridViewRow>(StringComparer.Ordinal);
                DataTable dt = LokasyonRepository.Listele();
                foreach (DataRow r in dt.Rows)
                {
                    string bolum = Convert.ToString(r["Bolum"]);
                    bool ana = Convert.ToInt32(r["AnaMi"]) == 1;
                    DataGridViewRow satir;
                    if (!bolumler.TryGetValue(bolum, out satir))
                    {
                        int i = grid.Rows.Add(bolum, "", "");
                        satir = grid.Rows[i];
                        satir.Tag = new List<string>();
                        bolumler[bolum] = satir;
                    }
                    if (ana) satir.Cells["cAna"].Value = Convert.ToString(r["Ad"]);
                    else Alt(satir).Add(Convert.ToString(r["Ad"]));
                }
                foreach (DataGridViewRow r in grid.Rows) AltYaz(r);
            }
            catch (Exception ex)
            {
                HataGunlugu.Yaz(ex);
                Mesajlar.Hata("Lokasyonlar okunamadı:\r\n\r\n" + Db.AnlasilirMesaj(ex));
            }
        }

        private void AltAc(DataGridViewRow r)
        {
            grid.EndEdit();
            string bolum = Convert.ToString(r.Cells["cBolum"].Value ?? "").Trim();
            string ana = Convert.ToString(r.Cells["cAna"].Value ?? "").Trim();
            if (bolum.Length == 0 || ana.Length == 0)
            {
                Mesajlar.Uyari("Alt lokasyon eklemek için önce bölümü ve ana lokasyonunu yazın.");
                return;
            }
            using (frmAltLokasyonlar f = new frmAltLokasyonlar(bolum, ana, Alt(r)))
            {
                if (f.ShowDialog(this) == DialogResult.OK)
                {
                    r.Tag = f.Sonuc;
                    AltYaz(r);
                }
            }
        }

        private void SatirSil()
        {
            if (grid.CurrentRow == null) return;
            string b = Convert.ToString(grid.CurrentRow.Cells["cBolum"].Value ?? "");
            if (!Mesajlar.Soru("'" + b + "' bölümü ve tüm lokasyonları listeden silinsin mi? (Kaydet'e basana kadar kalıcı olmaz.)")) return;
            grid.Rows.Remove(grid.CurrentRow);
        }

        private void Kaydet()
        {
            grid.EndEdit();
            List<LokasyonKaydi> liste = new List<LokasyonKaydi>();
            foreach (DataGridViewRow r in grid.Rows)
            {
                string bolum = Convert.ToString(r.Cells["cBolum"].Value ?? "").Trim();
                string ana = Convert.ToString(r.Cells["cAna"].Value ?? "").Trim();
                if (bolum.Length == 0 && ana.Length == 0 && Alt(r).Count == 0) continue;
                if (bolum.Length == 0) { Mesajlar.Uyari("Bölüm adı boş bırakılamaz."); return; }
                if (ana.Length == 0) { Mesajlar.Uyari(bolum + " bölümünün ana lokasyonunu yazın."); return; }
                LokasyonKaydi k = new LokasyonKaydi();
                k.Ad = ana; k.Bolum = bolum; k.AnaMi = true;
                liste.Add(k);
                foreach (string a in Alt(r))
                {
                    LokasyonKaydi x = new LokasyonKaydi();
                    x.Ad = a; x.Bolum = bolum; x.AnaMi = false;
                    liste.Add(x);
                }
            }
            try
            {
                string hata = LokasyonRepository.Kaydet(liste);
                if (hata != null) { Mesajlar.Uyari(hata); return; }
                Bildirim.Goster(this, BildirimTuru.Basari, "Kaydedildi", "Bölüm ve lokasyon tanımları güncellendi.");
                Yukle();
            }
            catch (Exception ex)
            {
                HataGunlugu.Yaz(ex);
                Mesajlar.Hata("Kaydedilemedi:\r\n\r\n" + Db.AnlasilirMesaj(ex));
            }
        }
    }

    /// <summary>Bir ana lokasyonun alt lokasyonlarını girdiğimiz küçük pencere.</summary>
    [DesignerCategory("Code")]
    public class frmAltLokasyonlar : TabanForm
    {
        private readonly Guna2DataGridView _grid;
        public List<string> Sonuc = new List<string>();

        public frmAltLokasyonlar(string bolum, string ana, List<string> mevcut)
            : base(ana + " — alt lokasyonlar", bolum + " bölümü", new Size(560, 560), true)
        {
            MinimumSize = new Size(480, 440);
            StartPosition = FormStartPosition.CenterParent;
            SuspendLayout();

            TableLayoutPanel kok = new TableLayoutPanel();
            kok.Dock = DockStyle.Fill;
            kok.ColumnCount = 1;
            kok.RowCount = 2;
            kok.BackColor = Color.Transparent;
            kok.Padding = new Padding(14, 12, 14, 14);
            kok.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            kok.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            kok.RowStyles.Add(new RowStyle(SizeType.Absolute, 58F));

            Guna2Panel kart = Tema.KartPaneli();
            kart.Dock = DockStyle.Fill;
            kart.Margin = new Padding(0, 0, 0, 8);
            kart.Padding = new Padding(10);
            _grid = Tema.Tablo();
            _grid.Dock = DockStyle.Fill;
            _grid.ReadOnly = false;
            _grid.AllowUserToAddRows = false;
            _grid.SelectionMode = DataGridViewSelectionMode.CellSelect;
            _grid.MultiSelect = false;
            _grid.EditMode = DataGridViewEditMode.EditOnEnter;
            DataGridViewTextBoxColumn c = new DataGridViewTextBoxColumn();
            c.HeaderText = "Alt lokasyon (" + ana + " altında)"; c.Name = "cAd"; c.Width = 460;
            c.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _grid.Columns.Add(c);
            _grid.EditingControlShowing += delegate (object s, DataGridViewEditingControlShowingEventArgs e)
            {
                TextBox t = e.Control as TextBox;
                if (t != null) t.CharacterCasing = CharacterCasing.Upper;
            };
            foreach (string a in mevcut) _grid.Rows.Add(a);
            kart.Controls.Add(_grid);

            FlowLayoutPanel dugmeler = new FlowLayoutPanel();
            dugmeler.Dock = DockStyle.Fill;
            dugmeler.BackColor = Color.Transparent;
            dugmeler.Padding = new Padding(0, 6, 0, 0);
            Guna2GradientButton btnEkle = Tema.Buton("+  Alt Lokasyon", Tema.Yesil, Color.FromArgb(16, 185, 129), 160, 42);
            Guna2GradientButton btnSil = Tema.Buton("Sil", Tema.Kirmizi, Color.FromArgb(190, 24, 93), 90, 42);
            Guna2GradientButton btnTamam = Tema.Buton("Tamam", Tema.Mavi, Tema.Mor, 110, 42);
            Guna2GradientButton btnIptal = Tema.Buton("Vazgeç", Tema.Gri1, Tema.Gri2, 110, 42);
            btnEkle.Click += delegate
            {
                int i = _grid.Rows.Add("");
                _grid.CurrentCell = _grid.Rows[i].Cells[0];
                _grid.BeginEdit(true);
            };
            btnSil.Click += delegate { if (_grid.CurrentRow != null) _grid.Rows.Remove(_grid.CurrentRow); };
            btnTamam.Click += delegate
            {
                _grid.EndEdit();
                List<string> l = new List<string>();
                foreach (DataGridViewRow r in _grid.Rows)
                {
                    string v = Convert.ToString(r.Cells[0].Value ?? "").Trim();
                    if (v.Length > 0 && !l.Contains(v)) l.Add(v);
                }
                Sonuc = l;
                DialogResult = DialogResult.OK;
            };
            btnIptal.Click += delegate { DialogResult = DialogResult.Cancel; };
            dugmeler.Controls.AddRange(new Control[] { btnEkle, btnSil, btnTamam, btnIptal });

            kok.Controls.Add(kart, 0, 0);
            kok.Controls.Add(dugmeler, 0, 1);
            Govde.Controls.Add(kok);
            ResumeLayout(false);
        }
    }
}
