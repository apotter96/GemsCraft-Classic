﻿// Copyright 2009-2012 Matvei Stefarov <me@matvei.org>
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Windows.Forms;
using fCraft.Events;
using fCraft.GUI;
using System.Threading;
using fCraft.ServerGUI;

namespace fCraft.ServerGUI
{

    public sealed partial class MainForm : Form
    {
        volatile bool shutdownPending, startupComplete, shutdownComplete;
        const int MaxLinesInLog = 2000,
                  LinesToTrimWhenExceeded = 50;

        public MainForm()
        {
            InitializeComponent();
            Shown += StartUp;
            console.OnCommand += console_Enter;
            logBox.LinkClicked += new LinkClickedEventHandler(Link_Clicked);
            MenuItem[] menuItems = new MenuItem[] { new MenuItem("Copy", new EventHandler(CopyMenuOnClickHandler)) };
            logBox.ContextMenu = new ContextMenu(menuItems);
            logBox.ContextMenu.Popup += new EventHandler(CopyMenuPopupHandler);
            playerList.MouseDoubleClick += new MouseEventHandler(playerList_MouseDoubleClick);
        }

        #region Startup
        Thread startupThread;

        void StartUp(object sender, EventArgs a)
        {
            Logger.Logged += OnLogged;
            Heartbeat.UriChanged += OnHeartbeatUriChanged;
            Server.PlayerListChanged += OnPlayerListChanged;
            Server.ShutdownEnded += OnServerShutdownEnded;
            Text = "LegendCraft v.1.9.0 - starting...";
            startupThread = new Thread(StartupThread);
            startupThread.Name = "LegendCraft ServerGUI Startup";
            startupThread.Start();
        }


        void StartupThread()
        {
#if !DEBUG
            try
            {
#endif
                Server.InitLibrary(Environment.GetCommandLineArgs());
                if (shutdownPending) return;

                Server.InitServer();
                if (shutdownPending) return;

                BeginInvoke((Action)OnInitSuccess);



                // set process priority
                if (!ConfigKey.ProcessPriority.IsBlank())
                {
                    try
                    {
                        Process.GetCurrentProcess().PriorityClass = ConfigKey.ProcessPriority.GetEnum<ProcessPriorityClass>();
                    }
                    catch (Exception)
                    {
                        Logger.Log(LogType.Warning,
                                    "MainForm.StartServer: Could not set process priority, using defaults.");
                    }
                }

                if (shutdownPending) return;
                if (Server.StartServer())
                {
                    startupComplete = true;
                    BeginInvoke((Action)OnStartupSuccess);
                }
                else
                {
                    BeginInvoke((Action)OnStartupFailure);
                }
#if !DEBUG
            }
            catch (Exception ex)
            {
                Logger.LogAndReportCrash("Unhandled exception in ServerGUI.StartUp", "ServerGUI", ex, true);
                Shutdown(ShutdownReason.Crashed, Server.HasArg(ArgKey.ExitOnCrash));
            }
#endif
        }


        void OnInitSuccess()
        {
            Text = "LegendCraft " + " - " + ConfigKey.ServerName.GetString();
        }


        void OnStartupSuccess()
        {
            if (!ConfigKey.HeartbeatEnabled.Enabled())
            {
                uriDisplay.Text = null;
            }
            console.Enabled = true;
            console.Text = "";
        }


        void OnStartupFailure()
        {
            Shutdown(ShutdownReason.FailedToStart, Server.HasArg(ArgKey.ExitOnCrash));
        }

        #endregion


        #region Shutdown

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (startupThread != null && !shutdownComplete)
            {
                Shutdown(ShutdownReason.ProcessClosing, true);
                e.Cancel = true;
            }
            else
            {
                base.OnFormClosing(e);
            }
        }


        void Shutdown(ShutdownReason reason, bool quit)
        {
            if (shutdownPending) return;
            shutdownPending = true;
            console.Enabled = false;
            console.Text = "Shutting down...";
            Text = "LegendCraft " + " - shutting down...";
            uriDisplay.Enabled = false;
            if (!startupComplete)
            {
                startupThread.Join();
            }
            Server.Shutdown(new ShutdownParams(reason, TimeSpan.Zero, quit, false), false);
        }


        void OnServerShutdownEnded(object sender, ShutdownEventArgs e)
        {
            try
            {
                BeginInvoke((Action)delegate
                {
                    shutdownComplete = true;
                    switch (e.ShutdownParams.Reason)
                    {
                        case ShutdownReason.FailedToInitialize:
                        case ShutdownReason.FailedToStart:
                        case ShutdownReason.Crashed:
                            if (Server.HasArg(ArgKey.ExitOnCrash))
                            {
                                Application.Exit();
                            }
                            break;
                        default:
                            Application.Exit();
                            break;
                    }
                });
            }
            catch (ObjectDisposedException)
            {
            }
            catch (InvalidOperationException) { }
        }

