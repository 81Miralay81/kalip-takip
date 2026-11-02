Imports System.Collections.Generic
Imports System.Data
Imports System.Data.SQLite
Imports System.Globalization
Imports System.Linq

Public Enum GirisSonucu
    Basarili
    KullaniciYok
    ParolaYanlis
    Pasif
End Enum

Public NotInheritable Class KullaniciRepository
    Private Sub New()
    End Sub

    ''' <summary>Kullanıcı adı + parola doğrularsa kullanıcıyı döner, aksi halde Nothing.</summary>
    Public Shared Function Giris(ad As String, parola As String) As Kullanici
        If String.IsNullOrWhiteSpace(ad) OrElse parola Is Nothing Then Return Nothing
        Dim dt As DataTable = Db.Tablo("SELECT Id, KullaniciAdi, ParolaHash, IsAdmin, Aktif, Bolum FROM Kullanici")
        For Each r As DataRow In dt.Rows
            If String.Compare(Convert.ToString(r("KullaniciAdi")), ad.Trim(), Kultur.Tr, CompareOptions.IgnoreCase) = 0 Then
                If Not Convert.ToBoolean(r("Aktif")) Then Return Nothing
                If Not Guvenlik.Dogrula(parola, Convert.ToString(r("ParolaHash"))) Then Return Nothing
                Return Olustur(r)
            End If
        Next
        Return Nothing
    End Function

    ''' <summary>Giriş denemesi; nedenini de bildirir (kullanıcı yok / parola yanlış / pasif).</summary>
    Public Shared Function GirisDene(ad As String, parola As String, ByRef kullanici As Kullanici) As GirisSonucu
        kullanici = Nothing
        Dim dt As DataTable = Db.Tablo("SELECT Id, KullaniciAdi, ParolaHash, IsAdmin, Aktif, Bolum FROM Kullanici")
        For Each r As DataRow In dt.Rows
            If String.Compare(Convert.ToString(r("KullaniciAdi")), If(ad, "").Trim(), Kultur.Tr, CompareOptions.IgnoreCase) = 0 Then
                If Not Guvenlik.Dogrula(If(parola, ""), Convert.ToString(r("ParolaHash"))) Then Return GirisSonucu.ParolaYanlis
                If Not Convert.ToBoolean(r("Aktif")) Then Return GirisSonucu.Pasif
                kullanici = Olustur(r)
                Return GirisSonucu.Basarili
            End If
        Next
        Return GirisSonucu.KullaniciYok
    End Function

    Public Shared Function Getir(id As Integer) As Kullanici
        Dim dt As DataTable = Db.Tablo("SELECT Id, KullaniciAdi, ParolaHash, IsAdmin, Aktif, Bolum FROM Kullanici WHERE Id = ?", id)
        If dt.Rows.Count = 0 Then Return Nothing
        Return Olustur(dt.Rows(0))
    End Function

    Private Shared Function Olustur(r As DataRow) As Kullanici
        Dim k As New Kullanici()
        k.Id = Convert.ToInt32(r("Id"))
        k.KullaniciAdi = Convert.ToString(r("KullaniciAdi"))
        k.IsAdmin = Convert.ToBoolean(r("IsAdmin"))
        k.Aktif = Convert.ToBoolean(r("Aktif"))
        k.Bolum = If(IsDBNull(r("Bolum")), Nothing, Convert.ToString(r("Bolum")))
        Try
            For Each x As DataRow In Db.Tablo("SELECT Bolum FROM KullaniciBolum WHERE KullaniciId = ? ORDER BY rowid", k.Id).Rows
                k.Bolumler.Add(Convert.ToString(x("Bolum")))
            Next
            Dim tb As Object = Db.Skaler("SELECT TumBolum FROM Kullanici WHERE Id = ?", k.Id)
            k.TumBolumler = tb Is Nothing OrElse IsDBNull(tb) OrElse Convert.ToInt32(tb) <> 0
        Catch
            k.TumBolumler = True
        End Try
        If k.Bolumler.Count = 0 AndAlso Not String.IsNullOrWhiteSpace(k.Bolum) Then k.Bolumler.Add(k.Bolum)
        Dim iz As DataTable = Db.Tablo("SELECT Anahtar FROM Yetki WHERE KullaniciId = ?", k.Id)
        For Each x As DataRow In iz.Rows
            k.Izinler.Add(Convert.ToString(x("Anahtar")))
        Next
        Return k
    End Function

    Public Shared Function Listele() As DataTable
        Return Db.Tablo("SELECT Id, KullaniciAdi, IsAdmin, Aktif FROM Kullanici ORDER BY KullaniciAdi")
    End Function

    ''' <summary>Aktif yönetici sayısı (son yöneticinin silinmesini/pasifleştirilmesini engellemek için).</summary>
    Public Shared Function AktifYoneticiSayisi() As Integer
        Return Convert.ToInt32(Db.Skaler("SELECT COUNT(*) FROM Kullanici WHERE IsAdmin = ? AND Aktif = ?", True, True))
    End Function

    ''' <summary>
    ''' Kullanıcıyı ve yetkilerini tek işlemde kaydeder. Id = 0 ise yeni kullanıcıdır (parola zorunlu).
    ''' Düzenlemede parola boş bırakılırsa değişmez. Kullanıcı adı çakışırsa SQLiteException fırlatır.
    ''' </summary>
    Public Shared Function Kaydet(k As Kullanici, yeniParola As String) As Integer
        Using c As SQLiteConnection = Db.Ac()
            Using tx As SQLiteTransaction = c.BeginTransaction()
                Dim id As Integer = k.Id
                If id = 0 Then
                    If String.IsNullOrEmpty(yeniParola) Then Throw New ArgumentException("Yeni kullanıcı için parola gerekli.")
                    Using cmd As SQLiteCommand = Db.Komut(c, tx,
                            "INSERT INTO Kullanici (KullaniciAdi, ParolaHash, IsAdmin, Aktif, Bolum) VALUES (?,?,?,?,?)",
                            k.KullaniciAdi, Guvenlik.Hashle(yeniParola), k.IsAdmin, k.Aktif, BosDeger(k.Bolum))
                        cmd.ExecuteNonQuery()
                    End Using
                    Using cmdId As New SQLiteCommand("SELECT last_insert_rowid()", c, tx)
                        id = Convert.ToInt32(cmdId.ExecuteScalar())
                    End Using
                ElseIf String.IsNullOrEmpty(yeniParola) Then
                    Using cmd As SQLiteCommand = Db.Komut(c, tx,
                            "UPDATE Kullanici SET KullaniciAdi=?, IsAdmin=?, Aktif=?, Bolum=? WHERE Id=?",
                            k.KullaniciAdi, k.IsAdmin, k.Aktif, BosDeger(k.Bolum), id)
                        cmd.ExecuteNonQuery()
                    End Using
                Else
                    Using cmd As SQLiteCommand = Db.Komut(c, tx,
                            "UPDATE Kullanici SET KullaniciAdi=?, ParolaHash=?, IsAdmin=?, Aktif=?, Bolum=? WHERE Id=?",
                            k.KullaniciAdi, Guvenlik.Hashle(yeniParola), k.IsAdmin, k.Aktif, BosDeger(k.Bolum), id)
                        cmd.ExecuteNonQuery()
                    End Using
                End If

                Using cmdSil As SQLiteCommand = Db.Komut(c, tx, "DELETE FROM Yetki WHERE KullaniciId = ?", id)
                    cmdSil.ExecuteNonQuery()
                End Using
                For Each anahtar As String In k.Izinler
                    Using cmdEkle As SQLiteCommand = Db.Komut(c, tx, "INSERT INTO Yetki (KullaniciId, Anahtar) VALUES (?, ?)", id, anahtar)
                        cmdEkle.ExecuteNonQuery()
                    End Using
                Next
                If k.BolumlerAyarli Then
                    Using cmdB As SQLiteCommand = Db.Komut(c, tx, "DELETE FROM KullaniciBolum WHERE KullaniciId = ?", id)
                        cmdB.ExecuteNonQuery()
                    End Using
                    For Each b As String In k.Bolumler
                        If String.IsNullOrWhiteSpace(b) Then Continue For
                        Using cmdB2 As SQLiteCommand = Db.Komut(c, tx, "INSERT OR IGNORE INTO KullaniciBolum (KullaniciId, Bolum) VALUES (?, ?)", id, b.Trim().ToUpper(Kultur.Tr))
                            cmdB2.ExecuteNonQuery()
                        End Using
                    Next
                    Using cmdT As SQLiteCommand = Db.Komut(c, tx, "UPDATE Kullanici SET TumBolum = ? WHERE Id = ?", If(k.TumBolumler, 1, 0), id)
                        cmdT.ExecuteNonQuery()
                    End Using
                End If
                tx.Commit()
                Return id
            End Using
        End Using
    End Function

    Private Shared Function BosDeger(s As String) As Object
        If String.IsNullOrWhiteSpace(s) Then Return DBNull.Value
        Return s.Trim().ToUpper(Kultur.Tr)
    End Function

    Public Shared Sub Sil(id As Integer)
        Using c As SQLiteConnection = Db.Ac()
            Using tx As SQLiteTransaction = c.BeginTransaction()
                Using cmd1 As SQLiteCommand = Db.Komut(c, tx, "DELETE FROM Yetki WHERE KullaniciId = ?", id)
                    cmd1.ExecuteNonQuery()
                End Using
                Using cmd1b As SQLiteCommand = Db.Komut(c, tx, "DELETE FROM KullaniciBolum WHERE KullaniciId = ?", id)
                    cmd1b.ExecuteNonQuery()
                End Using
                Using cmd2 As SQLiteCommand = Db.Komut(c, tx, "DELETE FROM Kullanici WHERE Id = ?", id)
                    cmd2.ExecuteNonQuery()
                End Using
                tx.Commit()
            End Using
        End Using
    End Sub
End Class

''' <summary>Açılır kutuların değerleri (Tür, Kalıp Türü).</summary>
Public NotInheritable Class ListeRepository
    Private Sub New()
    End Sub

    Public Const GrupTur As String = "Tur"
    Public Const GrupKalipTuru As String = "KalipTuru"

    Public Shared Function Getir(grup As String) As List(Of String)
        Dim dt As DataTable = Db.Tablo("SELECT Deger FROM Liste WHERE Grup = ? ORDER BY Sira, Id", grup)
        Return dt.Rows.Cast(Of DataRow)().Select(Function(r) Convert.ToString(r("Deger"))).ToList()
    End Function
End Class
