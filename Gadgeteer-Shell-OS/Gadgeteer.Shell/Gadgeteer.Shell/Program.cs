using Gadgeteer.Modules.GHIElectronics;
using System;
using System.Collections;
using System.Threading;
using Microsoft.SPOT;
using Microsoft.SPOT.Presentation;
using Microsoft.SPOT.Presentation.Controls;
using Microsoft.SPOT.Presentation.Media;
using Microsoft.SPOT.Presentation.Shapes;
using Microsoft.SPOT.Touch;
using NetMf.CommonExtensions;
using GHI.Processor;

using Gadgeteer.Networking;
using GT = Gadgeteer;
using GTM = Gadgeteer.Modules;
using GHI.Glide;
using Gadgeteer.Tool;
using System.IO;
using Microsoft.SPOT.IO;
using Skewworks.Labs;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.SPOT.Net.NetworkInformation;
using GHI.Utilities;
using System.Security.Cryptography;
using System.Xml;
using Microsoft.SPOT.Hardware;

namespace System.Diagnostics
{
    public enum DebuggerBrowsableState
    {
        Collapsed,
        Never,
        RootHidden
    }
}
namespace Gadgeteer.Shell
{
    #region Lora
    public class LoraModule
    {
        //LORA setting..
        private static SimpleSerial _loraSerial;
        private static string[] _dataInLora;
        static OutputPort _restPort = new OutputPort(GHI.Pins.FEZRaptor.Socket12.Pin6, true);


        // A delegate type for hooking up change notifications.
        public delegate void SendToScreenEventHandler(object sender, string msg);

        public event SendToScreenEventHandler SendToScreen;

        // Invoke the SendToScreen event; called whenever list changes
        protected virtual void OnSendToScreen(string msg)
        {
            if (SendToScreen != null)
                SendToScreen(this, msg);
        }
        public LoraModule()
        {
            _loraSerial = new SimpleSerial(GHI.Pins.FEZRaptor.Socket12.SerialPortName, 57600);
            _loraSerial.Open();
            _loraSerial.DataReceived += _loraSerial_DataReceived;
            //reset lora
            _restPort.Write(false);
            Thread.Sleep(1000);
            _restPort.Write(true);
            Thread.Sleep(1000);
            OnSendToScreen("Lora module is ready");
            /*
            //setup lora for point to point
            //get lora version
            _loraSerial.WriteLine("sys get ver");
            Thread.Sleep(1000);
            //pause join
            _loraSerial.WriteLine("mac pause");
            Thread.Sleep(1000);
            //set antena power
            _loraSerial.WriteLine("radio set pwr 14");
            Thread.Sleep(1000);
            */
        }

        public void ExecuteLoraCommand(string message)
        {
            _loraSerial.WriteLine(message);

        }

        public void SendData(string Message)
        {
            byte[] b = Encoding.UTF8.GetBytes(Message);
            string hex = "radio tx " + ToHexString(b, 0, b.Length); // TX payload needs to be HEX
            //send data via lora
            _loraSerial.WriteLine(hex);
                   
        }
        void _loraSerial_DataReceived(object sender, System.IO.Ports.SerialDataReceivedEventArgs e)
        {
            //just for debug
            _dataInLora = _loraSerial.Deserialize();
            for (int index = 0; index < _dataInLora.Length; index++)
            {
                Debug.Print(_dataInLora[index]);
                OnSendToScreen(_dataInLora[index]);
            }
        }
        //convert byte to hex
        public static string ToHexString(byte[] value, int index, int length)
        {
            char[] c = new char[length * 3];
            byte b;

            for (int y = 0, x = 0; y < length; ++y, ++x)
            {
                b = (byte)(value[index + y] >> 4);
                c[x] = (char)(b > 9 ? b + 0x37 : b + 0x30);
                b = (byte)(value[index + y] & 0xF);
                c[++x] = (char)(b > 9 ? b + 0x37 : b + 0x30);
            }
            return new string(c, 0, c.Length - 1);
        }

    }
    #endregion

    #region Shell
    public class GvShell
    {
        #region lora
        LoraModule loramodule = null;
        bool IsLoraMode { set; get; }
        #endregion

