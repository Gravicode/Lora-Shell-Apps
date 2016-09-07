using System;
using System.Collections;
using System.Threading;
using Microsoft.SPOT;
using Microsoft.SPOT.Presentation;
using Microsoft.SPOT.Presentation.Controls;
using Microsoft.SPOT.Presentation.Media;
using Microsoft.SPOT.Presentation.Shapes;
using Microsoft.SPOT.Touch;

using Gadgeteer.Networking;
using GT = Gadgeteer;
using GTM = Gadgeteer.Modules;
using Gadgeteer.Modules.GHIElectronics;
using GHI.Processor;
using Microsoft.SPOT.Hardware;
using GHI.Glide;
using System.Text;
using GHI.Glide.Geom;
using Json.NETMF;

using uPLibrary.Networking.M2Mqtt;
using uPLibrary.Networking.M2Mqtt.Messages;
using System.Net;
using Microsoft.SPOT.Net.NetworkInformation;
using System.Net.Sockets;

namespace LoraReceiver
{
    public partial class Program
    {
        //mqtt
        
        public static MqttClient client { set; get; }
        const string MQTT_BROKER_ADDRESS ="13.76.210.96";// "iothub.id";

        //lora init
        private static SimpleSerial _loraSerial;
        private static string[] _dataInLora;
        //lora reset pin
        static OutputPort _restPort = new OutputPort(GHI.Pins.FEZSpiderII.Socket11.Pin6, true);
        private static string rx;
        //UI
        GHI.Glide.UI.TextBlock txtTime = null;
        GHI.Glide.UI.TextBox txtMessage = null;
        GHI.Glide.Display.Window window = null;
        // This method is run when the mainboard is powered up or reset.   
        void ProgramStarted()
        {
            //set display
            this.videoOut.SetDisplayConfiguration(VideoOut.Resolution.Rca320x240);
            //set glide
            window = GlideLoader.LoadWindow(Resources.GetString(Resources.StringResources.Form1));

            txtTime = (GHI.Glide.UI.TextBlock)window.GetChildByName("txtTime");
            txtMessage = (GHI.Glide.UI.TextBox)window.GetChildByName("txtMessage");
            Glide.MainWindow = window;

            //reset lora
            _restPort.Write(false);
            Thread.Sleep(1000);
            _restPort.Write(true);
            Thread.Sleep(1000);


            _loraSerial = new SimpleSerial(GHI.Pins.FEZSpiderII.Socket11.SerialPortName, 57600);
            _loraSerial.Open();
            _loraSerial.DataReceived += _loraSerial_DataReceived;
            //get version
            _loraSerial.WriteLine("sys get ver");
            Thread.Sleep(1000);
            //pause join
            _loraSerial.WriteLine("mac pause");
            Thread.Sleep(1500);
            //antena power
            _loraSerial.WriteLine("radio set pwr 14");
            Thread.Sleep(1500);
            //set device to receive
            _loraSerial.WriteLine("radio rx 0"); //set module to RX
            txtMessage.Text = "LORA-RN2483 setup has been completed...";
            txtMessage.Invalidate();


            //connect wifi
            SetupNetwork();

            //sync time
            var result = Waktu.UpdateTimeFromNtpServer("time.nist.gov", 7);  // Eastern Daylight Time
            Debug.Print(result ? "Time successfully updated" : "Time not updated");
            txtMessage.Text = "Sync Time:"+DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss");
            txtMessage.Invalidate();

        }
        //convert hex to string
        string HexStringToString(string hexString)
        {
            if (hexString == null || (hexString.Length & 1) == 1)
            {
                throw new ArgumentException();
            }
            var sb = new StringBuilder();
            for (var i = 0; i < hexString.Length; i += 2)
            {
                var hexChar = hexString.Substring(i, 2);
                sb.Append((char)Convert.ToByte(hexChar));
            }
            return sb.ToString();
        }
        //convert hex to ascii
        private string HexString2Ascii(string hexString)
        {
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i <= hexString.Length - 2; i += 2)
            {
                int x = Int32.Parse(hexString.Substring(i, 2));
                sb.Append(new string(new char[] { (char)x }));
            }
            return sb.ToString();
        }
        //lora data received
        void _loraSerial_DataReceived(object sender, System.IO.Ports.SerialDataReceivedEventArgs e)
        {
            _dataInLora = _loraSerial.Deserialize();
            for (int index = 0; index < _dataInLora.Length; index++)
            {
                rx = _dataInLora[index];
                //if error
                if (_dataInLora[index].Length > 7)
                {
                    if (rx.Substring(0, 9) == "radio_err")
                    {
                        Debug.Print("!!!!!!!!!!!!! Radio Error !!!!!!!!!!!!!!");
                        PrintToLCD("Radio Error");

                        _restPort.Write(false);
                        Thread.Sleep(1000);
                        _restPort.Write(true);
                        Thread.Sleep(1000);
                        _loraSerial.WriteLine("mac pause");
                        Thread.Sleep(1000);
                        _loraSerial.WriteLine("radio rx 0");
                        return;

                    }
                    //if receive data
                    if (rx.Substring(0, 8) == "radio_rx")
                    {
                        string hex = _dataInLora[index].Substring(10);

                        Mainboard.SetDebugLED(true);
                        Thread.Sleep(500);
                        Mainboard.SetDebugLED(false);

                        Debug.Print(hex);
                        Debug.Print(Unpack(hex));
                        //update display
                        
                        PrintToLCD(Unpack(hex));
                        Thread.Sleep(100);
                        // set module to RX
                        _loraSerial.WriteLine("radio rx 0");
                    }
                }
            }

        }
        //extract hex to string
        public static string Unpack(string input)
        {
            byte[] b = new byte[input.Length / 2];

            for (int i = 0; i < input.Length; i += 2)
            {
                b[i / 2] = (byte)((FromHex(input[i]) << 4) | FromHex(input[i + 1]));
            }
            return new string(Encoding.UTF8.GetChars(b));
        }
        public static int FromHex(char digit)
        {
            if ('0' <= digit && digit <= '9')
            {
                return (int)(digit - '0');
            }

            if ('a' <= digit && digit <= 'f')
                return (int)(digit - 'a' + 10);

            if ('A' <= digit && digit <= 'F')
                return (int)(digit - 'A' + 10);

            throw new ArgumentException("digit");
        }
        void PrintToLCD(string message)
        {
            //cek message
            if (message != null && message.Length > 0)
            {
                try
                {
                    //update display
                    txtTime.Text = DateTime.Now.ToString("dd/MMM/yyyy HH:mm:sss");
                    txtMessage.Text = message;
                    txtTime.Invalidate();
                    txtMessage.Invalidate();
                    window.Invalidate();
                    KirimPesan(message);
                }
                catch (Exception ex)
                {
                    txtMessage.Text = message + "_" + ex.Message + "_" + ex.StackTrace;
                    txtMessage.Invalidate();
                }
            }

        }

