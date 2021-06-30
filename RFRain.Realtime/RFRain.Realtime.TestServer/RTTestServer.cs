using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

/// <summary>
/// Created by Matt Hay on 5/19/21
/// Simple test server to test requests from the RFRainRealtimeAPI
/// </summary>
namespace RFRain.Realtime.TestServer
{
    /// <summary>
    /// State object for reading client data asynchronously
    /// </summary>
    public class StateObject
    {
        /// <summary>
        /// Size of receive buffer.
        /// </summary>
        public const int BUFFER_SIZE = 1024;

        /// <summary>
        /// Receive buffer.
        /// </summary>
        public byte[] buffer = new byte[BUFFER_SIZE];

        /// <summary>
        /// Our connection socket.
        /// </summary>
        public Socket workSocket = null;
    }

    class RTTestServer
    {
        /// <summary>
        /// default constructor
        /// </summary>
        public RTTestServer() { }

        /// <summary>
        /// Thread signal indicating: have we connected?
        /// </summary>
        public static ManualResetEvent connectDone = new ManualResetEvent(false);
        /// <summary>
        /// Thread signal indicating: is the reader on?
        /// </summary>
        public static ManualResetEvent readerOn = new ManualResetEvent(false);

        /// <summary>
        /// How long we wait between sending a tag event
        /// </summary>
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
        private static bool status = false; //tag events will not send if the reader status is false
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
            IPHostEntry ipHostInfo = Dns.GetHostEntry(Dns.GetHostName());//name of host running the program
            IPAddress ipAddress = ipHostInfo.AddressList[0];//get host ip
            IPEndPoint localEndpoint = new IPEndPoint(ipAddress, 11111);//establish local endpoint

            //Create TCP/IP Socket
            Socket listener = new Socket(ipAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

            try
            {
                //Associate network address to server socket
                listener.Bind(localEndpoint);

                listener.Listen(100); //listens for "clients" trying to connect

                while (true)
                {
                    // Set the event to nonsignaled state.  
                    connectDone.Reset();

                    // Start an asynchronous socket to listen for connections.  
                    Console.WriteLine("Waiting for a connection...");

                    listener.BeginAccept(
                        new AsyncCallback(AcceptCallback), listener);

                    // Wait until a connection is made before continuing.  
                    connectDone.WaitOne();
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("Error in StartServer: " + e.ToString());
            }
        }

        /// <summary>
        /// Called when the server socket is accepting a client (the API)
        /// </summary>
        /// <param name="ar">the async paramter passed in</param>
        public static void AcceptCallback(IAsyncResult ar)
        {
            // Signal the main thread to continue.  
            connectDone.Set();

            // Get the socket that handles the client request.  
            Socket listener = (Socket)ar.AsyncState;
            Socket handler = listener.EndAccept(ar);

            // now that we've connected to the API, send connection ack
            Send(handler, "ack nummessages=1\r\n");
            Send(handler, "ack version=reader version\r\n");

            // Create the state object.  
            StateObject state = new StateObject();
            state.workSocket = handler;

            try
            { //begin receiving a message from the API
                handler.BeginReceive(state.buffer, 0, StateObject.BUFFER_SIZE, 0,
                    new AsyncCallback(ReadCallback), state);

                //start sending tags if the reader is on and the API is connected
                SendTagEvents(handler);
            }
            catch (Exception e) { Console.WriteLine("Error in AcceptCallback: " + e.ToString()); }
        }

        /// <summary>
        /// Callback for a socket read, if the server gets anything from the API/client, we get it through here
        /// </summary>
        /// <param name="asyncResult">The async parameter passed in</param>
        public static void ReadCallback(IAsyncResult asyncResult)
        {
            // Retrieve the state object and the handler socket  
            // from the asynchronous state object.  
            StateObject state = (StateObject)asyncResult.AsyncState;
            Socket handler = state.workSocket;

            // Read data from the client socket.
            try
            {
                int bytesRead = handler.EndReceive(asyncResult);

                if (bytesRead > 0)//if we actually received something
                {
                    String content = Encoding.ASCII.GetString(state.buffer, 0, bytesRead);

                    Console.WriteLine("Server Received: " + content);

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
                                HandleRequests(handler, requests[i]);
                            }
                        }
                    }
                    else //we only received one request on this receive
                    {
                        HandleRequests(handler, content);
                    }

                }

