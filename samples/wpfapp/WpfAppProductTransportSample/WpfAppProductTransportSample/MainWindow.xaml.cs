using Azure;
using Azure.DigitalTwins.Core;
using Azure.Identity;
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

namespace WpfAppProductTransportSample
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            this.Loaded += MainWindow_Loaded;
        }

        DigitalTwinsClient twinsClient = null;

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            this.cbStationsForCustomer.ItemsSource = stations;
            this.cbFactories.ItemsSource = factories;
            this.cbCCTrucks.ItemsSource = ccTrucks;
            this.cbDTruck.ItemsSource = dvTrucks;

            this.lbProducts.ItemsSource = products;

            try
            {
                var config = new ConfigurationBuilder().AddJsonFile("appsettings.json", optional: false, reloadOnChange: false).Build();
                tbADTInstanceUrl.Text = config["instanceurl"];
            }
            catch (Exception ex)
            {
                ShowLog(ex.Message);
            }
        }


        void ShowLog(string log)
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

        ObservableCollection<string> stations = new ObservableCollection<string>();
        ObservableCollection<string> factories = new ObservableCollection<string>();
        ObservableCollection<string> ccTrucks = new ObservableCollection<string>();
        ObservableCollection<string> dvTrucks = new ObservableCollection<string>();
        ObservableCollection<ProductInfo> products = new ObservableCollection<ProductInfo>();

        private void buttonConnectToADT_Click(object sender, RoutedEventArgs e)
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
                ShowLog("Conneced.");
                buttonGetStationsForCustomer.IsEnabled = true;
            }
            catch (Exception ex)
            {
                ShowLog(ex.Message);
            }
        }

        static readonly string stationModelId = "dtmi:embeddedgeorge:transport:const_temperature:sample:Station;1";
        static readonly string customerModelId = "dtmi:embeddedgeorge:transport:const_temperature:sample:Customer;1";
        static readonly string factoryModelId = "dtmi:embeddedgeorge:transport:const_temperature:sample:Factory;1";
        static readonly string orderModelId = "dtmi:embeddedgeorge:transport:const_temperature:sample:Order;1";
        static readonly string productModelId = "dtmi:embeddedgeorge:transport:const_temperature:sample:Product;1";
        static readonly string ccTruckModelId = "dtmi:embeddedgeorge:transport:const_temperature:CoolingContainerTruck;1";
        static readonly string dvTruckModelId = "dtmi:embeddedgeorge:transport:const_temperature:sample:DeliveryTruck;1";

        private async Task UpdateOrderStatus(string id, string newStatus)
        {
            var query = $"SELECT * FROM DigitalTwins WHERE IS_OF_MODEL('{orderModelId}') AND $dtId='{id}'";
            var queryResponse = twinsClient.QueryAsync<BasicDigitalTwin>(query);
            BasicDigitalTwin target = null;
            await foreach(var order in queryResponse)
            {
                target = order;
                break;
            }
            var updateTwin = new JsonPatchDocument();
            if (target != null)
            {
                if (target.Contents.ContainsKey("Status"))
                {
                    updateTwin.AppendReplace("/Status", newStatus);
                }
                else
                {
                    updateTwin.AppendAdd("/Status", newStatus);
                }
                await twinsClient.UpdateDigitalTwinAsync(id, updateTwin);

                ShowLog($"Updated order[{id}] Status -> {newStatus}");
                tbOrderStatus.Text = newStatus;
            }
        }

        private async Task UpdateTruckLocation(BasicDigitalTwin truck, string newLocation)
        {
            var updateTwin = new JsonPatchDocument();
            if (truck.Contents.ContainsKey("Location"))
            {
                updateTwin.AppendReplace("/Location", newLocation);
            }
            else
            {
                updateTwin.AppendAdd("/Location", newLocation);
            }
            await twinsClient.UpdateDigitalTwinAsync(truck.Id, updateTwin);

            ShowLog($"Updated Truck[{truck.Id}] Location -> {newLocation}");
        }


        private async void buttonGetStationsForCustomer_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                stations.Clear();
                var query = $"SELECT * FROM DigitalTwins WHERE IS_OF_MODEL('{stationModelId}')";
                var queryResponse = twinsClient.QueryAsync<BasicDigitalTwin>(query);
                await foreach (var station in queryResponse)
                {
                    stations.Add(station.Id);
                }
                cbStationsForCustomer.IsEnabled = true;
            }
            catch (Exception ex)
            {
                ShowLog(ex.Message);
            }
        }

        private void cbStationsForCustomer_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            buttonCreateCustomer.IsEnabled = true;
        }

        private async void buttonCreateCustomer_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(tbCustomerName.Text) || string.IsNullOrEmpty(tbCustomerTelNo.Text) || string.IsNullOrEmpty(tbCustomerAddress.Text))
            {
                MessageBox.Show("Please input 'Name', 'Tel No' and 'Address'.");
                return;
            }
            var customerId = tbCustomerId.Text;
            if (string.IsNullOrWhiteSpace(customerId) || string.IsNullOrEmpty(customerId))
            {
                customerId = tbCustomerName.Text.Trim().Replace(" ", "_");
                tbCustomerId.Text = customerId;
            }
            if (string.IsNullOrWhiteSpace(tbCustomerDtId.Text) || string.IsNullOrEmpty(tbCustomerDtId.Text))
            {
                tbCustomerDtId.Text = customerId;
            }
            if (cbStationsForCustomer.SelectedItem == null)
            {
                MessageBox.Show("Please select station for this customer");
                return;
            }
            try
            {
                var newCustomer = new BasicDigitalTwin()
                {
                    Id = tbCustomerDtId.Text,
                    Metadata =
                    {
                        ModelId = customerModelId
                    },
                    Contents =
                    {
                        {"CustomerId", customerId },
                        {"Name", tbCustomerName.Text },
                        {"TelNo",tbCustomerTelNo.Text },
                        {"Address",tbCustomerAddress.Text }
                    }
                };
                await twinsClient.CreateOrReplaceDigitalTwinAsync(customerId, newCustomer);
                ShowLog($"Created : Customer[{newCustomer.Id}]");

                buttonCreateOrder.IsEnabled = true;
                buttonCreateCustomer.IsEnabled = false;
            }
            catch(Exception ex)
            {
                ShowLog(ex.Message);
            }
        }

        string currentOrderId = null;
        private async void buttonCreateOrder_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(tbOrderId.Text) || string.IsNullOrEmpty(tbOrderId.Text))
                {
                    MessageBox.Show("Please input Order Id");
                    return;
                }

                var orderId = tbOrderId.Text.Trim().Replace(" ", "_");
                tbOrderDtId.Text = orderId;
                var newOrder = new BasicDigitalTwin()
                {
                    Id = orderId,
                    Metadata =
                {
                    ModelId=orderModelId
                },
                    Contents =
                {
                    {"OrderId",tbOrderId.Text },
                    {"Status", "Ordered" }
                }
                };
                tbOrderStatus.Text = newOrder.Contents["Status"] as string;
                await twinsClient.CreateOrReplaceDigitalTwinAsync(orderId, newOrder);
                ShowLog($"Created : Order[{newOrder.Id}]");
                currentOrderId = newOrder.Id;

                var relationship = new BasicRelationship()
                {
                    Name = "ordered_by",
                    SourceId = orderId,
                    TargetId = tbCustomerDtId.Text
                };
                var relationshipId = $"{relationship.SourceId}-{relationship.Name}-{relationship.TargetId}";
                await twinsClient.CreateOrReplaceRelationshipAsync(relationship.SourceId, relationshipId, relationship);
                ShowLog($"Created relationship : {relationshipId}");

                buttonGetFactory.IsEnabled = true;
                buttonCreateOrder.IsEnabled = false;
            }
            catch (Exception ex)
            {
                ShowLog(ex.Message);
            }
        }

        private async void buttonGetFactory_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                factories.Clear();
                var query = $"SELECT * FROM DigitalTwins WHERE IS_OF_MODEL('{factoryModelId}')";
                var queryResponse = twinsClient.QueryAsync<BasicDigitalTwin>(query);
                await foreach (var factory in queryResponse)
                {
                    factories.Add(factory.Id);
                }

                cbFactories.IsEnabled = true;
            }
            catch (Exception ex)
            {
                ShowLog(ex.Message);
            }
        }

        private void cbFactories_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            buttonAssignFactory.IsEnabled = true;
        }

        string currentFactoryId = null;
        private async void buttonAssignFactory_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var now = DateTime.Now;
                var relationship = new BasicRelationship()
                {
                    Name = "is_assigned_for",
                    SourceId = currentOrderId,
                    TargetId = cbFactories.SelectedItem as string,
                    Properties =
                {
                    {"AssignedDate", now.ToString("yyyy-MM-dd HH:mm:ss")}
                }
                };
                string relationshipId = $"{relationship.SourceId}-{relationship.Name}-{relationship.TargetId}";
                await twinsClient.CreateOrReplaceRelationshipAsync(relationship.SourceId, relationshipId, relationship);

                ShowLog($"Created relationship : {relationshipId}");
                currentFactoryId = relationship.TargetId;

                await UpdateOrderStatus(currentOrderId, "FactoryAssigend");

                buttonCreatedProduct.IsEnabled = true;

                buttonAssignFactory.IsEnabled = false;
                buttonGetFactory.IsEnabled = false;
                cbFactories.IsEnabled = false;
            }
            catch (Exception ex)
            {
                ShowLog(ex.Message);
            }
        }

        string currentProductId = null;
        private async void buttonCreatedProduct_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var id = Guid.NewGuid().ToString();
                var newProduct = new BasicDigitalTwin()
                {
                    Id = id,
                    Metadata =
                {
                    ModelId=productModelId
                },
                    Contents =
                {
                    {"ProductId",id },
                    {"CurrentTemperature",-10.0 },
                    {"LowAllowableTemperature", -15.0 },
                    {"HighAllowableTemperature", -5.0 },
                    {"Location",$"factory:{currentFactoryId}" },
                    {"Status", 0 }
                }
                };
                await twinsClient.CreateOrReplaceDigitalTwinAsync(id, newProduct);
                ShowLog($"Created : Product[{id}]");
                currentProductId = newProduct.Id;

                var now = DateTime.Now;
                var relationship = new BasicRelationship()
                {
                    Name = "created",
                    SourceId = currentOrderId,
                    TargetId = newProduct.Id
                };
                var relationshipId = $"{relationship.SourceId}-{relationship.Name}-{relationship.TargetId}";
                await twinsClient.CreateOrReplaceRelationshipAsync(currentOrderId, relationshipId, relationship);
                ShowLog($"Created relationship - {relationshipId}");

                relationship = new BasicRelationship()
                {
                    Name = "created_at",
                    SourceId = newProduct.Id,
                    TargetId = currentFactoryId,
                    Properties =
                {
                    {"CreatedDate",now.ToString("yyyy-MM-dd HH:mm:ss") }
                }
                };
                relationshipId = $"{relationship.SourceId}-{relationship.Name}-{relationship.TargetId}";
                await twinsClient.CreateOrReplaceRelationshipAsync(newProduct.Id, relationshipId, relationship);
                ShowLog($"Created relationship - {relationshipId}");

                await UpdateOrderStatus(currentOrderId, "ProductCreated");

                // Find Cooling Container Trucks
                ccTrucks.Clear();
                var query = $"SELECT * FROM DigitalTwins WHERE IS_OF_MODEL('{ccTruckModelId}')";
                var queryResponse = twinsClient.QueryAsync<BasicDigitalTwin>(query);
                await foreach(var truck in queryResponse)
                {
                    ccTrucks.Add(truck.Id);
                }

                cbCCTrucks.IsEnabled = true;
                buttonCreatedProduct.IsEnabled = false;

            }
            catch(Exception ex)
            {
                ShowLog(ex.Message);
            }
        }

        private void cbCCTrucks_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            buttonPickToCCTruck.IsEnabled = true;
        }

        string currentCCTruckId = null;
        string currentDestinationStationId = null;
        private async void buttonPickToCCTruck_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                currentCCTruckId = cbCCTrucks.SelectedItem as string;
                var query = $"SELECT * FROM DigitalTwins WHERE IS_OF_MODEL('{ccTruckModelId}') AND $dtId='{currentCCTruckId}'";
                BasicDigitalTwin ccTruck = null;
                var queryResponse = twinsClient.QueryAsync<BasicDigitalTwin>(query);
                await foreach (var truck in queryResponse)
                {
                    ccTruck = truck;
                    break;
                }
                if (ccTruck != null)
                {
                    var newLocation = $"Factory:{currentFactoryId}";
                    await UpdateTruckLocation(ccTruck, newLocation);

                    var relationship = new BasicRelationship()
                    {
                        Name = "carrying",
                        SourceId = ccTruck.Id,
                        TargetId = currentProductId
                    };
                    var relationshipId = $"{relationship.SourceId}-{relationship.Name}-{relationship.TargetId}";
                    await twinsClient.CreateOrReplaceRelationshipAsync(ccTruck.Id, relationshipId, relationship);
                    ShowLog($"Created Relationship - {relationshipId}");

                    // Find destination station for Cooling Container Truck
                    ShowLog("Search station where current truck will drive to...");
                    query = $"SELECT customer FROM digitaltwins order JOIN customer RELATED order.order_by WHERE order.OrderId = '{currentOrderId}' AND IS_OF_MODEL(order, '{orderModelId}')";
                    queryResponse = twinsClient.QueryAsync<BasicDigitalTwin>(query);
                    BasicDigitalTwin targetCustomer = null;
                    await foreach (var customer in queryResponse)
                    {
                        targetCustomer = customer;
                        break;
                    }
                    BasicDigitalTwin responsibleStation = null;
                    if (targetCustomer != null)
                    {
                        ShowLog($"Found Customer[{targetCustomer.Id}] of Order[{currentOrderId}] then search responsible station...");
                        query = $"SELECT station FROM digitalTwins station JOIN custoer RELATED station.responsible_for WHERE customer.CustomerId = '{targetCustomer.Id}' AND IS_OF_MODEL(station, '{stationModelId}')";
                        queryResponse = twinsClient.QueryAsync<BasicDigitalTwin>(query);
                        await foreach (var station in queryResponse)
                        {
                            responsibleStation = station;
                            break;
                        }
                    }
                    if (responsibleStation != null)
                    {
                        currentDestinationStationId = responsibleStation.Id;
                        ShowLog($"Found Station[{responsibleStation.Id}]...");
                        relationship = new BasicRelationship()
                        {
                            Name = "drive_to",
                            SourceId = ccTruck.Id,
                            TargetId = responsibleStation.Id
                        };
                        relationshipId = $"{relationship.SourceId}-${relationship.Name}-${relationship.TargetId}";
                        await twinsClient.CreateOrReplaceRelationshipAsync(ccTruck.Id, relationshipId, relationship);
                        ShowLog($"Created relationship - {relationshipId}");
                        ShowLog($"CoolingContainerTruck[{ccTruck.Id}] is ready for driving to Station[{responsibleStation.Id}]");

                        await UpdateOrderStatus(currentOrderId, "TransportingToStation");

                        buttonCreatedProduct.IsEnabled = false;
                        cbCCTrucks.IsEnabled = false;
                        buttonPickToCCTruck.IsEnabled = false;

                        buttonArrivedToStation.IsEnabled = true;
                    }
                }
            }
            catch(Exception ex)
            {
                ShowLog(ex.Message);
            }
        }

        private async void buttonArrivedToStation_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Update Cooling Container Truck location because the truck has arrive to the destination station
                var query = $"SELECT * FROM DigitalTwins WHERE IS_OF_MODEL('{ccTruckModelId}') AND $dtId='{currentCCTruckId}'";
                BasicDigitalTwin ccTruck = null;
                var queryResponse = twinsClient.QueryAsync<BasicDigitalTwin>(query);
                await foreach (var truck in queryResponse)
                {
                    ccTruck = truck;
                    break;
                }
                if (ccTruck != null)
                {
                    var newLocation = $"Station:{currentDestinationStationId}";
                    await UpdateTruckLocation(ccTruck, newLocation);

                    // and delete 'drive_to' relationship
                    // and move products which has been carryed by this truck to Station as parallel
                    var driveToRels = twinsClient.GetRelationshipsAsync<BasicRelationship>(currentCCTruckId);
                    await foreach (var rel in driveToRels)
                    {
                        if (rel.Name == "drive_to")
                        {
                            await twinsClient.DeleteRelationshipAsync(currentCCTruckId, rel.Id);
                            ShowLog($"Deleted relationship - {rel.Id}");
                        }
                        else if (rel.Name == "carrying")
                        {
                            // Cooling Container Truck が、複数のStationに Product を運ぶ場合は、この Relationship から Product -> Order -> Customer -> Station と辿って、現在の Station に合致するものだけ荷下ろしするという処理が必要になる  
                            // ここでは、簡単のため、積載されている全ての Product が到着した Station 向けであると仮定している。
                            await twinsClient.DeleteRelationshipAsync(currentCCTruckId, rel.Id);
                            ShowLog($"Deleted relationship - {rel.Id}");

                            var relationship = new BasicRelationship()
                            {
                                Name = "sort_to",
                                SourceId = currentDestinationStationId,
                                TargetId = rel.TargetId
                            };
                            var relationshipId = $"{relationship.SourceId}-{relationship.Name}-{relationship.TargetId}";
                            await twinsClient.CreateOrReplaceRelationshipAsync(currentDestinationStationId, relationshipId, relationship);
                            ShowLog($"Created relationship - {relationshipId}");

                            await UpdateOrderStatus(currentOrderId, "PreparingAtStation");
                        }
                    }

                    // Find delivery trucks
                    dvTrucks.Clear();
                    query = $"SELECT truck FROM digitaltwins station JOIN truck RELATED station.assign_to WHERE station.$dtId = '{currentDestinationStationId}' AND IS_OF_MODEL(station, '{stationModelId}')";
                    queryResponse = twinsClient.QueryAsync<BasicDigitalTwin>(query);
                    await foreach(var truck in queryResponse)
                    {
                        dvTrucks.Add(truck.Id);
                    }
                    buttonArrivedToStation.IsEnabled = false;
                    cbDTruck.IsEnabled = true;
                }
            }
            catch (Exception ex)
            {
                ShowLog(ex.Message);
            }
        }

        private void cbDTruck_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            buttonPickToDTruck.IsEnabled = true;
        }

        string currentDVTruckId = null;
        private async void buttonPickToDTruck_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                currentDVTruckId = cbDTruck.SelectedItem as string;
                // Delete sort_to relationship
                var rels = twinsClient.GetRelationshipsAsync<BasicRelationship>(currentDestinationStationId, relationshipName: "sort_to");
                await foreach (var rel in rels)
                {
                    await twinsClient.DeleteRelationshipAsync(currentDestinationStationId, rel.Id);
                    ShowLog($"Deleted relationship - {rel.Id}");
                    var relationship = new BasicRelationship()
                    {
                        Name = "carrying",
                        SourceId = currentDVTruckId,
                        TargetId = rel.TargetId
                    };
                    var relationshipId = $"{relationship.SourceId}-{relationship.Name}-{relationship.TargetId}";
                    await twinsClient.CreateOrReplaceRelationshipAsync(currentDVTruckId, relationshipId, relationship);
                    ShowLog($"Created relationship - {relationshipId}");

                    await UpdateOrderStatus(currentOrderId, "DeliveringToCustomer");

                    buttonDeliverToCustomer.IsEnabled = true;
                    buttonPickToDTruck.IsEnabled = false;
                    cbDTruck.IsEnabled = false;
                }
            }
            catch (Exception ex)
            {
                ShowLog(ex.Message);
            }
        }

        private async void buttonDeliverToCustomer_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var carringRels = twinsClient.GetRelationshipsAsync<BasicRelationship>(currentDVTruckId, relationshipName: "carrying");
                await foreach (var rel in carringRels)
                {
                    var query = $"SELECT order FROM digitalTwins order JOIN product RELATED order.created WHERE product.$dtId = '{currentProductId}' AND IS_OF_MODEL(order, '{orderModelId}')";
                    var queryResponse = twinsClient.QueryAsync<BasicDigitalTwin>(query);
                    await foreach (var order in queryResponse)
                    {
                        if (order.Id == currentOrderId)
                        {
                            await twinsClient.DeleteRelationshipAsync(currentDVTruckId, rel.Id);
                            ShowLog($"Deleted relationship - {rel.Id}");

                            await UpdateOrderStatus(currentOrderId, "Delivered");
                        }
                    }
                }
            }
            catch(Exception ex)
            {
                ShowLog(ex.Message);
            }
        }
    }

    public class ProductInfo
    {
        public string Id { get; set; }
        public string ProductId { get; set; }
        public string Status { get; set; }
        public string Location { get; set; }
        public double Temperature { get; set; }
    }
}