        void SetupNetwork()
        {
            string SSID = "wifi berbayar";
            string KeyWifi = "123qweasd";
            //NetworkChange.NetworkAvailabilityChanged += NetworkChange_NetworkAvailabilityChanged;
            //NetworkChange.NetworkAddressChanged += NetworkChange_NetworkAddressChanged;

            wifiRS21.NetworkInterface.Open();
            wifiRS21.NetworkInterface.EnableDhcp();
            wifiRS21.NetworkInterface.EnableDynamicDns();
            wifiRS21.NetworkInterface.Join(SSID, KeyWifi);

            txtMessage.Text = "Waiting for DHCP...";
            txtMessage.Invalidate();
            while (wifiRS21.NetworkInterface.IPAddress == "0.0.0.0")
            {
                Debug.Print("Waiting for DHCP");
                Thread.Sleep(250);
            }
            ListNetworkInterfaces();
            //The network is now ready to use.

            //set RTC
            //DateTime DT = new DateTime(2016, 4, 18, 13, 13, 50); // This will set a time for the Real-time Clock clock to 1:01:01 on 1/1/2014
            //RealTimeClock.SetDateTime(DT); //This will set the hardware Real-time Clock to what is in DT

            //hive mq
            client = new MqttClient(IPAddress.Parse( MQTT_BROKER_ADDRESS));

            string clientId = Guid.NewGuid().ToString();
            client.Connect(clientId, "mifmasterz", "123qweasd");
            SubscribeMessage();
        }
        void KirimPesan(string Message)
        {
            var node = new MqttData() { Tanggal=DateTime.Now, Pesan=Message };
            var JsonStr = Json.NETMF.JsonSerializer.SerializeObject(node);
            client.Publish("mifmasterz/lora/data", Encoding.UTF8.GetBytes(JsonStr), MqttMsgBase.QOS_LEVEL_AT_MOST_ONCE, false);
        }

        static void SubscribeMessage()
        {
            // register to message received 
            client.MqttMsgPublishReceived += client_MqttMsgPublishReceived;

            client.Subscribe(new string[] { "mifmasterz/lora/data", "mifmasterz/lora/control" }, new byte[] { MqttMsgBase.QOS_LEVEL_AT_MOST_ONCE, MqttMsgBase.QOS_LEVEL_AT_MOST_ONCE });

        }

        static void client_MqttMsgPublishReceived(object sender, MqttMsgPublishEventArgs e)
        {
            // handle message received 
            Debug.Print("Message : " + new string(Encoding.UTF8.GetChars(e.Message)));
        }


