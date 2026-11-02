using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;

namespace KalipTakip.Sunucu
{
    /// <summary>Talep akışı uçları: gönder, gönderimi iptal, teslim al, geri bildir, kapat (iade), tekrar gönder, rafa kaldır, rozet.</summary>
    internal static partial class Program
    {
        private static DataRow AkisTalebi(Dictionary<string, object> g)
        {
            int id;
            if (!int.TryParse(Metin(g, "id"), NumberStyles.Integer, Inv, out id)) throw new InvalidOperationException("Talep seçilmemiş.");
            DataTable dt = TalepRepository.Satir(id);
            if (dt.Rows.Count == 0) throw new InvalidOperationException("Talep bulunamadı (silinmiş olabilir).");
            return dt.Rows[0];
        }

        private static string Bol(DataRow r, string kolon) { return r[kolon] == DBNull.Value ? "" : Convert.ToString(r[kolon]); }

        private static void GonderenYetkisi(Kullanici k, DataRow t)
        {
            Gerekli(k, Izin.TalepGonder);
            if (!k.BolumGorunur(Bol(t, "Bolum"))) throw new UnauthorizedAccessException(Bol(t, "Bolum") + " bölümü adına işlem yapma yetkiniz yok.");
        }

        private static void AliciYetkisi(Kullanici k, DataRow t, string izin)
        {
            Gerekli(k, izin);
            if (!k.BolumGorunur(Bol(t, "AliciBolum"))) throw new UnauthorizedAccessException(Bol(t, "AliciBolum") + " bölümü adına işlem yapma yetkiniz yok.");
        }

        private static List<TeslimSatiri> TeslimListesi(Dictionary<string, object> g)
        {
            List<TeslimSatiri> liste = new List<TeslimSatiri>();
            foreach (object o in GListe(g, "satirlar"))
            {
                Dictionary<string, object> d = GSoz(o);
                TeslimSatiri x = new TeslimSatiri();
                int sid;
                if (!int.TryParse(Metin(d, "SatirId"), NumberStyles.Integer, Inv, out sid)) continue;
                x.SatirId = sid; x.Lokasyon = Metin(d, "Lokasyon"); x.Raf = Metin(d, "Raf");
                x.Isle = Metin(d, "Isle") == "True" || Metin(d, "Isle") == "true" || Metin(d, "Isle") == "1";
                liste.Add(x);
            }
            return liste;
        }

