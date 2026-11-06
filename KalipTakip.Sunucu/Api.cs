using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KalipTakip.Sunucu
{
    /// <summary>Tarayıcı arayüzünün kullandığı API: masaüstündeki tüm modüller (Kalıp Takip, Talep, Kalıp Listesi, Raflar, Lokasyonlar, Kullanıcılar).</summary>
    internal static partial class Program
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private static readonly CultureInfo TrK = new CultureInfo("tr-TR");

        // ------------------------------------------------------------ yardımcılar

        private static string Q(Baglanti ctx, string ad) { return (ctx.Request.QueryString[ad] ?? "").Trim(); }

        private static int QTam(Baglanti ctx, string ad, int varsayilan)
        {
            int n;
            return int.TryParse(Q(ctx, ad), NumberStyles.Integer, Inv, out n) ? n : varsayilan;
        }

        private static DateTime? Tarih(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return null;
            DateTime d;
            if (DateTime.TryParseExact(s.Trim().Substring(0, Math.Min(10, s.Trim().Length)), "yyyy-MM-dd", Inv, DateTimeStyles.None, out d)) return d.Date;
            return null;
        }

        private static DateTime? GTarih(Dictionary<string, object> g, string ad) { return Tarih(Metin(g, ad)); }

        private static double? GSayi(Dictionary<string, object> g, string ad, string etiket)
        {
            string s = Metin(g, ad);
            if (s.Length == 0) return null;
            double d;
            if (!double.TryParse(s.Replace(',', '.'), NumberStyles.Float, Inv, out d))
                throw new InvalidOperationException(etiket + " alanına sayı yazın.");
            if (d < 0) throw new InvalidOperationException(etiket + " alanı negatif olamaz.");
            return d;
        }

        private static int? GTam(Dictionary<string, object> g, string ad, string etiket)
        {
            double? d = GSayi(g, ad, etiket);
            if (!d.HasValue) return null;
            if (Math.Abs(d.Value - Math.Round(d.Value)) > 0.000001)
                throw new InvalidOperationException(etiket + " alanına tam sayı yazın.");
            return (int)Math.Round(d.Value);
        }

        private static List<object> GListe(Dictionary<string, object> g, string ad)
        {
            object v;
            if (!g.TryGetValue(ad, out v) || v == null) return new List<object>();
            System.Collections.ArrayList al = v as System.Collections.ArrayList;
            if (al != null) return al.Cast<object>().ToList();
            object[] arr = v as object[];
            if (arr != null) return arr.ToList();
            return new List<object>();
        }

        private static Dictionary<string, object> GSoz(object o)
        {
            return o as Dictionary<string, object> ?? new Dictionary<string, object>();
        }

        private static bool GBool(Dictionary<string, object> g, string ad)
        {
            object v;
            if (!g.TryGetValue(ad, out v) || v == null) return false;
            if (v is bool) return (bool)v;
            string s = Convert.ToString(v).ToLowerInvariant();
            return s == "true" || s == "1";
        }

        // Tarihler "yyyy-MM-dd" (düzenleme formları için), diğer değerler olduğu gibi.
        private static List<Dictionary<string, object>> DuzIso(DataTable dt)
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
                        d[c.ColumnName] = t.TimeOfDay.TotalSeconds > 0 ? t.ToString("yyyy-MM-dd HH:mm", Inv) : t.ToString("yyyy-MM-dd", Inv);
                    }
                    else d[c.ColumnName] = v;
                }
                liste.Add(d);
            }
            return liste;
        }

        private class KtYukleme { public string Yol; public string Kullanici; public DateTime Zaman; }
        private static readonly Dictionary<string, KtYukleme> KtYuklemeler = new Dictionary<string, KtYukleme>();

        // Toplu yükleme bölüm kısıtı taşımaz: yalnızca tüm bölümleri görebilen kullanıcılar kullanabilir.
        private static void KtYukleKisitla(Kullanici k)
        {
            if (GorunurBolumler(k) != null) throw new UnauthorizedAccessException("Toplu yükleme yalnızca tüm bölümleri görebilen kullanıcılar içindir.");
        }

        private static string CsvHucre(object v)
        {
            if (v == null || v == DBNull.Value) return "";
            string s;
            if (v is DateTime) s = ((DateTime)v).TimeOfDay.TotalSeconds > 0 ? ((DateTime)v).ToString("dd.MM.yyyy HH:mm", TrK) : ((DateTime)v).ToString("dd.MM.yyyy", TrK);
            else if (v is double || v is float || v is decimal) s = Convert.ToDouble(v).ToString("0.######", TrK);
            else s = Convert.ToString(v, TrK);
            if (s.IndexOfAny(new[] { ';', '"', '\r', '\n' }) >= 0) s = "\"" + s.Replace("\"", "\"\"") + "\"";
            return s;
        }

        private static void CsvGonder(Baglanti ctx, string dosya, DataTable dt, KolonTanimi[] kolonlar)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(string.Join(";", kolonlar.Select(k => CsvHucre(k.Baslik)))).Append("\r\n");
            foreach (DataRow r in dt.Rows)
                sb.Append(string.Join(";", kolonlar.Select(k => dt.Columns.Contains(k.Alan) ? CsvHucre(r[k.Alan]) : ""))).Append("\r\n");
            byte[] icerik = new UTF8Encoding(true).GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
            ctx.Response.Headers["Content-Disposition"] = "attachment; filename=\"" + dosya + "\"";
            Ham(ctx, 200, "text/csv; charset=utf-8", icerik);
        }

        // ------------------------------------------------------------ bölüm görünürlüğü

        // null = kullanıcı her şeyi görür; liste = yalnızca bu bölümlerin (ve sahibi atanmamış) kalıpları.
        private static List<string> GorunurBolumler(Kullanici k)
        {
            if (k.IsAdmin || k.Misafir || k.TumBolumler) return null;
            return k.Bolumler.Select(b => (b ?? "").Trim()).Where(b => b.Length > 0).ToList();
        }

        // Kalıp, sahibi bölüm ya da şu an bulunduğu bölüm (lokasyonunun bölümü) görünüyorsa görünür; sahibi atanmamışsa herkese görünür.
        private static Dictionary<string, string> LokasyonBolumleri()
        {
            Dictionary<string, string> h = new Dictionary<string, string>();
            foreach (DataRow r in Db.Tablo("SELECT Ad, Bolum FROM Lokasyon").Rows)
                h[TrMetin.Katla(Convert.ToString(r["Ad"]).Trim())] = Convert.ToString(r["Bolum"]);
            return h;
        }

        private static bool KalipGorunur(Kullanici k, string kod)
        {
            if (GorunurBolumler(k) == null) return true;
            DataTable dt = Db.Tablo("SELECT SahipBolum, Lokasyon FROM KalipLokasyon WHERE Anahtar = ?", TrMetin.Katla((kod ?? "").Trim()));
            if (dt.Rows.Count == 0) return true;
            string sahip = dt.Rows[0]["SahipBolum"] == DBNull.Value ? "" : Convert.ToString(dt.Rows[0]["SahipBolum"]).Trim();
            if (sahip.Length == 0 || k.BolumGorunur(sahip)) return true;
            string lok = dt.Rows[0]["Lokasyon"] == DBNull.Value ? "" : Convert.ToString(dt.Rows[0]["Lokasyon"]).Trim();
            string holder;
            return lok.Length > 0 && LokasyonBolumleri().TryGetValue(TrMetin.Katla(lok), out holder) && k.BolumGorunur(holder);
        }

        private static void KalipGorunurGerekli(Kullanici k, string kod)
        {
            if (!KalipGorunur(k, kod)) throw new UnauthorizedAccessException((kod ?? "").Trim() + " kalıbı bölümünüze ait değil; görme yetkiniz yok.");
        }

        // Kalıp kodu sütunu olan bir tablodan, kullanıcının göremeyeceği bölümlerin kalıplarını çıkarır.
        private static DataTable KalipSuz(Kullanici k, DataTable dt, string kodKolon)
        {
            if (GorunurBolumler(k) == null || !dt.Columns.Contains(kodKolon)) return dt;
            Dictionary<string, string> lokBolum = LokasyonBolumleri();
            Dictionary<string, bool> gizli = new Dictionary<string, bool>();
            foreach (DataRow r in Db.Tablo("SELECT Anahtar, SahipBolum, Lokasyon FROM KalipLokasyon WHERE SahipBolum IS NOT NULL AND TRIM(SahipBolum) <> ''").Rows)
            {
                string sahip = Convert.ToString(r["SahipBolum"]);
                bool gorunur = k.BolumGorunur(sahip);
                string lok = r["Lokasyon"] == DBNull.Value ? "" : Convert.ToString(r["Lokasyon"]).Trim();
                string holder;
                if (!gorunur && lok.Length > 0 && lokBolum.TryGetValue(TrMetin.Katla(lok), out holder) && k.BolumGorunur(holder)) gorunur = true;
                gizli[Convert.ToString(r["Anahtar"])] = !gorunur;
            }
            DataTable sonuc = dt.Clone();
            foreach (DataRow r in dt.Rows)
            {
                object v = r[kodKolon];
                bool g;
                if (v != DBNull.Value && gizli.TryGetValue(TrMetin.Katla(Convert.ToString(v).Trim()), out g) && g) continue;
                sonuc.ImportRow(r);
            }
            return sonuc;
        }

        private static DataTable Ilk(DataTable dt, int n)
        {
            if (dt.Rows.Count <= n) return dt;
            DataTable sonuc = dt.Clone();
            for (int i = 0; i < n; i++) sonuc.ImportRow(dt.Rows[i]);
            return sonuc;
        }

        // Bölüm sütunu olan bir tablodan (talepler), kullanıcının göremeyeceği bölümleri çıkarır.
        // Talepte gönderen ya da alıcı bölümden biri görünüyorsa talep görünür.
        private static DataTable BolumSuz(Kullanici k, DataTable dt, string bolumKolon)
        {
            if (GorunurBolumler(k) == null || !dt.Columns.Contains(bolumKolon)) return dt;
            bool aliciVar = dt.Columns.Contains("AliciBolum");
            DataTable sonuc = dt.Clone();
            foreach (DataRow r in dt.Rows)
            {
                bool ok = r[bolumKolon] == DBNull.Value || k.BolumGorunur(Convert.ToString(r[bolumKolon]));
                if (!ok && aliciVar && r["AliciBolum"] != DBNull.Value && k.BolumGorunur(Convert.ToString(r["AliciBolum"]))) ok = true;
                if (ok) sonuc.ImportRow(r);
            }
            return sonuc;
        }

        // ------------------------------------------------------------ yönlendirme

        private static bool Yonlendir(string yol, Kullanici k, Baglanti ctx)
        {
            if (BaglantiUclari(yol, k, ctx)) return true;
            if (BildirimUclari(yol, k, ctx)) return true;
            if (AkisUclari(yol, k, ctx)) return true;
            if (CihazUclari(yol, k, ctx)) return true;
            switch (yol)
            {
                case "/api/meta": Meta(k, ctx); return true;
                case "/api/okut":
                    Gerekli(k, Izin.KalipListeGor);
                    {
                        System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();
                        string metin = YapayZekaOku(Metin(Govde(ctx), "resim"));
                        Json(ctx, 200, new { metin, ms = sw.ElapsedMilliseconds });
                    }
                    return true;
                case "/api/okut/isin":
                    // Kamera açılırken Google'a bağlantı kurdurur; ilk gerçek okuma TLS el sıkışmasını beklemez.
                    Gerekli(k, Izin.KalipListeGor);
                    Task.Run(() => { try { HttpGetIsin("https://generativelanguage.googleapis.com/"); } catch { } });
                    Json(ctx, 200, new { tamam = true });
                    return true;
                case "/api/kodlar":
                    Gerekli(k, Izin.KalipListeGor);
                    {
                        DataTable dt = Db.Tablo("SELECT ParcaKodu AS Kod FROM Kalip WHERE ParcaKodu IS NOT NULL AND TRIM(ParcaKodu) <> '' " +
                                                "UNION SELECT KalipKodu FROM KalipTanim WHERE KalipKodu IS NOT NULL UNION SELECT KalipKodu FROM TalepKalip WHERE KalipKodu IS NOT NULL");
                        dt = KalipSuz(k, dt, "Kod");
                        List<string> kodListe = dt.Rows.Cast<DataRow>().Select(r => Convert.ToString(r["Kod"]).Trim()).Distinct().ToList();
                        HashSet<string> kume = new HashSet<string>(kodListe);
                        Dictionary<string, string> adlar = new Dictionary<string, string>();
                        foreach (DataRow r in Db.Tablo("SELECT KalipKodu AS Kod, KalipAdi AS Ad FROM KalipTanim WHERE KalipKodu IS NOT NULL AND KalipAdi IS NOT NULL AND TRIM(KalipAdi) <> ''").Rows)
                        {
                            string kk = Convert.ToString(r["Kod"]).Trim();
                            if (kume.Contains(kk) && !adlar.ContainsKey(kk)) adlar[kk] = Convert.ToString(r["Ad"]).Trim();
                        }
                        foreach (DataRow r in Db.Tablo("SELECT ParcaKodu AS Kod, ParcaAdi AS Ad FROM Kalip WHERE ParcaKodu IS NOT NULL AND ParcaAdi IS NOT NULL AND TRIM(ParcaAdi) <> '' ORDER BY Id DESC").Rows)
                        {
                            string kk = Convert.ToString(r["Kod"]).Trim();
                            if (kume.Contains(kk) && !adlar.ContainsKey(kk)) adlar[kk] = Convert.ToString(r["Ad"]).Trim();
                        }
                        Json(ctx, 200, new { kodlar = kodListe, adlar });
                    }
                    return true;

                // ---- Kalıp Takip
                case "/api/kt/ara":
                    Gerekli(k, Izin.KalipGor);
                    Json(ctx, 200, new { satirlar = DuzIso(KalipSuz(k, KalipRepository.Ara(Q(ctx, "alan"), Q(ctx, "q"), QTam(ctx, "limit", 200), Q(ctx, "bolum")), "ParcaKodu")) });
                    return true;
                case "/api/kt/kaydet": KtKaydet(k, ctx, false); return true;
                case "/api/kt/tashih": KtKaydet(k, ctx, true); return true;
                case "/api/kt/sil":
                    Gerekli(k, Izin.Sil);
                    {
                        int id = int.Parse(Metin(Govde(ctx), "id"), Inv);
                        if (!KalipRepository.Sil(id)) throw new InvalidOperationException("Kayıt zaten silinmiş.");
                        Json(ctx, 200, new { mesaj = "#" + id + " silindi." });
                    }
                    return true;
                case "/api/kt/istatistik":
                    Gerekli(k, Izin.Istatistik);
                    Json(ctx, 200, new { satirlar = DuzIso(KalipRepository.Istatistik(Tarih(Q(ctx, "bas")), Tarih(Q(ctx, "bit")), Q(ctx, "bolum"), Q(ctx, "hafta"))) });
                    return true;
                case "/api/kt/haftalar":
                    Gerekli(k, Izin.Istatistik);
                    {
                        var liste = KalipRepository.Haftalar().Rows.Cast<DataRow>().Select(r =>
                        {
                            string h = Convert.ToString(r["Hafta"]).Trim();
                            string[] pr = h.Split('/');
                            int w = 0, y = 0;
                            if (pr.Length == 2) { int.TryParse(pr[0], NumberStyles.Integer, Inv, out w); int.TryParse(pr[1], NumberStyles.Integer, Inv, out y); }
                            return new { hafta = h, adet = Convert.ToInt32(r["Adet"]), w, y };
                        }).OrderByDescending(x => x.y).ThenByDescending(x => x.w).Select(x => new { x.hafta, x.adet }).ToList();
                        Json(ctx, 200, new { haftalar = liste });
                    }
                    return true;
                case "/api/kt/excel":
                    Gerekli(k, Izin.Excel);
                    CsvGonder(ctx, "kalip_takip.csv", KalipSuz(k, KalipRepository.Ara(Q(ctx, "alan"), Q(ctx, "q"), 0, Q(ctx, "bolum")), "ParcaKodu"), KalipRepository.Kolonlar);
                    return true;

                case "/api/kt/yukle":
                    Gerekli(k, Izin.KalipYukle);
                    KtYukleKisitla(k);
                    {
                        Dictionary<string, object> g = Govde(ctx);
                        string ad = Metin(g, "ad");
                        string uzanti = Path.GetExtension(ad).ToLowerInvariant();
                        if (uzanti != ".xlsx" && uzanti != ".csv" && uzanti != ".txt") throw new InvalidOperationException("Yalnızca .xlsx veya .csv dosyası yüklenebilir.");
                        byte[] veri = Convert.FromBase64String(Metin(g, "veri"));
                        string gecici = Path.Combine(Path.GetTempPath(), "ktkayit_" + Guid.NewGuid().ToString("N") + uzanti);
                        File.WriteAllBytes(gecici, veri);
                        KalipRepository.KtYuklemeSonuc o;
                        try { o = KalipRepository.DosyadanYukle(gecici, k.KullaniciAdi, false, false, k.IzinVar(Izin.Ekle), k.IzinVar(Izin.Guncelle)); }
                        catch { try { File.Delete(gecici); } catch { } throw; }
                        string jeton = Guid.NewGuid().ToString("N");
                        lock (KtYuklemeler)
                        {
                            foreach (string eski in KtYuklemeler.Where(x => x.Value.Zaman < DateTime.Now.AddMinutes(-30)).Select(x => x.Key).ToList())
                            {
                                try { File.Delete(KtYuklemeler[eski].Yol); } catch { }
                                KtYuklemeler.Remove(eski);
                            }
                            KtYuklemeler[jeton] = new KtYukleme { Yol = gecici, Kullanici = k.KullaniciAdi, Zaman = DateTime.Now };
                        }
                        Json(ctx, 200, new { jeton, yeni = o.Yeni, degisen = o.Degisen, ayni = o.Ayni, hataSayisi = o.HataSayisi, hatalar = o.Hatalar, ornekler = o.Ornekler });
                    }
                    return true;
                case "/api/kt/yukle/onayla":
                    Gerekli(k, Izin.KalipYukle);
                    KtYukleKisitla(k);
                    {
                        Dictionary<string, object> g = Govde(ctx);
                        string jeton = Metin(g, "jeton");
                        KtYukleme y;
                        lock (KtYuklemeler)
                        {
                            if (!KtYuklemeler.TryGetValue(jeton, out y) || !string.Equals(y.Kullanici, k.KullaniciAdi, StringComparison.OrdinalIgnoreCase))
                                throw new InvalidOperationException("Önizleme süresi dolmuş. Dosyayı yeniden seçin.");
                            KtYuklemeler.Remove(jeton);
                        }
                        try
                        {
                            KalipRepository.KtYuklemeSonuc o = KalipRepository.DosyadanYukle(y.Yol, k.KullaniciAdi, true, Metin(g, "atla") == "1", k.IzinVar(Izin.Ekle), k.IzinVar(Izin.Guncelle));
                            Json(ctx, 200, new { mesaj = o.Degisen + " kayıt güncellendi, " + o.Yeni + " yeni kayıt eklendi, " + o.Ayni + " kayıt zaten aynıydı" + (o.HataSayisi > 0 ? "; " + o.HataSayisi + " hatalı satır atlandı." : ".") });
                        }
                        finally { try { File.Delete(y.Yol); } catch { } }
                    }
                    return true;

                // ---- Talep
                case "/api/talep/ara":
                    Gerekli(k, Izin.TalepGor);
                    Json(ctx, 200, new
                    {
                        satirlar = DuzIso(BolumSuz(k, TalepRepository.Ara(Q(ctx, "q"), Q(ctx, "statu"), Q(ctx, "bolum"), Tarih(Q(ctx, "bas")), Tarih(Q(ctx, "bit")), QTam(ctx, "limit", 100)), "Bolum"))
                    });
                    return true;
                case "/api/talep/getir":
                    Gerekli(k, Izin.TalepGor);
                    {
                        int id = QTam(ctx, "id", 0);
                        DataTable t = TalepRepository.Satir(id);
                        if (t.Rows.Count == 0) throw new InvalidOperationException("Talep bulunamadı (silinmiş olabilir).");
                        if (t.Rows[0]["Bolum"] != DBNull.Value && !k.BolumGorunur(Convert.ToString(t.Rows[0]["Bolum"]))
                            && !(t.Rows[0]["AliciBolum"] != DBNull.Value && k.BolumGorunur(Convert.ToString(t.Rows[0]["AliciBolum"]))))
                            throw new UnauthorizedAccessException("Bu talep bölümünüze ait değil.");
                        Json(ctx, 200, new { talep = DuzIso(t)[0], satirlar = DuzIso(TalepRepository.SatirlariGetir(id)) });
                    }
                    return true;
                case "/api/talep/kalipara":
                    Gerekli(k, Izin.TalepGor);
                    Json(ctx, 200, new { satirlar = DuzIso(Ilk(KalipSuz(k, TalepRepository.KalipAra(Q(ctx, "q"), 40), "ParcaKodu"), 12)) });
                    return true;
                case "/api/talep/ozet":
                    Gerekli(k, Izin.TalepGor);
                    {
                        string kod = Q(ctx, "kod");
                        KalipOzeti oz = TalepRepository.KalipOzet(kod);
                        KalipGorunurGerekli(k, oz.Bulundu ? oz.KalipKodu : kod);
                        string engel = TalepRepository.EklemeEngeli(oz.Bulundu ? oz.KalipKodu : kod, QTam(ctx, "id", 0), Q(ctx, "bolum"));
                        Json(ctx, 200, new { engel, ozet = oz });
                    }
                    return true;
                case "/api/talep/kaydet": TalepKaydet(k, ctx); return true;
                case "/api/talep/sil":
                    Gerekli(k, Izin.TalepSil);
                    {
                        int id = int.Parse(Metin(Govde(ctx), "id"), Inv);
                        DataTable sd = TalepRepository.Satir(id);
                        if (sd.Rows.Count > 0 && sd.Rows[0]["GonderTarihi"] != DBNull.Value
                            && Convert.ToString(sd.Rows[0]["Statu"]) != TalepKurallari.TalepKapandi && Convert.ToString(sd.Rows[0]["Statu"]) != TalepKurallari.TalepIptal)
                            throw new InvalidOperationException("Gönderilmiş talep silinemez. Önce gönderimi iptal edin.");
                        if (!TalepRepository.Sil(id)) throw new InvalidOperationException("Talep zaten silinmiş.");
                        Json(ctx, 200, new { mesaj = "Talep silindi." });
                    }
                    return true;
                case "/api/talep/excel":
                    Gerekli(k, Izin.TalepExcel);
                    CsvGonder(ctx, "talepler.csv", BolumSuz(k, TalepRepository.Ara(Q(ctx, "q"), Q(ctx, "statu"), Q(ctx, "bolum"), Tarih(Q(ctx, "bas")), Tarih(Q(ctx, "bit")), 0), "Bolum"), TalepRepository.Kolonlar);
                    return true;

                // ---- Kalıp Listesi
                case "/api/kl/kaydet":
                    {
                        Dictionary<string, object> g = Govde(ctx);
                        string kod = Metin(g, "kod");
                        if (kod.Length == 0) throw new InvalidOperationException("Kalıp kodu boş olamaz.");
                        // var olan kalıbı değiştirmek ayrı (kritik) yetkidir; istemcinin gönderdiği "duzenle" bayrağına güvenilmez
                        Gerekli(k, KalipListeRepository.KalipVarMi(kod) ? Izin.KalipListeDuzenle : Izin.KalipListeEkle);
                        KalipGorunurGerekli(k, kod);
                        KalipListeRepository.Kaydet(kod, Metin(g, "ad"), Metin(g, "tur"), Metin(g, "tepe"), Metin(g, "notlar"),
                            Metin(g, "lokasyon"), Metin(g, "sahip"), Metin(g, "raf"), k.KullaniciAdi, GBool(g, "duzenle"));
                        Json(ctx, 200, new { mesaj = kod + " kaydedildi." });
                    }
                    return true;
                case "/api/kl/yukle":
                    Gerekli(k, Izin.KalipListeYukle);
                    {
                        Dictionary<string, object> g = Govde(ctx);
                        string ad = Metin(g, "ad");
                        string uzanti = Path.GetExtension(ad).ToLowerInvariant();
                        if (uzanti != ".xlsx" && uzanti != ".csv" && uzanti != ".txt") throw new InvalidOperationException("Yalnızca .xlsx veya .csv dosyası yüklenebilir.");
                        byte[] veri = Convert.FromBase64String(Metin(g, "veri"));
                        string gecici = Path.Combine(Path.GetTempPath(), "kt_" + Guid.NewGuid().ToString("N") + uzanti);
                        File.WriteAllBytes(gecici, veri);
                        try { Json(ctx, 200, new { mesaj = KalipListeRepository.DosyadanYukle(gecici, k.KullaniciAdi) }); }
                        finally { try { File.Delete(gecici); } catch { } }
                    }
                    return true;
                case "/api/kl/onar":
                    YoneticiGerekli(k);
                    {
                        bool uygula = Metin(Govde(ctx), "uygula") == "1";
                        string[] s = KalipListeRepository.VeriOnar(uygula).Split('|');
                        Json(ctx, 200, new { lokasyon = long.Parse(s[0]), tur = long.Parse(s[1]), tip = long.Parse(s[2]), kalan = long.Parse(s[3]), uygulandi = uygula });
                    }
                    return true;
                case "/api/kl/excel":
                    Gerekli(k, Izin.KalipListeExcel);
                    CsvGonder(ctx, "kalip_listesi.csv", KalipListeRepository.Ara(Q(ctx, "q"), QTam(ctx, "mod", 0), 0, GorunurBolumler(k), Q(ctx, "alan")), KalipListeRepository.Kolonlar);
                    return true;

                // ---- Kalıp - bölüm ataması (kalıbın sahibi olan bölüm; kullanıcılar yalnızca kendi bölümlerinin kalıplarını görür)
                case "/api/atama/ara":
                    Gerekli(k, Izin.KalipListeEkle);
                    {
                        bool yalnizBos = Q(ctx, "bos") == "1";
                        DataTable dt = KalipListeRepository.Ara(Q(ctx, "q"), 0, 0, GorunurBolumler(k));
                        DataTable sonuc = dt.Clone();
                        foreach (DataRow r in dt.Rows)
                        {
                            bool bos = r["SahipBolum"] == DBNull.Value || Convert.ToString(r["SahipBolum"]).Trim().Length == 0;
                            if (yalnizBos && !bos) continue;
                            sonuc.ImportRow(r);
                            if (sonuc.Rows.Count >= 500) break;
                        }
                        int bosSayi = dt.Rows.Cast<DataRow>().Count(r => r["SahipBolum"] == DBNull.Value || Convert.ToString(r["SahipBolum"]).Trim().Length == 0);
                        Json(ctx, 200, new { satirlar = Duz(sonuc), toplam = dt.Rows.Count, atanmamis = bosSayi });
                    }
                    return true;
                case "/api/atama/kaydet":
                    Gerekli(k, Izin.KalipListeEkle);
                    {
                        Dictionary<string, object> g = Govde(ctx);
                        string bolum = Metin(g, "bolum").ToUpper(TrK);
                        List<string> kodlar = GListe(g, "kodlar").Select(o => Convert.ToString(o).Trim()).Where(x => x.Length > 0).Distinct().ToList();
                        if (kodlar.Count == 0) throw new InvalidOperationException("Önce kalıp seçin.");
                        if (bolum.Length > 0 && !LokasyonRepository.Bolumler().Any(b => string.Equals(b, bolum, StringComparison.OrdinalIgnoreCase)))
                            throw new InvalidOperationException("'" + bolum + "' adında bir bölüm tanımlı değil (Bölümler ve Lokasyonlar ekranından tanımlayın).");
                        foreach (string kod in kodlar) KalipGorunurGerekli(k, kod);
                        int n = KalipListeRepository.SahipYaz(kodlar, bolum, k.KullaniciAdi);
                        Json(ctx, 200, new { mesaj = n + " kalıbın sahip bölümü " + (bolum.Length == 0 ? "kaldırıldı." : bolum + " olarak kaydedildi.") });
                    }
                    return true;

                // ---- Raflar
                case "/api/raflar":
                    Gerekli(k, Izin.KalipListeGor);
                    Json(ctx, 200, new { satirlar = Duz(RafRepository.Listele()) });
                    return true;
                case "/api/kl/kodduzelt":
                    YoneticiGerekli(k);
                    {
                        Dictionary<string, object> g = Govde(ctx);
                        Json(ctx, 200, new { mesaj = KalipListeRepository.KodDuzelt(Metin(g, "eski"), Metin(g, "yeni"), k.KullaniciAdi) });
                    }
                    return true;
                case "/api/kl/sil":
                    Gerekli(k, Izin.KalipListeSil);
                    {
                        Dictionary<string, object> g = Govde(ctx);
                        Json(ctx, 200, new { mesaj = KalipListeRepository.KalipSil(Metin(g, "kod"), GBool(g, "zorla")) });
                    }
                    return true;
                case "/api/kl/temizsil":
                    Gerekli(k, Izin.KalipListeSil);
                    {
                        List<string> kodlar = GListe(Govde(ctx), "kodlar").Select(o => Convert.ToString(o)).ToList();
                        int atlanan = 0;
                        int silinen = KalipListeRepository.TemizSil(kodlar, ref atlanan);
                        Json(ctx, 200, new { mesaj = silinen + " kalıp silindi" + (atlanan > 0 ? "; " + atlanan + " kalıp kaydı/geçmişi olduğu için atlandı." : ".") });
                    }
                    return true;
                case "/api/kalip/sahip":
                    {
                        DataTable dt = Db.Tablo("SELECT SahipBolum FROM KalipLokasyon WHERE Anahtar = ?", TrMetin.Katla((Q(ctx, "kod") ?? "").Trim()));
                        string sahipBolum = dt.Rows.Count > 0 && dt.Rows[0]["SahipBolum"] != DBNull.Value ? Convert.ToString(dt.Rows[0]["SahipBolum"]).Trim() : "";
                        Json(ctx, 200, new { sahip = sahipBolum });
                    }
                    return true;
                // ---- Liste değerleri: kalıp türleri ve kayıt türleri (yönetici)
                case "/api/liste":
                    YoneticiGerekli(k);
                    Json(ctx, 200, new { satirlar = Duz(ListeRepository.Listele(Q(ctx, "grup"), Q(ctx, "bolum"))) });
                    return true;
                case "/api/liste/ekle":
                    YoneticiGerekli(k);
                    {
                        Dictionary<string, object> g = Govde(ctx);
                        ListeRepository.Ekle(Metin(g, "grup"), Metin(g, "deger"), Metin(g, "bolum"));
                        Json(ctx, 200, new { mesaj = "Eklendi." });
                    }
                    return true;
                case "/api/liste/ad":
                    YoneticiGerekli(k);
                    {
                        Dictionary<string, object> g = Govde(ctx);
                        int n = ListeRepository.Adlandir(Metin(g, "grup"), Metin(g, "eski"), Metin(g, "yeni"), Metin(g, "bolum"));
                        Json(ctx, 200, new { mesaj = "Ad değiştirildi" + (n > 0 ? "; " + n + " kayıt güncellendi." : ".") });
                    }
                    return true;
                case "/api/liste/ata":
                    YoneticiGerekli(k);
                    {
                        Dictionary<string, object> g = Govde(ctx);
                        string bol = Metin(g, "bolum");
                        int n = ListeRepository.BolumeAta(Metin(g, "deger"), bol, k.KullaniciAdi);
                        Json(ctx, 200, new { mesaj = "\"" + Metin(g, "deger") + "\" türü " + bol.ToUpper(new System.Globalization.CultureInfo("tr-TR")) + " bölümüne bağlandı; " + n + " kalıbın sahip bölümü güncellendi." });
                    }
                    return true;
                case "/api/liste/temizle":
                    YoneticiGerekli(k);
                    {
                        Dictionary<string, object> g = Govde(ctx);
                        int n = ListeRepository.Temizle(Metin(g, "grup"), Metin(g, "deger"), Metin(g, "bolum"));
                        Json(ctx, 200, new { mesaj = "Listeden kaldırıldı; " + n + " kayıttaki değer boşaltıldı." });
                    }
                    return true;
                case "/api/liste/sil":
                    YoneticiGerekli(k);
                    {
                        Dictionary<string, object> g = Govde(ctx);
                        ListeRepository.Sil(Metin(g, "grup"), Metin(g, "deger"), Metin(g, "bolum"));
                        Json(ctx, 200, new { mesaj = "Silindi." });
                    }
                    return true;
                case "/api/raflar/ekle":
                    Gerekli(k, Izin.KalipListeEkle);
                    {
                        string yeniRaf = RafRepository.Ekle(Metin(Govde(ctx), "ad"));
                        Json(ctx, 200, new { mesaj = yeniRaf + " rafı oluşturuldu.", ad = yeniRaf });
                    }
                    return true;
                case "/api/raflar/kaydet":
                    Gerekli(k, Izin.KalipListeEkle);
                    {
                        List<string> adlar = GListe(Govde(ctx), "adlar").Select(o => Convert.ToString(o)).ToList();
                        RafRepository.Kaydet(adlar);
                        Json(ctx, 200, new { mesaj = "Raflar kaydedildi." });
                    }
                    return true;

                // ---- Bölümler / lokasyonlar (yönetici)
                case "/api/lok":
                    Json(ctx, 200, new { satirlar = Duz(LokasyonRepository.Listele()) });
                    return true;
                case "/api/lok/kaydet":
                    YoneticiGerekli(k);
                    {
                        List<LokasyonKaydi> liste = new List<LokasyonKaydi>();
                        foreach (object o in GListe(Govde(ctx), "liste"))
                        {
                            Dictionary<string, object> d = GSoz(o);
                            LokasyonKaydi x = new LokasyonKaydi();
                            x.Ad = Metin(d, "Ad"); x.Bolum = Metin(d, "Bolum"); x.AnaMi = GBool(d, "AnaMi");
                            liste.Add(x);
                        }
                        string sonuc = LokasyonRepository.Kaydet(liste);
                        Json(ctx, 200, new { mesaj = string.IsNullOrEmpty(sonuc) ? "Kaydedildi." : sonuc });
                    }
                    return true;

                // ---- Kullanıcılar (yönetici)
                case "/api/kul/liste":
                    YoneticiGerekli(k);
                    {
                        DataTable dt = KullaniciRepository.Listele();
                        List<Dictionary<string, object>> sonuc = new List<Dictionary<string, object>>();
                        foreach (DataRow r in dt.Rows) sonuc.Add(KullaniciSoz(KullaniciRepository.Getir(Convert.ToInt32(r["Id"]))));
                        Json(ctx, 200, new { satirlar = sonuc });
                    }
                    return true;
                case "/api/kul/kaydet": KullaniciKaydet(k, ctx); return true;
                case "/api/kritik":
                    YoneticiGerekli(k);
                    {
                        List<object> liste = new List<object>();
                        foreach (DataRow r in KullaniciRepository.Listele().Rows)
                        {
                            Kullanici h = KullaniciRepository.Getir(Convert.ToInt32(r["Id"]));
                            if (h == null || h.IsAdmin || !h.Aktif) continue;
                            var kr = Izin.Tanimlar.Where(t => Izin.Kritik.Contains(t.Anahtar) && h.Izinler.Contains(t.Anahtar)).Select(t => new { anahtar = t.Anahtar, ad = t.Ad }).ToList();
                            if (kr.Count > 0) liste.Add(new { id = h.Id, ad = h.KullaniciAdi, izinler = kr });
                        }
                        Json(ctx, 200, new { kullanicilar = liste });
                    }
                    return true;
                case "/api/kritik/geri":
                    YoneticiGerekli(k);
                    {
                        Dictionary<string, object> g = Govde(ctx);
                        int id = int.Parse(Metin(g, "id"), Inv);
                        string anahtar = Metin(g, "anahtar");
                        if (!Izin.Kritik.Contains(anahtar)) throw new InvalidOperationException("Bu yetki kritik yetki listesinde değil.");
                        KullaniciRepository.IzinKaldir(id, anahtar);
                        OturumYenile(id);
                        Json(ctx, 200, new { mesaj = "Yetki geri alındı." });
                    }
                    return true;
                case "/api/kul/sil":
                    YoneticiGerekli(k);
                    {
                        int id = int.Parse(Metin(Govde(ctx), "id"), Inv);
                        Kullanici h = KullaniciRepository.Getir(id);
                        if (h == null) throw new InvalidOperationException("Kullanıcı bulunamadı.");
                        if (h.Id == k.Id) throw new InvalidOperationException("Kendi hesabınızı silemezsiniz.");
                        if (h.IsAdmin && h.Aktif && KullaniciRepository.AktifYoneticiSayisi() <= 1) throw new InvalidOperationException("Son aktif yönetici silinemez.");
                        KullaniciRepository.Sil(id);
                        Json(ctx, 200, new { mesaj = h.KullaniciAdi + " silindi." });
                    }
                    return true;
            }
            return false;
        }

        private static void YoneticiGerekli(Kullanici k)
        {
            if (!k.IsAdmin) throw new UnauthorizedAccessException("Bu işlem yalnızca yöneticiler içindir.");
        }

        // ------------------------------------------------------------ yapay zeka ile yazı okuma

        // Kameradan alınan kırpılmış fotoğrafı (base64 JPEG) Claude görüntü modeline gönderir; okunan metni döner.
        // Anahtar ve model App.config'te: AnthropicAnahtar, AnthropicModel. Sunucu bilgisayarının api.anthropic.com'a erişimi olmalı.
        private static string Ayar(string ad)
        {
            string v = Gizli.AyarAl(ad);
            return v == null ? "" : v.Trim();
        }

        private const string OkumaIstemi = "Metal üzerine dövülmüş/kazınmış kod. Sadece okunan karakterleri yaz; görüntüde karakterler arasında boşluk varsa boşluğu aynen koru, tire (-) yoksa kendin ekleme (örnekler: 1123 K 1,  2363, DK1156-1, SKS01833-3, S01833). Kod çoğunlukla 1-3 büyük harfle (DK, SKS, S gibi) başlar, rakamlarla devam eder; baştaki karakter 5 gibi görünse bile harf (S) olabilir, bağlama göre karar ver. Okunamıyorsa YOK.";

        // Ayarlardaki anahtara göre Gemini (ücretsiz katman) ya da Claude kullanılır; YZSaglayici = claude yazılırsa Claude öncelikli olur.
        private static string YapayZekaOku(string resimBase64)
        {
            if (string.IsNullOrWhiteSpace(resimBase64)) throw new InvalidOperationException("Görüntü gelmedi.");
            int virgul = resimBase64.IndexOf(',');
            if (virgul >= 0 && resimBase64.StartsWith("data:", StringComparison.OrdinalIgnoreCase)) resimBase64 = resimBase64.Substring(virgul + 1);
            bool gemini = Ayar("GeminiAnahtar").Length > 0;
            bool claude = Ayar("AnthropicAnahtar").Length > 0;
            if (!gemini && !claude)
                throw new InvalidOperationException("Yapay zeka ile okuma kapalı: KalipTakipSunucu.exe.config dosyasına GeminiAnahtar (ya da AnthropicAnahtar) yazılmamış.");
            if (claude && (!gemini || string.Equals(Ayar("YZSaglayici"), "claude", StringComparison.OrdinalIgnoreCase))) return ClaudeOku(resimBase64);
            return GeminiOku(resimBase64);
        }

        // Hız için yarış: ana model hemen başlar; 4 sn içinde cevap gelmezse (ya da hata verirse) yedek model de başlar,
        // ilk başarılı cevap kullanılır.
        private static string GeminiOku(string resimBase64)
        {
            string ana = Ayar("GeminiModel");
            if (ana.Length == 0) ana = "gemini-3.8-flash";
            string yedek = Ayar("GeminiYedekModel");
            if (yedek.Length == 0) yedek = "gemini-flash-lite-latest";
            if (string.Equals(yedek, ana, StringComparison.OrdinalIgnoreCase)) return GeminiGuvenli(ana, resimBase64);

            Task<string> t1 = Task.Run(() => GeminiGuvenli(ana, resimBase64));
            try { if (t1.Wait(4000)) return t1.Result; } catch (AggregateException) { }
            Task<string> t2 = Task.Run(() => GeminiGuvenli(yedek, resimBase64));
            List<Task<string>> bekleyen = new List<Task<string>>();
            if (!t1.IsCompleted) bekleyen.Add(t1);
            bekleyen.Add(t2);
            string sonHata = "Yapay zeka cevap vermedi.";
            if (t1.IsFaulted && t1.Exception != null) sonHata = t1.Exception.InnerException.Message;
            while (bekleyen.Count > 0)
            {
                int i = Task.WaitAny(bekleyen.ToArray(), 20000);
                if (i < 0) break;
                Task<string> t = bekleyen[i];
                if (t.Status == TaskStatus.RanToCompletion) return t.Result;
                if (t.Exception != null) sonHata = t.Exception.InnerException.Message;
                bekleyen.RemoveAt(i);
            }
            throw new InvalidOperationException(sonHata);
        }

        // Düşünme düzeyi reddedilirse o ayar olmadan bir kez daha dener.
        private static string GeminiGuvenli(string model, string resimBase64)
        {
            try { return GeminiIstek(model, resimBase64); }
            catch (InvalidOperationException ex)
            {
                if (ex.Message.IndexOf("hinking", StringComparison.OrdinalIgnoreCase) < 0) throw;
                _dusunmeKapali = true;
                return GeminiIstek(model, resimBase64);
            }
        }

        private static void HttpGetIsin(string adres)
        {
            System.Net.ServicePointManager.SecurityProtocol = System.Net.SecurityProtocolType.Tls12;
            System.Net.HttpWebRequest r = (System.Net.HttpWebRequest)System.Net.WebRequest.Create(adres);
            r.Method = "HEAD"; r.Timeout = 8000; r.KeepAlive = true;
            try
            {
                System.Net.IWebProxy vekil = System.Net.WebRequest.GetSystemWebProxy();
                vekil.Credentials = System.Net.CredentialCache.DefaultCredentials;
                r.Proxy = vekil;
            }
            catch { }
            try { using (r.GetResponse()) { } }
            catch (System.Net.WebException) { }
        }

        private static bool _dusunmeKapali = false;

        private static string GeminiIstek(string model, string resimBase64)
        {
            Dictionary<string, object> inline = new Dictionary<string, object>();
            inline["mime_type"] = "image/jpeg"; inline["data"] = resimBase64;
            Dictionary<string, object> parcaResim = new Dictionary<string, object>();
            parcaResim["inline_data"] = inline;
            Dictionary<string, object> parcaYazi = new Dictionary<string, object>();
            parcaYazi["text"] = OkumaIstemi;
            Dictionary<string, object> icerik = new Dictionary<string, object>();
            icerik["parts"] = new object[] { parcaResim, parcaYazi };
            Dictionary<string, object> ayar = new Dictionary<string, object>();
            ayar["temperature"] = 0; ayar["maxOutputTokens"] = 200;
            string dusun = _dusunmeKapali ? "" : Ayar("GeminiDusunme");
            if (dusun.Length > 0)
            {
                Dictionary<string, object> dc = new Dictionary<string, object>();
                dc["thinkingLevel"] = dusun;
                ayar["thinkingConfig"] = dc;
            }
            Dictionary<string, object> istek = new Dictionary<string, object>();
            istek["contents"] = new object[] { icerik };
            istek["generationConfig"] = ayar;
            string yanit = HttpPost("https://generativelanguage.googleapis.com/v1beta/models/" + Uri.EscapeDataString(model) + ":generateContent",
                                    Js.Serialize(istek), "x-goog-api-key", Ayar("GeminiAnahtar"), null, 12000);
            Dictionary<string, object> j = Js.Deserialize<Dictionary<string, object>>(yanit);
            foreach (object c in GListe(j, "candidates"))
            {
                Dictionary<string, object> cd = GSoz(GSoz(c).ContainsKey("content") ? GSoz(c)["content"] : null);
                foreach (object p in GListe(cd, "parts"))
                {
                    Dictionary<string, object> pd = GSoz(p);
                    if (pd.ContainsKey("text")) return Metin(pd, "text");
                }
            }
            return "";
        }

        private static string HttpPost(string adres, string govde, string anahtarBaslik, string anahtar, string ekBaslik, int zamanAsimi)
        {
            System.Net.ServicePointManager.SecurityProtocol = System.Net.SecurityProtocolType.Tls12;
            System.Net.ServicePointManager.Expect100Continue = false;
            System.Net.HttpWebRequest r = (System.Net.HttpWebRequest)System.Net.WebRequest.Create(adres);
            r.Method = "POST";
            r.ContentType = "application/json";
            r.Headers[anahtarBaslik] = anahtar;
            if (ekBaslik != null) { string[] p = ekBaslik.Split('='); r.Headers[p[0]] = p[1]; }
            // Şirket ağındaki vekil sunucu (proxy) için Windows oturumunun kimliği kullanılır.
            try
            {
                System.Net.IWebProxy vekil = System.Net.WebRequest.GetSystemWebProxy();
                vekil.Credentials = System.Net.CredentialCache.DefaultCredentials;
                r.Proxy = vekil;
            }
            catch { }
            r.Timeout = zamanAsimi;
            r.ReadWriteTimeout = zamanAsimi;
            r.KeepAlive = true;
            byte[] b = Encoding.UTF8.GetBytes(govde);
            r.ContentLength = b.Length;
            string asama = "bağlanırken";
            System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                using (Stream st = r.GetRequestStream()) { asama = "görüntü gönderilirken (" + (b.Length / 1024) + " KB)"; st.Write(b, 0, b.Length); }
                asama = "Google'ın cevabı beklenirken";
                using (System.Net.WebResponse yanit = r.GetResponse())
                using (StreamReader sr = new StreamReader(yanit.GetResponseStream(), Encoding.UTF8))
                    return sr.ReadToEnd();
            }
            catch (System.Net.WebException ex)
            {
                string ayrinti = "";
                if (ex.Response != null)
                    using (StreamReader sr = new StreamReader(ex.Response.GetResponseStream(), Encoding.UTF8)) ayrinti = sr.ReadToEnd();
                throw new InvalidOperationException("Yapay zeka servisi (" + new Uri(adres).Host + ") — " + asama + " " + (sw.ElapsedMilliseconds / 1000) + " sn sonra hata: " + ex.Message + (ayrinti.Length > 0 ? " " + (ayrinti.Length > 300 ? ayrinti.Substring(0, 300) : ayrinti) : ""));
            }
        }

        private static string ClaudeOku(string resimBase64)
        {
            string model = Ayar("AnthropicModel");
            if (model.Length == 0) model = "claude-haiku-5-5";
            Dictionary<string, object> istek = new Dictionary<string, object>();
            istek["model"] = model;
            istek["max_tokens"] = 60;
            Dictionary<string, object> kaynak = new Dictionary<string, object>();
            kaynak["type"] = "base64"; kaynak["media_type"] = "image/jpeg"; kaynak["data"] = resimBase64;
            Dictionary<string, object> resim = new Dictionary<string, object>();
            resim["type"] = "image"; resim["source"] = kaynak;
            Dictionary<string, object> yazi = new Dictionary<string, object>();
            yazi["type"] = "text"; yazi["text"] = OkumaIstemi;
            Dictionary<string, object> mesaj = new Dictionary<string, object>();
            mesaj["role"] = "user"; mesaj["content"] = new object[] { resim, yazi };
            istek["messages"] = new object[] { mesaj };
            string yanit = HttpPost("https://api.anthropic.com/v1/messages", Js.Serialize(istek), "x-api-key", Ayar("AnthropicAnahtar"), "anthropic-version=2023-06-01", 30000);
            Dictionary<string, object> j = Js.Deserialize<Dictionary<string, object>>(yanit);
            foreach (object o in GListe(j, "content"))
            {
                Dictionary<string, object> blok = GSoz(o);
                if (Metin(blok, "type") == "text") return Metin(blok, "text");
            }
            return "";
        }

        // ------------------------------------------------------------ meta

        // Açılır liste değerleri: tanımlı liste + kalıp kayıtlarında gerçekten kullanılmış değerler (liste boşsa da seçenek çıksın).
        private static List<string> EkSecenek(List<string> l, params string[] ek)
        {
            foreach (string e in ek) if (!l.Contains(e, StringComparer.OrdinalIgnoreCase)) l.Add(e);
            return l;
        }

        // Sahip bölüme göre kalıp türü seçenekleri: ortak değerler + bölüme tanımlılar + o bölümün kalıplarında kullanılanlar.
        private static Dictionary<string, List<string>> KalipTurBolum(IEnumerable<string> bolumler)
        {
            CultureInfo tr = new CultureInfo("tr-TR");
            Dictionary<string, List<string>> sonuc = new Dictionary<string, List<string>>();
            foreach (string b in bolumler) sonuc[(b ?? "").Trim().ToUpper(tr)] = ListeRepository.GetirBolum(ListeRepository.GrupKalipTuru, b);
            try
            {
                DataTable dt = Db.Tablo("SELECT DISTINCT l.SahipBolum AS B, TRIM(k.KalipTuru) AS T FROM Kalip k JOIN KalipLokasyon l ON l.Anahtar = KATLA(k.ParcaKodu) " +
                                        "WHERE k.KalipTuru IS NOT NULL AND TRIM(k.KalipTuru) <> '' AND l.SahipBolum IS NOT NULL AND TRIM(l.SahipBolum) <> ''");
                foreach (DataRow r in dt.Rows)
                {
                    string anahtar = Convert.ToString(r["B"]).Trim().ToUpper(tr), t = Convert.ToString(r["T"]).Trim();
                    if (!sonuc.ContainsKey(anahtar)) sonuc[anahtar] = ListeRepository.GetirBolum(ListeRepository.GrupKalipTuru, anahtar);
                    if (t.Length > 0 && !sonuc[anahtar].Contains(t, StringComparer.OrdinalIgnoreCase)) sonuc[anahtar].Add(t);
                }
            }
            catch { }
            return sonuc;
        }

        private static List<string> SecenekListesi(string grup, string kolon)
        {
            List<string> sonuc = ListeRepository.Getir(grup);
            try
            {
                DataTable dt = Db.Tablo("SELECT DISTINCT " + kolon + " AS D FROM Kalip WHERE " + kolon + " IS NOT NULL AND TRIM(" + kolon + ") <> '' ORDER BY " + kolon);
                foreach (DataRow r in dt.Rows)
                {
                    string d = Convert.ToString(r["D"]).Trim();
                    if (d.Length > 0 && !sonuc.Contains(d, StringComparer.OrdinalIgnoreCase)) sonuc.Add(d);
                }
            }
            catch { }
            return sonuc;
        }

        private static void Meta(Kullanici k, Baglanti ctx)
        {
            List<string> izinler = Izin.Tanimlar.Where(t => k.IzinVar(t.Anahtar)).Select(t => t.Anahtar).ToList();
            Json(ctx, 200, new
            {
                ad = k.KullaniciAdi,
                ai = !string.IsNullOrWhiteSpace(Ayar("GeminiAnahtar")) || !string.IsNullOrWhiteSpace(Ayar("AnthropicAnahtar")),
                bolum = k.Bolum,
                kBolumler = k.Bolumler.ToList(),
                tumBolum = k.IsAdmin || k.TumBolumler,
                admin = k.IsAdmin,
                izinler,
                tanimlar = Izin.Tanimlar.Select(t => new { grup = t.Grup, anahtar = t.Anahtar, ad = t.Ad, grupAnahtari = t.GrupAnahtari }).ToList(),
                ktKolonlar = KalipRepository.Kolonlar.Select(c => new { alan = c.Alan, baslik = c.Baslik, tip = c.Tip.ToString() }).ToList(),
                aramaAlanlari = KalipRepository.AramaAlanlari.Select(a => new { ad = a.Key, alan = a.Value }).ToList(),
                talepKolonlar = TalepRepository.Kolonlar.Select(c => new { alan = c.Alan, baslik = c.Baslik, tip = c.Tip.ToString() }).ToList(),
                klKolonlar = KalipListeRepository.Kolonlar.Select(c => new { alan = c.Alan, baslik = c.Baslik, tip = c.Tip.ToString() }).ToList(),
                kalipTurleri = SecenekListesi(ListeRepository.GrupKalipTuru, "KalipTuru"),
                kalipTurBolum = KalipTurBolum(TalepRepository.BolumListesi()),
                turler = EkSecenek(SecenekListesi(ListeRepository.GrupTur, "Tur"), "TAMİR", "HURDA"),
                kalipStatuleri = TalepKurallari.KalipStatuleri,
                talepStatuleri = TalepKurallari.TalepStatuleri,
                oncelikler = TalepRepository.Oncelikler,
                bolumler = TalepRepository.BolumListesi(),
                lokasyonlar = LokasyonRepository.Adlar(),
                lokasyonDetay = Duz(LokasyonRepository.Listele()),
                rafler = RafRepository.Adlar()
            });
        }

        // ------------------------------------------------------------ Kalıp Takip

        private static Kalip KalipOku(Dictionary<string, object> g)
        {
            if (Metin(g, "ParcaKodu").Length == 0) throw new InvalidOperationException("Parça kodu boş olamaz.");
            Kalip k = new Kalip();
            k.ParcaKodu = Metin(g, "ParcaKodu");
            k.KalipTuru = Metin(g, "KalipTuru");
            k.TepeKodu = Metin(g, "TepeKodu");
            k.ParcaAdi = Metin(g, "ParcaAdi");
            k.GelisTarihi = GTarih(g, "GelisTarihi");
            k.BitisTarihi = GTarih(g, "BitisTarihi");
            k.Tur = Metin(g, "Tur");
            k.Aciklama = Metin(g, "Aciklama");
            k.TashihSayi = GTam(g, "TashihSayi", "Tashih sayısı");
            k.CncNo = GTam(g, "CncNo", "CNC No");
            k.DovmeAdeti = GSayi(g, "DovmeAdeti", "Dövme adeti");
            k.IslemSuresi = GSayi(g, "IslemSuresi", "İşlem süresi");
            k.Ayar = GSayi(g, "Ayar", "Ayar");
            k.Bakim = GSayi(g, "Bakim", "Bakım");
            k.ElemanEksikligi = GSayi(g, "ElemanEksikligi", "Eleman eksikliği");
            k.Elektrik = GSayi(g, "Elektrik", "Elektrik");
            k.KalipAriza = GSayi(g, "KalipAriza", "Kalıp arıza");
            k.Takim = GSayi(g, "Takim", "Takım");
            k.Ariza = GSayi(g, "Ariza", "Arıza");
            double? t = null;
            foreach (double? d in new[] { k.IslemSuresi, k.Ayar, k.Bakim, k.ElemanEksikligi, k.Elektrik, k.KalipAriza, k.Takim, k.Ariza })
                if (d.HasValue) t = (t ?? 0.0) + d.Value;
            k.ToplamZaman = t.HasValue ? (double?)Math.Round(t.Value, 2, MidpointRounding.AwayFromZero) : null;
            k.Hafta = TarihYardimci.HaftaHesapla(k.BitisTarihi);
            return k;
        }

        private static void KtKaydet(Kullanici kul, Baglanti ctx, bool tashih)
        {
            Dictionary<string, object> g = Govde(ctx);
            Kalip k = KalipOku(g);
            int id = 0;
            int.TryParse(Metin(g, "Id"), NumberStyles.Integer, Inv, out id);
            int surum = 1;
            int.TryParse(Metin(g, "Surum"), NumberStyles.Integer, Inv, out surum);
            if (tashih)
            {
                Gerekli(kul, Izin.Tashih);
                if (id <= 0) throw new InvalidOperationException("Önce listeden bir kayıt seçin.");
                DateTime? islem = GTarih(g, "IslemTarihi");
                if (!islem.HasValue) throw new InvalidOperationException("Tashih kaydı için işlem tarihini girin (yeni kaydın geliş tarihi olur).");
                k.Id = id; k.Surum = surum;
                if (!KalipRepository.Guncelle(k, kul.KullaniciAdi))
                    throw new InvalidOperationException("Kayıt siz açtıktan sonra başka biri tarafından değiştirilmiş ya da silinmiş. Listeyi yenileyip tekrar deneyin.");
                Kalip y = new Kalip();
                y.ParcaKodu = k.ParcaKodu; y.KalipTuru = k.KalipTuru; y.TepeKodu = k.TepeKodu; y.ParcaAdi = k.ParcaAdi;
                y.GelisTarihi = islem.Value;
                y.TashihSayi = (k.TashihSayi ?? 0) + 1;
                y.Tur = "TASHİH";
                int yeniId = KalipRepository.Ekle(y, kul.KullaniciAdi);
                Json(ctx, 200, new { mesaj = "#" + id + " güncellendi; tashih kaydı #" + yeniId + " açıldı (tashih sayısı " + y.TashihSayi + ").", id = yeniId });
                return;
            }
            if (id > 0)
            {
                Gerekli(kul, Izin.Guncelle);
                k.Id = id; k.Surum = surum;
                if (!KalipRepository.Guncelle(k, kul.KullaniciAdi))
                    throw new InvalidOperationException("Kayıt siz açtıktan sonra başka biri tarafından değiştirilmiş ya da silinmiş. Listeyi yenileyip tekrar deneyin.");
                Json(ctx, 200, new { mesaj = "#" + id + " güncellendi.", id });
            }
            else
            {
                Gerekli(kul, Izin.Ekle);
                int yeni = KalipRepository.Ekle(k, kul.KullaniciAdi);
                Json(ctx, 200, new { mesaj = "Yeni kayıt eklendi (#" + yeni + ").", id = yeni });
            }
        }

        // ------------------------------------------------------------ Talep

        private static void TalepKaydet(Kullanici kul, Baglanti ctx)
        {
            Dictionary<string, object> g = Govde(ctx);
            int id = 0;
            int.TryParse(Metin(g, "id"), NumberStyles.Integer, Inv, out id);
            bool durumYetkisi = kul.IzinVar(Izin.TalepKalipDurum);

            Talep t = new Talep();
            t.Bolum = Metin(g, "bolum").ToUpper(TrK);
            if (t.Bolum.Length == 0) throw new InvalidOperationException("'Gönderen bölüm' seçilmelidir.");
            string alici = Metin(g, "aliciBolum").ToUpper(TrK);
            if (alici.Length == 0) throw new InvalidOperationException("'Alıcı bölüm' seçilmelidir.");
            if (TrMetin.Katla(alici) == TrMetin.Katla(t.Bolum)) throw new InvalidOperationException("Gönderen ve alıcı bölüm aynı olamaz.");
            if (!kul.BolumGorunur(t.Bolum)) throw new UnauthorizedAccessException(t.Bolum + " bölümü adına işlem yapma yetkiniz yok.");
            t.Oncelik = Metin(g, "oncelik");
            t.TalepTarihi = GTarih(g, "talepTarihi") ?? DateTime.Today;
            t.TerminTarihi = GTarih(g, "terminTarihi");
            t.TalepEden = Metin(g, "talepEden");
            t.Aciklama = Metin(g, "aciklama");
            t.Sonuc = Metin(g, "sonuc");

            Dictionary<int, DataRow> eskiSatirlar = new Dictionary<int, DataRow>();
            Talep mevcut = null;
            bool gonderilmis = false;
            if (id > 0)
            {
                Gerekli(kul, Izin.TalepGuncelle);
                DataTable dt = TalepRepository.Satir(id);
                if (dt.Rows.Count == 0) throw new InvalidOperationException("Talep silinmiş.");
                mevcut = Talep.SatirdanOku(dt.Rows[0]);
                if (!kul.BolumGorunur(mevcut.Bolum)) throw new UnauthorizedAccessException("Bu talep bölümünüze ait değil.");
                gonderilmis = dt.Rows[0]["GonderTarihi"] != DBNull.Value;
                if (gonderilmis)
                {
                    string eskiAlici = dt.Rows[0]["AliciBolum"] == DBNull.Value ? "" : Convert.ToString(dt.Rows[0]["AliciBolum"]);
                    if (TrMetin.Katla(eskiAlici) != TrMetin.Katla(alici)) throw new InvalidOperationException("Gönderilmiş talebin alıcı bölümü değiştirilemez.");
                    if (TrMetin.Katla(mevcut.Bolum) != TrMetin.Katla(t.Bolum)) throw new InvalidOperationException("Gönderilmiş talebin gönderen bölümü değiştirilemez.");
                }
                foreach (DataRow r in TalepRepository.SatirlariGetir(id).Rows) eskiSatirlar[Convert.ToInt32(r["SatirId"])] = r;
                t.Id = id;
                t.TalepNo = mevcut.TalepNo;
                int s;
                t.Surum = int.TryParse(Metin(g, "surum"), NumberStyles.Integer, Inv, out s) ? s : mevcut.Surum;
                t.TamamlanmaTarihi = mevcut.TamamlanmaTarihi;
                string istenen = Metin(g, "statu");
                t.Statu = istenen == TalepKurallari.TalepIptal ? TalepKurallari.TalepIptal : (mevcut.Statu == TalepKurallari.TalepIptal ? TalepKurallari.TalepAcik : mevcut.Statu);
                if (istenen != TalepKurallari.TalepIptal && mevcut.Statu == TalepKurallari.TalepIptal && istenen.Length > 0) t.Statu = TalepKurallari.TalepAcik;
            }
            else
            {
                Gerekli(kul, Izin.TalepEkle);
                t.Statu = TalepKurallari.TalepAcik;
            }

            List<TalepKalip> satirlar = new List<TalepKalip>();
            foreach (object o in GListe(g, "satirlar"))
            {
                Dictionary<string, object> d = GSoz(o);
                TalepKalip s = new TalepKalip();
                int sid = 0;
                int.TryParse(Metin(d, "SatirId"), NumberStyles.Integer, Inv, out sid);
                s.Id = sid;
                s.KalipKodu = Metin(d, "KalipKodu");
                s.Aciklama = Metin(d, "Aciklama");
                s.KalipStatu = Metin(d, "KalipStatu");
                s.Lokasyon = Metin(d, "Lokasyon");
                s.Raf = Metin(d, "Raf");
                s.SahipBolum = Metin(d, "SahipBolum");
                if (s.Id > 0 && eskiSatirlar.ContainsKey(s.Id))
                {
                    // statü / lokasyon / raf yalnızca akış düğmeleriyle (gönder, teslim al, rafa kaldır) değişir
                    DataRow er = eskiSatirlar[s.Id];
                    s.KalipStatu = Convert.ToString(er["KalipStatu"]);
                    s.Lokasyon = er["Lokasyon"] == DBNull.Value ? "" : Convert.ToString(er["Lokasyon"]);
                    s.Raf = "";
                    s.SahipBolum = "";   // sahip bölüm talep ekranından değişmez
                    s.KalipKodu = Convert.ToString(er["KalipKodu"]);
                }
                else
                {
                    if (gonderilmis) throw new InvalidOperationException("Gönderilmiş talebe kalıp eklenemez.");
                    s.Id = 0;
                    s.KalipStatu = TalepKurallari.Planlandi; s.Lokasyon = ""; s.Raf = "";
                }
                satirlar.Add(s);
            }
            string kullanici = kul.KullaniciAdi;
            if (id > 0)
            {
                if (gonderilmis)
                {
                    HashSet<int> gelen = new HashSet<int>(satirlar.Select(x => x.Id));
                    if (gelen.Count != eskiSatirlar.Count || eskiSatirlar.Keys.Any(x => !gelen.Contains(x)))
                        throw new InvalidOperationException("Gönderilmiş talepten kalıp çıkarılamaz. Önce gönderimi iptal edin.");
                    if (t.Statu == TalepKurallari.TalepIptal) throw new InvalidOperationException("Gönderilmiş talep iptal edilemez. Önce gönderimi iptal edin.");
                }
                if (!TalepRepository.Guncelle(t, satirlar, kullanici))
                    throw new InvalidOperationException("Talep siz açtıktan sonra başka biri tarafından değiştirilmiş ya da silinmiş. Talebi yeniden açıp tekrar deneyin.");
                TalepRepository.AliciYaz(id, alici);
                Json(ctx, 200, new { mesaj = t.TalepNo + " güncellendi.", id });
            }
            else
            {
                int yeni = TalepRepository.Ekle(t, satirlar, kullanici);
                TalepRepository.AliciYaz(yeni, alici);
                DataTable dt = TalepRepository.Satir(yeni);
                string no = dt.Rows.Count > 0 ? Convert.ToString(dt.Rows[0]["TalepNo"]) : "";
                Json(ctx, 200, new { mesaj = no + " numaralı talep açıldı.", id = yeni });
            }
        }

        // ------------------------------------------------------------ Kullanıcılar

        private static Dictionary<string, object> KullaniciSoz(Kullanici h)
        {
            Dictionary<string, object> d = new Dictionary<string, object>();
            d["id"] = h.Id; d["ad"] = h.KullaniciAdi; d["admin"] = h.IsAdmin; d["aktif"] = h.Aktif; d["bolum"] = h.Bolum;
            d["bolumler"] = h.Bolumler.ToList(); d["tum"] = h.TumBolumler;
            d["izinler"] = h.Izinler.ToList();
            foreach (DataRow ir in BildirimRepository.Iletisimler().Select("Id = " + h.Id.ToString(Inv)))
            {
                d["email"] = ir["Email"] == DBNull.Value ? "" : Convert.ToString(ir["Email"]);
                d["telegram"] = ir["TelegramId"] == DBNull.Value ? "" : Convert.ToString(ir["TelegramId"]);
                d["bildirim"] = ir["BildirimAl"] != DBNull.Value && Convert.ToInt32(ir["BildirimAl"]) != 0;
            }
            return d;
        }

        private static void KullaniciKaydet(Kullanici kul, Baglanti ctx)
        {
            YoneticiGerekli(kul);
            Dictionary<string, object> g = Govde(ctx);
            int id = 0;
            int.TryParse(Metin(g, "id"), NumberStyles.Integer, Inv, out id);
            string ad = Metin(g, "ad");
            if (ad.Length == 0) throw new InvalidOperationException("Kullanıcı adı boş olamaz.");
            string parola = Convert.ToString(g.ContainsKey("parola") ? g["parola"] : "") ?? "";
            if (id == 0 && parola.Length == 0) throw new InvalidOperationException("Yeni kullanıcı için parola girin.");

            Kullanici h = new Kullanici();
            h.Id = id; h.KullaniciAdi = ad; h.IsAdmin = GBool(g, "admin"); h.Aktif = GBool(g, "aktif"); h.Bolum = Metin(g, "bolum");
            h.BolumlerAyarli = true;
            h.TumBolumler = GBool(g, "tum");
            foreach (object o in GListe(g, "bolumler"))
            {
                string b = Convert.ToString(o).Trim().ToUpper(TrK);
                if (b.Length > 0 && !h.Bolumler.Contains(b)) h.Bolumler.Add(b);
            }
            h.Bolum = h.Bolumler.Count > 0 ? h.Bolumler[0] : "";
            HashSet<string> gecerli = new HashSet<string>(Izin.Tanimlar.Select(t => t.Anahtar), StringComparer.OrdinalIgnoreCase);
            foreach (object o in GListe(g, "izinler"))
            {
                string a = Convert.ToString(o);
                if (gecerli.Contains(a)) h.Izinler.Add(a);
            }
            if (id > 0)
            {
                Kullanici eski = KullaniciRepository.Getir(id);
                if (eski == null) throw new InvalidOperationException("Kullanıcı bulunamadı.");
                if (eski.IsAdmin && eski.Aktif && (!h.IsAdmin || !h.Aktif) && KullaniciRepository.AktifYoneticiSayisi() <= 1)
                    throw new InvalidOperationException("Son aktif yöneticinin yöneticiliği ya da aktifliği kaldırılamaz.");
            }
            try
            {
                int yeni = KullaniciRepository.Kaydet(h, parola);
                BildirimRepository.IletisimYaz(yeni, Metin(g, "email"), Metin(g, "telegram"), GBool(g, "bildirim"));
                Json(ctx, 200, new { mesaj = ad + " kaydedildi.", id = yeni });
            }
            catch (System.Data.SQLite.SQLiteException)
            {
                throw new InvalidOperationException("'" + ad + "' kullanıcı adı zaten var.");
            }
        }
    }
}