        #region Handler
        public delegate void IncomingPrintEventHandler(Bitmap result);
        public event IncomingPrintEventHandler PrintEvent;
        private void CallPrintEvent(Bitmap result)
        {
            // Event will be null if there are no subscribers
            if (PrintEvent != null)
            {
                PrintEvent(result);
            }
        }

        public delegate void IncomingClearScreenEventHandler();
        public event IncomingClearScreenEventHandler ClearScreenEvent;
        private void CallClearScreenEvent()
        {
            // Event will be null if there are no subscribers
            if (ClearScreenEvent != null)
            {
                ClearScreenEvent();
            }
        }
        #endregion Handler

        #region controller
        public WiFiRS21 wifiRS21; //{ set; get; }
        public DisplayTE35 displayTE35 { set; get; }
        public SDCard sdCard { set; get; }
        public USBClientEDP usbClientEDP { set; get; }
        public USBHost usbHost { set; get; }
        #endregion
        static SBASIC basic;
        public static string CurrentPath { set; get; }
        public static int CurrentLine { set; get; }
        public string TypedCommand { set; get; }
        public Bitmap Screen { set; get; }
        public int ScreenWidth { set; get; } //= 320;
        const int FontHeight = 20;
        public Color ForeGround { set; get; } //= GT.Color.White;
        public Color BackGround { set; get; } //= GT.Color.Black;
        public ArrayList DataLines { set; get; }
        public int MaxLine { set; get; }
        public int ScreenHeight { set; get; } //= 240;
        public Font CurrentFont { set; get; }
        public string SSID { set; get; }
        public string WifiKey { set; get; }
        private void ClearScreen()
        {
            Screen.Clear();
            Screen.DrawRectangle(Color.Black, 0, 0, 0, ScreenWidth, ScreenHeight, 0, 0, Color.Black, 0, 0, Color.Black, 0, 0, 100);
        }
        public void ExecuteScript(string Cmd)
        {
            if (Cmd.Length <= 0) return;
            Regex regex = new Regex("[^\\s\"']+|\"([^\"]*)\"|'([^']*)'");
            var parts = regex.Matches(Cmd);
            var ParsedMsg = new string[parts.Count];

            for (int i = 0; i < parts.Count; i++)
            {
                ParsedMsg[i] = parts[i].Value.Replace("\"", string.Empty);
            }

            PrintLine(">" + TypedCommand);
            if (IsLoraMode)
            {
                if (ParsedMsg[0].ToUpper() == "EXIT-LORA")
                {
                    IsLoraMode = false;
                    PrintLine("--Exit Lora Mode--");
                }
                else
                    loramodule.ExecuteLoraCommand(Cmd);
            }
            else
            {
                switch (ParsedMsg[0].ToUpper())
                {
                    case "CLS":
                        ClearScreen();
                        for (int i = 0; i < MaxLine; i++) DataLines[i] = string.Empty;
                        CurrentLine = 0;
                        break;
                    case "DIR":
                        if (sdCard.IsCardInserted && sdCard.IsCardMounted)
                        {
                            ShowFiles();
                        }
                        break;
                    case "CD..":
                        {
                            DirectoryInfo dir = new DirectoryInfo(CurrentPath);
                            if (CurrentPath.ToUpper() != "\\SD\\")
                            {
                                CurrentPath = Strings.NormalizeDirectory(dir.Parent.FullName);

                            }
                            PrintLine("Current:" + CurrentPath);
                        }
                        break;
                    case "CD":
                        {
                            DirectoryInfo dir = new DirectoryInfo(CurrentPath + ParsedMsg[1]);
                            if (dir.Exists)
                            {
                                CurrentPath = Strings.NormalizeDirectory(dir.FullName);
                                PrintLine("Current:" + CurrentPath);
                            }
                        }
                        break;
                    case "CURDIR":
                        {
                            PrintLine("Current:" + CurrentPath);
                        }
                        break;
                    case "MKDIR":
                        {
                            if (ParsedMsg.Length == 2)
                            {
                                string newFolder = CurrentPath + ParsedMsg[1];
                                DirectoryInfo dir = new DirectoryInfo(newFolder);
                                if (!dir.Exists)
                                {
                                    dir.Create();
                                    PrintLine("Directory Created:" + newFolder);
                                }
                                else
                                {
                                    PrintLine("Directory is already exists");

                                }
                            }
                        }
                        break;
                    case "RMDIR":
                        {
                            if (ParsedMsg.Length == 2)
                            {
                                string newFolder = CurrentPath + ParsedMsg[1];
                                DirectoryInfo dir = new DirectoryInfo(newFolder);
                                if (dir.Exists)
                                {
                                    dir.Delete();
                                    PrintLine("Directory Deleted:" + newFolder);
                                }
                                else
                                {
                                    PrintLine("Directory not found");

                                }
                            }
                        }
                        break;

                    case "DEL":
                        {
                            if (ParsedMsg.Length == 2)
                            {
                                string TargetFile = CurrentPath + ParsedMsg[1];
                                FileInfo info = new FileInfo(TargetFile);
                                if (info.Exists)
                                {
                                    info.Delete();
                                    PrintLine("File deleted successfully");
                                }
                                else
                                {
                                    PrintLine("File not found");

                                }
                            }
                        }
                        break;
                    case "SCANWIFI":
                        {
                            var thScan = new Thread(new ThreadStart(ScanWifi));
                            thScan.Start();
                        }
                        break;
                    case "JOINWIFI":
                        {
                            if (ParsedMsg.Length != 3)
                            {
                                PrintLine("Please type it: JOINWIFI [SSID] [KEY]");
                            }
                            else
                            {
                                //try to join network
                                SSID = ParsedMsg[1];
                                WifiKey = ParsedMsg[2];
                                var thJoin = new Thread(new ThreadStart(JoinNetwork));
                                thJoin.Start();
                            }

                        }
                        break;
                    case "DOWNLOADFILE":
                        if (ParsedMsg.Length == 2)
                        {
                            Thread thDownload = new Thread(() => DownloadFile(ParsedMsg[1]));
                            thDownload.Start();
                        }
                        break;
                    case "GETUPDATE":
                        if (ParsedMsg.Length == 3)
                        {
                            switch (ParsedMsg[2])
                            {
                                case "1":
                                    UpdateFirmware(ParsedMsg[1], InFieldUpdate.Types.Configuration);
                                    break;
                                case "2":
                                    UpdateFirmware(ParsedMsg[1], InFieldUpdate.Types.Firmware);
                                    break;
                                case "3":
                                    UpdateFirmware(ParsedMsg[1], InFieldUpdate.Types.Application);
                                    break;
                            }
                        }
                        else
                        {
                            PrintLine("format : GETUPDATE [path] [tipe: 1.config/2.firmware/3.app]");
                        }
                        break;

                    case "PRINT":
                        if (ParsedMsg.Length >= 2)
                        {
                            PrintFile(ParsedMsg[1]);
                        }
                        break;
                    case "ENCRYPT":
                        {
                            if (ParsedMsg.Length >= 2)
                            {
                                Thread t = new Thread(() =>
                                {
                                    FileInfo info = new FileInfo(CurrentPath + ParsedMsg[1]);
                                    if (info.Exists)
                                    {
                                        RSACryptoServiceProvider rsa = new RSACryptoServiceProvider();
                                        RSAParameters publicKey = rsa.ExportParameters(true);
                                        var MyKey = Reflection.Serialize(publicKey, typeof(RSAParameters));
                                        var data = new string(Encoding.UTF8.GetChars(File.ReadAllBytes(CurrentPath + ParsedMsg[1])));
                                        //convert to byte array
                                        byte[] original_data = UTF8Encoding.UTF8.GetBytes(data);
                                        //Encrypt the data
                                        byte[] encrypted_bytes = rsa.Encrypt(original_data);

                                        sdCard.StorageDevice.WriteFile(CurrentPath + ParsedMsg[1], encrypted_bytes);
                                        sdCard.StorageDevice.WriteFile(CurrentPath + ParsedMsg[1] + ".key", MyKey);

                                        PrintLine("encrypt succeed");
                                    }
                                });
                                t.Start();
                            }
                        }
                        break;
                    case "DECRYPT":
                        {
                            if (ParsedMsg.Length >= 2)
                            {
                                Thread t = new Thread(() =>
                                {
                                    FileInfo info = new FileInfo(CurrentPath + ParsedMsg[1]);
                                    if (info.Exists)
                                    {
                                        RSACryptoServiceProvider rsa = new RSACryptoServiceProvider();

                                        //convert to byte array
                                        byte[] original_data = sdCard.StorageDevice.ReadFile(CurrentPath + ParsedMsg[1]);
                                        byte[] original_key = sdCard.StorageDevice.ReadFile(CurrentPath + ParsedMsg[1] + ".key");

                                        RSAParameters publicKey = (RSAParameters)Reflection.Deserialize(original_key, typeof(RSAParameters));
                                        //decrypt the data
                                        rsa.ImportParameters(publicKey);
                                        byte[] decrypted_bytes = rsa.Decrypt(original_data);
                                        var data = new string(Encoding.UTF8.GetChars(decrypted_bytes));
                                        File.WriteAllBytes(CurrentPath + ParsedMsg[1], decrypted_bytes);

                                        PrintLine("decrypt succeed");

                                    }
                                });

                                t.Start();

                            }
                        }
                        break;
                    //LORA EXTENSION
                    case "SEND-MSG":
                        {
                            if (ParsedMsg.Length == 2)
                            {
                                if (loramodule == null)
                                {
                                    PrintLine("sys: Lora module is not ready");
                                }
                                else
                                {
                                    loramodule.SendData(ParsedMsg[1]);
                                    PrintLine("--sending message--");
                                }
                            }
                        }
                        break;

                    case "INIT-LORA":
                        {
                            if (ParsedMsg.Length == 1)
                            {
                                if (loramodule == null)
                                {
                                    loramodule = new LoraModule();
                                    loramodule.SendToScreen += (object sender, string msg)
                                    =>
                                    {
                                        PrintLine("sys: "+msg);
                                        PrintLine(">", false);
                                        CallPrintEvent(Screen);
                                    };
                                    PrintLine("sys: Lora module is ready");
                                }
                                else
                                {
                                    PrintLine("sys: Lora module is already initialized");
                                }


                            }
                        }
                        break;
                    //LORA EXTENSION
                    case "ENTER-LORA":
                        {
                            if (ParsedMsg.Length == 1)
                            {
                                if (loramodule == null)
                                {
                                    PrintLine("sys: Please init-lora first !");
                                }
                                else
                                {
                                    IsLoraMode = true;
                                    PrintLine("--Enter Lora Mode--");
                                }


                            }
                        }
                        break;
                    case "EXIT-LORA":
                        {
                            if (ParsedMsg.Length == 1)
                            {
                                PrintLine("sys: you're not in Lora Mode !");
                            }
                        }
                        break;

                    default:
                        bool Executed = false;
                        if (ParsedMsg.Length == 1)
                        {
                            //execute file
                            Executed = ExecuteFile(ParsedMsg[0]);
                        }
                        if (!Executed)
                            PrintLine("Unknown command.");
                        break;
                }
            }
            PrintLine(">", false);
            CallPrintEvent(Screen);
        }


