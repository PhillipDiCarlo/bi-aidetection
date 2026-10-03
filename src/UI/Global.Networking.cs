using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Management;
using System.Media;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml.Linq;

using Innovative.SolarCalculator;

using Microsoft.Win32;
using Microsoft.Win32.SafeHandles;

using NAudio.Wave;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

using static AITool.AITOOL;

namespace AITool
{
    public static partial class Global
    {

        public static async Task<bool> IsPortOpenAsync(string Host, int port)
        {
            Socket socket = null;

            try
            {
                // make a TCP based socket
                socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

                // connect
                await Task.Run(() => socket.Connect(Host, port));

                return true;
            }
            catch (SocketException ex)
            {
                if (ex.SocketErrorCode == SocketError.ConnectionRefused)
                {
                    return false;
                }

                //An error occurred when attempting to access the socket
                Debug.WriteLine(ex.ToString());
                Console.WriteLine(ex);
            }
            finally
            {
                if (socket?.Connected ?? false)
                {
                    socket?.Disconnect(false);
                }
                socket?.Close();
            }

            return false;
        }

        public static bool IsPortOpen(string Host, int port)
        {
            Socket socket = null;

            try
            {
                // make a TCP based socket
                socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

                // connect
                socket.Connect(Host, port);

                return true;
            }
            catch (SocketException ex)
            {
                if (ex.SocketErrorCode == SocketError.ConnectionRefused)
                {
                    return false;
                }

                //An error occurred when attempting to access the socket
                //Debug.WriteLine(ex.ToString());
                //Console.WriteLine(ex);
            }
            finally
            {
                if (socket?.Connected ?? false)
                {
                    socket?.Disconnect(false);
                }
                socket?.Close();
            }

            return false;
        }

