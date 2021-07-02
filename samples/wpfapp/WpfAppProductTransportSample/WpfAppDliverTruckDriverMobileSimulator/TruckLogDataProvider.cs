using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace WpfAppTruckSimulator
{
    abstract class TruckLogDataProvider: IDisposable
    {
        protected string logFile;
        private FileStream fileStream = null;
        protected StreamReader reader = null;
        protected TimeSpan deltaTimestamp;
        protected Dictionary<string, int> columnIndex = new Dictionary<string, int>();
        protected List<string> columnKeys = new List<string>();
        protected bool validated = false;

        public TruckLogDataProvider(string csvFileName, string[] keys)
        {
            logFile = csvFileName;
            foreach(var k in keys)
            {
                columnKeys.Add(k);
            }
        }

        public void Dispose()
        {
            if (reader != null)
            {
                reader.Dispose();
                fileStream.Dispose();
            }
        }

        public void Parse()
        {
            fileStream = File.OpenRead(logFile);
            reader = new StreamReader(fileStream);
            var topLineColumns = reader.ReadLine().Split(",");
            if (topLineColumns.Length != columnKeys.Count)
            {
                throw new ArgumentOutOfRangeException();
            }
            foreach(var key in columnKeys)
            {
                for(int i = 0; i < topLineColumns.Length; i++)
                {
                    if (topLineColumns[i] == key)
                    {
                        columnIndex.Add(key, i);
                        break;
                    }
                }
            }
            if (columnIndex.Count != columnKeys.Count)
            {
                throw new ArgumentOutOfRangeException();
            }
            validated = true;
        }
    }
}
