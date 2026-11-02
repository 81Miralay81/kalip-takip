Imports System.Collections.Generic
Imports System.Data
Imports System.Data.OleDb
Imports System.Data.SQLite
Imports System.IO

''' <summary>
''' Eski Access (.accdb) veritabanındaki verileri (Kalip, Kullanici, Yetki, Liste) yeni SQLite dosyasına TEK SEFERLİK aktarır.
''' Id'ler ve parola özetleri aynen korunur. Hepsi tek işlemde yazılır; sayılar tutmazsa hiçbir şey kaydedilmez.
''' Access sağlayıcısı (ACE) yalnızca bu aktarım sırasında, eski dosyayı okumak için gerekir.
''' </summary>
Public NotInheritable Class AccessAktarici
    Private Sub New()
    End Sub

    ''' <summary>Bu bilgisayarda kayıtlı ACE sağlayıcısı (yeniden eskiye); yoksa Nothing.</summary>
    Public Shared Function SaglayiciBul() As String
        For Each a As String In New String() {"Microsoft.ACE.OLEDB.16.0", "Microsoft.ACE.OLEDB.12.0"}
            Try
                If Type.GetTypeFromProgID(a, False) IsNot Nothing Then Return a
            Catch
            End Try
        Next
        Return Nothing
    End Function

    ' Kaynak SELECT'leri: kolon adları yeni tablolarla birebir aynıdır.
    Private Const KalipSelect As String =
        "SELECT Id, ParcaKodu, KalipTuru, TepeKodu, ParcaAdi, GelisTarihi, TashihSayi, BitisTarihi, Hafta, Tur, " &
        "DovmeAdeti, CncNo, Aciklama, IslemSuresi, Ayar, Bakim, ElemanEksikligi, Elektrik, KalipAriza, Takim, Ariza, ToplamZaman, " &
        "IIf(IsNull(Surum), 1, Surum) AS Surum, SonDegisiklik, SonKullanici FROM Kalip ORDER BY Id"

    Private Shared ReadOnly Tablolar As String(,) = {
        {"Kullanici", "SELECT Id, KullaniciAdi, ParolaHash, IsAdmin, Aktif FROM Kullanici ORDER BY Id"},
        {"Yetki", "SELECT KullaniciId, Anahtar FROM Yetki"},
        {"Liste", "SELECT Id, Grup, Deger, Sira FROM Liste ORDER BY Id"},
        {"Kalip", KalipSelect}
    }

    Public Shared Function Calistir(accdbYol As String, ilerleme As IProgress(Of String)) As AktarimSonucu
        If Not File.Exists(accdbYol) Then Throw New FileNotFoundException("Access dosyası bulunamadı: " & accdbYol)

        Dim saglayici As String = SaglayiciBul()
        If saglayici Is Nothing Then
            Throw New InvalidOperationException(
                "Eski Access dosyasını okumak için 'Microsoft Access Database Engine (ACE)' gerekli ve bu bilgisayarda yok." & vbCrLf &
                "Microsoft'un ücretsiz 'Access Database Engine 2016 Redistributable' paketini kurun (programla aynı bit sayısında: 32 ya da 64)." & vbCrLf &
                "Bu yalnızca bu tek seferlik aktarım için gerekir; sonrasında programın Access'e ihtiyacı kalmaz.")
        End If

        Rapor(ilerleme, "Yeni veritabanı hazırlanıyor...")
        Dim durum As String = Nothing
        If Db.DurumBul(durum) = VeritabaniDurumu.Hazir Then
            Throw New InvalidOperationException("Yeni veritabanı zaten kurulu; aktarım yalnızca boş veritabanına yapılır.")
        End If
        Dim dosyaOlustu As Boolean = Sema.VeritabaniOlustur()

        Dim sonuc As New AktarimSonucu()
        Try
            Sema.TablolariKur()
            Dim cs As String = "Provider=" & saglayici & ";Data Source=" & accdbYol & ";Mode=Read;Persist Security Info=False;"
            Using kaynak As New OleDbConnection(cs)
                kaynak.Open()
                Using hedef As SQLiteConnection = Db.Ac()
                    Using tx As SQLiteTransaction = hedef.BeginTransaction()
                        For i As Integer = 0 To Tablolar.GetLength(0) - 1
                            Dim ad As String = Tablolar(i, 0)
                            Rapor(ilerleme, ad & " aktarılıyor...")
                            Dim okunan As Integer = Kopyala(kaynak, hedef, tx, ad, Tablolar(i, 1))
                            If ad = "Kalip" Then sonuc.KayitSayisi = okunan
                            If ad = "Kullanici" Then sonuc.KullaniciSayisi = okunan
                        Next
                        If sonuc.KullaniciSayisi = 0 Then Throw New InvalidDataException("Access dosyasında kullanıcı bulunamadı; doğru dosyayı seçtiğinizden emin olun.")
                        tx.Commit()
                    End Using
                End Using
            End Using
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

    ''' <summary>Bir tabloyu Access'ten SQLite'a kopyalar; sayıları karşılaştırır, tutmazsa istisna fırlatır.</summary>
    Private Shared Function Kopyala(kaynak As OleDbConnection, hedef As SQLiteConnection, tx As SQLiteTransaction,
                                    tablo As String, secim As String) As Integer
        Dim beklenen As Integer
        Using sayCmd As New OleDbCommand("SELECT COUNT(*) FROM " & tablo, kaynak)
            beklenen = Convert.ToInt32(sayCmd.ExecuteScalar())
        End Using

        Dim yazilan As Integer
        Using cmd As New OleDbCommand(secim, kaynak)
            cmd.CommandTimeout = 300
            Using rd As OleDbDataReader = cmd.ExecuteReader()
                yazilan = TopluYaz.Kopyala(hedef, tx, tablo, rd)
            End Using
        End Using

        If yazilan <> beklenen Then
            Throw New InvalidDataException(tablo & " tablosunda sayı tutmadı: Access'te " & beklenen.ToString(Kultur.Inv) &
                                           ", yeni veritabanında " & yazilan.ToString(Kultur.Inv) & ". Hiçbir veri kaydedilmedi.")
        End If
        Return yazilan
    End Function
End Class
