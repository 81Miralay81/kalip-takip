Imports System.Collections.Generic
Imports System.Data
Imports System.Data.SQLite
Imports System.Linq

''' <summary>Teslim alma / rafa kaldırma ekranından gelen bir satır: hangi kalıp, hangi lokasyon, hangi raf.</summary>
Public Class TeslimSatiri
    Public Property SatirId As Integer
    Public Property Lokasyon As String
    Public Property Raf As String
    ''' <summary>Teslim alırken kalıbı hemen işleme al (yalnızca kalıp sahibine dönmüyorsa: kalıphane tarafı).</summary>
    Public Property Isle As Boolean
End Class

''' <summary>Akış işleminin özeti (bildirim metni ve ekran mesajı için).</summary>
Public Class AkisSonucu
    Public Property TalepId As Integer
    Public Property TalepNo As String
    Public Property GonderenBolum As String
    Public Property AliciBolum As String
    ''' <summary>İşlemin dokunduğu kalıp adedi.</summary>
    Public Property Adet As Integer
    ''' <summary>İşlemden sonra hâlâ sevk halinde (teslim alınmamış) kalıp adedi.</summary>
    Public Property Kalan As Integer
    Public Property Kapandi As Boolean
    ''' <summary>İşlemde İşleniyor durumuna alınan kalıp adedi.</summary>
    Public Property Islenen As Integer
    Public Property Mesaj As String
End Class