        #region Flashing Firmware / App
        public const int BLOCK_SIZE = 65536;

        public void UpdateFirmware(string FileName, InFieldUpdate.Types Tipe)
        {
            FileInfo info = new FileInfo(CurrentPath + FileName);
            if (!info.Exists)
            {
                PrintLine("File not found, type it correctly.");
                return;
            }
            PrintLine("update in progress, please wait a moment..");
            CallPrintEvent(Screen);            
            switch (Tipe)
            {
                case InFieldUpdate.Types.Application:
                    InFieldUpdate.Initialize(InFieldUpdate.Types.Application);
                    LoadFile(info.FullName, InFieldUpdate.Types.Application);
                    break;
                case InFieldUpdate.Types.Configuration:
                    InFieldUpdate.Initialize(InFieldUpdate.Types.Configuration);
                    LoadFile(info.FullName, InFieldUpdate.Types.Configuration);
                    break;
                case InFieldUpdate.Types.Firmware:
                    InFieldUpdate.Initialize(InFieldUpdate.Types.Firmware);
                    LoadFile(info.FullName, InFieldUpdate.Types.Firmware);
                    break;

            }
            InFieldUpdate.FlashAndReset();
        }

        public static void LoadFile(string filename, InFieldUpdate.Types type)
        {
            using (var stream = new FileStream(filename, FileMode.Open))
            {
                var data = new byte[BLOCK_SIZE];

                for (int i = 0; i < stream.Length / BLOCK_SIZE; i++)
                {
                    stream.Read(data, 0, BLOCK_SIZE);
                    InFieldUpdate.Load(type, data, BLOCK_SIZE);
                }

                stream.Read(data, 0, (int)stream.Length % BLOCK_SIZE);
                InFieldUpdate.Load(type, data, (int)stream.Length % BLOCK_SIZE);
            }
        }
        // This method is run when the mainboard is powered up or reset.   
        #endregion


