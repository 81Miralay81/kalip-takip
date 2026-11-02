using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Text;
using System.Threading.Tasks;

namespace KalipTakip.Sunucu
{
    /// <summary>
    /// Bildirimler: Telegram botu ve e-posta. Ayarlar veritabanındaki Ayar tablosundadır (portaldaki "Bildirim Ayarları" sayfasından girilir).
    /// Gönderim arka planda yapılır; her deneme BildirimLog tablosuna yazılır (başarılı / hata).
    /// </summary>
    internal static partial class Program
    {
        private static string AyarOku(string ad) { return AyarRepository.Al(ad, ""); }

        // ------------------------------------------------------------ gönderim

        // Bölümün bildirim alacak kullanıcılarına (Telegram ve/veya e-posta) arka planda mesaj gönderir.
        private static void Bildir(string bolum, string mesaj, int talepId, string izin = "")
        {
            if (string.IsNullOrWhiteSpace(bolum)) return;
            DataTable alicilar;
            try { alicilar = bolum == "*YONETICI*" ? CihazRepository.YoneticiAlicilari() : TalepRepository.BildirimAlicilari(bolum, izin); }
            catch (Exception ex) { BildirimRepository.LogYaz("-", bolum, mesaj, "Hata", "Alıcılar okunamadı: " + ex.Message, talepId); return; }
            if (alicilar.Rows.Count == 0)
            {
                BildirimRepository.LogYaz("-", bolum + " (kimse seçili değil)", mesaj, "Atlandı", "Bu bölümde 'bildirim al' işaretli kullanıcı yok.", talepId);
                return;
            }
            string link = AyarOku("link_adres").TrimEnd('/');
            string tamMesaj = link.Length > 0 && talepId > 0 ? mesaj + "\n" + link + "/#/talep?id=" + talepId.ToString(Inv) : mesaj;
            bool tgAktif = AyarOku("tg_aktif") == "1" && AyarOku("tg_token").Length > 0;
            bool mailAktif = AyarOku("mail_aktif") == "1" && AyarOku("mail_host").Length > 0;
            foreach (DataRow r in alicilar.Rows)
            {
                string ad = Convert.ToString(r["KullaniciAdi"]);
                string tg = r["TelegramId"] == DBNull.Value ? "" : Convert.ToString(r["TelegramId"]).Trim();
                string em = r["Email"] == DBNull.Value ? "" : Convert.ToString(r["Email"]).Trim();
                if (tgAktif && tg.Length > 0)
                {
                    string hedef = tg;
                    Task.Run(() => TelegramGonderGunluklu(hedef, ad, tamMesaj, talepId));
                }
                if (mailAktif && em.Length > 0)
                {
                    string hedef = em;
                    Task.Run(() => MailGonderGunluklu(hedef, ad, mesaj, tamMesaj, talepId));
                }
                if ((!tgAktif || tg.Length == 0) && (!mailAktif || em.Length == 0))
                    BildirimRepository.LogYaz("-", ad, mesaj, "Atlandı", "Kullanıcının kullanılabilir bir Telegram kimliği / e-postası yok ya da kanal kapalı.", talepId);
            }
        }

        private static void TelegramGonderGunluklu(string chatId, string ad, string mesaj, int talepId)
        {
            try { TelegramGonder(chatId, mesaj); BildirimRepository.LogYaz("Telegram", ad, mesaj, "Gönderildi", null, talepId); }
            catch (Exception ex) { BildirimRepository.LogYaz("Telegram", ad, mesaj, "Hata", ex.Message, talepId); }
        }

        private static void MailGonderGunluklu(string adres, string ad, string konu, string govde, int talepId)
        {
            try { MailGonder(adres, konu, govde); BildirimRepository.LogYaz("E-posta", ad + " <" + adres + ">", konu, "Gönderildi", null, talepId); }
            catch (Exception ex) { BildirimRepository.LogYaz("E-posta", ad + " <" + adres + ">", konu, "Hata", ex.Message, talepId); }
        }

