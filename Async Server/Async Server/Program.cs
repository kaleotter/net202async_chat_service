using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.IO;


namespace Async_Server
{
    public class ClientData
    {
        public TcpClient socket = null;
        public NetworkStream stream = null;
        public const int bufferSize = 1024;
        public byte[] buffer = new byte[bufferSize];
        public int id;
        public string lastChatMessage;
    }

    //this class defines what a message should contain
    public class ProtocolMessage
    {
        public string command, fileName, dataString;
        public Int32 filenameSize,
                        dataSize,
                        chunkSize,
                        commandSize,
                        commandSizeArray = 4,
                        chunkSizeArray = 4,
                        dataSizeArray = 4,
                        filenameSizeArray = 4;
    }
    // we need to store the filepath globally so all of the threads can access it. 
    public static class FilePath
    {
        public static string filePath = "";
    }

    //this flag will signal the server to close all conenctions
    public static class Closing
    {
        public static bool shutDown = false;
    }

    public static class ChatMessage
    {
        public static string message = "";
    }

    class Program
    {

        //list to hold client data
        public static List<ClientData> clients;

        public static ManualResetEvent handler = new ManualResetEvent(false);               //this tells any waiting threads that something has happened

        static ProtocolMessage decodeMessage(byte[] message)                                //whenever we receive a message, we need to decode it to work out what to do with the data that follows it. 
        {
            ProtocolMessage decodedMessage = new Async_Server.ProtocolMessage();            //We need a new protocol message object to hold the decoded data. I think?

            //first we need to get the size of the command bieng sent.
            byte[] commandSizeBytes = new byte[decodedMessage.commandSizeArray];             //Put the size of the command into a byte
            Array.Copy(message, 0, commandSizeBytes, 0, decodedMessage.commandSizeArray);
            decodedMessage.commandSize = BitConverter.ToInt32(commandSizeBytes, 0);

            //then we decode the command itself
            decodedMessage.command = "";
            if (decodedMessage.commandSize > 0)                                              //check the size of the command. We only need to bother processing if it is bigger than 0
            {
                byte[] commandBytes = new Byte[decodedMessage.commandSize];                 //create a new byte as large as the command is
                Array.Copy(message, decodedMessage.commandSizeArray, commandBytes, 0, decodedMessage.commandSize);
                decodedMessage.command = System.Text.Encoding.ASCII.GetString(commandBytes).Trim('\0');     //trim the string down to the correct size.
            }

            //then the filename size
            byte[] filenameSizeBytes = new byte[decodedMessage.filenameSizeArray];
            Array.Copy(message, decodedMessage.commandSizeArray + decodedMessage.commandSize, filenameSizeBytes, 0, decodedMessage.filenameSizeArray);
            decodedMessage.filenameSize = BitConverter.ToInt32(filenameSizeBytes, 0);

            //now the actual filename
            decodedMessage.fileName = "";                                                                                                                   //initialise this variable to null ready to accept whatever is in the message
            if (decodedMessage.filenameSize > 0)
            {
                byte[] fileNameBytes = new byte[decodedMessage.filenameSize];
                Array.Copy(message, decodedMessage.commandSizeArray + decodedMessage.filenameSizeArray + decodedMessage.commandSize, fileNameBytes, 0, decodedMessage.filenameSize);
                decodedMessage.fileName = System.Text.Encoding.ASCII.GetString(fileNameBytes);
                decodedMessage.fileName = FilePath.filePath + decodedMessage.fileName;


            }

            // now we need to calculate the number of packages to be recived (if the file is too long for a single packet)
            byte[] chunkBytes = new byte[decodedMessage.chunkSizeArray];
            Array.Copy(message, decodedMessage.commandSizeArray + decodedMessage.filenameSizeArray + decodedMessage.commandSize + decodedMessage.filenameSize, chunkBytes, 0, decodedMessage.chunkSizeArray);
            decodedMessage.chunkSize = BitConverter.ToInt32(chunkBytes, 0);

            //finally we need to get the size of the rest of the data, which might be 0
            byte[] dataSizeBytes = new byte[decodedMessage.dataSizeArray];
            Array.Copy(message, decodedMessage.commandSizeArray + decodedMessage.filenameSizeArray + decodedMessage.commandSize + decodedMessage.filenameSize + decodedMessage.chunkSizeArray, dataSizeBytes, 0, decodedMessage.dataSizeArray);
            decodedMessage.dataSizeArray = BitConverter.ToInt32(dataSizeBytes, 0);

            Console.Write("{0}, {1}, {2}, {3}\n", decodedMessage.command, decodedMessage.commandSize, decodedMessage.fileName, decodedMessage.filenameSize);
            return decodedMessage;          

        }