''' <summary>
''' Talep akışı: Planlandı → (Talep Gönder) Sevkediliyor → (Teslim Al) Teslim Alındı / Tamamlandı.
''' Teslim alınmayanlar için alıcı "geri bildir" der; gönderen talebi kapatır (kalıplar İade Edildi, eski lokasyon/raf geri gelir)
''' ya da yeniden gönderir. Teslim alan bölüm kalıbın sahibiyse kalıp Tamamlandı olur ve tamir adedi artar.
''' </summary>
Partial Public NotInheritable Class TalepRepository

    Private Shared Function TabloOku(c As SQLiteConnection, tx As SQLiteTransaction, sql As String, ParamArray p() As Object) As DataTable
        Dim dt As New DataTable()
        Using cmd As SQLiteCommand = Db.Komut(c, tx, sql, p)
            Using da As New SQLiteDataAdapter(cmd)
                da.Fill(dt)
            End Using
        End Using
        Return dt
    End Function

    Private Shared Function Metin(o As Object) As String
        If o Is Nothing OrElse IsDBNull(o) Then Return ""
        Return Convert.ToString(o)
    End Function

    Private Shared Function AyniBolum(a As String, b As String) As Boolean
        Return TrMetin.Katla(If(a, "").Trim()) = TrMetin.Katla(If(b, "").Trim())
    End Function

    Private Shared Function TalepBaslik(c As SQLiteConnection, tx As SQLiteTransaction, talepId As Integer) As DataRow
        Dim t As DataTable = TabloOku(c, tx, "SELECT Id, TalepNo, Bolum, AliciBolum, Statu, GonderTarihi, GeriDondu FROM Talep WHERE Id = ?", talepId)
        If t.Rows.Count = 0 Then Throw New InvalidOperationException("Talep bulunamadı (silinmiş olabilir).")
        Return t.Rows(0)
    End Function

    Private Shared Function KalanSayisi(c As SQLiteConnection, tx As SQLiteTransaction, talepId As Integer) As Integer
        Return Convert.ToInt32(Skaler(c, tx, "SELECT COUNT(*) FROM TalepKalip WHERE TalepId = ? AND KalipStatu = 'Sevkediliyor'", talepId))
    End Function

    Private Shared Function SonucOlustur(c As SQLiteConnection, tx As SQLiteTransaction, talepId As Integer, adet As Integer) As AkisSonucu
        Dim r As DataRow = TalepBaslik(c, tx, talepId)
        Dim s As New AkisSonucu()
        s.TalepId = talepId
        s.TalepNo = Metin(r("TalepNo"))
        s.GonderenBolum = Metin(r("Bolum"))
        s.AliciBolum = Metin(r("AliciBolum"))
        s.Adet = adet
        s.Kalan = KalanSayisi(c, tx, talepId)
        s.Kapandi = Metin(r("Statu")) = TalepKurallari.TalepKapandi
        Return s
    End Function

    ''' <summary>Talebin alıcı bölümünü yazar (talep kaydedildikten sonra çağrılır).</summary>
    Public Shared Sub AliciYaz(talepId As Integer, alici As String)
        Dim b As String = If(alici, "").Trim().ToUpper(Kultur.Tr)
        Using c As SQLiteConnection = Db.Ac()
            Using tx As SQLiteTransaction = c.BeginTransaction()
                Calistir(c, tx, "UPDATE Talep SET AliciBolum = ? WHERE Id = ?", Bos(b), talepId)
                tx.Commit()
            End Using
        End Using
    End Sub

    ' ------------------------------------------------------------------ GÖNDER

    ''' <summary>Planlandı satırlarını Sevkediliyor yapar; kalıplar gönderen bölümün ana lokasyonuna geçer, eski lokasyon/raf saklanır.</summary>
    Public Shared Function Gonder(talepId As Integer, kullanici As String) As AkisSonucu
        Dim pre As DataTable = Db.Tablo("SELECT TalepNo, Bolum, AliciBolum, Statu FROM Talep WHERE Id = ?", talepId)
        If pre.Rows.Count = 0 Then Throw New InvalidOperationException("Talep bulunamadı (silinmiş olabilir).")
        Dim bolum As String = Metin(pre.Rows(0)("Bolum"))
        Dim alici As String = Metin(pre.Rows(0)("AliciBolum"))
        Dim statu As String = Metin(pre.Rows(0)("Statu"))
        If statu = TalepKurallari.TalepIptal OrElse statu = TalepKurallari.TalepKapandi Then
            Throw New InvalidOperationException("İptal edilmiş ya da kapanmış talep gönderilemez.")
        End If
        If alici.Trim().Length = 0 Then Throw New InvalidOperationException("Alıcı bölüm seçilmemiş. Talebi açarken 'Alıcı bölüm'ü seçin.")
        If AyniBolum(alici, bolum) Then Throw New InvalidOperationException("Gönderen ve alıcı bölüm aynı olamaz.")
        Dim planli As DataTable = Db.Tablo("SELECT Id, KalipKodu FROM TalepKalip WHERE TalepId = ? AND KalipStatu = 'Planlandı' ORDER BY Id", talepId)
        If planli.Rows.Count = 0 Then Throw New InvalidOperationException("Gönderilecek (Planlandı durumunda) kalıp yok.")
        For Each r As DataRow In planli.Rows
            Dim engel As String = EklemeEngeli(Convert.ToString(r("KalipKodu")), talepId, bolum)
            If engel IsNot Nothing Then Throw New InvalidOperationException(engel)
        Next
        Dim ana As String = LokasyonRepository.AnaLokasyon(bolum)

        Using c As SQLiteConnection = Db.Ac()
            Using tx As SQLiteTransaction = c.BeginTransaction()
                Dim talepNo As String = Metin(pre.Rows(0)("TalepNo"))
                For Each r As DataRow In planli.Rows
                    Dim satirId As Integer = Convert.ToInt32(r("Id"))
                    Dim kod As String = Convert.ToString(r("KalipKodu"))
                    Dim kl As DataTable = TabloOku(c, tx, "SELECT Lokasyon, Raf FROM KalipLokasyon WHERE Anahtar = ?", Anahtar(kod))
                    Dim eskiLok As String = If(kl.Rows.Count > 0, Metin(kl.Rows(0)("Lokasyon")), "")
                    Dim eskiRaf As String = If(kl.Rows.Count > 0, Metin(kl.Rows(0)("Raf")), "")
                    Calistir(c, tx, "UPDATE TalepKalip SET KalipStatu = 'Sevkediliyor', Lokasyon = ?, OncekiLokasyon = ?, OncekiRaf = ?, SonDegisiklik = ?, SonKullanici = ? WHERE Id = ?",
                             Bos(ana), Bos(eskiLok), Bos(eskiRaf), Db.Prm(DbType.DateTime, Date.Now), Bos(kullanici), satirId)
                    HareketYaz(c, tx, kod, talepId, talepNo, TalepKurallari.Planlandi, TalepKurallari.Sevkediliyor, eskiLok, ana, kullanici)
                    Dim mevcutSahip As String = SahipBul(c, tx, kod)
                    LokasyonKaydet(c, tx, kod, ana, If(String.IsNullOrWhiteSpace(mevcutSahip), bolum.Trim().ToUpper(Kultur.Tr), Nothing), kullanici)
                    Calistir(c, tx, "UPDATE KalipLokasyon SET Raf = NULL WHERE Anahtar = ?", Anahtar(kod))
                Next
                Calistir(c, tx, "UPDATE Talep SET GonderTarihi = ?, GeriDondu = 0, GeriTarihi = NULL WHERE Id = ?", Db.Prm(DbType.DateTime, Date.Now), talepId)
                OzetiYenile(c, tx, talepId, statu)
                Dim s As AkisSonucu = SonucOlustur(c, tx, talepId, planli.Rows.Count)
                s.Mesaj = s.TalepNo & ": " & planli.Rows.Count.ToString(Kultur.Inv) & " kalıp " & s.AliciBolum & " bölümüne gönderildi."
                tx.Commit()
                Return s
            End Using
        End Using
    End Function

    ' Satırı hedef statüye alır ve kalıbın lokasyon/rafını gönderilmeden önceki haline döndürür.
    Private Shared Sub EskiyeDondur(c As SQLiteConnection, tx As SQLiteTransaction, satirId As Integer, hedefStatu As String,
                                    talepId As Integer, talepNo As String, kullanici As String)
        Dim d As DataTable = TabloOku(c, tx, "SELECT KalipKodu, KalipStatu, Lokasyon, OncekiLokasyon, OncekiRaf FROM TalepKalip WHERE Id = ?", satirId)
        If d.Rows.Count = 0 Then Return
        Dim kod As String = Convert.ToString(d.Rows(0)("KalipKodu"))
        Dim eskiStatu As String = Metin(d.Rows(0)("KalipStatu"))
        Dim suAnLok As String = Metin(d.Rows(0)("Lokasyon"))
        Dim onceki As String = Metin(d.Rows(0)("OncekiLokasyon"))
        Dim oncekiRaf As String = Metin(d.Rows(0)("OncekiRaf"))
        Dim hedefLok As String = If(onceki.Trim().Length > 0, onceki, suAnLok)
        Calistir(c, tx, "UPDATE TalepKalip SET KalipStatu = ?, Lokasyon = ?, Raf = NULL, SonDegisiklik = ?, SonKullanici = ? WHERE Id = ?",
                 hedefStatu, Bos(hedefLok), Db.Prm(DbType.DateTime, Date.Now), Bos(kullanici), satirId)
        HareketYaz(c, tx, kod, talepId, talepNo, eskiStatu, hedefStatu, suAnLok, hedefLok, kullanici)
        LokasyonKaydet(c, tx, kod, hedefLok, Nothing, kullanici)
        Calistir(c, tx, "UPDATE KalipLokasyon SET Raf = ? WHERE Anahtar = ?", Bos(oncekiRaf), Anahtar(kod))
    End Sub

    ''' <summary>Hiç teslim alınmamış bir talebin gönderimini geri alır (Sevkediliyor → Planlandı, eski lokasyon/raf döner).</summary>
    Public Shared Function GonderimIptal(talepId As Integer, kullanici As String) As AkisSonucu
        Using c As SQLiteConnection = Db.Ac()
            Using tx As SQLiteTransaction = c.BeginTransaction()
                Dim t As DataRow = TalepBaslik(c, tx, talepId)
                If IsDBNull(t("GonderTarihi")) Then Throw New InvalidOperationException("Talep gönderilmemiş.")
                Dim toplam As Integer = Convert.ToInt32(Skaler(c, tx, "SELECT COUNT(*) FROM TalepKalip WHERE TalepId = ?", talepId))
                Dim sevk As DataTable = TabloOku(c, tx, "SELECT Id FROM TalepKalip WHERE TalepId = ? AND KalipStatu = 'Sevkediliyor'", talepId)
                If sevk.Rows.Count = 0 OrElse sevk.Rows.Count <> toplam Then
                    Throw New InvalidOperationException("Bazı kalıplar teslim alınmış; gönderim artık iptal edilemez.")
                End If
                Dim no As String = Metin(t("TalepNo"))
                For Each r As DataRow In sevk.Rows
                    EskiyeDondur(c, tx, Convert.ToInt32(r("Id")), TalepKurallari.Planlandi, talepId, no, kullanici)
                Next
                Calistir(c, tx, "UPDATE Talep SET GonderTarihi = NULL, GeriDondu = 0, GeriTarihi = NULL WHERE Id = ?", talepId)
                OzetiYenile(c, tx, talepId, Metin(t("Statu")))
                Dim s As AkisSonucu = SonucOlustur(c, tx, talepId, sevk.Rows.Count)
                s.Mesaj = no & " gönderimi iptal edildi; kalıplar Planlandı durumuna ve eski lokasyonlarına döndü."
                tx.Commit()
                Return s
            End Using
        End Using
    End Function

    ' ------------------------------------------------------------------ TESLİM AL

    ''' <summary>
    ''' Seçilen Sevkediliyor satırlarını teslim alır. Lokasyon zorunlu ve alıcı bölümün lokasyonlarından olmalıdır.
    ''' Alıcı bölüm kalıbın sahibiyse: raf zorunlu, kalıp Tamamlandı olur, tamir adedi artar. Değilse kalıp Teslim Alındı olur
    ''' (raf, grup lideri tarafından "Rafa kaldır" ile girilir).
    ''' </summary>
    Public Shared Function TeslimAl(talepId As Integer, teslimler As IEnumerable(Of TeslimSatiri), kullanici As String) As AkisSonucu
        Dim liste As List(Of TeslimSatiri) = teslimler.ToList()
        If liste.Count = 0 Then Throw New InvalidOperationException("Teslim alınacak kalıp seçilmedi.")
        Dim pre As DataTable = Db.Tablo("SELECT TalepNo, Bolum, AliciBolum, Statu, GonderTarihi FROM Talep WHERE Id = ?", talepId)
        If pre.Rows.Count = 0 Then Throw New InvalidOperationException("Talep bulunamadı (silinmiş olabilir).")
        Dim alici As String = Metin(pre.Rows(0)("AliciBolum"))
        If IsDBNull(pre.Rows(0)("GonderTarihi")) Then Throw New InvalidOperationException("Talep henüz gönderilmemiş.")
        If Metin(pre.Rows(0)("Statu")) = TalepKurallari.TalepIptal Then Throw New InvalidOperationException("İptal edilmiş talep teslim alınamaz.")

        ' doğrulama (yazmadan önce)
        Dim plan As New List(Of Object())()   ' satirId, kod, lokasyon, raf, sahibeDondu
        For Each ts As TeslimSatiri In liste
            Dim sat As DataTable = Db.Tablo("SELECT KalipKodu, KalipStatu FROM TalepKalip WHERE Id = ? AND TalepId = ?", ts.SatirId, talepId)
            If sat.Rows.Count = 0 Then Throw New InvalidOperationException("Satır bu talebe ait değil.")
            Dim kod As String = Convert.ToString(sat.Rows(0)("KalipKodu"))
            If Metin(sat.Rows(0)("KalipStatu")) <> TalepKurallari.Sevkediliyor Then
                Throw New InvalidOperationException(kod & " sevk halinde değil (statü: " & Metin(sat.Rows(0)("KalipStatu")) & "); teslim alınamaz.")
            End If
            Dim lok As String = If(ts.Lokasyon, "").Trim()
            If lok.Length = 0 Then Throw New InvalidOperationException(kod & " için teslim lokasyonu seçilmelidir.")
            Dim tanim As Object = Db.Skaler("SELECT Ad FROM Lokasyon WHERE KATLA(Ad) = ?", TrMetin.Katla(lok))
            If tanim Is Nothing OrElse IsDBNull(tanim) Then Throw New InvalidOperationException("'" & lok & "' adında bir lokasyon tanımlı değil.")
            lok = Convert.ToString(tanim)
            If Not AyniBolum(LokasyonRepository.BolumBul(lok), alici) Then
                Throw New InvalidOperationException(kod & " için '" & lok & "' lokasyonu " & alici & " bölümüne ait değil.")
            End If
            Dim sahip As String = Convert.ToString(Db.Skaler("SELECT COALESCE(SahipBolum,'') FROM KalipLokasyon WHERE Anahtar = ?", Anahtar(kod)))
            Dim sahibeDondu As Boolean = sahip.Trim().Length > 0 AndAlso AyniBolum(sahip, alici)
            Dim raf As String = Nothing
            If sahibeDondu Then
                raf = RafDogrula(ts.Raf)
                If raf Is Nothing Then Throw New InvalidOperationException(kod & " sahibine dönüyor; raf seçimi zorunludur.")
            End If
            plan.Add(New Object() {ts.SatirId, kod, lok, raf, sahibeDondu, ts.Isle AndAlso Not sahibeDondu})
        Next

        Using c As SQLiteConnection = Db.Ac()
            Using tx As SQLiteTransaction = c.BeginTransaction()
                Dim no As String = Metin(pre.Rows(0)("TalepNo"))
                For Each p As Object() In plan
                    Dim satirId As Integer = Convert.ToInt32(p(0))
                    Dim kod As String = Convert.ToString(p(1))
                    Dim lok As String = Convert.ToString(p(2))
                    Dim raf As String = If(p(3) Is Nothing, Nothing, Convert.ToString(p(3)))
                    Dim sahibe As Boolean = Convert.ToBoolean(p(4))
                    Dim isle As Boolean = Convert.ToBoolean(p(5))
                    Dim yeniStatu As String = If(sahibe, TalepKurallari.Tamamlandi, If(isle, TalepKurallari.IslemeAlindi, TalepKurallari.TeslimAlindi))
                    Dim eskiLok As String = MevcutLokasyon(c, tx, kod)
                    Calistir(c, tx, "UPDATE TalepKalip SET KalipStatu = ?, Lokasyon = ?, Raf = ?, TeslimTarihi = ?, SonDegisiklik = ?, SonKullanici = ? WHERE Id = ?",
                             yeniStatu, lok, Bos(raf), Db.Prm(DbType.DateTime, Date.Now), Db.Prm(DbType.DateTime, Date.Now), Bos(kullanici), satirId)
                    HareketYaz(c, tx, kod, talepId, no, TalepKurallari.Sevkediliyor, yeniStatu, eskiLok, lok, kullanici)
                    LokasyonKaydet(c, tx, kod, lok, Nothing, kullanici)
                    Calistir(c, tx, "UPDATE KalipLokasyon SET Raf = ? WHERE Anahtar = ?", Bos(raf), Anahtar(kod))
                    If sahibe Then Calistir(c, tx, "UPDATE KalipLokasyon SET TamirAdedi = COALESCE(TamirAdedi, 0) + 1 WHERE Anahtar = ?", Anahtar(kod))
                Next
                OzetiYenile(c, tx, talepId, Metin(pre.Rows(0)("Statu")))
                Dim s As AkisSonucu = SonucOlustur(c, tx, talepId, plan.Count)
                If s.Kapandi Then Calistir(c, tx, "UPDATE Talep SET GeriDondu = 0, GeriTarihi = NULL WHERE Id = ?", talepId)
                s.Islenen = plan.Where(Function(q) Convert.ToBoolean(q(5))).Count()
                s.Mesaj = s.TalepNo & ": " & plan.Count.ToString(Kultur.Inv) & " kalıp teslim alındı." & If(s.Islenen > 0, " " & s.Islenen.ToString(Kultur.Inv) & " kalıp işleme alındı.", "") &
                          If(s.Kapandi, " Talep kapandı.", " " & s.Kalan.ToString(Kultur.Inv) & " kalıp henüz teslim alınmadı.")
                tx.Commit()
                Return s
            End Using
        End Using
    End Function

    ''' <summary>Alıcı, kalan (gelmeyen) kalıpları gönderene geri bildirir; talep kapanmaz, gönderenin kararını bekler.</summary>
    Public Shared Function GeriBildir(talepId As Integer, kullanici As String) As AkisSonucu
        Using c As SQLiteConnection = Db.Ac()
            Using tx As SQLiteTransaction = c.BeginTransaction()
                Dim t As DataRow = TalepBaslik(c, tx, talepId)
                If IsDBNull(t("GonderTarihi")) Then Throw New InvalidOperationException("Talep henüz gönderilmemiş.")
                Dim kalan As Integer = KalanSayisi(c, tx, talepId)
                If kalan = 0 Then Throw New InvalidOperationException("Teslim alınmamış kalıp yok.")
                Calistir(c, tx, "UPDATE Talep SET GeriDondu = 1, GeriTarihi = ? WHERE Id = ?", Db.Prm(DbType.DateTime, Date.Now), talepId)
                Dim s As AkisSonucu = SonucOlustur(c, tx, talepId, kalan)
                s.Mesaj = s.TalepNo & ": teslim alınmayan " & kalan.ToString(Kultur.Inv) & " kalıp " & s.GonderenBolum & " bölümüne geri bildirildi."
                tx.Commit()
                Return s
            End Using
        End Using
    End Function

    ''' <summary>Gönderen karar verir: gelmeyen kalıplar İade Edildi olur, eski lokasyon/raf geri gelir ve talep kapanır.</summary>
    Public Shared Function GeriKapat(talepId As Integer, kullanici As String) As AkisSonucu
        Using c As SQLiteConnection = Db.Ac()
            Using tx As SQLiteTransaction = c.BeginTransaction()
                Dim t As DataRow = TalepBaslik(c, tx, talepId)
                If Convert.ToInt32(t("GeriDondu")) <> 1 Then Throw New InvalidOperationException("Bu talep için alıcıdan bir geri bildirim yok.")
                Dim no As String = Metin(t("TalepNo"))
                Dim sevk As DataTable = TabloOku(c, tx, "SELECT Id FROM TalepKalip WHERE TalepId = ? AND KalipStatu = 'Sevkediliyor'", talepId)
                For Each r As DataRow In sevk.Rows
                    EskiyeDondur(c, tx, Convert.ToInt32(r("Id")), TalepKurallari.IadeEdildi, talepId, no, kullanici)
                Next
                Calistir(c, tx, "UPDATE Talep SET GeriDondu = 0, GeriTarihi = NULL WHERE Id = ?", talepId)
                OzetiYenile(c, tx, talepId, Metin(t("Statu")))
                Dim s As AkisSonucu = SonucOlustur(c, tx, talepId, sevk.Rows.Count)
                s.Mesaj = no & " kapatıldı; gelmeyen " & sevk.Rows.Count.ToString(Kultur.Inv) & " kalıp İade Edildi olarak işaretlendi, eski lokasyon ve rafına döndü."
                tx.Commit()
                Return s
            End Using
        End Using
    End Function

    ''' <summary>Gönderen, gelmeyen kalıpları tekrar gönderir (talep yeniden alıcıda açık görünür).</summary>
    Public Shared Function TekrarGonder(talepId As Integer, kullanici As String) As AkisSonucu
        Using c As SQLiteConnection = Db.Ac()
            Using tx As SQLiteTransaction = c.BeginTransaction()
                Dim t As DataRow = TalepBaslik(c, tx, talepId)
                If Convert.ToInt32(t("GeriDondu")) <> 1 Then Throw New InvalidOperationException("Bu talep için alıcıdan bir geri bildirim yok.")
                Calistir(c, tx, "UPDATE Talep SET GeriDondu = 0, GeriTarihi = NULL, GonderTarihi = ? WHERE Id = ?", Db.Prm(DbType.DateTime, Date.Now), talepId)
                Dim s As AkisSonucu = SonucOlustur(c, tx, talepId, KalanSayisi(c, tx, talepId))
                s.Mesaj = s.TalepNo & ": " & s.Kalan.ToString(Kultur.Inv) & " kalıp tekrar " & s.AliciBolum & " bölümüne gönderildi."
                tx.Commit()
                Return s
            End Using
        End Using
    End Function

    ' ------------------------------------------------------------------ RAFA KALDIR

    ''' <summary>Teslim alınmış (raf girilmemiş) kalıplara raf yazar. Raf zorunludur.</summary>
    Public Shared Function RafaKaldir(talepId As Integer, satirlar As IEnumerable(Of TeslimSatiri), kullanici As String) As AkisSonucu
        Dim liste As List(Of TeslimSatiri) = satirlar.ToList()
        If liste.Count = 0 Then Throw New InvalidOperationException("Rafa kaldırılacak kalıp seçilmedi.")
        Dim plan As New List(Of Object())()
        For Each ts As TeslimSatiri In liste
            Dim sat As DataTable = Db.Tablo("SELECT KalipKodu, KalipStatu FROM TalepKalip WHERE Id = ? AND TalepId = ?", ts.SatirId, talepId)
            If sat.Rows.Count = 0 Then Throw New InvalidOperationException("Satır bu talebe ait değil.")
            Dim kod As String = Convert.ToString(sat.Rows(0)("KalipKodu"))
            Dim st As String = Metin(sat.Rows(0)("KalipStatu"))
            If st <> TalepKurallari.TeslimAlindi AndAlso st <> TalepKurallari.Tamamlandi Then
                Throw New InvalidOperationException(kod & " teslim alınmamış; rafa kaldırılamaz.")
            End If
            Dim raf As String = RafDogrula(ts.Raf)
            If raf Is Nothing Then Throw New InvalidOperationException(kod & " için raf seçimi zorunludur.")
            plan.Add(New Object() {ts.SatirId, kod, raf})
        Next
        Using c As SQLiteConnection = Db.Ac()
            Using tx As SQLiteTransaction = c.BeginTransaction()
                For Each p As Object() In plan
                    Calistir(c, tx, "UPDATE TalepKalip SET Raf = ?, SonDegisiklik = ?, SonKullanici = ? WHERE Id = ?",
                             Convert.ToString(p(2)), Db.Prm(DbType.DateTime, Date.Now), Bos(kullanici), Convert.ToInt32(p(0)))
                    Calistir(c, tx, "UPDATE KalipLokasyon SET Raf = ?, SonDegisiklik = ?, SonKullanici = ? WHERE Anahtar = ?",
                             Convert.ToString(p(2)), Db.Prm(DbType.DateTime, Date.Now), Bos(kullanici), Anahtar(Convert.ToString(p(1))))
                Next
                Dim s As AkisSonucu = SonucOlustur(c, tx, talepId, plan.Count)
                s.Mesaj = s.TalepNo & ": " & plan.Count.ToString(Kultur.Inv) & " kalıp rafa kaldırıldı."
                tx.Commit()
                Return s
            End Using
        End Using
    End Function

    ' ------------------------------------------------------------------ İŞLEME AL

    ''' <summary>
    ''' Teslim alınmış (Teslim Alındı) kalıp tamir için raftan indirilir: statü İşleniyor olur, raf boşalır.
    ''' Yalnızca kalıp sahibine dönmeyen (kalıphane) tarafında geçerlidir.
    ''' </summary>
    Public Shared Function IsleAl(talepId As Integer, satirIdleri As IEnumerable(Of Integer), kullanici As String) As AkisSonucu
        Dim idler As List(Of Integer) = satirIdleri.ToList()
        If idler.Count = 0 Then Throw New InvalidOperationException("İşleme alınacak kalıp seçilmedi.")
        Dim pre As DataTable = Db.Tablo("SELECT TalepNo, AliciBolum, Statu FROM Talep WHERE Id = ?", talepId)
        If pre.Rows.Count = 0 Then Throw New InvalidOperationException("Talep bulunamadı (silinmiş olabilir).")
        Dim alici As String = Metin(pre.Rows(0)("AliciBolum"))
        Dim plan As New List(Of Object())()
        Dim bilgi As New List(Of String)()
        For Each id As Integer In idler
            Dim sat As DataTable = Db.Tablo("SELECT KalipKodu, KalipStatu, Lokasyon FROM TalepKalip WHERE Id = ? AND TalepId = ?", id, talepId)
            If sat.Rows.Count = 0 Then Throw New InvalidOperationException("Satır bu talebe ait değil.")
            Dim kod As String = Convert.ToString(sat.Rows(0)("KalipKodu"))
            If Metin(sat.Rows(0)("KalipStatu")) <> TalepKurallari.TeslimAlindi Then
                Throw New InvalidOperationException(kod & " 'Teslim Alındı' durumunda değil; işleme alınamaz.")
            End If
            Dim kl As DataTable = Db.Tablo("SELECT SahipBolum, Raf FROM KalipLokasyon WHERE Anahtar = ?", Anahtar(kod))
            Dim sahip As String = If(kl.Rows.Count > 0, Metin(kl.Rows(0)("SahipBolum")), "")
            If sahip.Trim().Length > 0 AndAlso AyniBolum(sahip, alici) Then
                Throw New InvalidOperationException(kod & " kendi bölümüne döndü; işleme alma yalnızca kalıphane tarafında geçerlidir.")
            End If
            Dim raf As String = If(kl.Rows.Count > 0, Metin(kl.Rows(0)("Raf")), "")
            bilgi.Add(If(raf.Length > 0, kod & " (raf " & raf & ")", kod))
            plan.Add(New Object() {id, kod, Metin(sat.Rows(0)("Lokasyon"))})
        Next
        Using c As SQLiteConnection = Db.Ac()
            Using tx As SQLiteTransaction = c.BeginTransaction()
                Dim no As String = Metin(pre.Rows(0)("TalepNo"))
                For Each p As Object() In plan
                    Dim kod As String = Convert.ToString(p(1))
                    Dim lok As String = Convert.ToString(p(2))
                    Calistir(c, tx, "UPDATE TalepKalip SET KalipStatu = ?, Raf = NULL, SonDegisiklik = ?, SonKullanici = ? WHERE Id = ?",
                             TalepKurallari.IslemeAlindi, Db.Prm(DbType.DateTime, Date.Now), Bos(kullanici), Convert.ToInt32(p(0)))
                    HareketYaz(c, tx, kod, talepId, no, TalepKurallari.TeslimAlindi, TalepKurallari.IslemeAlindi, lok, lok, kullanici)
                    Calistir(c, tx, "UPDATE KalipLokasyon SET Raf = NULL, SonDegisiklik = ?, SonKullanici = ? WHERE Anahtar = ?",
                             Db.Prm(DbType.DateTime, Date.Now), Bos(kullanici), Anahtar(kod))
                Next
                OzetiYenile(c, tx, talepId, Metin(pre.Rows(0)("Statu")))
                Dim s As AkisSonucu = SonucOlustur(c, tx, talepId, plan.Count)
                s.Islenen = plan.Count
                s.Mesaj = s.TalepNo & ": " & String.Join(", ", bilgi) & " raftan indirilip İşleniyor durumuna alındı."
                tx.Commit()
                Return s
            End Using
        End Using
    End Function

    ' ------------------------------------------------------------------ ROZET

    ''' <summary>
    ''' Portal rozetleri: gelen (teslim bekleyen) talepler, geri dönen talepler, raf bekleyen kalıplar.
    ''' hepsi = True ise bölüm süzülmez; değilse yalnızca verilen bölümler sayılır.
    ''' </summary>
    Public Shared Function Rozet(bolumler As IEnumerable(Of String), hepsi As Boolean) As Dictionary(Of String, Integer)
        Dim sonuc As New Dictionary(Of String, Integer)()
        sonuc("gelen") = 0
        sonuc("geriDonen") = 0
        sonuc("rafBekleyen") = 0
        Dim b As List(Of String) = bolumler.Select(Function(x) TrMetin.Katla(If(x, "").Trim())).Where(Function(x) x.Length > 0).Distinct().ToList()
        If Not hepsi AndAlso b.Count = 0 Then Return sonuc
        Dim yer As String = String.Join(",", Enumerable.Repeat("?", Math.Max(1, b.Count)))
        Dim pl As New List(Of Object)()
        If Not hepsi Then pl.AddRange(b.Cast(Of Object)())
        Dim aliciSuz As String = If(hepsi, "", " AND KATLA(t.AliciBolum) IN (" & yer & ")")
        Dim gonderSuz As String = If(hepsi, "", " AND KATLA(t.Bolum) IN (" & yer & ")")
        sonuc("gelen") = Convert.ToInt32(Db.Skaler(
            "SELECT COUNT(*) FROM Talep t WHERE t.GonderTarihi IS NOT NULL AND t.GeriDondu = 0 AND t.Statu NOT IN ('Kapandı','İptal') " &
            "AND EXISTS (SELECT 1 FROM TalepKalip k WHERE k.TalepId = t.Id AND k.KalipStatu = 'Sevkediliyor')" & aliciSuz, pl.ToArray()))
        sonuc("geriDonen") = Convert.ToInt32(Db.Skaler(
            "SELECT COUNT(*) FROM Talep t WHERE t.GeriDondu = 1 AND t.Statu NOT IN ('Kapandı','İptal')" & gonderSuz, pl.ToArray()))
        sonuc("rafBekleyen") = Convert.ToInt32(Db.Skaler(
            "SELECT COUNT(*) FROM TalepKalip k JOIN Talep t ON t.Id = k.TalepId WHERE t.GonderTarihi IS NOT NULL AND t.Statu <> 'İptal' " &
            "AND k.KalipStatu = 'Teslim Alındı' AND (k.Raf IS NULL OR TRIM(k.Raf) = '')" & aliciSuz, pl.ToArray()))
        Return sonuc
    End Function

    ''' <summary>
    ''' Bildirim alacak kullanıcılar: aktif, "bildirim al" işaretli ve verilen bölümde olanlar.
    ''' izin doluysa yalnızca o yetkiye sahip (ya da yönetici) olanlar (örn. grup liderleri: raf_kaldir).
    ''' </summary>
    Public Shared Function BildirimAlicilari(bolum As String, Optional izin As String = "") As DataTable
        Dim an As String = TrMetin.Katla(If(bolum, "").Trim())
        Dim sql As String =
            "SELECT u.Id AS Id, u.KullaniciAdi AS KullaniciAdi, u.Email AS Email, u.TelegramId AS TelegramId FROM Kullanici u " &
            "WHERE u.Aktif = 1 AND u.BildirimAl = 1 AND (" &
            "EXISTS (SELECT 1 FROM KullaniciBolum kb WHERE kb.KullaniciId = u.Id AND KATLA(kb.Bolum) = ?) OR KATLA(COALESCE(u.Bolum,'')) = ?)"
        If String.IsNullOrEmpty(izin) Then Return Db.Tablo(sql, an, an)
        sql &= " AND (u.IsAdmin = 1 OR EXISTS (SELECT 1 FROM Yetki y WHERE y.KullaniciId = u.Id AND y.Anahtar = ?))"
        Return Db.Tablo(sql, an, an, izin)
    End Function
End Class
