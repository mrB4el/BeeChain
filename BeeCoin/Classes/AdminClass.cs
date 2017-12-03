using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace BeeCoin
{
    public class AdminClass: Additional
    {
        private string public_key = "<RSAKeyValue><Modulus>5p3RuMqNJyGNI+hZ6Vr13wmNStg5qUrxJhNA1agZNoTrz4jAzRUES68uA1nZIqjqKUQjVx+q/tqWUXfZYlJv4ixiKDUxehiSUA8EBsQSIGnVdaL2iAeWg6Os6qPijk9cVAWTVIzcGr5GcCWXzht1ZkJQ6Yo6yAFo2I9H4B+H3Lk=</Modulus><Exponent>AQAB</Exponent></RSAKeyValue>";

        private RSACryptoServiceProvider RsaCryptoService = new RSACryptoServiceProvider();

        private FileSystem directory;
        private Information info;
        private UDPServer server;

        public const int command_size = 10;

        public int Initialize(Information main_info, FileSystem main_directory, UDPServer main_server, bool main_debug)
        {
            directory = main_directory;
            info = main_info;
            debug = main_debug;
            server = main_server;

            //window.WriteLine("class AdminClass: initialized");

            return 1;
        }

        public void MakeVersionFile()
        {
            //string publicKeyString = Convert.ToBase64String();

            //byte[] publicKeyBytes = Convert.FromBase64String(publicKeyString);
        }
        public async Task SignCurrentVersion(string root_private)
        {
            byte[] signature = MakeAdminSignature(info.signed_hash, root_private);

            string path = directory.FSConfig.root_path + @"\signature";

            await directory.AddInfoToFileAsync(path, signature, true);
        }

        public byte[] MakeAdminSignature(byte[] data, string root_private)
        {
            byte[] result = new byte[0];

            RsaCryptoService.FromXmlString(root_private);

            result = RsaCryptoService.SignData(data, new SHA256CryptoServiceProvider());

            return result;
        }

        public bool CheckAdminSignature(byte[] data, byte[] signed_data)
        {
            bool result = false;

            RsaCryptoService.FromXmlString(public_key);

            result = RsaCryptoService.VerifyData(data, new SHA256CryptoServiceProvider(), signed_data);

            return result;
        }

        public async Task DirectControlCommand(string aim, string command, string root_private)
        {
            try
            {
                int port = server.Port;
                IPAddress address;
                IPEndPoint target;
                int timeout = 0;

                byte[] command_bytes = Encoding.UTF8.GetBytes(command);
                byte[] signature = MakeAdminSignature(command_bytes, root_private);

                byte[] operation = AddOperation(command, AdminClass.command_size, signature);

                operation = AddOperation("admin", UDPServer.operation_size, operation);

                if (aim == null)
                {
                    List<string> black_list = new List<string>(0);
                    List<string> list_to = await server.GetKnownList();
                    list_to = server.RemoveIPAddresses(list_to, black_list);

                    foreach (var ip_address in list_to)
                    {

                        address = IPAddress.Parse(ip_address);
                        target = new IPEndPoint(address, port);

                        await server.Send(target, operation);

                        timeout++;
                        window.WriteLine("Updating..." + target.ToString());

                        if (timeout % 20 == 0)
                            await Task.Delay(100);
                    }

                }
                else
                {
                    address = IPAddress.Parse(aim);
                    target = new IPEndPoint(address, port);

                    window.WriteLine("Updating..." + target.ToString());
                    await server.Send(target, operation);
                }
            }
            catch(Exception ex)
            {
                window.WriteLine("Exception in AdminClass.DirectUpdate");
                window.WriteLine(ex.ToString());
            }
        }
    }
}
