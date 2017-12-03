using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Security.Cryptography;
using System.IO;
using System.Net;
using System.Diagnostics;

namespace BeeCoin
{
    public class Blocks : Additional
    {
        public struct Transaction_info
        {
            public int position;
            public string name;

            public Transaction_info(string ex_name, int ex_position)
            {
                name = ex_name;
                position = ex_position;
            }
        }

        /* BLOCK STRUCTURE
             * [protocol version]: int      // 4 bytes
             * [time]: int64 (ms)           // 4 bytes
             * [previous]: 32 bytes         // 64 bytes
             * [number of transaction]: int // 4 bytes
             * [root_hash]: 64 bytes        // 64 bytes
             * [flowing value]: uint        // 8 bytes
             * ==============================> HEADER = 116 bytes
             * {transactions} tran.size     // 100+
        */

        public const int block_version = 2;
        public const int header_info_size = 4;
        public const int block_length = 64;

        public const string first_block = "bbc5e661e106c6dcd8dc6dd186454c2fcba3c710fb4d8e71a60c93eaf077f073"; // system hash bbc5e661e106c6dcd8dc6dd186454c2fcba3c710fb4d8e71a60c93eaf077f073

        private Cryptography cryptography;
        private FileTransfering filetransfering;
        private FileSystem filesystem;
        private GetLogic getlogic;
        private UDPServer server;
        private Transactions transactions;

        public int Initialize(Cryptography ex_cryptography, FileTransfering ex_filetransfering, FileSystem ex_filesystem, UDPServer ex_server, GetLogic ex_getlogic, Transactions ex_transactions, bool ex_debug)
        {
            cryptography = ex_cryptography;
            debug = ex_debug;
            filesystem = ex_filesystem;
            filetransfering = ex_filetransfering;
            getlogic = ex_getlogic;
            server = ex_server;
            transactions = ex_transactions;

            //window.WriteLine("class Blocks: initialized");
            return 1;
        }

        public async Task Logic(string[] operations, byte[] data, IPEndPoint source)
        {
            string name;
            string path;
            byte[] some_data = new byte[0];

            switch (operations[1])
            {
                case "new":

                    name = Encoding.UTF8.GetString(data);
                    window.WriteLine("Got info about new block: " + name);

                    path = filesystem.FSConfig.db_blocks_path + @"\" + name;
                    if (!File.Exists(name))
                    {
                        some_data = Encoding.UTF8.GetBytes(name);
                        some_data = server.AddOperation("block", GetLogic.operation_size, some_data);
                        some_data = server.AddOperation(getlogic.request_template, UDPServer.operation_size, some_data);

                        await server.Send(source, some_data);
                    }

                    break;

                default:
                    break;
            }
        }

