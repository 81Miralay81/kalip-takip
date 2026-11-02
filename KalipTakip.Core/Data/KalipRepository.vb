Imports System.Collections.Generic
Imports System.Data
Imports System.Data.SQLite
Imports System.Globalization
Imports System.Linq
Imports System.Text

Public NotInheritable Class KalipRepository
    Private Sub New()
    End Sub

    ''' <summary>Listede görünen kolonlar (sıra = ekrandaki sıra).</summary>
    Public Shared ReadOnly Kolonlar As KolonTanimi() = {
        New KolonTanimi("Id", "ID", KolonTipi.Tam, 70),
        New KolonTanimi("ParcaKodu", "Parça Kodu", KolonTipi.Metin, 120),
        New KolonTanimi("KalipTuru", "Kalıp Türü", KolonTipi.Metin, 200),
        New KolonTanimi("TepeKodu", "Teklif Referans", KolonTipi.Metin, 130),
        New KolonTanimi("ParcaAdi", "Parçanın Adı", KolonTipi.Metin, 230),
        New KolonTanimi("GelisTarihi", "Geliş Tarihi", KolonTipi.Tarih, 100),
        New KolonTanimi("TashihSayi", "Tashih", KolonTipi.Tam, 65),
        New KolonTanimi("BitisTarihi", "Bitiş Tarihi", KolonTipi.Tarih, 100),
        New KolonTanimi("Hafta", "Hafta", KolonTipi.Metin, 75),
        New KolonTanimi("Tur", "Tasarım Modeli", KolonTipi.Metin, 85),
        New KolonTanimi("DovmeAdeti", "Dövme Adeti", KolonTipi.Ondalik, 100),
        New KolonTanimi("CncNo", "CNC No", KolonTipi.Tam, 70),
        New KolonTanimi("Aciklama", "Açıklama", KolonTipi.Metin, 220),
        New KolonTanimi("IslemSuresi", "İşlem Süresi", KolonTipi.Ondalik, 100),
        New KolonTanimi("Ayar", "Ayar", KolonTipi.Ondalik, 70),
        New KolonTanimi("Bakim", "Bakım", KolonTipi.Ondalik, 70),
        New KolonTanimi("ElemanEksikligi", "Eleman Eksikliği", KolonTipi.Ondalik, 120),
        New KolonTanimi("Elektrik", "Elektrik", KolonTipi.Ondalik, 75),
        New KolonTanimi("KalipAriza", "Kalıp Arıza", KolonTipi.Ondalik, 95),
        New KolonTanimi("Takim", "Takım", KolonTipi.Ondalik, 70),
        New KolonTanimi("Ariza", "Arıza", KolonTipi.Ondalik, 70),
        New KolonTanimi("ToplamZaman", "Toplam Zaman", KolonTipi.Ondalik, 110)
    }

    ''' <summary>Arama kutusundaki alan seçenekleri (Key = ekranda, Value = kolon adı; boş = hepsi).</summary>
    Public Shared ReadOnly AramaAlanlari As KeyValuePair(Of String, String)() = {
        New KeyValuePair(Of String, String)("Tümü", ""),
        New KeyValuePair(Of String, String)("Parça Kodu", "ParcaKodu"),
        New KeyValuePair(Of String, String)("Teklif Referans", "TepeKodu"),
        New KeyValuePair(Of String, String)("Parçanın Adı", "ParcaAdi"),
        New KeyValuePair(Of String, String)("Kalıp Türü", "KalipTuru"),
        New KeyValuePair(Of String, String)("Kalıp Tasarım Modeli", "Tur"),
        New KeyValuePair(Of String, String)("Geliş Tarihi (gg.aa.yyyy)", "GelisTarihi"),
        New KeyValuePair(Of String, String)("Bitiş Tarihi (gg.aa.yyyy)", "BitisTarihi"),
        New KeyValuePair(Of String, String)("Hafta", "Hafta"),
        New KeyValuePair(Of String, String)("CNC No", "CncNo"),
        New KeyValuePair(Of String, String)("Açıklama", "Aciklama")
    }

    Private Shared ReadOnly SecimListesi As String =
        String.Join(",", Kolonlar.Select(Function(k) k.Alan)) & ",Surum,SonDegisiklik,SonKullanici"

    ' Yazılan (kullanıcının girdiği) 21 alan; Degerler() ile aynı sırada olmalı.
    Private Shared ReadOnly YazilanAlanlar As String() = {
        "ParcaKodu", "KalipTuru", "TepeKodu", "ParcaAdi", "GelisTarihi", "TashihSayi", "BitisTarihi", "Hafta", "Tur",
        "DovmeAdeti", "CncNo", "Aciklama", "IslemSuresi", "Ayar", "Bakim", "ElemanEksikligi", "Elektrik",
        "KalipAriza", "Takim", "Ariza", "ToplamZaman"
    }

    ' ---------------------------------------------------------------- ARAMA

    ''' <summary>limit &gt; 0 ise en yeni 'limit' kayıt getirilir (Id'ye göre); 0 = hepsi.</summary>
    Public Shared Function Ara(alan As String, metin As String, Optional limit As Integer = 0, Optional bolum As String = "") As DataTable
        Dim sql As New StringBuilder("SELECT " & SecimListesi & " FROM Kalip")
        Dim p As New List(Of Object)()
        Dim kosullar As New List(Of String)()
        Dim aranan As String = If(metin, "").Trim()
        If aranan.Length > 0 Then kosullar.Add(Kosul(alan, aranan, p))
        Dim bk As String = BolumKosulu(bolum, p)
        If bk.Length > 0 Then kosullar.Add(bk)
        If kosullar.Count > 0 Then sql.Append(" WHERE ").Append(String.Join(" AND ", kosullar))
        sql.Append(" ORDER BY Id DESC")
        If limit > 0 Then sql.Append(" LIMIT ").Append(limit.ToString(Kultur.Inv))
        Return Db.Tablo(sql.ToString(), p.ToArray())
    End Function

    ''' <summary>Kalıbın sahibi (KalipLokasyon.SahipBolum) verilen bölümse eşleşir. Boş bölüm = süzme yok.</summary>
    Private Shared Function BolumKosulu(bolum As String, p As List(Of Object)) As String
        Dim b As String = TrMetin.Katla(If(bolum, "").Trim())
        If b.Length = 0 Then Return ""
        p.Add(b)
        Return "EXISTS (SELECT 1 FROM KalipLokasyon kl WHERE kl.Anahtar = KATLA(TRIM(Kalip.ParcaKodu)) AND KATLA(COALESCE(kl.SahipBolum,'')) = ?)"
    End Function

    Private Shared Function Kosul(alan As String, metin As String, p As List(Of Object)) As String
        Dim desen As String = AramaDeseni(TrMetin.Katla(metin))
        If String.IsNullOrEmpty(alan) Then
            For i As Integer = 1 To 5
                p.Add(desen)
            Next
            Return "(KATLA(ParcaKodu) LIKE ? ESCAPE '\' OR KATLA(TepeKodu) LIKE ? ESCAPE '\' OR KATLA(ParcaAdi) LIKE ? ESCAPE '\' " &
                   "OR KATLA(Aciklama) LIKE ? ESCAPE '\' OR KATLA(KalipTuru) LIKE ? ESCAPE '\')"
        End If
        Dim kd As KolonTanimi = Kolonlar.FirstOrDefault(Function(k) k.Alan = alan)
        If kd Is Nothing Then Throw New ArgumentException("Geçersiz arama alanı: " & alan)
        Select Case kd.Tip
            Case KolonTipi.Tarih
                p.Add("%" & LikeKacis(metin.Trim()) & "%")
                Return "strftime('%d.%m.%Y', " & alan & ") LIKE ? ESCAPE '\'"
            Case KolonTipi.Tam
                Dim n As Integer
                If Integer.TryParse(metin, NumberStyles.Integer, Kultur.Inv, n) Then
                    p.Add(n)
                    Return alan & " = ?"
                End If
                Return "1 = 0"
            Case Else
                p.Add(desen)
                Return "KATLA(" & alan & ") LIKE ? ESCAPE '\'"
        End Select
    End Function

    ''' <summary>
    ''' Arama metni yıldız (*) içermiyorsa "içerir" araması yapar. Yıldız varsa joker olur:
    ''' "1363*-1" = içinde 1363 geçen ve tam "-1" ile biten kodlar; sona yıldız konursa sonu serbest kalır.
    ''' </summary>
    Private Shared Function AramaDeseni(katlanmis As String) As String
        If katlanmis.IndexOf("*"c) < 0 Then Return "%" & LikeKacis(katlanmis) & "%"
        Dim parcalar As String() = katlanmis.Split("*"c)
        Dim sb As New System.Text.StringBuilder("%")
        For i As Integer = 0 To parcalar.Length - 1
            If i > 0 Then sb.Append("%")
            sb.Append(LikeKacis(parcalar(i)))
        Next
        Return sb.ToString()
    End Function

    Private Shared Function LikeKacis(s As String) As String
        Return s.Replace("\", "\\").Replace("%", "\%").Replace("_", "\_")
    End Function

    ''' <summary>Tek kaydı, liste ile aynı kolonlarla okur.</summary>
    Public Shared Function Satir(id As Integer) As DataTable
        Return Db.Tablo("SELECT " & SecimListesi & " FROM Kalip WHERE Id = ?", id)
    End Function

    ' ---------------------------------------------------------------- YAZMA

    Private Shared Function Bos(s As String) As Object
        If String.IsNullOrWhiteSpace(s) Then Return Nothing
        Return s.Trim()
    End Function

    Private Shared Function Degerler(k As Kalip) As List(Of Object)
        Dim m As DbType = DbType.String
        Dim d As DbType = DbType.Double
        Return New List(Of Object) From {
            Db.Prm(m, Bos(k.ParcaKodu)), Db.Prm(m, Bos(k.KalipTuru)), Db.Prm(m, Bos(k.TepeKodu)),
            Db.Prm(DbType.String, Bos(k.ParcaAdi)),
            Db.Prm(DbType.DateTime, k.GelisTarihi), Db.Prm(DbType.Int32, k.TashihSayi),
            Db.Prm(DbType.DateTime, k.BitisTarihi), Db.Prm(m, Bos(k.Hafta)), Db.Prm(m, Bos(k.Tur)),
            Db.Prm(d, k.DovmeAdeti), Db.Prm(DbType.Int32, k.CncNo), Db.Prm(m, Bos(k.Aciklama)),
            Db.Prm(d, k.IslemSuresi), Db.Prm(d, k.Ayar), Db.Prm(d, k.Bakim), Db.Prm(d, k.ElemanEksikligi),
            Db.Prm(d, k.Elektrik), Db.Prm(d, k.KalipAriza), Db.Prm(d, k.Takim), Db.Prm(d, k.Ariza),
            Db.Prm(d, k.ToplamZaman)
        }
    End Function

    ''' <summary>Yeni kayıt ekler, AutoNumber ID'yi döner.</summary>
    Public Shared Function Ekle(k As Kalip, kullanici As String) As Integer
        Dim alanlar As String = String.Join(",", YazilanAlanlar) & ",Surum,SonDegisiklik,SonKullanici"
        Dim yerTutucu As String = String.Join(",", Enumerable.Repeat("?", YazilanAlanlar.Length + 3))
        Dim p As List(Of Object) = Degerler(k)
        p.Add(Db.Prm(DbType.Int32, 1))
        p.Add(Db.Prm(DbType.DateTime, Date.Now))
        p.Add(Db.Prm(DbType.String, Bos(kullanici)))
        Return Db.EkleVeIdGetir("INSERT INTO Kalip (" & alanlar & ") VALUES (" & yerTutucu & ")", p.ToArray())
    End Function

    ''' <summary>
    ''' Kaydı günceller. Kayıt, okunduğundan beri başka biri tarafından değiştirilmiş ya da silinmişse
    ''' False döner (hiçbir şey yazılmaz).
    ''' </summary>
    Public Shared Function Guncelle(k As Kalip, kullanici As String) As Boolean
        Dim atamalar As String = String.Join(",", YazilanAlanlar.Select(Function(a) a & "=?")) & ",Surum=?,SonDegisiklik=?,SonKullanici=?"
        Dim p As List(Of Object) = Degerler(k)
        p.Add(Db.Prm(DbType.Int32, k.Surum + 1))
        p.Add(Db.Prm(DbType.DateTime, Date.Now))
        p.Add(Db.Prm(DbType.String, Bos(kullanici)))
        p.Add(Db.Prm(DbType.Int32, k.Id))
        p.Add(Db.Prm(DbType.Int32, k.Surum))
        Return Db.Calistir("UPDATE Kalip SET " & atamalar & " WHERE Id=? AND Surum=?", p.ToArray()) = 1
    End Function

    Public Shared Function Sil(id As Integer) As Boolean
        Return Db.Calistir("DELETE FROM Kalip WHERE Id=?", id) = 1
    End Function

    ' ---------------------------------------------------------------- RAPORLAR

    ''' <summary>Kalıp türü ve (YENİ/İLAVE/TASHİH) türüne göre adetler; isteğe bağlı bitiş tarihi aralığı.</summary>
    Public Shared Function Istatistik(baslangic As Date?, bitis As Date?, Optional bolum As String = "") As DataTable
        Dim sql As New StringBuilder("SELECT KalipTuru, Tur, COUNT(*) AS Adet FROM Kalip")
        Dim p As New List(Of Object)()
        Dim kosullar As New List(Of String)()
        If baslangic.HasValue Then
            kosullar.Add("BitisTarihi >= ?")
            p.Add(baslangic.Value.Date)
        End If
        If bitis.HasValue Then
            kosullar.Add("BitisTarihi <= ?")
            p.Add(bitis.Value.Date)
        End If
        Dim bk As String = BolumKosulu(bolum, p)
        If bk.Length > 0 Then kosullar.Add(bk)
        If kosullar.Count > 0 Then sql.Append(" WHERE ").Append(String.Join(" AND ", kosullar))
        sql.Append(" GROUP BY KalipTuru, Tur")
        Return Db.Tablo(sql.ToString(), p.ToArray())
    End Function
End Class