        bool DownloadFile(string UrlFile)
        {
            try
            {
                if (wifiRS21.IsNetworkConnected)
                {
                    var client = WebClient.GetFromWeb(UrlFile);
                    client.ResponseReceived += (HttpRequest sender, HttpResponse response)
                    =>
                    {
                        string filename = UrlFile.Substring(UrlFile.LastIndexOf('/') + 1);
                        sdCard.StorageDevice.WriteFile(CurrentPath + filename, response.RawContentBytes);
                        PrintLine("File has been downloaded successfully :" + filename);
                        CallPrintEvent(Screen);
                    };
                }
                else
                {
                    PrintLine("Network is not connected.");

                    CallPrintEvent(Screen);
                }
            }
            catch
            {
                PrintLine("Download file failed.");
                CallPrintEvent(Screen);
                return false;
            }
            return true;
        }        

        bool PrintFile(string Filename)
        {
            bool Result = false;
            FileInfo info = new FileInfo(CurrentPath + Filename);
            if (info.Exists)
            {
                var data = sdCard.StorageDevice.ReadFile(info.FullName);
                var strdata = new string(Encoding.UTF8.GetChars(data));
                try
                {

                    foreach (var str in Regex.Split("\r\n", strdata,
                        RegexOptions.IgnoreCase))
                    {
                        PrintLine(str);
                        CallPrintEvent(Screen);
                    }
                    Result = true;
                }
                catch { }
            }
            return Result;
        }
        bool ExecuteFile(string Filename)
        {
            bool Result = false;
            FileInfo info = new FileInfo(CurrentPath + Filename);
            if (info.Exists)
            {
                switch (info.Extension.ToLower())
                {
                    case ".bas":
                        {
                            var data = sdCard.StorageDevice.ReadFile(info.FullName);
                            var codes = new string(Encoding.UTF8.GetChars(data));
                            try
                            {
                                basic.Run(codes);
                                Result = true;
                            }
                            catch { }
                        }
                        break;
                    case ".pe":
                        try
                        {
                            //DeviceController ctl = new DeviceController(displayTE35, sdCard, usbHost, usbClientEDP);

                            Thread t = new Thread(() => Launcher.ExecApp(info.FullName));
                            t.Start();
                            Result = true;
                        }
                        catch { }
                        break;
                    case ".jpg":
                        {
                            var storage = sdCard.StorageDevice;
                            Bitmap bitmap = storage.LoadBitmap(info.FullName, Bitmap.BitmapImageType.Jpeg);
                            CallPrintEvent(bitmap);
                            Result = true;
                            Thread.Sleep(2000);
                        }
                        break;
                    case ".bmp":
                        {
                            var storage = sdCard.StorageDevice;
                            Bitmap bitmap = storage.LoadBitmap(info.FullName, Bitmap.BitmapImageType.Bmp);
                            CallPrintEvent(bitmap);
                            Result = true;
                            Thread.Sleep(2000);
                        }
                        break;
                    case ".gif":
                        {
                            var storage = sdCard.StorageDevice;
                            Bitmap bitmap = storage.LoadBitmap(info.FullName, Bitmap.BitmapImageType.Gif);
                            CallPrintEvent(bitmap);
                            Result = true;
                            Thread.Sleep(2000);

                        }
                        break;
                }
            }
            return Result;
        }
        
