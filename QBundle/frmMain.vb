﻿Public Class frmMain
    Private Delegate Sub DUpdate(ByVal [AppId] As Integer, ByVal [Operation] As Integer, ByVal [data] As String)
    Private Delegate Sub DStarting(ByVal [AppId] As Integer)
    Private Delegate Sub DStoped(ByVal [AppId] As Integer)
    Private Delegate Sub DAborted(ByVal [AppId] As Integer, ByVal [data] As String)
    Private Delegate Sub DAPIResult(ByVal [data] As String)
    Private Delegate Sub DNewUpdatesAvilable()
    Public Console(1) As List(Of String)
    Public Running As Boolean
    Public Updateinfo As String
    Public Repositories() As String
    Private LastException As Date
    Private WithEvents APITimer As Timer

#Region " Form Events "
    Private Sub frmMain_Load(sender As Object, e As EventArgs) Handles MyBase.Load
        Q = New clsQ

        QGlobal.BaseDir = Application.StartupPath
        If Not QGlobal.BaseDir.EndsWith("\") Then QGlobal.BaseDir &= "\"
        ' Q.settings = New clsSettings
        ' Q.settings.LoadSettings()
        ' Q.Accounts = New clsAccounts
        ' Q.LoadAccounts()

        QB.Generic.CheckCommandArgs()
        If Q.settings.AlwaysAdmin And Not QB.Generic.IsAdmin Then
            'restartme as admin
            QB.Generic.RestartAsAdmin()
            End
        End If
        If QB.Generic.DebugMe Then Me.Text = Me.Text & " (DebugMode)"
        LastException = Now 'for brs exception monitoring

        If Not QB.Generic.CheckWritePermission Then
            MsgBox("Burst Wallet launcher do not have writepermission to it's own folder. Please move to another location or change the permissions.", MsgBoxStyle.Critical Or MsgBoxStyle.OkOnly, "Permissions")
            End
        End If
        '################################
        'Start classes
        '################################
        Q.App = New clsApp
        Q.App.SetLocalInfo()
        For i As Integer = 0 To UBound(Console)
            Console(i) = New List(Of String)
        Next
        QB.Generic.CheckUpgrade() 'if there is any upgradescenarios
        If Q.settings.FirstRun Then
            frmFirstTime.ShowDialog()
        End If
        If Q.settings.FirstRun Then
            End
        End If



        If Q.settings.CheckForUpdates Then
            Q.App.StartUpdateNotifications()
            AddHandler Q.App.UpdateAvailable, AddressOf NewUpdatesAvilable
        End If
        SetDbInfo()
        lblWallet.Text = "Burst wallet v" & Q.App.GetLocalVersion(QGlobal.AppNames.BRS, False)

        If Q.settings.Cpulimit = 0 Or Q.settings.Cpulimit > Environment.ProcessorCount Then 'need to set correct cpu
            Select Case Environment.ProcessorCount
                Case 1
                    Q.settings.Cpulimit = 1
                Case 2
                    Q.settings.Cpulimit = 1
                Case 4
                    Q.settings.Cpulimit = 3
                Case Else
                    Q.settings.Cpulimit = Environment.ProcessorCount - 2
            End Select
        End If
        APITimer = New Timer
        Q.ProcHandler = New clsProcessHandler
        AddHandler Q.ProcHandler.Started, AddressOf Starting
        AddHandler Q.ProcHandler.Stopped, AddressOf Stopped
        AddHandler Q.ProcHandler.Update, AddressOf ProcEvents
        AddHandler Q.ProcHandler.Aborting, AddressOf Aborted


        StopWalletToolStripMenuItem.Enabled = False
        If Q.settings.QBMode = 0 Then
            StartStop()

        End If
        SetMode(Q.settings.QBMode)


    End Sub
    Private Sub SetMode(ByVal NewMode As Integer)
        Select Case NewMode
            Case 0 ' AIO Mode

                Me.FormBorderStyle = FormBorderStyle.Sizable
                Me.MaximizeBox = True
                Me.Width = 1024
                Me.Height = 768
                Q.settings.QBMode = 0
                Q.settings.SaveSettings()
                Me.Top = (My.Computer.Screen.WorkingArea.Height \ 2) - (Me.Height \ 2)
                Me.Left = (My.Computer.Screen.WorkingArea.Width \ 2) - (Me.Width \ 2)
                pnlAIO.Visible = True
                pnlLauncher.Visible = False

            Case 1 ' Launcher Mode
                Me.FormBorderStyle = FormBorderStyle.FixedDialog
                Me.MaximizeBox = False
                Me.Width = 464
                Me.Height = 181
                Q.settings.QBMode = 1
                Q.settings.SaveSettings()
                Me.Top = (My.Computer.Screen.WorkingArea.Height \ 2) - (Me.Height \ 2)
                Me.Left = (My.Computer.Screen.WorkingArea.Width \ 2) - (Me.Width \ 2)
                pnlAIO.Visible = False
                pnlLauncher.Visible = True
        End Select




    End Sub
    Private Sub frmMain_FormClosing(ByVal sender As Object, ByVal e As System.Windows.Forms.FormClosingEventArgs) Handles Me.FormClosing
        Try
            If Running Then
                MsgBox("You must stop the wallet before you can close the launcher", MsgBoxStyle.OkOnly, "Exit")
                e.Cancel = True
                Exit Sub
            End If
        Catch ex As Exception
            If QB.Generic.DebugMe Then QB.Generic.WriteDebug(ex.StackTrace, ex.Message)
        End Try

    End Sub
#End Region

#Region " Clickabe Objects "
    'buttons
    Private Sub btnStartStop_Click(sender As Object, e As EventArgs) Handles btnStartStop.Click
        StartStop()

    End Sub
    Private Sub StartStop()
        If Running Then
            lblGotoWallet.Visible = False
            btnStartStop.Text = "Stopping"
            btnStartStop.Enabled = False
            APITimer.Enabled = False
            APITimer.Stop()
            StopWallet()
            'stopsequence
            '0 NRS
            '1 Mariap if needed

        Else
            If Not QB.Generic.SanityCheck() Then Exit Sub
            QB.Generic.WriteNRSConfig()
            StartWallet()
            Running = True
            btnStartStop.Enabled = False
            btnStartStop.Text = "Starting"
            StartWalletToolStripMenuItem.Enabled = False
            StopWalletToolStripMenuItem.Enabled = True
        End If
    End Sub

    'labels
    Private Sub lblGotoWallet_LinkClicked(sender As Object, e As LinkLabelLinkClickedEventArgs) Handles lblGotoWallet.LinkClicked

        Dim s() As String = Split(Q.settings.ListenIf, ";")
        Dim url As String = Nothing
        If s(0) = "0.0.0.0" Then
            url = "http://127.0.0.1:" & s(1)
        Else
            url = "http://" & s(0) & ":" & s(1)
        End If
        Process.Start(url)
    End Sub
    Private Sub lblUpdates_Click(sender As Object, e As EventArgs) Handles lblUpdates.Click
        frmUpdate.Show()
    End Sub
    'toolstrips
    Private Sub ExitToolStripMenuItem1_Click(sender As Object, e As EventArgs) Handles ExitToolStripMenuItem1.Click
        Me.Close()
    End Sub
    Private Sub SettingsToolStripMenuItem_Click(sender As Object, e As EventArgs) Handles SettingsToolStripMenuItem.Click
        Dim s As New frmSettings
        s.ShowDialog()
    End Sub
    Private Sub ContributorsToolStripMenuItem_Click(sender As Object, e As EventArgs) Handles ContributorsToolStripMenuItem.Click
        frmContributors.Show()
    End Sub
    Private Sub CheckForUpdatesToolStripMenuItem_Click(sender As Object, e As EventArgs) Handles CheckForUpdatesToolStripMenuItem.Click
        frmUpdate.Show()
    End Sub
    Private Sub ChangeDatabaseToolStripMenuItem_Click(sender As Object, e As EventArgs) Handles ChangeDatabaseToolStripMenuItem.Click
        frmChangeDatabase.Show()
    End Sub
    Private Sub ExportDatabaseToolStripMenuItem_Click(sender As Object, e As EventArgs) Handles ExportDatabaseToolStripMenuItem.Click
        frmExportDb.Show()
    End Sub
    Private Sub ImportDatabaseToolStripMenuItem1_Click(sender As Object, e As EventArgs) Handles ImportDatabaseToolStripMenuItem1.Click
        frmImport.Show()
    End Sub
    Private Sub DeveloperToolStripMenuItem_Click(sender As Object, e As EventArgs) Handles DeveloperToolStripMenuItem.Click
        frmDeveloper.Show()
    End Sub
    Private Sub ConfigureWindowsFirewallToolStripMenuItem_Click(sender As Object, e As EventArgs) Handles ConfigureWindowsFirewallToolStripMenuItem.Click

        Dim msg As String = "Would you like to autmatically configure windows firewall with your wallet connection settings?" & vbCrLf
        msg &= "This will require Administrative priveleges."

        If MsgBox(msg, MsgBoxStyle.Information Or MsgBoxStyle.YesNo, "Windows firewall") = MsgBoxResult.Yes Then

            QB.Generic.SetFirewallFromSettings()

        End If
    End Sub
    Private Sub ViewConsolesToolStripMenuItem_Click(sender As Object, e As EventArgs) Handles ViewConsolesToolStripMenuItem.Click
        frmConsole.Show()
    End Sub
#End Region

#Region " Wallet Events "
    Private Sub Starting(ByVal AppId As Integer)
        If Me.InvokeRequired Then
            Dim d As New DStarting(AddressOf Starting)
            Me.Invoke(d, New Object() {AppId})
            Return
        End If

        Select Case AppId
            Case QGlobal.AppNames.BRS
                lblNrsStatus.Text = "Starting"
                lblNrsStatus.ForeColor = Color.DarkOrange
            Case QGlobal.AppNames.MariaPortable
                LblDbStatus.Text = "Starting"
                LblDbStatus.ForeColor = Color.DarkOrange
        End Select


    End Sub
    Private Sub Stopped(ByVal AppId As Integer)
        If Me.InvokeRequired Then
            Dim d As New DStoped(AddressOf Stopped)
            Me.Invoke(d, New Object() {AppId})
            Return
        End If

        If AppId = QGlobal.AppNames.BRS Then
            APITimer.Enabled = False
            APITimer.Stop()
            lblNrsStatus.Text = "Stopped"
            lblNrsStatus.ForeColor = Color.Red
        End If
        If Q.settings.DbType = QGlobal.DbType.pMariaDB Then
            If AppId = QGlobal.AppNames.MariaPortable Then
                LblDbStatus.Text = "Stopped"
                LblDbStatus.ForeColor = Color.Red
                btnStartStop.Text = "Start Wallet"
                btnStartStop.Enabled = True
                Running = False
            End If
        Else
            btnStartStop.Text = "Start Wallet"
            btnStartStop.Enabled = True
            Running = False
            StartWalletToolStripMenuItem.Enabled = True
            StopWalletToolStripMenuItem.Enabled = False
        End If

    End Sub
    Private Sub ProcEvents(ByVal AppId As Integer, ByVal Operation As Integer, ByVal data As String)
        If Me.InvokeRequired Then
            Dim d As New DUpdate(AddressOf ProcEvents)
            Me.Invoke(d, New Object() {AppId, Operation, data})
            Return
        End If
        'threadsafe here
        Select Case Operation
            Case QGlobal.ProcOp.Stopped  'Stoped
                If AppId = QGlobal.AppNames.BRS Then
                    LblDbStatus.Text = "Stopped"
                    LblDbStatus.ForeColor = Color.Red
                    APITimer.Enabled = False
                    APITimer.Stop()
                End If
                If AppId = QGlobal.AppNames.MariaPortable Then
                    lblNrsStatus.Text = "Stopped"
                    lblNrsStatus.ForeColor = Color.Red
                End If
            Case QGlobal.ProcOp.FoundSignal
                If AppId = QGlobal.AppNames.MariaPortable Then
                    LblDbStatus.Text = "Running"
                    LblDbStatus.ForeColor = Color.DarkGreen

                End If
                If AppId = QGlobal.AppNames.BRS Then
                    lblNrsStatus.Text = "Running"
                    lblNrsStatus.ForeColor = Color.DarkGreen
                    btnStartStop.Text = "Stop Wallet"
                    Running = True
                    btnStartStop.Enabled = True
                    lblGotoWallet.Visible = True
                    APITimer.Interval = "1000"
                    APITimer.Enabled = True
                    APITimer.Start()
                    Dim s() As String = Split(Q.settings.ListenIf, ";")
                    Dim url As String = Nothing
                    If s(0) = "0.0.0.0" Then
                        url = "http://127.0.0.1:" & s(1)
                    Else
                        url = "http://" & s(0) & ":" & s(1)
                    End If
                    wb1.Navigate(url)
                End If
            Case QGlobal.ProcOp.Stopping
                If AppId = QGlobal.AppNames.MariaPortable Then
                    LblDbStatus.Text = "Stopping"
                    LblDbStatus.ForeColor = Color.DarkOrange
                End If
                If AppId = QGlobal.AppNames.BRS Then
                    lblNrsStatus.Text = "Stopping"
                    lblNrsStatus.ForeColor = Color.DarkOrange
                    APITimer.Enabled = False
                    APITimer.Stop()
                End If
            Case QGlobal.ProcOp.ConsoleOut
                If AppId = QGlobal.AppNames.MariaPortable Then
                    Console(1).Add(data)
                    If Console(1).Count = 3001 Then Console(1).RemoveAt(0)
                End If
                If AppId = QGlobal.AppNames.BRS Then
                    Console(0).Add(data)
                    'here we can do error detection
                    If Q.settings.WalletException And LastException.AddHours(1) < Now Then
                        If data.StartsWith("Exception in") Then
                            LastException = Now
                            Q.ProcHandler.ReStartProcess(QGlobal.AppNames.BRS)
                        End If
                    End If

                    If Console(0).Count = 3001 Then Console(0).RemoveAt(0)
                End If
            Case QGlobal.ProcOp.ConsoleErr
                If AppId = QGlobal.AppNames.MariaPortable Then
                    Console(1).Add(data)
                    If Console(1).Count = 3001 Then Console(1).RemoveAt(0)
                End If
                If AppId = QGlobal.AppNames.BRS Then
                    Console(0).Add(data)
                    'here we can do error detection
                    If Q.settings.WalletException And LastException.AddHours(1) < Now Then
                        If data.StartsWith("Exception in") Then
                            LastException = Now
                            Q.ProcHandler.ReStartProcess(QGlobal.AppNames.BRS)
                        End If
                    End If

                    If Console(0).Count = 3001 Then Console(0).RemoveAt(0)
                End If

            Case QGlobal.ProcOp.Err  'Error
                MsgBox("A Unhandled error happend when services tried to start. Console view might give clue to what is wrong. Some services might still be running.", MsgBoxStyle.Critical Or MsgBoxStyle.OkOnly, "Error")
                Running = False
        End Select

    End Sub
    Private Sub Aborted(ByVal AppId As Integer, ByVal Data As String)
        If Me.InvokeRequired Then
            Dim d As New DAborted(AddressOf Aborted)
            Me.Invoke(d, New Object() {AppId, Data})
            Return
        End If
        MsgBox(Data)
        If AppId = QGlobal.AppNames.BRS Then
            lblNrsStatus.Text = "Stopped"
            lblNrsStatus.ForeColor = Color.Red
        End If
        If Q.settings.DbType = QGlobal.DbType.pMariaDB Then
            If AppId = QGlobal.AppNames.MariaPortable Then
                LblDbStatus.Text = "Stopped"
                LblDbStatus.ForeColor = Color.Red
                btnStartStop.Text = "Start Wallet"
                btnStartStop.Enabled = True
                Running = False
            End If
        Else
            btnStartStop.Text = "Start Wallet"
            btnStartStop.Enabled = True
            Running = False
        End If

    End Sub
    Private Sub StartWallet()

        If Q.settings.DbType = QGlobal.DbType.pMariaDB Then 'send startsequence
            Dim pset(1) As clsProcessHandler.pSettings
            pset(0) = New clsProcessHandler.pSettings
            'mariadb
            pset(0).AppId = QGlobal.AppNames.MariaPortable
            pset(0).AppPath = QGlobal.BaseDir & "MariaDb\bin\mysqld.exe"
            pset(0).Cores = 0
            pset(0).Params = "--console"
            pset(0).WorkingDirectory = QGlobal.BaseDir & "MariaDb\bin\"
            pset(0).StartSignal = "ready for connections"
            pset(0).StartsignalMaxTime = 60

            pset(1) = New clsProcessHandler.pSettings
            pset(1).AppId = QGlobal.AppNames.BRS
            If Q.settings.JavaType = QGlobal.AppNames.JavaInstalled Then
                pset(1).AppPath = "java"
            Else
                pset(1).AppPath = QGlobal.BaseDir & "Java\bin\java.exe"
            End If
            pset(1).Cores = Q.settings.Cpulimit
            pset(1).Params = "-cp burst.jar;lib\*;conf brs.Burst"
            pset(1).StartSignal = "Started API server at"
            pset(1).StartsignalMaxTime = 300
            pset(1).WorkingDirectory = QGlobal.BaseDir

            Q.ProcHandler.StartProcessSquence(pset)


        Else 'normal start
            Dim Pset As New clsProcessHandler.pSettings
            Pset.AppId = QGlobal.AppNames.BRS
            If Q.settings.JavaType = QGlobal.AppNames.JavaInstalled Then
                Pset.AppPath = "java"
            Else
                Pset.AppPath = QGlobal.BaseDir & "Java\bin\java.exe"
            End If
            Pset.Cores = Q.settings.Cpulimit
            Pset.Params = "-cp burst.jar;lib\;conf brs.Burst"
            Pset.StartSignal = "Started API server at"
            Pset.StartsignalMaxTime = 300
            Pset.WorkingDirectory = QGlobal.BaseDir

            Q.ProcHandler.StartProcess(Pset)

        End If
    End Sub
    Public Sub StopWallet()
        If Q.settings.DbType = QGlobal.DbType.pMariaDB Then 'send startsequence
            Dim Pid(1) As Object
            Pid(0) = QGlobal.AppNames.BRS
            Pid(1) = QGlobal.AppNames.MariaPortable
            Q.ProcHandler.StopProcessSquence(Pid)
        Else
            Q.ProcHandler.StopProcess(QGlobal.AppNames.BRS)
        End If
    End Sub
#End Region

#Region " Misc "
    Public Sub SetDbInfo()

        Select Case Q.settings.DbType
            Case QGlobal.DbType.FireBird
                lblDbName.Text = Q.App.GetDbNameFromType(QGlobal.DbType.FireBird)
                LblDbStatus.Text = "Embeded"
                LblDbStatus.ForeColor = Color.DarkGreen
            Case QGlobal.DbType.pMariaDB
                lblDbName.Text = Q.App.GetDbNameFromType(QGlobal.DbType.pMariaDB)
                LblDbStatus.Text = "Stopped"
                LblDbStatus.ForeColor = Color.Red
            Case QGlobal.DbType.MariaDB
                lblDbName.Text = Q.App.GetDbNameFromType(QGlobal.DbType.MariaDB)
                LblDbStatus.Text = "Unknown"
                LblDbStatus.ForeColor = Color.DarkOrange
            Case QGlobal.DbType.H2
                lblDbName.Text = Q.App.GetDbNameFromType(QGlobal.DbType.H2)
                LblDbStatus.Text = "Embeded"
                LblDbStatus.ForeColor = Color.DarkGreen
        End Select

    End Sub
    Private Sub NewUpdatesAvilable()
        If Me.InvokeRequired Then
            Dim d As New DNewUpdatesAvilable(AddressOf NewUpdatesAvilable)
            Me.Invoke(d, New Object() {})
            Return
        End If
        Try

            lblUpdates.Visible = True
        Catch ex As Exception
            If QB.Generic.DebugMe Then QB.Generic.WriteDebug(ex.StackTrace, ex.Message)
        End Try
    End Sub
#End Region

#Region " Get Block Info "

    Private Sub APITimer_tick(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles APITimer.Tick
        Dim trda As System.Threading.Thread
        trda = New System.Threading.Thread(AddressOf GetApiData)
        trda.IsBackground = True
        trda.Start()
        trda = Nothing
    End Sub

    Private Sub GetApiData()
        Try
            Dim http As New clsHttp
            Dim s() As String = Split(Q.settings.ListenIf, ";")
            Dim url As String = Nothing
            If s(0) = "0.0.0.0" Then
                url = "http://127.0.0.1:" & s(1)
            Else
                url = "http://" & s(0) & ":" & s(1)
            End If
            Dim Result() As String = Split(http.GetUrl(url & "/burst?requestType=getBlocks&lastIndex=1"), ",")
            Dim msg As String = ""

            For Each Line As String In Result
                If Line.StartsWith(Chr(34) & "height" & Chr(34)) Then
                    '"height":414439
                    msg = Line.Substring(9)
                    Exit For
                End If
            Next

            APIResult(msg)
        Catch ex As Exception

        End Try
    End Sub

    Private Sub APIResult(ByVal Data As String)
        Try
            If Me.InvokeRequired Then
                Dim d As New DAPIResult(AddressOf APIResult)
                Me.Invoke(d, New Object() {Data})
                Return
            End If
            lblBlockInfo.Text = Data
        Catch ex As Exception

        End Try


    End Sub

    Private Sub SwitchToAIOStyleToolStripMenuItem_Click(sender As Object, e As EventArgs) Handles SwitchToAIOStyleToolStripMenuItem.Click
        Q.settings.QBMode = 0
        Q.settings.SaveSettings()
        SetMode(0)
    End Sub

    Private Sub SwitchToLauncherStyleToolStripMenuItem_Click(sender As Object, e As EventArgs) Handles SwitchToLauncherStyleToolStripMenuItem.Click
        Q.settings.QBMode = 1
        Q.settings.SaveSettings()
        SetMode(1)
    End Sub

    Private Sub StartWalletToolStripMenuItem_Click(sender As Object, e As EventArgs) Handles StartWalletToolStripMenuItem.Click
        StartStop()

    End Sub

    Private Sub StopWalletToolStripMenuItem_Click(sender As Object, e As EventArgs) Handles StopWalletToolStripMenuItem.Click
        StartStop()
    End Sub

    Private Sub AddAccountToolStripMenuItem_Click(sender As Object, e As EventArgs) Handles AddAccountToolStripMenuItem.Click
        frmAccounts.Show()
    End Sub


#End Region





End Class