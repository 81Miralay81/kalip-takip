Imports System.Collections.Generic
Imports System.Data
Imports System.Linq

Public Enum KolonTipi
    Metin
    Tarih
    Tam
    Ondalik
End Enum

Public Class KolonTanimi
    Public ReadOnly Property Alan As String
    Public ReadOnly Property Baslik As String
    Public ReadOnly Property Tip As KolonTipi
    Public ReadOnly Property Genislik As Integer

    Public Sub New(alan As String, baslik As String, tip As KolonTipi, genislik As Integer)
        Me.Alan = alan
        Me.Baslik = baslik
        Me.Tip = tip
        Me.Genislik = genislik
    End Sub
End Class

''' <summary>Bir kalıp kaydı (Kalip tablosunun bir satırı).</summary>
Public Class Kalip
    Public Property Id As Integer
    Public Property ParcaKodu As String
    Public Property KalipTuru As String
    Public Property TepeKodu As String
    Public Property ParcaAdi As String
    Public Property GelisTarihi As Date?
    Public Property TashihSayi As Integer?
    Public Property BitisTarihi As Date?
    Public Property Hafta As String
    Public Property Tur As String
    Public Property DovmeAdeti As Double?
    Public Property CncNo As Integer?
    Public Property Aciklama As String
    Public Property IslemSuresi As Double?
    Public Property Ayar As Double?
    Public Property Bakim As Double?
    Public Property ElemanEksikligi As Double?
    Public Property Elektrik As Double?
    Public Property KalipAriza As Double?
    Public Property Takim As Double?
    Public Property Ariza As Double?
    Public Property ToplamZaman As Double?
    ''' <summary>Eşzamanlı düzenleme kontrolü için sürüm numarası.</summary>
    Public Property Surum As Integer
    Public Property SonDegisiklik As Date?
    Public Property SonKullanici As String

    Public Shared Function SatirdanOku(r As DataRow) As Kalip
        Dim k As New Kalip()
        k.Id = Convert.ToInt32(r("Id"))
        k.ParcaKodu = M(r("ParcaKodu"))
        k.KalipTuru = M(r("KalipTuru"))
        k.TepeKodu = M(r("TepeKodu"))
        k.ParcaAdi = M(r("ParcaAdi"))
        k.GelisTarihi = T(r("GelisTarihi"))
        k.TashihSayi = I(r("TashihSayi"))
        k.BitisTarihi = T(r("BitisTarihi"))
        k.Hafta = M(r("Hafta"))
        k.Tur = M(r("Tur"))
        k.DovmeAdeti = D(r("DovmeAdeti"))
        k.CncNo = I(r("CncNo"))
        k.Aciklama = M(r("Aciklama"))
        k.IslemSuresi = D(r("IslemSuresi"))
        k.Ayar = D(r("Ayar"))
        k.Bakim = D(r("Bakim"))
        k.ElemanEksikligi = D(r("ElemanEksikligi"))
        k.Elektrik = D(r("Elektrik"))
        k.KalipAriza = D(r("KalipAriza"))
        k.Takim = D(r("Takim"))
        k.Ariza = D(r("Ariza"))
        k.ToplamZaman = D(r("ToplamZaman"))
        k.Surum = If(IsDBNull(r("Surum")), 1, Convert.ToInt32(r("Surum")))
        k.SonDegisiklik = T(r("SonDegisiklik"))
        k.SonKullanici = M(r("SonKullanici"))
        Return k
    End Function

    Private Shared Function M(o As Object) As String
        If o Is Nothing OrElse IsDBNull(o) Then Return Nothing
        Return Convert.ToString(o)
    End Function

    Private Shared Function T(o As Object) As Date?
        If o Is Nothing OrElse IsDBNull(o) Then Return Nothing
        Return Convert.ToDateTime(o)
    End Function

    Private Shared Function D(o As Object) As Double?
        If o Is Nothing OrElse IsDBNull(o) Then Return Nothing
        Return Convert.ToDouble(o)
    End Function

    Private Shared Function I(o As Object) As Integer?
        If o Is Nothing OrElse IsDBNull(o) Then Return Nothing
        Return Convert.ToInt32(o)
    End Function
End Class

