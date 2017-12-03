using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using System.IO;

namespace BeeCoin
{
    public class StateObject
    {
        // Client  socket.  
        public Socket workSocket = null;
        // Size of receive buffer.  
        public const int BufferSize = 128;
        // Receive buffer.  
        public byte[] buffer = new byte[BufferSize];
        // Received data string.  
    }


    public class FileTransfering : Additional
    {
        // Thread signal.  
        private static ManualResetEvent allDone = new ManualResetEvent(false);

        // ManualResetEvent instances signal completion.  
        private static ManualResetEvent connectDone = new ManualResetEvent(false);
        private static ManualResetEvent sendDone = new ManualResetEvent(false);
        private static ManualResetEvent receiveDone = new ManualResetEvent(false);

        /// 
        /// КОСТЫЛИ
        /// 
        private byte[] new_data = new byte[0];
        private int ex_result_size = 0;
        private int retry = 0;
        private int sub_res = 0;

        private void zero_all()
        {
            allDone = new ManualResetEvent(false);
            connectDone = new ManualResetEvent(false);
            sendDone = new ManualResetEvent(false);
            receiveDone = new ManualResetEvent(false);
            new_data = new byte[0];
            ex_result_size = 0;
            retry = 0;
            sub_res = 0;
        }

        public void TcpDataSend(byte[] data)
        {
            try
            {
                zero_all();
                IPEndPoint local = new IPEndPoint(0, 6879);

                Cryptography cryptography = new Cryptography();

                window.WriteLine("TCP server: " + local.Address.ToString() + ":" + local.Port + " started");
                window.WriteLine("MD5 (send): " + cryptography.HashToString(cryptography.GetSHA256Hash(data)) + " size: " + data.Length);

                int res = ReceiveData(local, data);

                if (res != 0)
                {
                    TcpDataSend(data);
                }
            }
            catch (Exception ex)
            {
                window.WriteLine("Exception in filetransfering (send)");
                window.WriteLine(ex.ToString());
            }

        }

        public async Task<byte[]> TcpDataGet(IPEndPoint source, int result_size)
        {
            zero_all();
            Cryptography cryptography = new Cryptography();

            byte[] result = new byte[result_size];

            try
            {
                new_data = new byte[0];
                window.WriteLine("TCP client: " + source.Address.ToString() + ":" + source.Port + " started (wait for: " + result_size + " bytes)");
                result = await GetData(source, result_size);
                if( result.Length < result_size )
                {
                    window.WriteLine("starting TCP again");

                    result = await TcpDataGet(source, result_size);
                }
                window.WriteLine("MD5 (got): " + cryptography.HashToString(cryptography.GetSHA256Hash(result)) + " size: " + result.Length);
            }
            catch (Exception ex)
            {
                window.WriteLine("Exception in filetransfering (get)");
                window.WriteLine(ex.ToString());
            }

            return result;
        }


        private int ReceiveData(IPEndPoint local, byte[] data)
        {
            int result = 0;
            sub_res = result;

            new_data = data;


            // Establish the local endpoint for the socket.  
            // The DNS name of the computer  
            // running the listener is "host.contoso.com".  

            // Create a TCP/IP socket.  
            Socket listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            // Bind the socket to the local endpoint and listen for incoming connections.  
            try
            {
                listener.Bind(local);
                listener.Listen(100);


                // Set the event to nonsignaled state.  
                allDone.Reset();

                // Start an asynchronous socket to listen for connections.  
                if (debug)
                    window.WriteLine("[TCP Server] Waiting for a connection...");
                listener.BeginAccept(new AsyncCallback(AcceptCallback), listener);

                // Wait until a connection is made before continuing.  
                allDone.WaitOne();
                listener.Close();
            }
            catch (Exception e)
            {
                window.WriteLine(e.ToString());
                allDone.WaitOne();
                listener.Close();
            }

            result = sub_res;
            return result;
        }

        #region Server

        private void AcceptCallback(IAsyncResult ar)
        {
            try
            {
                // Get the socket that handles the client request.  
                Socket listener = (Socket)ar.AsyncState;
                Socket handler = listener.EndAccept(ar);

                // Create the state object.  
                StateObject state = new StateObject();
                state.workSocket = handler;
                handler.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0, new AsyncCallback(ReadCallback), state);
            }
            catch (Exception ex)
            {
                window.WriteLine("Exception in: FileTransfering.AcceptCallback");
                window.WriteLine(ex.ToString());
            }
        }

