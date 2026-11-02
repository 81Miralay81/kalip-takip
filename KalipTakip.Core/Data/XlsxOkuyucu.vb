Imports System.Collections.Generic
Imports System.IO
Imports System.IO.Compression
Imports System.Linq
Imports System.Xml
Imports System.Xml.Linq

''' <summary>
''' .xlsx/.xlsm dosyalarını Office ve sürücü gerektirmeden, akış halinde okur.
''' Hücre değerleri metin olarak döner (sayılar dosyadaki ham haliyle, "30.5" gibi).
''' </summary>
Public NotInheritable Class XlsxOkuyucu
    Implements IDisposable

    Private ReadOnly _zip As ZipArchive
    Private ReadOnly _paylasilan As New List(Of String)()
    Private ReadOnly _sayfalar As New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase)

    Private Shared ReadOnly NsMain As XNamespace = "http://schemas.openxmlformats.org/spreadsheetml/2006/main"
    Private Shared ReadOnly NsR As XNamespace = "http://schemas.openxmlformats.org/officeDocument/2006/relationships"
    Private Shared ReadOnly NsRel As XNamespace = "http://schemas.openxmlformats.org/package/2006/relationships"

    Public Sub New(yol As String)
        Dim fs As New FileStream(yol, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)
        Try
            _zip = New ZipArchive(fs, ZipArchiveMode.Read)
        Catch
            fs.Dispose()
            Throw
        End Try
        SayfalariOku()
        PaylasilanlariOku()
    End Sub

    Public Function IlkSayfa() As String
        Return _sayfalar.Keys.FirstOrDefault()
    End Function

    Public Function SayfaVar(ad As String) As Boolean
        Return _sayfalar.ContainsKey(ad)
    End Function

    Private Function BelgeYukle(girisAdi As String) As XDocument
        Dim g As ZipArchiveEntry = _zip.GetEntry(girisAdi)
        If g Is Nothing Then Return Nothing
        Using s As Stream = g.Open()
            Return XDocument.Load(s)
        End Using
    End Function

    Private Sub SayfalariOku()
        Dim wb As XDocument = BelgeYukle("xl/workbook.xml")
        Dim rels As XDocument = BelgeYukle("xl/_rels/workbook.xml.rels")
        If wb Is Nothing OrElse rels Is Nothing Then Throw New InvalidDataException("Geçerli bir Excel (.xlsx/.xlsm) dosyası değil.")
        Dim hedefler As Dictionary(Of String, String) =
            rels.Descendants(NsRel + "Relationship").ToDictionary(Function(e) CStr(e.Attribute("Id")), Function(e) CStr(e.Attribute("Target")))
        For Each sh As XElement In wb.Descendants(NsMain + "sheet")
            Dim ad As String = CStr(sh.Attribute("name"))
            Dim rid As String = CStr(sh.Attribute(NsR + "id"))
            Dim hedef As String = Nothing
            If hedefler.TryGetValue(rid, hedef) Then
                If hedef.StartsWith("/") Then
                    hedef = hedef.Substring(1)
                Else
                    hedef = "xl/" & hedef
                End If
                _sayfalar(ad) = hedef
            End If
        Next
    End Sub

    Private Sub PaylasilanlariOku()
        Dim d As XDocument = BelgeYukle("xl/sharedStrings.xml")
        If d Is Nothing Then Return
        For Each si As XElement In d.Descendants(NsMain + "si")
            Dim parcalar As IEnumerable(Of String) =
                si.Descendants(NsMain + "t").Where(Function(t) t.Parent.Name <> NsMain + "rPh").Select(Function(t) t.Value)
            _paylasilan.Add(String.Concat(parcalar))
        Next
    End Sub

    ''' <summary>Sayfanın satırlarını sırayla verir: her satır, sütun numarası (0'dan) -> metin sözlüğüdür.</summary>
    Public Iterator Function Satirlar(sayfaAdi As String) As IEnumerable(Of Dictionary(Of Integer, String))
        Dim yol As String = Nothing
        If Not _sayfalar.TryGetValue(sayfaAdi, yol) Then Throw New InvalidDataException("Sayfa bulunamadı: " & sayfaAdi)
        Dim giris As ZipArchiveEntry = _zip.GetEntry(yol)
        If giris Is Nothing Then Throw New InvalidDataException("Sayfa verisi bulunamadı: " & sayfaAdi)

        Dim ayar As New XmlReaderSettings()
        ayar.DtdProcessing = DtdProcessing.Prohibit
        ayar.IgnoreWhitespace = False

        Using s As Stream = giris.Open()
            Using xr As XmlReader = XmlReader.Create(s, ayar)
                Dim satir As Dictionary(Of Integer, String) = Nothing
                Dim sonKolon As Integer = -1
                While xr.Read()
                    If xr.NodeType = XmlNodeType.Element Then
                        If xr.LocalName = "row" Then
                            satir = New Dictionary(Of Integer, String)()
                            sonKolon = -1
                            If xr.IsEmptyElement Then
                                Yield satir
                                satir = Nothing
                            End If
                        ElseIf xr.LocalName = "c" AndAlso satir IsNot Nothing Then
                            Dim basvuru As String = xr.GetAttribute("r")
                            Dim tip As String = xr.GetAttribute("t")
                            Dim kolon As Integer = If(basvuru Is Nothing, sonKolon + 1, KolonNo(basvuru))
                            sonKolon = kolon
                            Dim deger As String = Nothing
                            If Not xr.IsEmptyElement Then
                                Using alt As XmlReader = xr.ReadSubtree()
                                    alt.Read()
                                    alt.Read()
                                    While Not alt.EOF
                                        If alt.NodeType = XmlNodeType.Element AndAlso (alt.LocalName = "v" OrElse alt.LocalName = "t") Then
                                            Dim ad As String = alt.LocalName
                                            Dim icerik As String = alt.ReadElementContentAsString()
                                            If ad = "v" Then
                                                deger = icerik
                                            Else
                                                deger = If(deger, "") & icerik
                                            End If
                                        Else
                                            alt.Read()
                                        End If
                                    End While
                                End Using
                            End If
                            If deger IsNot Nothing Then
                                If tip = "s" Then
                                    Dim idx As Integer
                                    If Integer.TryParse(deger, idx) AndAlso idx >= 0 AndAlso idx < _paylasilan.Count Then
                                        deger = _paylasilan(idx)
                                    End If
                                End If
                                satir(kolon) = deger
                            End If
                        End If
                    ElseIf xr.NodeType = XmlNodeType.EndElement AndAlso xr.LocalName = "row" Then
                        If satir IsNot Nothing Then Yield satir
                        satir = Nothing
                    End If
                End While
            End Using
        End Using
    End Function

    Private Shared Function KolonNo(basvuru As String) As Integer
        Dim n As Integer = 0
        For Each ch As Char In basvuru
            If ch >= "A"c AndAlso ch <= "Z"c Then
                n = n * 26 + (AscW(ch) - 64)
            Else
                Exit For
            End If
        Next
        Return n - 1
    End Function

    Public Sub Dispose() Implements IDisposable.Dispose
        _zip.Dispose()
    End Sub
End Class
