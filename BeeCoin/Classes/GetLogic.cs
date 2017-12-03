using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace BeeCoin
{
    public class GetLogic : Additional
    {
        private FileSystem filesystem;
        private UdpClient socket;
        private FileTransfering filetransfering;
        private Cryptography cryptography;
        private Blocks blocks;
        private UDPServer server;
        private Information information;

        public string request_template = "get|req";
        public string answer_template = "get|answ";
        public const int operation_size = 16;

        //FLAGS
        public bool waitingforactual = false;
        public bool waitingfornewactual = false;


        public int Initialize(FileSystem ex_filesystem, UdpClient ex_socket, FileTransfering ex_filetransfering, Cryptography ex_cryptography, UDPServer ex_server, Information ex_information, Blocks ex_blocks, bool ex_debug)
        {
            filesystem = ex_filesystem;
            socket = ex_socket;
            filetransfering = ex_filetransfering;
            cryptography = ex_cryptography;
            debug = ex_debug;
            blocks = ex_blocks;
            server = ex_server;
            information = ex_information;

            //window.WriteLine("class GetLogic: initialized");
            return 1;
        }
        
        public async Task SwitchLogic(string operation, byte[] incoming_data, IPEndPoint source)
        {
            try
            {
                byte[] data = new byte[0];
                string path = string.Empty;

                //window.WriteLine("Got GetLogic: " + operation);

                switch (operation)
                {
                    case "req":
                        await RequestsLogic(incoming_data, source);
                        break;
                    case "answ":
                        await AnswerLogic(incoming_data, source);
                        break;
                    default:
                        break;
                }
            }
            catch (Exception ex)
            {
                window.WriteLine(ex.ToString());
            }
        }

        private async Task RequestsLogic(byte[] incoming_data, IPEndPoint source)
        {
            try
            {
                TwoBytesArrays TWdata = new TwoBytesArrays();
                byte[] data = new byte[0];
                byte[] message = new byte[0];
                string operation = string.Empty;
                string path = string.Empty;

                TWdata = ByteArrayCut(incoming_data, GetLogic.operation_size);
                operation = BytesToOperation(TWdata.part1);

                window.WriteLine("Got RequestsLogic: " + operation);

                switch (operation)
                {
                    case "signature":
                        path = filesystem.FSConfig.root_path + @"\signature";
                        if (File.Exists(path))
                        {
                            data = await filesystem.GetFromFileAsync(path);
                            data = AddOperation(operation, UDPServer.operation_size, data);
                            data = AddOperation(answer_template, GetLogic.operation_size, data);
                            await server.Send(source, data);
                        }
                        break;

                    case "actualblock":
                        path = filesystem.FSConfig.db_blocks_path + @"\actualblock";
                        if (File.Exists(path))
                        {
                            data = await filesystem.GetFromFileAsync(path);
                            data = AddOperation(operation, UDPServer.operation_size, data);
                            data = AddOperation(answer_template, GetLogic.operation_size, data);

                            window.WriteLine("Sending actual block..");
                            await server.Send(source, data);
                        }
                        break;

                    case "block":
                        if (TWdata.part2.Length != 0)
                        {
                            path = filesystem.FSConfig.db_blocks_path + @"\" + Encoding.UTF8.GetString(TWdata.part2);
                            data = await filesystem.GetFromFileAsync(path);

                            message = Encoding.UTF8.GetBytes(data.Length.ToString());
                            message = AddOperation("block", UDPServer.operation_size, message);
                            message = AddOperation(answer_template, GetLogic.operation_size, message);

                            await server.Send(source, message);
                            window.WriteLine("Sending block: " + Encoding.UTF8.GetString(TWdata.part2));
                            filetransfering.TcpDataSend(data);
                        }
                        
                        break;
                    default:
                        break;
                }
            }
            catch(Exception ex)
            {
                Debug.WriteLine(ex.ToString());
            }
        }

        private async Task AnswerLogic(byte[] incoming_data, IPEndPoint source)
        {
            try
            {
                TwoBytesArrays TWdata = new TwoBytesArrays();
                byte[] data = new byte[0];
                string operation = string.Empty;
                string path = string.Empty;
                int size = 0;
                string name;

                TWdata = ByteArrayCut(incoming_data, GetLogic.operation_size);
                operation = BytesToOperation(TWdata.part1);

                window.WriteLine("Got AnswerLogic: " + operation);
                if (TWdata.part2.Length != 0)
                {
                    switch (operation)
                    {
                        case "signature":
                            path = filesystem.FSConfig.root_path + @"\signature";
                            window.WriteLine("Writing: " + path + " " + TWdata.part2.Length);
                            await filesystem.AddInfoToFileAsync(path, TWdata.part2, true);
                            await information.ActualizeSelfSignature();
                            break;

                        case "actualblock":
                            if (waitingforactual)
                            {
                                string new_actual = Encoding.UTF8.GetString(TWdata.part2);
                                path = filesystem.FSConfig.db_blocks_path + @"\actualblock";

                                window.WriteLine("AnswerLogic.actualblock " + new_actual);
                                data = await blocks.SearchBlock(new_actual);

                                if (data.Length == 0)
                                {
                                    waitingfornewactual = true;
                                }
                                else
                                {
                                    await filesystem.AddInfoToFileAsync(path, TWdata.part2, true);
                                    path = filesystem.FSConfig.db_blocks_path;
                                    string current = Encoding.UTF8.GetString(await filesystem.GetFromFileAsync(path));
                                    byte[] current_array = await filesystem.GetFromFileAsync(path + @"\" + current);
                                    byte[] new_array = await filesystem.GetFromFileAsync(path + @"\" + new_actual);
                                    Block current_block = new Block();
                                    Block new_block = new Block();

                                    current_block = blocks.BlockDeSerialize(current_array);
                                    new_block = blocks.BlockDeSerialize(new_array);

                                    if(current_block.time < new_block.time)
                                        await blocks.ActualBlockSet(new_actual);
                                }
                                waitingforactual = false;
                            }
                            break;

                        case "block":
                            //window.WriteLine("Got AnswerLogic block");

                            size = Convert.ToInt32(Encoding.UTF8.GetString(TWdata.part2));
                            window.WriteLine("getting block from: " + source.ToString());
                            data = await filetransfering.TcpDataGet(source, size);
                            window.WriteLine("Block got");
                            name = blocks.GetBlockName(data);
                            
                            if (name != string.Empty)
                            {
                                path = filesystem.FSConfig.db_blocks_path + @"\" + name;
                                window.WriteLine("new block: " + path);
                                await filesystem.AddInfoToFileAsync(path, data, true);
                                
                                if (waitingfornewactual)
                                {
                                    waitingfornewactual = false;
                                    await blocks.ActualBlockSet(name);
                                }
                                else
                                    await blocks.BlockChainActualize();
                            }

                            if (name.Length != 0)
                                window.WriteLine("block got: " + name);
                            else
                                window.WriteLine("wrong block");
                            break;
                        case "newblock":
                            //window.WriteLine("Got AnswerLogic block");

                            size = Convert.ToInt32(Encoding.UTF8.GetString(TWdata.part2));
                            window.WriteLine("getting newblock from: " + source.ToString());
                            data = await filetransfering.TcpDataGet(source, size);
                            window.WriteLine("newBlock got");
                            name = blocks.GetBlockName(data);

                            if (name != string.Empty)
                            {
                                path = filesystem.FSConfig.db_blocks_path + @"\" + name;
                                window.WriteLine("new block: " + path);
                                await filesystem.AddInfoToFileAsync(path, data, true);
                                await blocks.ActualBlockSet(name);
                            }
                            break;
                        default:
                            break;
                    }
                }
            }
            catch(Exception ex)
            {
                window.WriteLine(ex.ToString());
            }
        }
    }
}