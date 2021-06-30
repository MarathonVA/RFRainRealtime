using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace RFRain.Realtime.TestServerUDP
{
    /// <summary>
    /// Our state object helping to accept data from outside of the server asynchronously
    /// </summary>
    public class StateObjectUDP
    {
        // Size of receive buffer.  
        public const int BUFFER_SIZE = 1024;

        // Receive buffer.
        public byte[] byteData = new byte[BUFFER_SIZE];

        // Our connection socket.
        public Socket serverSocket = null;
    }

    class RTTestServerUDP
    {
        //default constructor
        public RTTestServerUDP() { }

        static string ADDRESS = "192.168.0.104";
        static int PORT = 11111;
        static UdpClient udpClient = new UdpClient();

        // Thread signals  
        public static ManualResetEvent readerOn = new ManualResetEvent(false); //is the reader on?
        public static ManualResetEvent PauseReceive = new ManualResetEvent(false);

        private static bool firstConnectionMade = false; //on the first connection this switches true and sends a connection response ack 

        //how long we wait between sending a tag event
        private static readonly int TIME_BTWN_TAGS = 3000; //3 seconds

        //variables for our "mock reader"
        private static string readerName = "readerName";
        private static string groupName = "groupName";
        private static string mode = "ServerModeEnhanced";
        private static int power = 26;
        private static string[] subzones = { "Zone1", "empty", "empty", "empty" };
        private static int monitor = 10;
        private static string target = "Target-A";
        private static string readMode = "S1";
        private static string tagMode = "EMBEDDED_ALL";
        private static bool status = true; //tag events will not send if the reader status is false
        private static string muteStatus = "off";

        /// <summary>
        /// Semaphore ensures that we are sending a full response before starting to send another
        /// </summary>
        private static Semaphore sendSem = new Semaphore(1, 1);

        /// <summary>
        /// Called when the server starts
        /// Creates a socket that listens for clients trying to connect
        /// Once a connection is made the server will begin receiving messages
        /// </summary>
        public static void StartListening()
        {
            try
            {
                udpClient.Client.Bind(new IPEndPoint(IPAddress.Parse(ADDRESS), PORT)); ;

                udpClient.BeginReceive(new AsyncCallback(ReceiveRequest), null);

                SendTagEvents(udpClient);
            }
            catch (Exception e) { Console.WriteLine("Error in StartListening: " + e.ToString()); }
        }

        /// <summary>
        /// Callback for a socket read, if the server gets anything from the API/client, we get it through here
        /// </summary>
        /// <param name="asyncResult"></param>
        public static void ReceiveRequest(IAsyncResult asyncResult)
        {
            PauseReceive.WaitOne();

            if (!firstConnectionMade)
            {
                Send(udpClient, "ack nummessages=1\r\n");
                Send(udpClient, "ack version=reader version\r\n");
                firstConnectionMade = true;
            }

            // Read data from the client socket.
            try
            {
                IPEndPoint remoteEP = new IPEndPoint(IPAddress.Parse(ADDRESS), PORT);
                byte[] received = udpClient.EndReceive(asyncResult, ref remoteEP);
                String content = Encoding.UTF8.GetString(received);
                Console.WriteLine("\nServer Received: " + content);

                if (received.Length > 0 && content.Contains("com reader"))//if we actually received something
                {
                    //if we received multiple messages from the API at the same time
                    //separate them out and send them individually - we use "com reader", because that's what each request begins with
                    if (content.LastIndexOf("com reader") != 0)
                    {
                        string[] requests = content.Split(new string[] { "com reader" }, StringSplitOptions.None);

                        for (int i = 0; i < requests.Length; i++)
                        {
                            //to ensure we do not send any blank elements in (split can sometimes split off empty strings
                            if (requests[i].Length > 1)
                            {
                                requests[i] = "com reader" + requests[i];
                                HandleRequests(udpClient, requests[i]);
                            }
                        }
                    }
                    else //we only received one request on this receive
                    {
                        HandleRequests(udpClient, content);
                    }

                }

                //listen again
                udpClient.BeginReceive(new AsyncCallback(ReceiveRequest), null);
            }
            catch (SocketException se) { Console.WriteLine("Socket Error in ReadCallback: " + se.ToString()); }
            catch (Exception e) { Console.WriteLine("Error in ReadCallback: " + e.ToString()); }
        }


        /// <summary>
        /// This method handles any request that comes in from the API
        /// ALWAYS END A MESSAGE/RESPONSE SEND WITH \r\n
        /// </summary>
        /// <param name="socket">the udp socket</param>
        /// <param name="request">the request that was sent in</param>
        public static void HandleRequests(UdpClient socket, string request)
        {
            sendSem.WaitOne();

            //handle the nummessages responses
            if ((request.Contains("com reader set") || request.Contains("com reader start") || request.Contains("com reader stop"))
                && !request.Contains("com reader set mute"))
                Send(socket, "ack nummessages=2\r\n");
            else
                Send(socket, "ack nummessages=1\r\n");

            if (request.Contains("com reader id") && !request.Contains("com reader identity"))//get id
                Send(socket, "ack response=B###EB######\r\n");
            if (request.Contains("com reader identity")) //get identity
                Send(socket, "ack response=" + readerName + " " + groupName + "\r\n");
            if (request.Contains("com reader mode")) //get mode
                Send(socket, "ack response=" + mode + "\r\n");
            if (request.Contains("com reader region")) //get region
                Send(socket, "ack response=North America\r\n");
            if (request.Contains("com reader power")) //get power
                Send(socket, "ack response=" + power + " dBm\r\n");
            if (request.Contains("com reader subzones")) //get subzones
                Send(socket, "ack response=" + subzones[0] + " " + subzones[1] + " " + subzones[2] + " " + subzones[3] + "\r\n");
            if (request.Contains("com reader monitor")) //get monitor
                Send(socket, "ack response=" + monitor + "\r\n");
            if (request.Contains("com reader target")) //get target
                Send(socket, "ack response=" + target + "\r\n");
            if (request.Contains("com reader readmode")) //get readmode
                Send(socket, "ack response=" + readMode + "\r\n");
            if (request.Contains("com reader tagmode")) //get tagmode
                Send(socket, "ack response=" + tagMode + "\r\n");
            if (request.Contains("com reader status")) //get status
            {
                if (status)
                    Send(socket, "ack response=on\r\n");
                else
                    Send(socket, "ack response=stop\r\n");
            }

            if (request.Contains("com reader mute")) //get mute
                Send(socket, "ack response=com mute " + muteStatus + "\r\n");


            if (request.Contains("com reader start")) //start reader
            {
                if (!status) //if the reader is not already on 
                {
                    Send(socket, "ack response=on\r\nack message=RFRain API - Reader started successfully\r\n");
                    status = true;
                    readerOn.Set(); //set the event boolean so that the SendTag method knows to begin sending tags
                }
                else
                    Send(socket, "ack response=on\r\nack message = WARNING: Reader is already started\r\n");
            }
            if (request.Contains("com reader stop")) //stop reader
            {
                if (status)
                {
                    Send(socket, "ack response=stop\r\nack message=RFRain API - The command completed successfully\r\n");
                    status = false;
                }
                else
                    Send(socket, "ack response=stop\r\nack message=Warning: Reader is already stopped\r\n");

            }


            if (request.Contains("com reader set identity")) //set identity
            {
                string names = request.Substring(request.IndexOf("set identity ") + 13);
                readerName = names.Split(" ")[0];
                groupName = names.Split(" ")[1];
                Send(socket, "ack response=success" + readerName + " " + groupName + "\r\nack message=Set Identity Message\r\n");
            }
            if (request.Contains("com reader set mode")) //set mode
            {
                mode = request.Substring(request.IndexOf("set mode ") + 9);

                Send(socket, "ack response=success\r\nack message=RFRain API - Request Succeeded");
            }
            if (request.Contains("com reader set power")) //set power
            {
                power = int.Parse(request.Substring(request.IndexOf("set power ") + 10));

                Send(socket, "ack response=success\r\nack message=additional power status info");
            }
            if (request.Contains("com reader set subzone")) //set subzone
            {
                string info = request.Substring(request.IndexOf("set subzone ") + 12);
                int portNum = int.Parse(info.Split(" ")[0]) - 1;

                subzones[portNum] = info.Split(" ")[1];

                Send(socket, "ack response=success\r\nack message=additional subzone status info");
            }
            if (request.Contains("com reader set monitor")) //set monitor
            {
                monitor = int.Parse(request.Substring(request.IndexOf("set monitor ") + 12));


                Send(socket, "ack response=success\r\nack message=additional monitor status info");
            }
            if (request.Contains("com reader set target")) //set target
            {
                target = request.Substring(request.IndexOf("set target ") + 11);

                Send(socket, "ack response=success\r\nack message=additional target status info");
            }
            if (request.Contains("com reader set readmode")) //set readmode
            {
                readMode = request.Substring(request.IndexOf("set readmode ") + 13);

                Send(socket, "ack response=success\r\nack message=additional readmodestatus info");
            }
            if (request.Contains("com reader set tagmode")) //set tagmode
            {
                tagMode = request.Substring(request.IndexOf("set tagmode ") + 12);

                Send(socket, "ack response=success\r\nack message=additional tagmode status info");
            }
            if (request.Contains("com reader set mute ")) //set mute
            {
                muteStatus = request.Substring(request.IndexOf("set mute ") + 9);

                Send(socket, "ack response=com set mute OK" + "\r\n");
            }

            sendSem.Release();
        }

        /// <summary>
        /// begins sending data async back to the API/client
        /// </summary>
        /// <param name="socket">the udp socket</param>
        /// <param name="response">the response to send</param>
        private static void Send(UdpClient socket, String response)
        {
            PauseReceive.Reset();
            //report when we are sending a request response, for testing
            Console.WriteLine("Sending: " + response);

            // Convert the string data to byte data using ASCII encoding.  
            byte[] data = Encoding.ASCII.GetBytes(response);

            // Begin sending the data to the remote device.  
            try
            {
                socket.SendAsync(data, data.Length, new IPEndPoint(IPAddress.Broadcast, PORT));
            }
            catch (Exception e) { Console.WriteLine("Error in Send: " + e.ToString()); }

            PauseReceive.Set();
        }

        /// <summary>
        /// sends a tag event at an interval based on the TIME_BTWN_TAGS var
        /// will only send if the reader status is on
        /// </summary>
        /// <param name="socket">the udp socket</param>
        private static void SendTagEvents(UdpClient socket)
        {
            if (status)  //if the reader is on
            {
                sendSem.WaitOne();

                Console.WriteLine("Sending a tag event");

                if (tagMode.Equals("EMBEDDED_TID_MEM"))
                {
                    Send(socket, "ack nummessages=2\r\n"); //send nummessages ack


                    string tagInfo = "ack taginfo=" + groupName + " " + readerName + " subz E200000012345142365 24 PRES Cabinet 182 emp 24\r\n"
                        + "ack tid=thisisTID\r\n";

                    Send(socket, tagInfo); //send tag info 
                }
                else if (tagMode.Equals("EMBEDDED_EPC_TID_MEM"))
                {
                    Send(socket, "ack nummessages=3\r\n"); //send nummessages ack


                    string tagInfo = "ack taginfo=" + groupName + " " + readerName + " subz E200000012345142365 24 PRES Cabinet 182 emp 24\r\n"
                        + "ack tid=thisisTID\r\n" + "ack epc=thisisEPC\r\n";

                    Send(socket, tagInfo); //send tag info 
                }
                else //if tagMode = EMBEDDED_ALL
                {
                    Send(socket, "ack nummessages=4\r\n"); //send nummessages ack


                    string tagInfo = "ack taginfo=" + groupName + " " + readerName + " subz E200000012345142365 24 PRES Cabinet 182 emp 24\r\n"
                        + "ack tid=thisisTID\r\n" + "ack epc=thisisEMP\r\n" + "ack user=thisisUSERDATA\r\n";

                    Send(socket, tagInfo); //send tag info 
                }

                sendSem.Release();

                Thread.Sleep(TIME_BTWN_TAGS);
            }
            else
            {
                readerOn.WaitOne(); //wait for the reader to turn on
                readerOn.Reset(); //reset the event boolean back to false
            }

            SendTagEvents(socket); //run again
        }

        /// <summary>
        /// main
        /// </summary>
        public static void Main()
        {
            StartListening();
        }
    }
}
