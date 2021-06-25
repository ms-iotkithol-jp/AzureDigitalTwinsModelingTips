using Azure.DigitalTwins.Core;
using Microsoft.Azure.Devices;
using Microsoft.Azure.Devices.Client;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
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
        public WindowDeliveryTruckDriverMobileDeviceSimulator(IIoTLogger logger)
        {
            InitializeComponent();
            this.Loaded += WindowDeliveryTruckDriverMobileDeviceSimulator_Loaded;
            this.logger = logger;
        }

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
        }

        DispatcherTimer sendTimer = null;
        DeviceClient deviceClient = null;

        private void buttonSendStart_Click(object sender, RoutedEventArgs e)
        {
            foreach(var tmd in TemparetureMewasurementDevices)
            {
                tmd.Temperature = double.Parse(tbInTemp.Text);
                tmd.ExternalTemperatre = double.Parse(tbExtTemp.Text);
                tmd.RiseRate = double.Parse(tbRiseRate.Text);
                tmd.BatteryLevel = double.Parse(tbBatteryLevel.Text);
                tmd.BatteryLevelDelta = double.Parse(tbBatteryLevelRate.Text);
                tmd.StartAutomaticTimestampUpdate(this.Dispatcher, int.Parse(tbUpdateInterval.Text));
            }
            var interval = int.Parse(tbSendInterval.Text);
            sendTimer = new DispatcherTimer();
            sendTimer.Interval = TimeSpan.FromSeconds(interval);
            sendTimer.Tick += async (s, e) =>
            {
                try
                {
                    var sendContent = new
                    {
                        temperatureMeasurementDevices = new List<object>(),
                        location = new
                        {
                            longitude = double.Parse(tbLongitude.Text),
                            latitude = double.Parse(tbLatitude.Text),
                            attitude = double.Parse(tbAttitude.Text)
                        },
                        status = cbStatus.SelectedIndex,
                        timestamp = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
                    };
                    foreach(var tmd in TemparetureMewasurementDevices)
                    {
                        sendContent.temperatureMeasurementDevices.Add(
                            new
                            {
                                tmdId = tmd.Id,
                                temperature = tmd.Temperature,
                                batteryLevel = tmd.BatteryLevel,
                                timestamp = tmd.Timestamp
                            });
                    }
                    var json = Newtonsoft.Json.JsonConvert.SerializeObject(sendContent);
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
            }
            catch(Exception ex)
            {
                logger.ShowLog(ex.Message);
            }
        }
    }
}
