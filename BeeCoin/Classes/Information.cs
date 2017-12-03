using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BeeCoin
{
    public class Information: Additional
    {
        public string version;
        public byte[] exe_hash;
        public byte[] signature;
        public byte[] signed_hash;
        public int self_size;

        public string self_location;
        public const string admin_public_key = "<RSAKeyValue><Modulus>5p3RuMqNJyGNI+hZ6Vr13wmNStg5qUrxJhNA1agZNoTrz4jAzRUES68uA1nZIqjqKUQjVx+q/tqWUXfZYlJv4ixiKDUxehiSUA8EBsQSIGnVdaL2iAeWg6Os6qPijk9cVAWTVIzcGr5GcCWXzht1ZkJQ6Yo6yAFo2I9H4B+H3Lk=</Modulus><Exponent>AQAB</Exponent></RSAKeyValue>";
        public int block_version;
        public int transaction_version;
        public int filesystem_version;

        public string ip;
        public int port;

        public Cryptography cryptography;
        FileSystem filesystem;

        public async Task<int> Initialize(Cryptography main_cryptography, Blocks mine, FileSystem main_filesystem, UDPServer server, bool main_debug)
        {
            byte[] data;
            try
            {
                cryptography = main_cryptography;
                filesystem = main_filesystem;
                debug = main_debug;

                version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString();
                self_location = System.Reflection.Assembly.GetExecutingAssembly().Location;

                data = await filesystem.GetFromFileAsync(self_location);
                self_size = data.Length;

                exe_hash = cryptography.GetSHA256Hash(data);

                TwoBytesArrays temp = new TwoBytesArrays();
                temp.part1 = OperationToBytes(version, Updating.version_size);
                temp.part2 = data;

                data = ByteArrayJoin(temp);

                signed_hash = cryptography.GetSHA256Hash(data);

                block_version = Blocks.block_version;

                await ActualizeSelfSignature();

                filesystem_version = filesystem.FSConfig.version;

                port = server.Port;

                //window.WriteLine("class Information: initialized");

                return 1;
            }
            catch(Exception e)
            {
                Debug.WriteLine("class Information: FAILED");
                Debug.WriteLine(e.ToString());
                return 0;
            }
        }
        public bool Self_verify()
        {
            return cryptography.VerifySign(signed_hash, signature, admin_public_key);
        }

        public async Task ActualizeSelfSignature()
        {
            try
            {
                string self_sign_path = filesystem.FSConfig.root_path + @"\signature";
                signature = await filesystem.GetFromFileAsync(self_sign_path);
            }
            catch(Exception e)
            {
                window.WriteLine(e.ToString());
            }
            bool fine = false;

            if (signature.Length == 0)
                window.WriteLine("Self signature checking error");
            else
                fine = Self_verify();

            if (fine)
                window.WriteLine("Current version verified");
            else
                window.WriteLine("Current version isnt verified");
        }
    }
}
