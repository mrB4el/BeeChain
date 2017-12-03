using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Security.Cryptography;
using System.Collections.Generic;
using System.Net.NetworkInformation;
using System.Text.RegularExpressions;
using System.Linq;

namespace BeeCoin
{
    public class UDPServer : Additional
    {
        public FileSystem directory;
        public Cryptography cryptography;
        private Updating update;
        public AdminClass admin;

        public Transactions transactions;
        private Blocks blocks;

        GetLogic getlogic;
        AdminLogic adminlogic;


        private IPAddress ipAddress;
        private int port;
        public UdpClient socket;

        public const int operation_size = 16;
        public List<string> myIpAddresses = new List<string>(0);

        public IPAddress IpAddress
        {
            get { return ipAddress; }
            set { if (ipAddress == default(IPAddress)) ipAddress = IPAddress.Parse("127.0.0.1"); else ipAddress = value; }
        }
        public int Port
        {
            get { return port; }
            set { if (port != default(int)) port = 6879; else port = value; }
        }

        public int Initialize(UDPServer main_server, FileSystem main_directory, FileTransfering main_filetransfering, Cryptography main_cryptography, Blocks main_blocks, Transactions main_transactions, Updating main_update, GetLogic ex_getlogic, AdminClass main_admin, bool main_debug)
        {
            debug = main_debug;
            directory = main_directory;
            cryptography = main_cryptography;
            update = main_update;
            admin = main_admin;
            blocks = main_blocks;
            transactions = main_transactions;
            getlogic = ex_getlogic;
            adminlogic = new AdminLogic(directory, socket, window, update, admin);

            //window.WriteLine("class UDPServer: initialized");
            return 1;
        }

        /* LOCAL SERVER */
        public async Task StartServer()
        {
            try
            {
                Port = 6879;
                socket = new UdpClient(port);
                await MyIPScan();
                socket.BeginReceive(new AsyncCallback(OnUdpData), socket);

                window.WriteLine("Server started");
            }
            catch(Exception ex)
            {
                window.WriteLine(ex.ToString());
            }
        }

        private async Task MyIPScan()
        {
            myIpAddresses = await GetMyIpAddresses();
        }

        private async void OnUdpData(IAsyncResult result)
        {
            // this is what had been passed into BeginReceive as the second parameter:
            //socket = result.AsyncState as UdpClient;
            // points towards whoever had sent the message:
            IPEndPoint source = new IPEndPoint(0, port);


            // get the actual message and fill out the source:
            try
            {
                byte[] message = socket.EndReceive(result, ref source);

                string operation_str = "status";


                TwoBytesArrays data = new TwoBytesArrays();
                data = ByteArrayCut(message, UDPServer.operation_size);

                operation_str = BytesToOperation(data.part1);

                string[] operations = operation_str.Split('|');
                
                
                foreach (var operation in operations)
                    window.WriteLine("[operation]: " + operation);
                

                switch (operations[0])
                {
                    case "status":
                        ReturnStatus(source);
                        window.WriteLine("errrr");
                        break;
                    case "registration":
                        List<string> reg_ip = new List<string>(1) { source.Address.ToString() };
                        reg_ip = RemoveIPAddresses(reg_ip, myIpAddresses);

                        window.WriteLine(operations[0] + @"|" + operations[1] + " from " + source);

                        if (reg_ip.Count > 0)
                        {
                            await AddToKnownList(reg_ip);

                            if (operations[1] == "1")
                            {
                                byte[] temp = AddOperation("registration|0", operation_size, Encoding.UTF8.GetBytes("123456"));
                                await Send(source, temp);
                            }
                        }
                        break;
                    case "update":
                        await update.UpdateLogic(operations[1], data.part2, source);
                        break;
                    case "newupdate":
                        socket.Close();
                        window.WriteLine("New update from: " + source.Address.ToString());

                        break;
                    case "transaction":
                        await transactions.GetLogic(source, data.part2);
                        break;
                    case "message":
                        ShowMessage(source, data.part2);
                        break;
                    case "get":
                        await getlogic.SwitchLogic(operations[1], data.part2, source);
                        break;
                    case "block":
                        await blocks.Logic(operations, data.part2, source);
                        break;
                    case "admin":
                        window.WriteLine("data2: " + data.part2.Length.ToString());
                        await adminlogic.SwitchLogic(data.part2, source);
                        break;
                    default:
                        break;
                }
                
                // schedule the next receive operation once reading is done:
                socket.BeginReceive(new AsyncCallback(OnUdpData), socket);
            }
            catch (Exception e)
            {
                window.WriteLine("Exception in: Server.UDPServer.OnUdpData");
                window.WriteLine(e.ToString());
            }
        }

