using System;
using System.Collections.Generic;
using System.Text;

namespace WpfAppTruckSimulator
{
    public class DeliveryTruckInfo : TruckInfo
    {
        public static readonly string DeliveryTruckModelId = "dtmi:embeddedgeorge:transport:const_temperature:sample:DeliveryTruck;1";

        public enum DTruckStatus
        {
            AtStation = 0,
            DeliveringToCustomers = 1,
            DriveToStation = 2,
            InAccident = 3
        }
        public DTruckStatus Status { get; set; }
    }
}
