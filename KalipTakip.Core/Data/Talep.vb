Imports System.Collections.Generic
Imports System.Data
Imports System.Data.SQLite
Imports System.Linq
Imports System.Text

''' <summary>Bir talep kaydı (Talep tablosunun bir satırı). Talebin kalıpları TalepKalip satırlarındadır.</summary>
Public Class Talep
    Public Property Id As Integer
    Public Property TalepNo As String
    ''' <summary>Özet: talepteki kalıp kodları (virgülle). Gerçek kalıp listesi TalepKalip tablosundadır.</summary>
    Public Property KalipKodu As String
    Public Property Bolum As String
    Public Property Lokasyon As String
    Public Property Oncelik As String
    ''' <summary>Talep statüsü: kalıp statülerinden otomatik hesaplanır (İptal elle).</summary>
    Public Property Statu As String
    Public Property TalepTarihi As Date?
    Public Property TerminTarihi As Date?
    Public Property TamamlanmaTarihi As Date?
    Public Property TalepEden As String
    Public Property Aciklama As String
    Public Property Sonuc As String
    ''' <summary>Eşzamanlı düzenleme kontrolü için sürüm numarası.</summary>
    Public Property Surum As Integer = 1
    Public Property SonDegisiklik As Date?
    Public Property SonKullanici As String

    Public Shared Function SatirdanOku(r As DataRow) As Talep
        Dim t As New Talep()
        t.Id = Convert.ToInt32(r("Id"))
        t.TalepNo = M(r("TalepNo"))
        t.KalipKodu = M(r("KalipKodu"))
        t.Bolum = M(r("Bolum"))
        t.Lokasyon = M(r("Lokasyon"))
        t.Oncelik = M(r("Oncelik"))
        t.Statu = M(r("Statu"))
        t.TalepTarihi = Tr(r("TalepTarihi"))
        t.TerminTarihi = Tr(r("TerminTarihi"))
        t.TamamlanmaTarihi = Tr(r("TamamlanmaTarihi"))
        t.TalepEden = M(r("TalepEden"))
        t.Aciklama = M(r("Aciklama"))
        t.Sonuc = M(r("Sonuc"))
        t.Surum = If(IsDBNull(r("Surum")), 1, Convert.ToInt32(r("Surum")))
        t.SonDegisiklik = Tr(r("SonDegisiklik"))
        t.SonKullanici = M(r("SonKullanici"))
        Return t
    End Function

    Private Shared Function M(o As Object) As String
        If o Is Nothing OrElse IsDBNull(o) Then Return Nothing
        Return Convert.ToString(o)
    End Function

    Private Shared Function Tr(o As Object) As Date?
        If o Is Nothing OrElse IsDBNull(o) Then Return Nothing
        Return Convert.ToDateTime(o)
    End Function
End Class

''' <summary>Talepteki bir kalıp satırı: kalıba özel açıklama, statü, lokasyon ve sayaçlar.</summary>
Public Class TalepKalip
    ''' <summary>0 = henüz kaydedilmemiş yeni satır.</summary>
    Public Property Id As Integer
    Public Property KalipKodu As String
    Public Property Aciklama As String
    Public Property KalipStatu As String
    Public Property Lokasyon As String
    ''' <summary>Teslim alırken seçilen raf (boşsa kalıbın rafı değişmez).</summary>
    Public Property Raf As String
    ''' <summary>Bu kalıbın kaçıncı talebi (1, 2, 3...).</summary>
    Public Property TalepSira As Integer
    ''' <summary>Talep açıldığı andaki tashih sayısı (Kalıp Takip kaydından).</summary>
    Public Property TashihSayisi As Integer
    Public Property SahipBolum As String
End Class

''' <summary>Kalıp okutulunca gösterilen özet.</summary>
Public Class KalipOzeti
    Public Property KalipKodu As String
    Public Property ParcaAdi As String
    Public Property KalipTuru As String
    ''' <summary>Kalıp Takip tablosunda bu kodla kayıt var mı?</summary>
    Public Property Bulundu As Boolean
    Public Property Lokasyon As String
    Public Property TalepAdedi As Integer
    Public Property TashihSayisi As Integer
    Public Property SahipBolum As String
End Class

''' <summary>Talep iş kuralları: kalıp statüleri, otomatik lokasyon, talep statüsü.</summary>
Public NotInheritable Class TalepKurallari
    Private Sub New()
    End Sub

    Public Const Planlandi As String = "Planlandı"
    Public Const Sevkediliyor As String = "Sevkediliyor"
    Public Const TeslimAlindi As String = "Teslim Alındı"
    Public Const IslemeAlindi As String = "İşleniyor"
    Public Const Tamamlandi As String = "Tamamlandı"
    Public Const Hurda As String = "Hurdaya Ayrıldı"
    Public Const IadeEdildi As String = "İade Edildi"

    Public Const TalepAcik As String = "Açık"
    Public Const TalepKismi As String = "Kısmi Teslim Alındı"
    Public Const TalepKapandi As String = "Kapandı"
    Public Const TalepIptal As String = "İptal"

    Public Const LokKaliphane As String = "KALIPHANE"

    Public Shared ReadOnly KalipStatuleri As String() = {Planlandi, Sevkediliyor, TeslimAlindi, IslemeAlindi, Tamamlandi, Hurda, IadeEdildi}
    Public Shared ReadOnly TalepStatuleri As String() = {TalepAcik, TalepKismi, TalepKapandi, TalepIptal}

    ' <summary>
    ' Kalıp statüsüne göre otomatik lokasyon: teslim alınan / işlemdeki / tamamlanan kalıp KALIPHANE'de;
    ' diğer durumlarda (planlandı, sevk, hurda, iade) talep eden bölümün ana lokasyonunda.
    ' </summary>
    Public Shared Function LokasyonBul(statu As String, bolum As String) As String
        Select Case statu
            Case TeslimAlindi, IslemeAlindi, Tamamlandi
                Return LokKaliphane
            Case Else
                Return LokasyonRepository.AnaLokasyon(bolum)
        End Select
    End Function

    Private Shared Function TeslimAlinmis(statu As String) As Boolean
        Return statu = TeslimAlindi OrElse statu = IslemeAlindi OrElse statu = Tamamlandi
    End Function

    ''' <summary>
    ''' Talep statüsü: hiçbir kalıp teslim alınmadıysa Açık; bir kısmı alındıysa Kısmi Teslim Alındı;
    ''' hepsi sonuçlandıysa (teslim alındı ya da hurda) Kapandı. İptal elle verilir ve korunur.
    ''' </summary>
    Public Shared Function TalepDurumu(kalipStatuleri As IEnumerable(Of String), mevcut As String) As String
        If mevcut = TalepIptal Then Return TalepIptal
        Dim liste As List(Of String) = kalipStatuleri.ToList()
        If liste.Count = 0 Then Return TalepAcik
        Dim hepsiSonuc As Boolean = liste.All(Function(s) TeslimAlinmis(s) OrElse s = Hurda OrElse s = IadeEdildi)
        If hepsiSonuc Then Return TalepKapandi
        If liste.Any(Function(s) TeslimAlinmis(s)) Then Return TalepKismi
        Return TalepAcik
    End Function
