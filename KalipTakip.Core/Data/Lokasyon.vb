Imports System.Collections.Generic
Imports System.Data
Imports System.Data.SQLite

''' <summary>Bir lokasyon: hangi bölüme ait olduğu ve o bölümün ana lokasyonu olup olmadığı.</summary>
Public Class LokasyonKaydi
    Public Property Ad As String
    Public Property Bolum As String
    ''' <summary>Bölümün ana lokasyonu: talep açılınca ve sevk işlemlerinde kalıp buraya yerleşir.</summary>
    Public Property AnaMi As Boolean
End Class

Public NotInheritable Class LokasyonRepository
    Private Sub New()
    End Sub

    Private Shared Function Buyuk(s As String) As String
        Return If(s, "").Trim().ToUpper(Kultur.Tr)
    End Function

    Public Shared Function Listele() As DataTable
        Return Db.Tablo("SELECT Ad, Bolum, AnaMi FROM Lokasyon ORDER BY Bolum, AnaMi DESC, Ad")
    End Function

    ''' <summary>Bölüm adları (lokasyon tanımlarından + eski bölüm listesinden).</summary>
    Public Shared Function Bolumler() As List(Of String)
        Dim sonuc As New List(Of String)()
        Try
            For Each r As DataRow In Db.Tablo("SELECT DISTINCT Bolum FROM Lokasyon ORDER BY Bolum").Rows
                sonuc.Add(Convert.ToString(r("Bolum")))
            Next
            For Each x As String In ListeRepository.Getir("Bolum")
                Dim b As String = Buyuk(x)
                If b.Length > 0 AndAlso Not sonuc.Contains(b) Then sonuc.Add(b)
            Next
        Catch
        End Try
        Return sonuc
    End Function

    Public Shared Function Adlar() As List(Of String)
        Dim sonuc As New List(Of String)()
        Try
            For Each r As DataRow In Db.Tablo("SELECT Ad FROM Lokasyon ORDER BY Bolum, Ad").Rows
                sonuc.Add(Convert.ToString(r("Ad")))
            Next
        Catch
        End Try
        Return sonuc
    End Function

    ''' <summary>Lokasyonun bölümü. Tanımlı değilse lokasyon adının kendisi bölüm sayılır.</summary>
    Public Shared Function BolumBul(lokasyon As String) As String
        Dim o As Object = Db.Skaler("SELECT Bolum FROM Lokasyon WHERE KATLA(Ad) = ?", TrMetin.Katla(If(lokasyon, "").Trim()))
        If o Is Nothing OrElse IsDBNull(o) Then Return Buyuk(lokasyon)
        Return Convert.ToString(o)
    End Function

    ''' <summary>Bölümün ana lokasyonu. Tanımlı değilse bölüm adı.</summary>
    Public Shared Function AnaLokasyon(bolum As String) As String
        If String.IsNullOrWhiteSpace(bolum) Then Return ""
        Dim o As Object = Db.Skaler("SELECT Ad FROM Lokasyon WHERE KATLA(Bolum) = ? AND AnaMi = 1 LIMIT 1", TrMetin.Katla(bolum.Trim()))
        If o Is Nothing OrElse IsDBNull(o) Then Return Buyuk(bolum)
        Return Convert.ToString(o)
    End Function

    ''' <summary>Tüm lokasyon tanımlarını değiştirir. Hata varsa mesaj döner (hiçbir şey yazılmaz), başarılıysa Nothing.</summary>
    Public Shared Function Kaydet(liste As IEnumerable(Of LokasyonKaydi)) As String
        Dim temiz As New List(Of LokasyonKaydi)()
        Dim adlar As New HashSet(Of String)()
        For Each x As LokasyonKaydi In liste
            Dim ad As String = Buyuk(x.Ad)
            Dim bolum As String = Buyuk(x.Bolum)
            If ad.Length = 0 AndAlso bolum.Length = 0 Then Continue For
            If ad.Length = 0 Then Return "Bölümü yazılmış ama adı boş bir lokasyon var."
            If bolum.Length = 0 Then Return ad & " lokasyonunun bölümü boş olamaz."
            If Not adlar.Add(TrMetin.Katla(ad)) Then Return ad & " lokasyonu birden fazla kez yazılmış."
            Dim k As New LokasyonKaydi()
            k.Ad = ad
            k.Bolum = bolum
            k.AnaMi = x.AnaMi
            temiz.Add(k)
        Next
        Dim bolumler As New Dictionary(Of String, Integer)()
        For Each k As LokasyonKaydi In temiz
            Dim an As String = TrMetin.Katla(k.Bolum)
            If Not bolumler.ContainsKey(an) Then bolumler(an) = 0
            If k.AnaMi Then bolumler(an) += 1
        Next
        For Each k As LokasyonKaydi In temiz
            Dim an As String = TrMetin.Katla(k.Bolum)
            If bolumler(an) > 1 Then Return k.Bolum & " bölümünde birden fazla ana lokasyon var. Bölüm başına yalnızca bir ana lokasyon olabilir."
            If bolumler(an) = 0 Then Return k.Bolum & " bölümünün ana lokasyonu yok. Hiçbir lokasyon bağımsız kalamaz; her bölümün bir ana lokasyonu olmalı."
        Next
        Using c As SQLiteConnection = Db.Ac()
            Using tx As SQLiteTransaction = c.BeginTransaction()
                Using cmd As SQLiteCommand = Db.Komut(c, tx, "DELETE FROM Lokasyon")
                    cmd.ExecuteNonQuery()
                End Using
                For Each k As LokasyonKaydi In temiz
                    Using cmd As SQLiteCommand = Db.Komut(c, tx, "INSERT INTO Lokasyon (Ad, Bolum, AnaMi) VALUES (?,?,?)", k.Ad, k.Bolum, If(k.AnaMi, 1, 0))
                        cmd.ExecuteNonQuery()
                    End Using
                Next
                tx.Commit()
            End Using
        End Using
        Return Nothing
    End Function
End Class