        public void ShowMessage(IPEndPoint source, byte[] data)
        {
            window.WriteLine("[" + source.Address.ToString() + "]: " + Encoding.UTF8.GetString(data));
        }

        public async Task Send(IPEndPoint target, byte[] data)
        {
            try
            {
                window.WriteLine("[UDP]: sending " + Encoding.UTF8.GetString(data) + " to " + target.ToString());
                await socket.SendAsync(data, data.Length, target);
            }
            catch(Exception ex)
            {
                window.WriteLine(ex.ToString());
            }
        }

        public async Task SendGlobal(byte[] data)
        {
            try
            {
                int port = Port;
                IPAddress address;
                IPEndPoint target;
                int timeout = 0;

                List<string> black_list = new List<string>(0);
                List<string> list_to = await GetKnownList();

                if (myIpAddresses != null)
                    black_list.AddRange(myIpAddresses);
                black_list.Add("192.168.1.99");

                list_to = RemoveIPAddresses(list_to, black_list);

                window.WriteLine("[Mass sending]: " + Encoding.UTF8.GetString(data));

                foreach (var ip_address in list_to)
                {

                    address = IPAddress.Parse(ip_address);
                    target = new IPEndPoint(address, port);

                    await Send(target, data);

                    timeout++;
                    window.WriteLine("[Mass sending]: " + target.ToString());

                    if (timeout % 20 == 0)
                        await Task.Delay(100);
                }
                window.WriteLine("[Mass sending]: completed");
            }
            catch (Exception ex)
            {
                window.WriteLine(ex.ToString());
            }
        }

        /* NETWORK */
        public async Task AddToKnownList(List<string> ip_list)
        {
            try
            {
                List<string> current = await GetKnownList();

                /*window.WriteLine("Knownlist (from add): ");

                foreach (var sad in current)
                {
                    window.WriteLine(sad);
                }
                window.WriteLine("==============================");*/

                if (!(current.IndexOf(ip_list[0]) >= 0))
                {

                    List<string> black_list = new List<string>(0);
                    List<string> toWrite = new List<string>(0);

                    if(myIpAddresses != null)
                        black_list.AddRange(myIpAddresses);

                    /*window.WriteLine("Blacklist:");
                    foreach(var iasd in myIpAddresses)
                    {
                        window.WriteLine(iasd);
                    }
                    window.WriteLine("==============================");*/

                    current = RemoveIPAddresses(current, black_list);
                    ip_list = RemoveIPAddresses(ip_list, black_list);

                    toWrite.AddRange(current);

                    if (ip_list.Count != 0)
                    {
                        foreach (var ip in ip_list)
                        {
                            bool trigger = true;

                            for (int i = 1; i < current.Count; i++)
                            {
                                if (current[i].Contains(ip)) trigger = false;
                            }
                            if (trigger)
                                toWrite.Add(ip);
                        }
                    }
                    string fulltext = String.Join("|", toWrite);
                    byte[] data = System.Text.Encoding.UTF8.GetBytes(fulltext);
                    //window.WriteLine("To write: " + fulltext);

                    string path = directory.FSConfig.config_path + @"\hosts";

                    await directory.AddInfoToFileAsync(path, data, true);
                }
            }
            catch (Exception ex)
            {
                window.WriteLine("Exception in: Server.AddToKnownList");
                window.WriteLine(ex.ToString());
            }
        
        }
        public async Task<List<string>> GetKnownList()
        {
            List<string> strings_res = new List<string>(0);

            try
            {
                string path = directory.FSConfig.config_path + @"\hosts";

                byte[] result = await directory.GetFromFileAsync(path);

                if (result.Length == 0)
                {
                    await directory.CheckFiles();
                    result = await directory.GetFromFileAsync(path);
                }

                string string_res = Encoding.UTF8.GetString(result);

                strings_res.AddRange(string_res.Split('|'));
            }
            catch (Exception ex)
            {
                window.WriteLine("Exception in: Server.GetKnownList");
                window.WriteLine(ex.ToString());
            }
            return strings_res;
        }

