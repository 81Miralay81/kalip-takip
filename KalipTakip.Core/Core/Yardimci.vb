Imports System.Data
Imports System.Globalization
Imports System.IO
Imports System.IO.Compression
Imports System.Linq
Imports System.Security.Cryptography
Imports System.Text
Imports System.Text.RegularExpressions
Imports System.Windows.Forms
Imports System.Xml

''' <summary>Kültür sabitleri: ekran Türkçe, veri alışverişi değişmez (invariant).</summary>
Public NotInheritable Class Kultur
    Private Sub New()
    End Sub
    Public Shared ReadOnly Tr As CultureInfo = New CultureInfo("tr-TR")
    Public Shared ReadOnly Inv As CultureInfo = CultureInfo.InvariantCulture
End Class

''' <summary>Tek tip mesaj kutuları.</summary>
Public NotInheritable Class Mesaj
    Private Sub New()
    End Sub
    Private Const Baslik As String = "Kalıp Takip Sistemi"

    Public Shared Sub Bilgi(metin As String)
        MessageBox.Show(metin, Baslik, MessageBoxButtons.OK, MessageBoxIcon.Information)
    End Sub

    Public Shared Sub Uyari(metin As String)
        MessageBox.Show(metin, Baslik, MessageBoxButtons.OK, MessageBoxIcon.Warning)
    End Sub

    Public Shared Sub Hata(metin As String)
        MessageBox.Show(metin, Baslik, MessageBoxButtons.OK, MessageBoxIcon.Error)
    End Sub

    Public Shared Function Soru(metin As String) As Boolean
        Return MessageBox.Show(metin, Baslik, MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) = DialogResult.Yes
    End Function
End Class

''' <summary>Hataları kullanıcının yerel klasörüne (ağ paylaşımına değil) yazar.</summary>
Public NotInheritable Class HataGunlugu
    Private Sub New()
    End Sub

    Public Shared Sub Yaz(ex As Exception)
        Try
            Dim dizin As String = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "KalipTakip")
            Directory.CreateDirectory(dizin)
            Dim satir As String = Date.Now.ToString("yyyy-MM-dd HH:mm:ss", Kultur.Inv) & vbCrLf & ex.ToString() & vbCrLf & New String("-"c, 60) & vbCrLf
            File.AppendAllText(Path.Combine(dizin, "hata.log"), satir, Encoding.UTF8)
        Catch
            ' günlük yazılamıyorsa sessizce geç
        End Try
    End Sub
End Class

''' <summary>İşlem sürerken bekleme imleci gösterir: Using New Bekleme() ... End Using</summary>
Public NotInheritable Class Bekleme
    Implements IDisposable

    Public Sub New()
        Cursor.Current = Cursors.WaitCursor
    End Sub

    Public Sub Dispose() Implements IDisposable.Dispose
        Cursor.Current = Cursors.Default
    End Sub
End Class

''' <summary>Sayı okuma/yazma. Hem 30.5 hem 30,5 yazımı ondalık kabul edilir.</summary>
Public NotInheritable Class Sayi
    Private Sub New()
    End Sub

    Public Shared Function TryParse(metin As String, ByRef sonuc As Double) As Boolean
        sonuc = 0
        If metin Is Nothing Then Return False
        Dim s As String = metin.Trim().Replace(" ", "").Replace(","c, "."c)
        If s.Length = 0 Then Return False
        If Not Double.TryParse(s, NumberStyles.Float, Kultur.Inv, sonuc) Then Return False
        If Double.IsNaN(sonuc) OrElse Double.IsInfinity(sonuc) Then
            sonuc = 0
            Return False
        End If
        Return True
    End Function

    ''' <summary>Düz sayıyı ya da "30+15,5" gibi basit işlemi hesaplar.</summary>
    Public Shared Function Hesapla(metin As String, ByRef sonuc As Double) As Boolean
        If TryParse(metin, sonuc) Then Return True
        If metin Is Nothing Then Return False
        Dim s As String = metin.Trim().Replace(",", ".")
        If s.Length = 0 OrElse Not Regex.IsMatch(s, "^[0-9\.\+\-\*/\(\)\s]+$") Then Return False
        s = Regex.Replace(s, "(?<![0-9\.])([0-9]+)(?![0-9\.])", "$1.0")
        Try
            Using t As New DataTable()
                Dim r As Object = t.Compute(s, Nothing)
                Dim d As Double = Convert.ToDouble(r, Kultur.Inv)
                If Double.IsNaN(d) OrElse Double.IsInfinity(d) Then Return False
                sonuc = d
                Return True
            End Using
        Catch
            Return False
        End Try
    End Function

    Public Shared Function Yaz(d As Double) As String
        Return Math.Round(d, 4).ToString("0.####", Kultur.Tr)
    End Function

    ''' <summary>Giriş alanları için: en fazla 2 ondalık, gereksiz sıfır yok (1 -> 1, 1,25 -> 1,25, 1,666554 -> 1,67).</summary>
    Public Shared Function Yaz2(d As Double) As String
        Return Math.Round(d, 2, MidpointRounding.AwayFromZero).ToString("0.##", Kultur.Tr)
    End Function

    ''' <summary>Hesap ara sonuçları için daha ayrıntılı (6 ondalık) yazım: 1100/60 -> 18,333333</summary>
    Public Shared Function YazAyrintili(d As Double) As String
        Return Math.Round(d, 6).ToString("0.######", Kultur.Tr)
    End Function
