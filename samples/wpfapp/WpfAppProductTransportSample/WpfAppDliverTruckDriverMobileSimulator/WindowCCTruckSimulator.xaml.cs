using Azure.DigitalTwins.Core;
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
using System.Windows.Shapes;

namespace WpfAppTruckSimulator
{
    /// <summary>
    /// WindowCCTruckSimulator.xaml の相互作用ロジック
    /// </summary>
    public partial class WindowCCTruckSimulator : Window
    {
        public  BasicDigitalTwin Target { get; set; }
        public string IotHubDeviceConnectionString { get; set; }
        public WindowCCTruckSimulator()
        {
            InitializeComponent();
        }
    }
}
