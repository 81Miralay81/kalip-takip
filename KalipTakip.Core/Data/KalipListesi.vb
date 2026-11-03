Imports System.Collections.Generic
Imports System.Data
Imports System.Data.SQLite
Imports System.IO
Imports System.Linq
Imports System.Text

' Kalıp listesi: her kalıp tek satır (Kalıp Takip kayıtları, elle tanımlar, talepler ve lokasyonlardan birleşik).
Public NotInheritable Class KalipListeRepository
    Private Sub New()
    End Sub

    Public Shared ReadOnly Kolonlar As KolonTanimi() = {
        New KolonTanimi("KalipKodu", "Kalıp Kodu", KolonTipi.Metin, 140),
        New KolonTanimi("KalipAdi", "Kalıp / Parça Adı", KolonTipi.Metin, 240),
        New KolonTanimi("KalipTuru", "Kalıp Türü", KolonTipi.Metin, 120),
        New KolonTanimi("SonTashih", "Son Tamir Tarihi", KolonTipi.Tarih, 120),
        New KolonTanimi("TamirToplam", "Toplam Tamir", KolonTipi.Tam, 90),
        New KolonTanimi("TashihSayisi", "Tamir (kayıt)", KolonTipi.Tam, 90),
        New KolonTanimi("Durum", "Durum (Aktif/Hurda)", KolonTipi.Metin, 110),
        New KolonTanimi("SonGelis", "Son Geliş", KolonTipi.Tarih, 100),
        New KolonTanimi("KayitSayisi", "Takip Kaydı", KolonTipi.Tam, 90),
        New KolonTanimi("Lokasyon", "Lokasyon", KolonTipi.Metin, 130),
        New KolonTanimi("Raf", "Raf", KolonTipi.Metin, 100),
        New KolonTanimi("SahipBolum", "Sahip Bölüm", KolonTipi.Metin, 130),
        New KolonTanimi("TamirAdedi", "Tamir (portal)", KolonTipi.Tam, 90),
        New KolonTanimi("TalepSayisi", "Talep Sayısı", KolonTipi.Tam, 95),
        New KolonTanimi("SonTalepTarihi", "Son Talep", KolonTipi.Tarih, 100),
        New KolonTanimi("SonKalipStatu", "Son Kalıp Statü", KolonTipi.Metin, 130),
        New KolonTanimi("Notlar", "Notlar", KolonTipi.Metin, 220)
    }

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

    Private Shared Function TarihliYap(dt As DataTable, ParamArray alanlar() As String) As DataTable
        Dim sonuc As DataTable = dt.Clone()
        For Each a As String In alanlar
            If sonuc.Columns.Contains(a) Then sonuc.Columns(a).DataType = GetType(DateTime)
        Next
        For Each r As DataRow In dt.Rows
            Dim n As DataRow = sonuc.NewRow()
            For Each c As DataColumn In dt.Columns
                Dim v As Object = r(c.ColumnName)
                If alanlar.Contains(c.ColumnName) Then
                    Dim d As DateTime
                    If v Is Nothing OrElse IsDBNull(v) OrElse Not DateTime.TryParse(Convert.ToString(v), Kultur.Inv, System.Globalization.DateTimeStyles.None, d) Then
                        n(c.ColumnName) = DBNull.Value
                    Else
                        n(c.ColumnName) = d
                    End If
                Else
                    n(c.ColumnName) = v
                End If
            Next
            sonuc.Rows.Add(n)
        Next
        Return sonuc
    End Function

    ' Kalıp listesi. arama 0 = kalıp koduna / adına göre ara (yalnız o kalıplar), arama 1 = rafa göre ara (o raftaki tüm kalıplar).
    ' Raf aramasında önce tam eşleşme denenir; sonuç yoksa raf adında geçenler gösterilir. Arama boşsa ilk 'limit' kalıp.
    Public Shared Function Ara(metin As String, arama As Integer, limit As Integer) As DataTable
        Return Ara(metin, arama, limit, Nothing)
    End Function

    ' izinliBolumler: Nothing = hepsi; dolu liste = yalnızca sahibi bu bölümlerden biri olan (ya da sahibi atanmamış) kalıplar.
    Public Shared Function Ara(metin As String, arama As Integer, limit As Integer, izinliBolumler As ICollection(Of String)) As DataTable
        Return Ara(metin, arama, limit, izinliBolumler, "")
    End Function

    ' alan: boş değilse arama o sütunda yapılır (KalipTuru, KalipAdi, TepeKodu, Lokasyon, SahipBolum, SonKalipStatu, Durum, Notlar). Joker (*) desteklenir.
    Public Shared Function Ara(metin As String, arama As Integer, limit As Integer, izinliBolumler As ICollection(Of String), alan As String) As DataTable
        If Not String.IsNullOrWhiteSpace(alan) AndAlso Not String.IsNullOrWhiteSpace(metin) Then
            Return Sorgu(metin, 3, limit, izinliBolumler, alan)
        End If
        If arama = 1 AndAlso Not String.IsNullOrWhiteSpace(metin) Then
            Dim tam As DataTable = Sorgu(metin, 2, limit, izinliBolumler)
            If tam.Rows.Count > 0 Then Return tam
            Return Sorgu(metin, 1, limit, izinliBolumler)
        End If
        Return Sorgu(metin, 0, limit, izinliBolumler)
    End Function

    Private Shared Function Sorgu(metin As String, bicim As Integer, limit As Integer, izinliBolumler As ICollection(Of String)) As DataTable
        Return Sorgu(metin, bicim, limit, izinliBolumler, "")
    End Function

    ' Aranabilir sütunlar (güvenli beyaz liste). Kalıp türü: kalıp tanımındaki ya da Kalıp Takip kayıtlarındaki herhangi bir değer.
    Private Shared Function AlanKosulu(alan As String, p As List(Of Object), desen As String) As String
        Select Case alan
            Case "KalipTuru"
                p.Add(desen)
                p.Add(desen)
                Return "(KATLA(COALESCE(COALESCE(t.KalipTuru, kk.KalipTuru), '')) LIKE ? ESCAPE '\' OR d.A IN (SELECT A FROM kturu WHERE T LIKE ? ESCAPE '\'))"
            Case "KalipAdi"
                p.Add(desen)
                Return "KATLA(COALESCE(COALESCE(t.KalipAdi, kk.ParcaAdi), '')) LIKE ? ESCAPE '\'"
            Case "TepeKodu"
                p.Add(desen)
                Return "KATLA(COALESCE(t.TepeKodu, '')) LIKE ? ESCAPE '\'"
            Case "Lokasyon"
                p.Add(desen)
                Return "KATLA(COALESCE(l.Lokasyon, '')) LIKE ? ESCAPE '\'"
            Case "SahipBolum"
                p.Add(desen)
                Return "KATLA(COALESCE(l.SahipBolum, '')) LIKE ? ESCAPE '\'"
            Case "SonKalipStatu"
                p.Add(desen)
                Return "KATLA(COALESCE(tk.KalipStatu, '')) LIKE ? ESCAPE '\'"
            Case "Notlar"
                p.Add(desen)
                Return "KATLA(COALESCE(t.Notlar, '')) LIKE ? ESCAPE '\'"
            Case "Durum"
                p.Add(desen)
                Return "KATLA(CASE WHEN COALESCE(kg.Hurda, 0) > 0 THEN 'Hurda' ELSE 'Aktif' END) LIKE ? ESCAPE '\'"
            Case Else
                Throw New InvalidOperationException("Bilinmeyen arama alanı: " & alan)
        End Select
    End Function

    ''' <summary>Kalıp koduna ait herhangi bir kayıt (tanım, lokasyon, talep, Kalıp Takip) var mı? Yeni kalıp mı, düzenleme mi ayrımı için.</summary>
    Public Shared Function KalipVarMi(kod As String) As Boolean
        Dim a As String = TrMetin.Katla(If(kod, "").Trim())
        Dim n As Object = Db.Skaler("SELECT (SELECT COUNT(*) FROM KalipTanim WHERE Anahtar = ?) + (SELECT COUNT(*) FROM KalipLokasyon WHERE Anahtar = ?) + (SELECT COUNT(*) FROM TalepKalip WHERE Anahtar = ?) + (SELECT COUNT(*) FROM Kalip WHERE KATLA(ParcaKodu) = ?)", a, a, a, a)
        Return Convert.ToInt64(n) > 0
    End Function

    ''' <summary>Eski hatalı yüklemelerden kalan verileri onarır: lokasyon listesinde olmayan lokasyonları boşaltır, bozuk kalıp türü adlarını (sayı ve iki tırnak ile yazılmış olanları) "1" DÖVME KALIBI biçimine çevirir. uygula=False ise yalnız sayar. Döner: lokasyon sayısı|tür sayısı</summary>
    Public Shared Function VeriOnar(uygula As Boolean) As String
        Dim lokSay As Long = 0
        If Convert.ToInt64(Db.Skaler("SELECT COUNT(*) FROM Lokasyon")) > 0 Then
            lokSay = Convert.ToInt64(Db.Skaler("SELECT COUNT(*) FROM KalipLokasyon WHERE Lokasyon IS NOT NULL AND TRIM(Lokasyon) <> '' AND KATLA(TRIM(Lokasyon)) NOT IN (SELECT KATLA(TRIM(Ad)) FROM Lokasyon)"))
            If uygula AndAlso lokSay > 0 Then
                Using c As SQLiteConnection = Db.Ac()
                    Using cmd As SQLiteCommand = Db.Komut(c, Nothing, "UPDATE KalipLokasyon SET Lokasyon = NULL WHERE Lokasyon IS NOT NULL AND TRIM(Lokasyon) <> '' AND KATLA(TRIM(Lokasyon)) NOT IN (SELECT KATLA(TRIM(Ad)) FROM Lokasyon)")
                        cmd.ExecuteNonQuery()
                    End Using
                End Using
            End If
        End If
        Dim turSay As Long = 0
        Dim rx As New System.Text.RegularExpressions.Regex("^(\d+)""""\s*(.*)$")
        For Each tablo As String In New String() {"Kalip", "KalipTanim"}
            Dim dt As DataTable = Db.Tablo("SELECT DISTINCT KalipTuru AS D FROM " & tablo & " WHERE KalipTuru LIKE ?", "%""""%")
            For Each r As DataRow In dt.Rows
                Dim eski As String = Convert.ToString(r("D"))
                Dim m As System.Text.RegularExpressions.Match = rx.Match(eski.Trim())
                If Not m.Success Then Continue For
                Dim yeni As String = """" & m.Groups(1).Value & """ " & m.Groups(2).Value.Trim()
                If uygula Then
                    Using c As SQLiteConnection = Db.Ac()
                        Using cmd As SQLiteCommand = Db.Komut(c, Nothing, "UPDATE " & tablo & " SET KalipTuru = ? WHERE KalipTuru = ?", yeni, eski)
                            turSay += cmd.ExecuteNonQuery()
                        End Using
                    End Using
                Else
                    turSay += Convert.ToInt64(Db.Skaler("SELECT COUNT(*) FROM " & tablo & " WHERE KalipTuru = ?", eski))
                End If
            Next
        Next
        ' Türü listede olmayan kalıplar (ör. parça adı yazılmış): kod sonundaki numaraya göre atanır (-1 -> "1" ..., -2 -> "2" ..., -3 -> "3" ...)
        Dim tipSay As Long = 0
        Dim kalan As Long = 0
        Dim gecerli As New HashSet(Of String)()
        Dim numTur As New Dictionary(Of String, String)()
        Dim rxN As New System.Text.RegularExpressions.Regex("^""(\d+)""")
        Dim rxS As New System.Text.RegularExpressions.Regex("-(?:[A-Za-z]+-)?(\d+)$")
        For Each r As DataRow In Db.Tablo("SELECT Deger FROM Liste WHERE Grup = 'KalipTuru' ORDER BY Sira, Id").Rows
            Dim d As String = Convert.ToString(r("Deger")).Trim()
            gecerli.Add(TrMetin.Katla(d))
            Dim mm As System.Text.RegularExpressions.Match = rxN.Match(d)
            If mm.Success AndAlso Not numTur.ContainsKey(mm.Groups(1).Value) Then numTur(mm.Groups(1).Value) = d
        Next
        If gecerli.Count > 0 AndAlso numTur.Count > 0 Then
            Dim hedefler As New Dictionary(Of String, List(Of String()))()
            For Each r As DataRow In Ara("", 0, 0, Nothing).Rows
                Dim tur As String = If(IsDBNull(r("KalipTuru")), "", Convert.ToString(r("KalipTuru")).Trim())
                If gecerli.Contains(TrMetin.Katla(tur)) Then Continue For
                Dim kod As String = Convert.ToString(r("KalipKodu"))
                Dim m As System.Text.RegularExpressions.Match = rxS.Match(kod.Trim())
                If m.Success AndAlso numTur.ContainsKey(m.Groups(1).Value) Then
                    Dim hedef As String = numTur(m.Groups(1).Value)
                    If Not hedefler.ContainsKey(hedef) Then hedefler(hedef) = New List(Of String())()
                    hedefler(hedef).Add(New String() {Convert.ToString(r("Anahtar")), kod.Trim()})
                    tipSay += 1
                Else
                    kalan += 1
                End If
            Next
            If uygula AndAlso tipSay > 0 Then
                Using c As SQLiteConnection = Db.Ac()
                    Using tx As SQLiteTransaction = c.BeginTransaction()
                        For Each kv As KeyValuePair(Of String, List(Of String())) In hedefler
                            For Each cift As String() In kv.Value
                                Using cmd As SQLiteCommand = Db.Komut(c, tx, "INSERT INTO KalipTanim (Anahtar, KalipKodu, KalipTuru) VALUES (?, ?, ?) ON CONFLICT(Anahtar) DO UPDATE SET KalipTuru = excluded.KalipTuru", cift(0), cift(1), kv.Key)
                                    cmd.ExecuteNonQuery()
                                End Using
                            Next
                            For bas As Integer = 0 To kv.Value.Count - 1 Step 300
                                Dim parcaListe As List(Of String()) = kv.Value.Skip(bas).Take(300).ToList()
                                Dim prm As New List(Of Object)()
                                prm.Add(kv.Key)
                                For Each cift As String() In parcaListe
                                    prm.Add(cift(0))
                                Next
                                Dim yer As String = String.Join(",", parcaListe.Select(Function(x) "?"))
                                Using cmd As SQLiteCommand = Db.Komut(c, tx, "UPDATE Kalip SET KalipTuru = ? WHERE KATLA(ParcaKodu) IN (" & yer & ")", prm.ToArray())
                                    cmd.ExecuteNonQuery()
                                End Using
                            Next
                        Next
                        tx.Commit()
                    End Using
                End Using
            End If
        End If
        Return lokSay.ToString() & "|" & turSay.ToString() & "|" & tipSay.ToString() & "|" & kalan.ToString()
    End Function

    ''' <summary>Arama kutusundaki açılır liste için benzersiz değerler (şu an: KalipTuru = kalıp tanımı + Kalıp Takip kayıtları + yönetim listesi).</summary>
    Public Shared Function Degerler(alan As String) As List(Of String)
        Dim sonuc As New List(Of String)()
        If alan <> "KalipTuru" Then Return sonuc
        Dim gorulen As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
        Dim dt As DataTable = Db.Tablo("SELECT TRIM(KalipTuru) AS D FROM Kalip WHERE KalipTuru IS NOT NULL AND TRIM(KalipTuru) <> '' " &
                                       "UNION SELECT TRIM(KalipTuru) FROM KalipTanim WHERE KalipTuru IS NOT NULL AND TRIM(KalipTuru) <> '' " &
                                       "UNION SELECT TRIM(Deger) FROM Liste WHERE Grup = 'KalipTuru' ORDER BY 1")
        For Each r As DataRow In dt.Rows
            Dim d As String = Convert.ToString(r("D"))
            If gorulen.Add(d) Then sonuc.Add(d)
        Next
        Return sonuc
    End Function

    ' bicim: 0 = kod/ad içinde geçen, 1 = raf içinde geçen, 2 = raf tam eşleşme, 3 = belirtilen sütunda ara
    Private Shared Function Sorgu(metin As String, bicim As Integer, limit As Integer, izinliBolumler As ICollection(Of String), alan As String) As DataTable
        Dim sql As New StringBuilder()
        sql.Append("WITH kg AS (SELECT KATLA(ParcaKodu) AS A, MIN(ParcaKodu) AS Kod, MAX(GelisTarihi) AS SonGelis, ")
        sql.Append("MAX(CASE WHEN (TashihSayi > 0 OR KATLA(COALESCE(Tur,'')) LIKE 'tash%' OR KATLA(COALESCE(Tur,'')) LIKE 'tamir%') THEN COALESCE(GelisTarihi, BitisTarihi) END) AS SonTashih, SUM(CASE WHEN KATLA(COALESCE(Tur,'')) LIKE 'ilave%' THEN 1 ELSE 0 END) AS Ilave, SUM(CASE WHEN KATLA(COALESCE(Tur,'')) LIKE 'yeni%' THEN 1 ELSE 0 END) AS Yeni, SUM(CASE WHEN KATLA(COALESCE(Tur,'')) LIKE 'hurda%' THEN 1 ELSE 0 END) AS Hurda, MAX(COALESCE(MAX(TashihSayi), 0), SUM(CASE WHEN (TashihSayi > 0 OR KATLA(COALESCE(Tur,'')) LIKE 'tash%' OR KATLA(COALESCE(Tur,'')) LIKE 'tamir%') THEN 1 ELSE 0 END)) AS Tashih, COUNT(*) AS Kayit, MAX(Id) AS SonId ")
        sql.Append("FROM Kalip WHERE ParcaKodu IS NOT NULL AND TRIM(ParcaKodu) <> '' GROUP BY KATLA(ParcaKodu)), ")
        sql.Append("tg AS (SELECT Anahtar AS A, COUNT(*) AS N, MAX(Id) AS SonId FROM TalepKalip GROUP BY Anahtar), ")
        sql.Append("kturu AS (SELECT DISTINCT KATLA(ParcaKodu) AS A, KATLA(TRIM(KalipTuru)) AS T FROM Kalip WHERE ParcaKodu IS NOT NULL AND KalipTuru IS NOT NULL AND TRIM(KalipTuru) <> ''), ")
        sql.Append("kodlar AS (SELECT A, Kod FROM kg UNION SELECT Anahtar, KalipKodu FROM KalipTanim UNION SELECT Anahtar, KalipKodu FROM TalepKalip ")
        sql.Append("UNION SELECT Anahtar, KalipKodu FROM KalipLokasyon) ")
        sql.Append("SELECT d.A AS Anahtar, d.Kod AS KalipKodu, COALESCE(t.KalipAdi, kk.ParcaAdi) AS KalipAdi, COALESCE(t.KalipTuru, kk.KalipTuru) AS KalipTuru, ")
        sql.Append("kg.SonTashih AS SonTashih, COALESCE(kg.Tashih, 0) AS TashihSayisi, COALESCE(kg.Tashih, 0) + COALESCE(l.TamirAdedi, 0) AS TamirToplam, ")
        sql.Append("COALESCE(kg.Ilave, 0) AS IlaveSayisi, COALESCE(kg.Hurda, 0) AS HurdaSayisi, MAX(0, COALESCE(kg.Yeni, 0) + COALESCE(kg.Ilave, 0) - COALESCE(kg.Hurda, 0)) AS AktifSayisi, CASE WHEN COALESCE(kg.Hurda, 0) > 0 THEN 'Hurda' ELSE 'Aktif' END AS Durum, ")
        sql.Append("kg.SonGelis AS SonGelis, COALESCE(kg.Kayit, 0) AS KayitSayisi, ")
        sql.Append("l.Lokasyon AS Lokasyon, l.Raf AS Raf, l.SahipBolum AS SahipBolum, COALESCE(l.TamirAdedi, 0) AS TamirAdedi, COALESCE(tg.N, 0) AS TalepSayisi, tt.TalepTarihi AS SonTalepTarihi, ")
        sql.Append("tk.KalipStatu AS SonKalipStatu, t.Notlar AS Notlar, t.TepeKodu AS TepeKodu ")
        sql.Append("FROM (SELECT A, MIN(Kod) AS Kod FROM kodlar GROUP BY A) d ")
        sql.Append("LEFT JOIN kg ON kg.A = d.A LEFT JOIN Kalip kk ON kk.Id = kg.SonId LEFT JOIN KalipTanim t ON t.Anahtar = d.A ")
        sql.Append("LEFT JOIN KalipLokasyon l ON l.Anahtar = d.A LEFT JOIN tg ON tg.A = d.A LEFT JOIN TalepKalip tk ON tk.Id = tg.SonId ")
        sql.Append("LEFT JOIN Talep tt ON tt.Id = tk.TalepId ")
        sql.Append("")
        Dim p As New List(Of Object)()
        Dim kosullar As New List(Of String)()
        If Not String.IsNullOrWhiteSpace(metin) Then
            Select Case bicim
                Case 0
                    kosullar.Add("(KATLA(d.Kod) LIKE ? ESCAPE '\' OR KATLA(COALESCE(t.KalipAdi, kk.ParcaAdi)) LIKE ? ESCAPE '\')")
                    p.Add(Desen(metin))
                    p.Add(Desen(metin))
                Case 1
                    kosullar.Add("KATLA(l.Raf) LIKE ? ESCAPE '\'")
                    p.Add(Desen(metin))
                Case 3
                    Dim parca As String() = alan.Split("|"c)
                    Dim alanAdi As String = parca(0)
                    Dim tamEsles As Boolean = parca.Contains("tam")
                    kosullar.Add(AlanKosulu(alanAdi, p, If(tamEsles, LikeKacis(TrMetin.Katla(metin.Trim())), Desen(metin))))
                    For Each t As String In parca
                        If t.StartsWith("b:", StringComparison.Ordinal) AndAlso t.Length > 2 Then
                            kosullar.Add("KATLA(COALESCE(l.SahipBolum, '')) = ?")
                            p.Add(TrMetin.Katla(t.Substring(2).Trim()))
                        End If
                    Next
                Case Else
                    kosullar.Add("KATLA(l.Raf) = ?")
                    p.Add(TrMetin.Katla(metin.Trim()))
            End Select
        End If
        If izinliBolumler IsNot Nothing Then
            Dim yer As New List(Of String)()
            For Each b As String In izinliBolumler
                yer.Add("?")
                p.Add(TrMetin.Katla(If(b, "").Trim()))
            Next
            If yer.Count = 0 Then
                kosullar.Add("(l.SahipBolum IS NULL OR TRIM(l.SahipBolum) = '')")
            Else
                ' kalıp, sahibi bölüm ya da şu an bulunduğu (lokasyonunun bölümü) bölüm ile görünür
                Dim liste As String = String.Join(",", yer)
                kosullar.Add("(l.SahipBolum IS NULL OR TRIM(l.SahipBolum) = '' OR KATLA(l.SahipBolum) IN (" & liste & ") " &
                             "OR EXISTS (SELECT 1 FROM Lokasyon lh WHERE KATLA(lh.Ad) = KATLA(l.Lokasyon) AND KATLA(lh.Bolum) IN (" & liste & ")))")
                For Each b As String In izinliBolumler
                    p.Add(TrMetin.Katla(If(b, "").Trim()))
                Next
            End If
        End If
        If kosullar.Count > 0 Then sql.Append("WHERE ").Append(String.Join(" AND ", kosullar)).Append(" ")
        sql.Append("ORDER BY d.Kod")
        If limit > 0 Then sql.Append(" LIMIT ").Append(limit.ToString(Kultur.Inv))
        Return TarihliYap(Db.Tablo(sql.ToString(), p.ToArray()), "SonTashih", "SonGelis", "SonTalepTarihi")
    End Function

    ' Kalıpların sahip bölümünü topluca yazar (bolum boş = sahibi kaldır). Kalıp kaydı yoksa oluşturulur. Döner: işlenen kalıp sayısı.
    Public Shared Function SahipYaz(kodlar As IEnumerable(Of String), bolum As String, kullanici As String) As Integer
        Dim b As String = If(String.IsNullOrWhiteSpace(bolum), Nothing, bolum.Trim().ToUpper(Kultur.Tr))
        Dim adet As Integer = 0
        Using c As SQLiteConnection = Db.Ac()
            Using tx As SQLiteTransaction = c.BeginTransaction()
                For Each kod As String In kodlar
                    If String.IsNullOrWhiteSpace(kod) Then Continue For
                    Using cmd As SQLiteCommand = Db.Komut(c, tx,
                            "INSERT INTO KalipLokasyon (Anahtar, KalipKodu, SahipBolum, SonDegisiklik, SonKullanici) VALUES (?,?,?,?,?) " &
                            "ON CONFLICT(Anahtar) DO UPDATE SET SahipBolum = excluded.SahipBolum, SonDegisiklik = excluded.SonDegisiklik, SonKullanici = excluded.SonKullanici",
                            TrMetin.Katla(kod.Trim()), kod.Trim(), Bos(b), Db.Prm(DbType.DateTime, Date.Now), Bos(kullanici))
                        cmd.ExecuteNonQuery()
                    End Using
                    adet += 1
                Next
                tx.Commit()
            End Using
        End Using
        Return adet
    End Function

    ' Tanımlı raflar (Raflar ekranından oluşturulur).
    Public Shared Function Rafler() As List(Of String)
        Return RafRepository.Adlar()
    End Function

    ' Kalıbı rafa yerleştirir (boş raf = raftan al). Kalıp daha önce hiç kaydedilmemişse kaydı oluşturulur.
    Public Shared Sub RafYaz(kod As String, raf As String, kullanici As String)
        Dim a As String = TrMetin.Katla(kod.Trim())
        raf = RafRepository.Normal(raf)
        If raf IsNot Nothing AndAlso Not RafRepository.Var(raf) Then
            Throw New InvalidOperationException("'" & raf & "' adında bir raf tanımlı değil. Önce Raflar ekranından rafı oluşturun.")
        End If
        Using c As SQLiteConnection = Db.Ac()
            Using cmd As SQLiteCommand = Db.Komut(c, Nothing,
                    "INSERT INTO KalipLokasyon (Anahtar, KalipKodu, Raf, SonDegisiklik, SonKullanici) VALUES (?,?,?,?,?) " &
                    "ON CONFLICT(Anahtar) DO UPDATE SET Raf = excluded.Raf, SonDegisiklik = excluded.SonDegisiklik, SonKullanici = excluded.SonKullanici",
                    a, kod.Trim(), Bos(raf), Db.Prm(DbType.DateTime, Date.Now), Bos(kullanici))
                cmd.ExecuteNonQuery()
            End Using
        End Using
    End Sub

    ' Kalıbın tüm talep geçmişi (en yeni önce).
    Public Shared Function TalepGecmisi(anahtar As String) As DataTable
        Return Db.Tablo(
            "SELECT t.TalepNo AS TalepNo, k.TalepSira AS TalepSira, t.TalepTarihi AS TalepTarihi, t.Bolum AS Bolum, t.Statu AS TalepStatu, " &
            "k.KalipStatu AS KalipStatu, k.Lokasyon AS Lokasyon, k.TashihSayisi AS TashihSayisi, t.Oncelik AS Oncelik, t.TalepEden AS TalepEden, " &
            "t.TerminTarihi AS TerminTarihi, k.Aciklama AS Aciklama FROM TalepKalip k JOIN Talep t ON t.Id = k.TalepId " &
            "WHERE k.Anahtar = ? ORDER BY k.Id DESC", anahtar)
    End Function

    ' Statü / lokasyon değişiklik geçmişi.
    Public Shared Function Hareketler(anahtar As String) As DataTable
        Return Db.Tablo(
            "SELECT Tarih, TalepNo, EskiStatu, YeniStatu, EskiLokasyon, YeniLokasyon, Kullanici FROM KalipHareket WHERE Anahtar = ? ORDER BY Id DESC", anahtar)
    End Function

    ' Kalıp Takip tablosundaki işlem / tashih kayıtları.
    Public Shared Function TakipKayitlari(anahtar As String) As DataTable
        Return Db.Tablo(
            "SELECT GelisTarihi, BitisTarihi, TashihSayi, Hafta, Tur, ParcaAdi, KalipTuru, ToplamZaman FROM Kalip " &
            "WHERE KATLA(ParcaKodu) = ? ORDER BY GelisTarihi DESC, Id DESC", anahtar)
    End Function

    Private Shared Function Bos(s As String) As Object
        If String.IsNullOrWhiteSpace(s) Then Return Nothing
        Return s.Trim()
    End Function

    ' Tek kalıbı ekler / günceller (elle tanım). Lokasyon ve sahip bölüm doluysa onları da işler.
    Public Shared Sub Kaydet(kod As String, ad As String, tur As String, tepe As String, notlar As String,
                             lokasyon As String, sahip As String, raf As String, kullanici As String, bosuTemizle As Boolean)
        raf = RafRepository.Normal(raf)
        If raf IsNot Nothing AndAlso Not RafRepository.Var(raf) Then
            Throw New InvalidOperationException("'" & raf & "' adında bir raf tanımlı değil. Önce Raflar ekranından rafı oluşturun.")
        End If
        Using c As SQLiteConnection = Db.Ac()
            Using tx As SQLiteTransaction = c.BeginTransaction()
                Yaz(c, tx, kod, ad, tur, tepe, notlar, lokasyon, sahip, raf, kullanici, bosuTemizle)
                tx.Commit()
            End Using
        End Using
    End Sub

    Private Shared Sub Yaz(c As SQLiteConnection, tx As SQLiteTransaction, kod As String, ad As String, tur As String, tepe As String,
                           notlar As String, lokasyon As String, sahip As String, raf As String, kullanici As String, bosuTemizle As Boolean)
        Dim a As String = TrMetin.Katla(kod.Trim())
        raf = RafRepository.Normal(raf)
        If raf IsNot Nothing Then
            Using cmd As SQLiteCommand = Db.Komut(c, tx, "INSERT OR IGNORE INTO Raf (Ad) VALUES (?)", raf)
                cmd.ExecuteNonQuery()
            End Using
        End If
        Dim zaman As SQLiteParameter = Db.Prm(DbType.DateTime, Date.Now)
        Dim guncelle As String
        If bosuTemizle Then
            guncelle = "KalipAdi = excluded.KalipAdi, KalipTuru = excluded.KalipTuru, TepeKodu = excluded.TepeKodu, Notlar = excluded.Notlar"
        Else
            guncelle = "KalipAdi = COALESCE(excluded.KalipAdi, KalipTanim.KalipAdi), KalipTuru = COALESCE(excluded.KalipTuru, KalipTanim.KalipTuru), " &
                       "TepeKodu = COALESCE(excluded.TepeKodu, KalipTanim.TepeKodu), Notlar = COALESCE(excluded.Notlar, KalipTanim.Notlar)"
        End If
        Using cmd As SQLiteCommand = Db.Komut(c, tx,
                "INSERT INTO KalipTanim (Anahtar, KalipKodu, KalipAdi, KalipTuru, TepeKodu, Notlar, SonDegisiklik, SonKullanici) VALUES (?,?,?,?,?,?,?,?) " &
                "ON CONFLICT(Anahtar) DO UPDATE SET KalipKodu = excluded.KalipKodu, " & guncelle & ", SonDegisiklik = excluded.SonDegisiklik, SonKullanici = excluded.SonKullanici",
                a, kod.Trim(), Bos(ad), Bos(tur), Bos(tepe), Bos(notlar), zaman, Bos(kullanici))
            cmd.ExecuteNonQuery()
        End Using
        If bosuTemizle OrElse Not String.IsNullOrWhiteSpace(lokasyon) OrElse Not String.IsNullOrWhiteSpace(sahip) OrElse Not String.IsNullOrWhiteSpace(raf) Then
            Dim rafGuncelle As String = If(bosuTemizle, "Raf = excluded.Raf", "Raf = COALESCE(excluded.Raf, KalipLokasyon.Raf)")
            Using cmd As SQLiteCommand = Db.Komut(c, tx,
                    "INSERT INTO KalipLokasyon (Anahtar, KalipKodu, Lokasyon, SahipBolum, Raf, SonDegisiklik, SonKullanici) VALUES (?,?,?,?,?,?,?) " &
                    "ON CONFLICT(Anahtar) DO UPDATE SET KalipKodu = excluded.KalipKodu, " &
                    "Lokasyon = COALESCE(excluded.Lokasyon, KalipLokasyon.Lokasyon), SahipBolum = COALESCE(excluded.SahipBolum, KalipLokasyon.SahipBolum), " &
                    rafGuncelle & ", SonDegisiklik = excluded.SonDegisiklik, SonKullanici = excluded.SonKullanici",
                    a, kod.Trim(), Bos(lokasyon), If(String.IsNullOrWhiteSpace(sahip), Nothing, sahip.Trim().ToUpper(Kultur.Tr)), Bos(raf),
                    Db.Prm(DbType.DateTime, Date.Now), Bos(kullanici))
                cmd.ExecuteNonQuery()
            End Using
        End If
    End Sub

    Private Shared ReadOnly KodAdlari As String() = {"kalipkodu", "parcakodu", "kod", "kalipno"}
    Private Shared ReadOnly AdAdlari As String() = {"kalipadi", "parcaadi", "ad", "adi", "tanim", "aciklama"}
    Private Shared ReadOnly TurAdlari As String() = {"kalipturu", "tur", "turu"}
    Private Shared ReadOnly TepeAdlari As String() = {"tepekodu", "tepe"}
    Private Shared ReadOnly LokAdlari As String() = {"lokasyon", "yer", "konum"}
    Private Shared ReadOnly SahipAdlari As String() = {"sahipbolum", "bolum", "sahip", "ana lokasyon", "analokasyon"}
    Private Shared ReadOnly RafAdlari As String() = {"raf", "rafno", "rafadi", "rafyeri"}
    Private Shared ReadOnly NotAdlari As String() = {"notlar", "not", "notu"}

    Private Shared Function Norm(s As String) As String
        Dim k As String = TrMetin.Katla(If(s, "").Trim())
        Return New String(k.Where(Function(ch) Char.IsLetterOrDigit(ch)).ToArray())
    End Function

    Private Shared Function KolonBul(baslik As List(Of String), adaylar As String()) As Integer
        For i As Integer = 0 To baslik.Count - 1
            Dim hedef As String = baslik(i)
            If adaylar.Any(Function(x) Norm(x) = hedef) Then Return i
        Next
        Return -1
    End Function

    Private Shared Function Al(satir As List(Of String), i As Integer) As String
        If i < 0 OrElse i >= satir.Count Then Return Nothing
        Dim v As String = satir(i)
        If String.IsNullOrWhiteSpace(v) Then Return Nothing
        Return v.Trim()
    End Function

    Friend Shared Function CsvOku(yol As String) As List(Of List(Of String))
        Dim sonuc As New List(Of List(Of String))()
        Dim metin As String = File.ReadAllText(yol, Encoding.UTF8)
        If metin.Length > 0 AndAlso metin(0) = ChrW(&HFEFF) Then metin = metin.Substring(1)
        If metin.Length = 0 Then Return sonuc
        ' Ayırıcı: ilk satırda (tırnak dışında) en çok geçen karakter
        Dim sayac As New Dictionary(Of Char, Integer) From {{";"c, 0}, {","c, 0}, {ControlChars.Tab, 0}}
        Dim ic As Boolean = False
        For Each ch0 As Char In metin
            If ch0 = """"c Then ic = Not ic
            If Not ic AndAlso (ch0 = ControlChars.Lf OrElse ch0 = ControlChars.Cr) Then Exit For
            If Not ic AndAlso sayac.ContainsKey(ch0) Then sayac(ch0) += 1
        Next
        Dim ayirici As Char = ";"c
        Dim enCok As Integer = sayac(";"c)
        For Each kv As KeyValuePair(Of Char, Integer) In sayac
            If kv.Value > enCok Then
                enCok = kv.Value
                ayirici = kv.Key
            End If
        Next
        ' RFC 4180: tırnaklı hücrelerde ayırıcı ve satır sonu serbest, "" = tek tırnak (Excel'in kaydettiği biçim)
        Dim satir As New List(Of String)()
        Dim hucre As New StringBuilder()
        Dim tirnakta As Boolean = False
        Dim i As Integer = 0
        While i < metin.Length
            Dim ch As Char = metin(i)
            If tirnakta Then
                If ch = """"c Then
                    If i + 1 < metin.Length AndAlso metin(i + 1) = """"c Then
                        hucre.Append(""""c)
                        i += 1
                    Else
                        tirnakta = False
                    End If
                Else
                    hucre.Append(ch)
                End If
            ElseIf ch = """"c AndAlso hucre.Length = 0 Then
                tirnakta = True
            ElseIf ch = ayirici Then
                satir.Add(hucre.ToString().Trim())
                hucre.Clear()
            ElseIf ch = ControlChars.Cr Then
                ' satır sonu çiftinin ilk karakteri: yoksay
            ElseIf ch = ControlChars.Lf Then
                satir.Add(hucre.ToString().Trim())
                hucre.Clear()
                If satir.Any(Function(x) x.Length > 0) Then sonuc.Add(satir)
                satir = New List(Of String)()
            Else
                hucre.Append(ch)
            End If
            i += 1
        End While
        satir.Add(hucre.ToString().Trim())
        If satir.Any(Function(x) x.Length > 0) Then sonuc.Add(satir)
        Return sonuc
    End Function

    Friend Shared Function ExcelOku(yol As String) As List(Of List(Of String))
        Dim sonuc As New List(Of List(Of String))()
        Using x As New XlsxOkuyucu(yol)
            Dim sayfa As String = x.IlkSayfa()
            If sayfa Is Nothing Then Throw New InvalidDataException("Excel dosyasında sayfa yok.")
            For Each satir As Dictionary(Of Integer, String) In x.Satirlar(sayfa)
                Dim son As Integer = If(satir.Count = 0, -1, satir.Keys.Max())
                Dim l As New List(Of String)()
                For i As Integer = 0 To son
                    Dim v As String = Nothing
                    satir.TryGetValue(i, v)
                    l.Add(If(v, ""))
                Next
                sonuc.Add(l)
            Next
        End Using
        Return sonuc
    End Function

    ' Excel (.xlsx) ya da CSV dosyasından kalıp listesi yükler. İlk satır başlıktır; 'Kalıp Kodu' sütunu zorunludur.
    ' Dönüş: sonuç özeti. Mevcut kalıplarda boş hücreler eski bilgiyi silmez.
    Public Shared Function DosyadanYukle(yol As String, kullanici As String) As String
        Dim ham As List(Of List(Of String)) = If(yol.EndsWith(".csv", StringComparison.OrdinalIgnoreCase) OrElse yol.EndsWith(".txt", StringComparison.OrdinalIgnoreCase), CsvOku(yol), ExcelOku(yol))
        If ham.Count < 2 Then Throw New InvalidDataException("Dosyada başlık satırı ve en az bir kalıp satırı olmalı.")
        Dim baslik As List(Of String) = ham(0).Select(Function(h) Norm(h)).ToList()
        Dim iKod As Integer = KolonBul(baslik, KodAdlari)
        If iKod < 0 Then Throw New InvalidDataException("'Kalıp Kodu' sütunu bulunamadı. İlk satırda 'Kalıp Kodu' başlığı olmalı (isteğe bağlı: Kalıp Adı, Kalıp Türü, Tepe Kodu, Lokasyon, Raf, Sahip Bölüm, Notlar).")
        Dim iAd As Integer = KolonBul(baslik, AdAdlari)
        Dim iTur As Integer = KolonBul(baslik, TurAdlari)
        Dim iTepe As Integer = KolonBul(baslik, TepeAdlari)
        Dim iLok As Integer = KolonBul(baslik, LokAdlari)
        Dim iSahip As Integer = KolonBul(baslik, SahipAdlari)
        Dim iNot As Integer = KolonBul(baslik, NotAdlari)
        Dim iRaf As Integer = KolonBul(baslik, RafAdlari)

        Dim yeni As Integer = 0
        Dim guncellenen As Integer = 0
        Dim atlanan As Integer = 0
        Dim gecersizLok As Integer = 0
        Dim gecerliLok As New HashSet(Of String)()
        For Each r As DataRow In Db.Tablo("SELECT Ad FROM Lokasyon").Rows
            gecerliLok.Add(TrMetin.Katla(Convert.ToString(r("Ad")).Trim()))
        Next
        Using c As SQLiteConnection = Db.Ac()
            Using tx As SQLiteTransaction = c.BeginTransaction()
                For i As Integer = 1 To ham.Count - 1
                    Dim kod As String = Al(ham(i), iKod)
                    If kod Is Nothing Then
                        atlanan += 1
                        Continue For
                    End If
                    Dim varMi As Boolean
                    Using cmd As SQLiteCommand = Db.Komut(c, tx, "SELECT COUNT(*) FROM KalipTanim WHERE Anahtar = ?", TrMetin.Katla(kod))
                        varMi = Convert.ToInt32(cmd.ExecuteScalar()) > 0
                    End Using
                    Dim lokHucre As String = Al(ham(i), iLok)
                    If lokHucre IsNot Nothing AndAlso gecerliLok.Count > 0 AndAlso Not gecerliLok.Contains(TrMetin.Katla(lokHucre)) Then
                        gecersizLok += 1
                        lokHucre = Nothing
                    End If
                    Yaz(c, tx, kod, Al(ham(i), iAd), Al(ham(i), iTur), Al(ham(i), iTepe), Al(ham(i), iNot), lokHucre, Al(ham(i), iSahip), Al(ham(i), iRaf), kullanici, False)
                    If varMi Then guncellenen += 1 Else yeni += 1
                Next
                tx.Commit()
            End Using
        End Using
        Return yeni.ToString() & " yeni kalıp eklendi, " & guncellenen.ToString() & " kalıp güncellendi" & If(atlanan > 0, ", " & atlanan.ToString() & " satır kodu boş olduğu için atlandı", "") &
               If(gecersizLok > 0, "; " & gecersizLok.ToString() & " satırdaki lokasyon, lokasyon listenizde olmadığı için yazılmadı.", ".")
    End Function

    ''' <summary>Bir kalıp koduna bağlı kayıt sayıları: Kalıp Takip kaydı, talep satırı, hareket.</summary>
    Public Shared Sub Kullanim(kod As String, ByRef takip As Long, ByRef talep As Long, ByRef hareket As Long)
        Dim a As String = TrMetin.Katla(If(kod, "").Trim())
        takip = Convert.ToInt64(Db.Skaler("SELECT COUNT(*) FROM Kalip WHERE KATLA(ParcaKodu) = ?", a))
        talep = Convert.ToInt64(Db.Skaler("SELECT COUNT(*) FROM TalepKalip WHERE Anahtar = ?", a))
        hareket = Convert.ToInt64(Db.Skaler("SELECT COUNT(*) FROM KalipHareket WHERE Anahtar = ?", a))
    End Sub

    ''' <summary>
    ''' Kalıp kodunu düzeltir: tanım, sahip/raf, talep satırları, hareketler ve Kalıp Takip kayıtları yeni koda taşınır.
    ''' Yeni kod zaten varsa kayıtlar o kalıba birleşir (eski tanım/lokasyon satırı silinir, yeni olan korunur).
    ''' </summary>
    Public Shared Function KodDuzelt(eski As String, yeni As String, kullanici As String) As String
        Dim e As String = If(eski, "").Trim()
        Dim y As String = If(yeni, "").Trim()
        If e.Length = 0 Then Throw New InvalidOperationException("Eski kod boş olamaz.")
        If y.Length = 0 Then Throw New InvalidOperationException("Yeni kod boş olamaz.")
        Dim aE As String = TrMetin.Katla(e)
        Dim aY As String = TrMetin.Katla(y)
        Dim takip, talep, hareket As Long
        Kullanim(e, takip, talep, hareket)
        Dim tanimVar As Long = Convert.ToInt64(Db.Skaler("SELECT COUNT(*) FROM KalipTanim WHERE Anahtar = ?", aE)) + Convert.ToInt64(Db.Skaler("SELECT COUNT(*) FROM KalipLokasyon WHERE Anahtar = ?", aE))
        If takip + talep + hareket + tanimVar = 0 Then Throw New InvalidOperationException("'" & e & "' koduna ait kayıt bulunamadı.")
        Dim birlesti As Boolean = (aY <> aE) AndAlso (Convert.ToInt64(Db.Skaler("SELECT COUNT(*) FROM KalipTanim WHERE Anahtar = ?", aY)) + Convert.ToInt64(Db.Skaler("SELECT COUNT(*) FROM KalipLokasyon WHERE Anahtar = ?", aY)) + Convert.ToInt64(Db.Skaler("SELECT COUNT(*) FROM TalepKalip WHERE Anahtar = ?", aY)) + Convert.ToInt64(Db.Skaler("SELECT COUNT(*) FROM Kalip WHERE KATLA(ParcaKodu) = ?", aY)) > 0)
        Dim talepIdleri As New List(Of Integer)()
        For Each r As DataRow In Db.Tablo("SELECT DISTINCT TalepId FROM TalepKalip WHERE Anahtar = ?", aE).Rows
            talepIdleri.Add(Convert.ToInt32(r("TalepId")))
        Next
        Using c As SQLiteConnection = Db.Ac()
            Using tx As SQLiteTransaction = c.BeginTransaction()
                For Each tablo As String In New String() {"KalipTanim", "KalipLokasyon"}
                    If aY = aE Then
                        Using cmd As SQLiteCommand = Db.Komut(c, tx, "UPDATE " & tablo & " SET KalipKodu = ? WHERE Anahtar = ?", y, aE)
                            cmd.ExecuteNonQuery()
                        End Using
                    Else
                        Dim hedefte As Long
                        Using cmd As SQLiteCommand = Db.Komut(c, tx, "SELECT COUNT(*) FROM " & tablo & " WHERE Anahtar = ?", aY)
                            hedefte = Convert.ToInt64(cmd.ExecuteScalar())
                        End Using
                        If hedefte > 0 Then
                            Using cmd As SQLiteCommand = Db.Komut(c, tx, "DELETE FROM " & tablo & " WHERE Anahtar = ?", aE)
                                cmd.ExecuteNonQuery()
                            End Using
                        Else
                            Using cmd As SQLiteCommand = Db.Komut(c, tx, "UPDATE " & tablo & " SET Anahtar = ?, KalipKodu = ? WHERE Anahtar = ?", aY, y, aE)
                                cmd.ExecuteNonQuery()
                            End Using
                        End If
                    End If
                Next
                Using cmd As SQLiteCommand = Db.Komut(c, tx, "UPDATE TalepKalip SET KalipKodu = ?, Anahtar = ? WHERE Anahtar = ?", y, aY, aE)
                    cmd.ExecuteNonQuery()
                End Using
                Using cmd As SQLiteCommand = Db.Komut(c, tx, "UPDATE KalipHareket SET KalipKodu = ?, Anahtar = ? WHERE Anahtar = ?", y, aY, aE)
                    cmd.ExecuteNonQuery()
                End Using
                Using cmd As SQLiteCommand = Db.Komut(c, tx, "UPDATE Kalip SET ParcaKodu = ? WHERE KATLA(ParcaKodu) = ?", y, aE)
                    cmd.ExecuteNonQuery()
                End Using
                For Each id As Integer In talepIdleri
                    Using cmd As SQLiteCommand = Db.Komut(c, tx, "UPDATE Talep SET KalipKodu = COALESCE((SELECT group_concat(KalipKodu, ', ') FROM TalepKalip WHERE TalepId = ?), '-') WHERE Id = ?", id, id)
                        cmd.ExecuteNonQuery()
                    End Using
                Next
                tx.Commit()
            End Using
        End Using
        Return e & " → " & y & " olarak düzeltildi (" & takip.ToString() & " Kalıp Takip kaydı, " & talep.ToString() & " talep satırı taşındı" & If(birlesti, "; mevcut kalıpla birleştirildi", "") & ")."
    End Function

    ''' <summary>
    ''' Kalıbı Kalıp Listesi'nden siler (tanım, sahip/raf, hareketler). Kalıp Takip kaydı varsa silinmez.
    ''' Talep geçmişi varsa yalnızca zorla=True ile silinir; bu durumda talep satırları da kalkar.
    ''' </summary>
    Public Shared Function KalipSil(kod As String, zorla As Boolean) As String
        Dim k As String = If(kod, "").Trim()
        If k.Length = 0 Then Throw New InvalidOperationException("Kalıp kodu boş olamaz.")
        Dim a As String = TrMetin.Katla(k)
        Dim takip, talep, hareket As Long
        Kullanim(k, takip, talep, hareket)
        If takip > 0 Then Throw New InvalidOperationException("'" & k & "' için " & takip.ToString() & " Kalıp Takip kaydı var; silinemez. Kod yanlış yazılmışsa 'Kodu düzelt' ile doğru koda taşıyın.")
        If talep > 0 AndAlso Not zorla Then Throw New InvalidOperationException("'" & k & "' " & talep.ToString() & " talep satırında geçiyor; silinemez. Kod yanlışsa 'Kodu düzelt' ile doğru koda taşıyın; gerçekten silmek için 'geçmişiyle birlikte sil' kullanın.")
        Dim talepIdleri As New List(Of Integer)()
        For Each r As DataRow In Db.Tablo("SELECT DISTINCT TalepId FROM TalepKalip WHERE Anahtar = ?", a).Rows
            talepIdleri.Add(Convert.ToInt32(r("TalepId")))
        Next
        Using c As SQLiteConnection = Db.Ac()
            Using tx As SQLiteTransaction = c.BeginTransaction()
                For Each sql As String In New String() {"DELETE FROM KalipTanim WHERE Anahtar = ?", "DELETE FROM KalipLokasyon WHERE Anahtar = ?", "DELETE FROM KalipHareket WHERE Anahtar = ?", "DELETE FROM TalepKalip WHERE Anahtar = ?"}
                    Using cmd As SQLiteCommand = Db.Komut(c, tx, sql, a)
                        cmd.ExecuteNonQuery()
                    End Using
                Next
                For Each id As Integer In talepIdleri
                    Using cmd As SQLiteCommand = Db.Komut(c, tx, "UPDATE Talep SET KalipKodu = COALESCE((SELECT group_concat(KalipKodu, ', ') FROM TalepKalip WHERE TalepId = ?), '-') WHERE Id = ?", id, id)
                        cmd.ExecuteNonQuery()
                    End Using
                Next
                tx.Commit()
            End Using
        End Using
        Return k & " silindi."
    End Function

    ''' <summary>Verilen kodlardan hiçbir kaydı (Kalıp Takip, talep, hareket) olmayanları siler; diğerleri atlanır.</summary>
    Public Shared Function TemizSil(kodlar As IEnumerable(Of String), ByRef atlanan As Integer) As Integer
        Dim silinen As Integer = 0
        atlanan = 0
        For Each kod As String In kodlar
            If String.IsNullOrWhiteSpace(kod) Then Continue For
            Dim takip, talep, hareket As Long
            Kullanim(kod, takip, talep, hareket)
            If takip + talep + hareket > 0 Then
                atlanan += 1
                Continue For
            End If
            Dim a As String = TrMetin.Katla(kod.Trim())
            Using c As SQLiteConnection = Db.Ac()
                Using tx As SQLiteTransaction = c.BeginTransaction()
                    Using cmd As SQLiteCommand = Db.Komut(c, tx, "DELETE FROM KalipTanim WHERE Anahtar = ?", a)
                        cmd.ExecuteNonQuery()
                    End Using
                    Using cmd As SQLiteCommand = Db.Komut(c, tx, "DELETE FROM KalipLokasyon WHERE Anahtar = ?", a)
                        cmd.ExecuteNonQuery()
                    End Using
                    tx.Commit()
                End Using
            End Using
            silinen += 1
        Next
        Return silinen
    End Function
End Class


' Raf tanımları (Raflar ekranı). Kalıba yalnızca burada tanımlı bir raf atanabilir; Excel yüklemede yeni raflar otomatik tanımlanır.
Public Class RafRepository
    ' Ad: boşluklar kırpılır, BÜYÜK harfe çevrilir; boşsa Nothing.
    Public Shared Function Normal(raf As String) As String
        If String.IsNullOrWhiteSpace(raf) Then Return Nothing
        Return raf.Trim().ToUpper(Kultur.Tr)
    End Function

    Public Shared Function Adlar() As List(Of String)
        Dim sonuc As New List(Of String)()
        Try
            For Each r As DataRow In Db.Tablo("SELECT Ad FROM Raf ORDER BY Ad").Rows
                sonuc.Add(Convert.ToString(r("Ad")))
            Next
        Catch
        End Try
        Return sonuc
    End Function

    ' Tek bir raf tanımlar (kamera ile okutulan yeni raf). Zaten varsa sessizce geçer.
    Public Shared Function Ekle(raf As String) As String
        Dim n As String = Normal(raf)
        If n Is Nothing Then Throw New InvalidOperationException("Raf adı boş olamaz.")
        Using c As SQLiteConnection = Db.Ac()
            Using cmd As SQLiteCommand = Db.Komut(c, Nothing, "INSERT OR IGNORE INTO Raf (Ad) VALUES (?)", n)
                cmd.ExecuteNonQuery()
            End Using
        End Using
        Return n
    End Function

    Public Shared Function Var(raf As String) As Boolean
        Dim t As DataTable = Db.Tablo("SELECT 1 FROM Raf WHERE Ad = ?", raf)
        Return t.Rows.Count > 0
    End Function

    ' Ekran: Ad, KalipSayisi (rafta bulunan kalıp adedi).
    Public Shared Function Listele() As DataTable
        Return Db.Tablo("SELECT r.Ad AS Ad, (SELECT COUNT(*) FROM KalipLokasyon l WHERE l.Raf = r.Ad) AS KalipSayisi FROM Raf r ORDER BY r.Ad")
    End Function

    ' Verilen listeyi kaydeder: yeni olanlar eklenir; listede olmayan ve içinde kalıp bulunmayan raflar silinir.
    ' Kalıp bulunan bir raf listeden çıkarılırsa InvalidOperationException.
    Public Shared Sub Kaydet(adlar As List(Of String))
        Dim temiz As New List(Of String)()
        For Each a As String In adlar
            Dim n As String = Normal(a)
            If n Is Nothing Then Continue For
            If temiz.Contains(n) Then Throw New InvalidOperationException("'" & n & "' raf adı birden fazla kez yazılmış.")
            temiz.Add(n)
        Next
        Using c As SQLiteConnection = Db.Ac()
            Using tx As SQLiteTransaction = c.BeginTransaction()
                Dim eskiler As New List(Of String)()
                Using cmd As SQLiteCommand = Db.Komut(c, tx, "SELECT Ad FROM Raf")
                    Using r As SQLiteDataReader = cmd.ExecuteReader()
                        While r.Read()
                            eskiler.Add(Convert.ToString(r("Ad")))
                        End While
                    End Using
                End Using
                For Each eski As String In eskiler
                    If temiz.Contains(eski) Then Continue For
                    Dim adet As Long
                    Using cmd As SQLiteCommand = Db.Komut(c, tx, "SELECT COUNT(*) FROM KalipLokasyon WHERE Raf = ?", eski)
                        adet = Convert.ToInt64(cmd.ExecuteScalar())
                    End Using
                    If adet > 0 Then
                        Throw New InvalidOperationException("'" & eski & "' rafında " & adet.ToString() & " kalıp var; silinemez. Önce kalıpları başka rafa taşıyın.")
                    End If
                    Using cmd As SQLiteCommand = Db.Komut(c, tx, "DELETE FROM Raf WHERE Ad = ?", eski)
                        cmd.ExecuteNonQuery()
                    End Using
                Next
                For Each n As String In temiz
                    Using cmd As SQLiteCommand = Db.Komut(c, tx, "INSERT OR IGNORE INTO Raf (Ad) VALUES (?)", n)
                        cmd.ExecuteNonQuery()
                    End Using
                Next
                tx.Commit()
            End Using
        End Using
    End Sub
End Class
