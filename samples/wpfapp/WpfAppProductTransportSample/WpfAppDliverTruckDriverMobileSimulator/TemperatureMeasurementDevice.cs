using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using System.Windows.Threading;

namespace WpfAppTruckSimulator
{
    public class TemperatureMeasurementDevice : INotifyPropertyChanged
    {
        private double externalTemperature = 30;
        private double riseRate = 0.01;
        private double batteryLevelDelta = 0.001;
        Random random;

        public TemperatureMeasurementDevice()
        {
            random = new Random(DateTime.Now.Millisecond);
        }

        private string id;
        private double temperature;
        private double batteryLevel;
        private DateTime timestamp;

        public string Id
        {
            get { return id; }
            set
            {
                id = value;
                OnPropertyChanged(nameof(Id));
            }
        }
        public double Temperature
        {
            get { return temperature; }
            set
            {
                temperature = value;
                OnPropertyChanged(nameof(Temperature));
            }
        }
        public double BatteryLevel
        {
            get { return batteryLevel; }
            set
            {
                batteryLevel = value;
                OnPropertyChanged(nameof(BatteryLevel));
            }
        }
        public DateTime Timestamp
        {
            get { return timestamp; }
            set
            {
                timestamp = value;
                OnPropertyChanged(nameof(Timestamp));
            }
        }

        public double ExternalTemperatre
        {
            get { return externalTemperature; }
            set { externalTemperature = value; }
        }
        public double RiseRate
        {
            get { return riseRate; }
            set { riseRate = value; }
        }
        public double BatteryLevelDelta
        {
            get { return batteryLevelDelta; }
            set { batteryLevelDelta = value; }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged(string propertyName)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
            }

        }

        public void StartAutomaticTimestampUpdate(Dispatcher owner, int intervalSec)
        {
            if (timer == null)
            {
                timer = new DispatcherTimer();
                timer.Interval = TimeSpan.FromSeconds(intervalSec);
                timer.Tick += (s, e) =>
                 {
                     owner.Invoke(() =>
                     {
                         Timestamp = DateTime.Now;
                         if (riseRate > 0)
                         {
                             var delta = (externalTemperature - temperature) * riseRate;
                             Temperature = temperature + delta * (1 + 0.002 * (random.NextDouble() - 0.5));
                         }
                         if (batteryLevelDelta > 0)
                         {
                             BatteryLevel = batteryLevel + batteryLevelDelta * (1 + 0.002 * (random.NextDouble() - 0.5));
                         }
                     });
                 };
                timer.Start();
            }
        }

        public void StopAutomaticTimestampUpdate()
        {
            if (timer != null)
            {
                timer.Stop();
            }
        }

        DispatcherTimer timer = null;
    }
}
