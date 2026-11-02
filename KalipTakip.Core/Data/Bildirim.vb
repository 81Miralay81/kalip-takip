Imports System.Collections.Generic
Imports System.Data
Imports System.Data.SQLite

''' <summary>Anahtar / değer ayarları (Telegram bot anahtarı, e-posta sunucusu vb.).</summary>
Public NotInheritable Class AyarRepository
    Private Sub New()
    End Sub

    Public Shared Function Al(anahtar As String, Optional varsayilan As String = "") As String
        Try
            Dim o As Object = Db.Skaler("SELECT Deger FROM Ayar WHERE Anahtar = ?", anahtar)
            If o Is Nothing OrElse IsDBNull(o) Then Return varsayilan
            Return Convert.ToString(o)
        Catch
            Return varsayilan
        End Try
    End Function

    Public Shared Sub Yaz(anahtar As String, deger As String)
        Using c As SQLiteConnection = Db.Ac()
            Using cmd As SQLiteCommand = Db.Komut(c, Nothing, "INSERT INTO Ayar (Anahtar, Deger) VALUES (?, ?) ON CONFLICT(Anahtar) DO UPDATE SET Deger = excluded.Deger",
                                                  anahtar, If(deger, ""))
                cmd.ExecuteNonQuery()
            End Using
        End Using
    End Sub
End Class

''' <summary>Bildirim günlüğü ve kullanıcıların iletişim bilgileri (e-posta, Telegram kimliği, bildirim al).</summary>
Public NotInheritable Class BildirimRepository
    Private Sub New()
    End Sub

    Public Shared Sub LogYaz(kanal As String, alici As String, mesaj As String, durum As String, hata As String, talepId As Integer)
        Try
            Using c As SQLiteConnection = Db.Ac()
                Using cmd As SQLiteCommand = Db.Komut(c, Nothing,
                        "INSERT INTO BildirimLog (Tarih, Kanal, Alici, Mesaj, Durum, Hata, TalepId) VALUES (?,?,?,?,?,?,?)",
                        Db.Prm(DbType.DateTime, Date.Now), kanal, alici, mesaj, durum, If(String.IsNullOrEmpty(hata), Nothing, hata), talepId)
                    cmd.ExecuteNonQuery()
                End Using
            End Using
        Catch
        End Try
    End Sub

    Public Shared Function Gunluk(limit As Integer) As DataTable
        Return Db.Tablo("SELECT Id, Tarih, Kanal, Alici, Mesaj, Durum, Hata, TalepId FROM BildirimLog ORDER BY Id DESC LIMIT " & limit.ToString(Kultur.Inv))
    End Function

    ''' <summary>Tüm kullanıcıların iletişim bilgileri: Id, Email, TelegramId, BildirimAl.</summary>
    Public Shared Function Iletisimler() As DataTable
        Return Db.Tablo("SELECT Id, Email, TelegramId, BildirimAl FROM Kullanici")
    End Function

    Public Shared Sub IletisimYaz(kullaniciId As Integer, email As String, telegramId As String, bildirimAl As Boolean)
        Using c As SQLiteConnection = Db.Ac()
            Using cmd As SQLiteCommand = Db.Komut(c, Nothing, "UPDATE Kullanici SET Email = ?, TelegramId = ?, BildirimAl = ? WHERE Id = ?",
                                                  If(String.IsNullOrWhiteSpace(email), Nothing, email.Trim()),
                                                  If(String.IsNullOrWhiteSpace(telegramId), Nothing, telegramId.Trim()),
                                                  If(bildirimAl, 1, 0), kullaniciId)
                cmd.ExecuteNonQuery()
            End Using
        End Using
    End Sub

    ''' <summary>Telegram kimliği (chat id) ile eşleşen kullanıcının Id'si; yoksa 0.</summary>
    Public Shared Function KullaniciBulTelegram(telegramId As String) As Integer
        Dim o As Object = Db.Skaler("SELECT Id FROM Kullanici WHERE TelegramId = ? LIMIT 1", telegramId)
        If o Is Nothing OrElse IsDBNull(o) Then Return 0
        Return Convert.ToInt32(o)
    End Function
End Class
