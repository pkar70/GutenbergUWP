

Public Class tListItem
    Public Property sTerm As String
    Public Property iNo As Integer
End Class

' UWAGA:
' nie otwiera pliku IDX, ani IND - dlatego zmieniam na idx.txt, i jest dobrze
' warto przekonwertowac politki - UTF8, nie ANSI

Public NotInheritable Class MainPage
    Inherits Page

    Const _TERMSINWINDOW = 200

    Private mlsTerms As List(Of tListItem) = New List(Of tListItem)
    Private maSeek As Long()

    Private Async Function WypelnListeZrodel(sDefault) As Task
        uiZrodlo.Items.Clear()

        Dim oFold As Windows.Storage.StorageFolder
        oFold = Await Windows.ApplicationModel.Package.Current.InstalledLocation.GetFolderAsync("Dane")

        ' sprobuj iterowaniem
        Try
            For Each oFile As Windows.Storage.StorageFile In Await oFold.GetFilesAsync
                If oFile.Name.ToLower.EndsWith(".htm") Then
                    Dim oNew As ComboBoxItem = New ComboBoxItem
                    oNew.Content = oFile.Name.Substring(0, oFile.Name.Length - 4)
                    uiZrodlo.Items.Add(oNew)
                    If sDefault = oNew.Content Then uiZrodlo.SelectedItem = oNew
                End If
            Next
            Return
        Catch ex As Exception
        End Try

        ' tu wszedl, to znaczy sie nie udalo iterowanie
        If Not File.Exists("Dane\index.txt") Then
            DialogBox("FAIL: błąd iterowania, oraz nie ma pliku index.txt")
            Return
        End If

        Dim aLista As String() = File.ReadAllLines("Dane\index.txt")
        For Each oItem As String In aLista
            Dim oNew As ComboBoxItem = New ComboBoxItem
            oNew.Content = oItem
            uiZrodlo.Items.Add(oNew)
            If sDefault = oNew.Content Then uiZrodlo.SelectedItem = oNew
        Next
    End Function

    Private mbInLoading As Boolean = True
    Private moTimer As DispatcherTimer = Nothing

    Private Async Function GetFolder() As Task(Of Windows.Storage.StorageFolder)
        Dim oFold As Windows.Storage.StorageFolder = Nothing
        Try
            oFold = Await Windows.ApplicationModel.Package.Current.InstalledLocation.GetFolderAsync("Dane")
        Catch ex As Exception
            oFold = Nothing
        End Try
        Return oFold
    End Function

    Private Async Function GetSelectedSource(sExt As String) As Task(Of Windows.Storage.StorageFile)
        Dim sFile As String = ""
        Try
            sFile = TryCast(uiZrodlo.SelectedItem, ComboBoxItem).Content
        Catch ex As Exception
            sFile = ""
        End Try
        If sFile = "" Then Return Nothing
        sFile = sFile & "." & sExt

        Dim oFold As Windows.Storage.StorageFolder = Await GetFolder()
        If oFold Is Nothing Then Return Nothing

        Dim oFile As Windows.Storage.StorageFile = Nothing
        oFile = TryCast(Await oFold.TryGetItemAsync(sFile), Windows.Storage.StorageFile)

        Return oFile

    End Function

    Private Async Function ReadTermList() As Task

        uiStatus.Text = "Reading terms' list..."

        Dim oFile As Windows.Storage.StorageFile = Await GetSelectedSource("txt")
        If oFile Is Nothing Then
            DialogBox("FAIL: cannot open index file")
            Return
        End If

        Dim oRdr As StreamReader
        oRdr = New StreamReader(Await oFile.OpenStreamForReadAsync)
        mlsTerms.Clear()

        Dim iInd As Integer
        While Not oRdr.EndOfStream
            Dim oNew As tListItem = New tListItem

            Dim sTxt As String = oRdr.ReadLine
            If sTxt.StartsWith("Item ") Then
                sTxt = sTxt.Substring(5)
                iInd = sTxt.IndexOf(": ")
                oNew.iNo = CInt(sTxt.Substring(0, iInd))
                oNew.sTerm = sTxt.Substring(iInd + 2)

                mlsTerms.Add(oNew)
            End If
        End While

        uiStatus.Text = ""

    End Function

    Private Sub FillTermList(sQuery As String)

        'int iSkip;
        'POSITION pos;
        'CString sTmp, ;
        'int iItemNo;

        uiListTerms.Items.Clear()

        Dim iLimit As Integer = _TERMSINWINDOW     ' bylo modyfikowalne via Registry 

        Dim bFirst As Boolean = False

        sQuery = sQuery.ToLower()
        If sQuery.Length > 1 AndAlso sQuery.Substring(0, 1) = "^" Then
            sQuery = sQuery.Substring(1)
            bFirst = True
        End If

        'iSkip = mSkipTerms;

        For Each oTerm As tListItem In mlsTerms
            Dim sTmpL As String = oTerm.sTerm.ToLower
            If (bFirst AndAlso sTmpL.IndexOf(sQuery) = 0) OrElse (Not bFirst AndAlso sTmpL.Contains(sQuery)) Then
                ' If (iSkip) Then
                ' {
                '	iSkip--;
                '	Continue While;
                '}
                Dim oNew As ListBoxItem = New ListBoxItem
                oNew.Content = oTerm.sTerm
                oNew.DataContext = oTerm
                oNew.MinHeight = 10
                oNew.Padding = New Thickness(2)
                uiListTerms.Items.Add(oNew)
                iLimit -= 1
                If iLimit < 1 Then Exit For
            End If
        Next

        If iLimit < 1 Then uiListTerms.Items.Add("--skorzystaj z szukania")

    End Sub

    Private Async Function LoadIndex() As Task
        ReDim maSeek(1)
        If mlsTerms.Count < 1 Then Return

        '// znajdz najwiekszy identyfikator
        uiStatus.Text = "Szukam najwiekszego numerka..."
        Dim iMax As Integer = 0
        For Each oItem As tListItem In mlsTerms
            iMax = Math.Max(iMax, oItem.iNo)
        Next

        ' proba innego podejscia...
        iMax = Aggregate c In mlsTerms Into iMax2 = Max(c.iNo)

        ReDim maSeek(iMax + 10)

        Dim oFile As Windows.Storage.StorageFile = Await GetSelectedSource("idx.txt")
        If oFile Is Nothing Then
            DialogBox("FAIL: cannot open IDX file")
            Return
        End If

        '	// *TODO* kontrola czy datafile nie jest nowszy niz index, i w razie czego recreate
        uiStatus.Text = "reading index..."

        ' // a teraz wczytaj index	
        Dim oRdr As StreamReader
        oRdr = New StreamReader(Await oFile.OpenStreamForReadAsync)

        Dim iInd, iArrI As Integer
        Dim iArrV As Long
        Dim sTxt As String
        While Not oRdr.EndOfStream
            sTxt = oRdr.ReadLine
            ' omijamy komentarze
            If sTxt.Substring(0, 1) <> "#" Then
                ' %d -> %d
                iInd = sTxt.IndexOf("->")
                If iInd > 0 Then
                    iArrI = CInt(sTxt.Substring(0, iInd).Trim)
                    ' If iArrI > iMax Then ... 
                    iArrV = CLng(sTxt.Substring(iInd + 2).Trim)
                    maSeek(iArrI) = iArrV
                End If
            End If
        End While

        uiStatus.Text = ""

    End Function

    Private Async Sub uiZrodlo_SelectionChanged(sender As Object, e As SelectionChangedEventArgs) Handles uiZrodlo.SelectionChanged
        If mbInLoading Then Return

        Await ReadTermList()
        FillTermList("")
        Await LoadIndex()
    End Sub

    Private Sub uiSearchTerm_TextChanged(sender As Object, e As TextChangedEventArgs) Handles uiSearchTerm.TextChanged
        If moTimer.IsEnabled Then moTimer.Stop()
        moTimer.Interval = TimeSpan.FromMilliseconds(500)
        moTimer.Start()
    End Sub

    Private Async Sub Page_Loaded(sender As Object, e As RoutedEventArgs)
        mbInLoading = True
        moTimer = New DispatcherTimer
        AddHandler moTimer.Tick, AddressOf TimerTick

        Dim sDefault As String = GetSettingsString("previousSource")
        Await WypelnListeZrodel(sDefault)
        If sDefault = "" AndAlso uiZrodlo.Items.Count > 0 Then
            uiZrodlo.SelectedIndex = 0
            sDefault = TryCast(uiZrodlo.Items(0), ComboBoxItem).Content
            SetSettingsString("previousSource", sDefault)
        End If

        mbInLoading = False
        uiZrodlo_SelectionChanged(Nothing, Nothing)    ' wczytaj indeks
    End Sub

    Private Sub TimerTick(sender As Object, e As Object)
        ' minal czas przy pisaniu searchterm 
        FillTermList(uiSearchTerm.Text)
    End Sub

    Private Async Sub uiListTerms_SelectionChanged(sender As Object, e As SelectionChangedEventArgs) Handles uiListTerms.SelectionChanged
        If e.AddedItems.Count < 1 Then Return

        If e.AddedItems.Count > 1 Then
            DialogBox("ERROR: nie umiem takiego SelectChanged")
            Return
        End If

        Dim oFile As Windows.Storage.StorageFile = Await GetSelectedSource("htm")
        If oFile Is Nothing Then
            DialogBox("Cannot open data file")
            Return
        End If

        Dim oItem As tListItem = TryCast(uiListTerms.SelectedItem, ListBoxItem).DataContext

        Dim sQry As String = "<!-- " & oItem.iNo & " -->"
        Dim sHtml As String = "<html><head><meta http-equiv=""Content-Language"" content=""pl"">" &
            "<meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">"

        '//	sTxt.AppendFormat("<base href=\"file:///%s\">",sLine); // nie dziala :(
        sHtml &= "</head><body>"
        '	// content type ustawiane niżej - bezposrednio w webbrowser, bo tu nie reaguje!

        Dim oRdr As StreamReader = New StreamReader(Await oFile.OpenStreamForReadAsync)

        If oItem.iNo < maSeek.GetUpperBound(0) Then
            '	// *TODO* odpornosc na drobne zmiany mozna osiagnac zmniejszajac troche iSeek
            Dim iSeek As Long
            iSeek = maSeek(oItem.iNo)
            If oRdr.BaseStream.CanSeek Then
                oRdr.BaseStream.Seek(iSeek, SeekOrigin.Begin)
            Else
                uiStatus.Text = "Cannot seek? Using slow search..."
            End If
        End If

        '	// teoretycznie od razu jest na miejscu, ale jakby cos sie przesunelo...
        While Not oRdr.EndOfStream
            Dim sLine As String = oRdr.ReadLine
            If sLine.Contains(sQry) Then
                sHtml = sHtml & sQry
                While Not oRdr.EndOfStream
                    sLine = oRdr.ReadLine
                    If sLine.Contains("<!--") Then Exit While
                    sHtml = sHtml & sLine
                End While

                Exit While
            End If
        End While

        sHtml = sHtml & "</body></html>"

        '	char cDir[1024];
        '	GetCurrentDirectory(1000,cDir);
        '	sLine = cDir;
        '	sLine.Replace("\\","/");
        '	sLine.Replace(" ","%20");
        '	sTmp.Format("src=\"file:///%s/",sLine);
        '	sTxt.Replace("src=\"",sTmp);
        '	sTxt.Replace("SRC=\"",sTmp);
        '	sTmp.Format("href=\"file:///%s/",sLine);
        '	sTxt.Replace("href=\"",sTmp);

        '	SetHTMLCtrlBody(sTxt);
        uiStatus.Text = ""
        uiWebView.NavigateToString(sHtml)
    End Sub
End Class
