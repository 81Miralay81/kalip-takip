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

    Private Shared Function Kolon(grup As String) As String
        If grup = GrupKalipTuru Then Return "KalipTuru"
        If grup = GrupTur Then Return "Tur"
        Throw New InvalidOperationException("Bilinmeyen liste: " & grup)
    End Function

    ' Kalıp türleri sahip bölüme bağlıdır (Bolum boşsa "ortak": her bölümde görünür). Tür (Tashih/İlave/Hurda...) genel kalır.
    Private Shared Function BolumKullan(grup As String, bolum As String) As Boolean
        Return grup = GrupKalipTuru AndAlso Not String.IsNullOrWhiteSpace(bolum)
    End Function

    Private Shared Function BolumKosuluListe(grup As String, bolum As String) As String
        If grup <> GrupKalipTuru Then Return ""
        If String.IsNullOrWhiteSpace(bolum) Then Return " AND (Bolum IS NULL OR TRIM(Bolum) = '')"
        Return " AND KATLA(COALESCE(Bolum, '')) = ?"
    End Function

    ''' <summary>Seçim listeleri: ortak değerler + bu bölüme tanımlı değerler.</summary>
    Public Shared Function GetirBolum(grup As String, bolum As String) As List(Of String)
        Dim sql As String = "SELECT Deger FROM Liste WHERE Grup = ?"
        Dim dt As DataTable
        If grup = GrupKalipTuru Then
            dt = Db.Tablo(sql & " AND (Bolum IS NULL OR TRIM(Bolum) = '' OR KATLA(Bolum) = ?) ORDER BY Sira, Id", grup, TrMetin.Katla(If(bolum, "").Trim()))
        Else
            dt = Db.Tablo(sql & " ORDER BY Sira, Id", grup)
        End If
        Return dt.Rows.Cast(Of DataRow)().Select(Function(r) Convert.ToString(r("Deger"))).ToList()
    End Function

    Private Shared Function KullanimKosulu(grup As String, bolum As String) As String
        If Not BolumKullan(grup, bolum) Then Return ""
        Return " AND KATLA(ParcaKodu) IN (SELECT Anahtar FROM KalipLokasyon WHERE KATLA(COALESCE(SahipBolum, '')) = ?)"
    End Function

    ''' <summary>Yönetim ekranı: seçili bölümün değerleri (bölüm boşsa ortak değerler) + kayıtlarda kullanılıp listede olmayanlar. Sütunlar: Deger, Kullanim, Listede.</summary>
    Public Shared Function Listele(grup As String, bolum As String) As DataTable
        Dim k As String = Kolon(grup)
        Dim sonuc As New DataTable()
        sonuc.Columns.Add("Deger", GetType(String))
        sonuc.Columns.Add("Kullanim", GetType(Long))
        sonuc.Columns.Add("Listede", GetType(Boolean))
        Dim kullanim As New Dictionary(Of String, Long)(StringComparer.OrdinalIgnoreCase)
        Dim ks As String = KullanimKosulu(grup, bolum)
        Dim dtK As DataTable
        Dim sqlK As String = "SELECT TRIM(" & k & ") AS D, COUNT(*) AS N FROM Kalip WHERE " & k & " IS NOT NULL AND TRIM(" & k & ") <> ''" & ks & " GROUP BY TRIM(" & k & ")"
        If ks.Length > 0 Then dtK = Db.Tablo(sqlK, TrMetin.Katla(bolum.Trim())) Else dtK = Db.Tablo(sqlK)
        For Each r As DataRow In dtK.Rows
            Dim d As String = Convert.ToString(r("D"))
            Dim n As Long = Convert.ToInt64(r("N"))
            If kullanim.ContainsKey(d) Then kullanim(d) += n Else kullanim(d) = n
        Next
        Dim eklenen As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
        Dim bk As String = BolumKosuluListe(grup, bolum)
        Dim dtL As DataTable
        If bk.Contains("?") Then
            dtL = Db.Tablo("SELECT Deger FROM Liste WHERE Grup = ?" & bk & " ORDER BY Sira, Id", grup, TrMetin.Katla(bolum.Trim()))
        Else
            dtL = Db.Tablo("SELECT Deger FROM Liste WHERE Grup = ?" & bk & " ORDER BY Sira, Id", grup)
        End If
        For Each r As DataRow In dtL.Rows
            Dim d As String = Convert.ToString(r("Deger"))
            If Not eklenen.Add(d) Then Continue For
            Dim n As Long = 0
            kullanim.TryGetValue(d, n)
            sonuc.Rows.Add(d, n, True)
        Next
        For Each kv As KeyValuePair(Of String, Long) In kullanim
            If eklenen.Add(kv.Key) Then sonuc.Rows.Add(kv.Key, kv.Value, False)
        Next
        Return sonuc
    End Function

    Public Shared Sub Ekle(grup As String, deger As String, bolum As String)
        Kolon(grup)
        Dim d As String = If(deger, "").Trim()
        If d.Length = 0 Then Throw New InvalidOperationException("Değer boş olamaz.")
        Dim b As String = If(BolumKullan(grup, bolum), bolum.Trim().ToUpper(Kultur.Tr), Nothing)
        For Each r As DataRow In Listele(grup, bolum).Rows
            If Convert.ToBoolean(r("Listede")) AndAlso String.Equals(Convert.ToString(r("Deger")).Trim(), d, StringComparison.OrdinalIgnoreCase) Then Throw New InvalidOperationException("'" & d & "' zaten listede var.")
        Next
        Dim sira As Object = Db.Skaler("SELECT COALESCE(MAX(Sira), 0) + 1 FROM Liste WHERE Grup = ?", grup)
        Using c As SQLiteConnection = Db.Ac()
            Using cmd As SQLiteCommand = Db.Komut(c, Nothing, "INSERT INTO Liste (Grup, Deger, Sira, Bolum) VALUES (?, ?, ?, ?)", grup, d, Convert.ToInt32(sira), If(b Is Nothing, CType(DBNull.Value, Object), b))
                cmd.ExecuteNonQuery()
            End Using
        End Using
    End Sub

    ''' <summary>Değeri yeniden adlandırır; bu değeri kullanan kalıp kayıtları (bölüm seçiliyse yalnız o bölümün kalıpları) da güncellenir. Güncellenen kayıt sayısını döndürür.</summary>
    Public Shared Function Adlandir(grup As String, eski As String, yeni As String, bolum As String) As Integer
        Dim k As String = Kolon(grup)
        Dim e As String = If(eski, "").Trim()
        Dim y As String = If(yeni, "").Trim()
        If y.Length = 0 Then Throw New InvalidOperationException("Yeni ad boş olamaz.")
        If e.Length = 0 Then Throw New InvalidOperationException("Eski ad bulunamadı.")
        For Each r As DataRow In Listele(grup, bolum).Rows
            Dim x As String = Convert.ToString(r("Deger")).Trim()
            If Convert.ToBoolean(r("Listede")) AndAlso String.Equals(x, y, StringComparison.OrdinalIgnoreCase) AndAlso Not String.Equals(x, e, StringComparison.OrdinalIgnoreCase) Then
                Throw New InvalidOperationException("'" & y & "' adı zaten listede var.")
            End If
        Next
        Dim bk As Boolean = BolumKullan(grup, bolum)
        Dim bKatla As String = If(bk, TrMetin.Katla(bolum.Trim()), "")
        Dim sayi As Integer = 0
        Using c As SQLiteConnection = Db.Ac()
            Using tx As SQLiteTransaction = c.BeginTransaction()
                Dim n As Integer
                If grup = GrupKalipTuru Then
                    If bk Then
                        Using cmd As SQLiteCommand = Db.Komut(c, tx, "UPDATE Liste SET Deger = ? WHERE Grup = ? AND Deger = ? AND KATLA(COALESCE(Bolum, '')) = ?", y, grup, e, bKatla)
                            n = cmd.ExecuteNonQuery()
                        End Using
                    Else
                        Using cmd As SQLiteCommand = Db.Komut(c, tx, "UPDATE Liste SET Deger = ? WHERE Grup = ? AND Deger = ? AND (Bolum IS NULL OR TRIM(Bolum) = '')", y, grup, e)
                            n = cmd.ExecuteNonQuery()
                        End Using
                    End If
                Else
                    Using cmd As SQLiteCommand = Db.Komut(c, tx, "UPDATE Liste SET Deger = ? WHERE Grup = ? AND Deger = ?", y, grup, e)
                        n = cmd.ExecuteNonQuery()
                    End Using
                End If
                If n = 0 Then
                    Dim sira As Integer
                    Using cmd As SQLiteCommand = Db.Komut(c, tx, "SELECT COALESCE(MAX(Sira), 0) + 1 FROM Liste WHERE Grup = ?", grup)
                        sira = Convert.ToInt32(cmd.ExecuteScalar())
                    End Using
                    Using cmd As SQLiteCommand = Db.Komut(c, tx, "INSERT INTO Liste (Grup, Deger, Sira, Bolum) VALUES (?, ?, ?, ?)", grup, y, sira, If(bk, CType(bolum.Trim().ToUpper(Kultur.Tr), Object), CType(DBNull.Value, Object)))
                        cmd.ExecuteNonQuery()
                    End Using
                End If
                If bk Then
                    Using cmd As SQLiteCommand = Db.Komut(c, tx, "UPDATE Kalip SET " & k & " = ? WHERE KATLA(TRIM(" & k & ")) = ? AND KATLA(ParcaKodu) IN (SELECT Anahtar FROM KalipLokasyon WHERE KATLA(COALESCE(SahipBolum, '')) = ?)", y, TrMetin.Katla(e), bKatla)
                        sayi = cmd.ExecuteNonQuery()
                    End Using
                    Using cmd As SQLiteCommand = Db.Komut(c, tx, "UPDATE KalipTanim SET KalipTuru = ? WHERE KATLA(TRIM(COALESCE(KalipTuru, ''))) = ? AND Anahtar IN (SELECT Anahtar FROM KalipLokasyon WHERE KATLA(COALESCE(SahipBolum, '')) = ?)", y, TrMetin.Katla(e), bKatla)
                        sayi += cmd.ExecuteNonQuery()
                    End Using
                Else
                    Using cmd As SQLiteCommand = Db.Komut(c, tx, "UPDATE Kalip SET " & k & " = ? WHERE KATLA(TRIM(" & k & ")) = ?", y, TrMetin.Katla(e))
                        sayi = cmd.ExecuteNonQuery()
                    End Using
                    If grup = GrupKalipTuru Then
                        Using cmd As SQLiteCommand = Db.Komut(c, tx, "UPDATE KalipTanim SET KalipTuru = ? WHERE KATLA(TRIM(COALESCE(KalipTuru, ''))) = ?", y, TrMetin.Katla(e))
                            sayi += cmd.ExecuteNonQuery()
                        End Using
                    End If
                End If
                tx.Commit()
            End Using
        End Using
        Return sayi
    End Function

    ''' <summary>Değeri hem listeden hem kayıtlardan kaldırır: o değeri taşıyan kalıp kayıtlarının alanı boşaltılır (bölüm seçiliyse yalnız o bölümün kalıpları). Etkilenen kayıt sayısını döndürür.</summary>
    Public Shared Function Temizle(grup As String, deger As String, bolum As String) As Integer
        Dim k As String = Kolon(grup)
        Dim d As String = If(deger, "").Trim()
        If d.Length = 0 Then Throw New InvalidOperationException("Değer boş olamaz.")
        Dim bk As Boolean = BolumKullan(grup, bolum)
        Dim bKatla As String = If(bk, TrMetin.Katla(bolum.Trim()), "")
        Dim sayi As Integer = 0
        Using c As SQLiteConnection = Db.Ac()
            Using tx As SQLiteTransaction = c.BeginTransaction()
                If bk Then
                    Using cmd As SQLiteCommand = Db.Komut(c, tx, "UPDATE Kalip SET " & k & " = NULL WHERE KATLA(TRIM(" & k & ")) = ? AND KATLA(ParcaKodu) IN (SELECT Anahtar FROM KalipLokasyon WHERE KATLA(COALESCE(SahipBolum, '')) = ?)", TrMetin.Katla(d), bKatla)
                        sayi = cmd.ExecuteNonQuery()
                    End Using
                    Using cmd As SQLiteCommand = Db.Komut(c, tx, "UPDATE KalipTanim SET KalipTuru = NULL WHERE KATLA(TRIM(COALESCE(KalipTuru, ''))) = ? AND Anahtar IN (SELECT Anahtar FROM KalipLokasyon WHERE KATLA(COALESCE(SahipBolum, '')) = ?)", TrMetin.Katla(d), bKatla)
                        sayi += cmd.ExecuteNonQuery()
                    End Using
                    Using cmd As SQLiteCommand = Db.Komut(c, tx, "DELETE FROM Liste WHERE Grup = ? AND Deger = ? AND KATLA(COALESCE(Bolum, '')) = ?", grup, d, bKatla)
                        cmd.ExecuteNonQuery()
                    End Using
                Else
                    Using cmd As SQLiteCommand = Db.Komut(c, tx, "UPDATE Kalip SET " & k & " = NULL WHERE KATLA(TRIM(" & k & ")) = ?", TrMetin.Katla(d))
                        sayi = cmd.ExecuteNonQuery()
                    End Using
                    If grup = GrupKalipTuru Then
                        Using cmd As SQLiteCommand = Db.Komut(c, tx, "UPDATE KalipTanim SET KalipTuru = NULL WHERE KATLA(TRIM(COALESCE(KalipTuru, ''))) = ?", TrMetin.Katla(d))
                            sayi += cmd.ExecuteNonQuery()
                        End Using
                        Using cmd As SQLiteCommand = Db.Komut(c, tx, "DELETE FROM Liste WHERE Grup = ? AND Deger = ? AND (Bolum IS NULL OR TRIM(Bolum) = '')", grup, d)
                            cmd.ExecuteNonQuery()
                        End Using
                    Else
                        Using cmd As SQLiteCommand = Db.Komut(c, tx, "DELETE FROM Liste WHERE Grup = ? AND Deger = ?", grup, d)
                            cmd.ExecuteNonQuery()
                        End Using
                    End If
                End If
                tx.Commit()
            End Using
        End Using
        Return sayi
    End Function

    ''' <summary>Değeri listeden siler. Kayıtlarda kullanılan değer silinemez (önce adını değiştirin).</summary>
    Public Shared Sub Sil(grup As String, deger As String, bolum As String)
        Dim d As String = If(deger, "").Trim()
        For Each r As DataRow In Listele(grup, bolum).Rows
            If String.Equals(Convert.ToString(r("Deger")), d, StringComparison.OrdinalIgnoreCase) AndAlso Convert.ToInt64(r("Kullanim")) > 0 Then
                Throw New InvalidOperationException("'" & d & "' " & Convert.ToInt64(r("Kullanim")).ToString() & " kalıp kaydında kullanılıyor; silinemez. Önce adını değiştirin.")
            End If
        Next
        Using c As SQLiteConnection = Db.Ac()
            If grup = GrupKalipTuru Then
                If BolumKullan(grup, bolum) Then
                    Using cmd As SQLiteCommand = Db.Komut(c, Nothing, "DELETE FROM Liste WHERE Grup = ? AND Deger = ? AND KATLA(COALESCE(Bolum, '')) = ?", grup, d, TrMetin.Katla(bolum.Trim()))
                        cmd.ExecuteNonQuery()
                    End Using
                Else
                    Using cmd As SQLiteCommand = Db.Komut(c, Nothing, "DELETE FROM Liste WHERE Grup = ? AND Deger = ? AND (Bolum IS NULL OR TRIM(Bolum) = '')", grup, d)
                        cmd.ExecuteNonQuery()
                    End Using
                End If
            Else
                Using cmd As SQLiteCommand = Db.Komut(c, Nothing, "DELETE FROM Liste WHERE Grup = ? AND Deger = ?", grup, d)
                    cmd.ExecuteNonQuery()
                End Using
            End If
        End Using
    End Sub
End Class