''' <summary>Uygulama kullanıcısı ve yetkileri.</summary>
Public Class Kullanici
    Public Property Id As Integer
    Public Property KullaniciAdi As String
    Public Property IsAdmin As Boolean
    Public Property Aktif As Boolean = True
    ''' <summary>Kullanıcının bölümü (talep açarken otomatik gelir).</summary>
    Public Property Bolum As String
    ''' <summary>Kullanıcının bölümleri (lokasyon grupları). Bolum = ilki.</summary>
    Public ReadOnly Property Bolumler As New List(Of String)()
    ''' <summary>True ise tüm bölümleri / lokasyonları görür (bölümle kısıtlanmaz).</summary>
    Public Property TumBolumler As Boolean = True
    ''' <summary>True ise Kaydet, Bolumler / TumBolumler değerlerini de yazar (False iken olduğu gibi bırakır).</summary>
    Public Property BolumlerAyarli As Boolean
    ''' <summary>Şifresiz, salt-okunur giriş.</summary>
    Public Property Misafir As Boolean

    ''' <summary>Verilen bölümü görebilir mi? (yönetici, misafir, tüm bölümler ya da listede olan)</summary>
    Public Function BolumGorunur(bolum As String) As Boolean
        If Misafir OrElse IsAdmin OrElse TumBolumler Then Return True
        Dim b As String = TrMetin.Katla(If(bolum, "").Trim())
        For Each x As String In Bolumler
            If TrMetin.Katla(x) = b Then Return True
        Next
        Return False
    End Function
    Public ReadOnly Property Izinler As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)

    Public Function IzinVar(anahtar As String) As Boolean
        If Misafir Then Return False
        Return IsAdmin OrElse Izinler.Contains(anahtar)
    End Function

    ''' <summary>Salt okunur görme: yönetici, misafir ya da ilgili 'görebilir' yetkisi olan kullanıcı.</summary>
    Public Function GorebilirMi(anahtar As String) As Boolean
        Return Misafir OrElse IsAdmin OrElse Izinler.Contains(anahtar)
    End Function
End Class

''' <summary>Yetki tanımı: hangi grupta (portal/modül), hangi anahtar, ekranda adı.</summary>
Public Class IzinTanimi
    Public ReadOnly Property Grup As String
    Public ReadOnly Property Anahtar As String
    Public ReadOnly Property Ad As String
    ''' <summary>True ise grubun "görebilir / açabilir" yetkisidir; kapalıyken grubun diğer yetkileri geçersizdir.</summary>
    Public ReadOnly Property GrupAnahtari As Boolean

    Public Sub New(grup As String, anahtar As String, ad As String, Optional grupAnahtari As Boolean = False)
        Me.Grup = grup
        Me.Anahtar = anahtar
        Me.Ad = ad
        Me.GrupAnahtari = grupAnahtari
    End Sub
End Class

