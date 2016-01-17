using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Microsoft.Lync.Model.Extensibility;
using Microsoft.Lync.Model;
using Microsoft.Lync.Controls;
using Microsoft.Lync.Model.Conversation.AudioVideo;
using System.Threading;
using ThingM.Blink1.ColorProcessor;
using ThingM.Blink1;
using Lync_Presence_Indicator.Properties;
using System.Windows.Threading;
using System.Windows.Forms;
using System.Diagnostics;
using Microsoft.Lync.Model.Conversation;

namespace Lync_Presence_Indicator
{
    public enum CustomContactAvailability
    {
        Invalid = -1,
        //
        // Summary:
        //     Do not use this enumerator. This flag indicates that the cotact state is
        //     unspecified.
        None = 0,
        //
        // Summary:
        //     A flag indicating that the contact is available.
        Free = 3500,
        //
        // Summary:
        //     Contact is free but inactive. Cannot be published as user state. Idle states
        //     are set automatically by Client.
        FreeIdle = 5000,
        //
        // Summary:
        //     A flag indicating that the contact is busy and inactive.
        Busy = 6500,
        //
        // Summary:
        //     Contact is busy but inactive. Cannot be published as user state. Idle states
        //     are set automatically by Client.
        BusyIdle = 7500,
        //
        // Summary:
        //     A flag indicating that the contact does not want to be disturbed.
        DoNotDisturb = 9500,
        //
        // Summary:
        //     A flag indicating that the contact is temporarily away.
        TemporarilyAway = 12500,
        //
        // Summary:
        //     A flag indicating that the contact is away.
        Away = 15500,
        //
        // Summary:
        //     A flag indicating that the contact is signed out.
        Offline = 18500,

        InACall = 1,
        PhoneRinging = 2,
        Listening = 3
    }

    public partial class Window1 : Window
    {
        public LyncClient lync;
        public Blink1 blink1 = new Blink1();
        public Blink1 blink;
        System.Timers.Timer timer1 = new System.Timers.Timer();
        System.Timers.Timer reconnectTimer = new System.Timers.Timer();
        private DispatcherTimer LyncConnectRetryTimer = new DispatcherTimer() { Interval = TimeSpan.FromSeconds(15) };
        private DispatcherTimer DelayedMuteTimer = new DispatcherTimer() { Interval = TimeSpan.FromMilliseconds(500) };
        public bool isLightDisconnected = true;
        public bool isPhoneRinging = false;
        public bool _connectedToLync = false;
        NotifyIcon ni = new System.Windows.Forms.NotifyIcon();

        public Window1()
        {
            InitializeComponent();
            //SignIn(null, null, null);
            //blink1.Open();
            //myStatus();
            //lync.Self.Contact.ContactInformationChanged +=
            //                new EventHandler<ContactInformationChangedEventArgs>(myStatusChange);
            //blink1.DeactivateInactivityMode();
            //blink1.Close();

            ni.Icon = Properties.Resources.LyncPresence;
            ni.Text = "Lync Presence Indicator";
            ni.Visible = true;
            ni.DoubleClick +=
                delegate(object sender, EventArgs args)
                {
                    DelayedMuteTimer.Stop();
                    this.Show();
                    this.WindowState = WindowState.Normal;
                };

            //DelayedMuteTimer.Tick += (s, e) =>
            //{
            //    DelayedMuteTimer.Stop();
            //    ToggleMute();
            //};
            //ni.Click += (s, e) =>
            //{
            //    var args = e as System.Windows.Forms.MouseEventArgs;
            //    if (args != null && args.Button == MouseButtons.Left)
            //    {
            //        DelayedMuteTimer.Start();
            //    }
            //};

            //var menuItems = new System.Windows.Forms.MenuItem[2];
            //var menuItem = new System.Windows.Forms.MenuItem("Toggle Mute", (s, e) =>
            //{
            //    ToggleMute();
            //});
            //menuItems[0] = menuItem;

            //menuItem = new System.Windows.Forms.MenuItem("Toggle Light", (s, e) =>
            //{
            //    LyncLightToggle.IsChecked = !LyncLightToggle.IsChecked;
            //    LyncLightToggle_Clicked(s, null);
            //});
            //menuItems[1] = menuItem;

            //ni.ContextMenu = new System.Windows.Forms.ContextMenu(menuItems);


            this.Loaded += MainWindow_Loaded;
            LyncConnectRetryTimer.Tick += LyncConnectRetryTimer_Tick;

            var lyncLightSetting = Settings.Default.LyncLightEnabled;
            LyncLightToggle.IsChecked = lyncLightSetting == "True";
            LyncLightToggle.Click += LyncLightToggle_Clicked;

        }

