using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using KalipTakip;

namespace KalipTakip.Sunucu
{
    /// <summary>
    /// Telefon (Android / iPhone tarayıcısı) için küçük HTTP sunucusu. Masaüstü programla aynı veritabanını kullanır.
    /// Fabrika Wi-Fi ağında http://BU-BILGISAYARIN-IP:8080 adresinden açılır.
    /// </summary>
    internal static partial class Program
    {
        private sealed class Oturum
        {
            public Kullanici Kullanici;
            public DateTime Bitis;
            public string CihazHash;
        }

        private static readonly Dictionary<string, Oturum> Oturumlar = new Dictionary<string, Oturum>();
        private static readonly object Kilit = new object();
        private static readonly JavaScriptSerializer Js = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
        private static byte[] _anaSayfa;

        private static int Main()
        {
            Console.OutputEncoding = Encoding.UTF8;
            Console.Title = "Kalıp Takip - Mobil Sunucu";
            Console.WriteLine("Kalıp Takip mobil sunucusu başlatılıyor...");

            string mesaj = null;
            VeritabaniDurumu durum = Db.DurumBul(ref mesaj);
            if (durum != VeritabaniDurumu.Hazir)
            {
                Console.WriteLine();
                Console.WriteLine("Veritabanı bulunamadı ya da açılamadı:");
                Console.WriteLine("  " + Db.Yol);
                if (!string.IsNullOrEmpty(mesaj)) Console.WriteLine("  " + mesaj);
                Console.WriteLine("KalipTakipSunucu.exe.config içindeki VeritabaniYolu ayarına masaüstü programın kullandığı KalipTakip.db yolunu yazın.");
                Console.ReadKey();
                return 1;
            }
            try { Sema.Yukselt(); } catch (Exception ex) { HataGunlugu.Yaz(ex); }
            Yedek.Baslat();

            using (Stream s = Assembly.GetExecutingAssembly().GetManifestResourceStream("index.html"))
            using (MemoryStream ms = new MemoryStream())
            {
                s.CopyTo(ms);
                _anaSayfa = ms.ToArray();
            }

            int port;
            if (!int.TryParse(ConfigurationManager.AppSettings["Port"], out port) || port < 1) port = 8080;
            TcpListener dinleyici = new TcpListener(IPAddress.Any, port);
            try
            {
                dinleyici.Start();
            }
            catch (SocketException ex)
            {
                Console.WriteLine();
                Console.WriteLine("Sunucu başlatılamadı (port " + port + " kullanımda olabilir): " + ex.Message);
                Console.WriteLine("KalipTakipSunucu.exe.config içindeki Port değerini değiştirip (ör. 8090) tekrar deneyin.");
                Console.ReadKey();
                return 2;
            }

            Console.WriteLine();
            Console.WriteLine("Veritabanı: " + Db.Yol);
            Console.WriteLine("Telefondan şu adreslerden birini açın (aynı Wi-Fi ağında olmalı):");
            foreach (string ip in YerelAdresler()) Console.WriteLine("   http://" + ip + ":" + port + "/");
            Console.WriteLine();
            Console.WriteLine("Windows güvenlik duvarı sorarsa 'Özel ağlar' için erişime izin verin.");
            Console.WriteLine("Kapatmak için bu pencereyi kapatın. İstekler:");

            int httpsPort;
            if (!int.TryParse(ConfigurationManager.AppSettings["HttpsPort"], out httpsPort)) httpsPort = 8443;
            if (httpsPort > 0)
            {
                try
                {
                    System.Security.Cryptography.X509Certificates.X509Certificate2 sertifika = Sertifika.Al();
                    TcpListener httpsDinleyici = new TcpListener(IPAddress.Any, httpsPort);
                    httpsDinleyici.Start();
                    Console.WriteLine("Güvenli (https) adres - telefonda kamera için bunu kullanın:");
                    foreach (string ip in YerelAdresler()) Console.WriteLine("   https://" + ip + ":" + httpsPort + "/");
                    Console.WriteLine("(İlk açılışta tarayıcı 'bağlantınız gizli değil' der: Gelişmiş > Siteye ilerle deyin; kamera böylece çalışır.)");
                    Task.Run(() => Dinle(httpsDinleyici, sertifika));
                }
                catch (Exception ex)
                {
                    Console.WriteLine("https başlatılamadı (http çalışmaya devam ediyor): " + ex.Message);
                }
            }

            Dinle(dinleyici, null);
            return 0;
        }

