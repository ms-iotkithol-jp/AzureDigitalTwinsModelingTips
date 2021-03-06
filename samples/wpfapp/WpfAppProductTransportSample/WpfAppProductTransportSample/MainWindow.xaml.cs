using Azure;
using Azure.DigitalTwins.Core;
using Azure.Identity;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
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
            this.cbCurrentCustomers.ItemsSource = currentCustomers;
            this.cbOrders.ItemsSource = ordersByCustomer;

            this.lbProducts.ItemsSource = products;

            try
            {
                var config = new ConfigurationBuilder().AddJsonFile("appsettings.json", optional: false, reloadOnChange: false).Build();
                tbADTInstanceUrl.Text = config["adt-instance-url"];
                tbSignalRInstanceUrl.Text = config["signalr-url"];

                twinOpT.TextBoxes.Add(tbCustomerDtId);
                twinOpT.TextBoxes.Add(tbCustomerId);
                twinOpT.TextBoxes.Add(tbCustomerName);
                twinOpT.TextBoxes.Add(tbCustomerAddress);
                twinOpT.TextBoxes.Add(tbCustomerTelNo);
                twinOpT.TextBoxes.Add(tbOrderDtId);
                twinOpT.TextBoxes.Add(tbOrderId);
                twinOpT.TextBoxes.Add(tbOrderStatus);
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
        ObservableCollection<OrderAndProductInfo> products = new ObservableCollection<OrderAndProductInfo>();
        ObservableCollection<string> currentCustomers = new ObservableCollection<string>();
        ObservableCollection<string> ordersByCustomer = new ObservableCollection<string>();

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
                buttonGetCurrentCustomers.IsEnabled = true;
                buttonDeleteOrderProductCustomer.IsEnabled = true;
                buttonConnectToSignalR.IsEnabled = true;
                buttonConnectToADT.IsEnabled = false;
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
        static readonly string ccTruckModelId = "dtmi:embeddedgeorge:transport:const_temperature:sample:CoolingContainerTruck;1";
        static readonly string dvTruckModelId = "dtmi:embeddedgeorge:transport:const_temperature:sample:DeliveryTruck;1";
        static readonly string tmdModelId = "dtmi:embeddedgeorge:transport:const_temperature:sample:TemperatureMeasurementDevice:deviceid;1";

        TwinOperationThread twinOpT = new TwinOperationThread();

        private async Task UpdateOrderStatus(string id, string newStatus)
        {
            var query = $"SELECT * FROM DigitalTwins WHERE IS_OF_MODEL('{orderModelId}') AND $dtId='{id}'";
            var queryResponse = twinsClient.QueryAsync<BasicDigitalTwin>(query);
            BasicDigitalTwin target = null;
            await foreach (var order in queryResponse)
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
                await GetCurrentStations();
                cbStationsForCustomer.IsEnabled = true;
            }
            catch (Exception ex)
            {
                ShowLog(ex.Message);
            }
        }

        private async Task GetCurrentStations()
        {
            stations.Clear();
            var query = $"SELECT * FROM DigitalTwins WHERE IS_OF_MODEL('{stationModelId}')";
            var queryResponse = twinsClient.QueryAsync<BasicDigitalTwin>(query);
            await foreach (var station in queryResponse)
            {
                stations.Add(station.Id);
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

                string selectedStationId = (string)cbStationsForCustomer.SelectedItem;
                var rel = new BasicRelationship()
                {
                    Name = "responsible_for",
                    SourceId = selectedStationId,
                    TargetId = newCustomer.Id
                };
                string relId = $"{rel.SourceId}-{rel.Name}-{rel.TargetId}";
                await twinsClient.CreateOrReplaceRelationshipAsync(selectedStationId, relId, rel);
                ShowLog($"Created relationship : {relId}");

                var rels = twinsClient.GetRelationshipsAsync<BasicRelationship>(selectedStationId);
                await foreach (var r in rels)
                {
                    relId = r.Id;
                }

                twinOpT.currentCustomerId = tbCustomerDtId.Text;
                tbOrderId.Text = $"order-{twinOpT.currentCustomerId}";

                buttonCreateOrder.IsEnabled = true;
                buttonCreateCustomer.IsEnabled = false;
            }
            catch (Exception ex)
            {
                ShowLog(ex.Message);
            }
        }

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
                twinOpT.currentOrderId = newOrder.Id;

                var relationship = new BasicRelationship()
                {
                    Name = "ordered_by",
                    SourceId = orderId,
                    TargetId = twinOpT.currentCustomerId
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
                await GetCurrentFactories();

                cbFactories.IsEnabled = true;
            }
            catch (Exception ex)
            {
                ShowLog(ex.Message);
            }
        }

        private async Task GetCurrentFactories()
        {
            factories.Clear();
            var query = $"SELECT * FROM DigitalTwins WHERE IS_OF_MODEL('{factoryModelId}')";
            var queryResponse = twinsClient.QueryAsync<BasicDigitalTwin>(query);
            await foreach (var factory in queryResponse)
            {
                factories.Add(factory.Id);
            }
        }

        private void cbFactories_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            buttonAssignFactory.IsEnabled = true;
        }

        private async void buttonAssignFactory_Click(object sender, RoutedEventArgs e)
        {
            if (cbFactories.SelectedItem == null)
            {
                MessageBox.Show("Please select factory");
                return;
            }
            try
            {
                var now = DateTime.Now;
                var relationship = new BasicRelationship()
                {
                    Name = "is_assigned_for",
                    SourceId = twinOpT.currentOrderId,
                    TargetId = cbFactories.SelectedItem as string,
                    Properties =
                        {
                            {"AssignedDate", now}
                        }
                };
                string relationshipId = $"{relationship.SourceId}-{relationship.Name}-{relationship.TargetId}";
                await twinsClient.CreateOrReplaceRelationshipAsync(relationship.SourceId, relationshipId, relationship);

                ShowLog($"Created relationship : {relationshipId}");
                twinOpT.currentFactoryId = relationship.TargetId;

                await UpdateOrderStatus(twinOpT.currentOrderId, "FactoryAssigend");

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

        private async void buttonCreatedProduct_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var productRels = twinsClient.GetRelationshipsAsync<BasicRelationship>(twinOpT.currentOrderId, "created");
                bool noProduct = true;
                await foreach(var pRel in productRels)
                {
                    noProduct = false;
                    break;
                }
                if (noProduct)
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
                    {"Location",$"factory:{twinOpT.currentFactoryId}" },
                    {"Status", 0 }
                }
                    };
                    await twinsClient.CreateOrReplaceDigitalTwinAsync(id, newProduct);
                    ShowLog($"Created : Product[{id}]");
                    twinOpT.currentProductId = newProduct.Id;

                    var now = DateTime.Now;
                    var relationship = new BasicRelationship()
                    {
                        Name = "created",
                        SourceId = twinOpT.currentOrderId,
                        TargetId = newProduct.Id
                    };
                    var relationshipId = $"{relationship.SourceId}-{relationship.Name}-{relationship.TargetId}";
                    await twinsClient.CreateOrReplaceRelationshipAsync(twinOpT.currentOrderId, relationshipId, relationship);
                    ShowLog($"Created relationship - {relationshipId}");

                    relationship = new BasicRelationship()
                    {
                        Name = "created_at",
                        SourceId = newProduct.Id,
                        TargetId = twinOpT.currentFactoryId,
                        Properties =
                        {
                            {"CreatedDate",now }
                        }
                    };
                    relationshipId = $"{relationship.SourceId}-{relationship.Name}-{relationship.TargetId}";
                    await twinsClient.CreateOrReplaceRelationshipAsync(newProduct.Id, relationshipId, relationship);
                    ShowLog($"Created relationship - {relationshipId}");

                    await UpdateOrderStatus(twinOpT.currentOrderId, "ProductCreated");
                }
                // Find Cooling Container Trucks
                ccTrucks.Clear();
                var query = $"SELECT * FROM DigitalTwins WHERE IS_OF_MODEL('{ccTruckModelId}') AND Status=0";
                var queryResponse = twinsClient.QueryAsync<BasicDigitalTwin>(query);
                await foreach (var truck in queryResponse)
                {
                    ccTrucks.Add(truck.Id);
                }
                if (ccTrucks.Count == 0)
                {
                    MessageBox.Show("There is no Cooling Container Truck that has been at this Factory");

                }
                else
                {
                    cbCCTrucks.IsEnabled = true;
                    buttonCreatedProduct.IsEnabled = false;
                }

            }
            catch (Exception ex)
            {
                ShowLog(ex.Message);
            }
        }

        private void cbCCTrucks_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            buttonPickToCCTruck.IsEnabled = true;
        }

        private async void buttonPickToCCTruck_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                twinOpT.currentCCTruckId = cbCCTrucks.SelectedItem as string;
                var query = $"SELECT * FROM DigitalTwins WHERE IS_OF_MODEL('{ccTruckModelId}') AND $dtId='{twinOpT.currentCCTruckId}'";
                BasicDigitalTwin ccTruck = null;
                var queryResponse = twinsClient.QueryAsync<BasicDigitalTwin>(query);
                await foreach (var truck in queryResponse)
                {
                    ccTruck = truck;
                    break;
                }
                if (ccTruck != null)
                {
                    var newLocation = $"Factory:{twinOpT.currentFactoryId}";
                    await UpdateTruckLocation(ccTruck, newLocation);

                    var relationship = new BasicRelationship()
                    {
                        Name = "carrying",
                        SourceId = ccTruck.Id,
                        TargetId = twinOpT.currentProductId
                    };
                    var relationshipId = $"{relationship.SourceId}-{relationship.Name}-{relationship.TargetId}";
                    await twinsClient.CreateOrReplaceRelationshipAsync(ccTruck.Id, relationshipId, relationship);
                    ShowLog($"Created Relationship - {relationshipId}");

                    // Find destination station for Cooling Container Truck
                    ShowLog("Search station where current truck will drive to...");
                    // 'order' can't be used in query because ...
                    query = $"SELECT customer FROM digitaltwins O JOIN customer RELATED O.ordered_by WHERE O.$dtId = '{twinOpT.currentOrderId}' AND IS_OF_MODEL(O, '{orderModelId}')";
                    queryResponse = twinsClient.QueryAsync<BasicDigitalTwin>(query);
                    string targetCustomerId = null;
                    await foreach (var customer in queryResponse)
                    {
                        targetCustomerId = ((JsonElement)customer.Contents["customer"]).GetProperty("$dtId").GetString();
                        break;
                    }
                    string responsibleStationId = null;
                    if (targetCustomerId != null)
                    {
                        ShowLog($"Found Customer[{targetCustomerId}] of Order[{twinOpT.currentOrderId}] then search responsible station...");
                        // query = $"SELECT station FROM digitalTwins station JOIN customer RELATED station.responsible_for WHERE customer.$dtId = '{targetCustomerId}' AND IS_OF_MODEL(station, '{stationModelId}')";
                        // queryResponse = twinsClient.QueryAsync<BasicDigitalTwin>(query);
                        // await foreach (var station in queryResponse)
                        // {
                        //     responsibleStationId = ((JsonElement) station.Contents["station"]).GetProperty("$dtId").GetString();
                        //     break;
                        //  }
                        // following code and above code are equivarent
                        var stationForCustomerRels = twinsClient.GetIncomingRelationshipsAsync(targetCustomerId);
                        await foreach (var sFcRels in stationForCustomerRels)
                        {
                            if (sFcRels.RelationshipName == "responsible_for")
                            {
                                responsibleStationId = sFcRels.SourceId;
                                break;
                            }
                        }
                    }
                    if (responsibleStationId != null)
                    {
                        twinOpT.currentDestinationStationId = responsibleStationId;
                        ShowLog($"Found Station[{responsibleStationId}]...");
                        relationship = new BasicRelationship()
                        {
                            Name = "drive_to",
                            SourceId = ccTruck.Id,
                            TargetId = responsibleStationId
                        };
                        relationshipId = $"{relationship.SourceId}-{relationship.Name}-{relationship.TargetId}";
                        await twinsClient.CreateOrReplaceRelationshipAsync(ccTruck.Id, relationshipId, relationship);
                        ShowLog($"Created relationship - {relationshipId}");
                        ShowLog($"CoolingContainerTruck[{ccTruck.Id}] is ready for driving to Station[{responsibleStationId}]");

                        await UpdateOrderStatus(twinOpT.currentOrderId, "TransportingToStation");

                        buttonCreatedProduct.IsEnabled = false;
                        cbCCTrucks.IsEnabled = false;
                        buttonPickToCCTruck.IsEnabled = false;

                        buttonArrivedToStation.IsEnabled = true;
                    }
                }
            }
            catch (Exception ex)
            {
                ShowLog(ex.Message);
            }
        }

        private async void buttonArrivedToStation_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Update Cooling Container Truck location because the truck has arrive to the destination station
                var query = $"SELECT * FROM DigitalTwins WHERE IS_OF_MODEL('{ccTruckModelId}') AND $dtId='{twinOpT.currentCCTruckId}'";
                BasicDigitalTwin ccTruck = null;
                var queryResponse = twinsClient.QueryAsync<BasicDigitalTwin>(query);
                await foreach (var truck in queryResponse)
                {
                    ccTruck = truck;
                    break;
                }
                if (ccTruck != null)
                {
                    // and delete 'drive_to' relationship
                    // and move products which has been carryed by this truck to Station as parallel
                    var driveToRels = twinsClient.GetRelationshipsAsync<BasicRelationship>(twinOpT.currentCCTruckId);
                    await foreach (var rel in driveToRels)
                    {
                        if (rel.Name == "drive_to")
                        {
                            await twinsClient.DeleteRelationshipAsync(twinOpT.currentCCTruckId, rel.Id);
                            ShowLog($"Deleted relationship - {rel.Id}");

                            var newLocation = $"Station:{twinOpT.currentDestinationStationId}";
                            await UpdateTruckLocation(ccTruck, newLocation);
                        }
                        else if (rel.Name == "carrying")
                        {
                            bool isValidProduct = false;
                            var orderForProductRels = twinsClient.GetIncomingRelationshipsAsync(rel.TargetId);
                            await foreach (var oFpRel in orderForProductRels)
                            {
                                if (oFpRel.RelationshipName == "created")
                                {
                                    var customerForOrderRels = twinsClient.GetRelationshipsAsync<BasicRelationship>(oFpRel.SourceId, relationshipName: "ordered_by");
                                    await foreach (var cFoRel in customerForOrderRels)
                                    {
                                        var stationForCustomerRels = twinsClient.GetIncomingRelationshipsAsync(cFoRel.TargetId);
                                        await foreach (var sFcRel in stationForCustomerRels)
                                        {
                                            if (sFcRel.RelationshipName == "responsible_for")
                                            {
                                                isValidProduct = true;
                                                break;
                                            }
                                        }
                                    }
                                }
                            }
                            if (isValidProduct)
                            {
                                await twinsClient.DeleteRelationshipAsync(twinOpT.currentCCTruckId, rel.Id);
                                ShowLog($"Deleted relationship - {rel.Id}");

                                var relationship = new BasicRelationship()
                                {
                                    Name = "sort_to",
                                    SourceId = twinOpT.currentDestinationStationId,
                                    TargetId = rel.TargetId
                                };
                                var relationshipId = $"{relationship.SourceId}-{relationship.Name}-{relationship.TargetId}";
                                await twinsClient.CreateOrReplaceRelationshipAsync(twinOpT.currentDestinationStationId, relationshipId, relationship);

                                ShowLog($"Created relationship - {relationshipId}");
                                await UpdateOrderStatus(twinOpT.currentOrderId, "PreparingAtStation");
                            }
                        }
                    }

                    buttonSetTMD.IsEnabled = true;
                    buttonArrivedToStation.IsEnabled = false;
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
            buttonSetTMD.IsEnabled = false;
        }

        string currentDVTruckId = null;
        private async void buttonPickToDTruck_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                currentDVTruckId = cbDTruck.SelectedItem as string;
                // Delete sort_to relationship
                var rels = twinsClient.GetRelationshipsAsync<BasicRelationship>(twinOpT.currentDestinationStationId, relationshipName: "sort_to");
                await foreach (var rel in rels)
                {
                    await twinsClient.DeleteRelationshipAsync(twinOpT.currentDestinationStationId, rel.Id);
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

                    await UpdateOrderStatus(twinOpT.currentOrderId, "DeliveringToCustomer");

                    var pRels = twinsClient.GetIncomingRelationshipsAsync(rel.TargetId);
                    await foreach(var pRel in pRels)
                    {
                        if (pRel.RelationshipName == "target")
                        {
                            var tmdRel = new BasicRelationship()
                            {
                                Name = "assigned_to",
                                SourceId = pRel.SourceId,
                                TargetId = currentDVTruckId
                            };
                            var tmdRelId = $"{tmdRel.SourceId}-{tmdRel.Name}-{tmdRel.TargetId}";
                            await twinsClient.CreateOrReplaceRelationshipAsync(pRel.SourceId, tmdRelId, tmdRel);
                            ShowLog($"Created relationship - {tmdRelId}");
                        }
                    }

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
                    var orderRels = twinsClient.GetIncomingRelationshipsAsync(rel.TargetId);
                    await foreach (var orderRel in orderRels)
                    {
                        if (orderRel.RelationshipName == "created")
                        {
                            if (orderRel.SourceId == twinOpT.currentOrderId)
                            {
                                await twinsClient.DeleteRelationshipAsync(currentDVTruckId, rel.Id);
                                ShowLog($"Deleted relationship - {rel.Id}");

                                await UpdateOrderStatus(twinOpT.currentOrderId, "Delivered");
                                var tmdOfPRels = twinsClient.GetIncomingRelationshipsAsync(rel.TargetId);
                                await foreach(var tmdOfPRel in tmdOfPRels)
                                {
                                    if (tmdOfPRel.RelationshipName == "target")
                                    {
                                        await twinsClient.DeleteRelationshipAsync(tmdOfPRel.SourceId, tmdOfPRel.RelationshipId);
                                        ShowLog($"Deleted relationship - {tmdOfPRel.RelationshipId}");
                                    }
                                }
                            }
                        }
                    }
                }
                MessageBox.Show("Twin Graph operation thread has been done!");
                if (MessageBox.Show("Do you delete current order and product?", "Notice", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                {
                    bool deleteCustomer = true;
                    if (MessageBox.Show("Do you delete current customer?", "Notice", MessageBoxButton.YesNo)== MessageBoxResult.No)
                    {
                        deleteCustomer = false;
                    }
                    await DeleteOrderProductForCustomer(deleteCustomer, twinOpT.currentCustomerId, twinOpT.currentOrderId);
                    twinOpT.Clear();
                }
                buttonDeliverToCustomer.IsEnabled = false;
                buttonDTruckBackToStation.IsEnabled = true;
            }
            catch (Exception ex)
            {
                ShowLog(ex.Message);
            }
        }

        private async void buttonGetCurrentCustomers_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                currentCustomers.Clear();
                var query = $"SELECT * FROM DigitalTwins WHERE IS_OF_MODEL('{customerModelId}')";
                var queryResponse = twinsClient.QueryAsync<BasicDigitalTwin>(query);
                await foreach (var c in queryResponse)
                {
                    currentCustomers.Add($"{c.Id}:{c.Contents["Name"]}");
#if false
                    var rels = twinsClient.GetIncomingRelationshipsAsync(c.Id);
                    await foreach (var rel in rels)
                    {
                        var relId = rel.RelationshipId;
                        await twinsClient.DeleteRelationshipAsync(rel.SourceId, rel.RelationshipId);
                    }
#endif
                }

                cbCurrentCustomers.IsEnabled = true;
            }
            catch (Exception ex)
            {
                ShowLog(ex.Message);
            }
        }

        private async void cbCurrentCustomers_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cbCurrentCustomers.SelectedItem == null)
            {
                return;
            }
            try
            {
                string selectedCustomer = (string)cbCurrentCustomers.SelectedItem;
                string customerId = selectedCustomer.Split(":")[0];
                string query = $"SELECT * FROM DigitalTwins WHERE IS_OF_MODEL('{customerModelId}') AND $dtId='{customerId}'";
                var queryResponse = twinsClient.QueryAsync<BasicDigitalTwin>(query);
                await foreach (var c in queryResponse)
                {
                    if (c.Contents.ContainsKey("Name") && c.Contents.ContainsKey("TelNo") && c.Contents.ContainsKey("Address"))
                    {
                        tbCustomerId.Text = "";
                        tbCustomerName.Text = "";
                        tbCustomerTelNo.Text = "";
                        tbCustomerAddress.Text = "";

                        tbCustomerDtId.Text = customerId;
                        tbCustomerId.Text = c.Contents["CustomerId"].ToString();
                        tbCustomerName.Text = c.Contents["Name"].ToString();
                        tbCustomerTelNo.Text = c.Contents["TelNo"].ToString();
                        tbCustomerAddress.Text = c.Contents["Address"].ToString();

                        buttonCreateOrder.IsEnabled = true;
                    }
                    break;
                }
                await GetCurrentStations();
                var icRels = twinsClient.GetIncomingRelationshipsAsync(tbCustomerDtId.Text);
                await foreach (var rel in icRels)
                {
                    if (rel.RelationshipName == "responsible_for")
                    {
                        for(int i = 0; i < stations.Count; i++)
                        {
                            if (stations[i] == rel.SourceId)
                            {
                                cbStationsForCustomer.SelectedIndex = i;
                                break;
                            }
                        }
                    }
                }
                twinOpT.currentCustomerId = tbCustomerDtId.Text;
                buttonGetOrders.IsEnabled = true;
            }
            catch (Exception ex)
            {
                ShowLog(ex.Message);
            }
        }

        private async void buttonDeleteOrderProductCustomer_Click(object sender, RoutedEventArgs e)
        {
            var targetCustomerId = twinOpT.currentCustomerId;
            bool isDeleteCustomer = false;
            bool isDeleteAll = false;
            if (!string.IsNullOrEmpty(targetCustomerId))
            {
                if (MessageBox.Show("Delete current Customer?", "Confirmation", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                {
                    isDeleteCustomer = true;
                    if (MessageBox.Show("Delete all existing twins and relationships of order, product and customer?", "Confirmation", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                    {
                        isDeleteAll = true;
                    }
                }
            }
            else
            {
                isDeleteAll = true;
                isDeleteCustomer = true;
            }
            var targetOrderId = twinOpT.currentOrderId;
            twinOpT.Clear();

            try
            {
                if (!string.IsNullOrEmpty(targetCustomerId))
                {
                    await DeleteOrderProductForCustomer(isDeleteCustomer, targetCustomerId, targetOrderId);
                }
                if (isDeleteAll)
                {
                    string query = $"SELECT * FROM DigitalTwins WHERE IS_OF_MODEL('{customerModelId}')";
                    var queryResponse = twinsClient.QueryAsync<BasicDigitalTwin>(query);
                    await foreach (var c in queryResponse)
                    {
                        if (targetCustomerId != c.Id)
                        {
                            await DeleteOrderProductForCustomer(true, c.Id);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ShowLog(ex.Message);
            }
        }

        private async Task DeleteOrderProductForCustomer(bool isDeleteCustomer, string targetCustomerId, string targetOrderId = null)
        {
            var orderForCustomerRels = twinsClient.GetIncomingRelationshipsAsync(targetCustomerId);
            bool hasDeletedAllRels = true;
            await foreach (var oFcRel in orderForCustomerRels)
            {
                if (oFcRel.RelationshipName == "ordered_by")
                {
                    var orderRels = twinsClient.GetRelationshipsAsync<BasicRelationship>(oFcRel.SourceId);
                    await foreach (var oRel in orderRels)
                    {
                        if (targetOrderId == null || (targetOrderId != null && oRel.SourceId == targetOrderId))
                        {
                            if (oRel.Name == "created")
                            {
                                // Target is 'Product'
                                var pRels = twinsClient.GetRelationshipsAsync<BasicRelationship>(oRel.TargetId);
                                await foreach (var pRel in pRels)
                                {
                                    await twinsClient.DeleteRelationshipAsync(pRel.SourceId, pRel.Id);
                                    ShowLog($"Deleted relationship:{pRel.Id}");
                                }
                                var pIRels = twinsClient.GetIncomingRelationshipsAsync(oRel.TargetId);
                                await foreach (var pIRel in pIRels)
                                {
                                    await twinsClient.DeleteRelationshipAsync(pIRel.SourceId, pIRel.RelationshipId);
                                    ShowLog($"Deleted relationship:{pIRel.RelationshipId}");
                                }

                                await twinsClient.DeleteDigitalTwinAsync(oRel.TargetId);
                                ShowLog($"Deleted Product:{oRel.TargetId}");
                            }
                            else if (oRel.Name == "is_assigned_for" || oRel.Name == "ordered_by")
                            {
                                await twinsClient.DeleteRelationshipAsync(oFcRel.SourceId, oRel.Id);
                                ShowLog($"Deleted relationship:{oRel.Id}");
                            }
                            else
                            {
                                hasDeletedAllRels = false;
                            }
                        }
                    }
                    if (targetOrderId == null || (targetOrderId != null && oFcRel.SourceId == targetOrderId))
                    {
                        await twinsClient.DeleteDigitalTwinAsync(oFcRel.SourceId);
                        ShowLog($"Deleted Order:{oFcRel.SourceId}");
                    }
                    else
                    {
                        hasDeletedAllRels = false;
                    }
                }
                else if (oFcRel.RelationshipName == "responsible_for")
                {
                    if (isDeleteCustomer)
                    {
                        await twinsClient.DeleteRelationshipAsync(oFcRel.SourceId, oFcRel.RelationshipId);
                        ShowLog($"Delete relationship:{oFcRel.RelationshipId}");
                    }
                }
            }
            if (isDeleteCustomer)
            {
                if (!hasDeletedAllRels)
                {
                    MessageBox.Show($"Customer:{targetCustomerId} has other orders so this can't be deleted!");
                }
                else
                {
                    await twinsClient.DeleteDigitalTwinAsync(targetCustomerId);
                    ShowLog($"Delete Customer:{targetCustomerId}");
                }
            }
        }

        private async void buttonSetTMD_Click(object sender, RoutedEventArgs e)
        {
            // First, list up products without TMD
            var productRels = twinsClient.GetRelationshipsAsync<BasicRelationship>(twinOpT.currentDestinationStationId, "sort_to");
            var sortingProducts = new List<string>();
            await foreach(var productRel in productRels)
            {
                var tmdOfPRels = twinsClient.GetIncomingRelationshipsAsync(productRel.TargetId);
                bool unassigned = true;
                await foreach(var tmdOfPRel in tmdOfPRels)
                {
                    if (tmdOfPRel.RelationshipName== "target")
                    {
                        unassigned = false;
                        break;
                    }
                }
                if (unassigned)
                {
                    sortingProducts.Add(productRel.TargetId);
                }
            }

            // Select tmd which is equipment of current station and is not used for product.
            var tmdRels = twinsClient.GetRelationshipsAsync<BasicRelationship>(twinOpT.currentDestinationStationId, "equipments");
            await foreach(var tmdRel in tmdRels)
            {
                var pRels = twinsClient.GetRelationshipsAsync<BasicRelationship>(tmdRel.TargetId, "target");
                bool unassigned = true;
                await foreach(var pRel in pRels)
                {
                    unassigned = false;
                    break;
                }
                if (unassigned)
                {
                    var rel = new BasicRelationship()
                    {
                        Name = "target",
                        TargetId = sortingProducts[0],
                        SourceId = tmdRel.TargetId
                    };
                    var relId = $"{rel.SourceId}-${rel.Name}-${rel.TargetId}";
                    await twinsClient.CreateOrReplaceRelationshipAsync(tmdRel.TargetId, relId, rel);
                    ShowLog($"Created relationship - {relId}");
                    sortingProducts.Remove(sortingProducts[0]);
                    if (sortingProducts.Count == 0)
                    {
                        break;
                    }
                }
            }
            if (sortingProducts.Count>0)
            {
                MessageBox.Show("Not enough TMD equipment that meets the condition!");
                return;
            }

            // Find delivery trucks
            dvTrucks.Clear();
            // query = $"SELECT truck FROM digitaltwins station JOIN truck RELATED station.assign_to WHERE station.$dtId = '{currentDestinationStationId}' AND IS_OF_MODEL(station, '{stationModelId}')";
            // queryResponse = twinsClient.QueryAsync<BasicDigitalTwin>(query);
            // await foreach(var truck in queryResponse)
            // {
            //    dvTrucks.Add(((JsonElement)truck.Contents["truck"]).GetProperty("$dtId").GetString());
            // }
            var dvTruckForStationRels = twinsClient.GetRelationshipsAsync<BasicRelationship>(twinOpT.currentDestinationStationId, "assign_to");
            await foreach (var dvtFsRel in dvTruckForStationRels)
            {
                dvTrucks.Add(dvtFsRel.TargetId);
            }
            cbDTruck.IsEnabled = true;
        }

        private async void buttonDTruckBackToStation_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var truckRels = twinsClient.GetIncomingRelationshipsAsync(currentDVTruckId);
                await foreach(var tRel in truckRels)
                {
                    if (tRel.RelationshipName== "assigned_to")
                    {
                        var pRels = twinsClient.GetRelationshipsAsync<BasicRelationship>(tRel.SourceId, "target");
                        bool untargeted = true;
                        await foreach(var pRel in pRels)
                        {
                            untargeted = true;
                            break;
                        }
                        if (untargeted)
                        {
                            await twinsClient.DeleteRelationshipAsync(tRel.SourceId, tRel.RelationshipId);
                            ShowLog($"Deleted relationship - {tRel.RelationshipId}");
                        }
                    }
                }

                buttonDTruckBackToStation.IsEnabled = false;
                buttonCreateCustomer.IsEnabled = true;
                buttonGetCurrentCustomers.IsEnabled = true;
            }
            catch (Exception ex)
            {
                ShowLog(ex.Message);
            }
        }

        private async void buttonConnectToSignalR_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var httpClient = new System.Net.Http.HttpClient();
                var response = await httpClient.PostAsync(tbSignalRInstanceUrl.Text + "/api/SignalRInfo", new System.Net.Http.StringContent(""));
                if (response.StatusCode == System.Net.HttpStatusCode.OK)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    dynamic signalRInfoJson = Newtonsoft.Json.JsonConvert.DeserializeObject(responseContent);
                    string signalRUrl = signalRInfoJson["url"];
                    string accessToken = signalRInfoJson["accessToken"];
                    string hub = signalRInfoJson["hub"];
                    var hubConnection = new HubConnectionBuilder().WithUrl(signalRUrl, (info) =>
                    {
                        info.AccessTokenProvider = () => Task.FromResult(accessToken);
                    }).Build();
                    hubConnection.On<string>("SendData", async (msg) =>
                    {
                        await this.Dispatcher.InvokeAsync(() =>
                        {
                            ShowLog($"Received SignalR Message - {msg}");
                            dynamic json = Newtonsoft.Json.JsonConvert.DeserializeObject(msg);
                            dynamic msgContent = json["message"];
                            if (msgContent != null)
                            {
                                string modelId = msgContent["modelId"];
                                string id = msgContent["Id"];
                                string eventType = msgContent["eventtype"];
                                if (!string.IsNullOrEmpty(eventType))
                                {
                                    if (modelId == orderModelId)
                                    {
                                        var candidates = products.Where(p => { return p.OrderId == id; });
                                        if (eventType == "Microsoft.DigitalTwins.Twin.Create")
                                        {
                                            if (candidates.Count() == 0)
                                            {
                                                products.Add(new OrderAndProductInfo()
                                                {
                                                    OrderId = id
                                                });
                                            }
                                        }
                                        else if (eventType == "Microsoft.DigitalTwins.Twin.Delete")
                                        {
                                            if (candidates.Count() >= 1)
                                            {
                                                products.Remove(candidates.First());
                                            }
                                        }
                                    }
                                    else if (modelId == productModelId)
                                    {
                                        if (eventType == "Microsoft.DigitalTwins.Twin.Create" && msgContent["orderId"] != null)
                                        {
                                            string orderId = msgContent["orderId"];
                                            var candidates = products.Where(p => { return p.OrderId == orderId; });
                                            if (candidates.Count() > 0)
                                            {
                                                candidates.First().ProductId = id;
                                            }
                                        }
                                    }
                                }

                                else
                                {
                                    if (modelId == orderModelId)
                                    {
                                        var orderCandidates = products.Where(o => { return o.OrderId == id; });
                                        if (orderCandidates.Count() > 0)
                                        {
                                            if (msgContent["Status"] != null)
                                            {
                                                orderCandidates.First().OrderStatus = msgContent["Status"];
                                            }
                                        }
                                    }
                                    else if (modelId == productModelId)
                                    {
                                        var productCandidates = products.Where(p => { return p.ProductId == id; });
                                        if (productCandidates.Count() > 0)
                                        {
                                            OrderAndProductInfo target = productCandidates.First();
                                            if (msgContent["Status"] != null)
                                            {
                                                int newStatus = msgContent["Status"];
                                                target.SetProductStatus(newStatus);
                                            }
                                            if (msgContent["Location"] != null)
                                            {
                                                target.ProductLocation = msgContent["Location"];
                                            }
                                            if (msgContent["Temperature"] != null)
                                            {
                                                target.ProductTemperature = msgContent["Temperature"];
                                            }
                                        }
                                    }
                                }
                            }
                        });
                    });
                    ShowLog("SignalR Subscribed.");

                    var query = $"SELECT * FROM DigitalTwins WHERE IS_OF_MODEL('{orderModelId}')";
                    var queryResponse = twinsClient.QueryAsync<BasicDigitalTwin>(query);
                    products.Clear();
                    await foreach(var o in queryResponse)
                    {
                        var oapInfo = new OrderAndProductInfo()
                        {
                            OrderId = o.Id
                        };
                        oapInfo.OrderStatus = ((JsonElement)o.Contents["Status"]).GetString();
                        var pRels = twinsClient.GetRelationshipsAsync<BasicRelationship>(o.Id, "created");
                        await foreach (var p in pRels)
                        {
                            oapInfo.ProductId = p.TargetId;
                            query = $"SELECT * FROM DigitalTwins WHERE IS_OF_MODEL('{productModelId}') AND $dtId='{p.TargetId}'";
                            var queryProducts = twinsClient.QueryAsync<BasicDigitalTwin>(query);
                            await foreach (var pTwin in queryProducts)
                            {
                                oapInfo.SetProductStatus(((JsonElement)pTwin.Contents["Status"]).GetInt32());
                                if (pTwin.Contents.ContainsKey("Location"))
                                {
                                    oapInfo.ProductLocation = ((JsonElement)pTwin.Contents["Location"]).GetString();
                                }
                                break;
                            }
                            break;
                        }
                        products.Add(oapInfo);
                    }

                    await hubConnection.StartAsync();
                    ShowLog("SignalR message receiving started.");
                    buttonConnectToSignalR.IsEnabled = false;
                }
            }
            catch (Exception ex)
            {
                ShowLog(ex.Message);
            }
        }

        private async void buttonGetOrders_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(twinOpT.currentCustomerId))
            {
                return;
            }
            try
            {
                ordersByCustomer.Clear();
                var orderRels = twinsClient.GetIncomingRelationshipsAsync(twinOpT.currentCustomerId);
                await foreach (var oRel in orderRels)
                {
                    if (oRel.RelationshipName == "ordered_by")
                    {
                        ordersByCustomer.Add(oRel.SourceId);
                    }
                }
                cbOrders.IsEnabled = true;
            }
            catch (Exception ex)
            {
                ShowLog(ex.Message);
            }
        }

        private async void cbOrders_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cbOrders.SelectedItem == null)
            {
                return;
            }
            try
            {
                twinOpT.currentOrderId = (string)cbOrders.SelectedItem;
                var query= $"SELECT * FROM DigitalTwins WHERE IS_OF_MODEL('{orderModelId}') AND $dtId='{twinOpT.currentOrderId}'";
                var queryResponse = twinsClient.QueryAsync<BasicDigitalTwin>(query);
                BasicDigitalTwin orderTwin = null;
                await foreach (var o in queryResponse)
                {
                    orderTwin = o;
                    break;
                }
                if (orderTwin != null)
                {
                    tbOrderDtId.Text = twinOpT.currentOrderId;
                    tbOrderId.Text = ((JsonElement)orderTwin.Contents["OrderId"]).GetString();
                    tbOrderStatus.Text = ((JsonElement)orderTwin.Contents["Status"]).GetString();
                    var relsOfOrder = twinsClient.GetRelationshipsAsync<BasicRelationship>(twinOpT.currentOrderId);
                    await foreach (var rel in relsOfOrder)
                    {
                        switch (rel.Name)
                        {
                            case "created":
                                twinOpT.currentProductId = rel.TargetId;
                                break;
                            case "is_assigned_for":
                                twinOpT.currentFactoryId = rel.TargetId;
                                break;
                        }
                    }
                    MessageBox.Show("Current implementation support only 'created' product step");
                }
            }
            catch (Exception ex)
            {
                ShowLog(ex.Message);
            }
        }

        private async void lbProducts_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (lbProducts.SelectedItem != null)
            {
                if (MessageBox.Show("Use this order for step by step try?", "Confirmation", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                {
                    var oapInfo = (OrderAndProductInfo)lbProducts.SelectedItem;
                    if (string.IsNullOrEmpty(tbCustomerDtId.Text))
                    {
                        var cRels = twinsClient.GetRelationshipsAsync<BasicRelationship>(oapInfo.OrderId, "ordered_by");
                        BasicDigitalTwin customerTwin = null;
                        await foreach (var cRel in cRels)
                        {
                            var cTwinResponse = await twinsClient.GetDigitalTwinAsync<BasicDigitalTwin>(cRel.TargetId);
                            if (cTwinResponse.GetRawResponse().Status == 200)
                            {
                                customerTwin = cTwinResponse.Value;
                            }
                            break;
                        }
                        if (customerTwin != null)
                        {
                            tbCustomerDtId.Text = customerTwin.Id;
                            tbCustomerId.Text = ((JsonElement)customerTwin.Contents["CustomerId"]).GetString();
                            tbCustomerName.Text = ((JsonElement)customerTwin.Contents["Name"]).GetString();
                            tbCustomerAddress.Text = ((JsonElement)customerTwin.Contents["Address"]).GetString();
                            tbCustomerTelNo.Text = ((JsonElement)customerTwin.Contents["TelNo"]).GetString();
                        }
                    }
                    else
                    {
                        MessageBox.Show("Customer's Id is not empty!");
                    }
                    var oTwinResponse = await twinsClient.GetDigitalTwinAsync<BasicDigitalTwin>(oapInfo.OrderId);
                    if (oTwinResponse.GetRawResponse().Status==200)
                    {
                        var oTwin = oTwinResponse.Value;
                        tbOrderDtId.Text = oTwin.Id;
                        tbOrderId.Text = ((JsonElement)oTwin.Contents["OrderId"]).GetString();
                        tbOrderStatus.Text = ((JsonElement)oTwin.Contents["Status"]).GetString();
                    }
                }
            }
        }
    }

    class TwinOperationThread
    {
        public string currentCustomerId { get; set; }
        public string currentOrderId { get; set; }
        public string currentFactoryId { get; set; }
        public string currentProductId { get; set; }
        public string currentCCTruckId { get; set; }
        public string currentDestinationStationId { get; set; }

        List<TextBox> textboxes = new List<TextBox>();
        public List<TextBox> TextBoxes { get { return textboxes; } }
        public void Clear()
        {
            currentCustomerId = null;
            currentOrderId = null;
            currentFactoryId = null;
            currentProductId = null;
            currentCCTruckId = null;
            currentDestinationStationId = null;
            foreach (var tb in textboxes)
            {
                tb.Text = "";
            }
        }

    }
}