        private System.Drawing.Icon NotifyIcon(string p)
        {
            throw new NotImplementedException();
        }

        private String lyncStatus(string ocsStatus)
        {
            switch (ocsStatus){
                case "Available":
                    blink1.SetColor(new Cmyk(100, 0, 100, 0));
                    return "Available";
                case "Busy":
                    blink1.SetColor(new Rgb(255, 0, 0));
                    return "Busy";
                case "Away":
                    return "Away";
                case "In a call":
                    return "In a call";
                case "Be right back":
                    return "Be right back";
                case "In a conference":
                    return "In a conference";
                case "In a meeting":
                    return "In a meeting";
                case "Do not disturb":
                    return "Do not disturb";
                case "Off work":
                    return "Off work";
                default:
                  return "Error - ocsStatus";
            }

        }

        ////Get initial status of the curent logged in user
        //private void myStatus()
        //{
        //    lync = LyncClient.GetClient();

        //    string ocsStatus = null;

        //    ocsStatus = lync.Self.Contact.GetContactInformation(ContactInformationType.Activity).ToString();

        //    lyncStatus(ocsStatus);
        //    //presence.Text = lyncStatus(ocsStatus);
        //}

        ////Get updates of status of the curent logged in user if changed
        //public void myStatusChange(object sender, ContactInformationChangedEventArgs e)
        //{
        //    if (e.ChangedContactInformation.Contains(ContactInformationType.Activity))
        //    {
        //        string ocsStatus = null;
        //        ocsStatus = lync.Self.Contact.GetContactInformation(ContactInformationType.Activity).ToString();

        //        lyncStatus(ocsStatus);
        //        //presence.Text = lyncStatus(ocsStatus);
        //    }

        //}

        bool errorShown = false;
        private void ConnectToLync()
        {
            try
            {
                lync = LyncClient.GetClient();
                lync.ConversationManager.ConversationAdded += ConversationManager_ConversationAdded;
                lync.ConversationManager.ConversationRemoved += ConversationManager_ConversationRemoved;

                if (LyncLightToggle.IsChecked.Value)
                {
                    ConnectToLight();
                }

                timer1.Elapsed += new System.Timers.ElapsedEventHandler(aTimer_Elapsed);
                timer1.Interval = 500;
                timer1.Enabled = true;
                timer1.Start();

                _connectedToLync = true;
                ErrorMessage.Text = "";
                LyncLightToggle.IsEnabled = true;
                ni.ShowBalloonTip(1000, "Lync Presence Indicator", "Connected To Lync", ToolTipIcon.Info);
                errorShown = false;
            }
            catch (Exception ex)
            {
                ErrorMessage.Text = ex.Message;
                _connectedToLync = false;
                timer1.Stop();
                if (!errorShown)
                {
                    ni.ShowBalloonTip(500, "Lync Presence Indicator", "Error - " + ex.Message, ToolTipIcon.Error);
                    errorShown = true;
                }
                DisconnectFromLight();
                LyncConnectRetryTimer.Start();
            }

        }


        void LyncLightToggle_Clicked(object sender, RoutedEventArgs e)
        {
            if (LyncLightToggle.IsChecked.Value)
            {
                ConnectToLight();

            }
            else
            {
                DisconnectFromLight();

            }
            //save setting
            Settings.Default.LyncLightEnabled = LyncLightToggle.IsChecked.ToString();
            Settings.Default.Save();
            ErrorMessage.Text = "";
        }

        private void DisconnectFromLight()
        {
            reconnectTimer.Stop();
            TurnOffAll();
            if (blink == null) blink.Close();
            status1 = "";
            status2 = "";
            isLightDisconnected = true;
        }

        private void ConnectToLight()
        {
            if (blink != null) { DisconnectFromLight(); } else { OpenCommunication(); }

            reconnectTimer.Elapsed += reconnectTimer_Elapsed;
            reconnectTimer.Interval = 5000;
            reconnectTimer.Enabled = true;
        }

        void LyncConnectRetryTimer_Tick(object sender, EventArgs e)
        {
            LyncConnectRetryTimer.Stop();
            if (!_connectedToLync)
            {
                ConnectToLync();
            }
        }


        void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            ConnectToLync();
            Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.BelowNormal;
            WindowState = System.Windows.WindowState.Minimized;
            //This hides the program to the task bar
            this.Hide();
        }

