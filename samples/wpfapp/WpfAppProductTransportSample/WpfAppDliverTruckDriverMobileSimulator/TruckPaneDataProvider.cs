using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Controls;

namespace WpfAppTruckSimulator
{
    abstract class TruckPaneDataProvider : IDisposable
    {
        protected TextBox tbAttitude;
        protected TextBox tbLatitude;
        protected TextBox tbLongitude;
        protected TextBox tbTemperature;

        public TruckPaneDataProvider(TextBox attitude, TextBox latitude, TextBox longitude, TextBox temperature)
        {
            tbAttitude = attitude;
            tbLatitude = latitude;
            tbLongitude = longitude;
            tbTemperature = temperature;
        }

        public void Dispose()
        {
            // Do nothing
        }
    }
}
