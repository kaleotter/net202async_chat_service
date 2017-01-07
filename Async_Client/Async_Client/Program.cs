using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;
using System.IO;

namespace Async_Client
{

    public class ClientData
    {
        public TcpClient socket = null;
        public NetworkStream stream = null;
        public const int bufferSize = 1024;
        public byte[] buffer = new byte[bufferSize];
    }

    //this class defines what a message should contain
    public class ProtocolMessage
    {
        public string command,
                      fileName,
                      dataString;
        public Int32 filenameSize,
                        dataSize,
                        chunkSize,
                        commandSize,
                        commandSizeArray = 4,
                        chunkSizeArray = 4,
                        dataSizeArray = 4,
                        filenameSizeArray = 4;
    }

    public static class Closing
    {
        public static bool shutDown = false;
    }
    class Program
    {
        static void encodeMessage(ProtocolMessage message, ClientData client)
        {
            //begin formatting the message here
            int messageSize = 0;
            byte[] command = System.Text.Encoding.ASCII.GetBytes(message.command);          //this will be the command
            byte[] commandSize = BitConverter.GetBytes(message.commandSize);                //gives us the size of the command
            messageSize = messageSize + command.Length + commandSize.Length;
            byte[] filenameByte = new Byte[0];
            byte[] filenameSize = new byte[0]; 

            if (message.fileName != null)                                                  //if our message to send DOES have a filename
            {
                filenameByte = System.Text.Encoding.ASCII.GetBytes(message.fileName);
                filenameSize = BitConverter.GetBytes(message.filenameSize);
                
            }

            messageSize = messageSize + filenameSize.Length + filenameByte.Length;


            //processing complete, now we can begin encoding
            byte[] packageSizeBytes = BitConverter.GetBytes(message.chunkSize);
            byte[] dataSizeBytes = new byte[0];


            if (message.dataString != null)                                             //check to see if the message has data
            {
                byte[] package = new byte[message.chunkSize];
                dataSizeBytes = BitConverter.GetBytes(message.dataString.Length * 2);
            }

            messageSize = messageSize + packageSizeBytes.Length + dataSizeBytes.Length;

            //Now declare and initialize an array that contains all the data we need to send.
            byte[] messageArray = new byte[messageSize];

            //now we populate the array with the data we need to send, the order should be the same as the protocol we use.
            Array.Copy(commandSize, messageArray, commandSize.Length);
            Array.Copy(command, 0, messageArray, commandSize.Length, command.Length);
            Array.Copy(filenameSize, 0, messageArray, commandSize.Length + command.Length, filenameSize.Length);
            Array.Copy(filenameByte, 0, messageArray, commandSize.Length + command.Length + filenameSize.Length, filenameByte.Length);
            Array.Copy(packageSizeBytes, 0, messageArray, commandSize.Length + command.Length + filenameSize.Length + filenameByte.Length, packageSizeBytes.Length);
            Array.Copy(dataSizeBytes, 0, messageArray, commandSize.Length + command.Length + filenameSize.Length + filenameByte.Length + packageSizeBytes.Length, dataSizeBytes.Length);

            Console.Write("{0}, {1}, {2}, {3} \n", message.command, message.commandSize, message.fileName, message.filenameSize);

            //Now we can write the encoded package

            while (client.stream.DataAvailable)             //while there is data on the stream
            {
                client.stream.ReadAsync(client.buffer, 0, client.buffer.Length);            //read the contents of the stream into the buffer
            }
            try
            {
                client.stream.WriteAsync(messageArray, 0, messageArray.Length);             //write the new message to the stream
            }
            catch                                                                           //if something goes wrong writing the stream
            {
                Console.Write("Something has gone badly, badly wrong. Closing now /n");
                Closing.shutDown = true;
                return;
            }

          
        }

        static void sendPing(ClientData client)
        {
            ProtocolMessage message = new ProtocolMessage();                                    //we need to build the message to inform the server of what we are planning to do

            //give the message a command so the serer knows how to handle it
            message.command = "ping";                                                           
            message.commandSize = System.Text.Encoding.ASCII.GetBytes(message.command).Length;

            //send the command to the server
            encodeMessage(message, client);

            //now we get a response and process it
            while (client.stream.DataAvailable)                                                 //now we need to check weather there is any data availiable on the stream
            {
                client.stream.Read(client.buffer, 0, client.buffer.Length);                     //write the contents to the client buffer
            }

            byte[] confirmation = new byte[500];
            client.stream.Read(confirmation, 0, confirmation.Length);
            if (System.Text.Encoding.ASCII.GetString(confirmation).Trim('\0') != "command received!")          //if we dont get the message we expect
            {
                Console.Write("unrecognised reply. Exiting \n");
                return;
            }

            byte[] pingBytes = new byte[150];
            client.stream.Read(pingBytes, 0, pingBytes.Length);
            Console.Write(System.Text.Encoding.ASCII.GetString(pingBytes));

            while(client.stream.DataAvailable)
            {
                client.stream.Read(client.buffer, 0, client.buffer.Length);
            }

            return;
        }

        static void SendMessage (IAsyncResult ar)
        {
            while (!Closing.shutDown)
            {

            }
        }

        static void Main(string[] args)
        {
            string command = "";
            Console.Write("please enter the ip address of the server to connect to: \n ");
            string ipAddress = Console.ReadLine();

            Console.Write("Please enter the port for the server you wish to connect to: \n");
            Int32 port = Int32.Parse(Console.ReadLine());

            ClientData client = new ClientData();
            client.socket = new TcpClient();

            //IPEndPoint remoteEP = new IPEndPoint(ipAddress, port)
            
            try
            {
                client.socket.Connect(ipAddress, port);
                Console.Write("Connected Successfully! \n");
            }
            catch (Exception)
            {
                Console.Write("Failed to connect, press enter to close");
                Console.ReadLine();
                return;
            }
            client.stream = client.socket.GetStream();
            while (!Closing.shutDown)
            {
                Console.Write("Availiable commands: ping, quit \n");
                Console.Write("please Enter your command:");
                command = Console.ReadLine();
                switch (command)
                {
                    case "ping":
                        sendPing(client);
                        break;

                    case "quit":
                        Closing.shutDown = true;
                        ProtocolMessage message = new ProtocolMessage();

                        while (client.stream.DataAvailable)
                        {
                            client.stream.Read(client.buffer, 0, client.buffer.Length);
                        }
                        message.command = "quit";
                        message.commandSize = message.command.Length;
                        encodeMessage(message, client);
                        byte[] quitConfirm = new Byte[256];
                        try
                        {
                            client.stream.Read(quitConfirm, 0, quitConfirm.Length);
                        }
                        catch (Exception)
                        {
                            client.socket.Close();
                            return;
                        }
                        client.stream.Close();
                        client.socket.Close();
                        Console.Write("press enter to quit program.");
                        Console.ReadLine();
                        return;

                    default:
                        Console.Write("Command not recognised, please try again \n");
                        break;
                }
            }

            ProtocolMessage closeMessage = new ProtocolMessage();
            while (client.stream.DataAvailable)
            {
                client.stream.Read(client.buffer, 0, client.buffer.Length);
            }
            closeMessage.command = "quit";
            closeMessage.commandSize = closeMessage.command.Length;
            encodeMessage(closeMessage, client);

            byte[] closeConfirm = new byte[256];
            client.stream.Read(closeConfirm, 0, closeConfirm.Length);
            client.stream.Close();
            client.socket.Close();
            Console.Write("Press enter to quit.");
            Console.ReadLine();
            return;

            }

        }
    }

