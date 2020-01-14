using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Diagnostics;

namespace Connectivity_Tester
{
    static class ConnectivityTester
    {
        private static double TimeLimitSeconds = 10.0;
        private static int NumPings = 5;

        public static void PrintLocalNetworkConfiguration()
        {
            // A network connection is considered to be available if any network interface is marked "up" and is not a loopback or tunnel interface.
            if (!System.Net.NetworkInformation.NetworkInterface.GetIsNetworkAvailable())
            {
                Console.WriteLine("There's no network connection available!");

                throw new Exception("No network connection available!");
            }

            var computerProperties = System.Net.NetworkInformation.IPGlobalProperties.GetIPGlobalProperties();
            var networkInterfaces = System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces();

            Console.WriteLine($"Interface information for {computerProperties.HostName}.{computerProperties.DomainName}:");

            foreach (var networkInterface in networkInterfaces)
            {
                if (networkInterface.NetworkInterfaceType == System.Net.NetworkInformation.NetworkInterfaceType.Loopback)
                {
                    continue;
                }

                var properties = networkInterface.GetIPProperties();

                Console.WriteLine($"\t\tInterface name: {networkInterface.Name}");
                Console.WriteLine($"\t\tInterface description: {networkInterface.Description}");
                Console.WriteLine($"\t\tInterface type: {networkInterface.NetworkInterfaceType}");
                Console.WriteLine($"\t\tOperational status: {networkInterface.OperationalStatus}");


                Console.WriteLine($"\t\tSupports IPv4: {networkInterface.Supports(System.Net.NetworkInformation.NetworkInterfaceComponent.IPv4)}");
                Console.WriteLine($"\t\tSupports IPv6: {networkInterface.Supports(System.Net.NetworkInformation.NetworkInterfaceComponent.IPv6)}");

                Console.WriteLine("\t\tUnicast address list:");
                if (properties.UnicastAddresses.Count == 0)
                {
                    Console.WriteLine("\t\t\t{empty}");
                }
                else
                {
                    foreach (var ip in properties.UnicastAddresses)
                    {
                        Console.WriteLine($"\t\t\t{ip.Address}");
                    }
                }

                Console.WriteLine("\t\tDNS server address list:");
                if (properties.DnsAddresses.Count == 0)
                {
                    Console.WriteLine("\t\t\t{empty}");
                }
                else
                {
                    foreach (var address in properties.DnsAddresses)
                    {
                        Console.WriteLine($"\t\t\t{address}");
                    }
                }

                Console.WriteLine();
            }
        }

        public static IPAddress[] PrintResolveHost(string host)
        {
            Console.WriteLine($"Resolving {host}:");

            try
            {
                var entry = Dns.GetHostEntry(host);

                Console.WriteLine($"\t{host} resolved to:");

                if (entry.AddressList.Length == 0)
                {
                    throw new Exception($"Error: Failed to resolve {host}!");
                }
                else
                {
                    foreach (var ip in entry.AddressList)
                    {
                        Console.WriteLine($"\t\t{ip}");
                    }
                }

                Console.WriteLine($"\t{host} aliases:");

                if (entry.Aliases.Length == 0)
                {
                    Console.WriteLine("\t\t{none}");
                }
                else
                {
                    foreach (var alias in entry.Aliases)
                    {
                        Console.WriteLine($"\t\t{alias}");
                    }
                }

                Console.WriteLine();

                return entry.AddressList;
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                return null;
            }
        }

        public static IPAddress[] TestSubnetConnectivity(string subnetAddressPrefix, int port, IPAddress[] resolvedAddresses = null)
        {
            string[] split = subnetAddressPrefix.Split('.', '/');

            byte[] ip = { byte.Parse(split[0]), byte.Parse(split[1]), byte.Parse(split[2]), byte.Parse(split[3]) };
            byte mask = byte.Parse(split[4]);

            Console.WriteLine($"Testing connectivity on subnet {subnetAddressPrefix}, port {port}...");

            var successfulConnections = GetSuccessfulConnections(ip, mask, port);

            if (successfulConnections != null)
            {
                Console.WriteLine($"\t{successfulConnections.Count} successful TCP connections established...");

                foreach (var ipAddress in successfulConnections)
                {
                    Console.Write($"\t\t{ipAddress}");
                    if (resolvedAddresses != null && resolvedAddresses.Contains(ipAddress))
                    {
                        Console.Write(" [Host resolved to this address]");
                    }
                    Console.WriteLine();
                }
                Console.WriteLine();

                return successfulConnections.ToArray();
            }
            return null;
        }