        private static bool AkisUclari(string yol, Kullanici k, Baglanti ctx)
        {
            switch (yol)
            {
                case "/api/rozet":
                    {
                        bool hepsi = GorunurBolumler(k) == null;
                        Dictionary<string, int> r = TalepRepository.Rozet(hepsi ? new List<string>() : GorunurBolumler(k), hepsi);
                        Json(ctx, 200, new { gelen = r["gelen"], geriDonen = r["geriDonen"], rafBekleyen = r["rafBekleyen"], cihazBekleyen = k.IsAdmin ? CihazRepository.BekleyenSayisi() : 0 });
                    }
                    return true;

                case "/api/talep/gonder":
                    {
                        Dictionary<string, object> g = Govde(ctx);
                        DataRow t = AkisTalebi(g);
                        GonderenYetkisi(k, t);
                        AkisSonucu s = TalepRepository.Gonder(Convert.ToInt32(t["Id"]), k.KullaniciAdi);
                        Bildir(s.AliciBolum, "📦 " + s.TalepNo + ": " + s.GonderenBolum + " bölümü size " + s.Adet + " kalıp gönderdi (" + k.KullaniciAdi + "). Lütfen kontrol edip teslim alın.", s.TalepId);
                        Json(ctx, 200, new { mesaj = s.Mesaj, id = s.TalepId });
                    }
                    return true;

                case "/api/talep/gonderiptal":
                    {
                        Dictionary<string, object> g = Govde(ctx);
                        DataRow t = AkisTalebi(g);
                        GonderenYetkisi(k, t);
                        AkisSonucu s = TalepRepository.GonderimIptal(Convert.ToInt32(t["Id"]), k.KullaniciAdi);
                        Bildir(s.AliciBolum, "↩ " + s.TalepNo + ": " + s.GonderenBolum + " bölümü gönderimi iptal etti; kalıplar yolda değil.", s.TalepId);
                        Json(ctx, 200, new { mesaj = s.Mesaj, id = s.TalepId });
                    }
                    return true;

                case "/api/talep/teslim":
                    {
                        Dictionary<string, object> g = Govde(ctx);
                        DataRow t = AkisTalebi(g);
                        AliciYetkisi(k, t, Izin.TalepTeslim);
                        List<TeslimSatiri> tl = TeslimListesi(g);
                        if (!k.IzinVar(Izin.RafKaldir)) foreach (TeslimSatiri x in tl) x.Isle = false;
                        AkisSonucu s = TalepRepository.TeslimAl(Convert.ToInt32(t["Id"]), tl, k.KullaniciAdi);
                        Bildir(s.GonderenBolum, (s.Kapandi ? "✅ " : "📥 ") + s.TalepNo + ": " + s.AliciBolum + " bölümü " + s.Adet + " kalıbı teslim aldı (" + k.KullaniciAdi + ")."
                            + (s.Kapandi ? " Talep kapandı." : " Teslim alınmayan: " + s.Kalan + " kalıp."), s.TalepId);
                        if (s.Islenen > 0)
                            Bildir(s.AliciBolum, "🛠 " + s.TalepNo + ": " + s.Islenen + " kalıp İŞLENİYOR durumuna alındı (" + k.KullaniciAdi + "). Grup liderleri dikkat: bu kalıplar raftan indirilmiş, tamirde.", s.TalepId, Izin.RafKaldir);
                        Json(ctx, 200, new { mesaj = s.Mesaj, id = s.TalepId, kalan = s.Kalan, kapandi = s.Kapandi });
                    }
                    return true;

                case "/api/talep/isle":
                    {
                        Dictionary<string, object> g = Govde(ctx);
                        DataRow t = AkisTalebi(g);
                        AliciYetkisi(k, t, Izin.RafKaldir);
                        List<int> idler = new List<int>();
                        foreach (TeslimSatiri x in TeslimListesi(g)) idler.Add(x.SatirId);
                        AkisSonucu s = TalepRepository.IsleAl(Convert.ToInt32(t["Id"]), idler, k.KullaniciAdi);
                        Bildir(s.AliciBolum, "🛠 " + s.TalepNo + ": " + s.Islenen + " kalıp raftan indirilip İŞLENİYOR durumuna alındı (" + k.KullaniciAdi + ").", s.TalepId, Izin.RafKaldir);
                        Json(ctx, 200, new { mesaj = s.Mesaj, id = s.TalepId });
                    }
                    return true;

                case "/api/talep/geribildir":
                    {
                        Dictionary<string, object> g = Govde(ctx);
                        DataRow t = AkisTalebi(g);
                        AliciYetkisi(k, t, Izin.TalepTeslim);
                        AkisSonucu s = TalepRepository.GeriBildir(Convert.ToInt32(t["Id"]), k.KullaniciAdi);
                        Bildir(s.GonderenBolum, "⚠ " + s.TalepNo + ": " + s.AliciBolum + " bölümüne ulaşmayan " + s.Kalan + " kalıp var. Talebi kapatıp kalıpları iade edebilir ya da tekrar gönderebilirsiniz.", s.TalepId);
                        Json(ctx, 200, new { mesaj = s.Mesaj, id = s.TalepId });
                    }
                    return true;

                case "/api/talep/gerikapat":
                    {
                        Dictionary<string, object> g = Govde(ctx);
                        DataRow t = AkisTalebi(g);
                        GonderenYetkisi(k, t);
                        AkisSonucu s = TalepRepository.GeriKapat(Convert.ToInt32(t["Id"]), k.KullaniciAdi);
                        Bildir(s.AliciBolum, "🔒 " + s.TalepNo + ": " + s.GonderenBolum + " bölümü talebi kapattı; ulaşmayan " + s.Adet + " kalıp iade edildi olarak işaretlendi.", s.TalepId);
                        Json(ctx, 200, new { mesaj = s.Mesaj, id = s.TalepId });
                    }
                    return true;

                case "/api/talep/tekrar":
                    {
                        Dictionary<string, object> g = Govde(ctx);
                        DataRow t = AkisTalebi(g);
                        GonderenYetkisi(k, t);
                        AkisSonucu s = TalepRepository.TekrarGonder(Convert.ToInt32(t["Id"]), k.KullaniciAdi);
                        Bildir(s.AliciBolum, "📦 " + s.TalepNo + ": " + s.GonderenBolum + " bölümü " + s.Kalan + " kalıbı tekrar gönderdi (" + k.KullaniciAdi + ").", s.TalepId);
                        Json(ctx, 200, new { mesaj = s.Mesaj, id = s.TalepId });
                    }
                    return true;

                case "/api/talep/rafa":
                    {
                        Dictionary<string, object> g = Govde(ctx);
                        DataRow t = AkisTalebi(g);
                        AliciYetkisi(k, t, Izin.RafKaldir);
                        AkisSonucu s = TalepRepository.RafaKaldir(Convert.ToInt32(t["Id"]), TeslimListesi(g), k.KullaniciAdi);
                        Json(ctx, 200, new { mesaj = s.Mesaj, id = s.TalepId });
                    }
                    return true;
            }
            return false;
        }
    }
}
