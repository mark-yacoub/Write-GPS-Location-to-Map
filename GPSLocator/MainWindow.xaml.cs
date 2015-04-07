using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using GMap.NET;
using GMap.NET.MapProviders;
using GMap.NET.WindowsPresentation;

namespace GPSLocator
{
    public partial class MainWindow
    {
        // timer to read from the latest position and update UI
        // Serial Interrupt can not access the UI as it's in a different thread
        // a timer avoid blocking the UI
        readonly DispatcherTimer m_uiTimer = new DispatcherTimer();
        private SerialProtocol m_serialProt;

        public MainWindow()
        {
            InitializeComponent();
            InitMap();
            CreateComPortsButtons();
        }

        // Create the map on the screen with all its features
        private void InitMap()
        {
            // choose Google map, can use Bing too or others
            mx_map.MapProvider = GoogleMapProvider.Instance;
            // Used Cached data, beside newly downloaded ones.
            GMaps.Instance.Mode = AccessMode.ServerAndCache;
            mx_map.TouchEnabled = true;

            // use a random place on earth to see the map changing after the first reading
            mx_map.Position = new PointLatLng(-79.894227, 36.562499);
            mx_map.MinZoom = 1;
            mx_map.MaxZoom = 20;
            // Current default Zoom
            mx_map.Zoom = 2;
            // Fill the empty tiles until it loads a more clear image
            mx_map.FillEmptyTiles = true;
            mx_map.ShowCenter = false;

            // Add a marker on the position on the map
            // Throw to Antarctica to ensure that the first reading will actually move it somewhere
            AddNewMarker(-79.894227, 36.562499);
        }

        // Creating the port button the screen, depending on the available port on the computer and the user chooses which port he's expecting
        // to read from. Device Manager/ Ports/ should tell you what you should pick
        private void CreateComPortsButtons()
        {
            // Get the ports from the Device Manager
            string[] ports = SerialProtocol.GetAvailablePorts();
            foreach (string port in ports)
            {
                // Create a button for each port
                var radioButton = new RadioButton();
                radioButton.Content = port;
                radioButton.Margin = new Thickness(10, 0, 10, 0);

                radioButton.Checked += RadioButtonOnChecked;
                radioButton.GroupName = "Ports";
                radioButton.IsChecked = false;

                // Add it to the UI
                mx_comPortsPanels.Children.Add(radioButton);
            }
        }

        // Create the image that serves as a marker on certain point on the map
        void AddNewMarker(double latitude, double longtitude)
        {
            // Create the marker on a certain point
            mx_map.Markers.Add(new GMapMarker(new PointLatLng(latitude, longtitude))
            {
                // the UI Element of the marker shape is a button
                Shape = new Button()
                {
                    // enclose an image inside the button
                    // a better photoshoped image could be used, this was used for evaluation purposes only
                    Content = new Image { Source = new BitmapImage(new Uri("marker.png", UriKind.Relative)) },
                    Background = Brushes.Transparent,
                    BorderBrush = Brushes.Transparent,
                    Width = 30,
                    IsHitTestVisible = false
                }
            });
            // position the map to the new point marked
            mx_map.Position = new PointLatLng(latitude, longtitude);
        }

        // call back function when the port desired is selected
        private void RadioButtonOnChecked(object sender, RoutedEventArgs e)
        {
            RadioButton radioButton = sender as RadioButton;
            // remove the buttons from the screen
            // TODO: Delete those buttons from memory if not needed at all
            mx_comPortsPanels.Visibility = Visibility.Collapsed;

            if (radioButton != null)
            {
                // open the serial port with the COM selected 
                m_serialProt = new SerialProtocol(radioButton.Content.ToString());

                // create the timer that updates the location of the marker
                m_uiTimer.Tick += (o, args) => UpdateMarkerOnTheMap();
                // Set the timer interval in MS
                m_uiTimer.Interval = new TimeSpan(0,0,0,0,900);
                m_uiTimer.Start();
            }
        }

        // add the new marker on the screen to the user
        private void UpdateMarkerOnTheMap()
        {
            // get the latest string received through the serial port
            string gpgllString = m_serialProt.FetchLatestReceivedString();

            if (!string.IsNullOrWhiteSpace(gpgllString))
            {
                string[] msgComponents = gpgllString.Split('$');
                if (msgComponents.Count() >= 2)
                {
                    // get the first point on the last read string
                    // TODO: do more checks, including Checksum to ensure the validity of the string

                    //expected message in GPGLL format, as: $GPGLL,DDMM.MMMMM,S,DDDMM.MMMMM,S,HHMMSS.SS,S*CC<CR><LF>
                    string[] dividedStrings = msgComponents[1].Split(',');
                    if (dividedStrings.Count() == 8)
                    {
                        PointLatLng coordPoint = UpdateMarkerCoordinates(dividedStrings);
                        // clear existing markers
                        mx_map.Markers.Clear();
                        AddNewMarker(coordPoint.Lat, coordPoint.Lng);
                        mx_map.ReloadMap();
                    }
                }
            }
        }

        private PointLatLng UpdateMarkerCoordinates(string[] msgComponents)
        {
            // more information about the format is found here: http://www.gpsinformation.org/dale/nmea.htm
            double latitude = Convert.ToDouble(msgComponents[1]);
            double longitude = Convert.ToDouble(msgComponents[3]);

            double latDegrees = Math.Floor(latitude / 100);
            double latMin = latitude - latDegrees * 100;
            double lat = latDegrees + (latMin / 60);
            if (msgComponents[2] == "S")
                lat *= -1;

            double longDegrees = Math.Floor(longitude / 100);
            double longMin = longitude - longDegrees * 100;
            double lng = longDegrees + (longMin / 60);
            if (msgComponents[4] == "W")
                lng *= -1;

            return new PointLatLng(lat, lng);
        }
    }
}