        public List<string> RemoveIPAddresses(List<string> ip_list, List<string> b_list)
        {
            List<string> black_list = new List<string>(0);
            try
            {
                List<string> GatewayAddresses = GetGatewayAddresses();

                if (GatewayAddresses != null)
                    black_list.AddRange(GetGatewayAddresses());

                if (b_list != null)
                    black_list.AddRange(b_list);

                for (int i = 0; i < black_list.Count; i++)
                {
                    ip_list.Remove(black_list[i]);
                }
            }
            catch (Exception ex)
            {
                window.WriteLine("Exception in: Server.RemoveIPAddresses");
                window.WriteLine(ex.ToString());
            }
            return ip_list;
        }

        public List<string> GetGatewayAddresses()
        {
            List<string> result = new List<string>(0);
            try
            {
               
                NetworkInterface[] adapters = NetworkInterface.GetAllNetworkInterfaces();
                foreach (NetworkInterface adapter in adapters)
                {
                    IPInterfaceProperties adapterProperties = adapter.GetIPProperties();
                    GatewayIPAddressInformationCollection addresses = adapterProperties.GatewayAddresses;
                    if (addresses.Count > 0)
                    {
                        foreach (GatewayIPAddressInformation address in addresses)
                        {

                            if (!(address.Address.ToString().IndexOf(':') >= 0))
                                result.Add(address.Address.ToString());
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                window.WriteLine("Exception in: Server.GetGatewayAddresses");
                window.WriteLine(ex.ToString());
            }
            return result;
        }

        public async Task<List<string>> GetMyIpAddresses()
        {
            List<string> ip_s = new List<string>(0);
            myIpAddresses = new List<string>(0);

            try
            {
                string externalIP;
                WebClient me = new WebClient();
                Uri ddns = new Uri(@"http://checkip.dyndns.org/");
                externalIP = await me.DownloadStringTaskAsync(ddns);

                externalIP = (new Regex(@"\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}"))
                             .Matches(externalIP)[0].ToString();
                myIpAddresses.Add(externalIP);

                window.Switch_status(2);
            }
            catch(Exception ex)
            {
                window.Switch_status(1);
            }
            try
            {
                var host = Dns.GetHostEntry(Dns.GetHostName());

                myIpAddresses.Add("127.0.0.1");
                myIpAddresses.Add("192.168.1.56");

                foreach (var ip in host.AddressList)
                {
                    if (ip.AddressFamily == AddressFamily.InterNetwork)
                    {
                        myIpAddresses.Add(ip.ToString());
                    }
                }

                

                window.WriteLine("GetMyIpAddresses [done]");

                ip_s = myIpAddresses;
            }
            catch(Exception ex)
            {
                window.WriteLine("Exception in: Server.GetMyIpAddresses");
                Debug.WriteLine(ex.ToString());
            }
            return ip_s;
        }

        public void ReturnStatus(IPEndPoint source)
        {
            window.WriteLine(IPAddress.Parse(source.Address.ToString()).ToString());
            //SendMessage(source.Address.ToString(), "hello");
        }

    }

    class AdminLogic : UDPServer
    {
        private FileSystem filesystem;
        private Updating update;


        public AdminLogic(FileSystem ex_filesystem, UdpClient ex_socket, MainWindow ex_window, Updating ex_update, AdminClass ex_admin)
        {
            filesystem = ex_filesystem;
            socket = ex_socket;
            window = ex_window;
            update = ex_update;
            admin = ex_admin;
        }

        public async Task SwitchLogic(byte[] incoming_data, IPEndPoint source)
        {
            try
            {
                string operation = string.Empty;
                string path = string.Empty;
                TwoBytesArrays temp = new TwoBytesArrays();

                temp = ByteArrayCut(incoming_data, AdminClass.command_size);

                operation = BytesToOperation(temp.part1);
                temp.part1 = Encoding.UTF8.GetBytes(operation);

                window.WriteLine("[admin]: " + operation);

                if (admin.CheckAdminSignature(temp.part1, temp.part2))
                {
                    switch (operation)
                    {
                        case "update":
                            window.WriteLine("[admin] updating");
                            await update.CheckForUpdate();
                            break;
                        case "scan":
                            window.WriteLine("[admin] scanning");
                            break;
                        default:
                            break;
                    }
                }
                else
                    window.WriteLine("Hacking trying");
                
            }
            catch (Exception ex)
            {
                window.WriteLine(ex.ToString());
            }
        }      
    }
}