End Class

Partial Public NotInheritable Class TalepRepository
    Private Sub New()
    End Sub

    Public Shared ReadOnly Statuler As String() = TalepKurallari.TalepStatuleri
    Public Shared ReadOnly Oncelikler As String() = {"Normal", "Acil", "Çok Acil"}

    ''' <summary>Talepler listesinde görünen kolonlar.</summary>
    Public Shared ReadOnly Kolonlar As KolonTanimi() = {
        New KolonTanimi("Id", "ID", KolonTipi.Tam, 60),
        New KolonTanimi("TalepNo", "Talep No", KolonTipi.Metin, 120),
        New KolonTanimi("KalipAdedi", "Kalıp Adedi", KolonTipi.Tam, 90),
        New KolonTanimi("KalipKodu", "Kalıplar", KolonTipi.Metin, 230),
        New KolonTanimi("Bolum", "Gönderen Bölüm", KolonTipi.Metin, 140),
        New KolonTanimi("AliciBolum", "Alıcı Bölüm", KolonTipi.Metin, 140),
        New KolonTanimi("Oncelik", "Öncelik", KolonTipi.Metin, 90),
        New KolonTanimi("Statu", "Talep Statü", KolonTipi.Metin, 150),
        New KolonTanimi("TalepTarihi", "Talep Tarihi", KolonTipi.Tarih, 105),
        New KolonTanimi("TerminTarihi", "Termin", KolonTipi.Tarih, 95),
        New KolonTanimi("TamamlanmaTarihi", "Kapanış", KolonTipi.Tarih, 95),
        New KolonTanimi("TalepEden", "Talep Eden", KolonTipi.Metin, 120),
        New KolonTanimi("Aciklama", "Genel Açıklama", KolonTipi.Metin, 260)
    }

    Private Const Secim As String =
        "Id,TalepNo,KalipKodu,Bolum,Lokasyon,Oncelik,Statu,TalepTarihi,TerminTarihi,TamamlanmaTarihi,TalepEden,Aciklama,Sonuc,Surum,SonDegisiklik,SonKullanici," &
        "AliciBolum,GonderTarihi,GeriDondu,GeriTarihi," &
        "(SELECT COUNT(*) FROM TalepKalip k WHERE k.TalepId = Talep.Id) AS KalipAdedi"

    Private Const YazilanAlanlar As String =
        "TalepNo,KalipKodu,Bolum,Lokasyon,Oncelik,Statu,TalepTarihi,TerminTarihi,TamamlanmaTarihi,TalepEden,Aciklama,Sonuc"

    Private Const SatirSecim As String =
        "Id AS SatirId, KalipKodu, KalipStatu, Lokasyon, Raf, Aciklama, TalepSira, TashihSayisi, TeslimTarihi, OncekiLokasyon, OncekiRaf, " &
        "(SELECT SahipBolum FROM KalipLokasyon l WHERE l.Anahtar = TalepKalip.Anahtar) AS SahipBolum"

    Private Shared Function LikeKacis(s As String) As String
        Return s.Replace("\", "\\").Replace("%", "\%").Replace("_", "\_")
    End Function

    ''' <summary>Yildiz (*) joker: "1123*-1" = icinde 1123 gecen ve "-1" ile biten; sona yildiz konursa sonu serbest. Yildiz yoksa "icerir".</summary>
    Private Shared Function JokerDeseni(katlanmis As String) As String
        If katlanmis.IndexOf("*"c) < 0 Then Return "%" & LikeKacis(katlanmis) & "%"
        Dim parcalar As String() = katlanmis.Split("*"c)
        Dim sb As New System.Text.StringBuilder("%")
        For i As Integer = 0 To parcalar.Length - 1
            If i > 0 Then sb.Append("%")
            sb.Append(LikeKacis(parcalar(i)))
        Next
        Return sb.ToString()
    End Function

    Private Shared Function Desen(metin As String) As String
        Return JokerDeseni(TrMetin.Katla(metin.Trim()))
    End Function

    Private Shared Function Anahtar(kod As String) As String
        Return TrMetin.Katla(If(kod, "").Trim())
    End Function

    Private Shared Function LimitEki(limit As Integer) As String
        If limit > 0 Then Return " LIMIT " & limit.ToString(Kultur.Inv)
        Return ""
    End Function

    ''' <summary>
    ''' Genel arama: metin (talep no, kalıp kodu, bölüm, talep eden, açıklama, kalıp açıklamaları), statü, bölüm, tarih aralığı.
    ''' En yeni talepler önce; limit 0 = hepsi.
    ''' </summary>
    Public Shared Function Ara(metin As String, statu As String, bolum As String, bas As Date?, bit As Date?, limit As Integer) As DataTable
        Dim sql As New StringBuilder("SELECT " & Secim & " FROM Talep")
        Dim kosullar As New List(Of String)()
        Dim p As New List(Of Object)()

        Dim aranan As String = If(metin, "").Trim()
        If aranan.Length > 0 Then
            kosullar.Add("(KATLA(TalepNo) LIKE ? ESCAPE '\' OR KATLA(KalipKodu) LIKE ? ESCAPE '\' OR KATLA(Bolum) LIKE ? ESCAPE '\' " &
                         "OR KATLA(TalepEden) LIKE ? ESCAPE '\' OR KATLA(Aciklama) LIKE ? ESCAPE '\' " &
                         "OR EXISTS (SELECT 1 FROM TalepKalip k WHERE k.TalepId = Talep.Id AND (KATLA(k.Aciklama) LIKE ? ESCAPE '\' OR KATLA(k.Lokasyon) LIKE ? ESCAPE '\')))")
            For i As Integer = 1 To 7
                p.Add(Desen(aranan))
            Next
        End If
        If Not String.IsNullOrEmpty(statu) Then
            kosullar.Add("Statu = ?")
            p.Add(statu)
        End If
        If Not String.IsNullOrWhiteSpace(bolum) Then
            kosullar.Add("KATLA(Bolum) LIKE ? ESCAPE '\'")
            p.Add(Desen(bolum))
        End If
        If bas.HasValue Then
            kosullar.Add("TalepTarihi >= ?")
            p.Add(bas.Value.Date)
        End If
        If bit.HasValue Then
            kosullar.Add("TalepTarihi <= ?")
            p.Add(bit.Value.Date)
        End If
        If kosullar.Count > 0 Then sql.Append(" WHERE ").Append(String.Join(" AND ", kosullar))
        sql.Append(" ORDER BY Id DESC").Append(LimitEki(limit))
        Return Db.Tablo(sql.ToString(), p.ToArray())
    End Function

    ''' <summary>
    ''' Kalıp bazlı geçmiş: bir kalıp kodunun geçtiği tüm talep satırları (kalıba özel açıklamalar dahil).
    ''' Kod boşsa en yeni 'limit' satır.
    ''' </summary>
    Public Shared Function KalipSatirlari(kod As String, limit As Integer) As DataTable
        Dim sql As New StringBuilder(
            "SELECT t.Id AS Id, k.Id AS SatirId, t.TalepNo, t.Statu, k.KalipStatu, t.TalepTarihi, t.Bolum, k.Lokasyon, " &
            "k.TalepSira, k.TashihSayisi, k.KalipKodu, k.Aciklama FROM TalepKalip k JOIN Talep t ON t.Id = k.TalepId")
        Dim p As New List(Of Object)()
        If Not String.IsNullOrWhiteSpace(kod) Then
            sql.Append(" WHERE k.Anahtar LIKE ? ESCAPE '\'")
            p.Add(Desen(kod))
        End If
        sql.Append(" ORDER BY k.Id DESC").Append(LimitEki(limit))
        Return Db.Tablo(sql.ToString(), p.ToArray())
    End Function

    Public Shared Function Satir(id As Integer) As DataTable
        Return Db.Tablo("SELECT " & Secim & " FROM Talep WHERE Id = ?", id)
    End Function

    ''' <summary>Talebin kalıp satırları.</summary>
    Public Shared Function SatirlariGetir(talepId As Integer) As DataTable
        Return Db.Tablo("SELECT " & SatirSecim & " FROM TalepKalip WHERE TalepId = ? ORDER BY Id", talepId)
    End Function

    ''' <summary>Kalıp Takip tablosunda bu koda tam uyan en yeni kayıt (ParcaKodu, ParcaAdi, KalipTuru).</summary>
    Public Shared Function KalipBul(kod As String) As DataTable
        Return Db.Tablo("SELECT ParcaKodu, ParcaAdi, KalipTuru FROM Kalip WHERE KATLA(ParcaKodu) = ? ORDER BY Id DESC LIMIT 1",
                        Anahtar(kod))
    End Function

    ''' <summary>Kalıp özeti: kalıp bilgisi, mevcut lokasyon, kaç talepte yer aldığı, tashih sayısı.</summary>
    Public Shared Function KalipOzet(kod As String) As KalipOzeti
        Dim o As New KalipOzeti()
        o.KalipKodu = If(kod, "").Trim()
        Dim kb As DataTable = KalipBul(kod)
        If kb.Rows.Count > 0 Then
            o.Bulundu = True
            o.KalipKodu = Convert.ToString(kb.Rows(0)("ParcaKodu"))
            o.ParcaAdi = If(IsDBNull(kb.Rows(0)("ParcaAdi")), "", Convert.ToString(kb.Rows(0)("ParcaAdi")))
            o.KalipTuru = If(IsDBNull(kb.Rows(0)("KalipTuru")), "", Convert.ToString(kb.Rows(0)("KalipTuru")))
        End If
        Dim a As String = Anahtar(o.KalipKodu)
        o.TalepAdedi = Convert.ToInt32(Db.Skaler("SELECT COUNT(*) FROM TalepKalip WHERE Anahtar = ?", a))
        Dim kl As DataTable = Db.Tablo("SELECT Lokasyon, SahipBolum FROM KalipLokasyon WHERE Anahtar = ?", a)
        If kl.Rows.Count > 0 Then
            If Not IsDBNull(kl.Rows(0)("Lokasyon")) Then o.Lokasyon = Convert.ToString(kl.Rows(0)("Lokasyon"))
            If Not IsDBNull(kl.Rows(0)("SahipBolum")) Then o.SahipBolum = Convert.ToString(kl.Rows(0)("SahipBolum"))
        End If
        o.TashihSayisi = TashihSayisiBul(o.KalipKodu)
        Return o
    End Function

    ' Yazdıkça öneri listesi: kalıp kodu / adı içinde geçenler, güncel lokasyon ve sahip bölümle.
    Public Shared Function KalipAra(metin As String, limit As Integer) As DataTable
        Dim d As String = Desen(If(metin, "").Trim())
        Return Db.Tablo(
            "SELECT k.ParcaKodu AS ParcaKodu, MAX(k.ParcaAdi) AS ParcaAdi, MAX(k.KalipTuru) AS KalipTuru, " &
            "MAX(l.Lokasyon) AS Lokasyon, MAX(l.SahipBolum) AS SahipBolum " &
            "FROM Kalip k LEFT JOIN KalipLokasyon l ON l.Anahtar = KATLA(k.ParcaKodu) " &
            "WHERE k.ParcaKodu IS NOT NULL AND (KATLA(k.ParcaKodu) LIKE ? ESCAPE '\' OR KATLA(k.ParcaAdi) LIKE ? ESCAPE '\') " &
            "GROUP BY KATLA(k.ParcaKodu) ORDER BY k.ParcaKodu LIMIT " & limit.ToString(Kultur.Inv), d, d)
    End Function

    Private Shared Function TashihSayisiBul(kod As String) As Integer
        Dim th As Object = Db.Skaler("SELECT MAX(COALESCE(MAX(TashihSayi), 0), SUM(CASE WHEN (TashihSayi > 0 OR KATLA(COALESCE(Tur,'')) LIKE 'tash%' OR KATLA(COALESCE(Tur,'')) LIKE 'tamir%') THEN 1 ELSE 0 END)) FROM Kalip WHERE ParcaKodu = ?", kod)
        If th Is Nothing OrElse IsDBNull(th) Then Return 0
        Return Convert.ToInt32(th)
    End Function

    ' <summary>
    ' Kalıp yeni bir talebe eklenebilir mi? Kalıp şu an talebi açan (gönderen) bölümde olmalıdır: bir bölüme teslim edilmiş
    ' kalıp, geri gönderilmedikçe başka bir bölüm adına talebe eklenemez (talep kapanmış olsa bile).
    ' Ayrıca henüz sonuçlanmamış (planlandı / sevk edilmiş) başka bir talepte olmamalıdır. Eklenebiliyorsa Nothing, değilse sebep mesajı döner.
    ' </summary>
    Public Shared Function EklemeEngeli(kod As String, haricTalepId As Integer, talepBolum As String) As String
        Dim a As String = Anahtar(kod)
        Dim kl As DataTable = Db.Tablo("SELECT Lokasyon, SahipBolum FROM KalipLokasyon WHERE Anahtar = ?", a)
        Dim lokasyon As String = ""
        Dim sahip As String = ""
        If kl.Rows.Count > 0 Then
            If Not IsDBNull(kl.Rows(0)("Lokasyon")) Then lokasyon = Convert.ToString(kl.Rows(0)("Lokasyon"))
            If Not IsDBNull(kl.Rows(0)("SahipBolum")) Then sahip = Convert.ToString(kl.Rows(0)("SahipBolum"))
        End If
        If Not String.IsNullOrWhiteSpace(talepBolum) Then
            Dim bilinen As List(Of String) = LokasyonRepository.Bolumler()
            Dim eldeki As String = ""
            If lokasyon.Trim().Length > 0 Then
                eldeki = LokasyonRepository.BolumBul(lokasyon)
            ElseIf sahip.Trim().Length > 0 Then
                eldeki = sahip
            End If
            Dim anEldeki As String = TrMetin.Katla(eldeki)
            If anEldeki.Length > 0 AndAlso bilinen.Any(Function(x) TrMetin.Katla(x) = anEldeki) AndAlso anEldeki <> TrMetin.Katla(talepBolum) Then
                Return kod & " şu an " & eldeki & " bölümünde" & If(lokasyon.Trim().Length > 0, " (" & lokasyon & ")", "") & "; " &
                       talepBolum.Trim().ToUpper(Kultur.Tr) & " bölümü adına gönderilemez." & vbCrLf & vbCrLf &
                       "Kalıp, bulunduğu bölüm tarafından geri gönderilmeden yeni bir talebe eklenemez."
            End If
        End If
        Dim dt As DataTable = Db.Tablo(
            "SELECT t.TalepNo, k.KalipStatu FROM TalepKalip k JOIN Talep t ON t.Id = k.TalepId " &
            "WHERE k.Anahtar = ? AND k.TalepId <> ? AND t.Statu <> 'İptal' " &
            "AND k.KalipStatu IN ('Planlandı','Sevkediliyor','Hurdaya Ayrıldı') LIMIT 1", a, haricTalepId)
        If dt.Rows.Count > 0 Then
            Return kod & " zaten " & Convert.ToString(dt.Rows(0)("TalepNo")) & " numaralı talepte (" & Convert.ToString(dt.Rows(0)("KalipStatu")) & "). Tekrar eklenemez."
        End If
        Return Nothing
    End Function

    Private Shared Function SahipBul(c As SQLiteConnection, tx As SQLiteTransaction, kod As String) As String
        Dim o As Object = Skaler(c, tx, "SELECT SahipBolum FROM KalipLokasyon WHERE Anahtar = ?", Anahtar(kod))
        If o Is Nothing OrElse IsDBNull(o) Then Return Nothing
        Return Convert.ToString(o)
    End Function

    ''' <summary>Daha önce kullanılmış bölüm adları (öneri listesi).</summary>
    Public Shared Function BolumListesi() As List(Of String)
        Dim sonuc As New List(Of String)()
        Try
            Dim dt As DataTable = Db.Tablo("SELECT DISTINCT Bolum FROM Talep WHERE Bolum IS NOT NULL AND Bolum <> '' ORDER BY Bolum")
            For Each r As DataRow In dt.Rows
                sonuc.Add(Convert.ToString(r("Bolum")))
            Next
            For Each x As String In ListeRepository.Getir("Bolum")
                If Not sonuc.Contains(x) Then sonuc.Add(x)
            Next
            For Each x As String In LokasyonRepository.Bolumler()
                If Not sonuc.Contains(x) Then sonuc.Add(x)
            Next
        Catch
        End Try
        Return sonuc
    End Function

    Private Shared Function Bos(s As String) As Object
        If String.IsNullOrWhiteSpace(s) Then Return Nothing
        Return s.Trim()
    End Function

    Private Shared Function Degerler(t As Talep) As List(Of Object)
        Dim m As DbType = DbType.String
        Dim d As DbType = DbType.DateTime
        Return New List(Of Object) From {
            Db.Prm(m, Bos(t.TalepNo)), Db.Prm(m, Bos(t.KalipKodu)), Db.Prm(m, Bos(t.Bolum)), Db.Prm(m, Bos(t.Lokasyon)),
            Db.Prm(m, Bos(t.Oncelik)), Db.Prm(m, Bos(t.Statu)),
            Db.Prm(d, t.TalepTarihi), Db.Prm(d, t.TerminTarihi), Db.Prm(d, t.TamamlanmaTarihi),
            Db.Prm(m, Bos(t.TalepEden)), Db.Prm(m, Bos(t.Aciklama)), Db.Prm(m, Bos(t.Sonuc))
        }
    End Function

    ' ---------------------------------------------------------------- YAZMA (tek işlemde)

    Private Shared Sub Calistir(c As SQLiteConnection, tx As SQLiteTransaction, sql As String, ParamArray p() As Object)
        Using cmd As SQLiteCommand = Db.Komut(c, tx, sql, p)
            cmd.ExecuteNonQuery()
        End Using
    End Sub

    Private Shared Function Skaler(c As SQLiteConnection, tx As SQLiteTransaction, sql As String, ParamArray p() As Object) As Object
        Using cmd As SQLiteCommand = Db.Komut(c, tx, sql, p)
            Return cmd.ExecuteScalar()
        End Using
    End Function

    Private Shared Sub HareketYaz(c As SQLiteConnection, tx As SQLiteTransaction, kod As String, talepId As Integer, talepNo As String,
                                  eskiStatu As String, yeniStatu As String, eskiLok As String, yeniLok As String, kullanici As String)
        Calistir(c, tx,
                 "INSERT INTO KalipHareket (Anahtar, KalipKodu, TalepId, TalepNo, EskiStatu, YeniStatu, EskiLokasyon, YeniLokasyon, Tarih, Kullanici) " &
                 "VALUES (?,?,?,?,?,?,?,?,?,?)",
                 Anahtar(kod), kod, talepId, talepNo, Bos(eskiStatu), Bos(yeniStatu), Bos(eskiLok), Bos(yeniLok),
                 Db.Prm(DbType.DateTime, Date.Now), Bos(kullanici))
    End Sub

    Private Shared Function MevcutLokasyon(c As SQLiteConnection, tx As SQLiteTransaction, kod As String) As String
        Dim o As Object = Skaler(c, tx, "SELECT Lokasyon FROM KalipLokasyon WHERE Anahtar = ?", Anahtar(kod))
        If o Is Nothing OrElse IsDBNull(o) Then Return Nothing
        Return Convert.ToString(o)
    End Function

    Private Shared Sub LokasyonKaydet(c As SQLiteConnection, tx As SQLiteTransaction, kod As String, lokasyon As String, sahip As String, kullanici As String,
                                      Optional raf As String = Nothing)
        Calistir(c, tx,
                 "INSERT INTO KalipLokasyon (Anahtar, KalipKodu, Lokasyon, SahipBolum, Raf, SonDegisiklik, SonKullanici) VALUES (?,?,?,?,?,?,?) " &
                 "ON CONFLICT(Anahtar) DO UPDATE SET KalipKodu = excluded.KalipKodu, Lokasyon = excluded.Lokasyon, " &
                 "SahipBolum = COALESCE(excluded.SahipBolum, KalipLokasyon.SahipBolum), Raf = COALESCE(excluded.Raf, KalipLokasyon.Raf), " &
                 "SonDegisiklik = excluded.SonDegisiklik, SonKullanici = excluded.SonKullanici",
                 Anahtar(kod), kod, Bos(lokasyon), Bos(sahip), Bos(raf), Db.Prm(DbType.DateTime, Date.Now), Bos(kullanici))
    End Sub

    ''' <summary>Raf adını düzenler; tanımlı değilse hata verir (boşsa Nothing).</summary>
    Private Shared Function RafDogrula(raf As String) As String
        Dim n As String = RafRepository.Normal(raf)
        If n IsNot Nothing AndAlso Not RafRepository.Var(n) Then
            Throw New InvalidOperationException("'" & n & "' adında bir raf tanımlı değil. Önce Raflar ekranından rafı oluşturun.")
        End If
        Return n
    End Function

    ''' <summary>Yeni kalıp satırı ekler: sıra ve tashih sayacını hesaplar, lokasyonu kurala göre belirler, hareketi yazar.</summary>
    Private Shared Sub SatirEkle(c As SQLiteConnection, tx As SQLiteTransaction, talepId As Integer, talepNo As String,
                                 bolum As String, s As TalepKalip, kullanici As String)
        Dim kod As String = s.KalipKodu.Trim()
        Dim engel As String = EklemeEngeli(kod, talepId, bolum)
        If engel IsNot Nothing Then Throw New InvalidOperationException(engel)
        Dim a As String = Anahtar(kod)
        Dim sira As Integer = Convert.ToInt32(Skaler(c, tx, "SELECT COUNT(*) FROM TalepKalip WHERE Anahtar = ?", a)) + 1
        Dim tashih As Integer = 0
        Dim th As Object = Skaler(c, tx, "SELECT MAX(COALESCE(MAX(TashihSayi), 0), SUM(CASE WHEN (TashihSayi > 0 OR KATLA(COALESCE(Tur,'')) LIKE 'tash%' OR KATLA(COALESCE(Tur,'')) LIKE 'tamir%') THEN 1 ELSE 0 END)) FROM Kalip WHERE ParcaKodu = ?", kod)
        If th IsNot Nothing AndAlso Not IsDBNull(th) Then tashih = Convert.ToInt32(th)
        Dim statu As String = If(String.IsNullOrWhiteSpace(s.KalipStatu), TalepKurallari.Planlandi, s.KalipStatu)
        Dim eskiLok As String = MevcutLokasyon(c, tx, kod)
        Dim lok As String
        If Not String.IsNullOrWhiteSpace(s.Lokasyon) Then
            lok = s.Lokasyon.Trim()
        ElseIf statu = TalepKurallari.Planlandi AndAlso Not String.IsNullOrWhiteSpace(eskiLok) Then
            lok = eskiLok            ' gönderilene kadar kalıp bulunduğu yerde kalır
        Else
            lok = TalepKurallari.LokasyonBul(statu, bolum)
        End If
        Dim raf As String = RafDogrula(s.Raf)

        Calistir(c, tx,
                 "INSERT INTO TalepKalip (TalepId, KalipKodu, Anahtar, Aciklama, KalipStatu, Lokasyon, Raf, TalepSira, TashihSayisi, SonDegisiklik, SonKullanici) " &
                 "VALUES (?,?,?,?,?,?,?,?,?,?,?)",
                 talepId, kod, a, Bos(s.Aciklama), statu, Bos(lok), Bos(raf), sira, tashih, Db.Prm(DbType.DateTime, Date.Now), Bos(kullanici))
        HareketYaz(c, tx, kod, talepId, talepNo, Nothing, statu, eskiLok, lok, kullanici)
        Dim sahip As String = SahipBul(c, tx, kod)
        If String.IsNullOrWhiteSpace(sahip) Then sahip = If(String.IsNullOrWhiteSpace(s.SahipBolum), bolum, s.SahipBolum)
        LokasyonKaydet(c, tx, kod, lok, sahip.Trim().ToUpper(Kultur.Tr), kullanici, raf)
    End Sub

    ''' <summary>Var olan satırı günceller; statü ya da lokasyon değiştiyse hareket yazar ve kalıbın güncel lokasyonunu işler.</summary>
    Private Shared Sub SatirGuncelle(c As SQLiteConnection, tx As SQLiteTransaction, talepId As Integer, talepNo As String,
                                     s As TalepKalip, kullanici As String)
        Dim mevcut As DataTable = Db.Tablo("SELECT KalipKodu, KalipStatu, Lokasyon FROM TalepKalip WHERE Id = ?", s.Id)
        If mevcut.Rows.Count = 0 Then Return
        Dim kod As String = Convert.ToString(mevcut.Rows(0)("KalipKodu"))
        Dim eskiStatu As String = If(IsDBNull(mevcut.Rows(0)("KalipStatu")), Nothing, Convert.ToString(mevcut.Rows(0)("KalipStatu")))
        Dim eskiLok As String = If(IsDBNull(mevcut.Rows(0)("Lokasyon")), Nothing, Convert.ToString(mevcut.Rows(0)("Lokasyon")))
        Dim yeniStatu As String = If(String.IsNullOrWhiteSpace(s.KalipStatu), eskiStatu, s.KalipStatu)
        Dim yeniLok As String = If(String.IsNullOrWhiteSpace(s.Lokasyon), eskiLok, s.Lokasyon.Trim())
        Dim yeniRaf As String = RafDogrula(s.Raf)
        Dim eskiRafObj As Object = Skaler(c, tx, "SELECT Raf FROM KalipLokasyon WHERE Anahtar = ?", Anahtar(kod))
        Dim eskiRaf As String = If(eskiRafObj Is Nothing OrElse IsDBNull(eskiRafObj), "", Convert.ToString(eskiRafObj))
        Dim rafDegisti As Boolean = yeniRaf IsNot Nothing AndAlso yeniRaf <> eskiRaf

        Calistir(c, tx, "UPDATE TalepKalip SET Aciklama = ?, KalipStatu = ?, Lokasyon = ?, Raf = COALESCE(?, Raf), SonDegisiklik = ?, SonKullanici = ? WHERE Id = ?",
                 Bos(s.Aciklama), yeniStatu, Bos(yeniLok), Bos(yeniRaf), Db.Prm(DbType.DateTime, Date.Now), Bos(kullanici), s.Id)

        Dim eskiSahip As String = SahipBul(c, tx, kod)
        Dim yeniSahip As String = If(String.IsNullOrWhiteSpace(s.SahipBolum), Nothing, s.SahipBolum.Trim().ToUpper(Kultur.Tr))
        Dim sahipDegisti As Boolean = yeniSahip IsNot Nothing AndAlso TrMetin.Katla(yeniSahip) <> TrMetin.Katla(If(eskiSahip, ""))
        If yeniStatu <> eskiStatu OrElse yeniLok <> eskiLok Then
            HareketYaz(c, tx, kod, talepId, talepNo, eskiStatu, yeniStatu, eskiLok, yeniLok, kullanici)
        End If
        If yeniStatu <> eskiStatu OrElse yeniLok <> eskiLok OrElse sahipDegisti OrElse rafDegisti Then
            LokasyonKaydet(c, tx, kod, If(String.IsNullOrWhiteSpace(yeniLok), eskiLok, yeniLok), If(sahipDegisti, yeniSahip, Nothing), kullanici,
                           If(rafDegisti, yeniRaf, Nothing))
        End If
    End Sub

    ''' <summary>Talep statüsünü, kalıp özetini ve kapanış tarihini kalıp satırlarından yeniden hesaplar.</summary>
    Private Shared Sub OzetiYenile(c As SQLiteConnection, tx As SQLiteTransaction, talepId As Integer, mevcutStatu As String)
        Dim satirlar As New DataTable()
        Using cmd As SQLiteCommand = Db.Komut(c, tx, "SELECT KalipKodu, KalipStatu FROM TalepKalip WHERE TalepId = ? ORDER BY Id", talepId)
            Using da As New SQLiteDataAdapter(cmd)
                da.Fill(satirlar)
            End Using
        End Using
        Dim statuler As New List(Of String)()
        Dim kodlar As New List(Of String)()
        For Each r As DataRow In satirlar.Rows
            statuler.Add(Convert.ToString(r("KalipStatu")))
            kodlar.Add(Convert.ToString(r("KalipKodu")))
        Next
        Dim durum As String = TalepKurallari.TalepDurumu(statuler, mevcutStatu)
        Dim ozet As String = If(kodlar.Count = 0, "-", String.Join(", ", kodlar))
        Dim kapanis As Object = Nothing
        If durum = TalepKurallari.TalepKapandi Then
            Dim eski As Object = Skaler(c, tx, "SELECT TamamlanmaTarihi FROM Talep WHERE Id = ?", talepId)
            kapanis = If(eski Is Nothing OrElse IsDBNull(eski), Date.Today, Convert.ToDateTime(eski))
        End If
        Calistir(c, tx, "UPDATE Talep SET Statu = ?, KalipKodu = ?, TamamlanmaTarihi = ? WHERE Id = ?",
                 durum, ozet, Db.Prm(DbType.DateTime, kapanis), talepId)
    End Sub

    ''' <summary>Yeni talep + kalıp satırları: talep numarasını (T2026-00001) üretir, kimliği döner.</summary>
    Public Shared Function Ekle(t As Talep, satirlar As IEnumerable(Of TalepKalip), kullanici As String) As Integer
        Dim alanlar As String = YazilanAlanlar & ",Surum,SonDegisiklik,SonKullanici"
        Dim yer As String = String.Join(",", Enumerable.Repeat("?", 15))
        If String.IsNullOrWhiteSpace(t.KalipKodu) Then t.KalipKodu = "-"
        t.Statu = TalepKurallari.TalepAcik
        Using c As SQLiteConnection = Db.Ac()
            Using tx As SQLiteTransaction = c.BeginTransaction()
                Dim p As List(Of Object) = Degerler(t)
                p.Add(Db.Prm(DbType.Int32, 1))
                p.Add(Db.Prm(DbType.DateTime, Date.Now))
                p.Add(Db.Prm(DbType.String, Bos(kullanici)))
                Calistir(c, tx, "INSERT INTO Talep (" & alanlar & ") VALUES (" & yer & ")", p.ToArray())
                Dim id As Integer
                Using cmdId As New SQLiteCommand("SELECT last_insert_rowid()", c, tx)
                    id = Convert.ToInt32(cmdId.ExecuteScalar())
                End Using
                Dim no As String = "T" & Date.Now.Year.ToString(Kultur.Inv) & "-" & id.ToString("00000", Kultur.Inv)
                Calistir(c, tx, "UPDATE Talep SET TalepNo = ? WHERE Id = ?", no, id)
                For Each s As TalepKalip In satirlar
                    If Not String.IsNullOrWhiteSpace(s.KalipKodu) Then SatirEkle(c, tx, id, no, t.Bolum, s, kullanici)
                Next
                OzetiYenile(c, tx, id, Nothing)
                tx.Commit()
                Return id
            End Using
        End Using
    End Function

    ''' <summary>
    ''' Talebi ve kalıp satırlarını günceller (tek işlem). Başkası değiştirmiş/silmişse False; hiçbir şey yazılmaz.
    ''' Listede olmayan eski satırlar silinir, Id'si 0 olanlar eklenir.
    ''' </summary>
    Public Shared Function Guncelle(t As Talep, satirlar As IEnumerable(Of TalepKalip), kullanici As String) As Boolean
        Dim atamalar As String = String.Join(",", YazilanAlanlar.Split(","c).Select(Function(a) a & "=?")) & ",Surum=?,SonDegisiklik=?,SonKullanici=?"
        If String.IsNullOrWhiteSpace(t.KalipKodu) Then t.KalipKodu = "-"
        Using c As SQLiteConnection = Db.Ac()
            Using tx As SQLiteTransaction = c.BeginTransaction()
                Dim p As List(Of Object) = Degerler(t)
                p.Add(Db.Prm(DbType.Int32, t.Surum + 1))
                p.Add(Db.Prm(DbType.DateTime, Date.Now))
                p.Add(Db.Prm(DbType.String, Bos(kullanici)))
                p.Add(Db.Prm(DbType.Int32, t.Id))
                p.Add(Db.Prm(DbType.Int32, t.Surum))
                Dim etkilenen As Integer
                Using cmd As SQLiteCommand = Db.Komut(c, tx, "UPDATE Talep SET " & atamalar & " WHERE Id=? AND Surum=?", p.ToArray())
                    etkilenen = cmd.ExecuteNonQuery()
                End Using
                If etkilenen <> 1 Then Return False

                Dim gelenIdler As New HashSet(Of Integer)()
                For Each s As TalepKalip In satirlar
                    If s.Id > 0 Then gelenIdler.Add(s.Id)
                Next
                Dim eskiler As DataTable = Db.Tablo("SELECT Id FROM TalepKalip WHERE TalepId = ?", t.Id)
                For Each r As DataRow In eskiler.Rows
                    Dim eskiId As Integer = Convert.ToInt32(r("Id"))
                    If Not gelenIdler.Contains(eskiId) Then Calistir(c, tx, "DELETE FROM TalepKalip WHERE Id = ?", eskiId)
                Next
                For Each s As TalepKalip In satirlar
                    If s.Id > 0 Then
                        SatirGuncelle(c, tx, t.Id, t.TalepNo, s, kullanici)
                    ElseIf Not String.IsNullOrWhiteSpace(s.KalipKodu) Then
                        SatirEkle(c, tx, t.Id, t.TalepNo, t.Bolum, s, kullanici)
                    End If
                Next
                OzetiYenile(c, tx, t.Id, t.Statu)
                tx.Commit()
                Return True
            End Using
        End Using
    End Function

    ' Kalıbın işlemdeki (iptal edilmemiş) talep satırının statüsünü değiştirir: lokasyon kurala göre otomatik atanır,
    ' hareket yazılır, talep statüsü yeniden hesaplanır. Mobil / hızlı işlem için. Uygun satır yoksa InvalidOperationException.
    Public Shared Function HizliDurum(kod As String, yeniStatu As String, kullanici As String) As String
        If Not TalepKurallari.KalipStatuleri.Contains(yeniStatu) Then Throw New InvalidOperationException("Geçersiz kalıp statüsü: " & yeniStatu)
        Dim a As String = Anahtar(kod)
        Dim dt As DataTable = Db.Tablo(
            "SELECT k.Id AS SatirId, k.TalepId AS TalepId, k.KalipKodu AS KalipKodu, k.KalipStatu AS KalipStatu, k.Lokasyon AS Lokasyon, " &
            "t.TalepNo AS TalepNo, t.Bolum AS Bolum, t.Statu AS TalepStatu FROM TalepKalip k JOIN Talep t ON t.Id = k.TalepId " &
            "WHERE k.Anahtar = ? AND t.Statu <> 'İptal' AND k.KalipStatu IN ('Planlandı','Sevkediliyor','Teslim Alındı','İşleniyor') " &
            "ORDER BY k.Id DESC LIMIT 1", a)
        If dt.Rows.Count = 0 Then Throw New InvalidOperationException(kod & " için işlemde olan (açık) bir talep bulunamadı.")
        Dim r As DataRow = dt.Rows(0)
        Dim satirId As Integer = Convert.ToInt32(r("SatirId"))
        Dim talepId As Integer = Convert.ToInt32(r("TalepId"))
        Dim talepNo As String = Convert.ToString(r("TalepNo"))
        Dim eskiStatu As String = Convert.ToString(r("KalipStatu"))
        Dim eskiLok As String = If(IsDBNull(r("Lokasyon")), Nothing, Convert.ToString(r("Lokasyon")))
        Dim kodTam As String = Convert.ToString(r("KalipKodu"))
        Dim lok As String = TalepKurallari.LokasyonBul(yeniStatu, Convert.ToString(r("Bolum")))
        Dim yeniTalepStatu As String
        Using c As SQLiteConnection = Db.Ac()
            Using tx As SQLiteTransaction = c.BeginTransaction()
                Calistir(c, tx, "UPDATE TalepKalip SET KalipStatu = ?, Lokasyon = ?, SonDegisiklik = ?, SonKullanici = ? WHERE Id = ?",
                         yeniStatu, Bos(lok), Db.Prm(DbType.DateTime, Date.Now), Bos(kullanici), satirId)
                HareketYaz(c, tx, kodTam, talepId, talepNo, eskiStatu, yeniStatu, eskiLok, lok, kullanici)
                LokasyonKaydet(c, tx, kodTam, lok, Nothing, kullanici)
                OzetiYenile(c, tx, talepId, Convert.ToString(r("TalepStatu")))
                yeniTalepStatu = Convert.ToString(Skaler(c, tx, "SELECT Statu FROM Talep WHERE Id = ?", talepId))
                tx.Commit()
            End Using
        End Using
        Return kodTam & ": " & eskiStatu & " → " & yeniStatu & " (lokasyon: " & lok & "). Talep " & talepNo & " statüsü: " & yeniTalepStatu
    End Function

    Public Shared Function Sil(id As Integer) As Boolean
        Using c As SQLiteConnection = Db.Ac()
            Using tx As SQLiteTransaction = c.BeginTransaction()
                Calistir(c, tx, "DELETE FROM TalepKalip WHERE TalepId = ?", id)
                Dim n As Integer
                Using cmd As SQLiteCommand = Db.Komut(c, tx, "DELETE FROM Talep WHERE Id = ?", id)
                    n = cmd.ExecuteNonQuery()
                End Using
                tx.Commit()
                Return n = 1
            End Using
        End Using
    End Function
End Class
