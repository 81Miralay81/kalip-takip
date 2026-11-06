# Kalıp Takip — Yol Haritası ve Değişiklik Kaydı

Bu dosya projenin **başından bugüne** adım adım kaydıdır. Her ekleme/güncellemede ilgili adım buraya eklenir.
Geriye dönük bir düzeltme gerektiğinde önce buraya bakılır: hangi adımda ne değişti, hangi sürümde, hangi commit'te.

- Depo: `81Miralay81/kalip-takip` (özel — herkese kapalı). **Veritabanı (.db) GitHub'a konmaz.**
- Veritabanı: `\\KALIPNAS2019\kaliphanenas\OMUR\takip\KalipTakip.db` (günlük otomatik yedek: `Yedek\KalipTakip_yyyyMMdd.db`, son 14).
- Derleme: GitHub Actions ("Derle") → "KalipTakip" paketi (Sunucu + masaüstü exe). Her sürümün **commit kodu** aşağıdaki tabloda yazılıdır (git etiketi bu ortamdan gönderilemedi; commit kodu aynı işi görür).
- Sürüm damgası tarayıcıda üst köşede görünür (örn. `03.11.2026-Z14`).

## Çalışma kuralları (kararlaştırılan)
1. Eskiyi bozmadan üzerine ekle.
2. Önce tarayıcı portalı; masaüstü exe geride kalabilir (aynı veritabanını kullanır).
3. Kritik işlemler (silme, toplu yükleme) ayrı yetkidir; yönetici her girişte kritik yetki taşıyanları görür.
4. Her değişiklikte: bu dosya + `OKU_BENI.txt` güncellenir, commit atılır, commit kodu tabloya yazılır.
5. Derleme kırmızıysa önce Actions günlüğü okunup düzeltilir; "tam paket" kurulur (dosya dosya değil).

## Geri dönüş (bir şey bozulursa)
- Hangi sürümde bozulduğunu aşağıdaki tablodan bulun → commit kodunu not alın.
- Sadece o değişikliği geri almak: `git revert <commit>`; komple eski sürüme bakmak: `git checkout <commit>`. Sürüm sürüm hazır paketler GitHub Actions çalışma geçmişinde de durur (yeşil olanlar).
- Veriler (.db) bu işlemlerden etkilenmez; veri bozulduysa `Yedek\` klasöründeki günlük yedeğe dönülür.
- Şema (tablo) değişiklikleri `Sema.vb` içinde korumalı ve tek seferliktir (bayraklı); geri alınan sürümde eklenen yeni tablo/sütunlar zararsız kalır.

---

## FAZ 0 — Başlangıç (GitHub öncesi; hafıza notlarından, tarihler yaklaşık)
| # | Adım | Durum |
|---|------|-------|
| 0.1 | Excel/VBA çalışma kitabı `kalıp_takip.xlsm` incelendi, kullanıcı formları ve kodu gözden geçirildi | ✅ |
| 0.2 | Karar: Visual Studio 2022 ile bağımsız exe; en çok 3 eşzamanlı kullanıcı; ağ paylaşımında | ✅ |
| 0.3 | C# + Guna.UI2 arayüz; Access yerine taşınabilir **SQLite** tek dosya; eski .accdb'den tek seferlik veri aktarımı (~81 bin kayıt) | ✅ |
| 0.4 | Portal ekranı (girişten sonra), kullanıcı yönetimi portala taşındı, yetkiler portal bazlı | ✅ |
| 0.5 | **Talep** modülü: bir talepte çok kalıp, kalıp başına not/statü/lokasyon; mükerrer talep engeli | ✅ |
| 0.6 | Bölüm (departman) modeli: kullanıcıya bölüm(ler), kalıba sahip bölüm, lokasyonlar bölüme bağlı | ✅ |
| 0.7 | **Kalıp Listesi** portalı: kalıp başına tek satır, geçmiş, tashih, raf, Excel yükleme | ✅ |
| 0.8 | Tarayıcı/mobil portal (`http://sunucu:8080`), tam özellik; kamera ile kod okuma (Google Gemini yapay zeka; OCR kaldırıldı), barkod | ✅ |
| 0.9 | Raf yönetimi + kamerayla raf okutma; DK koduna göre işlem adı (-1 Dövme, -2 Makas, -3 Baskı…) | ✅ |
| 0.10 | Talep iş akışı: Planlandı → Gönder (Sevkediliyor) → Kalıphane teslim alır (lokasyon zorunlu) → eksikler geri bildirilir → İade/yeniden gönder; "Rafa kaldır" yetkisi; "İşleniyor" statüsü | ✅ |
| 0.11 | Bildirim kanalları (Telegram, e-posta, portal rozeti); Telegram şirket ağında engelli → sunucuda denenecek | ✅ kod / ⏳ canlı test |
| 0.12 | Cihaz bazlı erişim kontrolü (onaylı cihaz, cihaz adı, 3 yerde saklanan kod) | ✅ |
| 0.13 | Günlük otomatik veritabanı yedeği; kod özel GitHub deposunda | ✅ |

