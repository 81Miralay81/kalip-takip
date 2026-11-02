# Kalıp Takip Sistemi (C# + Guna.UI2 + SQLite)

## Ne değişti
- Arayüz: C# / WinForms / Guna.UI2 (yuvarlak köşeler, gölge, sürüklerken yarı saydam, renkli düğmeler, bildirim balonları).
- Veritabanı: **SQLite tek dosya** (`KalipTakip.db`). Sunucu kurulumu YOK, Access YOK, taşınabilir.
- Eski Access (.accdb) verileri ilk açılışta kurulum ekranından **tek seferlik** aktarılır (kullanıcılar, parolalar, yetkiler, listeler, ~81 bin kayıt; Id'ler korunur, sayılar tutmazsa hiçbir şey kaydedilmez).
- `System.Data.OleDb.dll` (.NET 8) kullanılmaz: .NET Framework 4.8'de OleDb zaten yerleşik.

## Derleme (Visual Studio 2022)
1. `KalipTakip.sln` dosyasını açın. .NET Framework 4.8 Developer Pack kurulu olmalı.
2. İlk derlemede NuGet `System.Data.SQLite.Core` otomatik inecek (internet gerekir).
3. `Libs\Guna.UI2.dll` projeye bağlı. Lisanssız sürümde Guna filigranı çıkabilir.
4. Derle → `KalipTakip.exe` oluşur.

## Kurulum / ağ paylaşımı
- `App.config` içinde `VeritabaniYolu` değerine paylaşımdaki dosya yolunu yazın, örn. `\\SUNUCU\Kalip\KalipTakip.db`. Boşsa exe klasörü kullanılır.
- Paylaşım klasöründe herkesin **değiştirme** yetkisi olmalı. Herkes exe'ye kısayol alır (exe de paylaşımda durabilir).
- İlk açılışta kurulum ekranı gelir: eski `.accdb` dosyasını seçin → Başlat. Bu adım için bilgisayarda "Microsoft Access Database Engine (ACE)" gerekir (program ile aynı bit sayısı); yalnızca bu tek seferlik aktarım içindir.

## Güvenlik ve yedek
- Her gün ilk açılışta `Yedek\KalipTakip_yyyyMMdd.db` kopyası alınır, son 14 saklanır.
- Aynı kaydı iki kişi düzenlerse son kaydeden uyarılır (sürüm kontrolü), veri ezilmez.
- Dürüst not: SQLite ağ paylaşımında 3 kişilik kullanım için uygundur ama dosya kilidi SMB'ye bağlıdır. Bekleme süresi 15 sn'dir; "kilitli" uyarısı çıkarsa birkaç sn sonra tekrar deneyin. Yedekleri düzenli kontrol edin.

## Kısayollar
Ctrl+S kaydet/güncelle, Ctrl+N boş form, Ctrl+F ara, F5 yenile. Sayı kutularında Enter hesaplar (575+525) ve aynı kutuda kalır.