        private static void Dinle(TcpListener dinleyici, System.Security.Cryptography.X509Certificates.X509Certificate2 sertifika)
        {
            while (true)
            {
                TcpClient istemci;
                try { istemci = dinleyici.AcceptTcpClient(); }
                catch { break; }
                Task.Run(() =>
                {
                    Baglanti ctx = null;
                    try
                    {
                        Stream akis = null;
                        if (sertifika != null)
                        {
                            istemci.ReceiveTimeout = 15000; istemci.SendTimeout = 15000;
                            System.Net.Security.SslStream ssl = new System.Net.Security.SslStream(istemci.GetStream(), false);
                            ssl.AuthenticateAsServer(sertifika, false, System.Security.Authentication.SslProtocols.Tls12, false);
                            akis = ssl;
                        }
                        ctx = Baglanti.Oku(istemci, akis);
                        if (ctx != null) Isle(ctx);
                    }
                    catch { }
                    finally { if (ctx != null) ctx.Kapat(); else try { istemci.Close(); } catch { } }
                });
            }
        }

        private static IEnumerable<string> YerelAdresler()
        {
            foreach (NetworkInterface ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.OperationalStatus != OperationalStatus.Up || ni.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
                foreach (UnicastIPAddressInformation ua in ni.GetIPProperties().UnicastAddresses)
                    if (ua.Address.AddressFamily == AddressFamily.InterNetwork) yield return ua.Address.ToString();
            }
        }

        // ------------------------------------------------------------ istek işleme

