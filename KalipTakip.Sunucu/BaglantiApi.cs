using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;

namespace KalipTakip.Sunucu
{
    internal static partial class Program
    {
        private static bool Dogru(Dictionary<string, object> g, string ad)
        {
            object o; if (!g.TryGetValue(ad, out o) || o == null) return false;
            if (o is bool) return (bool)o;
            string s = Convert.ToString(o, CultureInfo.InvariantCulture).ToLowerInvariant();
            return s == "true" || s == "1" || s == "evet";
        }

        private static void YoneticiParolaDogrula(Kullanici k, Dictionary<string, object> g)
        {
            string p = Convert.ToString(g.ContainsKey("parola") ? g["parola"] : "");
            if (KullaniciRepository.Giris(k.KullaniciAdi, p) == null) throw new UnauthorizedAccessException("Yönetici parolası yanlış.");
        }

        private static Dictionary<string, object> VkJson(VkTanim t)
        {
            return new Dictionary<string, object>
            {
                { "id", t.id }, { "ad", t.ad }, { "tur", t.tur }, { "sorgu", t.sorgu }, { "anahtar", t.anahtar }, { "hedef", t.hedef },
                { "tablo", Vk.TabloAdi(t) }, { "aralikDk", t.aralikDk }, { "aktif", t.aktif }, { "silinenleriIsaretle", t.silinenleriIsaretle },
                { "sonCalisma", t.sonCalisma }, { "sonSonuc", t.sonSonuc }, { "baglantiVar", !string.IsNullOrEmpty(t.baglanti) },
                { "okuma", !string.IsNullOrWhiteSpace(t.sorgu) }, { "yazAktif", t.yazAktif }, { "yazTablo", t.yazTablo }, { "yazAnahtar", t.yazAnahtar },
                { "yazEslesme", t.yazEslesme ?? new List<VkEsle>() }, { "yazBaglantiVar", !string.IsNullOrEmpty(t.yazBaglanti) },
                { "sonYazma", t.sonYazma }, { "sonYazSonuc", t.sonYazSonuc }, { "temelVar", t.yazAktif && Vk.TemelSayisi(t.id) > 0 }
            };
        }

