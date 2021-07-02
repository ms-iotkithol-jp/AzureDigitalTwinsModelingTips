using Azure;
using Azure.DigitalTwins.Core;
using Microsoft.Azure.Devices.Client;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace WpfAppTruckSimulator
{
    /// <summary>
    /// WindowCCTruckSimulator.xaml の相互作用ロジック
    /// </summary>
    public partial class WindowCCTruckSimulator : Window
    {
        public BasicDigitalTwin Target { get; set; }
        public string IotHubDeviceConnectionString { get; set; }
        private DigitalTwinsClient twinsClient;
        ITelemetryDataProvider telemetryDataProvider = null;
        public WindowCCTruckSimulator(DigitalTwinsClient client, IIoTLogger logger)
        {
            InitializeComponent();
            this.Loaded += WindowCCTruckSimulator_Loaded;
            this.logger = logger;
            twinsClient = client;
        }

        IIoTLogger logger;
        int lastStatus = 0;

        private void WindowCCTruckSimulator_Loaded(object sender, RoutedEventArgs e)
        {
            tbIoTHubConnectionString.Text = IotHubDeviceConnectionString;
            tbCCTruckId.Text = Target.Id;
            if (Target.Contents.ContainsKey("Status"))
            {
  //              var jsonElem = ((JsonElement)Target.Contents["Status"]).;
                lastStatus = ((JsonElement)Target.Contents["Status"]).GetInt32();
                cbStatus.SelectedIndex = lastStatus;
            }
            telemetryDataProvider = new CCTruckPaneDataProvider(tbAttitude, tbLatitude, tbLongitude, tbTemp);
        }

        DeviceClient deviceClient = null;
        private async void buttonConnectToIoTHub_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                deviceClient = DeviceClient.CreateFromConnectionString(tbIoTHubConnectionString.Text);
                await deviceClient.OpenAsync();
                logger.ShowLog($"CCTruck[{Target.Id}] connected : ");
                buttonSendStart.IsEnabled = true;
                buttonConnectToIoTHub.IsEnabled = false;
            }
            catch (Exception ex)
            {
                logger.ShowLog(ex.Message);
            }
        }

        DispatcherTimer timer = null;
        private void buttonSendStart_Click(object sender, RoutedEventArgs e)
        {
            if (timer == null)
            {
                try
                {
                    timer = new DispatcherTimer();
                    timer.Interval = TimeSpan.FromSeconds(int.Parse(tbSendInterval.Text));
                    timer.Tick += async (s, e) =>
                    {
                        var json = telemetryDataProvider.GetNextMessage(null, cbStatus.SelectedIndex);
                        if (!string.IsNullOrEmpty(json))
                        {
                            var msg = new Message(System.Text.Encoding.UTF8.GetBytes(json));
                            msg.Properties.Add("application", "adt-transport-sample");
                            msg.Properties.Add("message-type", "cctruck");
                            await deviceClient.SendEventAsync(msg);
                            logger.ShowLog($"Send - {json}");
                        }
                        else
                        {
                            this.Dispatcher.Invoke(() =>
                            {
                                timer.Stop();
                                buttonSendStart.IsEnabled = true;
                                buttonSendStop.IsEnabled = false;
                            });
                        }
                    };
                    timer.Start();
                    logger.ShowLog("Sending...");
                }
                catch (Exception ex)
                {
                    logger.ShowLog(ex.Message);
                }
            }
            else
            {
                timer.Start();
            }
            buttonSendStop.IsEnabled = true;
            buttonSendStart.IsEnabled = false;
        }

        private void buttonSendStop_Click(object sender, RoutedEventArgs e)
        {
            if (timer != null)
            {
                timer.Stop();
                logger.ShowLog("Send Stopped.");
            }
            buttonSendStart.IsEnabled = true;
            buttonSendStop.IsEnabled = false;
        }

        private async void cbStatus_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cbStatus.SelectedItem != null && Target !=null)
            {
                bool updated = false;
                var patch = new JsonPatchDocument();
                if (Target.Contents["Status"] != null)
                {
                    if (cbStatus.SelectedIndex != lastStatus)
                    {
                        patch.AppendReplace("/Status", cbStatus.SelectedIndex);
                        lastStatus = cbStatus.SelectedIndex;
                        updated = true;
                    }
                }
                else
                {
                    patch.AppendAdd("/Status", cbStatus.SelectedItem);
                    Target.Contents.Add("Status", cbStatus.SelectedIndex);
                    updated = true;
                }
                if (updated)
                {
                    await twinsClient.UpdateDigitalTwinAsync(Target.Id, patch);
                    logger.ShowLog($"Update CCTruck[{Target.Id}] Status<={cbStatus.SelectedIndex}");
                }
            }
        }

        private void buttonOpenCSVFile_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog();
            dialog.Filter = "Prepared CSV file | *.csv";
            if (dialog.ShowDialog().Value)
            {
                tbCSVFileName.Text = dialog.FileName;
                var provider = new CCTruckLogDataProvider(tbCSVFileName.Text);
                try
                {
                    provider.Parse();
                    telemetryDataProvider = provider;
                }
                catch(Exception ex)
                {
                    MessageBox.Show(ex.Message);
                }
            }
        }
    }
}