        private static void Isle(Baglanti ctx)
        {
            string yol = ctx.Request.Url.AbsolutePath.ToLowerInvariant();
            try
            {
                if (yol == "/" || yol == "/index.html") { Ham(ctx, 200, "text/html; charset=utf-8", _anaSayfa); return; }
                if (yol == "/manifest.json")
                {
                    Ham(ctx, 200, "application/json", Encoding.UTF8.GetBytes(
                        "{\"name\":\"Kalıp Takip\",\"short_name\":\"Kalıp\",\"start_url\":\"/\",\"display\":\"standalone\",\"background_color\":\"#10142b\",\"theme_color\":\"#3b2a8f\",\"icons\":[]}"));
                    return;
                }
                if (!yol.StartsWith("/api/")) { Json(ctx, 404, new { hata = "Bulunamadı." }); return; }

                if (yol == "/api/giris") { Giris(ctx); return; }

                Oturum o = OturumBul(ctx);
                if (o == null) { Json(ctx, 401, new { hata = "Oturum süresi doldu, yeniden giriş yapın." }); return; }
                Kullanici k = o.Kullanici;

                switch (yol)
                {
                    case "/api/kalip":
                        Gerekli(k, Izin.KalipListeGor);
                        {
                            int mod = ctx.Request.QueryString["mod"] == "1" ? 1 : 0;
                            DataTable dt = KalipListeRepository.Ara(ctx.Request.QueryString["q"] ?? "", mod, 100, GorunurBolumler(k));
                            Json(ctx, 200, new { satirlar = Duz(dt), sinirli = dt.Rows.Count >= 100 });
                        }
                        break;
                    case "/api/gecmis":
                        Gerekli(k, Izin.KalipListeGor);
                        {
                            string a = ctx.Request.QueryString["k"] ?? "";
                            KalipGorunurGerekli(k, a);
                            Json(ctx, 200, new
                            {
                                talepler = Duz(KalipListeRepository.TalepGecmisi(a)),
                                hareketler = Duz(KalipListeRepository.Hareketler(a)),
                                takip = Duz(KalipListeRepository.TakipKayitlari(a))
                            });
                        }
                        break;
                    case "/api/rafler":
                        Gerekli(k, Izin.KalipListeGor);
                        Json(ctx, 200, new { rafler = KalipListeRepository.Rafler() });
                        break;
                    case "/api/raf":
                        Gerekli(k, Izin.KalipListeEkle);
                        {
                            Dictionary<string, object> g = Govde(ctx);
                            string kod = Metin(g, "kod");
                            if (kod.Length == 0) throw new InvalidOperationException("Kalıp kodu boş olamaz.");
                            string raf = Metin(g, "raf");
                            KalipOzeti oz = TalepRepository.KalipOzet(kod);
                            KalipGorunurGerekli(k, oz.Bulundu ? oz.KalipKodu : kod);
                            KalipListeRepository.RafYaz(oz.Bulundu ? oz.KalipKodu : kod, raf, k.KullaniciAdi);
                            Json(ctx, 200, new { mesaj = (oz.Bulundu ? oz.KalipKodu : kod) + (raf.Length == 0 ? " raftan alındı." : " → raf " + raf), bulundu = oz.Bulundu });
                        }
                        break;
                    case "/api/durum":
                        Gerekli(k, Izin.TalepKalipDurum);
                        {
                            Dictionary<string, object> g = Govde(ctx);
                            string kod = Metin(g, "kod");
                            if (kod.Length == 0) throw new InvalidOperationException("Kalıp kodu boş olamaz.");
                            KalipGorunurGerekli(k, kod);
                            string mesajSonuc = TalepRepository.HizliDurum(kod, Metin(g, "statu"), k.KullaniciAdi);
                            Json(ctx, 200, new { mesaj = mesajSonuc });
                        }
                        break;
                    case "/api/statuler":
                        Json(ctx, 200, new { statuler = TalepKurallari.KalipStatuleri });
                        break;
                    default:
                        if (!Yonlendir(yol, k, ctx)) Json(ctx, 404, new { hata = "Bulunamadı." });
                        break;
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                Json(ctx, 403, new { hata = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                Json(ctx, 400, new { hata = ex.Message });
            }
            catch (Exception ex)
            {
                HataGunlugu.Yaz(ex);
                Json(ctx, 500, new { hata = Db.AnlasilirMesaj(ex) });
            }
            finally
            {
                try { ctx.Response.Close(); } catch { }
            }
            Console.WriteLine(DateTime.Now.ToString("HH:mm:ss") + "  " + ctx.Request.HttpMethod + " " + yol);
        }

        private static void Giris(Baglanti ctx)
        {
            Dictionary<string, object> g = Govde(ctx);
            Kullanici k = null;
            GirisSonucu sonuc = KullaniciRepository.GirisDene(Metin(g, "ad"), Metin(g, "parola"), ref k);
            switch (sonuc)
            {
                case GirisSonucu.Basarili:
                    string cihazMesaj;
                    CihazSonuc cs = GirisCihazKontrol(ctx, k, g, out cihazMesaj);
                    if (cs != CihazSonuc.Izinli)
                    {
                        Json(ctx, 403, new { hata = cihazMesaj, cihaz = cs == CihazSonuc.Bekliyor ? "bekliyor" : (cs == CihazSonuc.AdGerekli ? "ad" : "red") });
                        break;
                    }
                    string token = Guid.NewGuid().ToString("N");
                    lock (Kilit)
                    {
                        List<string> eski = new List<string>();
                        foreach (KeyValuePair<string, Oturum> p in Oturumlar) if (p.Value.Bitis < DateTime.Now) eski.Add(p.Key);
                        foreach (string e in eski) Oturumlar.Remove(e);
                        Oturumlar[token] = new Oturum { Kullanici = k, Bitis = DateTime.Now.AddHours(12), CihazHash = YerelBaglanti(ctx) ? "" : CihazRepository.KodOzeti(Metin(g, "cihaz")) };
                    }
                    Json(ctx, 200, new
                    {
                        token,
                        ad = k.KullaniciAdi,
                        bolum = k.Bolum,
                        bolumler = k.Bolumler.ToList(),
                        liste = k.IzinVar(Izin.KalipListeGor),
                        raf = k.IzinVar(Izin.KalipListeEkle),
                        durum = k.IzinVar(Izin.TalepKalipDurum),
                        admin = k.IsAdmin
                    });
                    break;
                case GirisSonucu.KullaniciYok: Json(ctx, 401, new { hata = "Böyle bir kullanıcı yok." }); break;
                case GirisSonucu.ParolaYanlis: Json(ctx, 401, new { hata = "Yanlış parola girdiniz." }); break;
                default: Json(ctx, 401, new { hata = "Bu kullanıcı pasif." }); break;
            }
        }

        private static Oturum OturumBul(Baglanti ctx)
        {
            string t = ctx.Request.Headers["X-Token"];
            if (string.IsNullOrEmpty(t)) return null;
            lock (Kilit)
            {
                Oturum o;
                if (!Oturumlar.TryGetValue(t, out o)) return null;
                if (o.Bitis < DateTime.Now) { Oturumlar.Remove(t); return null; }
                o.Bitis = DateTime.Now.AddHours(12);
                return o;
            }
        }

        private static void Gerekli(Kullanici k, string izin)
        {
            if (!k.IzinVar(izin)) throw new UnauthorizedAccessException("Bu işlem için yetkiniz yok.");
        }

        private static Dictionary<string, object> Govde(Baglanti ctx)
        {
            using (StreamReader sr = new StreamReader(ctx.Request.InputStream, Encoding.UTF8))
            {
                string s = sr.ReadToEnd();
                if (string.IsNullOrWhiteSpace(s)) return new Dictionary<string, object>();
                return Js.Deserialize<Dictionary<string, object>>(s);
            }
        }

        private static string Metin(Dictionary<string, object> g, string anahtar)
        {
            object v;
            if (!g.TryGetValue(anahtar, out v) || v == null) return "";
            return Convert.ToString(v).Trim();
        }

        // ------------------------------------------------------------ yanıt

        private static List<Dictionary<string, object>> Duz(DataTable dt)
        {
            List<Dictionary<string, object>> liste = new List<Dictionary<string, object>>();
            foreach (DataRow r in dt.Rows)
            {
                Dictionary<string, object> d = new Dictionary<string, object>();
                foreach (DataColumn c in dt.Columns)
                {
                    object v = r[c];
                    if (v == null || v == DBNull.Value) { d[c.ColumnName] = null; continue; }
                    if (v is DateTime)
                    {
                        DateTime t = (DateTime)v;
                        d[c.ColumnName] = t.TimeOfDay.TotalSeconds > 0 ? t.ToString("dd.MM.yyyy HH:mm") : t.ToString("dd.MM.yyyy");
                    }
                    else d[c.ColumnName] = v;
                }
                liste.Add(d);
            }
            return liste;
        }

        private static void Json(Baglanti ctx, int kod, object veri)
        {
            Ham(ctx, kod, "application/json; charset=utf-8", Encoding.UTF8.GetBytes(Js.Serialize(veri)));
        }

        private static void Ham(Baglanti ctx, int kod, string tur, byte[] icerik)
        {
            ctx.Response.StatusCode = kod;
            ctx.Response.ContentType = tur;
            ctx.Response.Headers["Cache-Control"] = "no-store";
            ctx.Response.ContentLength64 = icerik.Length;
            ctx.Response.OutputStream.Write(icerik, 0, icerik.Length);
        }
    }
}
