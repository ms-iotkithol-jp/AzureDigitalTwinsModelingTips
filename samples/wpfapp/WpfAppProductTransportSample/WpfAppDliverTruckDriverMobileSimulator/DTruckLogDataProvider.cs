using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace WpfAppTruckSimulator
{
    class DTruckLogDataProvider : TruckLogDataProvider, ITelemetryDataProvider
    {
        private static readonly string columnKeyLongitude = "longitude";
        private static readonly string columnKeyLatitude = "latitude";
        private static readonly string columnKeyAttitude = "attitude";
        public DTruckLogDataProvider(string csvFileName) : 
            base(csvFileName, new string[]{ columnKeyAttitude,columnKeyLatitude,columnKeyLongitude})
        {
        }

        public string GetNextMessage(object context, int status)
        {
            string msg = null;
            if (validated)
            {
                if (!reader.EndOfStream)
                {
                    var columns = reader.ReadLine().Split(",");
                    var content = new
                    {
                        temperatureMeasurementDevices = context,
                        location = new
                        {
                            longitude = double.Parse(columns[columnIndex[columnKeyLongitude]]),
                            latitude = double.Parse(columns[columnIndex[columnKeyLatitude]]),
                            attitude = double.Parse(columns[columnIndex[columnKeyAttitude]])
                        },
                        status = status,
                        timestamp = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
                    };
                    msg = Newtonsoft.Json.JsonConvert.SerializeObject(content);
                }
            }
            return msg;
        }
    }
}