                //listen again
                handler.BeginReceive(state.buffer, 0, StateObject.BUFFER_SIZE, 0,
                    new AsyncCallback(ReadCallback), state);
            }
            catch (SocketException se) { Console.WriteLine("Socket Error in ReadCallback: " + se.ToString()); }
            catch (Exception e) { Console.WriteLine("Error in ReadCallback: " + e.ToString()); }
        }

        /// <summary>
        /// This method handles any request that comes in from the API
        /// ALWAYS END A MESSAGE/RESPONSE SEND WITH \r\n
        /// </summary>
        /// <param name="handler">Our connected Socket</param>
        /// <param name="request">The request we received</param>
        public static void HandleRequests(Socket handler, string request)
        {
            sendSem.WaitOne();

            //handle the nummessages responses
            if ((request.Contains("com reader set") || request.Contains("com reader start") || request.Contains("com reader stop"))
                && !request.Contains("com reader set mute"))
                Send(handler, "ack nummessages=2\r\n");
            else
                Send(handler, "ack nummessages=1\r\n");

            if (request.Contains("com reader id") && !request.Contains("com reader identity"))//get id
                Send(handler, "ack response=B###EB######\r\n");
            if (request.Contains("com reader identity")) //get identity
                Send(handler, "ack response=" + readerName + " " + groupName + "\r\n");
            if (request.Contains("com reader mode")) //get mode
                Send(handler, "ack response=" + mode + "\r\n");
            if (request.Contains("com reader region")) //get region
                Send(handler, "ack response=North America\r\n");
            if (request.Contains("com reader power")) //get power
                Send(handler, "ack response=" + power + " dBm\r\n");
            if (request.Contains("com reader subzones")) //get subzones
                Send(handler, "ack response=" + subzones[0] + " " + subzones[1] + " " + subzones[2] + " " + subzones[3] + "\r\n");
            if (request.Contains("com reader monitor")) //get monitor
                Send(handler, "ack response=" + monitor + "\r\n");
            if (request.Contains("com reader target")) //get target
                Send(handler, "ack response=" + target + "\r\n");
            if (request.Contains("com reader readmode")) //get readmode
                Send(handler, "ack response=" + readMode + "\r\n");
            if (request.Contains("com reader tagmode")) //get tagmode
                Send(handler, "ack response=" + tagMode + "\r\n");
            if (request.Contains("com reader status")) //get status
            {
                if (status)
                    Send(handler, "ack response=on\r\n");
                else
                    Send(handler, "ack response=stop\r\n");
            }

            if (request.Contains("com reader mute")) //get mute
                Send(handler, "ack response=com mute " + muteStatus + "\r\n");


            if (request.Contains("com reader start")) //start reader
            {
                if (!status) //if the reader is not already on 
                {
                    Send(handler, "ack response=on\r\nack message=RFRain API - Reader started successfully\r\n");
                    status = true;
                    readerOn.Set(); //set the event boolean so that the SendTag method knows to begin sending tags
                }
                else
                    Send(handler, "ack response=on\r\nack message = WARNING: Reader is already started\r\n");
            }
            if (request.Contains("com reader stop")) //stop reader
            {
                if (status)
                {
                    Send(handler, "ack response=stop\r\nack message=RFRain API - The command completed successfully\r\n");
                    status = false;
                }
                else
                    Send(handler, "ack response=stop\r\nack message=Warning: Reader is already stopped\r\n");

            }


            if (request.Contains("com reader set identity")) //set identity
            {
                string names = request.Substring(request.IndexOf("set identity ") + 13);
                readerName = names.Split(" ")[0];
                groupName = names.Split(" ")[1];
                Send(handler, "ack response=success" + readerName + " " + groupName + "\r\nack message=Set Identity Message\r\n");
            }
            if (request.Contains("com reader set mode")) //set mode
            {
                mode = request.Substring(request.IndexOf("set mode ") + 9);

                Send(handler, "ack response=success\r\nack message=RFRain API - Request Succeeded");
            }
            if (request.Contains("com reader set power")) //set power
            {
                power = int.Parse(request.Substring(request.IndexOf("set power ") + 10));

                Send(handler, "ack response=success\r\nack message=additional power status info");
            }
            if (request.Contains("com reader set subzone")) //set subzone
            {
                string info = request.Substring(request.IndexOf("set subzone ") + 12);
                int portNum = int.Parse(info.Split(" ")[0]) - 1;

                subzones[portNum] = info.Split(" ")[1];

                Send(handler, "ack response=success\r\nack message=additional subzone status info");
            }
            if (request.Contains("com reader set monitor")) //set monitor
            {
                monitor = int.Parse(request.Substring(request.IndexOf("set monitor ") + 12));


                Send(handler, "ack response=success\r\nack message=additional monitor status info");
            }
            if (request.Contains("com reader set target")) //set target
            {
                target = request.Substring(request.IndexOf("set target ") + 11);

                Send(handler, "ack response=success\r\nack message=additional target status info");
            }
            if (request.Contains("com reader set readmode")) //set readmode
            {
                readMode = request.Substring(request.IndexOf("set readmode ") + 13);

                Send(handler, "ack response=success\r\nack message=additional readmodestatus info");
            }
            if (request.Contains("com reader set tagmode")) //set tagmode
            {
                tagMode = request.Substring(request.IndexOf("set tagmode ") + 12);

                Send(handler, "ack response=success\r\nack message=additional tagmode status info");
            }
            if (request.Contains("com reader set mute ")) //set mute
            {
                muteStatus = request.Substring(request.IndexOf("set mute ") + 9);

                Send(handler, "ack response=com set mute OK" + "\r\n");
            }

            sendSem.Release();
        }

        /// <summary>
        /// Begins sending data async back to the API/client
        /// </summary>
        /// <param name="handler">Our connected Socket</param>
        /// <param name="response">The message being sent to the API</param>
        private static void Send(Socket handler, String response)
        {
            //report when we are sending a request response, for testing
            if (!response.Contains("testinfo"))
                Console.WriteLine("Sending: " + response);

            // Convert the string data to byte data using ASCII encoding.  
            byte[] byteData = Encoding.ASCII.GetBytes(response);

            // Begin sending the data to the remote device.  
            try
            {
                handler.BeginSend(byteData, 0, byteData.Length, 0,
                    new AsyncCallback(SendCallback), handler);
            }
            catch (Exception e) { Console.WriteLine("Error in Send: " + e.ToString()); }
        }

        /// <summary>
        /// Sends data back to API/Client
        /// </summary>
        /// <param name="asyncResult">async parameter passed in</param>
        private static void SendCallback(IAsyncResult asyncResult)
        {
            try
            {
                // Retrieve the socket from the state object.  
                Socket handler = (Socket)asyncResult.AsyncState;

                // Complete sending the data to the remote device.  
                int bytesSent = handler.EndSend(asyncResult);
            }
            catch (Exception e)
            {
                Console.WriteLine("Error in SendCallback: " + e.ToString());
            }
        }

        /// <summary>
        /// Sends a tag event at an interval based on the TIME_BTWN_TAGS var
        /// Will only send if the reader status is on
        /// </summary>
        /// <param name="handler">Our connected Socket</param>
        private static void SendTagEvents(Socket handler)
        {
            if (status)  //if the reader is on
            {
                sendSem.WaitOne();

                Console.WriteLine("Sending a tag event");

                if (tagMode.Equals("EMBEDDED_TID_MEM"))
                {
                    Send(handler, "ack nummessages=2\r\n"); //send nummessages ack


                    string tagInfo = "ack taginfo=" + groupName + " " + readerName + " subz E200000012345142365 24 PRES Cabinet 182 emp 24\r\n"
                        + "ack tid=thisisTID\r\n";

                    Send(handler, tagInfo); //send tag info 
                }
                else if (tagMode.Equals("EMBEDDED_EPC_TID_MEM"))
                {
                    Send(handler, "ack nummessages=3\r\n"); //send nummessages ack


                    string tagInfo = "ack taginfo=" + groupName + " " + readerName + " subz E200000012345142365 24 PRES Cabinet 182 emp 24\r\n"
                        + "ack tid=thisisTID\r\n" + "ack epc=thisisEPC\r\n";

                    Send(handler, tagInfo); //send tag info 
                }
                else //if tagMode = EMBEDDED_ALL
                {
                    Send(handler, "ack nummessages=4\r\n"); //send nummessages ack


                    string tagInfo = "ack taginfo=" + groupName + " " + readerName + " subz E200000012345142365 24 PRES Cabinet 182 emp 24\r\n"
                        + "ack tid=thisisTID\r\n" + "ack epc=thisisEMP\r\n" + "ack user=thisisUSERDATA\r\n";

                    Send(handler, tagInfo); //send tag info 
                }

                sendSem.Release();

                Thread.Sleep(TIME_BTWN_TAGS);
            }
            else
            {
                readerOn.WaitOne(); //wait for the reader to turn on
                readerOn.Reset(); //reset the event boolean back to false
            }

            SendTagEvents(handler); //run again
        }


        /// <summary>
        /// Main
        /// </summary>
        public static void Main()
        {
            StartListening();
        }
    }
}