        private static string HttpJson(string adres, string govde, string yontem, int zamanAsimi)
        {
            System.Net.ServicePointManager.SecurityProtocol = System.Net.SecurityProtocolType.Tls12;
            System.Net.ServicePointManager.Expect100Continue = false;
            HttpWebRequest r = (HttpWebRequest)WebRequest.Create(adres);
            r.Method = yontem;
            try
            {
                IWebProxy vekil = WebRequest.GetSystemWebProxy();
                vekil.Credentials = CredentialCache.DefaultCredentials;
                r.Proxy = vekil;
            }
            catch { }
            r.Timeout = zamanAsimi;
            r.ReadWriteTimeout = zamanAsimi;
            if (govde != null)
            {
                r.ContentType = "application/json";
                byte[] b = Encoding.UTF8.GetBytes(govde);
                r.ContentLength = b.Length;
                using (Stream st = r.GetRequestStream()) st.Write(b, 0, b.Length);
            }
            try
            {
                using (WebResponse yanit = r.GetResponse())
                using (StreamReader sr = new StreamReader(yanit.GetResponseStream(), Encoding.UTF8))
                    return sr.ReadToEnd();
            }
            catch (WebException ex)
            {
                string ayrinti = "";
                if (ex.Response != null)
                    using (StreamReader sr = new StreamReader(ex.Response.GetResponseStream(), Encoding.UTF8)) ayrinti = sr.ReadToEnd();
                throw new InvalidOperationException("Telegram'a ulaşılamadı: " + ex.Message + (ayrinti.Length > 0 ? " " + (ayrinti.Length > 250 ? ayrinti.Substring(0, 250) : ayrinti) : ""));
            }
        }

        private static void TelegramGonder(string chatId, string metin)
        {
            string token = AyarOku("tg_token");
            if (token.Length == 0) throw new InvalidOperationException("Telegram bot anahtarı girilmemiş.");
            Dictionary<string, object> d = new Dictionary<string, object>();
            d["chat_id"] = chatId;
            d["text"] = metin;
            HttpJson("https://api.telegram.org/bot" + token + "/sendMessage", Js.Serialize(d), "POST", 15000);
        }

        private static void MailGonder(string adres, string konu, string govde)
        {
            string host = AyarOku("mail_host");
            if (host.Length == 0) throw new InvalidOperationException("E-posta sunucusu girilmemiş.");
            int port;
            if (!int.TryParse(AyarOku("mail_port"), NumberStyles.Integer, Inv, out port)) port = 587;
            string gonderen = AyarOku("mail_gonderen");
            string kullanici = AyarOku("mail_kullanici");
            if (gonderen.Length == 0) gonderen = kullanici;
            if (gonderen.Length == 0) throw new InvalidOperationException("E-posta için 'gönderen adres' girilmemiş.");
            using (SmtpClient smtp = new SmtpClient(host, port))
            {
                smtp.EnableSsl = AyarOku("mail_ssl") == "1";
                smtp.Timeout = 20000;
                if (kullanici.Length > 0) smtp.Credentials = new NetworkCredential(kullanici, AyarOku("mail_parola"));
                else smtp.UseDefaultCredentials = false;
                using (MailMessage m = new MailMessage(gonderen, adres, "Kalıp Takip: " + konu, govde))
                {
                    m.BodyEncoding = Encoding.UTF8;
                    m.SubjectEncoding = Encoding.UTF8;
                    smtp.Send(m);
                }
            }
        }

        // ------------------------------------------------------------ API uçları (bildirim ayarları: yönetici)

        private static readonly string[] AyarAnahtarlari =
            { "tg_aktif", "tg_token", "mail_aktif", "mail_host", "mail_port", "mail_ssl", "mail_kullanici", "mail_parola", "mail_gonderen", "link_adres" };

