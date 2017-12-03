using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Security.Cryptography;
using System.IO;
using System.Diagnostics;

namespace BeeCoin
{
    public class Cryptography: Additional
    {
        FileSystem filesystem;

        private string private_key;
        public string public_key;
        RSACryptoServiceProvider RsaCryptoService = new RSACryptoServiceProvider();

        public int Initialize(FileSystem main_filesystem, bool main_debug)
        {
            RsaCryptoService = new RSACryptoServiceProvider();
            filesystem = main_filesystem;
            debug = main_debug;

            //window.WriteLine("class Cryptography: initialized");
            return 1;
        }

        public byte[] Enrypt(byte[] data)
        {
            byte[] result = new byte[0];
            result = RsaCryptoService.Encrypt(data, true);
            return result;
        }

        public byte[] Decrypt(byte[] data)
        {
            byte[] result = new byte[0];

            result = RsaCryptoService.Decrypt(data, true);
            return result;
        }

        public string CalculateMD5Hash(string input)

        {
            // step 1, calculate MD5 hash from input
            MD5 md5 = MD5.Create();

            byte[] inputBytes = Encoding.UTF8.GetBytes(input);

            byte[] hash = md5.ComputeHash(inputBytes);


            // step 2, convert byte array to hex string
            StringBuilder sb = new StringBuilder();

            for (int i = 0; i < hash.Length; i++)
            {
                sb.Append(hash[i].ToString("X2"));
            }

            return sb.ToString();
        }

        public string GetHashString(string data)
        {
            byte[] data_byte = Encoding.UTF8.GetBytes(data);
            data_byte = GetSHA256Hash(data_byte);

            return HashToString(data_byte);
        }

        public byte[] GetSHA256Hash(byte[] data)
        {
            byte[] result = new byte[0];

            SHA256Managed hash_function = new SHA256Managed();

            result = hash_function.ComputeHash(data);

            return result;
        }

        public string HashToString(byte[] data)
        {
            string result = string.Empty;

            foreach (byte x in data)
            {
                result += String.Format("{0:x2}", x);
            }

            return result;
        }
        
        /// <summary>
        /// Подпись информации RSA
        /// </summary>
        /// <param name="data">информация</param>
        /// <param name="private_key">приватный ключ XML</param>
        /// <returns>Подпись</returns>
        public byte[] Sign(byte[] data, string private_key)
        {

            byte[] result = new byte[0];
            try
            {
                RSACryptoServiceProvider RsaCryptoService_local = new RSACryptoServiceProvider();
                RsaCryptoService_local.FromXmlString(private_key);
                result = RsaCryptoService_local.SignData(data, new SHA256CryptoServiceProvider());
                Debug.WriteLine("data (sign): " + HashToString(GetSHA256Hash(data)));
            }
            catch(Exception ex)
            {
                window.WriteLine("Exception on: Cryptography.Sign");
                window.WriteLine(ex.ToString());
            }
            return result;
        }

        /// <summary>
        /// Проверка подписи RSA
        /// </summary>
        /// <param name="data">информация</param>
        /// <param name="signed_data">подпись</param>
        /// <param name="public_key">публичный ключ XML</param>
        /// <returns>верна/нет</returns>
        public bool VerifySign(byte[] data, byte[] signed_data, string public_key)
        {
            bool result = false;
            try
            {
                RSACryptoServiceProvider RsaCryptoService_local = new RSACryptoServiceProvider();
                RsaCryptoService_local.FromXmlString(public_key);
                result = RsaCryptoService_local.VerifyData(data, new SHA256CryptoServiceProvider(), signed_data);
                Debug.WriteLine("data (verify): " + HashToString(GetSHA256Hash(data)));
            }
            catch (Exception ex)
            {
                window.WriteLine("Exception on: Cryptography.VerifySign");
                window.WriteLine(ex.ToString());
            }
            return result;
        }


        #region Wallet
        private int header_size = 32;

        // HEADER: 1|username (16)|private_key size in bytes| walletdata

        public async Task MaketheWallet(string wallet_path, string username)
        {
            try
            {
                byte[] wallet = new byte[0];
                string header;
                string key;

                TwoBytesArrays temp = new TwoBytesArrays();
                key = RsaCryptoService.ToXmlString(true);
                temp.part1 = Encoding.UTF8.GetBytes(key);

                key = RsaCryptoService.ToXmlString(false);
                temp.part2 = Encoding.UTF8.GetBytes(key);

                wallet = ByteArrayJoin(temp);

                header = "1|" + username + "|" + temp.part1.Length;
                wallet = AddOperation(header, header_size, wallet);

                wallet_path = wallet_path + @"\wallet";
                int r = await filesystem.AddInfoToFileAsync(wallet_path, wallet, true);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }
        
        public async Task<List<string>> OpentheWallet(string path)
        {
            List<string> result = new List<string>(0);

            if(File.Exists(path))
            {
                string header = string.Empty;
                byte[] keys;
                TwoBytesArrays temp = new TwoBytesArrays();
                List<string> sizes = new List<string>();
                byte[] wallet = new byte[0];

                wallet = await filesystem.GetFromFileAsync(path);
                temp = ByteArrayCut(wallet, header_size);
                header = BytesToOperation(temp.part1);
                keys = temp.part2;

                if (header[0] == '1')
                    //Console.WriteLine("Correct password");

                sizes.AddRange(header.Split('|'));

                temp = ByteArrayCut(temp.part2, Convert.ToInt32(sizes[2]));

                private_key = BytesToOperation(temp.part1);
                public_key = BytesToOperation(temp.part2);
                /*
                Console.WriteLine(private_key);
                Console.WriteLine("===========================");
                Console.WriteLine(public_key);
                */
                RsaCryptoService.FromXmlString(private_key);
                RsaCryptoService.FromXmlString(public_key);

                result.Add(sizes[1]);
                result.Add(private_key);
                result.Add(public_key);
            }

            return result;
        }
        #endregion
    }
}
