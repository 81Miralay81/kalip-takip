using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Windows.Forms;
using Guna.UI2.WinForms;

namespace KalipTakip.Arayuz
{
    /// <summary>Raf tanımları: raf adlarını buradan oluşturun; kalıplara yalnızca bu raflar atanabilir.</summary>
    [DesignerCategory("Code")]
    public class frmRaflar : TabanForm
    {
        private Guna2DataGridView grid;

        public frmRaflar()
            : base("Raflar", "Raf adlarını tanımlayın; kalıp listesinden ve telefondan bu raflar seçilir", new Size(640, 700), true)
        {
            MinimumSize = new Size(520, 480);
            StartPosition = FormStartPosition.CenterParent;
            SuspendLayout();

            TableLayoutPanel kok = new TableLayoutPanel();
            kok.Dock = DockStyle.Fill;
            kok.ColumnCount = 1;
            kok.RowCount = 3;
            kok.BackColor = Color.Transparent;
            kok.Padding = new Padding(14, 12, 14, 14);
            kok.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            kok.RowStyles.Add(new RowStyle(SizeType.Absolute, 50F));
            kok.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            kok.RowStyles.Add(new RowStyle(SizeType.Absolute, 58F));

            Label not = Tema.Etiket("Yeni satır açıp raf adını yazın (ör. A-01, B-12). İçinde kalıp bulunan raf silinemez.", Tema.Kucuk, Tema.YaziSoluk);
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
            DataGridViewTextBoxColumn cAd = new DataGridViewTextBoxColumn();
            cAd.HeaderText = "Raf"; cAd.Name = "cAd"; cAd.Width = 300;
            DataGridViewTextBoxColumn cSay = new DataGridViewTextBoxColumn();
            cSay.HeaderText = "Rafta kalıp"; cSay.Name = "cSay"; cSay.Width = 140; cSay.ReadOnly = true;
            grid.Columns.AddRange(new DataGridViewColumn[] { cAd, cSay });
            grid.EditingControlShowing += delegate (object s, DataGridViewEditingControlShowingEventArgs e)
            {
                TextBox t = e.Control as TextBox;
                if (t != null) t.CharacterCasing = CharacterCasing.Upper;
            };
            kart.Controls.Add(grid);

            FlowLayoutPanel dugmeler = new FlowLayoutPanel();
            dugmeler.Dock = DockStyle.Fill;
            dugmeler.BackColor = Color.Transparent;
            dugmeler.Padding = new Padding(0, 6, 0, 0);
            Guna2GradientButton btnEkle = Tema.Buton("+  Raf Ekle", Tema.Yesil, Color.FromArgb(16, 185, 129), 150, 42);
            Guna2GradientButton btnKaydet = Tema.Buton("Kaydet", Tema.Mavi, Tema.Mor, 130, 42);
            Guna2GradientButton btnSil = Tema.Buton("Rafı Sil", Tema.Kirmizi, Color.FromArgb(190, 24, 93), 130, 42);
            Guna2GradientButton btnKapat = Tema.Buton("Kapat", Tema.Gri1, Tema.Gri2, 110, 42);
            btnEkle.Click += delegate { SatirEkle(); };
            btnKaydet.Click += delegate { Kaydet(); };
            btnSil.Click += delegate
            {
                if (grid.CurrentRow == null) return;
                grid.EndEdit();
                grid.Rows.Remove(grid.CurrentRow);
            };
            btnKapat.Click += delegate { Close(); };
            dugmeler.Controls.AddRange(new Control[] { btnEkle, btnKaydet, btnSil, btnKapat });

            kok.Controls.Add(not, 0, 0);
            kok.Controls.Add(kart, 0, 1);
            kok.Controls.Add(dugmeler, 0, 2);
            Govde.Controls.Add(kok);
            ResumeLayout(false);
            Shown += delegate { Yukle(); };
        }

        private void SatirEkle()
        {
            int i = grid.Rows.Add("", "0");
            grid.CurrentCell = grid.Rows[i].Cells["cAd"];
            grid.BeginEdit(true);
        }

        private void Yukle()
        {
            try
            {
                grid.Rows.Clear();
                foreach (DataRow r in RafRepository.Listele().Rows)
                    grid.Rows.Add(Convert.ToString(r["Ad"]), Convert.ToString(r["KalipSayisi"]));
            }
            catch (Exception ex) { HataGunlugu.Yaz(ex); Mesajlar.Hata(Db.AnlasilirMesaj(ex)); }
        }

        private void Kaydet()
        {
            if (!Oturum.Aktif.IzinVar(KalipTakip.Izin.KalipListeEkle)) { Mesajlar.Uyari("Raf tanımlama yetkiniz yok."); return; }
            grid.EndEdit();
            List<string> liste = new List<string>();
            foreach (DataGridViewRow r in grid.Rows) liste.Add(Convert.ToString(r.Cells["cAd"].Value ?? ""));
            try
            {
                RafRepository.Kaydet(liste);
                Yukle();
                Mesajlar.Basari("Raflar kaydedildi.");
            }
            catch (InvalidOperationException ex) { Mesajlar.Uyari(ex.Message); }
            catch (Exception ex) { HataGunlugu.Yaz(ex); Mesajlar.Hata(Db.AnlasilirMesaj(ex)); }
        }
    }
}
