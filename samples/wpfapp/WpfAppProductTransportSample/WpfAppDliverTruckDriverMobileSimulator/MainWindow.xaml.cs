using Azure.DigitalTwins.Core;
using Azure.Identity;
using Microsoft.Azure.Devices;
using Microsoft.Azure.Devices.Client;
using Microsoft.Azure.Devices.Provisioning.Client;
using Microsoft.Azure.Devices.Provisioning.Client.Transport;
using Microsoft.Azure.Devices.Shared;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace WpfAppTruckSimulator
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, IIoTLogger
    {
        RegistryManager registryManager = null;

        public MainWindow()
        {
            InitializeComponent();
            this.Loaded += MainWindow_Loaded;
        }

        ObservableCollection<string> ccTrucks = new ObservableCollection<string>();
        ObservableCollection<string> dTrucks = new ObservableCollection<string>();

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                lbCCTrucks.ItemsSource = ccTrucks;
                lbDTrucks.ItemsSource = dTrucks;

                var config = new ConfigurationBuilder().AddJsonFile("appsettings.json", optional: false, reloadOnChange: false).Build();
                tbIoTHubConnectionString.Text = config["iothub-connetion-string"];
                tbADTInstanceUrl.Text = config["adt-instance-url"];
            }
            catch (Exception ex)
            {
                ShowLog(ex.Message);
            }
        }

        public void ShowLog(string log)
        {
            this.Dispatcher.Invoke(() =>
            {
                var builder = new StringBuilder();
                using (var writer = new StringWriter(builder))
                {
                    writer.WriteLine($"[{DateTime.Now.ToString("yyyyMMdd-HHmmss")}] {log}");
                    writer.Write(tbLog.Text);
                    tbLog.Text = builder.ToString();
                }
            });

        }

        private async void buttonConnectToIoTHub_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                registryManager = RegistryManager.CreateFromConnectionString(tbIoTHubConnectionString.Text);
                await registryManager.OpenAsync();
                ShowLog("Connected to IoT Hub as registry readable/writeable.");
                buttonConnectToIoTHub.IsEnabled = false;
                buttonGetCCTrucks.IsEnabled = true;
                buttonGetDeliverTrucks.IsEnabled = true;
            }
            catch(Exception ex)
            {
                ShowLog(ex.Message);
            }
        }


        DigitalTwinsClient twinsClient = null;

        private void buttonConnectToAzureDigitalTwins_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var credential = new DefaultAzureCredential();
                var instanceUrl = tbADTInstanceUrl.Text;
                if (!instanceUrl.StartsWith("http"))
                {
                    instanceUrl = "https://" + instanceUrl;
                }
                twinsClient = new DigitalTwinsClient(new Uri(instanceUrl), credential);
                ShowLog("Conneced to ADT.");
                buttonConnectToIoTHub.IsEnabled = true;
                buttonConnectToAzureDigitalTwins.IsEnabled = false;
            }
            catch (Exception ex)
            {
                ShowLog(ex.Message);
            }

        }

        Dictionary<string, BasicDigitalTwin> ccTruckTwins = new Dictionary<string, BasicDigitalTwin>();
        Dictionary<string, BasicDigitalTwin> dTruckTwins = new Dictionary<string, BasicDigitalTwin>();

        private async void buttonGetCCTrucks_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ccTrucks.Clear();
                ccTruckTwins.Clear();
                var query = $"SELECT * FROM DigitalTwins WHERE IS_OF_MODEL('{CoolingContainerTruckInfo.CoolingContainerTruckModelId}')";
                var queryResponse = twinsClient.QueryAsync<BasicDigitalTwin>(query);
                await foreach (var truck in queryResponse)
                {
                    ccTrucks.Add(truck.Id);
                    ccTruckTwins.Add(truck.Id, truck);
                }
            }
            catch(Exception ex)
            {
                ShowLog(ex.Message);
            }
        }

        private async void buttonGetDeliverTrucks_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                dTrucks.Clear();
                dTruckTwins.Clear();
                var query = $"SELECT * FROM DigitalTwins WHERE IS_OF_MODEL('{DeliveryTruckInfo.DeliveryTruckModelId}')";
                var queryResponse = twinsClient.QueryAsync<BasicDigitalTwin>(query);
                await foreach (var truck in queryResponse)
                {
                    dTrucks.Add(truck.Id);
                    dTruckTwins.Add(truck.Id, truck);
                }
            }
            catch (Exception ex)
            {
                ShowLog(ex.Message);
            }
        }

        private async void buttonStartCCTruckSimulation_Click(object sender, RoutedEventArgs e)
        {
            var ccTruckId = (string)lbCCTrucks.SelectedItem;
            if (string.IsNullOrEmpty(ccTruckId))
            {
                MessageBox.Show("Please select CC Truck!");
                return;
            }
            try
            {
                var ccTruckTwin = ccTruckTwins[ccTruckId];
                var deviceId = ccTruckTwin.Contents["iothub_deviceid"].ToString();
                Device target = await registryManager.GetDeviceAsync(deviceId);
                if (target != null)
                {
                    var builder = Microsoft.Azure.Devices.IotHubConnectionStringBuilder.Create(tbIoTHubConnectionString.Text);
                    string connectionString = $"HostName={builder.HostName};DeviceId={target.Id};SharedAccessKey={target.Authentication.SymmetricKey.PrimaryKey}";

                    var simulatorWindow = new WindowCCTruckSimulator(this)
                    {
                        IotHubDeviceConnectionString = connectionString,
                        Target = ccTruckTwins[ccTruckId]
                    };
                    simulatorWindow.Show();
                }
            }
            catch(Exception ex)
            {
                ShowLog(ex.Message);
            }
        }

        private async void buttonStartDTruckSimulation_Click(object sender, RoutedEventArgs e)
        {
            var dTruckId = (string)lbDTrucks.SelectedItem;
            if (string.IsNullOrEmpty(dTruckId))
            {
                MessageBox.Show("Please select Delivery Truck!");
                return;
            }
            try
            {
                var deviceId = dTruckTwins[dTruckId].Contents["iothub_deviceid"].ToString();
                Device target = await registryManager.GetDeviceAsync(deviceId);
                if (target != null)
                {
                    var builder = Microsoft.Azure.Devices.IotHubConnectionStringBuilder.Create(tbIoTHubConnectionString.Text);
                    string connectionString = $"HostName={builder.HostName};DeviceId={target.Id};SharedAccessKey={target.Authentication.SymmetricKey.PrimaryKey}";

                    var simulatorWindow = new WindowDeliveryTruckDriverMobileDeviceSimulator(this)
                    {
                        IotHubDeviceConnectionString = connectionString,
                        Target = dTruckTwins[dTruckId]
                    };
                    var tmdRels = twinsClient.GetIncomingRelationshipsAsync(dTruckId);
                    await foreach (var rel in tmdRels)
                    {
                        if (rel.RelationshipName == "assigned_to")
                        {
                            simulatorWindow.TemparetureMewasurementDevices.Add(new TemperatureMeasurementDevice() { Id = rel.SourceId });
                        }
                    }
                    simulatorWindow.Show();
                }
            }
            catch (Exception ex)
            {
                ShowLog(ex.Message);
            }
        }

        private void lbCCTrucks_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (lbCCTrucks.SelectedItem != null)
            {
                buttonStartCCTruckSimulation.IsEnabled = true;
            }
        }

        private void lbDTrucks_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (lbDTrucks.SelectedItem != null)
            {
                buttonStartDTruckSimulation.IsEnabled = true;
            }
        }
    }
}
