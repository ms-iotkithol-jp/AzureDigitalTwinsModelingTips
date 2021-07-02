using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Controls;

namespace WpfAppTruckSimulator
{
    class CCTruckPaneDataProvider : TruckPaneDataProvider, ITelemetryDataProvider
    {
        public CCTruckPaneDataProvider(TextBox attitude, TextBox latitude, TextBox longitude, TextBox temperature) :
            base(attitude, latitude, longitude, temperature)
        {
        }

        public string GetNextMessage(object context, int status)
        {
            var content = new
            {
                location = new
                {
                    longitude = double.Parse(tbLongitude.Text),
                    latitude = double.Parse(tbLatitude.Text),
                    attitude = double.Parse(tbAttitude.Text)
                },
                container_temperature = double.Parse(tbTemperature.Text),
                status = status,
                timestamp = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
            };
            return Newtonsoft.Json.JsonConvert.SerializeObject(content);
        }
    }
}
