using System;
using System.Collections.Generic;
using System.Text;

namespace WpfAppTruckSimulator
{
    public class CoolingContainerTruckInfo : TruckInfo
    {
        public static readonly string CoolingContainerTruckModelId = "dtmi:embeddedgeorge:transport:const_temperature:sample:CoolingContainerTruck;1";
        public double Temperature { get; set; }

        public enum CCTruckStatus
        {
            Ready = 0,
            DriveToFactory = 1,
            AtFactory = 2,
            DriveToStation = 3,
            AtStation = 4,
            InAccident = 5
        }
        public CCTruckStatus Status { get; set; }
    }
}