        private static bool BaglantiUclari(string yol, Kullanici k, Baglanti ctx)
        {
            if (!yol.StartsWith("/api/gizli/") && !yol.StartsWith("/api/vk/") && !yol.StartsWith("/api/sqlsrv/")) return false;
            YoneticiGerekli(k);
            Dictionary<string, object> g;
            switch (yol)
            {
                // ---------------------------------------------------- şifreli ayarlar
                case "/api/gizli/liste":
                    {
                        List<object> l = new List<object>();
                        if (!Gizli.Kilitli)
                            foreach (string a in Gizli.Anahtarlar().OrderBy(x => x))
                                l.Add(new { anahtar = a, deger = Gizli.Maske(Gizli.Al(a)), ic = a.StartsWith("vk.") || a == Aktarim.Anahtar });
                        Json(ctx, 200, new { kilitli = Gizli.Kilitli, kurtarmaVar = Gizli.KurtarmaVar, dosya = Gizli.Dosya, ayarlar = l, tasinacak = Gizli.TasinacakAnahtarlar });
                    }
                    return true;
                case "/api/gizli/goster":
                    g = Govde(ctx); YoneticiParolaDogrula(k, g);
                    Json(ctx, 200, new { deger = Gizli.Al(Metin(g, "anahtar")) });
                    return true;
                case "/api/gizli/kaydet":
                    g = Govde(ctx);
                    {
                        string a = Metin(g, "anahtar");
                        if (string.IsNullOrEmpty(a) || a.StartsWith("vk.")) throw new ArgumentException("Geçersiz anahtar adı.");
                        string v = g.ContainsKey("deger") ? Convert.ToString(g["deger"]) : "";
                        Gizli.Yaz(a, string.IsNullOrEmpty(v) ? null : v);
                        Json(ctx, 200, new { mesaj = "Kaydedildi (şifreli)." });
                    }
                    return true;
                case "/api/gizli/sil":
                    g = Govde(ctx); YoneticiParolaDogrula(k, g);
                    Gizli.Sil(Metin(g, "anahtar"));
                    Json(ctx, 200, new { mesaj = "Silindi." });
                    return true;
                case "/api/gizli/kurtarma":
                    g = Govde(ctx); YoneticiParolaDogrula(k, g);
                    Gizli.KurtarmaParolasiBelirle(Convert.ToString(g.ContainsKey("yeni") ? g["yeni"] : ""));
                    Json(ctx, 200, new { mesaj = "Kurtarma parolası belirlendi. Güvenli bir yere yazın; unutulursa dosya başka bilgisayarda açılamaz." });
                    return true;
                case "/api/gizli/kurtar":
                    g = Govde(ctx);
                    if (!Gizli.KurtarmaIleAc(Convert.ToString(g.ContainsKey("yeni") ? g["yeni"] : ""))) throw new UnauthorizedAccessException("Kurtarma parolası yanlış.");
                    Json(ctx, 200, new { mesaj = "Gizli ayar dosyası açıldı." });
                    return true;
                case "/api/gizli/tasi":
                    g = Govde(ctx); YoneticiParolaDogrula(k, g);
                    {
                        List<string> t = Gizli.ConfigtenTasi();
                        Json(ctx, 200, new { mesaj = t.Count == 0 ? "Taşınacak değer bulunamadı (config zaten boş)." : "Şifreli depoya taşındı, config'ten silindi: " + string.Join(", ", t) + ". Yeniden başlatma gerekebilir.", tasinan = t });
                    }
                    return true;

                // ---------------------------------------------------- IFS kaynakları
                case "/api/vk/liste":
                    Json(ctx, 200, new { kaynaklar = Vk.Liste().Select(VkJson).ToList() });
                    return true;
                case "/api/vk/kaydet":
                    g = Govde(ctx);
                    {
                        VkTanim t = new VkTanim
                        {
                            id = Metin(g, "id"), ad = Metin(g, "ad"), tur = Metin(g, "tur"), baglanti = Metin(g, "baglanti"),
                            sorgu = Metin(g, "sorgu"), anahtar = Metin(g, "anahtar"), hedef = Metin(g, "hedef"),
                            aktif = Dogru(g, "aktif"), silinenleriIsaretle = Dogru(g, "silinenleriIsaretle"),
                            yazAktif = Dogru(g, "yazAktif"), yazBaglanti = Metin(g, "yazBaglanti"), yazTablo = Metin(g, "yazTablo"), yazAnahtar = Metin(g, "yazAnahtar"),
                            yazEslesme = new List<VkEsle>()
                        };
                        object es;
                        if (g.TryGetValue("yazEslesme", out es) && es is System.Collections.IEnumerable && !(es is string))
                            foreach (object o in (System.Collections.IEnumerable)es)
                            {
                                Dictionary<string, object> d = o as Dictionary<string, object>;
                                if (d != null) t.yazEslesme.Add(new VkEsle { hedef = Convert.ToString(d.ContainsKey("hedef") ? d["hedef"] : ""), kaynak = Convert.ToString(d.ContainsKey("kaynak") ? d["kaynak"] : "") });
                            }
                        int dk; int.TryParse(Metin(g, "aralikDk"), NumberStyles.Integer, CultureInfo.InvariantCulture, out dk); t.aralikDk = dk;
                        if (t.yazAktif) YoneticiParolaDogrula(k, g);   // IFS'e yazmayı açmak için yönetici parolası
                        Vk.Kaydet(t, !string.IsNullOrEmpty(t.baglanti));
                        Json(ctx, 200, new { mesaj = "Kaydedildi." });
                    }
                    return true;
                case "/api/vk/sil":
                    Vk.Sil(Metin(Govde(ctx), "id"));
                    Json(ctx, 200, new { mesaj = "Silindi (yerel tablo korunur)." });
                    return true;
                case "/api/vk/test":
                    g = Govde(ctx);
                    {
                        string bag = Metin(g, "baglanti");
                        string id = Metin(g, "id");
                        if (bag.Length == 0 && id.Length > 0) { VkTanim m = Vk.Liste().FirstOrDefault(x => x.id == id); if (m != null) bag = m.baglanti; }
                        Json(ctx, 200, Vk.Dene(Metin(g, "tur"), bag, Metin(g, "sorgu")));
                    }
                    return true;
                case "/api/vk/cek":
                    g = Govde(ctx);
                    {
                        string id = Metin(g, "id");
                        List<VkTanim> hedefler = Vk.Liste().Where(x => !string.IsNullOrWhiteSpace(x.sorgu) && (id.Length == 0 ? x.aktif : x.id == id)).ToList();
                        if (hedefler.Count == 0) { Json(ctx, 200, new { mesaj = "Çekilecek kaynak yok. Önce Bağlantılar'dan bir kaynak ekleyin.", sonuclar = new object[0] }); return true; }
                        List<object> s = new List<object>();
                        foreach (VkTanim t in hedefler) s.Add(new { ad = t.ad, sonuc = Vk.CekVeKaydet(t) });
                        Json(ctx, 200, new { mesaj = "Veri çekme tamamlandı.", sonuclar = s });
                    }
                    return true;
                case "/api/vk/ozet":
                    Json(ctx, 200, new { adet = Vk.Liste().Count(x => x.aktif && !string.IsNullOrWhiteSpace(x.sorgu)) });
                    return true;
                case "/api/vk/alanlar":
                    Json(ctx, 200, new { alanlar = Vk.YazAlanlari.Select(a => new { ad = a.Key, tur = a.Value }).ToList() });
                    return true;
                case "/api/vk/gonder":
                    YoneticiParolaDogrula(k, g = Govde(ctx));
                    {
                        string id = Metin(g, "id"), mod = Metin(g, "mod");
                        if (mod != "tum" && mod != "temel" && mod != "degisen") throw new ArgumentException("Geçersiz gönderim türü.");
                        VkTanim t = Vk.Liste().FirstOrDefault(x => x.id == id);
                        if (t == null) throw new ArgumentException("Kaynak bulunamadı.");
                        string ozet = Vk.GonderVeKaydet(t, mod);
                        Json(ctx, 200, new { mesaj = ozet, hata = ozet.StartsWith("HATA") });
                    }
                    return true;
                case "/api/vk/gunluk":
                    {
                        DataTable dt = Vk.Gunluk(100);
                        List<Dictionary<string, object>> l = new List<Dictionary<string, object>>();
                        foreach (DataRow r in dt.Rows)
                        {
                            Dictionary<string, object> d = new Dictionary<string, object>();
                            foreach (DataColumn c in dt.Columns) d[c.ColumnName] = r[c] is DBNull ? null : r[c];
                            l.Add(d);
                        }
                        Json(ctx, 200, new { satirlar = l });
                    }
                    return true;

                // ---------------------------------------------------- SQL Server
                case "/api/sqlsrv/durum":
                    Json(ctx, 200, new { baglantiVar = !string.IsNullOrEmpty(Aktarim.Baglanti()), calisiyor = Aktarim.Calisiyor, hata = Aktarim.Hata, durum = Aktarim.Durum, rapor = Aktarim.Rapor });
                    return true;
                case "/api/sqlsrv/kaydet":
                    g = Govde(ctx);
                    {
                        string b = Metin(g, "baglanti");
                        if (b.Length == 0) { Gizli.Sil(Aktarim.Anahtar); Json(ctx, 200, new { mesaj = "SQL Server bağlantısı silindi." }); return true; }
                        Aktarim.Dene(b);
                        Gizli.Yaz(Aktarim.Anahtar, b);
                        Json(ctx, 200, new { mesaj = "Bağlantı denendi ve şifreli olarak kaydedildi." });
                    }
                    return true;
                case "/api/sqlsrv/test":
                    g = Govde(ctx);
                    {
                        string b = Metin(g, "baglanti"); if (b.Length == 0) b = Aktarim.Baglanti();
                        Json(ctx, 200, Aktarim.Dene(b));
                    }
                    return true;
                case "/api/sqlsrv/aktar":
                    g = Govde(ctx); YoneticiParolaDogrula(k, g);
                    {
                        string b = Aktarim.Baglanti();
                        if (string.IsNullOrEmpty(b)) throw new InvalidOperationException("Önce SQL Server bağlantısını kaydedin.");
                        string mod = Metin(g, "mod") == "bos" ? "bos" : "tam";
                        Aktarim.Baslat(b, mod, Dogru(g, "uzerineYaz"), Metin(g, "yoneticiAd"), Convert.ToString(g.ContainsKey("yoneticiParola") ? g["yoneticiParola"] : ""));
                        Json(ctx, 200, new { mesaj = "Aktarım başladı." });
                    }
                    return true;
            }
            return false;
        }
    }
}
