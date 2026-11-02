using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;

namespace KalipTakip.Sunucu
{
    /// <summary>
    /// Cihaz yetkilendirmesi (sunucu tarafı): girişte cihaz kontrolü, istemci MAC'i (aynı ağdaki cihazlar için ARP ile),
    /// yönetici uçları (liste, onay/ret/silme, kontrolü aç/kapat, muaf kullanıcılar).
    /// </summary>
    internal static partial class Program
    {
        [DllImport("iphlpapi.dll", ExactSpelling = true)]
        private static extern int SendARP(int hedefIp, int kaynakIp, byte[] mac, ref int uzunluk);

        // İstemcinin MAC adresi: yalnızca sunucuyla aynı yerel ağdaki (yönlendirmesiz) cihazlar için okunabilir.
        // Farklı alt ağdan gelen istekte yönlendiricinin MAC'i görünebilir; bu nedenle MAC bilgi amaçlıdır, cihaz tanıma kodu esastır.
        private static string MacBul(string ip)
        {
            try
            {
                IPAddress a;
                if (!IPAddress.TryParse(ip, out a) || a.AddressFamily != AddressFamily.InterNetwork || IPAddress.IsLoopback(a)) return "";
                int hedef = BitConverter.ToInt32(a.GetAddressBytes(), 0);
                byte[] mac = new byte[6];
                int uz = 6;
                if (SendARP(hedef, 0, mac, ref uz) != 0 || uz < 6) return "";
                return string.Join(":", mac.Select(x => x.ToString("X2")).ToArray());
            }
            catch { return ""; }
        }

        private static bool YerelBaglanti(Baglanti ctx)
        {
            IPAddress a;
            return IPAddress.TryParse(ctx.IstemciIp ?? "", out a) && IPAddress.IsLoopback(a);
        }

        // Sunucu bilgisayarının kendisinden (localhost) girenler cihaz kontrolüne takılmaz; yönetici kilitlenirse kurtarma yoludur.
        private static CihazSonuc GirisCihazKontrol(Baglanti ctx, Kullanici k, Dictionary<string, object> g, out string mesaj)
        {
            mesaj = "";
            if (YerelBaglanti(ctx)) return CihazSonuc.Izinli;
            string ip = ctx.IstemciIp ?? "";
            bool yeni = false;
            CihazSonuc s = CihazRepository.Kontrol(k.Id, Metin(g, "cihaz"), Metin(g, "cihazAd"), MacBul(ip), ip, Metin(g, "bilgi"), "Tarayıcı", ref mesaj, ref yeni);
            if (yeni)
                Bildir("*YONETICI*", "🔐 Yeni cihaz erişim isteği: " + k.KullaniciAdi + (Metin(g, "cihazAd").Length > 0 ? " (" + Metin(g, "cihazAd") + ")" : "") + " - IP " + ip + ". Portal > Cihaz Erişimleri'nden onaylayın.", 0);
            return s;
        }

        private static void CihazOturumlariniKapat(int kullaniciId, string kodHash)
        {
            lock (Kilit)
            {
                List<string> sil = new List<string>();
                foreach (KeyValuePair<string, Oturum> p in Oturumlar)
                    if (p.Value.Kullanici != null && p.Value.Kullanici.Id == kullaniciId && p.Value.CihazHash == kodHash) sil.Add(p.Key);
                foreach (string t in sil) Oturumlar.Remove(t);
            }
        }

        private static bool CihazUclari(string yol, Kullanici k, Baglanti ctx)
        {
            switch (yol)
            {
                case "/api/cihaz/liste":
                    YoneticiGerekli(k);
                    Json(ctx, 200, new
                    {
                        satirlar = DuzIso(CihazRepository.Liste(Q(ctx, "durum"), QTam(ctx, "kul", 0))),
                        kontrol = CihazRepository.KontrolAcik(),
                        bekleyen = CihazRepository.BekleyenSayisi()
                    });
                    return true;

                case "/api/cihaz/karar":
                    YoneticiGerekli(k);
                    {
                        Dictionary<string, object> g = Govde(ctx);
                        int id = int.Parse(Metin(g, "id"), CultureInfo.InvariantCulture);
                        string durum = Metin(g, "durum");
                        int kid = 0; string hash = "";
                        if (!CihazRepository.Karar(id, durum, k.KullaniciAdi, ref kid, ref hash)) throw new InvalidOperationException("Cihaz kaydı bulunamadı.");
                        if (durum != CihazRepository.Onayli) CihazOturumlariniKapat(kid, hash);
                        Json(ctx, 200, new { mesaj = durum == CihazRepository.Onayli ? "Cihaz onaylandı." : (durum == CihazRepository.Reddedildi ? "Cihaz reddedildi; açık oturumları kapatıldı." : "Cihaz tekrar onay bekliyor durumuna alındı.") });
                    }
                    return true;

                case "/api/cihaz/sil":
                    YoneticiGerekli(k);
                    {
                        int id = int.Parse(Metin(Govde(ctx), "id"), CultureInfo.InvariantCulture);
                        int kid = 0; string hash = "";
                        if (!CihazRepository.Sil(id, ref kid, ref hash)) throw new InvalidOperationException("Cihaz kaydı zaten silinmiş.");
                        CihazOturumlariniKapat(kid, hash);
                        Json(ctx, 200, new { mesaj = "Cihaz kaydı silindi; bu cihaz yeniden giriş denerse yeni istek açılır." });
                    }
                    return true;

                case "/api/cihaz/ayar":
                    YoneticiGerekli(k);
                    {
                        List<Dictionary<string, object>> kul = new List<Dictionary<string, object>>();
                        foreach (DataRow r in CihazRepository.KullaniciMuafListesi().Rows)
                        {
                            Dictionary<string, object> d = new Dictionary<string, object>();
                            d["id"] = Convert.ToInt32(r["Id"]);
                            d["ad"] = Convert.ToString(r["KullaniciAdi"]);
                            d["muaf"] = Convert.ToInt32(r["Muaf"]) == 1;
                            kul.Add(d);
                        }
                        Json(ctx, 200, new { kontrol = CihazRepository.KontrolAcik(), kullanicilar = kul, bekleyen = CihazRepository.BekleyenSayisi() });
                    }
                    return true;

                case "/api/cihaz/ayar/kaydet":
                    YoneticiGerekli(k);
                    {
                        Dictionary<string, object> g = Govde(ctx);
                        string kontrol = Metin(g, "kontrol");
                        if (kontrol.Length > 0) CihazRepository.KontrolYaz(kontrol == "True" || kontrol == "true" || kontrol == "1");
                        string kid = Metin(g, "kullaniciId");
                        if (kid.Length > 0)
                        {
                            string m = Metin(g, "muaf");
                            CihazRepository.MuafYaz(int.Parse(kid, CultureInfo.InvariantCulture), m == "True" || m == "true" || m == "1");
                        }
                        Json(ctx, 200, new { mesaj = "Kaydedildi." });
                    }
                    return true;
            }
            return false;
        }
    }
}
