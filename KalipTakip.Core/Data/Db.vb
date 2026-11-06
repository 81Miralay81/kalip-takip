Imports System.Collections.Generic
Imports System.Configuration
Imports System.Data
Imports System.Data.SQLite
Imports System.IO
Imports System.Linq
Imports System.Text

Public Enum VeritabaniDurumu
    ''' <summary>Veritabanı dosyası henüz yok (ilk kurulum gerekir).</summary>
    DosyaYok
    ''' <summary>Dosya var ama uygulama tabloları kurulmamış.</summary>
    SemaYok
    Hazir
    ''' <summary>Dosya açılamadı (ağ yolu yok, yetki yok, bozuk dosya...).</summary>
    Hata
End Enum

''' <summary>
''' Taşınabilir (kurulumsuz) SQLite veritabanı: tek dosya, sunucu yok.
''' Her işlemde bağlantı açılıp kapatılır; ağ paylaşımında birden çok kullanıcı için en güvenli yöntem budur.
''' Yazma çakışmalarında SQLite bekler (BusyTimeout), kayıt düzeyinde ise Surum alanıyla çakışma yakalanır.
''' </summary>
Public NotInheritable Class Db
    Private Sub New()
    End Sub

    Private Shared _yol As String

    ''' <summary>Gizli ayar kaynağı (örn. şifreli depo). Verilirse VeritabaniYolu önce buradan okunur; boşsa .config'e bakılır.</summary>
    Public Shared Property AyarOkuyucu As Func(Of String, String)

    Shared Sub New()
        Try
            SQLiteFunction.RegisterFunction(GetType(KatlaFonksiyonu))
        Catch
        End Try
    End Sub

    ''' <summary>Veritabanı dosyasının tam yolu (App.config > VeritabaniYolu, boşsa exe klasöründe KalipTakip.db).</summary>
    Public Shared ReadOnly Property Yol As String
        Get
            If _yol Is Nothing Then
                Dim ayar As String = Nothing
                If AyarOkuyucu IsNot Nothing Then
                    Try
                        ayar = AyarOkuyucu("VeritabaniYolu")
                    Catch
                    End Try
                End If
                If String.IsNullOrWhiteSpace(ayar) Then ayar = ConfigurationManager.AppSettings("VeritabaniYolu")
                Dim temel As String = AppDomain.CurrentDomain.BaseDirectory
                If String.IsNullOrWhiteSpace(ayar) Then
                    _yol = Path.Combine(temel, "KalipTakip.db")
                ElseIf Path.IsPathRooted(ayar.Trim()) Then
                    _yol = ayar.Trim()
                Else
                    _yol = Path.GetFullPath(Path.Combine(temel, ayar.Trim()))
                End If
            End If
            Return _yol
        End Get
    End Property

    Private Shared Function YolDosyasi() As String
        Return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "KalipTakip", "veritabani.txt")
    End Function

    Private Shared Function KayitliYol() As String
        Try
            Dim d As String = YolDosyasi()
            If File.Exists(d) Then
                Dim y As String = File.ReadAllText(d).Trim()
                If y.Length > 0 Then Return y
            End If
        Catch
        End Try
        Return Nothing
    End Function

    ' Hazır veritabanı dosyasını seçer ve bu bilgisayar için hatırlar (her derlemede sorulmaz).
    Public Shared Sub YoluKaydet(yol As String)
        Dim d As String = YolDosyasi()
        Directory.CreateDirectory(Path.GetDirectoryName(d))
        File.WriteAllText(d, yol.Trim())
        _yol = yol.Trim()
        _etkinYol = Nothing
    End Sub

    ''' <summary>SQLite'in gercekten acabildigi yol bicimi (ag yollarinda farkli bicimler denenir).</summary>
    Private Shared _etkinYol As String

    Public Shared ReadOnly Property ConnString As String
        Get
            Return "Data Source=" & If(_etkinYol, Yol) & ";Version=3;Pooling=False;Journal Mode=Delete;Synchronous=Full;" &
                   "Default Timeout=30;BusyTimeout=15000;"
        End Get
    End Property

    ''' <summary>Dosyaya bir kez erişip kapatır (ağ yolunu önceden uyandırır). Hata vermez.</summary>
    Public Shared Sub SicakTut()
        Try
            If File.Exists(Yol) Then
                Using c As SQLiteConnection = Ac()
                End Using
            End If
        Catch
        End Try
    End Sub

    Public Shared Sub SicakBirak()
    End Sub

    ''' <summary>Ayni dosya icin SQLite'a verilebilecek yol bicimleri (ag yolu sorunlarina karsi).</summary>
    Private Shared Function YolAdaylari() As List(Of String)
        Dim l As New List(Of String)()
        l.Add(Yol)
        If Yol.StartsWith("\\", StringComparison.Ordinal) AndAlso Not Yol.StartsWith("\\?\", StringComparison.Ordinal) Then
            l.Add("\\" & Yol)
            l.Add("\\?\UNC\" & Yol.Substring(2))
        End If
        Return l
    End Function

    ''' <summary>Dosya / tablo durumunu belirler. Hata olursa mesaja nedeni yazilir.</summary>
    Public Shared Function DurumBul(ByRef mesaj As String) As VeritabaniDurumu
        mesaj = Nothing
        If Not File.Exists(Yol) Then Return VeritabaniDurumu.DosyaYok
        Dim ilkHata As Exception = Nothing
        For Each aday As String In YolAdaylari()
            _etkinYol = aday
            Try
                Using c As SQLiteConnection = Ac()
                    Using cmd As New SQLiteCommand("SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='Kalip'", c)
                        If Convert.ToInt32(cmd.ExecuteScalar()) = 1 Then Return VeritabaniDurumu.Hazir
                        Return VeritabaniDurumu.SemaYok
                    End Using
                End Using
            Catch ex As Exception
                If ilkHata Is Nothing Then ilkHata = ex
            End Try
        Next
        _etkinYol = Nothing
        mesaj = AnlasilirMesaj(ilkHata)
        Return VeritabaniDurumu.Hata
    End Function

    Public Shared Function Ac() As SQLiteConnection
        Dim c As New SQLiteConnection(ConnString)
        Try
            c.Open()
        Catch
            c.Dispose()
            Throw
        End Try
        Return c
    End Function

    ''' <summary>
    ''' Konumsal (?) parametreli komut üretir; her ? sırayla @p0, @p1... olur (tırnak içindeki ? dokunulmaz).
    ''' Nothing değerler NULL yazılır. SQLiteParameter verilirse tipi korunur.
    ''' </summary>
    Public Shared Function Komut(c As SQLiteConnection, tx As SQLiteTransaction, sql As String, ParamArray p() As Object) As SQLiteCommand
        Dim sb As New StringBuilder(sql.Length + 16)
        Dim n As Integer = 0
        Dim tirnak As Boolean = False
        For Each ch As Char In sql
            If ch = "'"c Then tirnak = Not tirnak
            If ch = "?"c AndAlso Not tirnak Then
                sb.Append("@p").Append(n.ToString(Kultur.Inv))
                n += 1
            Else
                sb.Append(ch)
            End If
        Next

        Dim cmd As New SQLiteCommand(sb.ToString(), c, tx)
        cmd.CommandTimeout = 60
        If p IsNot Nothing Then
            For i As Integer = 0 To p.Length - 1
                Dim ad As String = "@p" & i.ToString(Kultur.Inv)
                Dim tipli As SQLiteParameter = TryCast(p(i), SQLiteParameter)
                If tipli IsNot Nothing Then
                    tipli.ParameterName = ad
                    cmd.Parameters.Add(tipli)
                Else
                    cmd.Parameters.AddWithValue(ad, If(p(i) Is Nothing, CType(DBNull.Value, Object), p(i)))
                End If
            Next
        End If
        Return cmd
    End Function

    ''' <summary>Tipi açıkça belirtilmiş parametre; Nothing NULL yazar.</summary>
    Public Shared Function Prm(tip As DbType, deger As Object) As SQLiteParameter
        Dim p As New SQLiteParameter()
        p.DbType = tip
        p.Value = If(deger Is Nothing, CType(DBNull.Value, Object), deger)
        Return p
    End Function

    Public Shared Function Tablo(sql As String, ParamArray p() As Object) As DataTable
        Using c As SQLiteConnection = Ac()
            Using cmd As SQLiteCommand = Komut(c, Nothing, sql, p)
                Using da As New SQLiteDataAdapter(cmd)
                    Dim dt As New DataTable()
                    da.Fill(dt)
                    Return dt
                End Using
            End Using
        End Using
    End Function

    Public Shared Function Skaler(sql As String, ParamArray p() As Object) As Object
        Using c As SQLiteConnection = Ac()
            Using cmd As SQLiteCommand = Komut(c, Nothing, sql, p)
                Return cmd.ExecuteScalar()
            End Using
        End Using
    End Function

    ''' <summary>INSERT/UPDATE/DELETE çalıştırır, etkilenen satır sayısını döner.</summary>
    Public Shared Function Calistir(sql As String, ParamArray p() As Object) As Integer
        Using c As SQLiteConnection = Ac()
            Using cmd As SQLiteCommand = Komut(c, Nothing, sql, p)
                Return cmd.ExecuteNonQuery()
            End Using
        End Using
    End Function

    ''' <summary>INSERT çalıştırır ve oluşan kimlik değerini aynı bağlantıdan okur.</summary>
    Public Shared Function EkleVeIdGetir(sql As String, ParamArray p() As Object) As Integer
        Using c As SQLiteConnection = Ac()
            Using cmd As SQLiteCommand = Komut(c, Nothing, sql, p)
                cmd.ExecuteNonQuery()
            End Using
            Using cmd2 As New SQLiteCommand("SELECT last_insert_rowid()", c)
                Return Convert.ToInt32(cmd2.ExecuteScalar())
            End Using
        End Using
    End Function

    ''' <summary>
    ''' Günde bir kez (ilk açılışta) veritabanının tutarlı bir kopyasını Yedek klasörüne alır, son 14 kopyayı saklar.
    ''' Hata verirse sessizce geçer.
    ''' </summary>
    Public Shared Sub GunlukYedek()
        Try
            If Not File.Exists(Yol) Then Return
            Dim dizin As String = Path.Combine(Path.GetDirectoryName(Yol), "Yedek")
            Directory.CreateDirectory(dizin)
            Dim hedef As String = Path.Combine(dizin, "KalipTakip_" & Date.Today.ToString("yyyyMMdd", Kultur.Inv) & ".db")
            If File.Exists(hedef) Then Return
            Using c As SQLiteConnection = Ac()
                Using cmd As New SQLiteCommand("VACUUM INTO '" & hedef.Replace("'", "''") & "'", c)
                    cmd.CommandTimeout = 300
                    cmd.ExecuteNonQuery()
                End Using
            End Using
            Dim eskiler As String() = Directory.GetFiles(dizin, "KalipTakip_*.db").OrderByDescending(Function(f) f).Skip(14).ToArray()
            For Each f As String In eskiler
                Try
                    File.Delete(f)
                Catch
                End Try
            Next
        Catch
        End Try
    End Sub

    ''' <summary>Dosyanın neden açılamadığını dosya sistemi düzeyinde sınar (yetki, kilit, bulunamama).</summary>
    Public Shared Function Teshis() As String
        Try
            If Not File.Exists(Yol) Then Return "Dosya bulunamadı."
            Dim dizin As String = Path.GetDirectoryName(Yol)
            Try
                Using fs As New FileStream(Yol, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite)
                End Using
            Catch ex As UnauthorizedAccessException
                Return "Bu kullanıcının dosyaya YAZMA yetkisi yok (ağ klasöründe 'değiştirme' yetkisi verin). Ayrıntı: " & ex.Message
            Catch ex As IOException
                Return "Dosya başka bir işlem tarafından kilitli ya da ağ sürücüsü yanıt vermiyor. Ayrıntı: " & ex.Message
            End Try
            Dim deneme As String = Path.Combine(dizin, "yazma_testi_" & Guid.NewGuid().ToString("N") & ".tmp")
            Try
                File.WriteAllText(deneme, "x")
                File.Delete(deneme)
            Catch ex As Exception
                Return "Klasörde dosya oluşturma/silme yetkisi yok (SQLite geçici günlük dosyası oluşturamıyor). Ayrıntı: " & ex.Message
            End Try
            Return "Dosyaya yazılabiliyor ama SQLite açamadı: ağ paylaşımı kilitlemeyi desteklemiyor olabilir (NAS ayarı: SMB/oplocks)."
        Catch ex As Exception
            Return ex.Message
        End Try
    End Function

    ''' <summary>Teknik hata mesajını kullanıcıya anlaşılır hale getirir.</summary>
    Public Shared Function AnlasilirMesaj(ex As Exception) As String
        Dim sx As SQLiteException = TryCast(ex, SQLiteException)
        Dim m As String = ex.Message
        If sx IsNot Nothing Then
            Dim k As String = m.ToLowerInvariant()
            If k.Contains("database is locked") OrElse k.Contains("busy") Then
                Return "Veritabanı şu anda başka bir kullanıcı tarafından kullanılıyor (kilitli). Birkaç saniye sonra tekrar deneyin."
            End If
            If k.Contains("readonly") OrElse k.Contains("read-only") Then
                Return "Veritabanı klasöründe yazma yetkiniz yok. Klasörde herkesin 'değiştirme' yetkisi olmalı." & vbCrLf & vbCrLf & Yol
            End If
            If k.Contains("unable to open") Then
                Return "Veritabanı dosyası açılamadı:" & vbCrLf & vbCrLf & Yol & vbCrLf & vbCrLf & "Neden: " & Teshis()
            End If
            If k.Contains("malformed") OrElse k.Contains("not a database") Then
                Return "Veritabanı dosyası bozuk görünüyor. Yedek klasöründeki son kopyayı geri yükleyin:" & vbCrLf & vbCrLf & Yol
            End If
            If k.Contains("unique") Then Return "Aynı kayıt zaten var (benzersiz alan çakışması)."
        End If
        Return m
    End Function
End Class
