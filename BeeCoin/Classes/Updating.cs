using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace BeeCoin
{

    public class Updating: Additional
    {
        private FileSystem directory;
        private FileTransfering filetransfering;
        private Cryptography cryptography;
        private UDPServer server;

        private byte[] update_data = new byte[0];

        public Information info;

        public const int version_size = 20;
        public int size_size = 10;

        public int Initialize(UDPServer main_server, FileSystem main_directory, FileTransfering main_filetransfering, Cryptography main_cryptography, Information main_info, bool main_debug)
        {
            debug = main_debug;
            directory = main_directory;
            server = main_server;
            filetransfering = main_filetransfering;
            cryptography = main_cryptography;
            info = main_info;

            //window.WriteLine("class Updating: initialized");
            return 1;
        }

        public async Task CheckForUpdate()
        {
            IPAddress address;
            IPEndPoint target;
            int port = Convert.ToInt32(server.Port);
            byte[] operation_bytes;

            List<string> black_list = new List<string>(0);
            List<string> list_to = await server.GetKnownList();

            list_to = server.RemoveIPAddresses(list_to, black_list);

            operation_bytes = server.OperationToBytes("update|tell", UDPServer.operation_size);

            if (list_to.Count > 0)
            {
                int timeout = 0;
                foreach (var ip_address in list_to)
                {
                    address = IPAddress.Parse(ip_address);
                    target = new IPEndPoint(address, port);

                    await server.Send(target, operation_bytes);
                    timeout++;
                    window.WriteLine("Updating..." + timeout);


                    if (timeout % 20 == 0)
                        await Task.Delay(100);
                }
            }
        }

        public async Task UpdateLogic(string operation, byte[] data, IPEndPoint source)
        {
            byte[] local_data;
            string full_packet_size = string.Empty;
            window.WriteLine("OP is: " + operation);

            switch (operation)
            {
                case "tell":
                    full_packet_size = Convert.ToString(version_size + info.self_size);
                    local_data = server.AddOperation(full_packet_size, size_size, info.signature);
                    local_data = server.AddOperation(info.version, version_size, local_data);
                    local_data = server.AddOperation("update|info", UDPServer.operation_size, local_data);
                    await server.Send(source, local_data);
                    break;

                case "info":
                    await CheckVersion(data, source);

                    break;

                case "give":
                    //window.WriteLine("LocalIP: " + info.ip);
                    //IPEndPoint local = new IPEndPoint(IPAddress.Parse(info.ip), info.port);
                    IPAddress local_ip = IPAddress.Parse(Encoding.UTF8.GetString(data));

                    window.WriteLine("TCP running: " + local_ip + ":" + server.Port);

                    IPEndPoint local = new IPEndPoint(0, server.Port);

                    local_data = await directory.GetFromFileAsync(info.self_location);
                    local_data = AddOperation(info.version, version_size, local_data);
                    StartUpdateServer(local, source, local_data);
                    break;

                default:
                    break;
            }
        }
        
        // версия размер(версия+подпись) подпись(версия+информация)

        private async Task CheckVersion(byte[] data, IPEndPoint source)
        {

            string version;
            byte[] signature = new byte[0];
            byte[] last_data;
            string file_size = string.Empty;

            TwoBytesArrays temp = new TwoBytesArrays();

            temp = ByteArrayCut(data, version_size);
            version = BytesToOperation(temp.part1);
            last_data = temp.part2;

            temp = ByteArrayCut(last_data, size_size);
            file_size = BytesToOperation(temp.part1);
            signature = temp.part2;

            window.WriteLine("Siganture: " + cryptography.HashToString(signature));

            if (String.CompareOrdinal(version, info.version) > 0)
            {
                
                last_data = Encoding.UTF8.GetBytes(source.Address.ToString());
                last_data = AddOperation("update|give", UDPServer.operation_size, last_data);
                await server.Send(source, last_data);
                await UpdateSelf(file_size, signature, source, version);
            }
            else
            {
                window.WriteLine("no updates");
            }
        }

        private void StartUpdateServer(IPEndPoint local, IPEndPoint target, byte[] update)
        {

            try
            {
                window.WriteLine("Server will send: " + update.Length);
                window.WriteLine("Hash: " + cryptography.HashToString(cryptography.GetSHA256Hash(update)));
                window.WriteLine("Signed_hash: " + cryptography.HashToString(info.signed_hash));
                window.WriteLine("exe hash: " + cryptography.HashToString(info.exe_hash));

                filetransfering.TcpDataSend(update);
            }
            catch (Exception e)
            {
                window.WriteLine(e.ToString());
            }
        }


        // версия+информация
        private async Task UpdateSelf(string size_str, byte[] signature, IPEndPoint source, string new_version)
        {
            try
            {
                window.WriteLine("Client pending: " + size_str);
                window.WriteLine("nv: " + new_version);

                int size = Convert.ToInt32(size_str);
                byte[] buffer = await filetransfering.TcpDataGet(source, size);

                byte[] hash = cryptography.GetSHA256Hash(buffer);

                window.WriteLine("Client got: " + buffer.Length + " bytes");

                TwoBytesArrays temp = new TwoBytesArrays();
                temp = ByteArrayCut(buffer, version_size);
                string version = string.Empty;

                version = BytesToOperation(temp.part1);

                window.WriteLine("Version " + version +" ready");
                window.WriteLine("Client: " + temp.part1.Length + " + " + temp.part2.Length + " = " + (temp.part1.Length + temp.part2.Length));
                window.WriteLine("Siganture: " + cryptography.HashToString(signature));
                window.WriteLine("Hash to check: " + cryptography.HashToString(hash));

                // hash(version[15] + hash(row_data)[64]);

                window.WriteLine("exe hash: " + cryptography.HashToString(cryptography.GetSHA256Hash(temp.part2)) );

                if (cryptography.VerifySign(hash, signature, Information.admin_public_key))
                {
                    window.WriteLine("Update to version: " + version + " started");
                    update_data = temp.part2;
                    window.WriteLine("Update to : " + update_data.Length);

                    window.ShowUpdateAvailable(version, update_data);
                }
                else
                {
                    window.WriteLine("Wrong signature");
                }
            }
            catch (Exception e)
            {
                window.WriteLine(e.ToString());
            }

        }

        public void StartUpdateSelf(byte[] update)
        {
            Updater(update);
        }

        private void Updater(byte[] buffer)
        {
            try
            {
                server.socket.Close();
                string self = System.Reflection.Assembly.GetExecutingAssembly().Location;
                string current_directory = Path.GetDirectoryName(self);

                string correct_path = directory.FSConfig.root_path + @"\" + "BeeCoin.exe";
                string update_path = directory.FSConfig.temp_path + @"\Update.exe";

                FileStream fs = new FileStream(update_path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite, buffer.Length, true);

                fs.WriteAsync(buffer, 0, buffer.Length);
                fs.FlushAsync();
                fs.Close();

                string updater_path = directory.FSConfig.temp_path + @"\Updater.bat";
                if (debug)
                {
                    window.WriteLine(updater_path);
                    window.WriteLine(update_path);
                    window.WriteLine(correct_path);
                    window.WriteLine(current_directory);
                }

                StreamWriter batUpdater = new StreamWriter(File.Create(updater_path));
                batUpdater.WriteLine("@ECHO OFF");
                batUpdater.WriteLine("TIMEOUT /t 3 /nobreak > NUL");
                batUpdater.WriteLine("TASKKILL /IM \"{0}\" > NUL", self);
                batUpdater.WriteLine("MOVE \"{0}\" \"{1}\"", update_path, correct_path);
                batUpdater.WriteLine("DEL \"%~f0\" & START \"\" \"{0}\"", correct_path);

                batUpdater.Flush();
                batUpdater.Close();

                ProcessStartInfo startInfo = new ProcessStartInfo(updater_path);
                // Hide the terminal window
                startInfo.CreateNoWindow = true;
                startInfo.UseShellExecute = false;
                //startInfo.WorkingDirectory = directory;
                Process.Start(startInfo);

                Environment.Exit(0);
            }
            catch (Exception e)
            {
                window.WriteLine(e.ToString());
            }

        }
    }
}
