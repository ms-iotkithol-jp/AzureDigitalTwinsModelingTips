using Azure;
using Azure.DigitalTwins.Core;
using Microsoft.Azure.Devices;
using Microsoft.Azure.Devices.Client;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
using Message = Microsoft.Azure.Devices.Client.Message;

namespace WpfAppTruckSimulator
{
    /// <summary>
    /// WindowDeliveryTruckDriverMobileDeviceSimulator.xaml の相互作用ロジック
    /// </summary>
    public partial class WindowDeliveryTruckDriverMobileDeviceSimulator : Window
    {
        public string IotHubDeviceConnectionString { get; set; }
        public BasicDigitalTwin Target { get; set; }
        public ObservableCollection<TemperatureMeasurementDevice> TemparetureMewasurementDevices { get { return tmds; } }

        ObservableCollection<TemperatureMeasurementDevice> tmds = new ObservableCollection<TemperatureMeasurementDevice>();
        IIoTLogger logger = null;
        DigitalTwinsClient twinsClient = null;
        ITelemetryDataProvider telemetryDataProvider = null;
        public WindowDeliveryTruckDriverMobileDeviceSimulator(DigitalTwinsClient client, IIoTLogger logger)
        {
            InitializeComponent();
            this.Loaded += WindowDeliveryTruckDriverMobileDeviceSimulator_Loaded;
            this.logger = logger;
            twinsClient = client;
        }

        int lastStatus = 0;
        private void WindowDeliveryTruckDriverMobileDeviceSimulator_Loaded(object sender, RoutedEventArgs e)
        {
            lbTemperatureMeasuredDevices.ItemsSource = tmds;
            tbIoTHubConnectionString.Text = IotHubDeviceConnectionString;
            tbDTruckId.Text = Target.Id;
            foreach(var tmd in tmds)
            {
                tmd.BatteryLevel = double.Parse(tbBatteryLevel.Text);
                tmd.BatteryLevelDelta = double.Parse(tbBatteryLevelRate.Text);
                tmd.ExternalTemperatre = double.Parse(tbExtTemp.Text);
                tmd.RiseRate = double.Parse(tbRiseRate.Text);
                tmd.Temperature = double.Parse(tbInTemp.Text);
            }
            if (Target.Contents.ContainsKey("Status"))
            {
                lastStatus = ((JsonElement)Target.Contents["Status"]).GetInt32();
                cbStatus.SelectedIndex = lastStatus;
            }
            telemetryDataProvider = new DTruckPaneDataProvider(tbAttitude, tbLatitude, tbLongitude);
        }

        DispatcherTimer sendTimer = null;
        DeviceClient deviceClient = null;

        private void buttonSendStart_Click(object sender, RoutedEventArgs e)
        {
            foreach (var tmd in TemparetureMewasurementDevices)
            {
                tmd.Temperature = double.Parse(tbInTemp.Text);
                tmd.ExternalTemperatre = double.Parse(tbExtTemp.Text);
                tmd.RiseRate = double.Parse(tbRiseRate.Text);
                tmd.BatteryLevel = double.Parse(tbBatteryLevel.Text);
                tmd.BatteryLevelDelta = double.Parse(tbBatteryLevelRate.Text);
                tmd.StartAutomaticTimestampUpdate(this.Dispatcher, int.Parse(tbUpdateInterval.Text));
            }
            if (sendTimer == null)
            {
                sendTimer = new DispatcherTimer();
                sendTimer.Tick += async (s, e) =>
                {
                    try
                    {
                        var tmds = new List<object>();
                        foreach (var tmd in TemparetureMewasurementDevices)
                        {
                            tmds.Add(
                                new
                                {
                                    tmdId = tmd.Id,
                                    temperature = tmd.Temperature,
                                    batteryLevel = tmd.BatteryLevel,
                                    timestamp = tmd.Timestamp
                                });
                        }
                        var json = telemetryDataProvider.GetNextMessage(tmds, cbStatus.SelectedIndex);
                        var msg = new Message(System.Text.Encoding.UTF8.GetBytes(json));
                        msg.Properties.Add("application", "adt-transport-sample");
                        msg.Properties.Add("message-type", "driver-mobile");
                        await deviceClient.SendEventAsync(msg);
                        logger.ShowLog($"Send - {json}");
                    }
                    catch (Exception ex)
                    {
                        logger.ShowLog(ex.Message);
                    }
                };
            }
            var interval = int.Parse(tbSendInterval.Text);
            sendTimer.Interval = TimeSpan.FromSeconds(interval);
            sendTimer.Start();
            buttonSendStop.IsEnabled = true;
            buttonSendStart.IsEnabled = false;
        }

        private void buttonSendStop_Click(object sender, RoutedEventArgs e)
        {
            if (sendTimer != null && sendTimer.IsEnabled)
            {
                sendTimer.Stop();
                buttonSendStart.IsEnabled = true;
                buttonSendStop.IsEnabled = false;
                foreach (var tmd in TemparetureMewasurementDevices)
                {
                    tmd.StopAutomaticTimestampUpdate();
                }
            }
        }

        private async void buttonConnectToIoTHub_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                deviceClient = DeviceClient.CreateFromConnectionString(tbIoTHubConnectionString.Text);
                await deviceClient.OpenAsync();
                logger.ShowLog("Delivery Truck Driver's Mobile Equipment has been connected.");
                buttonSendStart.IsEnabled = true;
                buttonConnectToIoTHub.IsEnabled = false;
            }
            catch(Exception ex)
            {
                logger.ShowLog(ex.Message);
            }
        }

        private async void cbStatus_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cbStatus.SelectedItem != null && Target != null)
            {
                var patch = new JsonPatchDocument();
                bool updated = false;
                if (Target.Contents.ContainsKey("Status"))
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
                    patch.AppendAdd("/Status", cbStatus.SelectedIndex);
                    Target.Contents.Add("Status", cbStatus.SelectedIndex);
                    updated = true;
                }
                if (updated)
                {
                    await twinsClient.UpdateDigitalTwinAsync(Target.Id, patch);
                    logger.ShowLog($"Update DTruck[{Target.Id}] Status<={cbStatus.SelectedIndex}");
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
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message);
                }
            }
        }
    }
}
