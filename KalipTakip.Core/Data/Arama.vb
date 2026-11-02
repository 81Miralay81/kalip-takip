Imports System.Data.SQLite
Imports System.Text

''' <summary>
''' Arama için metin katlama: büyük/küçük harf ve Türkçe karakter farkını yok sayar
''' (ı, İ, I, i -> i; ş -> s; ğ -> g; ü -> u; ö -> o; ç -> c). Böylece "kalip" yazınca "KALIP" ve "kalıp" bulunur.
''' </summary>
Public NotInheritable Class TrMetin
    Private Sub New()
    End Sub

    Public Shared Function Katla(s As String) As String
        If String.IsNullOrEmpty(s) Then Return s
        Dim sb As New StringBuilder(s.Length)
        For Each ch As Char In s
            Select Case ch
                Case "ı"c, "İ"c, "I"c, "i"c
                    sb.Append("i"c)
                Case "ş"c, "Ş"c
                    sb.Append("s"c)
                Case "ğ"c, "Ğ"c
                    sb.Append("g"c)
                Case "ü"c, "Ü"c
                    sb.Append("u"c)
                Case "ö"c, "Ö"c
                    sb.Append("o"c)
                Case "ç"c, "Ç"c
                    sb.Append("c"c)
                Case Else
                    sb.Append(Char.ToLowerInvariant(ch))
            End Select
        Next
        Return sb.ToString()
    End Function
End Class

''' <summary>SQL içinde KATLA(kolon) olarak kullanılan, Türkçe duyarsız karşılaştırma işlevi.</summary>
<SQLiteFunction(Name:="KATLA", Arguments:=1, FuncType:=FunctionType.Scalar)>
Public Class KatlaFonksiyonu
    Inherits SQLiteFunction

    Public Overrides Function Invoke(args() As Object) As Object
        If args Is Nothing OrElse args.Length = 0 OrElse args(0) Is Nothing OrElse IsDBNull(args(0)) Then Return DBNull.Value
        Return TrMetin.Katla(Convert.ToString(args(0)))
    End Function
End Class