        #endregion


        public void OnLogged(object sender, LogEventArgs e)
        {
            if (!e.WriteToConsole) return;
            try
            {
                if (shutdownComplete) return;
                if (logBox.InvokeRequired)
                {
                    BeginInvoke((EventHandler<LogEventArgs>)OnLogged, sender, e);
                }
                else
                {
                    // store user's selection
                    int userSelectionStart = logBox.SelectionStart;
                    int userSelectionLength = logBox.SelectionLength;
                    bool userSelecting = (logBox.SelectionStart != logBox.Text.Length && logBox.Focused ||
                                          logBox.SelectionLength > 0);

                    // insert and color a new message
                    int oldLength = logBox.Text.Length;
                    string msgToAppend = e.Message + Environment.NewLine;
                    logBox.AppendText(msgToAppend);
                    logBox.Select(oldLength, msgToAppend.Length);
                    switch (e.MessageType)
                    {
                        case LogType.PrivateChat:
                            logBox.SelectionColor = System.Drawing.Color.Teal;
                            break;
                        case LogType.IRC:
                            if (ThemeBox.SelectedItem == null)
                            {
                                logBox.SelectionColor = System.Drawing.Color.Navy;
                            }
                            else
                            {
                                switch (ThemeBox.SelectedItem.ToString())
                                {
                                    default:
                                        logBox.SelectionColor = System.Drawing.Color.LightSkyBlue;
                                        break;
                                    case "Default LC":
                                        logBox.SelectionColor = System.Drawing.Color.Navy;
                                        break;
                                }
                            }
                            break;
                        case LogType.ChangedWorld:
                            logBox.SelectionColor = System.Drawing.Color.Orange;
                            break;
                        case LogType.Warning:
                            if (ThemeBox.SelectedItem == null)
                            {
                                logBox.SelectionColor = System.Drawing.Color.Yellow;
                            }
                            else
                            {
                                switch (ThemeBox.SelectedItem.ToString())
                                {
                                    default:
                                        logBox.SelectionColor = System.Drawing.Color.MediumOrchid;
                                        break;
                                    case "Default LC":
                                        logBox.SelectionColor = System.Drawing.Color.Yellow;
                                        break;
                                }
                            }
                            break;
                        case LogType.Debug:
                            logBox.SelectionColor = System.Drawing.Color.DarkGray;
                            break;
                        case LogType.Error:
                        case LogType.SeriousError:
                            logBox.SelectionColor = System.Drawing.Color.Red;
                            break;
                        case LogType.ConsoleInput:
                        case LogType.ConsoleOutput:
                            if (ThemeBox.SelectedItem == null)
                            {
                                logBox.SelectionColor = System.Drawing.Color.White;
                            }
                            else
                            {
                                switch (ThemeBox.SelectedItem.ToString())
                                {
                                    default:
                                        logBox.SelectionColor = System.Drawing.Color.Black;
                                        break;
                                    case "Default LC":
                                        logBox.SelectionColor = System.Drawing.Color.White;
                                        break;
                                }
                            }
                            break;
                        default:
                            if (ThemeBox.SelectedItem == null)
                            {
                                logBox.SelectionColor = System.Drawing.Color.LightGray;
                            }
                            else
                            {
                                switch (ThemeBox.SelectedItem.ToString())
                                {
                                    default:
                                        logBox.SelectionColor = System.Drawing.Color.Black;
                                        break;
                                    case "Default LC":
                                        logBox.SelectionColor = System.Drawing.Color.LightGray;
                                        break;
                                }
                            }
                            break;
                    }

                    // cut off the log, if too long
                    if (logBox.Lines.Length > MaxLinesInLog)
                    {
                        logBox.SelectionStart = 0;
                        logBox.SelectionLength = logBox.GetFirstCharIndexFromLine(LinesToTrimWhenExceeded);
                        userSelectionStart -= logBox.SelectionLength;
                        if (userSelectionStart < 0) userSelecting = false;
                        string textToAdd = "----- cut off, see " + Logger.CurrentLogFileName + " for complete log -----" + Environment.NewLine;
                        logBox.SelectedText = textToAdd;
                        userSelectionStart += textToAdd.Length;
                        logBox.SelectionColor = System.Drawing.Color.DarkGray;
                    }

                    // either restore user's selection, or scroll to end
                    if (userSelecting)
                    {
                        logBox.Select(userSelectionStart, userSelectionLength);
                    }
                    else
                    {
                        logBox.SelectionStart = logBox.Text.Length;
                        logBox.ScrollToCaret();
                    }
                }
            }
            catch (ObjectDisposedException)
            {
            }
            catch (InvalidOperationException) { }
        }


