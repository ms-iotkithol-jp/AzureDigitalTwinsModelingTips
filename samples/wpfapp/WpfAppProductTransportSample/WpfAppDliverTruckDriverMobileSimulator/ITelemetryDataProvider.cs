using System;
using System.Collections.Generic;
using System.Text;

namespace WpfAppTruckSimulator
{
    interface ITelemetryDataProvider : IDisposable
    {
        string GetNextMessage(object context, int status);
    }
}
