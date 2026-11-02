using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Data.SQLite;
using System.Drawing;
using System.Windows.Forms;
using Guna.UI2.WinForms;

namespace KalipTakip.Arayuz
{
    /// <summary>Yönetici ekranı: kullanıcı ekle/düzenle/sil, parola belirle, yetkileri ayarla.</summary>
    [DesignerCategory("Code")]
    public class frmKullanicilar : TabanForm
    {
        private Guna2DataGridView grid;
        private Guna2TextBox txtAd, txtParola;
        private OneriKutusu txtBolum;
        private Label lblParola, lblBaslik;
        private Guna2CheckBox chkAdmin, chkAktif;
        private Guna2CheckBox[] izinKutulari;
        private Kullanici _secili;          // null = yeni kullanıcı
        private bool _yukleniyor;

        public frmKullanicilar()
            : base("Kullanıcılar ve Yetkiler", "Kullanıcı ekle, parola belirle, yetkileri ayarla", new Size(1100, 800), true)
        {
            MinimumSize = new Size(960, 740);
            StartPosition = FormStartPosition.CenterParent;
            SuspendLayout();

            TableLayoutPanel icerik = new TableLayoutPanel();
            icerik.Dock = DockStyle.Fill;
            icerik.ColumnCount = 2;
            icerik.RowCount = 1;
            icerik.BackColor = Color.Transparent;
            icerik.Padding = new Padding(14, 12, 14, 14);
            icerik.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 350F));
            icerik.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            icerik.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            // Sol: liste
            Guna2Panel solKart = Tema.KartPaneli();
            solKart.Dock = DockStyle.Fill;
            solKart.Margin = new Padding(0, 0, 12, 0);
            solKart.Padding = new Padding(10);
            grid = Tema.Tablo();
            grid.Dock = DockStyle.Fill;
            DataGridViewTextBoxColumn cAd = new DataGridViewTextBoxColumn();
            cAd.DataPropertyName = "KullaniciAdi";
            cAd.HeaderText = "Kullanıcı";
            cAd.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            grid.Columns.Add(cAd);
            DataGridViewCheckBoxColumn cAdmin = new DataGridViewCheckBoxColumn();
            cAdmin.DataPropertyName = "IsAdmin";
            cAdmin.HeaderText = "Yönetici";
            cAdmin.Width = 80;
            grid.Columns.Add(cAdmin);
            DataGridViewCheckBoxColumn cAktif = new DataGridViewCheckBoxColumn();
            cAktif.DataPropertyName = "Aktif";
            cAktif.HeaderText = "Aktif";
            cAktif.Width = 64;
            grid.Columns.Add(cAktif);
            solKart.Controls.Add(grid);

            // Sağ: düzenleme
            Guna2Panel sagKart = Tema.KartPaneli();
            sagKart.Dock = DockStyle.Fill;
            sagKart.Margin = new Padding(0);
            sagKart.Padding = new Padding(26, 18, 26, 18);

            lblBaslik = Tema.Etiket("Yeni kullanıcı", Tema.Orta, Tema.Yazi);
            lblBaslik.Location = new Point(26, 18);

            Label l1 = Tema.Etiket("Kullanıcı adı", Tema.Kucuk, Tema.YaziSoluk);
            l1.Location = new Point(26, 62);
            txtAd = Tema.Kutu(50);
            txtAd.CharacterCasing = CharacterCasing.Upper;
            txtAd.SetBounds(26, 84, 290, 38);

            lblParola = Tema.Etiket("Parola", Tema.Kucuk, Tema.YaziSoluk);
            lblParola.Location = new Point(336, 62);
            txtParola = Tema.Kutu(100);
            txtParola.UseSystemPasswordChar = true;
            txtParola.SetBounds(336, 84, 290, 38);

            Label lBolum = Tema.Etiket("Bölüm (talep açarken otomatik gelir)", Tema.Kucuk, Tema.YaziSoluk);
            lBolum.Location = new Point(26, 136);
            txtBolum = new OneriKutusu { MaxLength = 60 };
            txtBolum.SetBounds(26, 158, 290, 38);
            try { txtBolum.SecenekleriYukle(LokasyonRepository.Bolumler()); } catch { }

