using System;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using Guna.UI2.WinForms;

namespace KalipTakip.Arayuz
{
    /// <summary>İlk kurulum: yeni (taşınabilir) veritabanını eski Access ya da Excel dosyasından oluşturur.</summary>
    [DesignerCategory("Code")]
    public class frmSetup : TabanForm
    {
        private readonly Guna2RadioButton _rbAccess;
        private readonly Guna2RadioButton _rbExcel;
        private readonly Guna2TextBox _txtDosya;
        private readonly Guna2GradientButton _btnGozat;
        private readonly Guna2GradientButton _btnBaslat;
        private readonly Guna2GradientButton _btnCikis;
        private readonly Guna2GradientButton _btnBos;
        private readonly Guna2GradientButton _btnBosKur;
        private readonly Guna2CheckBox _chkYeniden;
        private readonly Guna2ProgressBar _bar;
        private readonly Label _lblDurum;
        private readonly TextBox _txtGunluk;
        private readonly Label _lblKaynak;
        private readonly Timer _animasyon = new Timer();
        private bool _mesgul;

        public frmSetup() : base("İlk Kurulum", "Yeni veritabanı eski dosyanızdan oluşturulacak", new Size(720, 640), false)
        {
            SuspendLayout();

            Label aciklama = Tema.Etiket(
                "Veritabanı bulunamadı. Hazır KalipTakip.db dosyanız varsa 'Hazır .db Dosyasını Seç' ile gösterin; exe'nin yanına kopyalanır, herkes oradan kullanır. Yoksa eski dosyanızı seçin; tüm kayıtlar, kullanıcılar (parolalarıyla) ve yetkiler " +
                "tek seferde aktarılır. Eski dosyaya dokunulmaz.", Tema.Normal, Tema.YaziSoluk);
            aciklama.AutoSize = false;
            aciklama.SetBounds(30, 16, 660, 44);

            _rbAccess = Tema.Secenek("Eski Access veritabanı (.accdb)  — önerilen");
            _rbAccess.Checked = true;
            _rbAccess.Location = new Point(30, 70);
            _rbExcel = Tema.Secenek("Eski Excel dosyası (.xlsm / .xlsx)");
            _rbExcel.Location = new Point(30, 100);

            _lblKaynak = Tema.Etiket("Aktarılacak dosya", Tema.Kalin, Tema.Yazi);
            _lblKaynak.Location = new Point(30, 140);
            _txtDosya = Tema.Kutu();
            _txtDosya.SetBounds(30, 164, 520, 38);
            _txtDosya.PlaceholderText = "Dosya yolu...";
            _btnGozat = Tema.Buton("Gözat...", Tema.Gri1, Tema.Gri2, 126, 38);
            _btnGozat.Location = new Point(562, 164);

            Label l2 = Tema.Etiket("Oluşturulacak yeni veritabanı", Tema.Kalin, Tema.Yazi);
            l2.Location = new Point(30, 216);
            Label lblDb = Tema.Etiket(Db.Yol, Tema.Normal, Tema.YaziSoluk);
            lblDb.AutoSize = false;
            lblDb.SetBounds(30, 240, 660, 22);

            _chkYeniden = Tema.Onay("Toplam Zaman'ı bileşenlerden yeniden hesapla (yalnız Excel; önerilmez)");
            _chkYeniden.Location = new Point(30, 272);
            _chkYeniden.Visible = false;

            _btnBaslat = Tema.Buton("Aktarımı Başlat", Tema.Yesil, Color.FromArgb(16, 185, 129), 150, 46);
            _btnBaslat.Location = new Point(30, 312);
            _btnCikis = Tema.Buton("Çıkış", Tema.Gri1, Tema.Gri2, 90, 46);
            _btnCikis.Location = new Point(190, 312);
            _btnBos = Tema.Buton("Hazır .db Seç", Tema.Mavi, Tema.Mor, 170, 46);
            _btnBos.Location = new Point(290, 312);
            _btnBosKur = Tema.Buton("Boş Veritabanı Kur", Tema.Turuncu, Tema.Pembe, 210, 46);
            _btnBosKur.Location = new Point(480, 312);

            _bar = new Guna2ProgressBar();
            _bar.FillColor = Tema.GirdiDolgu;
            _bar.ProgressColor = Tema.Mavi;
            _bar.ProgressColor2 = Tema.Mor;
            _bar.BorderRadius = 6;
            _bar.BackColor = Color.Transparent;
            _bar.SetBounds(30, 376, 660, 12);
            _bar.Visible = false;

            _lblDurum = Tema.Etiket("", Tema.Kalin, Tema.Yazi);
            _lblDurum.AutoSize = false;
            _lblDurum.SetBounds(30, 396, 660, 22);

            _txtGunluk = new TextBox();
            _txtGunluk.Multiline = true;
            _txtGunluk.ReadOnly = true;
            _txtGunluk.ScrollBars = ScrollBars.Vertical;
            _txtGunluk.Font = Tema.Kucuk;
            _txtGunluk.BorderStyle = BorderStyle.None;
            _txtGunluk.BackColor = Tema.GirdiSalt;
            _txtGunluk.ForeColor = Tema.Yazi;
            _txtGunluk.SetBounds(30, 424, 660, 130);

            Govde.Controls.AddRange(new Control[] { aciklama, _rbAccess, _rbExcel, _lblKaynak, _txtDosya, _btnGozat, l2, lblDb,
                                                    _chkYeniden, _btnBaslat, _btnCikis, _btnBos, _btnBosKur, _bar, _lblDurum, _txtGunluk });

            _rbAccess.CheckedChanged += delegate { KaynakDegisti(); };
            _rbExcel.CheckedChanged += delegate { KaynakDegisti(); };
            _btnGozat.Click += GozatTiklandi;
            _btnBaslat.Click += BaslatTiklandi;
            _btnCikis.Click += delegate { DialogResult = DialogResult.Cancel; };
            _btnBos.Click += BosBaslaTiklandi;
            _btnBosKur.Click += BosKurTiklandi;

            _animasyon.Interval = 40;
            _animasyon.Tick += delegate { _bar.Value = (_bar.Value + 4) % 101; };

            ResumeLayout(false);
        }

