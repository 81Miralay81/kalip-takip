Imports System.Data.SQLite
Imports System.IO

''' <summary>SQLite dosyasını ve tabloları kurar.</summary>
Public NotInheritable Class Sema
    Private Sub New()
    End Sub

    ''' <summary>Dosya yoksa oluşturur. Dönüş: True = bu çağrı oluşturdu, False = dosya zaten vardı.</summary>
    Public Shared Function VeritabaniOlustur() As Boolean
        Dim yol As String = Db.Yol
        Dim dizin As String = Path.GetDirectoryName(yol)
        Try
            If Not String.IsNullOrEmpty(dizin) AndAlso Not Directory.Exists(dizin) Then Directory.CreateDirectory(dizin)
            If File.Exists(yol) Then Return False
            SQLiteConnection.CreateFile(yol)
            Return True
        Catch ex As Exception
            Throw New InvalidOperationException(
                "Veritabanı dosyası oluşturulamadı:" & vbCrLf & yol & vbCrLf & vbCrLf &
                "Klasörün var olduğunu ve herkesin 'değiştirme' yetkisi bulunduğunu kontrol edin." & vbCrLf & ex.Message, ex)
        End Try
    End Function

    ' Eski veri aktarmadan, boş bir veritabanı kurar ve yönetici kullanıcıyı oluşturur (verilmezse ADMIN / 1234).
    Public Shared Sub BosKur(Optional yoneticiAdi As String = "ADMIN", Optional yoneticiParola As String = "1234")
        Try
            VeritabaniOlustur()
            TablolariKur()
            Yukselt()
            Dim k As New Kullanici()
            k.KullaniciAdi = If(String.IsNullOrWhiteSpace(yoneticiAdi), "ADMIN", yoneticiAdi.Trim())
            k.IsAdmin = True
            k.Aktif = True
            KullaniciRepository.Kaydet(k, If(String.IsNullOrEmpty(yoneticiParola), "1234", yoneticiParola))
        Catch
            TablolariSil()
            Throw
        End Try
    End Sub

    Public Shared Sub TablolariKur()
        Dim komutlar As String() = {
            "CREATE TABLE Kalip (" &
            "Id INTEGER PRIMARY KEY AUTOINCREMENT NOT NULL, " &
            "ParcaKodu TEXT, KalipTuru TEXT, TepeKodu TEXT, ParcaAdi TEXT, " &
            "GelisTarihi DATE, TashihSayi INTEGER, BitisTarihi DATE, Hafta TEXT, Tur TEXT, " &
            "DovmeAdeti REAL, CncNo INTEGER, Aciklama TEXT, " &
            "IslemSuresi REAL, Ayar REAL, Bakim REAL, ElemanEksikligi REAL, Elektrik REAL, " &
            "KalipAriza REAL, Takim REAL, Ariza REAL, ToplamZaman REAL, " &
            "Surum INTEGER NOT NULL DEFAULT 1, SonDegisiklik DATETIME, SonKullanici TEXT)",
            "CREATE INDEX ix_Kalip_ParcaKodu ON Kalip (ParcaKodu)",
            "CREATE INDEX ix_Kalip_TepeKodu ON Kalip (TepeKodu)",
            "CREATE INDEX ix_Kalip_BitisTarihi ON Kalip (BitisTarihi)",
            "CREATE INDEX ix_Kalip_CncNo ON Kalip (CncNo)",
            "CREATE INDEX ix_Kalip_KalipTuru ON Kalip (KalipTuru)",
            "CREATE TABLE Kullanici (" &
            "Id INTEGER PRIMARY KEY AUTOINCREMENT NOT NULL, KullaniciAdi TEXT NOT NULL, ParolaHash TEXT NOT NULL, " &
            "IsAdmin BOOLEAN NOT NULL DEFAULT 0, Aktif BOOLEAN NOT NULL DEFAULT 1)",
            "CREATE UNIQUE INDEX ux_Kullanici_Ad ON Kullanici (KullaniciAdi)",
            "CREATE TABLE Yetki (" &
            "KullaniciId INTEGER NOT NULL, Anahtar TEXT NOT NULL, PRIMARY KEY (KullaniciId, Anahtar))",
            "CREATE TABLE Liste (" &
            "Id INTEGER PRIMARY KEY AUTOINCREMENT NOT NULL, Grup TEXT NOT NULL, Deger TEXT NOT NULL, Sira INTEGER)"
        }
        Using c As SQLiteConnection = Db.Ac()
            Using tx As SQLiteTransaction = c.BeginTransaction()
                For Each sql As String In komutlar
                    Using cmd As New SQLiteCommand(sql, c, tx)
                        cmd.ExecuteNonQuery()
                    End Using
                Next
                tx.Commit()
            End Using
        End Using
    End Sub

    ''' <summary>
    ''' Var olan veritabanını yeni sürüme yükseltir (idempotent): Talep tablosu yoksa kurar ve mevcut kullanıcılara
    ''' 'kalip_gor' yetkisini verir (Kalıp Takip portalı yetkiye bağlandığı için eski kullanıcılar erişimini kaybetmesin).
    ''' Dönüş: True = bu çağrı yükseltme yaptı.
    ''' </summary>
    Public Shared Function Yukselt() As Boolean
        Using c As SQLiteConnection = Db.Ac()
            Dim degisti As Boolean = False
            If Not TabloVar(c, "Talep") Then
                Dim komutlar As String() = {
                    "CREATE TABLE Talep (" &
                    "Id INTEGER PRIMARY KEY AUTOINCREMENT NOT NULL, TalepNo TEXT, KalipKodu TEXT NOT NULL, Bolum TEXT, Lokasyon TEXT, " &
                    "Oncelik TEXT, Statu TEXT, TalepTarihi DATE, TerminTarihi DATE, TamamlanmaTarihi DATE, TalepEden TEXT, " &
                    "Aciklama TEXT, Sonuc TEXT, Surum INTEGER NOT NULL DEFAULT 1, SonDegisiklik DATETIME, SonKullanici TEXT)",
                    "CREATE INDEX ix_Talep_TalepNo ON Talep (TalepNo)",
                    "CREATE INDEX ix_Talep_KalipKodu ON Talep (KalipKodu)",
                    "CREATE INDEX ix_Talep_Statu ON Talep (Statu)",
                    "CREATE INDEX ix_Talep_TalepTarihi ON Talep (TalepTarihi)",
                    "INSERT OR IGNORE INTO Yetki (KullaniciId, Anahtar) SELECT Id, 'kalip_gor' FROM Kullanici"
                }
                Calistir(c, komutlar)
                degisti = True
            End If

            ' Sürüm 2: bir talepte birden çok kalıp, kalıp bazlı statü / lokasyon / hareket geçmişi.
            If Not TabloVar(c, "TalepKalip") Then
                Dim komutlar As String() = {
                    "CREATE TABLE TalepKalip (" &
                    "Id INTEGER PRIMARY KEY AUTOINCREMENT NOT NULL, TalepId INTEGER NOT NULL, KalipKodu TEXT NOT NULL, Anahtar TEXT NOT NULL, " &
                    "Aciklama TEXT, KalipStatu TEXT, Lokasyon TEXT, TalepSira INTEGER NOT NULL DEFAULT 1, TashihSayisi INTEGER NOT NULL DEFAULT 0, " &
                    "SonDegisiklik DATETIME, SonKullanici TEXT)",
                    "CREATE INDEX ix_TalepKalip_TalepId ON TalepKalip (TalepId)",
                    "CREATE INDEX ix_TalepKalip_Anahtar ON TalepKalip (Anahtar)",
                    "CREATE TABLE KalipLokasyon (" &
                    "Anahtar TEXT PRIMARY KEY NOT NULL, KalipKodu TEXT, Lokasyon TEXT, SahipBolum TEXT, Raf TEXT, SonDegisiklik DATETIME, SonKullanici TEXT)",
                    "CREATE TABLE KalipHareket (" &
                    "Id INTEGER PRIMARY KEY AUTOINCREMENT NOT NULL, Anahtar TEXT NOT NULL, KalipKodu TEXT, TalepId INTEGER, TalepNo TEXT, " &
                    "EskiStatu TEXT, YeniStatu TEXT, EskiLokasyon TEXT, YeniLokasyon TEXT, Tarih DATETIME, Kullanici TEXT)",
                    "CREATE INDEX ix_KalipHareket_Anahtar ON KalipHareket (Anahtar)",
                    "UPDATE Talep SET Statu = 'Açık' WHERE Statu IS NULL OR Statu NOT IN ('Açık','Kısmi Teslim Alındı','Kapandı','İptal')",
                    "INSERT INTO TalepKalip (TalepId, KalipKodu, Anahtar, Aciklama, KalipStatu, Lokasyon, TalepSira, TashihSayisi, SonDegisiklik, SonKullanici) " &
                    "SELECT t.Id, t.KalipKodu, KATLA(t.KalipKodu), t.Aciklama, " &
                    "CASE t.Statu WHEN 'Kapandı' THEN 'Teslim Alındı' ELSE 'Planlandı' END, t.Lokasyon, " &
                    "(SELECT COUNT(*) FROM Talep x WHERE KATLA(x.KalipKodu) = KATLA(t.KalipKodu) AND x.Id <= t.Id), 0, t.SonDegisiklik, t.SonKullanici " &
                    "FROM Talep t WHERE t.KalipKodu <> '-'"
                }
                Calistir(c, komutlar)
                degisti = True
            End If

            ' Sürüm 3: bölüm / lokasyon yapısı, kullanıcıya bölüm, kalıba sahip bölüm.
            If Not SutunVar(c, "Kullanici", "Bolum") Then
                Calistir(c, New String() {"ALTER TABLE Kullanici ADD COLUMN Bolum TEXT"})
                degisti = True
            End If
            If Not SutunVar(c, "KalipLokasyon", "SahipBolum") Then
                Calistir(c, New String() {"ALTER TABLE KalipLokasyon ADD COLUMN SahipBolum TEXT"})
                degisti = True
            End If
            If Not TabloVar(c, "Lokasyon") Then
                Calistir(c, New String() {
                    "CREATE TABLE Lokasyon (Ad TEXT PRIMARY KEY NOT NULL, Bolum TEXT NOT NULL, AnaMi INTEGER NOT NULL DEFAULT 0)",
                    "INSERT OR IGNORE INTO Lokasyon (Ad, Bolum, AnaMi) VALUES ('KALIPHANE','KALIPHANE',1)",
                    "INSERT OR IGNORE INTO Lokasyon (Ad, Bolum, AnaMi) SELECT DISTINCT UPPER(TRIM(Bolum)), UPPER(TRIM(Bolum)), 1 FROM Talep WHERE Bolum IS NOT NULL AND TRIM(Bolum) <> ''",
                    "INSERT OR IGNORE INTO Lokasyon (Ad, Bolum, AnaMi) SELECT DISTINCT UPPER(TRIM(Deger)), UPPER(TRIM(Deger)), 1 FROM Liste WHERE Grup = 'Bolum' AND Deger IS NOT NULL AND TRIM(Deger) <> ''"
                })
                degisti = True
            End If
            ' Sürüm 4: kalıp listesi (elle tanım / Excel yükleme) ve portal yetkisi
            If Not TabloVar(c, "KalipTanim") Then
                Calistir(c, New String() {
                    "CREATE TABLE KalipTanim (Anahtar TEXT PRIMARY KEY NOT NULL, KalipKodu TEXT NOT NULL, KalipAdi TEXT, KalipTuru TEXT, " &
                    "TepeKodu TEXT, Notlar TEXT, SonDegisiklik DATETIME, SonKullanici TEXT)",
                    "INSERT OR IGNORE INTO Yetki (KullaniciId, Anahtar) SELECT KullaniciId, 'kl_gor' FROM Yetki WHERE Anahtar = 'kalip_gor'"
                })
                degisti = True
            End If
            ' Sürüm 5: kalıp rafı
            If Not SutunVar(c, "Liste", "Bolum") Then
                Calistir(c, New String() {"ALTER TABLE Liste ADD COLUMN Bolum TEXT"})
                degisti = True
            End If
            If Not SutunVar(c, "KalipLokasyon", "Raf") Then
                Calistir(c, New String() {"ALTER TABLE KalipLokasyon ADD COLUMN Raf TEXT"})
                degisti = True
            End If
            ' Sürüm 6: raf tanımları (mevcut kalıplardaki raflar aktarılır)
            If Not TabloVar(c, "Raf") Then
                Calistir(c, New String() {
                    "CREATE TABLE Raf (Ad TEXT PRIMARY KEY NOT NULL)",
                    "INSERT OR IGNORE INTO Raf (Ad) SELECT DISTINCT Raf FROM KalipLokasyon WHERE Raf IS NOT NULL AND TRIM(Raf) <> ''"
                })
                degisti = True
            End If
            ' Sürüm 7: kullanıcıya birden fazla bölüm / tüm bölümler; talep satırında teslim rafı
            If Not TabloVar(c, "KullaniciBolum") Then
                Calistir(c, New String() {
                    "CREATE TABLE KullaniciBolum (KullaniciId INTEGER NOT NULL, Bolum TEXT NOT NULL, PRIMARY KEY (KullaniciId, Bolum))",
                    "INSERT OR IGNORE INTO KullaniciBolum (KullaniciId, Bolum) SELECT Id, UPPER(TRIM(Bolum)) FROM Kullanici WHERE Bolum IS NOT NULL AND TRIM(Bolum) <> ''"
                })
                degisti = True
            End If
            ' Mevcut kullanıcıların davranışı değişmesin diye varsayılan 1 (tüm bölümleri görür); yönetici sonradan kısıtlar.
            If Not SutunVar(c, "Kullanici", "TumBolum") Then
                Calistir(c, New String() {"ALTER TABLE Kullanici ADD COLUMN TumBolum INTEGER NOT NULL DEFAULT 1"})
                degisti = True
            End If
            If Not SutunVar(c, "TalepKalip", "Raf") Then
                Calistir(c, New String() {"ALTER TABLE TalepKalip ADD COLUMN Raf TEXT"})
                degisti = True
            End If
            ' Sürüm 8: talep akışı (gönder / teslim al / geri bildir), tamir adedi, bildirimler
            If Not SutunVar(c, "Talep", "AliciBolum") Then
                Calistir(c, New String() {
                    "ALTER TABLE Talep ADD COLUMN AliciBolum TEXT",
                    "ALTER TABLE Talep ADD COLUMN GonderTarihi DATETIME",
                    "ALTER TABLE Talep ADD COLUMN GeriDondu INTEGER NOT NULL DEFAULT 0",
                    "ALTER TABLE Talep ADD COLUMN GeriTarihi DATETIME"
                })
                degisti = True
            End If
            If Not SutunVar(c, "TalepKalip", "OncekiLokasyon") Then
                Calistir(c, New String() {
                    "ALTER TABLE TalepKalip ADD COLUMN OncekiLokasyon TEXT",
                    "ALTER TABLE TalepKalip ADD COLUMN OncekiRaf TEXT",
                    "ALTER TABLE TalepKalip ADD COLUMN TeslimTarihi DATETIME"
                })
                degisti = True
            End If
            If Not SutunVar(c, "KalipLokasyon", "TamirAdedi") Then
                Calistir(c, New String() {"ALTER TABLE KalipLokasyon ADD COLUMN TamirAdedi INTEGER NOT NULL DEFAULT 0"})
                degisti = True
            End If
            If Not SutunVar(c, "Kullanici", "BildirimAl") Then
                Calistir(c, New String() {
                    "ALTER TABLE Kullanici ADD COLUMN Email TEXT",
                    "ALTER TABLE Kullanici ADD COLUMN TelegramId TEXT",
                    "ALTER TABLE Kullanici ADD COLUMN BildirimAl INTEGER NOT NULL DEFAULT 0",
                    "INSERT OR IGNORE INTO Yetki (KullaniciId, Anahtar) SELECT KullaniciId, 'talep_gonder' FROM Yetki WHERE Anahtar = 'talep_guncelle'",
                    "INSERT OR IGNORE INTO Yetki (KullaniciId, Anahtar) SELECT KullaniciId, 'talep_teslim' FROM Yetki WHERE Anahtar = 'talep_kalip_durum'",
                    "INSERT OR IGNORE INTO Yetki (KullaniciId, Anahtar) SELECT KullaniciId, 'raf_kaldir' FROM Yetki WHERE Anahtar = 'talep_kalip_durum'"
                })
                degisti = True
            End If
            If Not TabloVar(c, "Ayar") Then
                Calistir(c, New String() {
                    "CREATE TABLE Ayar (Anahtar TEXT PRIMARY KEY NOT NULL, Deger TEXT)"
                })
                degisti = True
            End If
            If Not TabloVar(c, "BildirimLog") Then
                Calistir(c, New String() {
                    "CREATE TABLE BildirimLog (Id INTEGER PRIMARY KEY AUTOINCREMENT NOT NULL, Tarih DATETIME NOT NULL, Kanal TEXT, Alici TEXT, " &
                    "Mesaj TEXT, Durum TEXT, Hata TEXT, TalepId INTEGER)"
                })
                degisti = True
            End If
            If Not SutunVar(c, "Kullanici", "CihazMuaf") Then
                Calistir(c, New String() {"ALTER TABLE Kullanici ADD COLUMN CihazMuaf INTEGER NOT NULL DEFAULT 0"})
                degisti = True
            End If
            If Not TabloVar(c, "CihazIzin") Then
                Calistir(c, New String() {
                    "CREATE TABLE CihazIzin (Id INTEGER PRIMARY KEY AUTOINCREMENT NOT NULL, KullaniciId INTEGER NOT NULL, KodHash TEXT NOT NULL, " &
                    "Ad TEXT, Mac TEXT, Ip TEXT, Bilgi TEXT, Tur TEXT, Durum TEXT NOT NULL, IlkIstek DATETIME, SonGiris DATETIME, " &
                    "KararTarihi DATETIME, KararVeren TEXT, GirisSayisi INTEGER NOT NULL DEFAULT 0)",
                    "CREATE UNIQUE INDEX ux_Cihaz ON CihazIzin (KullaniciId, KodHash)",
                    "CREATE INDEX ix_Cihaz_Durum ON CihazIzin (Durum)"
                })
                degisti = True
            End If
            ' "İşleme Alındı" statüsünün adı "İşleniyor" oldu
            Dim eskiAd As Object
            Using cmdE As New SQLiteCommand("SELECT COUNT(*) FROM TalepKalip WHERE KalipStatu = 'İşleme Alındı'", c)
                eskiAd = cmdE.ExecuteScalar()
            End Using
            If Convert.ToInt32(eskiAd) > 0 Then
                Calistir(c, New String() {
                    "UPDATE TalepKalip SET KalipStatu = 'İşleniyor' WHERE KalipStatu = 'İşleme Alındı'",
                    "UPDATE KalipHareket SET EskiStatu = 'İşleniyor' WHERE EskiStatu = 'İşleme Alındı'",
                    "UPDATE KalipHareket SET YeniStatu = 'İşleniyor' WHERE YeniStatu = 'İşleme Alındı'"
                })
                degisti = True
            End If
            ' Sürüm 9: kalıp düzenleme ve toplu yükleme ayrı yetki oldu; kalıp ekleme yetkisi olanlara bir kez otomatik verilir (silme verilmez).
            If AyarRepository.Al("mig_kl_yetki", "") <> "1" Then
                Calistir(c, New String() {
                    "INSERT OR IGNORE INTO Yetki (KullaniciId, Anahtar) SELECT KullaniciId, 'kl_duzenle' FROM Yetki WHERE Anahtar = 'kl_ekle'",
                    "INSERT OR IGNORE INTO Yetki (KullaniciId, Anahtar) SELECT KullaniciId, 'kl_yukle' FROM Yetki WHERE Anahtar = 'kl_ekle'",
                    "INSERT OR REPLACE INTO Ayar (Anahtar, Deger) VALUES ('mig_kl_yetki', '1')"
                })
                degisti = True
            End If
            Return degisti
        End Using
    End Function

    Private Shared Function SutunVar(c As SQLiteConnection, tablo As String, sutun As String) As Boolean
        Using cmd As New SQLiteCommand("PRAGMA table_info(" & tablo & ")", c)
            Using r As SQLiteDataReader = cmd.ExecuteReader()
                While r.Read()
                    If String.Equals(Convert.ToString(r("name")), sutun, StringComparison.OrdinalIgnoreCase) Then Return True
                End While
            End Using
        End Using
        Return False
    End Function

    Private Shared Function TabloVar(c As SQLiteConnection, ad As String) As Boolean
        Using cmd As New SQLiteCommand("SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name=?", c)
            cmd.Parameters.AddWithValue("@p0", ad)
            Return Convert.ToInt32(cmd.ExecuteScalar()) = 1
        End Using
    End Function

    Private Shared Sub Calistir(c As SQLiteConnection, komutlar As String())
        Using tx As SQLiteTransaction = c.BeginTransaction()
            For Each sql As String In komutlar
                Using cmd As New SQLiteCommand(sql, c, tx)
                    cmd.ExecuteNonQuery()
                End Using
            Next
            tx.Commit()
        End Using
    End Sub

    ''' <summary>Başarısız aktarımdan sonra oluşan tabloları temizler; en iyi çaba.</summary>
    Public Shared Sub TablolariSil()
        Try
            Using c As SQLiteConnection = Db.Ac()
                For Each ad As String In New String() {"CihazIzin", "BildirimLog", "Ayar", "Yetki", "KullaniciBolum", "Kullanici", "Liste", "KalipTanim", "TalepKalip", "KalipLokasyon", "KalipHareket", "Raf", "Lokasyon", "Talep", "Kalip"}
                    Try
                        Using cmd As New SQLiteCommand("DROP TABLE IF EXISTS " & ad, c)
                            cmd.ExecuteNonQuery()
                        End Using
                    Catch
                    End Try
                Next
            End Using
        Catch
        End Try
    End Sub

    ''' <summary>Başarısız ilk kurulumda, bu kurulumun oluşturduğu dosyayı siler.</summary>
    Public Shared Sub DosyayiSil()
        Try
            GC.Collect()
            GC.WaitForPendingFinalizers()
            If File.Exists(Db.Yol) Then File.Delete(Db.Yol)
            Dim gunluk As String = Db.Yol & "-journal"
            If File.Exists(gunluk) Then File.Delete(gunluk)
        Catch
        End Try
    End Sub
End Class
