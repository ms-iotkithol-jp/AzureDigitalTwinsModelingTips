using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Controls;

namespace WpfAppTruckSimulator
{
    class DTruckPaneDataProvider: TruckPaneDataProvider, ITelemetryDataProvider
    {
        public DTruckPaneDataProvider(TextBox attitude, TextBox latitude, TextBox longitude):
            base (attitude,latitude,longitude,null)
        {

        }

        public string GetNextMessage(object context, int status)
        {
            var sendContent = new
            {
                temperatureMeasurementDevices = context,
                location = new
                {
                    longitude = double.Parse(tbLongitude.Text),
                    latitude = double.Parse(tbLatitude.Text),
                    attitude = double.Parse(tbAttitude.Text)
                },
                status = status,
                timestamp = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
            };
            return Newtonsoft.Json.JsonConvert.SerializeObject(sendContent);
        }
    }
}
