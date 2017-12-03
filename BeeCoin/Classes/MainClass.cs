using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Net;
using System.Diagnostics;

namespace BeeCoin
{
    public class MainClass
    {
        /*

        public FileSystem filesystem;
        public UDPServer server;
        public Cryptography cryptography;
        public Mining mine;
        public Information info;
        public FileTransfering filetransfering;
        public AdminClass admin;
        public Updating update;*/

        public FileSystem filesystem = new FileSystem();
        public UDPServer server = new UDPServer();
        public Cryptography cryptography = new Cryptography();
        public Blocks mine = new Blocks();
        public Information info = new Information();
        public FileTransfering filetransfering = new FileTransfering();
        public AdminClass admin = new AdminClass();
        public Updating update = new Updating();
        public Blocks blocks = new Blocks();
        public Transactions transactions = new Transactions();
        public GetLogic getlogic = new GetLogic();

        public MainWindow window;

        public async Task Initialize(MainWindow main_window)
        {
            try
            {
                Debug.WriteLine("Program started");
                window = main_window;
                bool refresh = false;
                bool debug = false;

                filesystem.WindowInit(window);
                server.WindowInit(window);
                cryptography.WindowInit(window);
                mine.WindowInit(window);
                info.WindowInit(window);
                filetransfering.WindowInit(window);
                admin.WindowInit(window);
                update.WindowInit(window);
                blocks.WindowInit(window);
                transactions.WindowInit(window);
                getlogic.WindowInit(window);

                int count = 0;

                count += filesystem.Initialize();
                await filesystem.CreateFSAsync(refresh);

                count += cryptography.Initialize(filesystem, debug);
                count += server.Initialize(server, filesystem, filetransfering, cryptography, blocks, transactions, update, getlogic, admin, debug);
                count += update.Initialize(server, filesystem, filetransfering, cryptography, info, debug);
                count += admin.Initialize(info, filesystem, server, debug);
                count += await info.Initialize(cryptography, mine, filesystem, server, debug);
                count += transactions.Initialize(filesystem, server, cryptography, blocks, debug);
                count += blocks.Initialize(cryptography, filetransfering, filesystem, server, getlogic, transactions, debug);
                count += getlogic.Initialize(filesystem, server.socket, filetransfering, cryptography, server, info,blocks, debug);


                window.WriteLine("[" + count + "/9] classes initialized");
                window.WriteLine("class MainClass: initialized (ver. " + info.version + ")");
            }
            catch (Exception e)
            {
                Debug.WriteLine("class MainClass: FAILED");
                Debug.WriteLine(e.ToString());
            }
        }

        public async Task StartAll()
        {
            try
            {
                await server.StartServer();

                window.Switch_status(1);

                //await Scan(50);
                await Send("oleg", info.version);

                await Actualize();
            }
            catch (Exception e)
            {
                Debug.WriteLine("Starting: FAILED");
                Debug.WriteLine(e.ToString());
            }
        }

        public async Task Actualize()
        {
            IPAddress senpai = IPAddress.Parse("78.139.208.149");
            int port = 6879;

            IPEndPoint sen = new IPEndPoint(senpai, port);
            byte[] message;

            if (!window.admin)
            {
                message = Encoding.UTF8.GetBytes("signature");
                message = server.AddOperation(getlogic.request_template, UDPServer.operation_size, message);

                await server.Send(sen, message);
            }
            
            
            await blocks.BlockChainActualize();

            window.WriteLine("Actualization started");
            await info.ActualizeSelfSignature();
        }

        public async Task Scan(int size = 10)
        {
            try
            {
                string operation = "registration|1";
                byte[] operation_bytes = Encoding.UTF8.GetBytes(operation);

                List<string> black_list = new List<string>(0);
                string senpai = "78.139.208.149";

                IPAddress address = IPAddress.Parse(senpai);
                int port = Convert.ToInt32(server.Port);
                IPEndPoint target = new IPEndPoint(address, port);

                await server.Send(target, operation_bytes);

                List<string> list_to_connect = new List<string>(0);

                foreach (var ip_gateway in server.GetGatewayAddresses())
                {
                    IPAddress temp_ip = IPAddress.Parse(ip_gateway);
                    byte[] bytes;


                    for (int i = 0; i < size; i++)
                    {
                        bytes = temp_ip.GetAddressBytes();
                        if (++bytes[3] == 0)
                            if (++bytes[2] == 0)
                                if (++bytes[1] == 0)
                                    ++bytes[0];

                        temp_ip = new IPAddress(bytes);
                        list_to_connect.Add(temp_ip.ToString());
                    }
                }
                list_to_connect = server.RemoveIPAddresses(list_to_connect, black_list);

                if (list_to_connect.Count > 0)
                {
                    int timeout = 0;
                    foreach (var ip_address in list_to_connect)
                    {
                        port = Convert.ToInt32(server.Port);
                        address = IPAddress.Parse(ip_address);
                        target = new IPEndPoint(address, port);

                        await server.Send(target, operation_bytes);

                        timeout++;

                        if (timeout % 20 == 0)
                            await Task.Delay(100);
                    }
                }
            }
            catch (Exception e)
            {
                window.RichTextBox_Console.AppendText(e.ToString());
            }
        }

        public async Task Send(string address, string message)
        {
            try
            {
                if (address == "oleg") address = "78.139.208.149";
                if (address == "me") address = "127.0.0.1";

                IPAddress ip = IPAddress.Parse(address);
                int port = Convert.ToInt32(server.Port);

                IPEndPoint destination = new IPEndPoint(ip, port);

                byte[] operation_bytes = server.AddOperation("message", UDPServer.operation_size, Encoding.UTF8.GetBytes(message));

                await server.Send(destination, operation_bytes);
            }
            catch (Exception e)
            {
                window.WriteLine(e.ToString());
            }
        }


        #region HOME
        public int CountLocalTransactions()
        {
            int result = 0;
            try
            {
                List<string> temp = new List<string>(0);

                temp = filesystem.GetFilesListFromDirectory(filesystem.FSConfig.db_path);
                result = temp.Count;
            }
            catch(Exception ex)
            {
                Debug.WriteLine(ex.ToString());
            }
            return result;
        }

        public int CountLocalBlocks()
        {
            int result = 0;
            List<string> temp = new List<string>(0);
            temp = filesystem.GetFilesListFromDirectory(filesystem.FSConfig.db_blocks_path);
            result = temp.Count;

            if (result > 0) result--;

            return result;
        }

        public async Task<int> CountGlobalTransactions()
        {
            int result = 0;

            string current = await blocks.ActualBlockGet();

            if (current.Length != 0)
            {

                bool trigger = true;

                while (trigger)
                {
                    byte[] temp = await blocks.SearchBlock(current);

                    if (current.Length != 0)
                    {

                        Additional.Block block = new Additional.Block();

                        block = blocks.BlockDeSerialize(temp);

                        result += block.transactions_count;

                        if (block.previous == Blocks.first_block)
                            trigger = false;
                        current = block.previous;
                    }
                    else
                    {
                        trigger = false;
                    }
                }
            }
            return result;
        }

        public int CountGlobalBlocks()
        {
            int result = 0;
            List<string> temp = new List<string>(0);

            temp = filesystem.GetFilesListFromDirectory(filesystem.FSConfig.db_path);
            result = temp.Count;

            if (result > 0) result--;
            return result;
        }
        #endregion
    }
}
