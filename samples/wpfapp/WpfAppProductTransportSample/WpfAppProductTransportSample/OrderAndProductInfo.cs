using System;
using System.Collections.Generic;
using System.Text;

namespace WpfAppProductTransportSample
{
    public class OrderAndProductInfo
    {
        public string OrderId { get; set; }
        public string OrderStatus { get; set; }
        public string ProductId { get; set; }
        public double ProductTemperature { get; set; }
        public string ProductStatus { get; set; }
        public string ProductLocation { get; set; }

        public string SetProductStatus(int newStatus)
        {
            string newProductStatus = "Unknown";
            switch (newStatus)
            {
                case 0:
                    newProductStatus = "Proper";
                    break;
                case 1:
                    newProductStatus = "NearLowLimit";
                    break;
                case 2:
                    newProductStatus = "NearHighLimit";
                    break;
                case 3:
                    newProductStatus = "Hopeless";
                    break;
                default:
                    newProductStatus = "Unknown";
                    break;
            }
            ProductStatus = newProductStatus;
            return newProductStatus;
        }
    }
}