        private void KaynakDegisti()
        {
            _chkYeniden.Visible = _rbExcel.Checked;
            _lblKaynak.Text = _rbAccess.Checked ? "Eski Access dosyası (.accdb)" : "Eski Excel dosyası (.xlsm)";
            _txtDosya.Text = "";
        }

        private void GozatTiklandi(object sender, EventArgs e)
        {
            using (OpenFileDialog dlg = new OpenFileDialog())
            {
                if (_rbAccess.Checked)
                {
                    dlg.Filter = "Access veritabanı (*.accdb;*.mdb)|*.accdb;*.mdb";
                    dlg.Title = "Eski Access veritabanını seçin";
                }
                else
                {
                    dlg.Filter = "Excel dosyaları (*.xlsm;*.xlsx)|*.xlsm;*.xlsx";
                    dlg.Title = "Eski kalıp takip dosyasını seçin";
                }
                if (dlg.ShowDialog(this) == DialogResult.OK) _txtDosya.Text = dlg.FileName;
            }
        }

        private void Yaz(string metin)
        {
            _lblDurum.Text = metin;
            _txtGunluk.AppendText(DateTime.Now.ToString("HH:mm:ss", Kultur.Inv) + "  " + metin + "\r\n");
        }

        private void Mesgul(bool deger)
        {
            _mesgul = deger;
            _btnBaslat.Enabled = !deger;
            _btnGozat.Enabled = !deger;
            _btnCikis.Enabled = !deger;
            _btnBos.Enabled = !deger;
            _btnBosKur.Enabled = !deger;
            _txtDosya.Enabled = !deger;
            _chkYeniden.Enabled = !deger;
            _rbAccess.Enabled = !deger;
            _rbExcel.Enabled = !deger;
            _bar.Visible = deger;
            if (deger) _animasyon.Start(); else _animasyon.Stop();
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (_mesgul && keyData == Keys.Escape) return true;
            return base.ProcessCmdKey(ref msg, keyData);
        }

        // Eski dosya olmadan, tamamen boş bir veritabanı kurar; yönetici: ADMIN / 1234 (ilk girişten sonra parolayı değiştirin).
        private void BosKurTiklandi(object sender, EventArgs e)
        {
            if (!Mesajlar.Soru("Boş bir veritabanı kurulacak:\r\n\r\n" + Db.Yol + "\r\n\r\nSonraki adımda yönetici kullanıcı adı ve parolasını belirleyeceksiniz.\r\n\r\nDevam edilsin mi?")) return;
            string yAd, yParola;
            if (!YoneticiSor(out yAd, out yParola)) return;
            try
            {
                Sema.BosKur(yAd, yParola);
                DialogResult = DialogResult.OK;
            }
            catch (Exception ex)
            {
                HataGunlugu.Yaz(ex);
                Mesajlar.Hata("Veritabanı kurulamadı:\r\n\r\n" + Db.AnlasilirMesaj(ex));
            }
        }