            chkAdmin = Tema.Onay("Yönetici (tüm yetkiler + kullanıcı yönetimi)");
            chkAdmin.Location = new Point(26, 208);
            chkAktif = Tema.Onay("Aktif (giriş yapabilir)");
            chkAktif.Checked = true;
            chkAktif.Location = new Point(420, 208);

            Label l2 = Tema.Etiket("YETKİLER", Tema.Kalin, Color.FromArgb(253, 224, 71));
            l2.Location = new Point(26, 250);
            Guna2Panel izinKart = new Guna2Panel();
            izinKart.FillColor = Tema.KartAcik;
            izinKart.BorderColor = Tema.KartKenar;
            izinKart.BorderThickness = 1;
            izinKart.BorderRadius = 12;
            izinKart.BackColor = Color.Transparent;
            izinKart.SetBounds(26, 274, 600, 284);
            izinKart.AutoScroll = true;
            izinKutulari = new Guna2CheckBox[Izin.Tanimlar.Length];
            string sonGrup = null;
            int y = 12;
            int sutun = 0;
            for (int i = 0; i < Izin.Tanimlar.Length; i++)
            {
                IzinTanimi t = Izin.Tanimlar[i];
                if (t.Grup != sonGrup)
                {
                    if (sonGrup != null) y += (sutun == 1 ? 34 : 0) + 12;
                    sutun = 0;
                    Label g = Tema.Etiket(t.Grup.ToUpper(Kultur.Tr) + "  portalı", Tema.Kalin, t.Grup == Izin.GrupTalep ? Color.FromArgb(103, 232, 249) : Color.FromArgb(165, 180, 252));
                    g.Location = new Point(16, y);
                    izinKart.Controls.Add(g);
                    y += 30;
                    sonGrup = t.Grup;
                }
                Guna2CheckBox c = Tema.Onay(t.Ad);
                if (t.GrupAnahtari)
                {
                    // 'Görebilir' yetkisi tam satır kaplar
                    c.Location = new Point(18, y);
                    y += 36;
                    sutun = 0;
                    string grup = t.Grup;
                    c.CheckedChanged += delegate { GrupGorDegisti(grup); };
                }
                else
                {
                    c.Location = new Point(18 + sutun * 290, y);
                    if (sutun == 1) { y += 34; sutun = 0; } else sutun = 1;
                }
                izinKutulari[i] = c;
                izinKart.Controls.Add(c);
            }

            Guna2GradientButton btnKaydet = Tema.Buton("Kaydet", Tema.Mavi, Tema.Mor, 130, 42);
            btnKaydet.Location = new Point(26, 566);
            Guna2GradientButton btnYeni = Tema.Buton("Yeni Kullanıcı", Tema.Yesil, Color.FromArgb(16, 185, 129), 150, 42);
            btnYeni.Location = new Point(170, 566);
            Guna2GradientButton btnSil = Tema.Buton("Sil", Tema.Kirmizi, Color.FromArgb(190, 24, 93), 100, 42);
            btnSil.Location = new Point(334, 566);
            Guna2GradientButton btnKapat = Tema.Buton("Kapat", Tema.Gri1, Tema.Gri2, 110, 42);
            btnKapat.Location = new Point(516, 566);

            sagKart.Controls.AddRange(new Control[] { lblBaslik, l1, txtAd, lblParola, txtParola, lBolum, txtBolum, chkAdmin, chkAktif, l2, izinKart,
                                                      btnKaydet, btnYeni, btnSil, btnKapat });

            icerik.Controls.Add(solKart, 0, 0);
            icerik.Controls.Add(sagKart, 1, 0);
            Govde.Controls.Add(icerik);