        void ShowFiles()
        {
            if (VolumeInfo.GetVolumes()[0].IsFormatted)
            {
                if (Directory.Exists(CurrentPath))
                {
                    DirectoryInfo dir = new DirectoryInfo(CurrentPath);

                    var files = dir.GetFiles();
                    var folders = dir.GetDirectories();

                    PrintLine("Files available on " + CurrentPath + ":");
                    if (files.Length > 0)
                    {
                        for (int i = 0; i < files.Length; i++)
                            PrintLine(files[i].Name + " - " + Strings.FormatDiskSize(files[i].Length));
                    }
                    else
                    {
                        PrintLine("Files not found.");
                    }

                    PrintLine("Folders available on " + CurrentPath + ":");
                    if (folders.Length > 0)
                    {
                        for (int i = 0; i < folders.Length; i++)
                            PrintLine(folders[i].Name);
                    }
                    else
                    {
                        PrintLine("folders not found.");
                    }
                }
            }
            else
            {
                PrintLine("Storage is not formatted. " +
                    "Format on PC with FAT32/FAT16 first!");
            }
        }
        public void PrintLine(string Output, bool AddNewLine = true)
        {
            if (CurrentLine >= MaxLine - 1)
            {
                ClearScreen();
                for (int i = 0; i < MaxLine - 1; i++)
                {
                    DataLines[i] = DataLines[i + 1];
                    Screen.DrawText(DataLines[i].ToString(), CurrentFont, ForeGround, 5, FontHeight * i);
                }
                DataLines[CurrentLine] = Output;
                Screen.DrawText(Output, CurrentFont, ForeGround, 5, FontHeight * CurrentLine);
            }
            else
            {
                DataLines[CurrentLine] = Output;
                Screen.DrawText(Output, CurrentFont, ForeGround, 5, FontHeight * CurrentLine);
                if (AddNewLine)
                    CurrentLine++;
            }

        }
        public void PrintLine(int LineNumber, string Output)
        {
            ClearScreen();
            for (int i = 0; i <= CurrentLine - 1; i++)
            {
                Screen.DrawText(DataLines[i].ToString(), CurrentFont, ForeGround, 5, FontHeight * i);
            }
            Screen.DrawText(">" + Output, CurrentFont, ForeGround, 5, FontHeight * CurrentLine);
        }
        public void TypeInCommand(char KeyDown)
        {
            switch ((int)KeyDown)
            {
                //enter
                case 10:
                    ExecuteScript(TypedCommand.Trim());
                    TypedCommand = string.Empty;
                    break;
                //backspace
                case 8:
                    if (TypedCommand.Length > 0)
                    {
                        TypedCommand = TypedCommand.Substring(0, TypedCommand.Length - 1);
                        PrintLine(CurrentLine, TypedCommand);
                        CallPrintEvent(Screen);
                    }
                    break;
                default:
                    TypedCommand = TypedCommand + KeyDown.ToString();
                    PrintLine(CurrentLine, TypedCommand);
                    CallPrintEvent(Screen);
                    break;
            }

        }
        public GvShell(ref SDCard sdCard, ref USBHost usbHost, ref DisplayTE35 displayT35, ref USBClientEDP usbClientEdp, ref WiFiRS21 wifiRS21)
        {
            IsLoraMode = false;
            //initial / default params
            ForeGround=GT.Color.White;
            BackGround=GT.Color.Black;
            ScreenWidth = 320;
            ScreenHeight = 240;
            this.wifiRS21 = wifiRS21;
            this.displayTE35 = displayT35;
            this.usbHost = usbHost;
            this.usbClientEDP = usbClientEdp;
            this.sdCard = sdCard;
            Screen = new Bitmap(ScreenWidth, ScreenHeight);
            ClearScreen();
            MaxLine = ScreenHeight / 20;
            CurrentLine = 0;
            CurrentFont = Resources.GetFont(Resources.FontResources.NinaB);
            CurrentPath = "\\SD\\";
            DataLines = new ArrayList();
            for (int i = 0; i < MaxLine; i++) DataLines.Add(string.Empty);
            TypedCommand = string.Empty; if (basic == null)
                if (basic == null)
                {
                    basic = new SBASIC();
                    basic.Print += Basic_Print;
                    basic.ClearScreen += Basic_ClearScreen;
                }
            //setup network
            wifiRS21.DebugPrintEnabled = true;
            NetworkChange.NetworkAvailabilityChanged += NetworkChange_NetworkAvailabilityChanged;
            NetworkChange.NetworkAddressChanged += NetworkChange_NetworkAddressChanged;
            //setup network
            wifiRS21.NetworkInterface.Open();
            wifiRS21.NetworkInterface.EnableDhcp();
            wifiRS21.NetworkInterface.EnableDynamicDns();

        }