        // Yönetici kullanıcı adı ve parolası (varsayılan parola kullanılmaz; en az 4 karakter, iki kez girilir).
        private bool YoneticiSor(out string ad, out string parola)
        {
            ad = null; parola = null;
            using (Form f = new Form())
            {
                f.Text = "Yönetici kullanıcı";
                f.StartPosition = FormStartPosition.CenterParent;
                f.FormBorderStyle = FormBorderStyle.FixedDialog;
                f.MaximizeBox = false; f.MinimizeBox = false;
                f.ClientSize = new Size(360, 230);
                Label l1 = new Label { Text = "Kullanıcı adı", Left = 16, Top = 14, Width = 320 };
                TextBox t1 = new TextBox { Text = "ADMIN", Left = 16, Top = 34, Width = 328 };
                Label l2 = new Label { Text = "Parola (en az 4 karakter)", Left = 16, Top = 66, Width = 320 };
                TextBox t2 = new TextBox { Left = 16, Top = 86, Width = 328, UseSystemPasswordChar = true };
                Label l3 = new Label { Text = "Parola (tekrar)", Left = 16, Top = 118, Width = 320 };
                TextBox t3 = new TextBox { Left = 16, Top = 138, Width = 328, UseSystemPasswordChar = true };
                Button tamam = new Button { Text = "Kur", Left = 188, Top = 184, Width = 75, DialogResult = DialogResult.OK };
                Button iptal = new Button { Text = "Vazgeç", Left = 269, Top = 184, Width = 75, DialogResult = DialogResult.Cancel };
                f.Controls.AddRange(new Control[] { l1, t1, l2, t2, l3, t3, tamam, iptal });
                f.AcceptButton = tamam; f.CancelButton = iptal;
                while (true)
                {
                    if (f.ShowDialog(this) != DialogResult.OK) return false;
                    if (t1.Text.Trim().Length == 0) { Mesajlar.Uyari("Kullanıcı adı boş olamaz."); continue; }
                    if (t2.Text.Length < 4) { Mesajlar.Uyari("Parola en az 4 karakter olmalı."); continue; }
                    if (t2.Text != t3.Text) { Mesajlar.Uyari("Parolalar aynı değil."); continue; }
                    ad = t1.Text.Trim(); parola = t2.Text;
                    return true;
                }
            }
        }

        private void BosBaslaTiklandi(object sender, EventArgs e)
        {
            using (OpenFileDialog dlg = new OpenFileDialog())
            {
                dlg.Title = "Hazır KalipTakip.db dosyasını seçin";
                dlg.Filter = "SQLite veritabanı (*.db;*.sqlite)|*.db;*.sqlite|Tüm dosyalar (*.*)|*.*";
                dlg.CheckFileExists = true;
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    string hedef = Db.Yol;
                    if (!string.Equals(System.IO.Path.GetFullPath(dlg.FileName), System.IO.Path.GetFullPath(hedef), StringComparison.OrdinalIgnoreCase))
                        System.IO.File.Copy(dlg.FileName, hedef, true);
                    string mesaj = null;
                    if (Db.DurumBul(ref mesaj) != VeritabaniDurumu.Hazir)
                    {
                        Mesajlar.Uyari("Seçilen dosya geçerli bir Kalıp Takip veritabanı değil" + (string.IsNullOrEmpty(mesaj) ? "." : ":\r\n\r\n" + mesaj));
                        return;
                    }
                    DialogResult = DialogResult.OK;
                }
                catch (Exception ex)
                {
                    HataGunlugu.Yaz(ex);
                    Mesajlar.Hata("Dosya seçilemedi:\r\n\r\n" + Db.AnlasilirMesaj(ex));
                }
            }
        }

        private async void BaslatTiklandi(object sender, EventArgs e)
        {
            string yol = _txtDosya.Text.Trim();
            if (yol.Length == 0 || !File.Exists(yol))
            {
                Mesajlar.Uyari("Lütfen geçerli bir dosya seçin.");
                return;
            }
            bool access = _rbAccess.Checked;
            bool yeniden = _chkYeniden.Checked;
            Progress<string> ilerleme = new Progress<string>(Yaz);

            Mesgul(true);
            try
            {
                AktarimSonucu sonuc;
                if (access)
                    sonuc = await Task.Run(() => AccessAktarici.Calistir(yol, ilerleme));
                else
                    sonuc = await Task.Run(() => ExcelAktarici.Calistir(yol, yeniden, ilerleme));

                string ozet = sonuc.KayitSayisi.ToString("N0", Kultur.Tr) + " kayıt ve " +
                              sonuc.KullaniciSayisi.ToString(Kultur.Tr) + " kullanıcı aktarıldı." + "\r\n\r\n" +
                              (access
                                  ? "Kullanıcıların parolaları ve yetkileri olduğu gibi korundu."
                                  : "Kullanıcıların eski parolaları korunur (güvenli biçimde saklanır). Listedeki ilk kullanıcı yönetici yapıldı; yetkileri Kullanıcılar ekranından değiştirebilirsiniz.");
                if (sonuc.SorunSayisi > 0)
                {
                    ozet += "\r\n\r\n" + sonuc.SorunSayisi.ToString("N0", Kultur.Tr) + " kayıtta düzeltme/uyarı notu var.\r\nAyrıntılar: " + sonuc.SorunDosyasi;
                }
                Mesajlar.Basari(ozet);
                DialogResult = DialogResult.OK;
            }
            catch (Exception ex)
            {
                HataGunlugu.Yaz(ex);
                Yaz("Aktarım başarısız.");
                Mesajlar.Hata("Aktarım tamamlanamadı:\r\n\r\n" + Db.AnlasilirMesaj(ex));
                Mesgul(false);
            }
        }
    }
}