            btnKaydet.Click += delegate { Kaydet(); };
            btnYeni.Click += delegate { YeniKullanici(); };
            btnSil.Click += delegate { SilTiklandi(); };
            btnKapat.Click += delegate { Close(); };
            chkAdmin.CheckedChanged += delegate { IzinleriAyarla(); };
            grid.SelectionChanged += GridSecimDegisti;
            Shown += delegate { Yenile(0); };
            ResumeLayout(false);
        }

        /// <summary>Bir portalın 'görebilir' yetkisi kapatılırsa o portalın diğer yetkileri de kapanır.</summary>
        private void GrupGorDegisti(string grup)
        {
            if (!_yukleniyor)
            {
                Guna2CheckBox gor = GorKutusu(grup);
                if (gor != null && !gor.Checked)
                {
                    for (int i = 0; i < Izin.Tanimlar.Length; i++)
                        if (Izin.Tanimlar[i].Grup == grup && !Izin.Tanimlar[i].GrupAnahtari) izinKutulari[i].Checked = false;
                }
            }
            IzinleriAyarla();
        }

        private Guna2CheckBox GorKutusu(string grup)
        {
            for (int i = 0; i < Izin.Tanimlar.Length; i++)
                if (Izin.Tanimlar[i].Grup == grup && Izin.Tanimlar[i].GrupAnahtari) return izinKutulari[i];
            return null;
        }

        private void IzinleriAyarla()
        {
            for (int i = 0; i < izinKutulari.Length; i++)
            {
                IzinTanimi t = Izin.Tanimlar[i];
                bool acik = !chkAdmin.Checked;
                if (acik && !t.GrupAnahtari)
                {
                    Guna2CheckBox gor = GorKutusu(t.Grup);
                    acik = gor != null && gor.Checked;
                }
                izinKutulari[i].Enabled = acik;
            }
        }

        private void Yenile(int seciliId)
        {
            DataTable dt;
            try
            {
                dt = KullaniciRepository.Listele();
            }
            catch (Exception ex)
            {
                HataGunlugu.Yaz(ex);
                Mesajlar.Hata("Kullanıcılar okunamadı:\r\n\r\n" + Db.AnlasilirMesaj(ex));
                return;
            }

            _yukleniyor = true;
            try
            {
                grid.DataSource = dt;
                grid.ClearSelection();
            }
            finally
            {
                _yukleniyor = false;
            }

            if (seciliId > 0)
            {
                for (int i = 0; i < dt.Rows.Count; i++)
                {
                    if (Convert.ToInt32(dt.Rows[i]["Id"]) == seciliId)
                    {
                        grid.CurrentCell = grid.Rows[i].Cells[0];
                        grid.Rows[i].Selected = true;
                        return;
                    }
                }
            }
            YeniKullanici();
        }

        private void GridSecimDegisti(object sender, EventArgs e)
        {
            if (_yukleniyor || grid.SelectedRows.Count == 0) return;
            DataRowView rv = grid.SelectedRows[0].DataBoundItem as DataRowView;
            if (rv == null) return;
            Kullanici k;
            try
            {
                k = KullaniciRepository.Getir(Convert.ToInt32(rv["Id"]));
            }
            catch (Exception ex)
            {
                HataGunlugu.Yaz(ex);
                return;
            }
            if (k == null) return;
            _secili = k;
            lblBaslik.Text = "Kullanıcı: " + k.KullaniciAdi;
            lblParola.Text = "Yeni parola (boş bırakırsanız değişmez)";
            txtAd.Text = k.KullaniciAdi;
            txtParola.Text = "";
            chkAdmin.Checked = k.IsAdmin;
            chkAktif.Checked = k.Aktif;
            txtBolum.Text = k.Bolum ?? "";
            for (int i = 0; i < Izin.Tanimlar.Length; i++)
                izinKutulari[i].Checked = k.Izinler.Contains(Izin.Tanimlar[i].Anahtar);
            IzinleriAyarla();
        }

        private void YeniKullanici()
        {
            _yukleniyor = true;
            try { grid.ClearSelection(); }
            finally { _yukleniyor = false; }
            _secili = null;
            lblBaslik.Text = "Yeni kullanıcı";
            lblParola.Text = "Parola (en az 4 karakter)";
            txtAd.Text = "";
            txtParola.Text = "";
            chkAdmin.Checked = false;
            chkAktif.Checked = true;
            txtBolum.Text = "";
            foreach (Guna2CheckBox c in izinKutulari) c.Checked = false;
            IzinleriAyarla();
            txtAd.Focus();
        }

        private void Kaydet()
        {
            string ad = txtAd.Text.Trim().ToUpper(Kultur.Tr);
            if (ad.Length < 2)
            {
                Mesajlar.Uyari("Kullanıcı adı en az 2 karakter olmalı.");
                txtAd.Focus();
                return;
            }
            bool yeni = _secili == null;
            if (yeni && txtParola.Text.Length < 4)
            {
                Mesajlar.Uyari("Yeni kullanıcı için en az 4 karakterlik bir parola girin.");
                txtParola.Focus();
                return;
            }
            if (!yeni && txtParola.Text.Length > 0 && txtParola.Text.Length < 4)
            {
                Mesajlar.Uyari("Parola en az 4 karakter olmalı (değiştirmek istemiyorsanız boş bırakın).");
                txtParola.Focus();
                return;
            }

            // Son aktif yöneticiyi kaybetme
            if (!yeni && _secili.IsAdmin && _secili.Aktif && (!chkAdmin.Checked || !chkAktif.Checked))
            {
                try
                {
                    if (KullaniciRepository.AktifYoneticiSayisi() <= 1)
                    {
                        Mesajlar.Uyari("Sistemde en az bir aktif yönetici kalmalı. Bu kullanıcının yönetici/aktif durumunu kaldıramazsınız.");
                        return;
                    }
                }
                catch (Exception ex)
                {
                    HataGunlugu.Yaz(ex);
                    Mesajlar.Hata(Db.AnlasilirMesaj(ex));
                    return;
                }
            }

            Kullanici k = new Kullanici();
            k.Id = yeni ? 0 : _secili.Id;
            k.KullaniciAdi = ad;
            k.IsAdmin = chkAdmin.Checked;
            k.Aktif = chkAktif.Checked;
            k.Bolum = txtBolum.Text.Trim();
            for (int i = 0; i < Izin.Tanimlar.Length; i++)
                if (izinKutulari[i].Checked) k.Izinler.Add(Izin.Tanimlar[i].Anahtar);

            try
            {
                int id;
                using (new Bekleme())
                {
                    id = KullaniciRepository.Kaydet(k, txtParola.Text);
                }
                // Kendi kaydını değiştirdiyse oturumdaki yetkileri de tazele
                if (Oturum.Aktif != null && Oturum.Aktif.Id == id)
                {
                    Kullanici taze = KullaniciRepository.Getir(id);
                    if (taze != null) Oturum.Aktif = taze;
                }
                Yenile(id);
                Bildirim.Goster(this, BildirimTuru.Basari, "Kullanıcı kaydedildi", "'" + k.KullaniciAdi + "' için bilgiler ve yetkiler kaydedildi.");
            }
            catch (SQLiteException ex)
            {
                HataGunlugu.Yaz(ex);
                Mesajlar.Hata("Kullanıcı kaydedilemedi. Aynı adda başka bir kullanıcı olabilir.\r\n\r\n" + Db.AnlasilirMesaj(ex));
            }
            catch (Exception ex)
            {
                HataGunlugu.Yaz(ex);
                Mesajlar.Hata("Kullanıcı kaydedilemedi:\r\n\r\n" + ex.Message);
            }
        }

        private void SilTiklandi()
        {
            if (_secili == null)
            {
                Mesajlar.Bilgi("Silmek için soldan bir kullanıcı seçin.");
                return;
            }
            if (Oturum.Aktif != null && Oturum.Aktif.Id == _secili.Id)
            {
                Mesajlar.Uyari("Kendi kullanıcınızı silemezsiniz.");
                return;
            }
            try
            {
                if (_secili.IsAdmin && _secili.Aktif && KullaniciRepository.AktifYoneticiSayisi() <= 1)
                {
                    Mesajlar.Uyari("Sistemde en az bir aktif yönetici kalmalı.");
                    return;
                }
                if (!Mesajlar.Soru("'" + _secili.KullaniciAdi + "' kullanıcısı silinecek. Emin misiniz?\r\n\r\n" +
                                   "(Giriş yapmasını engellemek için silmek yerine 'Aktif' işaretini kaldırabilirsiniz.)")) return;
                string silinenAd = _secili.KullaniciAdi;
                KullaniciRepository.Sil(_secili.Id);
                Yenile(0);
                Bildirim.Goster(this, BildirimTuru.Basari, "Kullanıcı silindi", "'" + silinenAd + "' kullanıcısı silindi.");
            }
            catch (Exception ex)
            {
                HataGunlugu.Yaz(ex);
                Mesajlar.Hata("Kullanıcı silinemedi:\r\n\r\n" + Db.AnlasilirMesaj(ex));
            }
        }
    }
}