        public static bool IsLocalPortInUse(int port)
        {

            try
            {
                // Evaluate current system tcp connections. This is the same information provided
                // by the netstat command line application, just in .Net strongly-typed object
                // form.  We will look through the list, and if our port we would like to use
                // in our TcpClient is occupied, we will set isAvailable to false.
                IPGlobalProperties ipGlobalProperties = IPGlobalProperties.GetIPGlobalProperties();

                TcpConnectionInformation[] tcpConnInfoArray = ipGlobalProperties.GetActiveTcpConnections();

                foreach (TcpConnectionInformation tcpi in tcpConnInfoArray)
                {
                    if (tcpi.LocalEndPoint.Port == port)
                    {
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                Log($"Error: {ex.Msg()}");
                return true;
            }

            return false;
        }



        public static string UpdateURL(string InURL, int DefaultPort, string DefaultPath, string Host, ref bool WasFixed, ref bool HadError)
        {
            string ret = InURL;
            UriBuilder uriBuilder = new UriBuilder();
            try
            {
                Uri url = new Uri(InURL);

                if (url != null)
                {

                    uriBuilder.Scheme = url.Scheme;
                    uriBuilder.Host = url.Host;
                    uriBuilder.Path = url.PathAndQuery;
                    uriBuilder.Port = url.Port;

                    if (url.HostNameType == UriHostNameType.IPv6 && url.Host.Contains("[") && !InURL.Contains("["))
                    {
                        //it adds [ ] around an ipv6 address
                        Log($"Debug: Placed square brackets around IPV6 address: '{url.Host}' for {InURL}");
                        WasFixed = true;
                    }


                    if (!string.IsNullOrEmpty(Host) && !Host.Equals(url.Host, StringComparison.OrdinalIgnoreCase))
                    {
                        uriBuilder.Host = Host;
                        Log($"Debug: Changed host from '{url.Host}' to '{Host}' for {InURL}");
                        WasFixed = true;
                    }

                    if (DefaultPort > 0 && url.Port != DefaultPort)
                    {
                        uriBuilder.Port = DefaultPort;
                        Log($"Debug: Changed port from '{url.Port}' to '{DefaultPort}' for {InURL}");
                        WasFixed = true;
                    }

                    //scheme=http or https
                    if (DefaultPort == 443 && url.Scheme != "https")
                    {
                        Log($"Debug: Changed scheme from '{url.Scheme}' to 'https' for {InURL}");
                        uriBuilder.Scheme = "https";
                        WasFixed = true;
                    }
                    else if (DefaultPort == 80 && url.Scheme == "https")
                    {
                        Log($"Debug: Changed scheme from '{url.Scheme}' to 'http' for {InURL}");
                        uriBuilder.Scheme = "http";
                        WasFixed = true;
                    }

                    //if (!InURL.Contains($":{uriBuilder.Port}"))
                    //{
                    //    //this doesnt work because URI.ToString does not include the port if it is the default port for the scheme
                    //    Log($"Debug: Port added to URL: '{uriBuilder.Port}' for {InURL}");
                    //    WasFixed = true;
                    //}

                    if (!string.IsNullOrEmpty(DefaultPath) && string.IsNullOrEmpty(url.PathAndQuery) || url.PathAndQuery == "/" || url.PathAndQuery == "\\" || !url.PathAndQuery.StartsWith(DefaultPath, StringComparison.OrdinalIgnoreCase))
                    {
                        Log($"Debug: Added correct path of '{DefaultPath}' to {InURL}");
                        uriBuilder.Path = DefaultPath;
                        WasFixed = true;
                    }


                    if (!WasFixed)
                    {
                        string newurl = uriBuilder.Uri.GetComponents(UriComponents.AbsoluteUri, UriFormat.Unescaped);
                        if (!newurl.Equals(ret, StringComparison.OrdinalIgnoreCase))
                        {
                            Log($"Debug: Updated URL '{InURL}' to '{newurl}'");
                            WasFixed = true;
                        }

                    }

                }
                else
                {
                    HadError = true;
                    Log($"Error: Bad url '{InURL}'");
                }

            }
            catch (Exception ex)
            {
                HadError = true;
                Log($"Error: {InURL}: {ex.Message}");
            }

            if (WasFixed)
            {
                //ret = uriBuilder.Uri.ToString();
                ret = uriBuilder.Uri.GetComponents(UriComponents.AbsoluteUri, UriFormat.SafeUnescaped);
            }

            return ret;
        }

        public static string GetURLPath(string InURL)
        {
            string ret = "";
            try
            {
                Uri url = new Uri(InURL);
                if (url != null && !string.IsNullOrEmpty(url.PathAndQuery) && url.PathAndQuery != "\\" && url.PathAndQuery != "/")
                    ret = url.PathAndQuery;
            }
            catch (Exception ex)
            {
                Log($"Error: {InURL}: {ex.Message}");
            }
            return ret;
        }


        public static bool IsValidURL(string InURL)
        {
            bool ret = false;
            try
            {
                Uri url = new Uri(InURL);
                if (url != null &&
                    !string.IsNullOrEmpty(url.PathAndQuery) &&
                    url.PathAndQuery != "/" &&
                    !url.PathAndQuery.Contains("|") &&
                    !url.PathAndQuery.Contains(";") &&
                    url.Port != 0)
                    ret = true;
            }
            catch (Exception ex)
            {
                //Log($"Error: {InURL}: {ex.Message}");
            }
            return ret;
        }

        private static string CachedMacAddress = "";
        /// <summary>
        /// Finds the MAC address of the NIC with maximum speed.
        /// </summary>
        /// <returns>The MAC address.</returns>
        public static string GetMacAddress()
        {

            if (CachedMacAddress.IsNotNull())
                return CachedMacAddress;

            const int MIN_MAC_ADDR_LENGTH = 12;
            string macAddress = string.Empty;
            long maxSpeed = -1;
            Stopwatch sw = Stopwatch.StartNew();

            try
            {
                foreach (NetworkInterface nic in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (nic.OperationalStatus == OperationalStatus.Up && nic.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                    {
                        string tempMac = nic.GetPhysicalAddress().ToString();

                        if (nic.Speed > maxSpeed && !string.IsNullOrEmpty(tempMac) && tempMac.Length >= MIN_MAC_ADDR_LENGTH)
                        {
                            Log("Trace: New Max Speed = " + nic.Speed + ", MAC: " + tempMac);
                            maxSpeed = nic.Speed;
                            macAddress = tempMac;
                        }

                    }

                }

            }
            catch (Exception ex)
            {
                Log("Error: " + ex.Msg());
            }

            if (sw.ElapsedMilliseconds > 500)  //should it really take this long??
                Log($"Warn: It tool {sw.ElapsedMilliseconds}ms to get the MAC address?");

            CachedMacAddress = macAddress;

            return macAddress;
        }

        private static Dictionary<string, string> HostNameCache = new Dictionary<string, string>();
        public static async Task<string> GetHostNameAsync(string IPAddressOrHostName)
        {

            if (HostNameCache.ContainsKey(IPAddressOrHostName.ToLower()))
                return HostNameCache[IPAddressOrHostName.ToLower()];

            Stopwatch sw = Stopwatch.StartNew();
            string ret = IPAddressOrHostName;

            try
            {
                if (!IsValidIPAddress(IPAddressOrHostName, out IPAddress FoundIP))
                    return IPAddressOrHostName;  //assume valid hostname if it doesnt look like an ip address

                //why is this taking close to 5 seconds for an internal network dns call??

                IPHostEntry entry = await Dns.GetHostEntryAsync(IPAddressOrHostName);
                if (entry != null)
                {
                    ret = entry.HostName;
                }
            }
            catch (Exception ex)
            {
                Log("Error: " + ex.Message);
            }
            finally
            {
                Log($"Trace: Resolved Host name '{IPAddressOrHostName}' to '{ret}' in {sw.ElapsedMilliseconds}ms");
            }

            HostNameCache.Add(IPAddressOrHostName.ToLower(), ret);

            return ret;

        }

        private static string CurrentIP = "";
        private static string CurrentHost = "";
        public static bool IsLocalHost(string HostNameOrIPAddress)
        {
            try
            {
                bool ret = false;

                if (CurrentIP.IsEmpty())
                    CurrentIP = GetAllLocalIPs(NetworkInterfaceType.Ethernet)[0].ToString();

                if (CurrentHost.IsEmpty())
                    CurrentHost = Dns.GetHostName();

                if (string.IsNullOrEmpty(HostNameOrIPAddress) ||
                    HostNameOrIPAddress == "." ||
                    HostNameOrIPAddress.EqualsIgnoreCase("localhost") ||
                    HostNameOrIPAddress == "127.0.0.1" ||
                    HostNameOrIPAddress == "0.0.0.0" ||
                    HostNameOrIPAddress == "::1:" ||
                    HostNameOrIPAddress == "[::1:]" ||
                    HostNameOrIPAddress == "0:0:0:0:0:0:0:1" ||
                    HostNameOrIPAddress == "[0:0:0:0:0:0:0:1]" ||
                    HostNameOrIPAddress.EqualsIgnoreCase(CurrentIP) ||
                    HostNameOrIPAddress.EqualsIgnoreCase(CurrentHost))
                {
                    ret = true;
                }
                else
                {
                    IPAddress ip = GetIPAddressFromHostname(HostNameOrIPAddress);
                    if (IPAddress.IsLoopback(ip))
                    {
                        ret = true;
                    }
                    else if (HostNameOrIPAddress.EqualsIgnoreCase(CurrentIP) ||
                             HostNameOrIPAddress.EqualsIgnoreCase(CurrentHost))
                    {
                        ret = true;
                    }
                }

                ////if (!ret)
                ////    Log($"Host or IP not detected as 'localhost': '{HostNameOrIPAddress}' ThisHost='{CurrentHost}', ThisIP='{CurrentIP}'");

                return ret;

            }
            catch (Exception ex)
            {
                Log($"Error: {ex.Msg()}");
                return false;
            }


        }


        public static async Task<bool> IsLocalHostAsync(string HostNameOrIPAddress)
        {
            try
            {
                bool ret = false;

                if (CurrentIP.IsEmpty())
                {
                    List<IPAddress> ips = GetAllLocalIPs(NetworkInterfaceType.Ethernet);
                    //was getting index out of bounds on one machine that has never had AITOOL installed
                    if (ips.Count > 0)
                        CurrentIP = ips[0].ToString();
                    else
                    {
                        CurrentIP = "127.0.0.1";  //this should not happen - antivirus/firewall blocking?
                    }
                }


                if (CurrentHost.IsEmpty())
                    CurrentHost = Dns.GetHostName();

                if (string.IsNullOrEmpty(HostNameOrIPAddress) ||
                    HostNameOrIPAddress == "." ||
                    HostNameOrIPAddress.EqualsIgnoreCase("localhost") ||
                    HostNameOrIPAddress == "127.0.0.1" ||
                    HostNameOrIPAddress == "0.0.0.0" ||
                    HostNameOrIPAddress == "::1:" ||
                    HostNameOrIPAddress == "[::1:]" ||
                    HostNameOrIPAddress == "0:0:0:0:0:0:0:1" ||
                    HostNameOrIPAddress == "[0:0:0:0:0:0:0:1]" ||
                    HostNameOrIPAddress.EqualsIgnoreCase(CurrentIP) ||
                    HostNameOrIPAddress.EqualsIgnoreCase(CurrentHost))
                {
                    ret = true;
                }
                else
                {
                    IPAddress ip = await GetIPAddressFromHostnameAsync(HostNameOrIPAddress);
                    if (IPAddress.IsLoopback(ip))
                    {
                        ret = true;
                    }
                    else if (HostNameOrIPAddress.EqualsIgnoreCase(CurrentIP) ||
                             HostNameOrIPAddress.EqualsIgnoreCase(CurrentHost))
                    {
                        ret = true;
                    }
                }

                ////if (!ret)
                ////    Log($"Host or IP not detected as 'localhost': '{HostNameOrIPAddress}' ThisHost='{CurrentHost}', ThisIP='{CurrentIP}'");

                return ret;

            }
            catch (Exception ex)
            {
                Log($"Error: {ex.Msg()}");
                return false;
            }


        }


        public static bool IsLocalNetwork(string HostNameOrIPAddress)
        {

            if (IsLocalHost(HostNameOrIPAddress))
                return true;

            IPAddress ip = GetIPAddressFromHostname(HostNameOrIPAddress);

            if (ip != IPAddress.None)
            {
                if (IPAddress.IsLoopback(ip))
                    return true;

                if (ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    byte[] bytes = ip.GetAddressBytes();
                    switch (bytes[0])
                    {
                        case 10:
                            return true;
                        case 172:
                            return bytes[1] < 32 && bytes[1] >= 16;
                        case 192:
                            return bytes[1] == 168;
                        default:
                            return false;
                    }
                }
                else if (ip.AddressFamily == AddressFamily.InterNetworkV6)
                {
                    var addressAsString = ip.ToString();
                    var firstWord = addressAsString.Split(new[] { ':' }, StringSplitOptions.RemoveEmptyEntries)[0];

                    // Make sure we are dealing with an IPv6 address
                    if (ip.AddressFamily != AddressFamily.InterNetworkV6) return false;

                    // The original IPv6 Site Local addresses (fec0::/10) are deprecated. Unfortunately IsIPv6SiteLocal only checks for the original deprecated version:
                    else if (ip.IsIPv6SiteLocal) return true;

                    // These days Unique Local Addresses (ULA) are used in place of Site Local. 
                    // ULA has two variants: 
                    //      fc00::/8 is not defined yet, but might be used in the future for internal-use addresses that are registered in a central place (ULA Central). 
                    //      fd00::/8 is in use and does not have to registered anywhere.
                    else if (firstWord.Substring(0, 2) == "fc" && firstWord.Length >= 4) return true;
                    else if (firstWord.Substring(0, 2) == "fd" && firstWord.Length >= 4) return true;

                    // Link local addresses (prefixed with fe80) are not routable
                    else if (firstWord == "fe80") return true;

                    // Discard Prefix
                    else if (firstWord == "100") return true;

                    // Any other IP address is not Unique Local Address (ULA)
                    else return false;
                }
            }

            return false;


        }

        public enum IPType
        {
            Path,
            URL
        }

        public static string IP2Str(IPAddress ip, IPType type)
        {
            if (ip == IPAddress.None)
                return ip.ToString(); //??

            if (ip.AddressFamily == AddressFamily.InterNetwork)
                return ip.ToString();

            if (ip.AddressFamily == AddressFamily.InterNetworkV6)
            {

                if (type == IPType.Path)
                {
                    return $"{ip.ToString().Replace(":::", "::").Replace(":", "-").Replace("%", "s")}.ipv6-literal.net";  //https://devblogs.microsoft.com/oldnewthing/20100915-00/?p=12863

                }
                else if (type == IPType.URL)
                {
                    return $"[{ip.ToString().Replace(":::", "::")}]";  //add square brackets to make the URL valid.  Fix where BlueIris returns 3 colons vs 2 correct.
                }
            }

            return ip.ToString();

        }

        public static string IP2Str(string HostNameOrIPAddress, IPType type)
        {
            IPAddress ip = GetIPAddressFromIPString(HostNameOrIPAddress.Replace(":::", "::"));
            if (ip == IPAddress.None)
                return HostNameOrIPAddress;

            return IP2Str(ip, type);


        }

        public static string CleanIPV6Address(string IP)
        {
            //2600-6c64-6b7f-f8d8--1d4.ipv6-literal.net
            //2600:6c64:6b7f:f8d8::1d4
            //[2600:6c64:6b7f:f8d8::1d4]
            //[2600:6c64:6b7f:f8d8:::1d4] - bad, 3 colons
            IP = IP.Trim("[] ".ToCharArray());
            if (IP.IndexOf(".ipv6-literal.net", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                IP = IP.Replace(".ipv6-literal.net", "").Replace(":::", "::").Replace("-", ":").Replace("s", "%").Trim();
            }
            return IP;
        }

        static Dictionary<string, string> DNSErrCache = new Dictionary<string, string>();
        public static IPAddress GetIPAddressFromHostname(string HostNameOrIPAddress = "")
        {
            IPAddress ret = IPAddress.None;
            try
            {
                //stop any errors from happening more than once since it may take a long time to resolve an invalid host:
                if (DNSErrCache.ContainsKey(HostNameOrIPAddress.ToLower()))
                    return ret;

                if (IsValidIPAddress(HostNameOrIPAddress, out IPAddress FoundIP))
                    return FoundIP;

                IPHostEntry Host;
                if (string.IsNullOrWhiteSpace(HostNameOrIPAddress))
                    Host = Dns.GetHostEntry(Dns.GetHostName());
                else
                    Host = Dns.GetHostEntry(HostNameOrIPAddress);

                foreach (IPAddress IP in Host.AddressList)
                {
                    if (IP.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                    {
                        // just return the first one ipv4 address
                        ret = IP;
                        break;
                    }
                }
                if (!IsValidIPAddress(ret, out FoundIP))
                {
                    // fall back to ipv6
                    foreach (IPAddress IP in Host.AddressList)
                    {
                        if (IP.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6)
                        {
                            // just return the first ipv6 address
                            ret = IP;
                            break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ret = IPAddress.None;
                if (!DNSErrCache.ContainsKey(HostNameOrIPAddress.ToLower()))
                    DNSErrCache.Add(HostNameOrIPAddress.ToLower(), ex.Message);

                Log($"Error: Hostname '{HostNameOrIPAddress}': {ex.Msg()}");
            }

            return ret;
        }

        public static List<IPAddress> GetAllLocalIPs(NetworkInterfaceType _type)
        {
            List<IPAddress> ipAddrList = new List<IPAddress>();
            try
            {
                foreach (NetworkInterface item in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (item.NetworkInterfaceType == _type && item.OperationalStatus == OperationalStatus.Up)
                    {
                        foreach (UnicastIPAddressInformation ip in item.GetIPProperties().UnicastAddresses)
                        {
                            if (ip.Address.AddressFamily == AddressFamily.InterNetwork)
                            {
                                ipAddrList.Add(ip.Address);
                            }
                        }
                    }
                }
                //fall back to IPV6 if needed
                if (ipAddrList.Count == 0)
                {
                    foreach (NetworkInterface item in NetworkInterface.GetAllNetworkInterfaces())
                    {
                        if (item.NetworkInterfaceType == _type && item.OperationalStatus == OperationalStatus.Up)
                        {
                            foreach (UnicastIPAddressInformation ip in item.GetIPProperties().UnicastAddresses)
                            {
                                if (ip.Address.AddressFamily == AddressFamily.InterNetworkV6)
                                {
                                    ipAddrList.Add(ip.Address);
                                }
                            }
                        }
                    }

                }

                if (ipAddrList.Count == 0)
                    Log($"Error: No IP addresses found for NetworkInterfaceType '{_type}' with Operational status = UP????");

            }
            catch (Exception ex)
            {

                Log("Error: " + ex.Msg());
            }

            return ipAddrList;
        }

        public static async Task<IPAddress> GetIPAddressFromHostnameAsync(string HostNameOrIPAddress = "")
        {
            IPAddress ret = IPAddress.None;
            Stopwatch sw = Stopwatch.StartNew();

            try
            {
                //stop any errors from happening more than once:
                if (DNSErrCache.ContainsKey(HostNameOrIPAddress.ToLower()))
                    return ret;

                if (IsValidIPAddress(HostNameOrIPAddress, out IPAddress FoundIP))
                    return FoundIP;

                IPHostEntry Host;
                if (string.IsNullOrWhiteSpace(HostNameOrIPAddress))
                    Host = await Dns.GetHostEntryAsync(Dns.GetHostName());
                else
                    Host = await Dns.GetHostEntryAsync(HostNameOrIPAddress);

                //prefer ipv4 address
                foreach (IPAddress IP in Host.AddressList)
                {
                    if (IP.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                    {
                        // just return the first ipv4 found
                        ret = IP;
                        break;
                    }
                }
                if (!IsValidIPAddress(ret, out FoundIP))
                {
                    // fall back to ipv6
                    foreach (IPAddress IP in Host.AddressList)
                    {
                        if (IP.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6)
                        {
                            // just return the first ipv6
                            ret = IP;
                            break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ret = IPAddress.None;
                if (!DNSErrCache.ContainsKey(HostNameOrIPAddress.ToLower()))
                    DNSErrCache.Add(HostNameOrIPAddress.ToLower(), ex.Message);

                Log($"Error: Hostname '{HostNameOrIPAddress}': {ex.Msg()}");
            }
            finally
            {
                if (ret != IPAddress.None)
                    Log($"Trace: Resolved host '{HostNameOrIPAddress}' to IP {ret.ToString()} in {sw.ElapsedMilliseconds}ms");
            }

            return ret;
        }
        public static IPAddress GetIPAddressFromIPString(string IP)
        {
            IPAddress ret = IPAddress.None;
            if (IsValidIPAddress(IP, out IPAddress FoundIP))
                return FoundIP;

            return ret;
        }

        public static bool IsValidIPAddress(string IP, out IPAddress FoundIP)
        {
            FoundIP = IPAddress.None;
            if (!string.IsNullOrEmpty(IP))
            {
                IP = CleanIPV6Address(IP);
                if (IPAddress.TryParse(IP, out FoundIP) && !FoundIP.Equals(IPAddress.None) && (FoundIP.AddressFamily == AddressFamily.InterNetwork || FoundIP.AddressFamily == AddressFamily.InterNetworkV6))
                    return true;
            }
            return false;
        }
        public static bool IsValidIPAddress(IPAddress IP, out IPAddress FoundIP)
        {
            FoundIP = IPAddress.None;
            if (IP != null && !IP.Equals(IPAddress.None))
            {
                if (IsValidIPAddress(IP.ToString(), out FoundIP))
                    return true;
            }
            return false;
        }

        public class ClsPingOut
        {
            public bool Success = false;
            public PingReply PingReply = null;
            public long DNSResolveMS = 0;
            public string PingError = "";
            public int Hops = 0;
            public int Retries = 0;
            public long AvgTimeMS = 0;
            public long MaxTimeMS = 0;
            public long MinTimeMS = 0;
            public long TotalTimeMS = 0;
            public List<long> Pings = new List<long>();
        }

        public static async Task<ClsPingOut> IsConnected(string HostOrIPToPing = "www.google.com", int TimeoutMS = 2000, int RetryCount = 3, int DelayMS = 25, bool AlwaysRetry = false)
        {
            ClsPingOut ret = new ClsPingOut();
            Stopwatch SW = Stopwatch.StartNew();

            try
            {
                IPAddress IP = null;

                if (!IsValidIPAddress(HostOrIPToPing, out IP))
                    IP = await GetIPAddressFromHostnameAsync(HostOrIPToPing);

                ret.DNSResolveMS = SW.ElapsedMilliseconds;

                if (!IsValidIPAddress(IP, out IP))
                    return ret;

                Log($"Trace: Pinging {HostOrIPToPing} ({IP.ToString()}) With timeout:{TimeoutMS}ms And Ping Retry Count:{RetryCount}...");
                for (int Tries = 1; Tries <= RetryCount; Tries++)
                {
                    ret.Retries = Tries;
                    byte[] buffer = new byte[32];
                    PingOptions pingOptions = new PingOptions(128, false);

                    int TTLBefore = pingOptions.Ttl;
                    try
                    {
                        using (Ping Myping = new Ping())
                        {
                            ret.PingReply = await Myping.SendPingAsync(IP, TimeoutMS, buffer, pingOptions);
                        }
                    }
                    catch (Exception ex)
                    {
                        ret.PingError = ex.GetBaseException().Message;
                    }
                    if (ret.PingReply != null)
                    {
                        ret.Success = (ret.PingReply.Status == IPStatus.Success);
                        ret.PingError = ret.PingReply.Status.ToString();
                        if (ret.PingReply.Options != null) //ipv6 returns null
                            ret.Hops = TTLBefore - ret.PingReply.Options.Ttl;
                        if (ret.Success)
                        {
                            ret.Pings.Add(ret.PingReply.RoundtripTime);

                            if (!AlwaysRetry) //If we want to get a true ping average, dont break out of the loop yet
                                break;
                        }
                    }

                    // wait before next try
                    await Task.Delay(DelayMS);
                }
            }
            catch (Exception ex)
            {
                ret.PingError = $"{ex.Msg()} (Site={HostOrIPToPing} Timeout was {TimeoutMS}ms)";
                Log($"Error: {ret.PingError}");
            }
            finally
            {
            }

            if (ret.Pings.Count > 0)
            {
                ret.AvgTimeMS = System.Convert.ToInt64(ret.Pings.Average());
                ret.MaxTimeMS = System.Convert.ToInt64(ret.Pings.Max());
                ret.MinTimeMS = System.Convert.ToInt64(ret.Pings.Min());
            }

            SW.Stop();
            ret.TotalTimeMS = SW.ElapsedMilliseconds;

            Log($"Trace: ...Result={ret.Success}, {ret.TotalTimeMS}ms, {ret.PingError}");

            return ret;
        }

        public static HttpContent CreateHttpContentString(object content)
        {
            HttpContent httpContent = null;

            if (content != null)
            {
                var json = Global.GetJSONString(content); //JsonConvert.SerializeObject(content);
                httpContent = new StringContent(json, Encoding.UTF8, "application/json");
            }

            return httpContent;
        }

        public static HttpContent CreateHttpContentStream(object content)
        {
            HttpContent httpContent = null;

            if (content != null)
            {
                var ms = new MemoryStream();
                SerializeJsonIntoStream(content, ms);
                ms.Seek(0, SeekOrigin.Begin);
                httpContent = new StreamContent(ms);
                httpContent.Headers.ContentType = new MediaTypeHeaderValue("application/json");

            }

            return httpContent;
        }
    }
}