''' <summary>Yetki anahtarları, portal (modül) bazında gruplanmıştır.</summary>
Public NotInheritable Class Izin
    Private Sub New()
    End Sub

    Public Const KalipGor As String = "kalip_gor"         ' Kalıp Takip portalını açabilir / sorgu yapabilir
    Public Const Ekle As String = "ekle"
    Public Const Guncelle As String = "guncelle"
    Public Const Sil As String = "sil"
    Public Const Excel As String = "excel"
    Public Const Tashih As String = "tashih"
    Public Const Istatistik As String = "istatistik"

    Public Const TalepGor As String = "talep_gor"         ' Talep portalını açabilir / sorgu yapabilir
    Public Const TalepEkle As String = "talep_ekle"       ' Yeni talep açabilir
    Public Const TalepGuncelle As String = "talep_guncelle"
    Public Const TalepSil As String = "talep_sil"
    Public Const TalepListe As String = "talep_liste"     ' "Talepler" (tüm liste) sayfasını görebilir
    Public Const TalepExcel As String = "talep_excel"
    Public Const TalepKalipDurum As String = "talep_kalip_durum"  ' Kalıp statüsü / lokasyon değiştirme
    Public Const TalepGonder As String = "talep_gonder"           ' Talebi gönderme / geri dönen talebi kapatma ya da yeniden gönderme
    Public Const TalepTeslim As String = "talep_teslim"           ' Gelen talebi teslim alma / eksikleri geri bildirme
    Public Const RafKaldir As String = "raf_kaldir"               ' Teslim alınan kalıba raf girme (grup lideri)

    Public Const GrupKalip As String = "Kalıp Takip"
    Public Const KalipListeGor As String = "kl_gor"         ' Kalıp Listesi portalını açabilir / sorgulayabilir
    Public Const KalipListeEkle As String = "kl_ekle"       ' Kalıp ekleme / düzenleme / Excel'den yükleme
    Public Const KalipListeExcel As String = "kl_excel"
    Public Const KalipListeDuzenle As String = "kl_duzenle"  ' Var olan kalıbın bilgilerini düzenleme (KRİTİK)
    Public Const KalipListeYukle As String = "kl_yukle"      ' Excel / CSV'den toplu kalıp yükleme (KRİTİK)
    Public Const KalipListeSil As String = "kl_sil"          ' Kalıbı listeden silme, toplu temizlik (KRİTİK)
    Public Const GrupKalipListe As String = "Kalıp Listesi"
    Public Const GrupTalep As String = "Talep"

    Public Shared ReadOnly Tanimlar As IzinTanimi() = {
        New IzinTanimi(GrupKalip, KalipGor, "Portalı açabilir / kayıtları görüp sorgulayabilir", True),
        New IzinTanimi(GrupKalip, Ekle, "Yeni kayıt ekleme"),
        New IzinTanimi(GrupKalip, Guncelle, "Kayıt güncelleme"),
        New IzinTanimi(GrupKalip, Sil, "⚠ KRİTİK: Kalıp Takip kaydı silme"),
        New IzinTanimi(GrupKalip, Tashih, "Güncelle + Tashih kaydı / Temizle"),
        New IzinTanimi(GrupKalip, Excel, "Excel'e aktarma"),
        New IzinTanimi(GrupKalip, Istatistik, "İstatistik raporu"),
        New IzinTanimi(GrupTalep, TalepGor, "Portalı açabilir / talepleri görüp sorgulayabilir", True),
        New IzinTanimi(GrupTalep, TalepEkle, "Yeni talep açma"),
        New IzinTanimi(GrupTalep, TalepGuncelle, "Talep güncelleme (statü, sonuç...)"),
        New IzinTanimi(GrupTalep, TalepSil, "⚠ KRİTİK: Talep silme"),
        New IzinTanimi(GrupTalep, TalepListe, "'Talepler' (tüm liste) sayfasını görme"),
        New IzinTanimi(GrupTalep, TalepExcel, "Talep listesini Excel'e aktarma"),
        New IzinTanimi(GrupTalep, TalepKalipDurum, "Kalıp statüsü ve lokasyonunu değiştirme (kalıphane / grup lideri)"),
        New IzinTanimi(GrupTalep, TalepGonder, "Talebi gönderme; geri dönen talebi kapatma / yeniden gönderme"),
        New IzinTanimi(GrupTalep, TalepTeslim, "Gelen talebi teslim alma; gelmeyen kalıpları geri bildirme"),
        New IzinTanimi(GrupTalep, RafKaldir, "Teslim alınan kalıbı rafa kaldırma (raf girme)"),
        New IzinTanimi(GrupKalipListe, KalipListeGor, "Portalı açabilir / kalıpları ve geçmişini görüp sorgulayabilir", True),
        New IzinTanimi(GrupKalipListe, KalipListeEkle, "Yeni kalıp ekleme; raf, bölüm ataması ve hızlı işlem"),
        New IzinTanimi(GrupKalipListe, KalipListeDuzenle, "⚠ KRİTİK: Var olan kalıbı düzenleme"),
        New IzinTanimi(GrupKalipListe, KalipListeYukle, "⚠ KRİTİK: Excel / CSV'den toplu kalıp yükleme"),
        New IzinTanimi(GrupKalipListe, KalipListeSil, "⚠ KRİTİK: Kalıp silme (tek tek ve toplu temizlik)"),
        New IzinTanimi(GrupKalipListe, KalipListeExcel, "Kalıp listesini Excel'e aktarma")
    }

    ''' <summary>Kritik yetkiler: yönetici her girişte bu yetkileri taşıyan kullanıcılar için uyarılır.</summary>
    Public Shared ReadOnly Kritik As HashSet(Of String) = New HashSet(Of String)(New String() {Sil, TalepSil, KalipListeDuzenle, KalipListeYukle, KalipListeSil}, StringComparer.OrdinalIgnoreCase)

    ''' <summary>Eski kullanım için (anahtar, ad) çiftleri.</summary>
    Public Shared ReadOnly Liste As KeyValuePair(Of String, String)() =
        Tanimlar.Select(Function(t) New KeyValuePair(Of String, String)(t.Anahtar, t.Ad)).ToArray()
End Class