        public void PrintWelcome()
        {
            PrintLine("Welcome to Gadgeteer Shell (C) Gravicode");
            PrintLine(">", false);
            CallPrintEvent(Screen);


        }

        private void Basic_ClearScreen(SBASIC sender)
        {
            ClearScreen();
        }

        private void Basic_Print(SBASIC sender, string value)
        {
            PrintLine(value);
            CallPrintEvent(Screen);
        }

        void JoinNetwork()
        {
            if (SSID.Length > 0 && WifiKey.Length > 0)
            {
                GHI.Networking.WiFiRS9110.NetworkParameters[] info = wifiRS21.NetworkInterface.Scan(SSID);
                if (info != null)
                {
                    wifiRS21.NetworkInterface.Join(SSID, WifiKey);
                    PrintLine("Waiting for DHCP...");
                    while (wifiRS21.NetworkInterface.IPAddress == "0.0.0.0")
                    {
                        Thread.Sleep(250);
                    }
                    PrintLine("network joined");
                    PrintLine("active network:"+SSID);
              
                    ListNetworkInterfaces();
                }
                else
                {
                    PrintLine("SSID cannot be found!");
                    CallPrintEvent(Screen);
                }
            }
        }

        void ListNetworkInterfaces()
        {
            var settings = wifiRS21.NetworkSettings;

            PrintLine("------------------------------------------------");
            PrintLine("MAC: " + ByteExt.ToHexString(settings.PhysicalAddress, "-"));
            PrintLine("IP Address:   " + settings.IPAddress);
            PrintLine("DHCP Enabled: " + settings.IsDhcpEnabled);
            PrintLine("Subnet Mask:  " + settings.SubnetMask);
            PrintLine("Gateway:      " + settings.GatewayAddress);
            PrintLine("------------------------------------------------");
            CallPrintEvent(Screen);
        }