## FAZ 1 — GitHub'da kayıtlı sürümler
| # | Tarih | Sürüm | Commit | Yapılan | Durum |
|---|-------|-------|--------|---------|-------|
| 1 | 02.11 | O | `fa79518` | İlk yükleme (tüm proje GitHub'a) | ✅ |
| 2 | 02.11 | P | `6c970ca` | Sunucu veritabanı yolu `…\takip\KalipTakip.db` olarak sabitlendi | ✅ |
| 3 | 02.11 | — | `845c095` | Derleme paketi: Sunucu + masaüstü exe aynı veritabanı yolunu kullanır | ✅ |
| 4 | 02.11 | R | `f2c6a55` | Kalıp ve kayıt **türleri** yönetim sayfası (ekle/düzenle/kaldır) | ✅ |
| 5 | 02.11 | S | `b4b66a9` | Talep: çoklu seçim (Ctrl/Shift/sürükle), sağ tık menüsü, talebi/seçili kalıpları rafa al | ✅ |
| 6 | 02.11 | T | `898128b` | Kalıp türleri **sahip bölüme** bağlı | ✅ |
| 7 | 02.11 | U | `d0dfced` | Kalıp Listesi: **kodu düzelt**, sil, kaydı olmayanları toplu sil | ✅ |
| 8 | 02.11 | V | `5b80e78` | Kalıp Listesi: başlıklara göre arama; türlerden "kalıpları göster" | ✅ |
| 9 | 02.11 | W | `6d3f9ac` | Başlık aramasında açılır liste; yavaş kalıp türü sorgusu düzeltildi (>100 sn → 1,5 sn) | ✅ |
| 10 | 02.11 | X | `6a5e0c8` | Türler: kayıtlarda geçen değeri temizleyip kaldırma ("yalnız kayıtlarda" değerleri) | ✅ |
| 11 | 02.11 | Y | `83deb53` | Türler: türü **toplu sahip bölüme bağla** (sütun + Kaydet) | ✅ |
| 12 | 03.11 | Z | `24640ee` | **CSV yükleme kök neden düzeltmesi** (RFC4180: `1"" DÖVME` bozulması), geçersiz lokasyon yazılmaz, "Verileri onar", bölümlü kalıp gösterimi, tam ekran düzen, **sürüklenebilir sütun genişliği** (saklanır) | ✅ |
| 13 | 03.11 | Z2 | `94439d2` | Verileri onar: türü listede olmayanlara kod sonundaki numaraya göre tür atar | ✅ |
| 14 | 03.11 | Z3 | `64b2fa7` | Uygulamaya özel **sağ tık menüsü** + **klavye kısayolları** (Ctrl+S, Alt+N/U/E/K, /, Alt+R/P, F1) | ✅ |
| 15 | 03.11 | Z4 | `f2e5dcc` | Menü/kısayollar **yetkiye bağlı**, uygun olmayan işlemler pasif, sayfa yetki kontrolü | ✅ |
| 16 | 03.11 | Z5–Z6 | `c763f7a` | **Ayrı yetkiler** kl_duzenle / kl_yukle / kl_sil (⚠ KRİTİK); **girişte kritik yetki uyarısı** (Kalsın / Geri al); telefonda **uzun basma menüsü + seçim modu**; **cihaz kontrolü varsayılan AÇIK** | ✅ |
| 17 | 03.11 | Z7 | `17f9bba` | Kullanıcı formunda açılır gruplar (Bölümler, Bildirim, her yetki grubu; seçili/toplam sayacı) | ✅ |
| 18 | 03.11 | Z8 | `8299331` | Kritik uyarıda "Geri al" → açık kullanıcı formundaki kutu anında kalkar | ✅ |
| 19 | 03.11 | Z9–Z10 | `8f59a16` | Tüm tablolarda **sütun ayırıcı çizgiler**; **Kalıp Takip Excel/CSV yükleme** (`kt_yukle`, ⚠ KRİTİK): ID ile eşleşir, önizleme + onay, silme yok, parça kodu değişmez, Hafta/Toplam Zaman otomatik | ✅ (⚠ bu commit derleme hatalıydı → #23) |
| 20 | 03.11 | Z11 | `18d9244` | İstatistik: yığılmış **sütun grafik + pasta (halka) grafik** | ✅ |
| 21 | 03.11 | Z12 | `b76eb03` | İstatistik: tablo satırı seçilince grafikler o kalıp türüne göre değişir; tekrar tıklama / "Seçimi kaldır" | ✅ |
| 22 | 03.11 | Z13 | `5a42013` | İstatistik: **hafta/yıl araması** (yazdıkça 42/2026 önerisi, `/api/kt/haftalar`) | ✅ |
| 23 | 03.11 | — | `ae80270` | **Derleme hatası düzeltildi** (VB'de yerel `kolonlar` ile `Kolonlar` çakışması, BC30311). Z10–Z13 arası 4 derleme bu yüzden kırmızıydı; ilk yeşil derleme bu | ✅ |
| 24 | 03.11 | Z14 | `38ae1af` | **"İşleniyor" göstergesi** (üst çubuk + yazı + düğmede dönen simge, çift tıklama kilidi); Kalıp Takip aramasında `(boş)` ile boş alanlar; İstatistik düzeni (Bölüm/Getir alt satıra) | ✅ derleme başarılı |
| 25 | 05.11 | — | `7545330` | Yol haritası (bu dosya) ve `CLAUDE.md` çalışma kuralı | ✅ |
| 26 | 05.11 | Z15 | `93f02ad` | **Kanatix işareti**: masaüstü pencerelerde (TabanForm) ve tarayıcı portalında sağ alt köşede küçük, silik işaret (16–18 px, ~%30 saydam, tıklamayı engellemez). Neden: kullanıcı yazılımları için ortak marka Kanatix'i seçti; şirket yazılımı olduğu için yalnız fark edilmeyecek kadar küçük işaret. Uygulama adı/simgesi bilerek DEĞİŞMEDİ | ✅ derleme başarılı |
| 27 | 06.11 | Z16 | `915b67b` | **Aşama 1 — Gizli ayarlar + Bağlantılar**: (1) şifreli ayar deposu `gizli.ayar` (AES-256 + HMAC, anahtar Windows DPAPI ile korunur, isteğe bağlı kurtarma parolası); Gizli Ayarlar sayfası (maskeli değerler, göstermek için yönetici parolası, config'ten taşı); (2) Bağlantılar sayfası: SQL Server bağlantısı + **SQLite→SQL Server aktarımı** (tam veri ya da boş şema + yönetici); (3) **IFS kaynakları** (Oracle/SQL Server, yalnız SELECT, çoklu kaynak, otomatik çekme aralığı, satır özetiyle değişen/yeni/aynı ayrımı, günlük) + üst çubukta "⟳ Veri çek"; (4) boş veritabanı kurulumunda yönetici adı/parolası artık sorulur; (5) CI: LocalDB ile kendi kendine sınama (`--sinama`). Uygulama hâlâ SQLite ile çalışır | ✅ derleme + SQL Server sınaması başarılı |
| 28 | 06.11 | Z17 | `COMMIT` | **IFS'e yazma (gönderme)**: Bağlantılar'da kaynak başına yazılacak IFS tablosu, anahtar sütun ve sütun eşleştirme. Kalıp başına tek satır; Kalıp Listesi/Talep değişince (≈30 sn) satır güncellenir, yoksa eklenir. Güvenlik: yazmayı açarken yönetici parolası; otomatik gönderim ancak "Temel al" (göndermeden) ya da "Tümünü gönder" yapıldıktan sonra; yalnız değişenler gider; hatalı satır sonraki turda yeniden denenir. Okuma sorgusu boş bırakılabilir (yalnız yazma). LocalDB sınamasına eklendi | ⏳ derleme bekleniyor |

> Ayrıntılı açıklamalar her sürüm için `OKU_BENI.txt` içindedir.

## FAZ 2 — Yapılacaklar / Açık konular (öncelik sırasıyla öneri)
| # | Konu | Not | Durum |
|---|------|-----|-------|
| A1 | **Kalıp Takip yüklemesini gerçek veriyle dene** | Önce küçük dosya (5–10 satır), sonra 91 bin satır; yedek alın | ⏳ |
| A2 | Cihaz kontrolü açıkken yeni telefon/PC girişlerini onaylama düzeni | Yeni cihaz için önce Cihaz Erişimleri'nden onay | ⏳ gözlem |
| A3 | Türler sayfasına "Kalıp sayısı" sütunu | Daha önce önerildi, beklemede | ⏳ |
| A4 | İstatistikten ilgili kayıtlara geçiş (örn. `(boş)` satırına tıklayıp kayıtları listele) | `(boş)` araması hazır, kısayol yok | ⏳ öneri |
| A5 | Telegram/e-posta bildirimlerinin sunucuda canlı testi | Şirket ağı Telegram'ı engelliyor; WhatsApp sadece fikir | ⏳ |
| A6 | IFS tablosunun gerçek adı/sütunları ve yazma yetkili kullanıcı | Z17 altyapı hazır; IFS tarafından tablo yapısı alınıp Bağlantılar'da eşleştirilecek | ⏳ |
| A11 | **Aşama 2: uygulamanın tamamı SQL Server'da çalışsın** | ~290 SQLite cümlesi için lehçe katmanı; LocalDB sınamasıyla adım adım | ⏳ |
| A12 | App.config'teki Gemini anahtarını yenile ve Gizli Ayarlar'a taşı | Özel depoda düz yazılı kalmış | ⏳ acil |
| A7 | Genel internete açma (onaylı cihaz tekrar sorulmasın) | Güvenlik gözden geçirilecek | ⏳ |
| A8 | Masaüstü exe'de Talep ekranı yeni iş akışına uyarlanmadı | Karar: tarayıcı öncelikli | 💤 |
| A9 | Yönetici toplu veri düzenleme aracı (Excel'e gerek kalmadan) | Fikir | 💤 |
| A10 | Kalan: türü kod sonundan tahmin edilemeyen kalıplar (Verileri onar dokunmaz) | Excel ile düzeltilir | ⏳ |

Durum: ✅ tamam · ⏳ bekliyor · 💤 ertelendi
