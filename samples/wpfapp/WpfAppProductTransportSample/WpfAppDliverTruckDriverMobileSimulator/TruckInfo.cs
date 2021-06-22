using System;
using System.Collections.Generic;
using System.Text;

namespace WpfAppTruckSimulator
{
    public abstract class TruckInfo
    {
        public static readonly string TruckModelId = "dtmi:embeddedgeorge:transport:const_temperature:sample:Truck;1";
        public string Id { get; set; }
        public string DeviceId { get; set; }
        public string ConnectionString { get; set; }
        public double Longitude { get; set; }
        public double Latitude { get; set; }
        public double Attitude { get; set; }

        public DateTime Timestamp { get; set; }
    }
}
