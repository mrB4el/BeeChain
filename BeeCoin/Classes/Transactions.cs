using System;
using System.Diagnostics;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace BeeCoin
{
    public class Transactions : Additional
    {
        public const int size_transaction = 674; //bytes (545)
        public const int size_info_in_transaction = 128; //symbols
        public const int size_header = 30;      //symbols
        public const int size_suboperation = 10; //symbols
        public const float minimal_fee = 0.001f;

        public const int size_one_input = 10;

        private Cryptography cryptography;
        private UDPServer server;
        private FileSystem filesystem;
        private Blocks blocks;

        public int Initialize(FileSystem ex_filesystem, UDPServer ex_server, Cryptography ex_cryptography, Blocks ex_blocks, bool ex_debug)
        {
            filesystem = ex_filesystem;
            server = ex_server;
            cryptography = ex_cryptography;
            debug = ex_debug;
            blocks = ex_blocks;

            //window.WriteLine("class Transactions: initialized");

            return 1;
        }

        public async Task GetLogic(IPEndPoint source, byte[] data)
        {
            try
            {
                TwoBytesArrays tran = new TwoBytesArrays();
                tran = ByteArrayCut(data, Transactions.size_suboperation);

                string sub_operation = BytesToOperation(tran.part1);
                string[] sub = sub_operation.Split('|');
                string name;
                string path;

                List<string> ip_list = new List<string>(0);
                Transaction transaction = new Transaction();

                window.WriteLine("[45]: " + sub[0] + " from " + source.ToString());
                window.WriteLine("[46]: " + sub_operation);

                if (sub[0] == "new")
                {
                    transaction = ParseTMessage(tran.part2);
                    window.WriteLine("[tr] Transaction got: ");
                    int check = await CheckTransaction(transaction);
                    window.WriteLine("[tr] Checking result: " + check);

                    if ((check == 2) || (check == 1))
                    {

                        name = cryptography.HashToString(cryptography.GetSHA256Hash(tran.part2));
                        path = filesystem.FSConfig.db_path + @"\" + name;

                        int status = await filesystem.AddInfoToFileAsync(path, tran.part2, true);

                        if (status != 3)
                        {
                            byte[] message = Encoding.UTF8.GetBytes(name);
                            message = AddOperation("confirm", Transactions.size_suboperation, message);
                            message = AddOperation("transaction", UDPServer.operation_size, message);

                            await server.Send(source, message);

                            if (debug)
                                window.WriteLine("Transaction (" + name + ") confirmed to " + source.ToString());
                        }


                        int sub_times = Convert.ToInt32(sub[1]) - 1;

                        if (sub_times >= 1)
                        {
                            List<string> known_hosts = await server.GetKnownList();
                            List<string> black_list = new List<string>(0);


                            black_list.Add(source.Address.ToString());
                            black_list.AddRange(server.myIpAddresses);

                            known_hosts = server.RemoveIPAddresses(known_hosts, black_list);

                            //await SendTransaction(tran.part2, sub_times, known_hosts);
                        }
                    }
                }

                if (sub[0] == "confirm")
                {
                    name = Encoding.UTF8.GetString(tran.part2);
                    if (debug)
                        window.WriteLine("Transaction (" + name + ") confirmed by " + source.ToString());
                }

                ip_list.Add(source.Address.ToString());
                await server.AddToKnownList(ip_list);
            }
            catch(Exception ex)
            {
                window.WriteLine(ex.ToString());
            }
        }

        

        /* 
         * transaction data: transaction + signature
         * signature(input, output, in_value, out_value public_key):
        */

        public async Task<string> MakeNewTransaction(Transaction transaction, string private_key)
        {
            string result = string.Empty;

            try
            {
                byte[] data = MakeTMessage(transaction, private_key);

                await SendTransaction(data, 2);

                result = cryptography.HashToString(cryptography.GetSHA256Hash(data));

                string path = filesystem.FSConfig.db_path + @"\" + result;

                await filesystem.AddInfoToFileAsync(path, data);
            }
            catch(Exception ex)
            {
                window.WriteLine(ex.ToString());
            }

            return result;
        }

        private async Task SendTransaction(byte[] transaction, int iteration)
        {
            string header = "new|" + iteration;

            transaction = AddOperation(header, Transactions.size_suboperation, transaction);
            transaction = AddOperation("transaction", UDPServer.operation_size, transaction);

            await server.SendGlobal(transaction);
        }

        public byte[] MakeTMessage(Transaction transaction, string private_key)
        {
            byte[] result_data = new byte[0];

            try
            {
                byte[] body_data;
                string header;
                string tempstr = string.Empty;
                string operations = string.Empty;
                string key = private_key;
                int info = 0;

                TwoBytesArrays temp = new TwoBytesArrays();
                byte[] additional_info = new byte[0];

                info = transaction.information.Length;

                operations = transaction.input.put 
                    + "|" + transaction.input.value 
                    + "|" + transaction.output.put 
                    + "|" + transaction.output.value 
                    + "|" + GetDate() 
                    + "|"  + transaction.public_key;

                temp.part1 = Encoding.UTF8.GetBytes(operations);
                temp.part2 = ByteArrayFill(transaction.information, Transactions.size_info_in_transaction);
                body_data = ByteArrayJoin(temp);

                temp.part1 = body_data;
                temp.part2 = cryptography.Sign(body_data, key);
                body_data = ByteArrayJoin(temp);

                //window.WriteLine("Body hash: " + cryptography.HashToString(cryptography.GetSHA256Hash(temp.part1)));
                //window.WriteLine("Signature hash: " + cryptography.HashToString(cryptography.GetSHA256Hash(temp.part2)));

                header = transaction.version + "|" + temp.part1.Length + "|" + info;

                result_data = AddOperation(header, Transactions.size_header, body_data);

                Transaction tmp = ParseTMessage(result_data);

                //WriteToConsole(tmp);
            }
            catch(Exception ex)
            {
                Debug.WriteLine(ex.ToString());
            }

            return result_data;
        }

        public Transaction ParseTMessage(byte[] data)
        {
            Transaction result = new Transaction();

            try
            {
                byte[] result_data = new byte[0];
                byte[] signature = new byte[0];
                string operations = string.Empty;
                string tmp = string.Empty;
                string[] tmp_list = new string[0];
                byte[] body_data = new byte[0];
                int info = 0;
                int bd_length = 0;

                TwoBytesArrays temp = new TwoBytesArrays();

                temp = ByteArrayCut(data, Transactions.size_header);
                tmp = BytesToOperation(temp.part1);
                tmp_list = tmp.Split('|');

                result.version = Convert.ToInt32(tmp_list[0]);
                bd_length = Convert.ToInt32(tmp_list[1]);
                info = Convert.ToInt32(tmp_list[2]);

                temp = ByteArrayCut(temp.part2, bd_length); //transaction
                signature = temp.part2;

                temp = ByteArrayCut(temp.part1, temp.part1.Length - Transactions.size_info_in_transaction);
                operations = Encoding.UTF8.GetString(temp.part1);
                //Debug.WriteLine("operations[1]: " + cryptography.GetHashString(operations));
                //Debug.WriteLine("info[1]: " + cryptography.HashToString(cryptography.GetSHA256Hash(temp.part2)));

                tmp_list = operations.Split('|');
                result.input.put = tmp_list[0];
                result.input.value = Convert.ToInt32(tmp_list[1]);
                result.output.put = tmp_list[2];
                result.output.value = Convert.ToInt32(tmp_list[3]);
                result.date = Convert.ToInt32(tmp_list[4]);
                result.public_key = tmp_list[5];

                operations = string.Empty;
                operations = result.input.put
                    + "|" + result.input.value
                    + "|" + result.output.put
                    + "|" + result.output.value
                    + "|" + result.date
                    + "|" + result.public_key;
                

                result.information = ByteArrayGet(temp.part2, info);

                temp.part1 = Encoding.UTF8.GetBytes(operations);
                temp.part2 = ByteArrayFill(result.information, Transactions.size_info_in_transaction);
                body_data = ByteArrayJoin(temp);

                //Debug.WriteLine("operations[2]: " + cryptography.GetHashString(operations));
                //Debug.WriteLine("info[2]: " + cryptography.HashToString(cryptography.GetSHA256Hash(temp.part2)));

                //window.WriteLine("Body hash: " + cryptography.HashToString(cryptography.GetSHA256Hash(body_data)));
                //window.WriteLine("Signature hash: " + cryptography.HashToString(cryptography.GetSHA256Hash(signature)));

                if (cryptography.VerifySign(body_data, signature, result.public_key))
                {
                    result.signature = signature;
                    result.name = cryptography.HashToString(cryptography.GetSHA256Hash(data));
                }
                else
                    window.WriteLine("Transaction checking error");
            }
            catch(Exception ex)
            {
                Debug.WriteLine(ex.ToString());
            }

            return result;
        }

        /// <summary>
        /// Проверка транзакции
        /// </summary>
        /// <param name="transaction"></param>
        /// <returns>
        /// 0 - не найдена транзакция-предшественник
        /// 1 - найдена глобальная (подтвержденная) транзакция-предшественник
        /// 2 - найдена локальная транзакция-предшественник, ссылающаяся на подтвержденную
        /// 9 - подпись неверна
        /// </returns>
        public async Task<int> CheckTransaction(Transaction transaction)
        {
            int result = 0;

            Transaction temp_transaction = new Transaction();
            TwoBytesArrays temp = new TwoBytesArrays();
            byte[] body_data;
            byte[] signature;
            string operations;

            operations = transaction.input.put
                    + "|" + transaction.input.value
                    + "|" + transaction.output.put
                    + "|" + transaction.output.value
                    + "|" + GetDate()
                    + "|" + transaction.public_key;

            temp.part1 = Encoding.UTF8.GetBytes(operations);
            temp.part2 = ByteArrayFill(transaction.information, Transactions.size_info_in_transaction);
            body_data = ByteArrayJoin(temp);

            signature = transaction.signature;

            window.WriteLine("[tr] checking");
            WriteToConsole(transaction);

            if (cryptography.VerifySign(body_data, signature, transaction.public_key))
            {
                string search = transaction.input.put;

                if (search == "8c6976e5b5410415bde908bd4dee15dfb167a9c873fc4bb8a81f6f2ab448a918") //admin
                {
                    if(transaction.public_key == Information.admin_public_key)
                    {
                        result = 1;
                    }
                }
                else
                {
                    temp_transaction = await SearchTransactionLocal(search);

                    if (temp_transaction.public_key != null)
                    {
                        result = await CheckTransaction(temp_transaction);
                    }
                    else
                        result = 3;

                    if (temp_transaction.public_key != null) 
                        result = 2;
                }
            }
            else
                result = 9;

            return result;
        }

        public async Task<byte[]> Transaction_data(string name)
        {
            byte[] result = new byte[0];

            byte[] temp;

            List<string> transactions = new List<string>();

            FileSystem filesystem = new FileSystem();

            temp = await filesystem.GetFromFileAsync(filesystem.FSConfig.db_path + @"\" + name);

            if(temp.Length == 0)
            {
                Debug.WriteLine("Transaction (" + name + ") not found in local db");
            }

            return result;
        }

        public async Task<Transaction> Search(string name)
        {
            Transaction result = new Transaction();

            bool trigger = false;

            try
            {
                byte[] data = new byte[0];

                result = await SearchTransactionGlobal(name);

                if (result.information == null)
                    result = await SearchTransactionLocal(name);
                else
                    trigger = true;

                if (data.Length != 0)
                    result = ParseTMessage(data);

            }
            catch (Exception ex)
            {
                window.WriteLine(ex.ToString());
            }
            result.status = trigger;
            return result;
        }

        public async Task<Transaction> SearchTransactionLocal(string name)
        {
            Transaction result = new Transaction();
            try
            {

                string path = filesystem.FSConfig.db_path + @"\" + name;
                byte[] data = await filesystem.GetFromFileAsync(path);

                if (data.Length == 0)
                {
                    window.WriteLine("Transaction (" + path + ") not found in local db");
                }
                else
                {
                    result = ParseTMessage(data);
                }
            }
            catch(Exception ex)
            {
                window.WriteLine(ex.ToString());
            }
            return result;
        }

        public async Task<Transaction> SearchTransactionGlobal(string name)
        {
            Transaction result = new Transaction();

            result = await blocks.SearchTransaction(name);

            if (result.information != null)
            {
                window.WriteLine("Transaction (" + name + ") not found in global db");
            }

            return result;
        }

        public async Task<List<Transaction>> GetTransactionsListToPublicKey(string wallet)
        {
            List<Transaction> transactions = new List<Transaction>();

            try
            {
                Transaction temp_tran = new Transaction();
                List<Transaction> black = new List<Transaction>();

                string current = await blocks.ActualBlockGet();
                int tst = Transactions.size_transaction;
                byte[] temp_transaction = new byte[tst];

                Block temp = new Block();

                bool trigger = true;

                if (current.Length != 0)
                {
                    while (trigger)
                    {
                        byte[] data = await blocks.SearchBlock(current);
                        temp = blocks.BlockDeSerialize(data);

                        for (int i = 0; i < temp.transactions_count; i++)
                        {
                            Array.Copy(temp.transactions, i * tst, temp_transaction, 0, tst);
                            temp_tran = ParseTMessage(temp_transaction);

                            if (temp_tran.output.put == wallet)
                            {
                                transactions.Add(temp_tran);
                            }
                        }

                        if (temp.previous == Blocks.first_block)
                            trigger = false;

                        current = temp.previous;
                    }

                    trigger = true;
                    current = await blocks.ActualBlockGet();

                    while (trigger)
                    {
                        byte[] data = await blocks.SearchBlock(current);
                        temp = blocks.BlockDeSerialize(data);

                        for (int i = 0; i < temp.transactions_count; i++)
                        {
                            Array.Copy(temp.transactions, i * tst, temp_transaction, 0, tst);
                            temp_tran = ParseTMessage(temp_transaction);

                            foreach (Transaction trn in transactions)
                            {
                                if (temp_tran.input.put == trn.name)
                                {
                                    black.Add(trn);
                                }
                            }
                        }


                        if (temp.previous == Blocks.first_block)
                            trigger = false;

                        current = temp.previous;
                    }
                }

                List<string> local_list = new List<string>();
                string path = filesystem.FSConfig.db_path;

                local_list = filesystem.GetFilesListFromDirectory(path);

                foreach(string local_el in local_list)
                {
                    Transaction local = new Transaction();
                    

                    byte[] local_by = new byte[0];

                    local_by = await filesystem.GetFromFileAsync(path + @"\" + local_el);
                    local = ParseTMessage(local_by);

                    foreach (Transaction trn in transactions)
                    {
                        if (local.input.put == trn.name)
                        {
                            black.Add(trn);
                        }
                    }      
                }

                foreach (Transaction trn in black)
                {
                    transactions.Remove(trn);
                }
            }
            catch (Exception ex)
            {
                window.WriteLine(ex.ToString());
            }
            window.WriteLine("Found: " + transactions.Count);
            return transactions;
        }

        // DEBUG

        public void WriteToConsole(Transaction transaction)
        {
            try
            {
                window.WriteLine("[tr]transaction.version: " + transaction.version);
                window.WriteLine("[tr]transaction.input.put: " + transaction.input.put);
                window.WriteLine("[tr]transaction.input.value: " + transaction.input.value);
                window.WriteLine("[tr]transaction.output.put: " + transaction.output.put);
                window.WriteLine("[tr]transaction.output.value: " + transaction.output.value);
                window.WriteLine("[tr]transaction.public_key: " + transaction.public_key);
                window.WriteLine("[tr]transaction.date: " + transaction.date);
                window.WriteLine("[tr]transaction.information: " + Encoding.UTF8.GetString(transaction.information));
                window.WriteLine("[tr]transaction.signature(lenght): " + transaction.signature.Length);
            }
            catch(Exception ex)
            {
                Debug.WriteLine(ex.ToString());
            }
        }
    }
}