        protected override void OnStateChanged(EventArgs e)
        {
            if (WindowState == WindowState.Minimized)
                //This hides the program to the task bar
                this.Hide();

            base.OnStateChanged(e);
        }

        void ConversationManager_ConversationRemoved(object sender, Microsoft.Lync.Model.Conversation.ConversationManagerEventArgs e)
        {
            isPhoneRinging = false;
            if (e.Conversation == _connectedConversation) _connectedConversation = null;
        }

        void ConversationManager_ConversationAdded(object sender, Microsoft.Lync.Model.Conversation.ConversationManagerEventArgs e)
        {
            var modes = e.Conversation.Modalities;
            foreach (var mode in modes)
            {

                if (mode.Value is AVModality)
                {
                    var state = ((AVModality)mode.Value).State;
                    if (state == Microsoft.Lync.Model.Conversation.ModalityState.Notified)
                    {
                        UpdateStatus(CustomContactAvailability.PhoneRinging);
                        isPhoneRinging = true;
                    }
                    ((AVModality)mode.Value).ModalityStateChanged += MainWindow_ModalityStateChanged;
                }
            }
        }

        private Conversation _connectedConversation = null;

        void MainWindow_ModalityStateChanged(object sender, Microsoft.Lync.Model.Conversation.ModalityStateChangedEventArgs e)
        {
            if (e.NewState != Microsoft.Lync.Model.Conversation.ModalityState.Notified)
            {
                isPhoneRinging = false;
                status1 = "";
                status2 = "";
            }

            if (e.NewState == Microsoft.Lync.Model.Conversation.ModalityState.Connected)
            {
                _connectedConversation = ((Modality)sender).Conversation;
            }
            else if (e.NewState == ModalityState.Disconnected)
            {
                _connectedConversation = null;
            }
        }

        bool resetRinger = false;
        void reconnectTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            if (isPhoneRinging)
            {
                if (resetRinger)
                {
                    isPhoneRinging = false;
                    status1 = "";
                    status2 = "";
                    resetRinger = false;
                }
                else
                    resetRinger = true;

                return;
            }

            bool wasLightDiconnected = isLightDisconnected;