        private static LinkedList<IPAddress> GetSuccessfulConnections(byte[] ip, byte mask, int port)
        {
            try
            {
                var clients = new LinkedList<TcpClient>();
                var asyncResults = new Dictionary<TcpClient, IPAddress>();

                var sucConnections = new LinkedList<IPAddress>();

                for (int i = 0; i < (int)Math.Pow(2, 32 - mask); i++)
                {
                    var client = new TcpClient();
                    var ipAddress = new IPAddress(ip);

                    clients.AddLast(client);
                    asyncResults.Add(client, ipAddress);
                    client.BeginConnect(ipAddress, port, null, null);

                    if ((i + 1) % 16777216 == 0)
                    {
                        ip[0]++;
                        ip[1] = ip[2] = ip[3] = 0;
                    }
                    else if ((i + 1) % 65536 == 0)
                    {
                        ip[1]++;
                        ip[2] = ip[3] = 0;
                    }
                    else if ((i + 1) % 256 == 0)
                    {
                        ip[2]++;
                        ip[3] = 0;
                    }
                    else
                        ip[3]++;
                }

                System.Threading.Thread.Sleep(TimeSpan.FromSeconds(TimeLimitSeconds));

                foreach (var client in clients)
                {
                    if (client.Connected)
                    {
                        sucConnections.AddLast(asyncResults[client]);
                    }
                    client.Close();
                }

                return sucConnections;
            }
            catch
            {
                return null;
            }
        }

        public static void PingTCP(string hostname, int port)
        {
            using (var client = new TcpClient())
            {
                if (client.ConnectAsync(hostname, port).Wait(TimeSpan.FromSeconds(TimeLimitSeconds)))
                {
                    Console.WriteLine($"Successful TCP/IP connection established to {hostname}:{port}...");
                }
                else
                {
                    Console.WriteLine($"Unsucessful TCP/IP probe to {hostname}:{port}...");
                }
            }
        }

        private static void MeasureAverageResponseTimeForSuccessfulConnections(IPAddress[] successfulConnections)
        {
            if (successfulConnections != null)
            {
                Console.WriteLine($"Measuring average response time for {NumPings} connection attempts:");

                var stopwatch = new Stopwatch();


                foreach (var ipAddress in successfulConnections)
                {
                    double sum = 0;
                    int numFailed = 0;
                    int numSuccessful = 0;

                    for (int i = 0; i < NumPings; i++)
                    {
                        using (var client = new TcpClient())
                        {
                            try
                            {
                                stopwatch.Restart();
                                client.Connect(ipAddress, 1433);
                                stopwatch.Stop();

                                sum += stopwatch.ElapsedMilliseconds;

                                numSuccessful++;
                            }
                            catch
                            {
                                numFailed++;
                            }
                        }
                    }

                    Console.WriteLine($"\tIP Address: {ipAddress} Successful connections: {numSuccessful} Failed connections: {numFailed} Average response time: {sum / numSuccessful} ms");
                }

                Console.WriteLine();
            }
        }

        public static void Main(String[] args)
        {
            if (args.Length != 2)
            {
                Console.WriteLine("Usage: ConnectivityTester.exe <miSubnetRange> <miHostname>");
                Environment.Exit(-1);
            }
            else if (!Regex.IsMatch(args[0], @"\b\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}/\d{1,2}\b"))
            {
                Console.WriteLine("Invalid IP address range format!");
                Console.WriteLine("Usage: ConnectivityTester.exe <miSubnetRange> <miHostname>");
                Environment.Exit(-2);
            }

            PrintLocalNetworkConfiguration();
            IPAddress[] resolvedAddresses = PrintResolveHost(args[1]);
            IPAddress[] successfulConnections = TestSubnetConnectivity(args[0], 1433, resolvedAddresses);
            MeasureAverageResponseTimeForSuccessfulConnections(successfulConnections);
            PingTCP(args[1], 3342);
        }
    }
}