        public void OnHeartbeatUriChanged(object sender, UriChangedEventArgs e)
        {
            try
            {
                if (shutdownPending) return;
                if (uriDisplay.InvokeRequired)
                {
                    BeginInvoke((EventHandler<UriChangedEventArgs>)OnHeartbeatUriChanged,
                            sender, e);
                }
                else
                {
                    uriDisplay.Text = e.NewUri.ToString();
                    uriDisplay.Enabled = true;
                    bPlay.Enabled = true;
                }
            }
            catch (ObjectDisposedException)
            {
            }
            catch (InvalidOperationException) { }
        }


        public void OnPlayerListChanged(object sender, EventArgs e)
        {
            try
            {
                if (shutdownPending) return;
                if (playerList.InvokeRequired)
                {
                    BeginInvoke((EventHandler)OnPlayerListChanged, null, EventArgs.Empty);
                }
                else
                {
                    playerList.Items.Clear();
                    Player[] playerListCache = Server.Players.OrderBy(p => p.Info.Rank.Index).ToArray();
                    foreach (Player player in playerListCache)
                    {
                        playerList.Items.Add(player.Info.Rank.Name + " - " + player.Name);
                    }
                }
            }
            catch (ObjectDisposedException)
            {
            }
            catch (InvalidOperationException) { }
        }

        private void console_Enter()
        {
            string[] separator = { Environment.NewLine };
            string[] lines = console.Text.Trim().Split(separator, StringSplitOptions.RemoveEmptyEntries);
            foreach (string line in lines)
            {
#if !DEBUG
                try
                {
#endif
                    if (line.Equals("/Clear", StringComparison.OrdinalIgnoreCase))
                    {
                        logBox.Clear();
                    }
                    else if (line.Equals("/credits", StringComparison.OrdinalIgnoreCase))
                    {
                        new AboutWindow().Show();
                    }
                    else
                    {
                        Player.Console.ParseMessage(line, true);
                    }
#if !DEBUG
                }
                catch (Exception ex)
                {
                    Logger.LogToConsole("Error occured while trying to execute last console command: ");
                    Logger.LogToConsole(ex.GetType().Name + ": " + ex.Message);
                    Logger.LogAndReportCrash("Exception executing command from console", "ServerGUI", ex, false);
                }
#endif
            }
            console.Text = "";
        }



        private void bPlay_Click(object sender, EventArgs e)
        {
            try
            {
                Process.Start(uriDisplay.Text);
            }
            catch (Exception)
            {
                MessageBox.Show("Could not open server URL. Please copy/paste it manually.");
            }
        }

        private void logBox_TextChanged(object sender, EventArgs e)
        {

        }
        private void Link_Clicked(object sender, LinkClickedEventArgs e)
        {
            System.Diagnostics.Process.Start(e.LinkText);
        }

