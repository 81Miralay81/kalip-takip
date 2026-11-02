Imports System.Collections.Generic
Imports System.Data
Imports System.Data.SQLite
Imports System.Globalization
Imports System.IO
Imports System.Linq
Imports System.Text

Public Class AktarimSonucu
    Public Property KayitSayisi As Integer
    Public Property KullaniciSayisi As Integer
    Public Property SorunSayisi As Integer
    Public Property ToplamDuzeltilen As Integer
    Public Property SorunDosyasi As String
End Class

''' <summary>
''' Eski kalıp_takip.xlsm dosyasından (AddressBook + USERS + Setting sayfaları) yeni SQLite veritabanına tek seferlik aktarım.
''' Hiçbir veri sessizce atılmaz: çevrilemeyen değerler "Açıklama" alanına not düşülür ve sorun raporuna (CSV) yazılır.
''' </summary>
Public Class ExcelAktarici

    Private Shared ReadOnly SureAdlari As String() = {
        "İşlem Süresi", "Ayar", "Bakım", "Eleman Eksikliği", "Elektrik", "Kalıp Arıza", "Takım", "Arıza"
    }

    Private ReadOnly _sorunlar As New List(Of String())()
    Private _notlar As New List(Of String)()

    Public Shared Function Calistir(xlsmYol As String, toplamYenidenHesapla As Boolean, ilerleme As IProgress(Of String)) As AktarimSonucu
        Return New ExcelAktarici().Yurut(xlsmYol, toplamYenidenHesapla, ilerleme)
    End Function

    Private Function Yurut(xlsmYol As String, yenidenHesapla As Boolean, ilerleme As IProgress(Of String)) As AktarimSonucu
        Dim sonuc As New AktarimSonucu()
        Dim dosyaOlustu As Boolean = False
        Try
            Using x As New XlsxOkuyucu(xlsmYol)
                If Not x.SayfaVar("AddressBook") Then Throw New InvalidDataException("Excel dosyasında 'AddressBook' sayfası bulunamadı.")
                If Not x.SayfaVar("USERS") Then Throw New InvalidDataException("Excel dosyasında 'USERS' sayfası bulunamadı.")

                Rapor(ilerleme, "Veritabanı oluşturuluyor...")
                dosyaOlustu = Sema.VeritabaniOlustur()
                Sema.TablolariKur()

                sonuc.KayitSayisi = KayitlariAktar(x, yenidenHesapla, sonuc, ilerleme)
                sonuc.KullaniciSayisi = KullanicilariAktar(x, ilerleme)
                Rapor(ilerleme, "Liste değerleri aktarılıyor...")
                ListeleriAktar(x)
            End Using
            sonuc.SorunSayisi = _sorunlar.Count
            sonuc.SorunDosyasi = SorunDosyasiYaz()
            Rapor(ilerleme, "Tamamlandı.")
            Return sonuc
        Catch
            If dosyaOlustu Then
                Sema.DosyayiSil()
            Else
                Sema.TablolariSil()
            End If
            Throw
        End Try
    End Function

    Private Shared Sub Rapor(ilerleme As IProgress(Of String), metin As String)
        If ilerleme IsNot Nothing Then ilerleme.Report(metin)
    End Sub

    ' ------------------------------------------------------------------ KAYITLAR

    Private Function KayitlariAktar(x As XlsxOkuyucu, yenidenHesapla As Boolean, sonuc As AktarimSonucu, ilerleme As IProgress(Of String)) As Integer
        Dim dt As DataTable = KalipTablosu()
        Dim say As Integer = 0
        Dim ilk As Boolean = True
        Dim simdi As Date = Date.Now

        For Each satir As Dictionary(Of Integer, String) In x.Satirlar("AddressBook")
                        If ilk Then
                            ilk = False
                            If Not String.Equals(Al(satir, 0), "ID", StringComparison.OrdinalIgnoreCase) Then
                                Throw New InvalidDataException("AddressBook sayfasının ilk sütunu 'ID' olmalı; beklenen sütun düzeni bulunamadı.")
                            End If
                            Continue For
                        End If

                        Dim idMetin As String = Al(satir, 0)
                        If idMetin Is Nothing Then Continue For
                        Dim idD As Double
                        If Not Sayi.TryParse(idMetin, idD) OrElse idD < 1 OrElse idD > Integer.MaxValue Then
                            Sorun(idMetin, "ID", idMetin, "Geçersiz ID; satır aktarılmadı")
                            Continue For
                        End If
                        Dim id As Integer = CInt(Math.Round(idD))
                        Dim idStr As String = id.ToString(Kultur.Inv)
                        _notlar = New List(Of String)()

                        Dim parcaKodu As String = Kes(Al(satir, 1), 50)
                        Dim kalipTuru As String = Kes(Al(satir, 2), 60)
                        Dim tepeKodu As String = Kes(Al(satir, 3), 100)
                        Dim parcaAdi As String = Al(satir, 4)
                        Dim gelis As Date? = TarihAl(satir, 5, idStr, "Geliş Tarihi")
                        Dim tashih As Integer? = TamAl(satir, 6, idStr, "Tashih Sayı")
                        Dim bitis As Date? = TarihAl(satir, 7, idStr, "Bitiş Tarihi")
                        Dim hafta As String = Kes(Al(satir, 8), 10)
                        Dim tur As String = Kes(Al(satir, 9), 30)
                        Dim dovme As Double? = SayiAl(satir, 10, idStr, "Dövme Adeti", False)
                        Dim cnc As Integer? = TamAl(satir, 11, idStr, "CNC NO")
                        Dim aciklama As String = Al(satir, 12)

                        Dim sureler(7) As Double?
                        Dim hesap As Double? = Nothing
                        For j As Integer = 0 To 7
                            sureler(j) = SayiAl(satir, 13 + j, idStr, SureAdlari(j), True)
                            If sureler(j).HasValue Then hesap = If(hesap, 0.0) + sureler(j).Value
                        Next

                        Dim toplam As Double? = SayiAl(satir, 21, idStr, "TOPLAM ZAMAN", False)
                        If hesap.HasValue Then
                            If Not toplam.HasValue OrElse Math.Abs(toplam.Value - hesap.Value) > 0.051 Then
                                Dim kayitli As String = If(toplam.HasValue, Sayi.Yaz(toplam.Value), "(boş)")
                                Dim notMetin As String = "Kayıtlı toplam " & kayitli & ", bileşenler toplamı " & Sayi.Yaz(hesap.Value)
                                If yenidenHesapla Then
                                    notMetin &= " (yeniden hesaplandı)"
                                    toplam = hesap
                                    sonuc.ToplamDuzeltilen += 1
                                End If
                                Sorun(idStr, "TOPLAM ZAMAN", kayitli, notMetin)
                            End If
                        End If

                        If _notlar.Count > 0 Then
                            Dim ek As String = "[Aktarım: " & String.Join("; ", _notlar) & "]"
                            aciklama = Kes((If(aciklama, "") & " " & ek).Trim(), 255)
                        Else
                            aciklama = Kes(aciklama, 255)
                        End If

                        Dim degerler As Object() = {
                            id, parcaKodu, kalipTuru, tepeKodu, parcaAdi,
                            gelis, tashih, bitis, hafta, tur,
                            dovme, cnc, aciklama,
                            sureler(0), sureler(1), sureler(2), sureler(3), sureler(4), sureler(5), sureler(6), sureler(7), toplam,
                            1, simdi, "AKTARIM"
                        }
                        For i As Integer = 0 To degerler.Length - 1
                            If degerler(i) Is Nothing Then degerler(i) = DBNull.Value
                        Next
                        dt.Rows.Add(degerler)
                        say += 1
                        If say Mod 2000 = 0 Then Rapor(ilerleme, say.ToString("N0", Kultur.Tr) & " kayıt okundu...")
        Next

        Rapor(ilerleme, "Kayıtlar veritabanına yazılıyor (" & say.ToString("N0", Kultur.Tr) & ")...")
        Using c As SQLiteConnection = Db.Ac()
            Using tx As SQLiteTransaction = c.BeginTransaction()
                TopluYaz.Ekle(c, tx, "Kalip", dt)
                tx.Commit()
            End Using
        End Using
        Return say
    End Function

    ''' <summary>Kalip tablosuyla aynı kolon sırasında (Id + 21 alan + Surum, SonDegisiklik, SonKullanici) boş tablo.</summary>
    Private Shared Function KalipTablosu() As DataTable
        Dim dt As New DataTable("Kalip")
        Dim adlar As String() = {"Id", "ParcaKodu", "KalipTuru", "TepeKodu", "ParcaAdi", "GelisTarihi", "TashihSayi", "BitisTarihi",
                                 "Hafta", "Tur", "DovmeAdeti", "CncNo", "Aciklama", "IslemSuresi", "Ayar", "Bakim",
                                 "ElemanEksikligi", "Elektrik", "KalipAriza", "Takim", "Ariza", "ToplamZaman",
                                 "Surum", "SonDegisiklik", "SonKullanici"}
        Dim tipler As Type() = {GetType(Integer), GetType(String), GetType(String), GetType(String), GetType(String),
                                GetType(Date), GetType(Integer), GetType(Date),
                                GetType(String), GetType(String), GetType(Double), GetType(Integer), GetType(String),
                                GetType(Double), GetType(Double), GetType(Double),
                                GetType(Double), GetType(Double), GetType(Double),
                                GetType(Double), GetType(Double), GetType(Double),
                                GetType(Integer), GetType(Date), GetType(String)}
        For i As Integer = 0 To adlar.Length - 1
            dt.Columns.Add(adlar(i), tipler(i))
        Next
        Return dt
    End Function

    Private Shared Function Al(satir As Dictionary(Of Integer, String), kol As Integer) As String
        Dim v As String = Nothing
        If satir.TryGetValue(kol, v) AndAlso v IsNot Nothing Then
            v = v.Trim()
            If v.Length > 0 Then Return v
        End If
        Return Nothing
    End Function

    Private Shared Function Kes(s As String, en As Integer) As String
        If s Is Nothing Then Return Nothing
        Return If(s.Length > en, s.Substring(0, en), s)
    End Function

    Private Sub Sorun(id As String, alan As String, ham As String, aciklama As String)
        _sorunlar.Add(New String() {id, alan, ham, aciklama})
    End Sub

    Private Function TarihAl(satir As Dictionary(Of Integer, String), kol As Integer, id As String, alan As String) As Date?
        Dim ham As String = Al(satir, kol)
        If ham Is Nothing Then Return Nothing
        Dim d As Date
        If TarihCevir(ham, d) Then Return d
        Sorun(id, alan, ham, "Geçerli tarih değil; boş bırakıldı, Açıklama alanına not eklendi")
        _notlar.Add(alan & ": " & ham)
        Return Nothing
    End Function

    Private Shared Function TarihCevir(ham As String, ByRef d As Date) As Boolean
        Dim bulundu As Boolean = Date.TryParseExact(ham, {"d.M.yyyy", "dd.MM.yyyy"}, Kultur.Inv, DateTimeStyles.None, d)
        If Not bulundu Then
            Dim seri As Double
            If Double.TryParse(ham, NumberStyles.Float, Kultur.Inv, seri) AndAlso seri > 30000 AndAlso seri < 80000 Then
                d = Date.FromOADate(seri)
                bulundu = True
            End If
        End If
        If Not bulundu Then Return False
        Return d.Year >= 1990 AndAlso d.Year <= Date.Today.Year + 1
    End Function

    Private Function TamAl(satir As Dictionary(Of Integer, String), kol As Integer, id As String, alan As String) As Integer?
        Dim ham As String = Al(satir, kol)
        If ham Is Nothing Then Return Nothing
        Dim d As Double
        If Sayi.TryParse(ham, d) AndAlso d >= 0 AndAlso d <= Integer.MaxValue AndAlso Math.Abs(d - Math.Round(d)) < 0.000001 Then
            Return CInt(Math.Round(d))
        End If
        Sorun(id, alan, ham, "Tam sayıya çevrilemedi; boş bırakıldı, Açıklama alanına not eklendi")
        _notlar.Add(alan & ": " & ham)
        Return Nothing
    End Function

    Private Function SayiAl(satir As Dictionary(Of Integer, String), kol As Integer, id As String, alan As String, ifadeIzinli As Boolean) As Double?
        Dim ham As String = Al(satir, kol)
        If ham Is Nothing Then Return Nothing
        Dim d As Double
        If Sayi.TryParse(ham, d) Then Return d
        If ifadeIzinli AndAlso Sayi.Hesapla(ham, d) Then
            Sorun(id, alan, ham, "İşlem olarak hesaplandı: " & Sayi.Yaz(d))
            Return d
        End If
        Sorun(id, alan, ham, "Sayıya çevrilemedi; boş bırakıldı, Açıklama alanına not eklendi")
        _notlar.Add(alan & ": " & ham)
        Return Nothing
    End Function

    ' ------------------------------------------------------------------ KULLANICILAR

    ''' <summary>Eski CommandButtonN sütunlarının yeni yetki anahtarlarına karşılığı.</summary>
    Private Shared Function ButonHaritasi() As Dictionary(Of String, String)
        Return New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase) From {
            {"CommandButton1", Izin.Guncelle},
            {"CommandButton4", Izin.Sil},
            {"CommandButton5", Izin.Excel},
            {"CommandButton6", Izin.Ekle},
            {"CommandButton7", Izin.Tashih},
            {"CommandButton10", Izin.Istatistik}
        }
    End Function

    Private Function KullanicilariAktar(x As XlsxOkuyucu, ilerleme As IProgress(Of String)) As Integer
        Rapor(ilerleme, "Kullanıcılar aktarılıyor...")
        Dim basliklar As New Dictionary(Of Integer, String)()
        Dim harita As Dictionary(Of String, String) = ButonHaritasi()
        Dim gorulen As New HashSet(Of String)(StringComparer.Ordinal)
        Dim ilk As Boolean = True
        Dim eklenen As Integer = 0

        For Each satir As Dictionary(Of Integer, String) In x.Satirlar("USERS")
            If ilk Then
                ilk = False
                For Each kv As KeyValuePair(Of Integer, String) In satir
                    basliklar(kv.Key) = kv.Value.Trim()
                Next
                Continue For
            End If

            Dim ad As String = Al(satir, 0)
            Dim parola As String = Al(satir, 1)
            If ad Is Nothing OrElse parola Is Nothing Then Continue For
            ad = ad.ToUpper(Kultur.Tr)
            If Not gorulen.Add(ad) Then Continue For

            Dim k As New Kullanici()
            k.KullaniciAdi = ad
            k.Aktif = True
            k.IsAdmin = (eklenen = 0)   ' ilk kullanıcı yönetici olur; sonra Kullanıcılar ekranından değiştirilebilir
            For Each kv As KeyValuePair(Of Integer, String) In basliklar
                Dim anahtar As String = Nothing
                If kv.Key >= 2 AndAlso harita.TryGetValue(kv.Value, anahtar) Then
                    If Al(satir, kv.Key) = "1" Then k.Izinler.Add(anahtar)
                End If
            Next
            KullaniciRepository.Kaydet(k, parola)
            eklenen += 1
        Next
        If eklenen = 0 Then Throw New InvalidDataException("USERS sayfasında kullanıcı bulunamadı.")
        Return eklenen
    End Function

    ' ------------------------------------------------------------------ LİSTELER

    Private Shared Sub ListeyeEkle(liste As List(Of String), deger As String)
        If deger IsNot Nothing AndAlso Not liste.Contains(deger) Then liste.Add(deger)
    End Sub

    Private Sub ListeleriAktar(x As XlsxOkuyucu)
        Dim tur As New List(Of String)()
        Dim kalip As New List(Of String)()

        Dim ilk As Boolean = True
        For Each satir As Dictionary(Of Integer, String) In x.Satirlar("USERS")
            If ilk Then
                ilk = False
                Continue For
            End If
            ListeyeEkle(tur, Al(satir, 20))     ' USERS!U : YENİ / İLAVE / TASHİH
            ListeyeEkle(kalip, Al(satir, 21))   ' USERS!V : kalıp türleri
        Next
        If x.SayfaVar("Setting") Then
            For Each satir As Dictionary(Of Integer, String) In x.Satirlar("Setting")
                ListeyeEkle(tur, Al(satir, 0))  ' Setting!A : FASON, RVZYON, ISIL İŞLEM ...
            Next
        End If

        ' Listede olmayıp veride en az 3 kez geçen kalıp türlerini de ekle (ör. "7" SICAK KALİBRE DELME KALIBI)
        Dim veride As System.Data.DataTable = Db.Tablo("SELECT KalipTuru FROM Kalip WHERE KalipTuru IS NOT NULL GROUP BY KalipTuru HAVING COUNT(*) >= 3")
        For Each r As System.Data.DataRow In veride.Rows
            ListeyeEkle(kalip, Convert.ToString(r("KalipTuru")))
        Next

        ListeYaz(ListeRepository.GrupTur, tur)
        ListeYaz(ListeRepository.GrupKalipTuru, kalip)
    End Sub

    Private Shared Sub ListeYaz(grup As String, degerler As List(Of String))
        For i As Integer = 0 To degerler.Count - 1
            Db.Calistir("INSERT INTO Liste (Grup, Deger, Sira) VALUES (?, ?, ?)", grup, degerler(i), i + 1)
        Next
    End Sub

    ' ------------------------------------------------------------------ SORUN RAPORU

    Private Function SorunDosyasiYaz() As String
        If _sorunlar.Count = 0 Then Return Nothing
        Dim ad As String = "aktarim_sorunlari_" & Date.Now.ToString("yyyyMMdd_HHmm", Kultur.Inv) & ".csv"
        Dim yol As String = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ad)
        Try
            CsvYaz(yol)
        Catch
            yol = Path.Combine(Path.GetTempPath(), ad)
            CsvYaz(yol)
        End Try
        Return yol
    End Function

    Private Sub CsvYaz(yol As String)
        Using w As New StreamWriter(yol, False, New UTF8Encoding(True))
            w.WriteLine("ID;Alan;Ham değer;Açıklama")
            For Each s As String() In _sorunlar
                w.WriteLine(String.Join(";", s.Select(Function(h) """" & If(h, "").Replace("""", """""") & """")))
            Next
        End Using
    End Sub
End Class