        void ListNetworkInterfaces()
        {
            var settings = wifiRS21.NetworkSettings;

            Debug.Print("------------------------------------------------");
            Debug.Print("MAC: " + ByteExt.ToHexString(settings.PhysicalAddress, "-"));
            Debug.Print("IP Address:   " + settings.IPAddress);
            Debug.Print("DHCP Enabled: " + settings.IsDhcpEnabled);
            Debug.Print("Subnet Mask:  " + settings.SubnetMask);
            Debug.Print("Gateway:      " + settings.GatewayAddress);
            Debug.Print("------------------------------------------------");

            txtMessage.Text = "IP:" + settings.IPAddress;
            txtMessage.Invalidate();
        }


        private static void NetworkChange_NetworkAddressChanged(object sender, Microsoft.SPOT.EventArgs e)
        {
            Debug.Print("Network address changed");
        }

        private static void NetworkChange_NetworkAvailabilityChanged(object sender, NetworkAvailabilityEventArgs e)
        {
            Debug.Print("Network availability: " + e.IsAvailable.ToString());
        }

       

    }

    public class MqttData
    {
        public DateTime Tanggal { set; get; }
        public string Pesan { set; get; }
    }
    public static class ByteExt
    {
        private static char[] _hexCharacterTable = new char[] { '0', '1', '2', '3', '4', '5', '6', '7', '8', '9', 'A', 'B', 'C', 'D', 'E', 'F' };

#if MF_FRAMEWORK_VERSION_V4_1
    public static string ToHexString(byte[] array, string delimiter = "-")
#else
        public static string ToHexString(this byte[] array, string delimiter = "-")
#endif
        {
            if (array.Length > 0)
            {
                // it's faster to concatenate inside a char array than to
                // use string concatenation
                char[] delimeterArray = delimiter.ToCharArray();
                char[] chars = new char[array.Length * 2 + delimeterArray.Length * (array.Length - 1)];

                int j = 0;
                for (int i = 0; i < array.Length; i++)
                {
                    chars[j++] = (char)_hexCharacterTable[(array[i] & 0xF0) >> 4];
                    chars[j++] = (char)_hexCharacterTable[array[i] & 0x0F];

                    if (i != array.Length - 1)
                    {
                        foreach (char c in delimeterArray)
                        {
                            chars[j++] = c;
                        }

                    }
                }

                return new string(chars);
            }
            else
            {
                return string.Empty;
            }
        }
    }

    public class Waktu
    {
        public static bool UpdateTimeFromNtpServer(string server, int timeZoneOffset)
        {
            try
            {
                var currentTime = GetNtpTime(server, timeZoneOffset);
                Microsoft.SPOT.Hardware.Utility.SetLocalTime(currentTime);

                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Get DateTime from NTP Server
        /// Based on:
        /// http://weblogs.asp.net/mschwarz/archive/2008/03/09/wrong-datetime-on-net-micro-framework-devices.aspx
        /// </summary>
        /// <param name="timeServer">Time Server (NTP) address</param>
        /// <param name="timeZoneOffset">Difference in hours from UTC</param>
        /// <returns>Local NTP Time</returns>
        private static DateTime GetNtpTime(String timeServer, int timeZoneOffset)
        {
            // Find endpoint for TimeServer
            var ep = new IPEndPoint(Dns.GetHostEntry(timeServer).AddressList[0], 123);

            // Make send/receive buffer
            var ntpData = new byte[48];

            // Connect to TimeServer
            using (var s = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp))
            {
                // Set 10s send/receive timeout and connect
                s.SendTimeout = s.ReceiveTimeout = 10000; // 10,000 ms
                s.Connect(ep);

                // Set protocol version
                ntpData[0] = 0x1B;

                // Send Request
                s.Send(ntpData);

                // Receive Time
                s.Receive(ntpData);

                // Close the socket
                s.Close();
            }

            const byte offsetTransmitTime = 40;

            ulong intpart = 0;
            ulong fractpart = 0;

            for (var i = 0; i <= 3; i++)
                intpart = (intpart << 8) | ntpData[offsetTransmitTime + i];

            for (var i = 4; i <= 7; i++)
                fractpart = (fractpart << 8) | ntpData[offsetTransmitTime + i];

            ulong milliseconds = (intpart * 1000 + (fractpart * 1000) / 0x100000000L);

            var timeSpan = TimeSpan.FromTicks((long)milliseconds * TimeSpan.TicksPerMillisecond);
            var dateTime = new DateTime(1900, 1, 1);
            dateTime += timeSpan;

            var offsetAmount = new TimeSpan(timeZoneOffset, 0, 0);
            var networkDateTime = (dateTime + offsetAmount);

            return networkDateTime;
        }
    }
}