        private void comboBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (SizeBox.SelectedItem.ToString() == "Normal")
            {
                logBox.ZoomFactor = 1;
            }
            if (SizeBox.SelectedItem.ToString() == "Big")
            {
                logBox.ZoomFactor = (float)1.2;
            }
            if (SizeBox.SelectedItem.ToString() == "Large")
            {
                logBox.ZoomFactor = (float)1.5;
            }
        }

        private void CopyMenuOnClickHandler(object sender, EventArgs e)
        {
            if (logBox.SelectedText.Length > 0)
                Clipboard.SetText(logBox.SelectedText.ToString(), TextDataFormat.Text);
        }

        private void CopyMenuPopupHandler(object sender, EventArgs e)
        {
            ContextMenu menu = sender as ContextMenu;
            if (menu != null)
            {
                menu.MenuItems[0].Enabled = (logBox.SelectedText.Length > 0);
            }
        }


        private bool _isBlackText = false;
        private void ThemeBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (ThemeBox.SelectedItem.ToString().Equals("Default LC")) { SetDefTheme(); }
            if (ThemeBox.SelectedItem.ToString().Equals("Alternate LC")) { SetAltTheme(); }
            if (ThemeBox.SelectedItem.ToString().Equals("Pink")) { SetPinkTheme(); }
            if (ThemeBox.SelectedItem.ToString().Equals("Yellow")) { SetYellowTheme(); }
            if (ThemeBox.SelectedItem.ToString().Equals("Green")) { SetGreenTheme(); }
            if (ThemeBox.SelectedItem.ToString().Equals("Purple")) { SetPurpleTheme(); }
            if (ThemeBox.SelectedItem.ToString().Equals("Grey")) { SetGreyTheme(); }
        }


        public void SetDefTheme()
        {
            BackColor = System.Drawing.Color.Firebrick;
            playerList.BackColor = System.Drawing.Color.White;
            logBox.BackColor = System.Drawing.Color.Black;
            if (!_isBlackText)
            {
                logBox.SelectAll();
                logBox.SelectionColor = System.Drawing.Color.LightGray;
                logBox.SelectionStart = logBox.Text.Length;
                logBox.ScrollToCaret();
                _isBlackText = true;

            }
        }
        public void SetAltTheme()
        {
            BackColor = System.Drawing.Color.Black;
            playerList.BackColor = System.Drawing.Color.White;
            logBox.BackColor = System.Drawing.Color.Firebrick;
            if (!_isBlackText)
            {
                logBox.SelectAll();
                logBox.SelectionColor = System.Drawing.Color.Black;
                logBox.SelectionStart = logBox.Text.Length;
                logBox.ScrollToCaret();
                _isBlackText = true;
            }
        }
        public void SetPinkTheme()
        {
            BackColor = System.Drawing.Color.Pink;
            playerList.BackColor = System.Drawing.Color.LightPink;
            logBox.BackColor = System.Drawing.Color.LightPink;
            if (!_isBlackText)
            {
                logBox.SelectAll();
                logBox.SelectionColor = System.Drawing.Color.Black;
                logBox.SelectionStart = logBox.Text.Length;
                logBox.ScrollToCaret();
                _isBlackText = true;
            }
        }

        public void SetYellowTheme()
        {
            BackColor = System.Drawing.Color.LightGoldenrodYellow;
            playerList.BackColor = System.Drawing.Color.LightYellow;
            logBox.BackColor = System.Drawing.Color.LightYellow;
            if (!_isBlackText)
            {
                logBox.SelectAll();
                logBox.SelectionColor = System.Drawing.Color.Black;
                logBox.SelectionStart = logBox.Text.Length;
                logBox.ScrollToCaret();
                _isBlackText = true;
            }
        }

        public void SetGreenTheme()
        {
            BackColor = System.Drawing.Color.SpringGreen;
            playerList.BackColor = System.Drawing.Color.LightGreen;
            logBox.BackColor = System.Drawing.Color.LightGreen;
            if (!_isBlackText)
            {
                logBox.SelectAll();
                logBox.SelectionColor = System.Drawing.Color.Black;
                logBox.SelectionStart = logBox.Text.Length;
                logBox.ScrollToCaret();
                _isBlackText = true;
            }
        }

        public void SetPurpleTheme()
        {
            BackColor = System.Drawing.Color.MediumPurple;
            playerList.BackColor = System.Drawing.Color.Plum;
            logBox.BackColor = System.Drawing.Color.Plum;
            if (!_isBlackText)
            {
                logBox.SelectAll();
                logBox.SelectionColor = System.Drawing.Color.Black;
                logBox.SelectionStart = logBox.Text.Length;
                logBox.ScrollToCaret();
                _isBlackText = true;
            }
        }

        public void SetGreyTheme()
        {
            BackColor = System.Drawing.SystemColors.Control;
            playerList.BackColor = System.Drawing.SystemColors.ControlLight;
            logBox.BackColor = System.Drawing.SystemColors.ControlLight;
            if (!_isBlackText)
            {
                logBox.SelectAll();
                logBox.SelectionColor = System.Drawing.Color.Black;
                logBox.SelectionStart = logBox.Text.Length;
                logBox.ScrollToCaret();
                _isBlackText = true;
            }
        }

        #region PlayerViewer
      
        private void playerList_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            try
            {
                string s = (string)playerList.Items[playerList.SelectedIndex];
                s = s.Substring(s.IndexOf('-'),
                    s.Length - s.IndexOf('-'))
                    .Replace("-", "")
                    .Replace(" ", "")
                    .Trim();
                PlayerInfo player = PlayerDB.FindPlayerInfoExact(s);
                if (player == null) return;
                var v = new PlayerViewer(player);
                v.Show();
            }
            catch { } //do nothing at all
        }
        
        
        #endregion

        #region PreventClose
        //should prevent users from accidently closing the ServerGUI X out window

        private void Mainform_FormClosing(object sender, FormClosingEventArgs e)
        {
            switch (MessageBox.Show("Would you like to save the changes before exiting?", "Warning", MessageBoxButtons.YesNoCancel))
            {
                case DialogResult.Yes:
                    return;

                case DialogResult.Cancel:
                    e.Cancel = true;
                    return;
            }
        }


        #endregion


    }
}