End Class

''' <summary>Hafta hesabı: eski Excel sistemindeki formülün birebir karşılığı.</summary>
Public NotInheritable Class TarihYardimci
    Private Sub New()
    End Sub

    ''' <summary>
    ''' Eski VBA: DatePart("ww", tarih, vbMonday, vbFirstJan1) - 1, sonuç "hafta/yıl" metni.
    ''' (2024-2026 kayıtları bu formülle üretilmiş. ISO haftaya geçmek isterseniz
    ''' yalnızca bu fonksiyonu değiştirmeniz yeterli.)
    ''' </summary>
    Public Shared Function HaftaHesapla(tarih As Date?) As String
        If Not tarih.HasValue Then Return Nothing
        Dim d As Date = tarih.Value
        Dim hafta As Integer = DatePart(DateInterval.WeekOfYear, d, FirstDayOfWeek.Monday, FirstWeekOfYear.Jan1) - 1
        Return hafta.ToString(Kultur.Inv) & "/" & d.Year.ToString(Kultur.Inv)
    End Function
End Class

''' <summary>Parola özeti (PBKDF2-SHA256, tuzlu). Düz metin parola saklanmaz.</summary>
Public NotInheritable Class Guvenlik
    Private Sub New()
    End Sub

    Private Const TurSayisi As Integer = 100000

    Public Shared Function Hashle(parola As String) As String
        Dim tuz(15) As Byte
        Using rng As New RNGCryptoServiceProvider()
            rng.GetBytes(tuz)
        End Using
        Dim h As Byte() = Turet(parola, tuz, TurSayisi)
        Return "PBKDF2$" & TurSayisi.ToString(Kultur.Inv) & "$" & Convert.ToBase64String(tuz) & "$" & Convert.ToBase64String(h)
    End Function

    Public Shared Function Dogrula(parola As String, kayit As String) As Boolean
        If parola Is Nothing OrElse kayit Is Nothing Then Return False
        Dim p As String() = kayit.Split("$"c)
        If p.Length <> 4 OrElse p(0) <> "PBKDF2" Then Return False
        Dim tur As Integer
        If Not Integer.TryParse(p(1), NumberStyles.Integer, Kultur.Inv, tur) Then Return False
        Try
            Dim tuz As Byte() = Convert.FromBase64String(p(2))
            Dim beklenen As Byte() = Convert.FromBase64String(p(3))
            Dim h As Byte() = Turet(parola, tuz, tur)
            Return SabitZamanliEsit(beklenen, h)
        Catch
            Return False
        End Try
    End Function

    Private Shared Function Turet(parola As String, tuz As Byte(), tur As Integer) As Byte()
        Using d As New Rfc2898DeriveBytes(parola, tuz, tur, HashAlgorithmName.SHA256)
            Return d.GetBytes(32)
        End Using
    End Function

    Private Shared Function SabitZamanliEsit(a As Byte(), b As Byte()) As Boolean
        If a.Length <> b.Length Then Return False
        Dim fark As Integer = 0
        For i As Integer = 0 To a.Length - 1
            fark = fark Or (a(i) Xor b(i))
        Next
        Return fark = 0
    End Function
End Class

''' <summary>Harici kütüphane olmadan gerçek .xlsx dosyası yazar.</summary>
Public NotInheritable Class XlsxYazici
    Private Sub New()
    End Sub

    Public Shared Sub Yaz(yol As String, gorunum As DataView, kolonlar As KolonTanimi())
        If File.Exists(yol) Then File.Delete(yol)
        Using fs As New FileStream(yol, FileMode.Create, FileAccess.Write)
            Using zip As New ZipArchive(fs, ZipArchiveMode.Create)
                YazMetin(zip, "[Content_Types].xml",
                    "<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>" &
                    "<Types xmlns=""http://schemas.openxmlformats.org/package/2006/content-types"">" &
                    "<Default Extension=""rels"" ContentType=""application/vnd.openxmlformats-package.relationships+xml""/>" &
                    "<Default Extension=""xml"" ContentType=""application/xml""/>" &
                    "<Override PartName=""/xl/workbook.xml"" ContentType=""application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml""/>" &
                    "<Override PartName=""/xl/worksheets/sheet1.xml"" ContentType=""application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml""/>" &
                    "<Override PartName=""/xl/styles.xml"" ContentType=""application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml""/>" &
                    "</Types>")
                YazMetin(zip, "_rels/.rels",
                    "<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>" &
                    "<Relationships xmlns=""http://schemas.openxmlformats.org/package/2006/relationships"">" &
                    "<Relationship Id=""rId1"" Type=""http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument"" Target=""xl/workbook.xml""/>" &
                    "</Relationships>")
                YazMetin(zip, "xl/workbook.xml",
                    "<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>" &
                    "<workbook xmlns=""http://schemas.openxmlformats.org/spreadsheetml/2006/main"" xmlns:r=""http://schemas.openxmlformats.org/officeDocument/2006/relationships"">" &
                    "<sheets><sheet name=""Kalip Listesi"" sheetId=""1"" r:id=""rId1""/></sheets></workbook>")
                YazMetin(zip, "xl/_rels/workbook.xml.rels",
                    "<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>" &
                    "<Relationships xmlns=""http://schemas.openxmlformats.org/package/2006/relationships"">" &
                    "<Relationship Id=""rId1"" Type=""http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet"" Target=""worksheets/sheet1.xml""/>" &
                    "<Relationship Id=""rId2"" Type=""http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles"" Target=""styles.xml""/>" &
                    "</Relationships>")
                YazMetin(zip, "xl/styles.xml",
                    "<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>" &
                    "<styleSheet xmlns=""http://schemas.openxmlformats.org/spreadsheetml/2006/main"">" &
                    "<numFmts count=""1""><numFmt numFmtId=""164"" formatCode=""dd\.mm\.yyyy""/></numFmts>" &
                    "<fonts count=""2""><font><sz val=""11""/><name val=""Calibri""/></font><font><b/><sz val=""11""/><color rgb=""FFFFFFFF""/><name val=""Calibri""/></font></fonts>" &
                    "<fills count=""3""><fill><patternFill patternType=""none""/></fill><fill><patternFill patternType=""gray125""/></fill>" &
                    "<fill><patternFill patternType=""solid""><fgColor rgb=""FF1A105E""/><bgColor indexed=""64""/></patternFill></fill></fills>" &
                    "<borders count=""1""><border><left/><right/><top/><bottom/><diagonal/></border></borders>" &
                    "<cellStyleXfs count=""1""><xf numFmtId=""0"" fontId=""0"" fillId=""0"" borderId=""0""/></cellStyleXfs>" &
                    "<cellXfs count=""3"">" &
                    "<xf numFmtId=""0"" fontId=""0"" fillId=""0"" borderId=""0"" xfId=""0""/>" &
                    "<xf numFmtId=""0"" fontId=""1"" fillId=""2"" borderId=""0"" xfId=""0"" applyFont=""1"" applyFill=""1""/>" &
                    "<xf numFmtId=""164"" fontId=""0"" fillId=""0"" borderId=""0"" xfId=""0"" applyNumberFormat=""1""/>" &
                    "</cellXfs>" &
                    "<cellStyles count=""1""><cellStyle name=""Normal"" xfId=""0"" builtinId=""0""/></cellStyles>" &
                    "</styleSheet>")
                YazSayfa(zip, gorunum, kolonlar)
            End Using
        End Using
    End Sub

    Private Shared Sub YazMetin(zip As ZipArchive, ad As String, icerik As String)
        Dim giris As ZipArchiveEntry = zip.CreateEntry(ad, CompressionLevel.Optimal)
        Using w As New StreamWriter(giris.Open(), New UTF8Encoding(False))
            w.Write(icerik)
        End Using
    End Sub

    Private Shared Sub YazSayfa(zip As ZipArchive, gorunum As DataView, kolonlar As KolonTanimi())
        Dim giris As ZipArchiveEntry = zip.CreateEntry("xl/worksheets/sheet1.xml", CompressionLevel.Optimal)
        Using w As New StreamWriter(giris.Open(), New UTF8Encoding(False))
            w.Write("<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>")
            w.Write("<worksheet xmlns=""http://schemas.openxmlformats.org/spreadsheetml/2006/main"">")
            w.Write("<sheetViews><sheetView workbookViewId=""0""><pane ySplit=""1"" topLeftCell=""A2"" activePane=""bottomLeft"" state=""frozen""/></sheetView></sheetViews>")
            w.Write("<cols>")
            For i As Integer = 0 To kolonlar.Length - 1
                Dim genislik As String = Math.Max(8, kolonlar(i).Genislik \ 7).ToString(Kultur.Inv)
                w.Write("<col min=""" & (i + 1).ToString(Kultur.Inv) & """ max=""" & (i + 1).ToString(Kultur.Inv) & """ width=""" & genislik & """ customWidth=""1""/>")
            Next
            w.Write("</cols><sheetData>")

            w.Write("<row r=""1"">")
            For i As Integer = 0 To kolonlar.Length - 1
                w.Write("<c r=""" & Harf(i) & "1"" s=""1"" t=""inlineStr""><is><t>" & Kac(kolonlar(i).Baslik) & "</t></is></c>")
            Next
            w.Write("</row>")

            Dim satirNo As Integer = 1
            For Each rv As DataRowView In gorunum
                satirNo += 1
                w.Write("<row r=""" & satirNo.ToString(Kultur.Inv) & """>")
                For i As Integer = 0 To kolonlar.Length - 1
                    Dim v As Object = rv(kolonlar(i).Alan)
                    If v Is Nothing OrElse IsDBNull(v) Then Continue For
                    Dim basvuru As String = Harf(i) & satirNo.ToString(Kultur.Inv)
                    Select Case kolonlar(i).Tip
                        Case KolonTipi.Tarih
                            Dim d As Date = Convert.ToDateTime(v)
                            w.Write("<c r=""" & basvuru & """ s=""2""><v>" & d.ToOADate().ToString("R", Kultur.Inv) & "</v></c>")
                        Case KolonTipi.Tam, KolonTipi.Ondalik
                            Dim n As Double = Convert.ToDouble(v)
                            w.Write("<c r=""" & basvuru & """><v>" & n.ToString("R", Kultur.Inv) & "</v></c>")
                        Case Else
                            w.Write("<c r=""" & basvuru & """ t=""inlineStr""><is><t xml:space=""preserve"">" & Kac(Convert.ToString(v)) & "</t></is></c>")
                    End Select
                Next
                w.Write("</row>")
            Next
            w.Write("</sheetData>")
            w.Write("<autoFilter ref=""A1:" & Harf(kolonlar.Length - 1) & Math.Max(1, satirNo).ToString(Kultur.Inv) & """/>")
            w.Write("</worksheet>")
        End Using
    End Sub

    Private Shared Function Harf(i As Integer) As String
        Dim s As String = ""
        Dim n As Integer = i + 1
        While n > 0
            Dim m As Integer = (n - 1) Mod 26
            s = ChrW(65 + m) & s
            n = (n - 1) \ 26
        End While
        Return s
    End Function

    Private Shared Function Kac(s As String) As String
        If s Is Nothing Then Return ""
        Dim sb As New StringBuilder(s.Length + 8)
        For Each ch As Char In s
            Select Case ch
                Case "&"c
                    sb.Append("&amp;")
                Case "<"c
                    sb.Append("&lt;")
                Case ">"c
                    sb.Append("&gt;")
                Case Else
                    If XmlConvert.IsXmlChar(ch) Then sb.Append(ch)
            End Select
        Next
        Return sb.ToString()
    End Function
End Class