        void ScanWifi()
        {
            // look for avaiable networks
            var scanResults = wifiRS21.NetworkInterface.Scan();
            
            // go through each network and print out settings in the debug window
            foreach (GHI.Networking.WiFiRS9110.NetworkParameters result in scanResults)
            {

                PrintLine("****" + result.Ssid + "****");
                //PrintLine("ChannelNumber = " + result.Channel);
                PrintLine("networkType = " + result.NetworkType);
                //PrintLine("PhysicalAddress = " + GetMACAddress(result.PhysicalAddress));
                //PrintLine("RSSI = " + result.Rssi);
                PrintLine("SecMode = " + result.SecurityMode);
            }
            CallPrintEvent(Screen);
        }
        string GetMACAddress(byte[] PhysicalAddress)
        {
            return ByteToHex(PhysicalAddress[0]) + "-"
                                + ByteToHex(PhysicalAddress[1]) + "-"
                                + ByteToHex(PhysicalAddress[2]) + "-"
                                + ByteToHex(PhysicalAddress[3]) + "-"
                                + ByteToHex(PhysicalAddress[4]) + "-"
                                + ByteToHex(PhysicalAddress[5]);
        }
        string ByteToHex(byte number)
        {
            string hex = "0123456789ABCDEF";
            return new string(new char[] { hex[(number & 0xF0) >> 4], hex[number & 0x0F] });
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
    #endregion

    #region Forms
    public class Screen
    {
        public enum ScreenTypes { Splash = 0, Prompt };
        public delegate void GoToFormEventHandler(ScreenTypes form, params string[] Param);
        public event GoToFormEventHandler FormRequestEvent;
        protected void CallFormRequestEvent(ScreenTypes form, params string[] Param)
        {
            // Event will be null if there are no subscribers
            if (FormRequestEvent != null)
            {
                FormRequestEvent(form, Param);
            }
        }
        protected GHI.Glide.Display.Window MainWindow { set; get; }
        public virtual void Init(params string[] Param)
        {
            //do nothing
        }

        public Screen(ref GHI.Glide.Display.Window window)
        {
            MainWindow = window;
        }
    }
    public class PromptForm : Screen
    {
        int LineCounter
        {
            set; get;
        }

        const int LineSpacing = 20;
        private Gadgeteer.Modules.GHIElectronics.DisplayTE35 displayTE35;
        WiFiRS21 wifiRS21;
        SDCard sdCard;
        USBHost usbHost;
        USBClientEDP usbClientEDP;
        GHI.Glide.UI.Image imgCode { set; get; }
        ArrayList LinesOfCode;

        public PromptForm(ref GHI.Glide.Display.Window window, ref Gadgeteer.Modules.GHIElectronics.DisplayTE35 displayTE35, ref SDCard sdCard, ref USBHost usbHost, ref USBClientEDP usbClientEDP, ref WiFiRS21 wifiRS21)
            : base(ref window)
        {
            this.usbClientEDP = usbClientEDP;
            this.usbHost = usbHost;
            this.sdCard = sdCard;
            this.displayTE35 = displayTE35;
            this.wifiRS21 = wifiRS21;
     
        }
        public override void Init(params string[] Param)
        {
            LinesOfCode = new ArrayList();
            LineCounter = 0;
            MainWindow = GlideLoader.LoadWindow(Resources.GetString(Resources.StringResources.PromptForm));

            imgCode = (GHI.Glide.UI.Image)MainWindow.GetChildByName("imgCode");

            Glide.MainWindow = MainWindow;
            GvShell s = new GvShell(ref sdCard, ref usbHost, ref displayTE35, ref usbClientEDP, ref wifiRS21);
            s.PrintEvent += S_Print;
            s.ClearScreenEvent += S_ClearScreen;
            if (usbHost.IsKeyboardConnected)
            {
                usbHost.ConnectedKeyboard.KeyDown += (GHI.Usb.Host.Keyboard sender, GHI.Usb.Host.Keyboard.KeyboardEventArgs args) =>
                {
                    //Debug.Print(((int)args.ASCII).ToString());
                    s.TypeInCommand(args.ASCII);
                };
            }
            s.PrintWelcome();
            Thread.Sleep(500);

        }

        private void S_ClearScreen()
        {

        }

        private void S_Print(Bitmap value)
        {
            imgCode.Bitmap = value;
            imgCode.Invalidate();
        }
    }
    public class SplashForm : Screen
    {
        public SplashForm(ref GHI.Glide.Display.Window window) : base(ref window)
        {

        }
        public override void Init(params string[] Param)
        {
            MainWindow = GlideLoader.LoadWindow(Resources.GetString(Resources.StringResources.SplashForm));
            var img = (GHI.Glide.UI.Image)MainWindow.GetChildByName("ImgLogo");

            GT.Picture pic = new GT.Picture(Resources.GetBytes(Resources.BinaryResources.logo), GT.Picture.PictureEncoding.JPEG);
            img.Bitmap = pic.MakeBitmap();

            Glide.MainWindow = MainWindow;
            Thread.Sleep(2000);
            CallFormRequestEvent(ScreenTypes.Prompt);

        }
    }

    #endregion
    public partial class Program
    {
        private static GHI.Glide.Display.Window MainWindow;
        private static Screen.ScreenTypes ActiveWindow { set; get; }
        Hashtable Screens { set; get; }
        // This method is run when the mainboard is powered up or reset.   
        void ProgramStarted()
        {
            Debug.Print("Program Started");

            Screens = new Hashtable();
            //populate all form
            var F1 = new SplashForm(ref MainWindow);
            F1.FormRequestEvent += General_FormRequestEvent;
            Screens.Add(Screen.ScreenTypes.Splash, F1);

            var F2 = new PromptForm(ref MainWindow, ref displayTE35, ref sdCard, ref usbHost, ref usbClientEDP, ref wifiRS21);
            F2.FormRequestEvent += General_FormRequestEvent;
            Screens.Add(Screen.ScreenTypes.Prompt, F2);

            Glide.FitToScreen = true;
            GlideTouch.Initialize();

            //load splash
            LoadForm(Screen.ScreenTypes.Splash);
        }
        void LoadForm(Screen.ScreenTypes form, params string[] Param)
        {
            ActiveWindow = form;
            switch (form)
            {
                case Screen.ScreenTypes.Splash:
                case Screen.ScreenTypes.Prompt:
                    (Screens[form] as Screen).Init(Param);
                    break;
                default:
                    return;
                    //throw new Exception("Belum diterapkan");
            }

        }
        void General_FormRequestEvent(Screen.ScreenTypes form, params string[] Param)
        {
            LoadForm(form, Param);
        }
        
    }

    #region Helper
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
    #endregion
}

