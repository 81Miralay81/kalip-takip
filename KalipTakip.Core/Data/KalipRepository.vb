Imports System.Collections.Generic
Imports System.Data
Imports System.Data.SQLite
Imports System.Globalization
Imports System.IO
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
        ' "(boş)" yazılırsa seçili alanı boş olan kayıtlar gelir
        Dim bosMu As String = metin.Trim().ToLowerInvariant()
        If bosMu = "(boş)" OrElse bosMu = "(bos)" OrElse bosMu = "=boş" OrElse bosMu = "=bos" Then
            If kd.Tip = KolonTipi.Metin Then Return "(" & alan & " IS NULL OR TRIM(" & alan & ") = '')"
            Return alan & " IS NULL"
        End If
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

    ''' <summary>Kayıtlarda geçen hafta/yıl değerleri ve kayıt sayıları (yeni yıl, yeni hafta önce).</summary>
    Public Shared Function Haftalar() As DataTable
        Return Db.Tablo("SELECT Hafta, COUNT(*) AS Adet FROM Kalip WHERE Hafta IS NOT NULL AND TRIM(Hafta) <> '' GROUP BY Hafta")
    End Function

    ''' <summary>Kalıp türü ve (YENİ/İLAVE/TASHİH) türüne göre adetler; isteğe bağlı bitiş tarihi aralığı.</summary>
    Public Shared Function Istatistik(baslangic As Date?, bitis As Date?, Optional bolum As String = "", Optional hafta As String = "") As DataTable
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
        Dim hf As String = If(hafta, "").Trim()
        If hf.Length > 0 Then
            ' '42/2026' tam eşleşir; '42', '42/20' gibi eksik yazım önek olarak aranır
            If System.Text.RegularExpressions.Regex.IsMatch(hf, "^\d+/\d{4}$") Then
                kosullar.Add("Hafta = ?")
                p.Add(hf)
            Else
                kosullar.Add("Hafta LIKE ? ESCAPE '\'")
                p.Add(LikeKacis(hf) & "%")
            End If
        End If
        Dim bk As String = BolumKosulu(bolum, p)
        If bk.Length > 0 Then kosullar.Add(bk)
        If kosullar.Count > 0 Then sql.Append(" WHERE ").Append(String.Join(" AND ", kosullar))
        sql.Append(" GROUP BY KalipTuru, Tur")
        Return Db.Tablo(sql.ToString(), p.ToArray())
    End Function

    ' ---------------------------------------------------------------- TOPLU YÜKLEME (Excel / CSV)

    Public Class KtYuklemeSonuc
        Public Property Yeni As Integer
        Public Property Degisen As Integer
        Public Property Ayni As Integer
        Public Property HataSayisi As Integer
        Public ReadOnly Property Hatalar As New List(Of String)()
        Public ReadOnly Property Ornekler As New List(Of String)()
    End Class

    Private Shared Function YNorm(s As String) As String
        Dim k As String = TrMetin.Katla(If(s, "").Trim())
        Return New String(k.Where(Function(ch) Char.IsLetterOrDigit(ch)).ToArray())
    End Function

    Private Shared ReadOnly TarihBicimleri As String() = {
        "dd.MM.yyyy HH:mm", "dd.MM.yyyy", "d.M.yyyy", "d.M.yyyy HH:mm", "dd.MM.yyyy HH:mm:ss", "d.M.yyyy HH:mm:ss",
        "yyyy-MM-dd", "yyyy-MM-dd HH:mm", "yyyy-MM-dd HH:mm:ss", "yyyy-MM-ddTHH:mm:ss", "dd/MM/yyyy", "d/M/yyyy"
    }

    Private Shared Function YTarih(v As String, ByRef hata As Boolean) As Date?
        If String.IsNullOrWhiteSpace(v) Then Return Nothing
        Dim t As String = v.Trim()
        ' Excel (.xlsx) tarihleri seri numarası olarak gelir (örn. 45123 ya da 45123.5)
        If System.Text.RegularExpressions.Regex.IsMatch(t, "^\d{5}(\.\d+)?$") Then
            Dim n As Double = Double.Parse(t, NumberStyles.Float, Kultur.Inv)
            Return Date.FromOADate(Math.Round(n * 1440) / 1440)
        End If
        Dim d As Date
        If Date.TryParseExact(t, TarihBicimleri, Kultur.Inv, DateTimeStyles.None, d) Then Return d
        hata = True
        Return Nothing
    End Function

    Private Shared Function YSayi(v As String, ByRef hata As Boolean) As Double?
        If String.IsNullOrWhiteSpace(v) Then Return Nothing
        Dim t As String = v.Trim().Replace(" ", "")
        If t.Contains(",") AndAlso t.Contains(".") Then
            t = t.Replace(".", "").Replace(",", ".")
        ElseIf t.Contains(",") Then
            t = t.Replace(",", ".")
        End If
        Dim n As Double
        If Double.TryParse(t, NumberStyles.Float, Kultur.Inv, n) Then Return n
        hata = True
        Return Nothing
    End Function

    Private Shared Function YTam(v As String, ByRef hata As Boolean) As Integer?
        Dim x As Double? = YSayi(v, hata)
        If Not x.HasValue Then Return Nothing
        If Math.Abs(x.Value - Math.Round(x.Value)) > 0.000001 Then
            hata = True
            Return Nothing
        End If
        Return CInt(Math.Round(x.Value))
    End Function

    Private Shared Function YMetin(v As String) As String
        If String.IsNullOrWhiteSpace(v) Then Return Nothing
        Return v.Trim()
    End Function

    Private Shared Function YS(x As Double?) As String
        If Not x.HasValue Then Return ""
        Return Math.Round(x.Value, 6).ToString("0.######", Kultur.Inv)
    End Function

    Private Shared Function YT(x As Date?) As String
        If Not x.HasValue Then Return ""
        Return x.Value.ToString("yyyy-MM-dd HH:mm", Kultur.Inv)
    End Function

    Private Shared Function YI(x As Integer?) As String
        If Not x.HasValue Then Return ""
        Return x.Value.ToString(Kultur.Inv)
    End Function

    ''' <summary>Karşılaştırma için kullanıcının girdiği alanların metin özeti (Hafta/ToplamZaman hesaplanan alanlardır, dahil değil).</summary>
    Private Shared Function Imza(k As Kalip) As Dictionary(Of String, String)
        Dim d As New Dictionary(Of String, String)()
        d("ParcaKodu") = If(k.ParcaKodu, "").Trim()
        d("KalipTuru") = If(k.KalipTuru, "").Trim()
        d("TepeKodu") = If(k.TepeKodu, "").Trim()
        d("ParcaAdi") = If(k.ParcaAdi, "").Trim()
        d("GelisTarihi") = YT(k.GelisTarihi)
        d("TashihSayi") = YI(k.TashihSayi)
        d("BitisTarihi") = YT(k.BitisTarihi)
        d("Tur") = If(k.Tur, "").Trim()
        d("DovmeAdeti") = YS(k.DovmeAdeti)
        d("CncNo") = YI(k.CncNo)
        d("Aciklama") = If(k.Aciklama, "").Trim()
        d("IslemSuresi") = YS(k.IslemSuresi)
        d("Ayar") = YS(k.Ayar)
        d("Bakim") = YS(k.Bakim)
        d("ElemanEksikligi") = YS(k.ElemanEksikligi)
        d("Elektrik") = YS(k.Elektrik)
        d("KalipAriza") = YS(k.KalipAriza)
        d("Takim") = YS(k.Takim)
        d("Ariza") = YS(k.Ariza)
        Return d
    End Function

    Private Shared Sub YAta(k As Kalip, alan As String, v As String, ByRef hata As Boolean)
        Select Case alan
            Case "ParcaKodu" : k.ParcaKodu = YMetin(v)
            Case "KalipTuru" : k.KalipTuru = YMetin(v)
            Case "TepeKodu" : k.TepeKodu = YMetin(v)
            Case "ParcaAdi" : k.ParcaAdi = YMetin(v)
            Case "GelisTarihi" : k.GelisTarihi = YTarih(v, hata)
            Case "TashihSayi" : k.TashihSayi = YTam(v, hata)
            Case "BitisTarihi" : k.BitisTarihi = YTarih(v, hata)
            Case "Tur" : k.Tur = YMetin(v)
            Case "DovmeAdeti" : k.DovmeAdeti = YSayi(v, hata)
            Case "CncNo" : k.CncNo = YTam(v, hata)
            Case "Aciklama" : k.Aciklama = YMetin(v)
            Case "IslemSuresi" : k.IslemSuresi = YSayi(v, hata)
            Case "Ayar" : k.Ayar = YSayi(v, hata)
            Case "Bakim" : k.Bakim = YSayi(v, hata)
            Case "ElemanEksikligi" : k.ElemanEksikligi = YSayi(v, hata)
            Case "Elektrik" : k.Elektrik = YSayi(v, hata)
            Case "KalipAriza" : k.KalipAriza = YSayi(v, hata)
            Case "Takim" : k.Takim = YSayi(v, hata)
            Case "Ariza" : k.Ariza = YSayi(v, hata)
        End Select
    End Sub

    ''' <summary>Toplam zaman ve hafta, kayıt formundaki (sunucu) kuralla aynı şekilde yeniden hesaplanır.</summary>
    Private Shared Sub YHesapla(k As Kalip)
        Dim t As Double? = Nothing
        For Each d As Double? In New Double?() {k.IslemSuresi, k.Ayar, k.Bakim, k.ElemanEksikligi, k.Elektrik, k.KalipAriza, k.Takim, k.Ariza}
            If d.HasValue Then t = If(t.HasValue, t.Value, 0.0) + d.Value
        Next
        If t.HasValue Then
            k.ToplamZaman = Math.Round(t.Value, 2, MidpointRounding.AwayFromZero)
        Else
            k.ToplamZaman = Nothing
        End If
        k.Hafta = TarihYardimci.HaftaHesapla(k.BitisTarihi)
    End Sub

    Private Shared Sub YHata(s As KtYuklemeSonuc, mesaj As String)
        s.HataSayisi += 1
        If s.Hatalar.Count < 40 Then s.Hatalar.Add(mesaj)
    End Sub

    Private Shared Function KolonAdi(alan As String) As String
        Dim kd As KolonTanimi = Kolonlar.FirstOrDefault(Function(x) x.Alan = alan)
        Return If(kd Is Nothing, alan, kd.Baslik)
    End Function

    ''' <summary>
    ''' Kalıp Takip'ten dışa aktarılan Excel/CSV'yi geri yükler. 'ID' sütunu zorunludur:
    ''' dolu ID = o kayıt güncellenir (yalnızca dosyada bulunan sütunlar, yalnızca değişenler yazılır);
    ''' boş ID = yeni kayıt. Silme yapılmaz; parça kodu değiştirilemez. uygula=False ise hiçbir şey yazılmaz (önizleme).
    ''' </summary>
    Public Shared Function DosyadanYukle(yol As String, kullanici As String, uygula As Boolean, hatalilariAtla As Boolean, yeniIzni As Boolean, guncelleIzni As Boolean) As KtYuklemeSonuc
        Dim csv As Boolean = yol.EndsWith(".csv", StringComparison.OrdinalIgnoreCase) OrElse yol.EndsWith(".txt", StringComparison.OrdinalIgnoreCase)
        Dim ham As List(Of List(Of String)) = If(csv, KalipListeRepository.CsvOku(yol), KalipListeRepository.ExcelOku(yol))
        If ham.Count < 2 Then Throw New InvalidDataException("Dosyada başlık satırı ve en az bir kayıt satırı olmalı.")
        Dim baslik As List(Of String) = ham(0).Select(Function(h) YNorm(h)).ToList()
        Dim iId As Integer = baslik.IndexOf("id")
        If iId < 0 Then Throw New InvalidDataException("'ID' sütunu bulunamadı. Önce Kalıp Takip'ten 'Excel'e aktar' ile dosyayı indirin, onu düzenleyip yükleyin; ID sütununu silmeyin.")
        Dim eslesen As New List(Of KeyValuePair(Of KolonTanimi, Integer))()
        For Each kd As KolonTanimi In Kolonlar
            If kd.Alan = "Id" OrElse kd.Alan = "Hafta" OrElse kd.Alan = "ToplamZaman" Then Continue For
            Dim kdYerel As KolonTanimi = kd
            Dim idx As Integer = baslik.FindIndex(Function(h) h = YNorm(kdYerel.Baslik) OrElse h = YNorm(kdYerel.Alan))
            If idx >= 0 Then eslesen.Add(New KeyValuePair(Of KolonTanimi, Integer)(kd, idx))
        Next
        If Not eslesen.Any(Function(x) x.Key.Alan = "ParcaKodu") Then Throw New InvalidDataException("'Parça Kodu' sütunu bulunamadı.")

        Dim mevcut As New Dictionary(Of Integer, DataRow)()
        For Each r As DataRow In Db.Tablo("SELECT " & SecimListesi & " FROM Kalip").Rows
            mevcut(Convert.ToInt32(r("Id"))) = r
        Next

        Dim sonuc As New KtYuklemeSonuc()
        Dim gorulen As New HashSet(Of Integer)()
        Dim guncellenecek As New List(Of Kalip)()
        Dim eklenecek As New List(Of Kalip)()
        For i As Integer = 1 To ham.Count - 1
            Dim satir As List(Of String) = ham(i)
            Dim sno As String = (i + 1).ToString(Kultur.Inv) & ". satır: "
            Dim idMetin As String = If(iId < satir.Count, satir(iId), "").Trim()
            Dim k As Kalip
            Dim eskiK As Kalip = Nothing
            Dim id As Integer = 0
            If idMetin.Length > 0 Then
                If Not Integer.TryParse(idMetin, NumberStyles.Integer, Kultur.Inv, id) OrElse id <= 0 Then
                    YHata(sonuc, sno & "ID '" & idMetin & "' geçersiz.")
                    Continue For
                End If
                If Not mevcut.ContainsKey(id) Then
                    YHata(sonuc, sno & "ID " & id.ToString(Kultur.Inv) & " veritabanında yok (silinmiş olabilir). Yeni kayıt için ID hücresini boş bırakın.")
                    Continue For
                End If
                If Not gorulen.Add(id) Then
                    YHata(sonuc, sno & "ID " & id.ToString(Kultur.Inv) & " dosyada birden çok kez geçiyor.")
                    Continue For
                End If
                If Not guncelleIzni Then
                    YHata(sonuc, sno & "Kayıt güncelleme yetkiniz yok.")
                    Continue For
                End If
                eskiK = Kalip.SatirdanOku(mevcut(id))
                k = Kalip.SatirdanOku(mevcut(id))
            Else
                If Not yeniIzni Then
                    YHata(sonuc, sno & "Yeni kayıt ekleme yetkiniz yok.")
                    Continue For
                End If
                k = New Kalip()
            End If
            Dim hatali As Boolean = False
            For Each kv As KeyValuePair(Of KolonTanimi, Integer) In eslesen
                Dim v As String = If(kv.Value < satir.Count, satir(kv.Value), "")
                Dim h As Boolean = False
                YAta(k, kv.Key.Alan, v, h)
                If h Then
                    YHata(sonuc, sno & kv.Key.Baslik & " '" & v & "' anlaşılamadı.")
                    hatali = True
                    Exit For
                End If
            Next
            If hatali Then Continue For
            If String.IsNullOrWhiteSpace(k.ParcaKodu) Then
                YHata(sonuc, sno & "Parça kodu boş olamaz.")
                Continue For
            End If
            If eskiK IsNot Nothing Then
                If TrMetin.Katla(If(eskiK.ParcaKodu, "").Trim()) <> TrMetin.Katla(k.ParcaKodu.Trim()) Then
                    YHata(sonuc, sno & "ID " & id.ToString(Kultur.Inv) & ": parça kodu burada değiştirilemez (" & eskiK.ParcaKodu & " → " & k.ParcaKodu & "). Kalıp Listesi'ndeki 'Kodu düzelt' kullanın.")
                    Continue For
                End If
                Dim eski As Dictionary(Of String, String) = Imza(eskiK)
                Dim yeniImza As Dictionary(Of String, String) = Imza(k)
                Dim farklar As New List(Of String)()
                For Each kv2 As KeyValuePair(Of String, String) In eski
                    If yeniImza(kv2.Key) <> kv2.Value Then farklar.Add(KolonAdi(kv2.Key) & ": '" & kv2.Value & "' → '" & yeniImza(kv2.Key) & "'")
                Next
                If farklar.Count = 0 Then
                    sonuc.Ayni += 1
                Else
                    sonuc.Degisen += 1
                    YHesapla(k)
                    guncellenecek.Add(k)
                    If sonuc.Ornekler.Count < 8 Then sonuc.Ornekler.Add("#" & id.ToString(Kultur.Inv) & " " & k.ParcaKodu & " — " & String.Join("; ", farklar.Take(3)) & If(farklar.Count > 3, " (+" & (farklar.Count - 3).ToString(Kultur.Inv) & " alan)", ""))
                End If
            Else
                YHesapla(k)
                eklenecek.Add(k)
                sonuc.Yeni += 1
                If sonuc.Ornekler.Count < 8 Then sonuc.Ornekler.Add("YENİ " & k.ParcaKodu & If(String.IsNullOrEmpty(k.KalipTuru), "", " — " & k.KalipTuru))
            End If
        Next

        If Not uygula Then Return sonuc
        If sonuc.HataSayisi > 0 AndAlso Not hatalilariAtla Then Throw New InvalidOperationException("Dosyada " & sonuc.HataSayisi.ToString(Kultur.Inv) & " hatalı satır var; hatalı satırlar atlanarak yazılması onaylanmalı.")
        Dim alanlar As String = String.Join(",", YazilanAlanlar) & ",Surum,SonDegisiklik,SonKullanici"
        Dim yerTutucu As String = String.Join(",", Enumerable.Repeat("?", YazilanAlanlar.Length + 3))
        Dim atamalar As String = String.Join(",", YazilanAlanlar.Select(Function(a) a & "=?")) & ",Surum=?,SonDegisiklik=?,SonKullanici=?"
        Using c As SQLiteConnection = Db.Ac()
            Using tx As SQLiteTransaction = c.BeginTransaction()
                For Each k As Kalip In guncellenecek
                    Dim p As List(Of Object) = Degerler(k)
                    p.Add(Db.Prm(DbType.Int32, k.Surum + 1))
                    p.Add(Db.Prm(DbType.DateTime, Date.Now))
                    p.Add(Db.Prm(DbType.String, Bos(kullanici)))
                    p.Add(Db.Prm(DbType.Int32, k.Id))
                    Using cmd As SQLiteCommand = Db.Komut(c, tx, "UPDATE Kalip SET " & atamalar & " WHERE Id=?", p.ToArray())
                        cmd.ExecuteNonQuery()
                    End Using
                Next
                For Each k As Kalip In eklenecek
                    Dim p As List(Of Object) = Degerler(k)
                    p.Add(Db.Prm(DbType.Int32, 1))
                    p.Add(Db.Prm(DbType.DateTime, Date.Now))
                    p.Add(Db.Prm(DbType.String, Bos(kullanici)))
                    Using cmd As SQLiteCommand = Db.Komut(c, tx, "INSERT INTO Kalip (" & alanlar & ") VALUES (" & yerTutucu & ")", p.ToArray())
                        cmd.ExecuteNonQuery()
                    End Using
                Next
                tx.Commit()
            End Using
        End Using
        Return sonuc
    End Function
End Class