        private static bool BildirimUclari(string yol, Kullanici k, Baglanti ctx)
        {
            switch (yol)
            {
                case "/api/bildirim/ayar":
                    YoneticiGerekli(k);
                    {
                        Dictionary<string, object> d = new Dictionary<string, object>();
                        foreach (string a in AyarAnahtarlari) d[a] = a == "mail_parola" || a == "tg_token" ? "" : AyarOku(a);
                        d["tg_token_var"] = AyarOku("tg_token").Length > 0;
                        d["mail_parola_var"] = AyarOku("mail_parola").Length > 0;
                        Json(ctx, 200, d);
                    }
                    return true;
                case "/api/bildirim/ayar/kaydet":
                    YoneticiGerekli(k);
                    {
                        Dictionary<string, object> g = Govde(ctx);
                        foreach (string a in AyarAnahtarlari)
                        {
                            if (!g.ContainsKey(a)) continue;
                            string v = (Metin(g, a) ?? "").Trim();
                            if ((a == "tg_token" || a == "mail_parola") && v.Length == 0) continue;   // boş bırakılırsa eski değer kalır
                            AyarRepository.Yaz(a, v);
                        }
                        Json(ctx, 200, new { mesaj = "Bildirim ayarları kaydedildi." });
                    }
                    return true;
                case "/api/bildirim/test":
                    YoneticiGerekli(k);
                    {
                        Dictionary<string, object> g = Govde(ctx);
                        string kanal = Metin(g, "kanal");
                        string hedef = Metin(g, "hedef");
                        if (hedef.Length == 0) throw new InvalidOperationException("Test için bir alıcı yazın.");
                        string metin = "Kalıp Takip test mesajı (" + DateTime.Now.ToString("dd.MM.yyyy HH:mm", TrK) + "). Bu mesajı görüyorsanız bildirimler çalışıyor.";
                        if (kanal == "telegram") { TelegramGonder(hedef, metin); Json(ctx, 200, new { mesaj = "Telegram mesajı gönderildi." }); }
                        else { MailGonder(hedef, "Test", metin); Json(ctx, 200, new { mesaj = "E-posta gönderildi: " + hedef }); }
                    }
                    return true;
                case "/api/bildirim/telegram-bul":
                    YoneticiGerekli(k);
                    {
                        // Bota yazanların kimliklerini listeler (kullanıcı kayıtlarına yazılacak "Telegram kimliği").
                        string token = AyarOku("tg_token");
                        if (token.Length == 0) throw new InvalidOperationException("Önce bot anahtarını kaydedin.");
                        string yanit = HttpJson("https://api.telegram.org/bot" + token + "/getUpdates", null, "GET", 15000);
                        Dictionary<string, object> j = Js.Deserialize<Dictionary<string, object>>(yanit);
                        List<Dictionary<string, object>> sonuc = new List<Dictionary<string, object>>();
                        HashSet<string> gorulen = new HashSet<string>();
                        if (j.ContainsKey("result") && j["result"] is System.Collections.IEnumerable && !(j["result"] is string))
                            foreach (object o in (System.Collections.IEnumerable)j["result"])
                            {
                                Dictionary<string, object> u = GSoz(o);
                                Dictionary<string, object> mesaj = GSoz(u.ContainsKey("message") ? u["message"] : null);
                                Dictionary<string, object> chat = GSoz(mesaj.ContainsKey("chat") ? mesaj["chat"] : null);
                                if (!chat.ContainsKey("id")) continue;
                                string id = Convert.ToString(chat["id"], Inv);
                                if (!gorulen.Add(id)) continue;
                                string ad = (chat.ContainsKey("first_name") ? Convert.ToString(chat["first_name"]) : "") + " " + (chat.ContainsKey("last_name") ? Convert.ToString(chat["last_name"]) : "");
                                if (chat.ContainsKey("username")) ad += " (@" + Convert.ToString(chat["username"]) + ")";
                                Dictionary<string, object> s = new Dictionary<string, object>();
                                s["id"] = id; s["ad"] = ad.Trim();
                                sonuc.Add(s);
                            }
                        Json(ctx, 200, new { satirlar = sonuc });
                    }
                    return true;
                case "/api/bildirim/log":
                    YoneticiGerekli(k);
                    Json(ctx, 200, new { satirlar = DuzIso(BildirimRepository.Gunluk(100)) });
                    return true;
            }
            return false;
        }
    }
}