        private void ReadCallback(IAsyncResult ar)
        {
            try
            {
                string content = string.Empty;

                // Retrieve the state object and the handler socket  
                // from the asynchronous state object.  
                StateObject state = (StateObject)ar.AsyncState;
                Socket handler = state.workSocket;

                // Read data from the client socket.   
                int bytesRead = handler.EndReceive(ar);

                byte[] temp = new byte[bytesRead];
                Array.Copy(state.buffer, temp, bytesRead);

                if (bytesRead > 0)
                {
                    // There  might be more data, so store the data received so far.  
                    window.WriteLine("c: " + bytesRead);
                    window.WriteLine("c: " + Encoding.UTF8.GetString(temp));

                    // Check for end-of-file tag. If it is not there, read   
                    // more data. 

                    content = Encoding.UTF8.GetString(temp);

                    window.WriteLine(content);

                    if (content.IndexOf("ready") > -1)
                    {
                        // All the data has been read from the   
                        // client. Display it on the window.  
                        //window.WriteLine("Read {0} bytes from socket. \n Data : {1}", content.Length, content);
                        // Echo the data back to the client.
                        window.WriteLine("ready got");
                        Send(handler, new_data);

                        handler.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0, new AsyncCallback(ReadCallback), state);
                    }
                    else
                    {
                        if (retry <= 5)
                        {
                            if (content.IndexOf("ok") == -1)
                            {
                                sub_res = 1;
                            }
                            else
                            {
                                window.WriteLine("All send ok");
                                allDone.Set();
                            }
                            retry = retry + 1;
                        }
                        else
                        {
                            window.WriteLine("Retry full");
                            allDone.Set();
                        }

                    }
                }
            }
            catch (Exception ex)
            {
                window.WriteLine("Exception in ReadCallback");
                window.WriteLine(ex.ToString());
                allDone.Set();
            }

        }
        #endregion

        private async Task<byte[]> GetData(IPEndPoint source, int result_size)
        {
            byte[] result = new byte[0];
            byte[] data = new byte[0];
            new_data = new byte[0];

            ex_result_size = result_size;
            if (debug)
                window.WriteLine("[TCP client] Client started");

            // Connect to a remote device.  
            try
            {

                // Create a TCP/IP socket.  
                Socket client = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

                // Connect to the remote endpoint.  
                client.BeginConnect(source, new AsyncCallback(ConnectCallback), client);
                connectDone.WaitOne();

                if (debug)
                    window.WriteLine("[TCP client] Client connected");

                // Send test data to the remote device.  
                data = Encoding.UTF8.GetBytes("ready");
                Send(client, data);
                sendDone.WaitOne();

                // Receive the response from the remote device.  
                Receive(client);
                receiveDone.WaitOne();

                int nd_size = new_data.Length;

                if (nd_size < result_size)
                {
                    data = Encoding.UTF8.GetBytes("plsagain");
                    Send(client, data);
                    sendDone.WaitOne();
                    if (debug)
                        window.WriteLine("[TCP client] trying again");

                    client.Shutdown(SocketShutdown.Both);
                    client.Close();

                }
                else
                {
                    data = Encoding.UTF8.GetBytes("ok");
                    Send(client, data);
                    sendDone.WaitOne();

                    if (debug)
                        window.WriteLine("[TCP client] ok");
                    result = new_data;

                    // Release the socket.  
                    client.Shutdown(SocketShutdown.Both);
                    client.Close();
                }

            }
            catch (Exception e)
            {
                window.WriteLine(e.ToString());
            }

            return result;
        }

        #region Client

        // The response from the remote device.  

        private void ConnectCallback(IAsyncResult ar)
        {
            try
            {
                // Retrieve the socket from the state object.  
                Socket client = (Socket)ar.AsyncState;

                // Complete the connection.  
                client.EndConnect(ar);
                if (debug)
                    window.WriteLine("Socket connected to" + client.RemoteEndPoint.ToString());

                // Signal that the connection has been made.  
                connectDone.Set();
            }
            catch (Exception e)
            {
                window.WriteLine(e.ToString());
            }
        }

        private void Receive(Socket client)
        {
            try
            {
                // Create the state object.  
                StateObject state = new StateObject();
                state.workSocket = client;

                // Begin receiving the data from the remote device.
                client.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0, new AsyncCallback(ReceiveCallback), state);
            }
            catch (Exception e)
            {
                window.WriteLine(e.ToString());
                receiveDone.Set();
            }
        }

        private void ReceiveCallback(IAsyncResult ar)
        {
            try
            {
                // Retrieve the state object and the client socket   
                // from the asynchronous state object.  
                StateObject state = (StateObject)ar.AsyncState;
                Socket client = state.workSocket;

                // Read data from the remote device.  
                int bytesRead = client.EndReceive(ar);

                byte[] temp = new byte[bytesRead];
                Array.Copy(state.buffer, temp, bytesRead);

                TwoBytesArrays temp1;
                temp1.part1 = new_data;
                temp1.part2 = temp;

                new_data = ByteArrayJoin(temp1);

                if (debug)
                    window.WriteLine("[ft]summary:" + new_data.Length + "(+" + bytesRead + "[" + StateObject.BufferSize + "])");


                if ((bytesRead > 0) && (new_data.Length < ex_result_size))
                {
                    // There might be more data, so store the data received so far.  
                    client.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0, new AsyncCallback(ReceiveCallback), state);
                }
                else
                {
                    receiveDone.Set();
                }

            }
            catch (Exception e)
            {
                window.WriteLine(e.ToString());
                receiveDone.Set();
            }
        }

        private void Send(Socket client, byte[] data)
        {
            try
            {
                window.WriteLine("[TCP] client: " + client.LocalEndPoint.ToString());
                client.BeginSend(data, 0, data.Length, 0, new AsyncCallback(SendCallback), client);
            }
            catch (Exception ex)
            {
                window.WriteLine("Exception in: FileTransfering.Send");
                window.WriteLine("client: " + client.LocalEndPoint.ToString());
                window.WriteLine(ex.ToString());
            }
        }

        private void SendCallback(IAsyncResult ar)
        {
            try
            {
                // Retrieve the socket from the state object.  
                Socket client = (Socket)ar.AsyncState;

                // Complete sending the data to the remote device.  
                int bytesSent = client.EndSend(ar);
                if (!debug)
                    window.WriteLine("Sent " + bytesSent + " bytes to server.");

                // Signal that all bytes have been sent.  
                sendDone.Set();
            }
            catch (Exception e)
            {
                window.WriteLine(e.ToString());
                sendDone.Set();
            }
        }
        #endregion
    }
}
