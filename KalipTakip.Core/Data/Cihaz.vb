Imports System.Collections.Generic
Imports System.Data
Imports System.Data.SQLite
Imports System.Security.Cryptography
Imports System.Text

''' <summary>Cihaz kontrolünün sonucu.</summary>
Public Enum CihazSonuc
    Izinli = 0
    Bekliyor = 1
    Reddedildi = 2
    ''' <summary>Yeni cihaz: önce cihaza bir ad verilmesi gerekir (istek henüz açılmadı).</summary>
    AdGerekli = 3
End Enum

''' <summary>
''' Cihaz yetkilendirmesi: kullanıcı adı + parola doğru olsa bile yalnızca yöneticinin onayladığı cihazlar sisteme girebilir.
''' Cihaz, ilk girişte oluşturulan rastgele bir cihaz kodu (tarayıcıda saklanır; exe'de Windows makine kimliği) ile tanınır;
''' MAC, IP ve tarayıcı/cihaz bilgisi yöneticinin cihazı tanıması için kaydedilir. Veritabanında yalnızca kodun özeti (SHA-256) tutulur.
''' Kontrol kapalıyken (varsayılan) girişte kullanılan cihazlar otomatik "Onaylı" olarak kaydedilir; kontrol açıldığında onlar çalışmaya devam eder.
''' </summary>
Public NotInheritable Class CihazRepository
    Private Sub New()
    End Sub

    Public Const AyarKontrol As String = "cihaz_kontrol"
    Public Const Bekliyor As String = "Bekliyor"
    Public Const Onayli As String = "Onaylı"
    Public Const Reddedildi As String = "Reddedildi"

    Public Shared Function KontrolAcik() As Boolean
        Return AyarRepository.Al(AyarKontrol, "0") = "1"
    End Function

    Public Shared Sub KontrolYaz(acik As Boolean)
        AyarRepository.Yaz(AyarKontrol, If(acik, "1", "0"))
    End Sub

    Public Shared Function KodOzeti(kod As String) As String
        Using sha As SHA256 = SHA256.Create()
            Dim b As Byte() = sha.ComputeHash(Encoding.UTF8.GetBytes("kt-cihaz|" & If(kod, "")))
            Dim sb As New StringBuilder(b.Length * 2)
            For Each x As Byte In b
                sb.Append(x.ToString("x2"))
            Next
            Return sb.ToString()
        End Using
    End Function

    Private Shared Function Kisalt(s As String, uzunluk As Integer) As String
        Dim t As String = If(s, "").Trim()
        If t.Length > uzunluk Then t = t.Substring(0, uzunluk)
        Return t
    End Function

    Public Shared Function Muaf(kullaniciId As Integer) As Boolean
        Dim o As Object = Db.Skaler("SELECT CihazMuaf FROM Kullanici WHERE Id = ?", kullaniciId)
        Return o IsNot Nothing AndAlso Not IsDBNull(o) AndAlso Convert.ToInt32(o) = 1
    End Function

    Public Shared Sub MuafYaz(kullaniciId As Integer, deger As Boolean)
        Db.Calistir("UPDATE Kullanici SET CihazMuaf = ? WHERE Id = ?", If(deger, 1, 0), kullaniciId)
    End Sub

    ''' <summary>
    ''' Girişte çağrılır (kullanıcı adı ve parola doğrulandıktan sonra). Izinli değilse mesaj kullanıcıya gösterilir.
    ''' yeniIstek = bu çağrıyla yeni bir onay isteği oluştu (yöneticiye bildirim için).
    ''' </summary>
    Public Shared Function Kontrol(kullaniciId As Integer, kod As String, cihazAdi As String, mac As String, ip As String,
                                   bilgi As String, tur As String, ByRef mesaj As String, ByRef yeniIstek As Boolean) As CihazSonuc
        mesaj = ""
        yeniIstek = False
        If String.IsNullOrWhiteSpace(kod) OrElse kod.Trim().Length < 16 Then
            mesaj = "Cihaz kodu alınamadı. Tarayıcıyı güncelleyip yeniden deneyin (gizli sekme kullanmayın)."
            Return CihazSonuc.Reddedildi
        End If
        Dim acik As Boolean = KontrolAcik()
        If acik AndAlso Muaf(kullaniciId) Then Return CihazSonuc.Izinli
        Dim hash As String = KodOzeti(kod)
        Dim simdi As Date = Date.Now
        Dim var As DataTable = Db.Tablo("SELECT Id, Durum FROM CihazIzin WHERE KullaniciId = ? AND KodHash = ?", kullaniciId, hash)
        If var.Rows.Count = 0 Then
            If String.IsNullOrWhiteSpace(cihazAdi) AndAlso tur <> "Exe" Then
                mesaj = "Bu cihaz ilk kez kullanılıyor. Yöneticinin tanıması için 'Cihaz adı' kutusuna cihazı tanımlayan bir ad yazın (örn. Ali - telefon) ve tekrar giriş yapın."
                Return CihazSonuc.AdGerekli
            End If
            Dim durum As String = If(acik, Bekliyor, Onayli)
            Db.Calistir("INSERT INTO CihazIzin (KullaniciId, KodHash, Ad, Mac, Ip, Bilgi, Tur, Durum, IlkIstek, SonGiris, KararTarihi, KararVeren, GirisSayisi) " &
                        "VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?)",
                        kullaniciId, hash, Kisalt(cihazAdi, 80), Kisalt(mac, 40), Kisalt(ip, 60), Kisalt(bilgi, 600), Kisalt(tur, 20), durum,
                        Db.Prm(DbType.DateTime, simdi), Db.Prm(DbType.DateTime, simdi),
                        If(acik, Nothing, Db.Prm(DbType.DateTime, simdi)), If(acik, Nothing, "otomatik (kontrol kapalıyken)"), If(acik, 0, 1))
            If acik Then
                yeniIstek = True
                mesaj = "Bu cihaz ilk kez kullanılıyor. Erişim isteğiniz yöneticiye iletildi; onaylanınca giriş yapabilirsiniz."
                Return CihazSonuc.Bekliyor
            End If
            Return CihazSonuc.Izinli
        End If
        Dim id As Integer = Convert.ToInt32(var.Rows(0)("Id"))
        Dim d As String = Convert.ToString(var.Rows(0)("Durum"))
        If Not acik Then
            ' Kontrol kapalı: bilgiler güncellenir; reddedilmiş/bekleyen cihazlar da kapalıyken engellenmez.
            Db.Calistir("UPDATE CihazIzin SET Mac = ?, Ip = ?, Bilgi = ?, Ad = CASE WHEN ? <> '' THEN ? ELSE Ad END, SonGiris = ?, GirisSayisi = GirisSayisi + 1 WHERE Id = ?",
                        Kisalt(mac, 40), Kisalt(ip, 60), Kisalt(bilgi, 600), Kisalt(cihazAdi, 80), Kisalt(cihazAdi, 80), Db.Prm(DbType.DateTime, simdi), id)
            Return CihazSonuc.Izinli
        End If
        If d = Onayli Then
            Db.Calistir("UPDATE CihazIzin SET Mac = ?, Ip = ?, Bilgi = ?, Ad = CASE WHEN ? <> '' THEN ? ELSE Ad END, SonGiris = ?, GirisSayisi = GirisSayisi + 1 WHERE Id = ?",
                        Kisalt(mac, 40), Kisalt(ip, 60), Kisalt(bilgi, 600), Kisalt(cihazAdi, 80), Kisalt(cihazAdi, 80), Db.Prm(DbType.DateTime, simdi), id)
            Return CihazSonuc.Izinli
        End If
        ' bekleyen / reddedilen: bilgiler güncellenir (yönetici güncel IP/MAC'i görsün)
        Db.Calistir("UPDATE CihazIzin SET Mac = ?, Ip = ?, Bilgi = ?, SonGiris = ?, Ad = CASE WHEN ? <> '' THEN ? ELSE Ad END WHERE Id = ?",
                    Kisalt(mac, 40), Kisalt(ip, 60), Kisalt(bilgi, 600), Db.Prm(DbType.DateTime, simdi), Kisalt(cihazAdi, 80), Kisalt(cihazAdi, 80), id)
        If d = Reddedildi Then
            mesaj = "Bu cihaza erişim yetkisi verilmedi. Gerekirse yöneticiyle görüşün."
            Return CihazSonuc.Reddedildi
        End If
        mesaj = "Bu cihaz için erişim isteği yönetici onayı bekliyor. Onaylanınca giriş yapabilirsiniz."
        Return CihazSonuc.Bekliyor
    End Function

    ''' <summary>Cihaz listesi. durum boşsa hepsi; kullaniciId 0 ise tüm kullanıcılar.</summary>
    Public Shared Function Liste(durum As String, kullaniciId As Integer) As DataTable
        Dim sql As New StringBuilder(
            "SELECT c.Id AS Id, c.KullaniciId AS KullaniciId, u.KullaniciAdi AS Kullanici, c.Ad AS Ad, c.Tur AS Tur, c.Mac AS Mac, c.Ip AS Ip, " &
            "c.Bilgi AS Bilgi, c.Durum AS Durum, c.IlkIstek AS IlkIstek, c.SonGiris AS SonGiris, c.KararTarihi AS KararTarihi, " &
            "c.KararVeren AS KararVeren, c.GirisSayisi AS GirisSayisi FROM CihazIzin c JOIN Kullanici u ON u.Id = c.KullaniciId WHERE 1 = 1")
        Dim p As New List(Of Object)()
        If Not String.IsNullOrEmpty(durum) Then
            sql.Append(" AND c.Durum = ?")
            p.Add(durum)
        End If
        If kullaniciId > 0 Then
            sql.Append(" AND c.KullaniciId = ?")
            p.Add(kullaniciId)
        End If
        sql.Append(" ORDER BY CASE c.Durum WHEN 'Bekliyor' THEN 0 WHEN 'Onaylı' THEN 1 ELSE 2 END, c.SonGiris DESC")
        Return Db.Tablo(sql.ToString(), p.ToArray())
    End Function

    Public Shared Function BekleyenSayisi() As Integer
        Try
            Return Convert.ToInt32(Db.Skaler("SELECT COUNT(*) FROM CihazIzin WHERE Durum = 'Bekliyor'"))
        Catch
            Return 0
        End Try
    End Function

    ''' <summary>Onay / ret kararı. Cihazın kullanıcı id'si ve kod özeti döner (oturumları kapatmak için).</summary>
    Public Shared Function Karar(id As Integer, durum As String, yapan As String, ByRef kullaniciId As Integer, ByRef kodHash As String) As Boolean
        If durum <> Onayli AndAlso durum <> Reddedildi AndAlso durum <> Bekliyor Then Throw New ArgumentException("Geçersiz durum.")
        Dim t As DataTable = Db.Tablo("SELECT KullaniciId, KodHash FROM CihazIzin WHERE Id = ?", id)
        If t.Rows.Count = 0 Then Return False
        kullaniciId = Convert.ToInt32(t.Rows(0)("KullaniciId"))
        kodHash = Convert.ToString(t.Rows(0)("KodHash"))
        Db.Calistir("UPDATE CihazIzin SET Durum = ?, KararTarihi = ?, KararVeren = ? WHERE Id = ?",
                    durum, Db.Prm(DbType.DateTime, Date.Now), If(yapan, ""), id)
        Return True
    End Function

    Public Shared Function Sil(id As Integer, ByRef kullaniciId As Integer, ByRef kodHash As String) As Boolean
        Dim t As DataTable = Db.Tablo("SELECT KullaniciId, KodHash FROM CihazIzin WHERE Id = ?", id)
        If t.Rows.Count = 0 Then Return False
        kullaniciId = Convert.ToInt32(t.Rows(0)("KullaniciId"))
        kodHash = Convert.ToString(t.Rows(0)("KodHash"))
        Return Db.Calistir("DELETE FROM CihazIzin WHERE Id = ?", id) = 1
    End Function

    ''' <summary>Yeni cihaz isteği bildirimini alacak yöneticiler (aktif, bildirim al işaretli).</summary>
    Public Shared Function YoneticiAlicilari() As DataTable
        Return Db.Tablo("SELECT Id, KullaniciAdi, Email, TelegramId FROM Kullanici WHERE Aktif = 1 AND IsAdmin = 1 AND BildirimAl = 1")
    End Function

    ''' <summary>Muaf işaretleme ekranı için: tüm kullanıcılar ve muaf durumları.</summary>
    Public Shared Function KullaniciMuafListesi() As DataTable
        Return Db.Tablo("SELECT Id, KullaniciAdi, IsAdmin, COALESCE(CihazMuaf, 0) AS Muaf FROM Kullanici ORDER BY KullaniciAdi")
    End Function
End Class