        public static void sendCallback (IAsyncResult ar)                               // will handle all asynchronous writing tasks. Should be started once/client connection?
        {

        }


        static void returnPing(ClientData Client)
        {
            string pingMessage = "pong!";
            Console.Write("client {0} just pinged us! \n", Client.id);
            byte[] confirmMessage = System.Text.Encoding.ASCII.GetBytes(pingMessage);
            Client.stream.WriteAsync(confirmMessage, 0, confirmMessage.Length);
        }

        static void chatReceived(ClientData Client)
        {

        }

        public static void receiveCallBack(IAsyncResult ar)
        {
            TcpListener server = (TcpListener)ar.AsyncState;
            Int32 id= 0;
            ClientData client = new ClientData();
            clients.Add(new ClientData());      

            foreach (ClientData c in clients)                           //we have to find out how many clients there are
            {
                id++;                                                   //increment this id number
            }
            id--; 

            clients[id].id = id;
            try
            {
                //client.socket = server.EndAcceptTcpClient(ar); old way of doing it. keep for now
                clients[id].socket = server.EndAcceptTcpClient(ar); // do it this way now?
            }
            catch(Exception)
            {
                handler.Set();
                return;
            }
            handler.Set();

            //client.stream = client.socket.GetStream(); old way. Keep for now
            clients[id].stream = clients[id].socket.GetStream();
            ManualResetEvent clientHandler = new ManualResetEvent(false);

            //we set up the connection. so we'll use this to call a callback to send messages
            

            while(!Closing.shutDown)                                        //while the server is not trying to close down
            {

                clients[id].stream.Read(clients[id].buffer, 0, clients[id].buffer.Length);
                ProtocolMessage message = decodeMessage(clients[id].buffer);
                byte[] confirmMessage = System.Text.Encoding.ASCII.GetBytes("command received!");

                switch (message.command)
                {
                    case "ping":
                        //client.stream.WriteAsync(confirmMessage, 0, confirmMessage.Length);
                        clients[id].stream.WriteAsync(confirmMessage, 0, confirmMessage.Length);
                        returnPing(clients[id]);
                        break;

                    case "chat":
                        clients[id].stream.WriteAsync(confirmMessage, 0, confirmMessage.Length);
                        chatReceived(clients[id]);
                        break;

                    case "quit":
                        clients[id].stream.WriteAsync(confirmMessage, 0, confirmMessage.Length);
                        clients[id].stream.Close();
                        clients[id].socket.Close();
                        Closing.shutDown = true;
                        handler.Set();
                        return;

                    default:
                        confirmMessage = System.Text.Encoding.ASCII.GetBytes("command not recognised");
                        clients[id].stream.WriteAsync(confirmMessage, 0, confirmMessage.Length);
                        break;
                }


            }
            clients[id].stream.Close();
            clients[id].socket.Close();
            return;
        }

        static void Main(string[] args)
        {
            //Instaniate our client list
            clients = new List<ClientData>();

            //Console.Write("enter the file path to use: \n");
            //FilePath.filePath = Console.ReadLine();

            Console.Write("please provide the IP address of the server host machine: \n");
            string address = Console.ReadLine();
            IPAddress ipAddress = IPAddress.Parse(address);

            Console.Write("please provide the port number for this server: \n");
            Int32 port = Int32.Parse(Console.ReadLine());
            TcpListener server = new TcpListener(ipAddress, port);

            server.Start();
                try
            {
                while (!Closing.shutDown)
                {
                    handler.Reset();
                    Console.Write("Waiting for connections... \n");
                    server.BeginAcceptTcpClient(new AsyncCallback(receiveCallBack), server);
                    handler.WaitOne();
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
            Thread.Sleep(500);
            server.Stop();
            Console.Write("press enter to exit \n");
            Console.ReadLine();
        }
    }
}
