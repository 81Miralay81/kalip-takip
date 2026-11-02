Imports System.Data
Imports System.Data.SQLite
Imports System.Linq
Imports System.Collections.Generic
Imports System.Text

''' <summary>Tek işlem içinde hazırlanmış (parametreli) komutla toplu kayıt ekler; 100 binlerce satır birkaç saniyede yazılır.</summary>
Public NotInheritable Class TopluYaz
    Private Sub New()
    End Sub

    ''' <summary>DataTable'daki tüm satırları, kolon adlarıyla eşleşen tabloya ekler (Id dahil kolonlar olduğu gibi yazılır).</summary>
    Public Shared Function Ekle(c As SQLiteConnection, tx As SQLiteTransaction, tablo As String, dt As DataTable) As Integer
        If dt.Rows.Count = 0 Then Return 0
        Dim adlar As New List(Of String)()
        For Each k As DataColumn In dt.Columns
            adlar.Add(k.ColumnName)
        Next
        Using cmd As SQLiteCommand = Hazirla(c, tx, tablo, adlar)
            For Each r As DataRow In dt.Rows
                For i As Integer = 0 To adlar.Count - 1
                    cmd.Parameters(i).Value = r(i)
                Next
                cmd.ExecuteNonQuery()
            Next
        End Using
        Return dt.Rows.Count
    End Function

    ''' <summary>Bir okuyucudan (ör. Access) satır satır kopyalar; kolon adları hedef tabloyla aynı olmalı. Kopyalanan satır sayısını döner.</summary>
    Public Shared Function Kopyala(c As SQLiteConnection, tx As SQLiteTransaction, tablo As String, rd As IDataReader) As Integer
        Dim adlar As New List(Of String)()
        For i As Integer = 0 To rd.FieldCount - 1
            adlar.Add(rd.GetName(i))
        Next
        Dim say As Integer = 0
        Using cmd As SQLiteCommand = Hazirla(c, tx, tablo, adlar)
            While rd.Read()
                For i As Integer = 0 To adlar.Count - 1
                    Dim v As Object = rd.GetValue(i)
                    cmd.Parameters(i).Value = If(v Is Nothing, CType(DBNull.Value, Object), v)
                Next
                cmd.ExecuteNonQuery()
                say += 1
            End While
        End Using
        Return say
    End Function

    Private Shared Function Hazirla(c As SQLiteConnection, tx As SQLiteTransaction, tablo As String, adlar As List(Of String)) As SQLiteCommand
        Dim sb As New StringBuilder("INSERT INTO " & tablo & " (")
        sb.Append(String.Join(",", adlar)).Append(") VALUES (")
        sb.Append(String.Join(",", adlar.Select(Function(a, i) "@p" & i.ToString(Kultur.Inv))))
        sb.Append(")")
        Dim cmd As New SQLiteCommand(sb.ToString(), c, tx)
        For i As Integer = 0 To adlar.Count - 1
            cmd.Parameters.Add(New SQLiteParameter("@p" & i.ToString(Kultur.Inv)))
        Next
        Return cmd
    End Function
End Class