            var result = OpenCommunication();
            isLightDisconnected = result != 0;
            if (wasLightDiconnected && !isLightDisconnected)
            {
                status2 = "";
            }
        }

        public void CloseAll()
        {
            try
            {
                timer1.Enabled = false;
                TurnOffAll();
                this.Close();
            }
            catch (Exception ex)
            {
            }
        }

        public delegate void UpdateStatusCallback(CustomContactAvailability value);


        void aTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            try
            {
                CustomContactAvailability status = (CustomContactAvailability)lync.Self.Contact.GetContactInformation(ContactInformationType.Availability);
                var activity = lync.Self.Contact.GetContactInformation(ContactInformationType.Activity);
                if (activity.ToString().Contains("call")) status = CustomContactAvailability.InACall;

                statusTxt.Dispatcher.Invoke(new UpdateStatusCallback(this.UpdateStatus), new object[] { status });

            }
            catch (Exception ex)
            {
                ConnectToLync();
            }
        }

        string status1;
        string status2;
        bool blinkOpen = false;
        bool noBlinks = true;

        public uint OpenCommunication()
        {
            //try
            //{
                if (blink == null) blink = new Blink1();
                blink.Open();
                blinkOpen = true;
            //}
            //catch (Exception ex)
            //{
            //    ErrorMessage.Text = "No Blink(1) device found.";
            //    //throw new InvalidOperationException("No Blink(1) device found.");
            //}

            //var numBlinks = blink.enumerate();
            //if (numBlinks > 0)
            //{
            //    if (noBlinks)
            //    {
            //        if (blinkOpen) blink.Close();
            //        blink.Open();
            //        blinkOpen = true;
            //    }
            //    noBlinks = false;
            //    return 0;
            //}
            //else noBlinks = true;

            return 0;
        }

        public void UpdateStatus(CustomContactAvailability status)
        {
            if (isPhoneRinging)
                return;

            status1 = status.ToString();

            if (status1 != status2)
            {
                TurnOffAll();

                switch (status)
                {
                    case CustomContactAvailability.Away:
                        statusTxt.Text = "Away";
                        //Leave light off
                        status2 = status.ToString();
                        break;
                    case CustomContactAvailability.Busy:
                        statusTxt.Text = "Busy";
                        //Leave light off
                        Red();
                        status2 = status.ToString();
                        break;
                    //case ContactAvailability.BusyIdle:
                    //    break;
                    case CustomContactAvailability.DoNotDisturb:
                        //Leave light off
                        //YellowAndRed();
                        status2 = status.ToString();
                        break;
                    case CustomContactAvailability.Free:
                        statusTxt.Text = "On-line";
                        //Leave light off
                        Green();
                        status2 = status.ToString();
                        break;
                    //case ContactAvailability.FreeIdle:
                    //    break;
                    //case ContactAvailability.Invalid:
                    //    break;
                    //case ContactAvailability.None:
                    //    break;
                    //case ContactAvailability.Offline:
                    //    break;
                    case CustomContactAvailability.TemporarilyAway:
                        statusTxt.Text = "Be Right Back";
                        //Yellow();
                        status2 = status.ToString();
                        break;
                    case CustomContactAvailability.InACall:
                        statusTxt.Text = "In A Call";
                        Red();
                        status2 = status.ToString();
                        //ni.Icon = new System.Drawing.Icon("micicon.ico");
                        break;
                    case CustomContactAvailability.PhoneRinging:
                        RedBlinking();
                        status2 = status.ToString();
                        break;
                    case CustomContactAvailability.Listening:
                        statusTxt.Text = "Listening";

                        Yellow();
                        status2 = status.ToString();
                        break;
                    default:
                        statusTxt.Text = "Away";
                        //Leave light off
                        status2 = status.ToString();
                        break;
                }
            }

        }


        private void GreenBlinking()
        {
            System.Diagnostics.Debug.WriteLine("Blinking Green");

            if (!isLightDisconnected)
            {
                SetBlinkLight(Color.FromRgb(0, 255, 0), Color.FromRgb(0, 0, 0), 10, true, 250);
            }
        }


        public void Green()
        {
            System.Diagnostics.Debug.WriteLine("Setting Green");
            if (!isLightDisconnected)
            {
                SetBlinkLight(Color.FromRgb(0, 255, 0));

            }

            //ni.Icon = new System.Drawing.Icon("greenicon.ico");
        }

        public void Yellow()
        {

            if (!isLightDisconnected)
            {
                SetBlinkLight(Color.FromRgb(255, 200, 0));

            }

            //ni.Icon = new System.Drawing.Icon("yellowicon.ico");

        }

        private void RedBlinking()
        {
            System.Diagnostics.Debug.WriteLine("Blinking Red");

            if (!isLightDisconnected)
            {
                SetBlinkLight(Color.FromRgb(255, 0, 0), Color.FromRgb(0, 0, 0), 10, true, 250);
            }
        }

        public void Red()
        {

            if (!isLightDisconnected)
            {
                SetBlinkLight(Color.FromRgb(255, 0, 0));


            }

            //ni.Icon = new System.Drawing.Icon("redicon.ico");
        }

        public void YellowAndRed()
        {

            if (!isLightDisconnected)
            {
                SetBlinkLight(Color.FromRgb(255, 125, 0));
            }

            //ni.Icon = new System.Drawing.Icon("orangeicon.ico");
        }

        public void TurnOffAll()
        {

            if (blink != null) SetBlinkLight(Color.FromRgb(0, 0, 0));

        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            CloseAll();
        }

        System.Timers.Timer blinkTimer;
        private void SetBlinkLight(Color c1, Color c2 = new Color(), ushort fadeMilliseconds = 0, bool repeat = false, int periodMilliseconds = 0)
        {
            try
            {
                if (blinkTimer != null)
                {
                    blinkTimer.Stop();
                    blinkTimer = null;
                }
                if (blink != null)
                {
                    if (fadeMilliseconds == 0)
                    {
                        blink.SetColor(new Rgb(c1.R, c1.G, c1.B));
                    }
                    else
                    {
                        blink.FadeToColor(fadeMilliseconds, c1.R, c1.G, c1.B, false);
                        if (repeat)
                        {
                            blinkTimer = new System.Timers.Timer();
                            blinkTimer.Interval = periodMilliseconds;
                            var lastColor = c1;
                            blinkTimer.Elapsed += (o, e) =>
                            {
                                if (lastColor == c1)
                                {
                                    blink.FadeToColor(fadeMilliseconds, c2.R, c2.G, c2.B,false);
                                    lastColor = c2;
                                }
                                else
                                {
                                    blink.FadeToColor(fadeMilliseconds, c1.R, c1.G, c1.B,false);
                                    lastColor = c1;
                                }
                            };
                            blinkTimer.Start();
                        }
                    }

                }
            }
            catch { }
        }
    }
}

