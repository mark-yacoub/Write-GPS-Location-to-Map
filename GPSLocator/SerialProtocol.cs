using System;
using System.Diagnostics;
using System.IO.Ports;
using System.Threading;

namespace GPSLocator
{
    public class SerialProtocol
    {
        private readonly SerialPort m_serialPort;
        // string holding the last string sent by the GPS
        private string m_gpsStringReceived;
        private Mutex m_stringMutex = new Mutex();
        
        // Start the Serial Protocol 
        public SerialProtocol(string portName)
        {
            m_serialPort = new SerialPort(portName);
            m_serialPort.BaudRate = 115200;
            m_serialPort.DataBits = 8;
            m_serialPort.Handshake = Handshake.None;
            m_serialPort.Parity = Parity.None;
            m_serialPort.StopBits = StopBits.One;
            m_serialPort.ReadTimeout = 500;
            m_serialPort.DataReceived += SerialPort_DataReceived;

            try
            {
                if (!m_serialPort.IsOpen)
                    m_serialPort.Open();
            }
            catch (Exception exception)
            {
                Debug.WriteLine("Serial Port failed to open: " + exception);
            }
        }

        // Interrupt when any data is received through the Serial Port
        private void SerialPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            // Use Mutex to avoid writing on the string while it's being read
            m_stringMutex.WaitOne();
            {
                // Save and overwrite the last data received. Only the latest data is needed
                m_gpsStringReceived += m_serialPort.ReadExisting();
            }
            m_stringMutex.ReleaseMutex();
        }

        // Get the latest saved 
        public string FetchLatestReceivedString()
        {
            string latestString;
            // use Mutex to avoid reading the string while it's being written
            m_stringMutex.WaitOne();
            {
                latestString =  m_gpsStringReceived;
                m_gpsStringReceived = string.Empty;
            }
            m_stringMutex.ReleaseMutex();
            return latestString;
        }

        // Get the list of the available ports on device
        public static string[] GetAvailablePorts()
        {
            return SerialPort.GetPortNames();
        }

    }
}