        public async Task<int> BlockCreate(bool admin = false)
        {
            int result = 0;

            try
            {
                string previous = cryptography.GetHashString("system");     // system hash bbc5e661e106c6dcd8dc6dd186454c2fcba3c710fb4d8e71a60c93eaf077f073
                string previous2 = cryptography.GetHashString("system");    // system hash bbc5e661e106c6dcd8dc6dd186454c2fcba3c710fb4d8e71a60c93eaf077f073
                string path;
                string name;

                previous = await ActualBlockGet();

                if (admin)
                    previous = cryptography.GetHashString("system");

                byte[] transactions = new byte[0];
                List<string> transactions_list = new List<string>();
                byte[] transaction = new byte[0];
                string transaction_hash = string.Empty;
                string hash = string.Empty;
                string transaction_info = string.Empty;
                TwoBytesArrays temp = new TwoBytesArrays();
                temp.part1 = new byte[0];
                int counter = 0;
                byte[] block;

                path = filesystem.FSConfig.db_path;
                transactions_list = filesystem.GetFilesListFromDirectory(path);

                foreach (string element in transactions_list)
                {
                    path = filesystem.FSConfig.db_path + @"\" + element;
                    transaction = await filesystem.GetFromFileAsync(path);

                    transaction_hash = cryptography.HashToString((cryptography.GetSHA256Hash(transaction)));
                    hash += transaction_hash;
                    hash = cryptography.GetHashString(hash);


                    transaction_info += transaction_hash + "#" + counter.ToString() + "|";

                    temp.part2 = transaction;
                    temp.part1 = ByteArrayJoin(temp);

                    transaction = temp.part1;

                    counter++;
                    if (!File.Exists(filesystem.FSConfig.db_temp_path + @"\" + element))
                        File.Move(filesystem.FSConfig.db_path + @"\" + element, filesystem.FSConfig.db_temp_path + @"\" + element);
                    else
                        File.Delete(filesystem.FSConfig.db_path + @"\" + element);
                }
                transactions = temp.part2;

                int count = transactions_list.Count;

                byte[] header;
                header = await Task.Run(() => BlockGenerate(previous, count, Encoding.UTF8.GetBytes(hash), Encoding.UTF8.GetBytes(transaction_info)));

                name = cryptography.HashToString(cryptography.GetSHA256Hash(header));

                window.WriteLine("Header size: " + header.Length);

                previous2 = await ActualBlockGet();

                if (admin)
                    previous2 = cryptography.GetHashString("system");


                if ((previous == previous2) && (previous.Length != 0))
                {
                    path = filesystem.FSConfig.db_blocks_path + @"\" + name;

                    block = AddOperation(header.Length.ToString(), Blocks.header_info_size, header);

                    temp.part1 = block;
                    temp.part2 = transaction;

                    block = ByteArrayJoin(temp);

                    window.WriteLine("[block]: transactions part (" + transaction.Length + ")");

                    await filesystem.AddInfoToFileAsync(path, block, true);

                    window.WriteLine("[block]: block created(" + path + ")");

                    if (!admin)
                        await SayNewBlock(name);

                    await ActualBlockSet(name);
                    result = 1;
                }
                else
                    result = 2;
            }
            catch (Exception ex)
            {
                window.WriteLine(ex.ToString());
            }

            return result;
        }

        //int version, int timestamp, string previous_block, string root_hash, int number_of_transaction, byte[] transactions

        private byte[] BlockGenerate(string previous_block, int count, byte[] root_hash, byte[] transaction_info)
        {
            byte[] block = new byte[0];
            try
            {
                TwoBytesArrays pre_block = new TwoBytesArrays();
                TwoBytesArrays temp = new TwoBytesArrays();

                int time = GetDate();

                UInt64 flowing = 0;

                pre_block.part1 = IntToBytes(block_version);
                window.WriteLine("Version: " + BytesToInt(pre_block.part1) + " [" + pre_block.part1.Length + "]");

                pre_block.part2 = IntToBytes(time);
                window.WriteLine("Time: " + BytesToInt(pre_block.part2) + " [" + pre_block.part2.Length + "]");

                pre_block.part1 = ByteArrayJoin(pre_block);

                pre_block.part2 = Encoding.UTF8.GetBytes(previous_block);
                window.WriteLine("[block] Previous: " + Encoding.UTF8.GetString(pre_block.part2) + " [" + pre_block.part2.Length + "]");

                pre_block.part1 = ByteArrayJoin(pre_block);

                pre_block.part2 = IntToBytes(count);
                window.WriteLine("Count: " + BytesToInt(pre_block.part2) + " [" + pre_block.part2.Length + "]");

                pre_block.part1 = ByteArrayJoin(pre_block);

                pre_block.part2 = root_hash;
                window.WriteLine("root_hash: " + Encoding.UTF8.GetString(pre_block.part2) + " [" + pre_block.part2.Length + "]");

                pre_block.part1 = ByteArrayJoin(pre_block);

                pre_block.part2 = IntToBytes(transaction_info.Length);
                window.WriteLine("T_info.lenght: " + BytesToInt(pre_block.part2) + " [" + pre_block.part2.Length + "]");

                pre_block.part1 = ByteArrayJoin(pre_block);

                pre_block.part2 = transaction_info;
                window.WriteLine("T_info: " + Encoding.UTF8.GetString(pre_block.part2) + " [" + pre_block.part2.Length + "]");

                byte[] backup = ByteArrayJoin(pre_block);

                string temp_str = "fail";

                bool trigger = false;

                while (!trigger)
                {
                    temp.part2 = Uint64ToBytes(flowing);
                    temp.part1 = backup;
                    temp.part1 = ByteArrayJoin(temp);

                    temp_str = cryptography.HashToString(cryptography.GetSHA256Hash(temp.part1));
                    trigger = CheckBlockHash(temp_str);

                    flowing++;
                }
                window.WriteLine("Flowing: " + BytesToUint64(temp.part2) + " [" + temp.part2.Length + "]");

                block = temp.part1;


                window.WriteLine("Block: " + temp_str);
            }
            catch (Exception ex)
            {
                window.WriteLine(ex.ToString());
            }
            return block;
        }

        public Block BlockDeSerialize(byte[] block)
        {
            Block full_block = new Block();
            try
            {
                TwoBytesArrays temp = new TwoBytesArrays();
                byte[] transactions = new byte[0];

                temp = ByteArrayCut(block, Blocks.header_info_size);
                string header = BytesToOperation(temp.part1);

                //window.WriteLine("Header size: " + Convert.ToInt32(header));

                temp = ByteArrayCut(temp.part2, Convert.ToInt32(header));
                transactions = temp.part2;

                full_block.name = cryptography.HashToString(cryptography.GetSHA256Hash(temp.part1));

                //window.WriteLine("[last]: " + temp.part1.Length);

                temp = ByteArrayCut(temp.part1, 4);
                full_block.version = BytesToInt(temp.part1);

                //window.WriteLine("[last]: " + temp.part2.Length);

                temp = ByteArrayCut(temp.part2, 4);
                full_block.time = BytesToInt(temp.part1);

                //window.WriteLine("[last]: " + temp.part2.Length);

                temp = ByteArrayCut(temp.part2, 64);
                full_block.previous = Encoding.UTF8.GetString(temp.part1);

                //window.WriteLine("[last]: " + temp.part2.Length);

                temp = ByteArrayCut(temp.part2, 4);
                full_block.transactions_count = BytesToInt(temp.part1);

                //window.WriteLine("[last]: " + temp.part2.Length);

                temp = ByteArrayCut(temp.part2, 64);
                full_block.root_hash = Encoding.UTF8.GetString(temp.part1);

                //window.WriteLine("[last]: " + temp.part2.Length);

                temp = ByteArrayCut(temp.part2, 4);
                int t_info_length = BytesToInt(temp.part1);
                full_block.transactions_info_size = t_info_length;

                //window.WriteLine("[last]: " + temp.part2.Length);

                temp = ByteArrayCut(temp.part2, t_info_length);
                full_block.transactions_info = Encoding.UTF8.GetString(temp.part1);

                //window.WriteLine("[last]: " + temp.part2.Length);

                full_block.flowing = BytesToUint64(temp.part2);
                full_block.transactions = transactions;

                if (debug)
                {
                    window.WriteLine("Name: " + full_block.name);
                    window.WriteLine("Version: " + full_block.version);
                    window.WriteLine("Time: " + full_block.time);
                    window.WriteLine("Previous: " + full_block.previous);
                    window.WriteLine("Count: " + full_block.transactions_count);
                    window.WriteLine("root_hash: " + full_block.root_hash);
                    window.WriteLine("T_info.lenght: " + full_block.transactions_info_size);
                    window.WriteLine("Transactions [info]: " + full_block.transactions_info);
                    window.WriteLine("Flowing: " + full_block.flowing);


                    window.WriteLine("Transactions [full]: " + transactions.Length / 712 + " [" + transactions.Length + "]");
                }
            }
            catch (Exception ex)
            {
                window.WriteLine("Exception in BlockDeSerialize");
                window.WriteLine(ex.ToString());
            }
            return full_block;
        }

        private bool CheckBlockHash(string block)
        {
            bool result = false;

            if (block.EndsWith("00"))
                result = true;
            return result;
        }

        public async Task ActualBlockSet(string actual_block)
        {
            string path = filesystem.FSConfig.db_blocks_path + @"\actualblock";
            byte[] data = Encoding.UTF8.GetBytes(actual_block);

            if (File.Exists(path))
            {
                string actual;
                string new_actual;
                string str;

                if (!File.Exists(filesystem.FSConfig.db_blocks_path + @"\" + actual_block))
                    await SearchBlock(actual_block);

                await filesystem.AddInfoToFileAsync(path, data, true);
            }
            else
            {
                window.WriteLine("Writing: " + path);
                await filesystem.AddInfoToFileAsync(path, data, true);
            }
        }

        public async Task<string> ActualBlockGet()
        {
            string result = string.Empty;
            string path = filesystem.FSConfig.db_blocks_path + @"\actualblock";

            if (File.Exists(path))
            {
                byte[] data = await filesystem.GetFromFileAsync(path);
                result = Encoding.UTF8.GetString(data);
            }

            return result;
        }

        public async Task BlockChainActualize()
        {
            byte[] message = new byte[0];
            string path = filesystem.FSConfig.db_blocks_path;
            string actual = "actualblock";
            Block temp_block = new Block();
            byte[] temp_array = new byte[0];


            bool trigger1 = true;
            bool trigger2 = true;
            int tring1 = 0;
            int tring2 = 0;



            await Task.Run(async () => {
                try
                {
                    message = Encoding.UTF8.GetBytes(actual);
                    message = server.AddOperation(getlogic.request_template, UDPServer.operation_size, message);
                    getlogic.waitingforactual = true;

                    await server.SendGlobal(message);
                    await Task.Delay(5000);

                    while (trigger1)
                    {
                        path = filesystem.FSConfig.db_blocks_path + @"\" + actual;

                        if (!File.Exists(path))
                        {
                            message = Encoding.UTF8.GetBytes(actual);
                            message = server.AddOperation(getlogic.request_template, UDPServer.operation_size, message);
                            getlogic.waitingforactual = true;

                            await server.SendGlobal(message);
                            await Task.Delay(5000);
                            tring1++;
                        }
                        else
                        {
                            actual = Encoding.UTF8.GetString(await filesystem.GetFromFileAsync(path));
                            trigger1 = false;
                            window.WriteLine("[BlockChainActualize]: actualblock file found on " + tring1 + " try");
                        }

                        if (tring1 > 2)
                        {
                            trigger1 = false;
                            trigger2 = false;
                            window.WriteLine("[BlockChainActualize]: actualblock file not found");
                        }
                    }

                    tring2 = 0;

                    while (trigger2)
                    {
                        path = filesystem.FSConfig.db_blocks_path + @"\" + actual;

                        temp_array = await SearchBlock(actual);

                        if (temp_array.Length == 0)
                        {
                            await Task.Delay(10000);
                            tring2++;
                        }
                        else
                        {
                            temp_block = BlockDeSerialize(temp_array);

                            if (actual.Length == Blocks.block_length)
                            {
                                if (temp_block.previous == Blocks.first_block)
                                {
                                    trigger2 = false;
                                    window.WriteLine("[BlockChainActualize]: Completed");
                                }
                                else
                                {
                                    actual = temp_block.previous;
                                    tring2 = 0;
                                }
                            }
                        }

                        if (tring2 > 0)
                        {
                            trigger2 = false;
                            window.WriteLine("[BlockChainActualize]: block (" + actual + ") not found");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("Exception: " + ex.ToString());
                }
        });
        }

        public async Task<string> BlockChainCheckLeft(byte[] block)
        {
            string result = string.Empty;

            TwoBytesArrays temp = new TwoBytesArrays();
            Block new_block = new Block();
            string path = filesystem.FSConfig.db_blocks_path;
            string str;
            byte[] data = new byte[0];


            string real_name = GetBlockName(block);

            if (CheckBlockHash(real_name))
            {
                new_block = BlockDeSerialize(block);

                if (new_block.previous != Blocks.first_block)
                {

                    data = await SearchBlock(new_block.previous);

                    if (data.Length != 0)
                    {
                        str = await BlockChainCheckLeft(data);
                        if (str == Blocks.first_block)
                        {
                            result = real_name;
                        }
                    }
                }
                else
                {
                    result = real_name;
                }
            }
            else
            {
                window.WriteLine("[block]: wrong type");
            }

            return result;
        }

        public string GetBlockName(byte[] block)
        {
            string result = string.Empty;

            if (block != null)
            {
                TwoBytesArrays temp = new TwoBytesArrays();

                temp = ByteArrayCut(block, Blocks.header_info_size);
                int header_size = Convert.ToInt32(BytesToOperation(temp.part1));
                temp = ByteArrayCut(temp.part2, header_size);

                result = cryptography.HashToString(cryptography.GetSHA256Hash(temp.part1));
            }

            return result;
        }

        public async Task SayNewBlock(string name)
        {
            byte[] data = await filesystem.GetFromFileAsync(name);

            byte[] operation = Encoding.UTF8.GetBytes(data.Length.ToString());
            operation = AddOperation("newblock", UDPServer.operation_size, operation);
            operation = AddOperation(getlogic.answer_template, GetLogic.operation_size, operation);
            await server.SendGlobal(operation);

            window.WriteLine("New block global sending completed");
        }

        public async Task<byte[]> SearchBlock(string name, bool global=true)
        {
            string path;
            byte[] result = new byte[0];
            byte[] some_data = new byte[0];


            if (name != null)
            {
                path = filesystem.FSConfig.db_blocks_path + @"\" + name;

                if ((!File.Exists(path)) && global)
                {
                    some_data = Encoding.UTF8.GetBytes(name);
                    some_data = server.AddOperation("block", GetLogic.operation_size, some_data);
                    some_data = server.AddOperation(getlogic.request_template, UDPServer.operation_size, some_data);

                    await server.SendGlobal(some_data);
                }
                else
                {
                    result = await filesystem.GetFromFileAsync(path);
                }
            }
            else
            {
                window.WriteLine("[search] searching empty: " + name);
            }
            return result;
        }

        public async Task<Transaction> SearchTransaction(string name)
        {
            Transaction result = new Transaction();

            string current = await ActualBlockGet();

            Block temp = new Block();

            bool trigger = true;

            if (current.Length != 0)
            {
                while (trigger)
                {
                    byte[] data = await SearchBlock(current);

                    temp = BlockDeSerialize(data);

                    result = SearchTransactionInBlock(name, temp);

                    if (result.public_key != null)
                    {
                        trigger = false;
                        window.WriteLine("[WARNING] TRANSACTION FOUND");
                    }

                    if (temp.previous == Blocks.first_block)
                        trigger = false;

                    current = temp.previous;
                }
            }
            return result;
        }

        private Transaction SearchTransactionInBlock(string transaction, Block block)
        {
            Transaction result = new Transaction();

            if (block.name != null)
            {
                List<string> temp = new List<string>();
                List<string> transactions_list = new List<string>();
                int magic = 0;

                if (block.transactions_info.IndexOf(transaction) > -1)
                {
                    transactions_list.AddRange(block.transactions_info.Split('|'));

                    foreach (string tran in transactions_list)
                    {
                        temp.Clear();
                        temp.AddRange(tran.Split('#'));

                        if (temp[0] == transaction)
                        {
                            magic = Convert.ToInt32(temp[1]);
                        }
                    }
                    magic = magic * Transactions.size_transaction;

                    byte[] data = new byte[Transactions.size_transaction];
                    Array.Copy(block.transactions, magic, data, 0, Transactions.size_transaction);

                    result = transactions.ParseTMessage(data);
                    transactions.WriteToConsole(result);
                }
            }

            return result;
        }
    }
}
