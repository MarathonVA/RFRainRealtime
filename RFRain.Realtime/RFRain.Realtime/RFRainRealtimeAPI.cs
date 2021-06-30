using System;
using System.Collections.Generic;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Linq;

/// <summary>
/// Created by Matt Hay on 5/18/21
/// </summary>
namespace RFRain.Realtime
{
     /// <summary>
     /// This State Object is used to receive messages from the reader asynchronously
     /// It includes: the Socket needed to receive a message
     /// the buffer taking in the message and its size
     /// the number of messages it is receiving for a specific call
     /// and the actual message
     /// </summary>
    public class ReceiverStateObject
    {
        /// <summary>
        /// TCP socket the API is using (we need it in here to transfer it between async methods)
        /// </summary>
        public Socket workSocket = null;

        /// <summary>
        /// The max buffer size
        /// </summary>
        public const int BUFFER_SIZE = 1024;

        /// <summary>
        /// buffer where we place data we've received into
        /// </summary>
        public byte[] buffer = new byte[BUFFER_SIZE];

        /// <summary>
        /// the number of messages in the received response
        /// </summary>
        public int numMessages = 0;

        /// <summary>
        /// our message data string
        /// </summary>
        public StringBuilder message = new StringBuilder();
    }


    public class RFRainRealtimeAPI : RealtimeAPIInterface
    {
        /// <summary>
        /// this is the tcp socket or udp socket for the api
        /// it will relay messages from the client to the reader and vice versa
        /// </summary>
        private Socket apiSocket;

        /// <summary>
        /// will our socket be TCP or UDP?
        /// </summary>
        private bool socketIsTCP;


        /// <summary>
        /// The endpoint from which we'll be receiving data (UDP only)
        /// </summary>
        private static EndPoint ipEndSender;

        /// <summary>
        /// the ip address we are connecting to
        /// </summary>
        private readonly string IP_ADDRESS;
        /// <summary>
        /// the port we are using
        /// </summary>
        private readonly int PORT;

        /// <summary>
        /// we will switch this event to true when a response is received, then reset it for the next
        /// </summary>
        private ManualResetEvent responseReceived = new ManualResetEvent(false);

        /// <summary>
        /// the list of each set of responses we have received from the reader that have not been matched with a request
        /// </summary>
        private List<string[]> messagesReceived = new List<string[]>();

        /// <summary>
        /// semaphore lock for our response list, so that no async received overlap on each other
        /// </summary>
        private Semaphore msgReceivedLock = new Semaphore(1, 1);

        /// <summary>
        /// event will fire when a tag is received 
        /// </summary>
        public event EventHandler<TagInfo> TagEvent;

        /// <summary>
        /// this boolean signals if the reader is currently on - we begin by assuming the reader is off
        /// the boolean switches to true when StartReader is called OR when the API receives a tag
        /// </summary>
        private bool READER_IS_ON = false;

        /// <summary>
        /// this bool will be true only if Disconnect() has been called. It signals the ReceiveCallback method to stop receiving 
        /// </summary>
        private bool disconnecting = false;

        /// <summary>
        /// 5 seconds of wait time to receive a messages
        /// </summary>
        public readonly int WAIT_TIME = 5000;
        /// <summary>
        /// the number of times we should wait for a response before resending a request
        /// </summary>
        public readonly int NUM_RESPONSE_WAITS = 3;

        /// <summary>
        /// thrown when a set is called but the reader is on
        /// </summary>
        Exception ReaderOnException = new Exception("ERROR: Cannot fulfill this request because the reader is still on");

        /// <summary>
        /// these are all of the types of requests that can be sent by the API
        /// used in the API when checking a response to see if it contains a request's ACK
        /// </summary>
        private enum RequestTypes
        {
            CONNECT,

            //Getters
            GET_ID,
            GET_IDENTITY,
            GET_MODE,
            GET_REGION,
            GET_POWER,
            GET_SUBZONES,
            GET_MONITOR,
            GET_TARGET,
            GET_READMODE,
            GET_TAGMODE,
            GET_STATUS,
            GET_MUTE,

            //Setters
            START,
            STOP,
            SET_MUTE,
            SET_IDENTITY,
            SET_MODE,
            SET_POWER,
            SET_SUBZONE,
            SET_MONITOR,
            SET_TARGET,
            SET_READMODE,
            SET_TAGMODE
        }

        /// <summary>
        /// Constructor: Takes in the IP Address,the port for the reader and whether the API is using a TCP or UDP socket
        /// </summary>
        /// <param name="ip_address">the reader IP</param>
        /// <param name="port">the reader port</param>
        /// <param name="isTCP">tcp = true, udp = false</param>
        public RFRainRealtimeAPI(string ip_address, int port, bool isTCP)
        {
            IP_ADDRESS = ip_address;
            PORT = port;
            socketIsTCP = isTCP;
        }


        /// <summary>
        /// This method connects the client to the reader.
        /// It will receive the connection ack and also send a request for and receive the reader status 
        /// The client will remain connected to the reader until Disconnect is called (or an error occurs)
        /// </summary>
        /// <returns>true if a connection was made</returns>
        public bool Connect() 
        {   
            //Setup local endpoint based on the IP address and port
            IPAddress address = IPAddress.Parse(IP_ADDRESS); //parse our IP from string 
            IPEndPoint localEndpoint = new IPEndPoint(address, PORT); //establish local endpoint  

            //Create our socket
            if(socketIsTCP)
            {
                apiSocket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                //connect to the reader
                apiSocket.Connect(localEndpoint);
            }   
            else
            {
                apiSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                //connect to the reader
                apiSocket.Bind(new IPEndPoint(IPAddress.Any,PORT));
                apiSocket.Connect(localEndpoint);
            }        

            //begin listening for responses from either UDP or TCP 
            //first response will be a connection ACK
            StartListener(apiSocket);
            string version = CheckForAck(RequestTypes.CONNECT);
            bool connected = version != null;

            //get our status immediately so we know if the reader is on or off before we send any more requests
            string status = GetStatus();
            bool statusReceived = status != null;

            //wait for our connection and status responses, if we have received both we have connected successfully
            return connected && statusReceived; 
        }


        /// <summary>
        /// Start Listener is called when the connection to the reader is made
        /// It will create a state object to hold the response data and open a listener that will listen
        /// for responses until the connection is terminated.
        /// </summary>
        /// <param name="receiver">the socket used to receive the message</param>
        private void StartListener(Socket receiver)
        {
            try
            {
                //Create state object that holds our socket and received message
                ReceiverStateObject state = new ReceiverStateObject()
                {
                    workSocket = receiver
                };

                //Receive data from the reader
                if (socketIsTCP)
                    receiver.BeginReceive(state.buffer, 0, ReceiverStateObject.BUFFER_SIZE, 0, new AsyncCallback(ReceiveCallback), state);
                else
                {
                    //Receive data from the reader
                    ipEndSender = new IPEndPoint(IPAddress.Broadcast, PORT);

                    receiver.BeginReceiveFrom(state.buffer, 0, ReceiverStateObject.BUFFER_SIZE, SocketFlags.None, ref ipEndSender, new AsyncCallback(ReceiveCallback), state);
                }         
            }
            catch (Exception e)
            {
                throw e;
            }
        }


        /// <summary>
        /// This is the callback method that recieves messages from the reader asynchronously
        /// The method first receives the readers "ack nummessages" response followed by the messages
        /// Once it is done receiving the response, it saves it to a string so its contents can be parsed
        /// This method is only called when the connection is made otherwise it is called by itself to keep receiving messages when they arrive
        /// </summary>
        /// <param name="asyncResult">An IAsyncResult argument based on its caller</param>
        public void ReceiveCallback(IAsyncResult asyncResult)
        {
            //get our state object from the AsyncResult
            ReceiverStateObject state = (ReceiverStateObject)asyncResult.AsyncState;
            //get our socket from the state object
            Socket receiver = state.workSocket;

            //as long as we aren't trying to disconnect
            if (!disconnecting)
            {
                int bytesRead;

                //Read data from the reader
                if (socketIsTCP)
                    bytesRead = receiver.EndReceive(asyncResult);
                else
                {
                    bytesRead = receiver.EndReceiveFrom(asyncResult, ref ipEndSender);
                }
                    
                    
                //the response we just received (in string form) - then split it into each indiv msg
                string response = Encoding.ASCII.GetString(state.buffer, 0, bytesRead);

                string[] messages = response.Split("\r\n");

                 foreach (string msg in messages)
                 {
                    if (msg.Length > 1) //splitting by line sometimes creates empty array entries, dont let those through
                    {
                        //if we are seeing a new response
                        if (state.message.Equals(string.Empty) && msg.Contains("ack nummessages"))
                        {
                            state.message.Append(msg); //add str to the message
                            state.numMessages = int.Parse(msg.Substring(msg.IndexOf("ack nummessages=") + 16)); //grab the number of messages
                        }
                        else if (msg.Contains("ack nummessages=")) //if we are mid string and find a new nummessage ack
                        {
                            //make sure nothing from the last message is left over 
                            if(msg.IndexOf("ack nummessages=") != 0)
                            {
                                string remainder = msg.Substring(0, msg.IndexOf("ack nummessages="));

                                if(remainder.Contains("ack")) //check whether its an entire ack or part of a previous ack, then add it to the last msg
                                    state.message.Append("\n" + remainder); 
                                else
                                    state.message.Append(remainder); 
                            }

                            FinishReceiving(state); //finish receiving our last message

                            //ensure the section of the new message contains a number, otherwise pass it off to the next run
                            if (msg.Any(c => char.IsDigit(c)))
                            {
                                state.numMessages = int.Parse(msg.Substring(msg.IndexOf("ack nummessages=") + 16)); //grab the number of messages
                            }
                            else
                            {
                                state.numMessages = 1;
                            }

                            state.message.Append(msg); //now that the message var is empty again, begin refilling it 
                        }
                        else if(msg.Contains("ack")) //if we are receiving an ack from the current msg  
                        {
                            //if we have part of the previous ack in this message, place them in the response appropiately
                            if (msg.IndexOf("ack") != 0)
                            {
                                state.message.Append(msg.Substring(0, msg.IndexOf("ack")));

                                state.message.Append("\n" + msg.Substring(msg.IndexOf("ack")));
                            }
                            else
                            {
                                state.message.Append("\n" + msg);
                            }

                            state.numMessages--;
                        }
                        else //if we are receiving a piece of an ack 
                        {
                            state.message.Append(msg);
                        }
                    }
                }

                //once we have read all of the messages in the response
                if (state.numMessages == 0)
                {
                    FinishReceiving(state);
                }

                //wait for the next message
                if (socketIsTCP)
                    receiver.BeginReceive(state.buffer, 0, ReceiverStateObject.BUFFER_SIZE, 0, new AsyncCallback(ReceiveCallback), state);
                else
                {
                    //Receive data from the reader
                    ipEndSender = new IPEndPoint(IPAddress.Any, PORT);
                    receiver.BeginReceiveFrom(state.buffer, 0, ReceiverStateObject.BUFFER_SIZE, SocketFlags.None, ref ipEndSender, new AsyncCallback(ReceiveCallback), state);
                }
            }
        }

        /// <summary>
        /// This method is called when a full message of responses has been received from the server/reader
        /// It checks to see if the message it has received is a tag or a regular response
        /// </summary>
        /// <param name="state">the state object (passed in Async in ReceiveCallback) used to collect the message</param>
        private void FinishReceiving(ReceiverStateObject state)
        {
            string fullMessage = state.message.ToString();

            if (fullMessage.Contains("taginfo")) //if this message contains a taginfo ACK, then it is a tag event
            {
                TagInfo newTag = new TagInfo(fullMessage); //create new taginfo object

                newTag.ToString();

                //remind the API that the reader is on since we are receiving tags
                READER_IS_ON = true;

                //fire off event
                TagEvent?.Invoke(null, newTag);

                //reset our state object message for the next recieve
                state.message.Clear();
            }
            else if (fullMessage.Contains("ack nummessages")) //if its not a tag event, add it to our response list
            {
                string[] messages = state.message.ToString().Split("\n");

                //enter and exit critical section (our msg list)
                msgReceivedLock.WaitOne();
                messagesReceived.Add(messages);
                msgReceivedLock.Release();

                //reset our state object message for the next recieve
                state.message.Clear();

                //signal that a full response (not a tag event) was received
                responseReceived.Set();
            }
            else
                state.message.Clear();
        }


        /// <summary>
        /// This method preps our message to be sent async
        /// </summary>
        /// <param name="sender">the socket that is sending the msg</param>
        /// <param name="msgToSend">the message being sent</param>
        private void SendMessage(Socket sender, string msgToSend)
        {
            //Convert string data to byte data
            byte[] byteData = Encoding.ASCII.GetBytes(msgToSend);

            //Begin sending - the callback will run the "SendCallback" method once the corresponding operation completes
            if(socketIsTCP)
                sender.BeginSend(byteData, 0, byteData.Length, 0, new AsyncCallback(SendCallback), sender);
            else
                sender.BeginSend(byteData, 0, byteData.Length, SocketFlags.None, new AsyncCallback(SendCallback), sender);
        }


        /// <summary>
        /// This method finishes sending a message to the reader and notifies us when it is complete
        /// </summary>
        /// <param name="asyncResult">The IAsyncResult references the status of the operation </param>
        private void SendCallback(IAsyncResult asyncResult)
        {
            // Get our socket from the state object   
            Socket sender = (Socket)asyncResult.AsyncState;

            // Complete sending the data to the remote device.  
            int bytesSent = sender.EndSend(asyncResult);
        }


        /// <summary>
        /// This method checks if an ack has been sent for a corresponding request
        /// After sending a request, the method will wait for up to 3 responses to come in from the reader (or for WAIT_TIME # of seconds to pass 3 times)
        /// If none of the acks received after 3 iterations match the request, we return null
        /// </summary>
        /// <param name="requestType">The type of request that is looking for its ack</param>
        /// <returns>the full message from the reader divides into each of its responses</returns>
        private string CheckForAck(RequestTypes requestType)
        {
            //receive the response messages
            responseReceived.Reset(); //reset signal

            int numWaits = NUM_RESPONSE_WAITS;

            //while we have not exceeded the number of waits
            while (numWaits > 0)
            {
                int index = 0; //counter for messagesReceived list

                //grab the first msg in the list. Does it have a matching nummessages? If so, remove it from the list and we have received our ack
                //if it does not have a matching nummessages, go to the next message in the list, if we reached the end of the list, wait for another response
                while (index < messagesReceived.Count)
                {
                    string[] currentResponse = messagesReceived[index];

                    string ack = CheckByRequestType(requestType, currentResponse);
                    //if this message is our request, remove it from the messagesReceived list and return
                    if (ack != null)
                    {
                        //enter and exitcritical section (our msg list)
                        msgReceivedLock.WaitOne();
                        messagesReceived.RemoveAt(index);
                        msgReceivedLock.Release();

                        return ack;
                    }

                    index++;
                }

                //wait for another response to come in. If one doesnt come after WAIT_TIME seconds, continue
                responseReceived.WaitOne(WAIT_TIME);
                responseReceived.Reset();
                numWaits--;
            }

            //if we didnt find an ack for our message, return null
            return null;
        }

        /// <summary>
        /// This is a helper method for the CheckForACK method
        /// It checks if the message contains the correct ACK based on the requestType
        /// </summary>
        /// <param name="requestType">the type of request we're looking for</param>
        /// <param name="message">the message we are currently viewing from the response list</param>
        /// <returns>the response variable info if it is found, null otherwise</returns>
        private string CheckByRequestType(RequestTypes requestType, string[] message)
        {

            //get the number of messages (the first message in the response contains this #)
            int numMessages = int.Parse(message[0].Substring(message[0].IndexOf("nummessages=") + 12));

            //if we received an error ACK
            if(message[1].Contains("reader ERROR"))
            {
                throw new Exception("The reader sent an error ACK regarding: " + requestType.ToString());
            }

            //all string[] messages have a "nummessages" ack and at least 1 other ack, this switch statement
            //assumes the array will have at least 2 elements
            switch(requestType)
            {
                case RequestTypes.CONNECT: //connection ack only returns the reader version 
                        if (numMessages == 1 && message[1].Contains("ack version=")) 
                        return message[1];
                    break;

                case RequestTypes.GET_ID:
                    if(numMessages == 1 && message[1].Contains("ack response=")) //is this the response string
                    {
                        string id = message[1].Substring(message[1].IndexOf("=") + 1);

                        if(id.Length >= 12) //if its an id, it will be of length 12 (>= in case of a hidden \r\n)
                        {
                            //ID format is B###EB###### - check for that 
                            if (id.IndexOf("B") == 0 && id.IndexOf("EB") == 4 )
                                return id;
                        }
                    }
                    break;

                case RequestTypes.GET_IDENTITY:
                    //we cannot determine the reader name and group name, but we can check if they are in the response
                    string identity = message[1].Length > 10 ? message[1].Substring(message[1].IndexOf("response=") + 9) : " ";
                    if (numMessages == 1 && identity.Split(" ").Length == 2) //if our message splits into 2 parts (ack response=readername groupname) then this is the right ack
                        return identity;
                    break;

                case RequestTypes.GET_MODE:
                    //as long as it returns one of the supported modes, its an ack for getMode
                    foreach (string mode in Enum.GetNames(typeof(SupportedModes)))
                    {
                        if (numMessages == 1 && message[1].Contains("ack response=" + mode)) 
                            return mode;
                    }
                    break;

                case RequestTypes.GET_REGION:
                    //as long as it returns one of the 7 major regions, its an ack for getRegion
                    if (numMessages == 1 && (message[1].Contains("response=North America") || message[1].Contains("response=South America") ||
                        message[1].Contains("response=Asia") || message[1].Contains("response=Africa") ||
                        message[1].Contains("response=Europe") || message[1].Contains("response=Australia")))
                        return message[1].Substring(message[1].IndexOf("response=") + 9);
                    break;

                case RequestTypes.GET_POWER:
                    //as long as it returns a number followed by dBm, its an ack for getPower
                    if (numMessages == 1 && message[1].Contains("dBm")) 
                        return message[1].Substring(message[1].IndexOf("response=") + 9);
                    break;

                case RequestTypes.GET_SUBZONES:
                    string ports = message[1].Length > 10 ? message[1].Substring(message[1].IndexOf("response=") + 9) : " ";

                    //subzones will return 4 separate port names, make sure they are there (we cant determine their name)
                    if (numMessages == 1 && ports.Split(" ").Length == 4) //if our message splits into 4 parts (ack response=port1, port2, port3, port4) then this is the right ack
                        return ports;
                    break;

                case RequestTypes.GET_MONITOR:
                    //as long as it returns a single value btwn 1 and 3000, its an ack for getMonitor
                    string response = message[1].Length > 10 ? message[1].Substring(message[1].IndexOf("response=") + 9) : " ";

                    int number;

                    if (!int.TryParse(response, out number))
                        break;

                    if (numMessages == 1 && number >= 1 && number <= 3000)
                        return response;
                    break;

                case RequestTypes.GET_TARGET:
                    foreach (string setting in Enum.GetNames(typeof(SupportedTargetSettings)))
                    {
                        //the enum name has an underscore, the actual setting has a dash - must use str.replace()
                        if (numMessages == 1 && message[1].Contains("ack response=" + setting.Replace("_", "-"))) 
                            return message[1].Substring(message[1].IndexOf("response=") + 9);
                    }
                    break;

                case RequestTypes.GET_READMODE:
                    foreach (string mode in Enum.GetNames(typeof(SupportedReadModes)))
                    {
                        if (numMessages == 1 && message[1].Contains("ack response=" + mode)) 
                            return message[1].Substring(message[1].IndexOf("response=") + 9);
                    }
                    break;

                case RequestTypes.GET_TAGMODE:
                    foreach (string mode in Enum.GetNames(typeof(SupportedTagModes)))
                    {
                        if (numMessages == 1 && message[1].Contains("ack response=" + mode)) 
                            return message[1].Substring(message[1].IndexOf("response=") + 9);
                    }
                    break;

                case RequestTypes.GET_STATUS:
                    //If this is the correct ack, set the READER_IS_ON variable accordingly for the API
                    if (numMessages == 1)
                    {
                        if (message[1].Contains("response=stop"))
                        {
                            READER_IS_ON = false;
                            return "stop";
                        }
                        else if (message[1].Contains("response=on"))
                        {
                            READER_IS_ON = true;
                            return "on";
                        }
                    }
                        
                    break;


                case RequestTypes.GET_MUTE:
                    if (numMessages == 1)
                    {
                        if (message[1].Contains("response=com mute off"))
                            return "com mute off";
                        if (message[1].Contains("response=com mute on"))
                            return "com mute on";
                    }
                    break;


                case RequestTypes.START:
                    //the first response tells us if the reader is on or off, the second sends us a message ack
                    if (numMessages == 2 && message[1].Contains("ack response=on") && 
                        (message[2].Contains("ack message=RFRain API - Reader started successfully") 
                        || message[2].Contains("ack message=WARNING: Reader is already started")))
                    {
                        READER_IS_ON = true;
                        return "on";
                    }      
                    break;

                case RequestTypes.STOP:
                    //the first response tells us if the reader is on or off, the second sends us a message ack
                    if (numMessages == 2 && message[1].Contains("ack response=stop") &&
                        (message[2].Contains("ack message=RFRain API - The command completed successfully") 
                        || message[2].Contains("ack message=Warning: Reader is already stopped")))
                    {
                        READER_IS_ON = false;
                        return "off";
                    }
                    break;

                case RequestTypes.SET_IDENTITY:
                    //we cannot determine the reader name and group name, but we can check if they are in the response
                    string names = message[1].Length > 10 ? message[1].Substring(message[1].IndexOf("response=") + 9) : " ";

                    //returns a response with a status message and a message with status info 
                    if (numMessages == 2 && names.Split(" ").Length == 2 && message[2].Contains("ack message="))
                    {
                        if (message[1].Contains("ack response=success"))
                            return "success";
                        else
                            throw new Exception("ERROR: SET_IDENTITY received an ACK, but the set was unsuccessful");
                    }
                    break;

                case RequestTypes.SET_MODE:
                    if (numMessages == 2 && (message[1].Contains("ack response=") || message[1].Contains("ack response=RMGR"))
                        && message[2].Contains("ack message="))
                    {
                        if (message[1].Contains("ack response=success"))
                            return "success";
                        else
                            throw new Exception("ERROR: SET_MODE received an ACK, but the set was unsuccessful");
                    }
                    break;

                case RequestTypes.SET_POWER:
                    //responds with "success" and then sends a message with status info
                    if (numMessages == 2 && message[1].Contains("ack response=")
                        && message[2].Contains("ack message="))
                    {
                        if (message[1].Contains("ack response=success"))
                            return "success";
                        else
                            throw new Exception("ERROR: SET_POWER received an ACK, but the set was unsuccessful");
                    }
                    break;

                case RequestTypes.SET_SUBZONE:
                    //returns a response with a status message and a message with status info 
                    if (numMessages == 2 && message[1].Contains("ack response=") && message[2].Contains("ack message="))
                    {
                        if (message[1].Contains("ack response=success"))
                            return "success";
                        else
                            throw new Exception("ERROR: SET_SUBZONE received an ACK, but the set was unsuccessful");
                    }
                    break;

                case RequestTypes.SET_MONITOR:
                    //returns a response with a status message and a message with status info 
                    if (numMessages == 2 && message[1].Contains("ack response=") && message[2].Contains("ack message="))
                    {
                        if (message[1].Contains("ack response=success"))
                            return "success";
                        else
                            throw new Exception("ERROR: SET_MONITOR received an ACK, but the set was unsuccessful");
                    }
                    break;

                case RequestTypes.SET_TARGET:
                    //returns a response with a status message and a message with status info 
                    if (numMessages == 2 && message[1].Contains("ack response=") && message[2].Contains("ack message=RFRain API - Request Succeeded"))
                    {
                        if (message[1].Contains("ack response=success"))
                            return "success";
                        else
                            throw new Exception("ERROR: SET_TARGET received an ACK, but the set was unsuccessful");
                    }
                    break;

                case RequestTypes.SET_READMODE:
                    //returns a response with a status message and a message with status info  
                    if (numMessages == 2 && message[1].Contains("ack response=") && message[2].Contains("ack message="))
                    {
                        if (message[1].Contains("ack response=success"))
                            return "success";
                        else
                            throw new Exception("ERROR: SET_READMODE received an ACK, but the set was unsuccessful");
                    }
                    break;

                case RequestTypes.SET_TAGMODE:
                    //returns a response with a status message and a message with status info 
                    if (numMessages == 2 && message[1].Contains("ack response=") && message[2].Contains("ack message="))
                    {
                        if (message[1].Contains("ack response=success"))
                            return "success";
                        else
                            throw new Exception("ERROR: SET_TAGMODE received an ACK, but the set was unsuccessful");
                    }
                    break;

                case RequestTypes.SET_MUTE:
                    if (numMessages == 1 && message[1].Contains("ack response=com set mute "))
                    {
                        //double check to make sure we get the OK (we will accept "com set mute O" as well in case any cutoff happens)
                        if (message[1].Contains("com set mute OK") || message[1].Contains("com set mute O"))
                            return "success";
                        else
                            throw new Exception("ERROR: Issue with muting reader. \"com set mute OK\" was not in its response.");
                    }
                    break;
            }

            //if none of these cases were valid, this message is not the matching ACK
            return null;
        }


        /// <summary>
        /// This method ensures that a command's corresponding ACK response was received
        /// If ReceiveResponse() returns null, this method will send another request
        /// If a request is sent 3 times and no responses are received, an error is thrown
        /// NOTE: This method does not handle set requests!! Only Getters and Start/Stop, set requests are handled in their own specific methods
        /// </summary>
        /// <param name="requestType">the type of command being sent</param>
        /// <returns>A string containing the variable info from the reader's response</returns>
        private string MakeGetRequest(RequestTypes requestType)
        {
            int numRequestSent = 0;

            while (numRequestSent < 3)
            {
                //switch will handle which request should be sent based on the requestType
                switch (requestType)
                {
                    case RequestTypes.GET_ID:
                        SetMute(true);
                        SendMessage(apiSocket, "com reader id");
                        SetMute(false); 
                        break;

                    case RequestTypes.GET_IDENTITY:
                        SetMute(true);
                        SendMessage(apiSocket, "com reader identity");
                        SetMute(false); 
                        break;

                    case RequestTypes.GET_MODE:
                        SetMute(true);
                        SendMessage(apiSocket, "com reader mode");
                        SetMute(false); 
                        break;

                    case RequestTypes.GET_REGION:
                        SetMute(true);
                        SendMessage(apiSocket, "com reader region");
                        SetMute(false); 
                        break;

                    case RequestTypes.GET_POWER:
                        SetMute(true);
                        SendMessage(apiSocket, "com reader power");
                        SetMute(false); 
                        break;

                    case RequestTypes.GET_SUBZONES:
                        SetMute(true);
                        SendMessage(apiSocket, "com reader subzones");
                        SetMute(false);
                        break;

                    case RequestTypes.GET_MONITOR:
                        SetMute(true);
                        SendMessage(apiSocket, "com reader monitor");
                        SetMute(false); 
                        break;

                    case RequestTypes.GET_TARGET:
                        SetMute(true);
                        SendMessage(apiSocket, "com reader target");
                        SetMute(false); 
                        break;

                    case RequestTypes.GET_READMODE:
                        SetMute(true);
                        SendMessage(apiSocket, "com reader readmode");
                        SetMute(false); 
                        break;

                    case RequestTypes.GET_TAGMODE:
                        SetMute(true);
                        SendMessage(apiSocket, "com reader tagmode");
                        SetMute(false); 
                        break;

                    case RequestTypes.GET_STATUS:
                        SetMute(true);
                        SendMessage(apiSocket, "com reader status");
                        SetMute(false); 
                        break;

                    case RequestTypes.GET_MUTE: 
                        SendMessage(apiSocket, "com reader mute"); 
                        break;

                    case RequestTypes.START:
                        SetMute(true);
                        SendMessage(apiSocket, "com reader start");
                        SetMute(false); 
                        break;

                    case RequestTypes.STOP:
                        SetMute(true);
                        SendMessage(apiSocket, "com reader stop");
                        SetMute(false); 
                        break;
                }

                string response = CheckForAck(requestType);

                if (response != null)
                    return response;
                else
                    numRequestSent++;
            }

            throw new Exception(requestType + " ACK not Received");
        }


        /// <summary>
        /// This method disconnects the client from the reader
        /// (Closes out the socket connection)
        /// </summary>
        public void Disconnect()
        {
            disconnecting = true;

            //disconnect the sockets
            apiSocket.Shutdown(SocketShutdown.Both);
            apiSocket.Close();
        }


        /******************************
        ****************************
        * GETTER REQUESTS
        **************************** 
        ******************************/

        /// <summary>
        /// Gets Reader ID
        /// </summary>
        /// <returns>The ID of the reader</returns>
        public string GetID()
        {
            return MakeGetRequest(RequestTypes.GET_ID);
        }

        /// <summary>
        /// This method requests the reader name and group name
        /// </summary>
        /// <returns>The reader and group names</returns>
        public string GetIdentity()
        {
            return MakeGetRequest(RequestTypes.GET_IDENTITY);
        }

        /// <summary>
        /// This method requests the reader mode
        /// </summary>
        /// <returns>The Mode</returns>
        public SupportedModes GetMode()
        {
            string response = MakeGetRequest(RequestTypes.GET_MODE);

            SupportedModes readerMode;
            Enum.TryParse<SupportedModes>(response, out readerMode);
            return readerMode;
        }

        /// <summary>
        /// This method requests the readers current region settings
        /// </summary>
        /// <returns>the region</returns>
        public string GetRegion()
        {
            return MakeGetRequest(RequestTypes.GET_REGION);
        }

        /// <summary>
        /// This method requests the readers current power settings
        /// </summary>
        /// <returns>The Power level in dBm</returns>
        public int GetPower()
        {
            string power = MakeGetRequest(RequestTypes.GET_POWER);
            power = power.Remove(power.IndexOf("dBm"));
            return int.Parse(power);
        }

        /// <summary>
        /// This method requests the readers current port settings
        /// </summary>
        /// <returns>All 4 subzones</returns>
        public string GetSubzones()
        {
            return MakeGetRequest(RequestTypes.GET_SUBZONES);
        }

        /// <summary>
        /// This method requests the amount of seconds before a tag is set to miss
        /// </summary>
        /// <returns>the Monitor Value</returns>
        public int GetMonitor()
        {
            return int.Parse(MakeGetRequest(RequestTypes.GET_MONITOR));
        }

        /// <summary>
        /// This method requests the readers target type
        /// </summary>
        /// <returns>The Target Setting</returns>
        public SupportedTargetSettings GetTarget()
        {
            string response =  MakeGetRequest(RequestTypes.GET_TARGET).Replace("-", "_");

            SupportedTargetSettings target;
            Enum.TryParse<SupportedTargetSettings>(response, out target);
            return target;
        }

        /// <summary>
        /// This method requests the readers tag session info
        /// </summary>
        /// <returns>The Read Mode</returns>
        public SupportedReadModes GetReadMode()
        {
            string response = MakeGetRequest(RequestTypes.GET_READMODE);

            SupportedReadModes readMode;
            Enum.TryParse<SupportedReadModes>(response, out readMode);
            return readMode;
        }

        /// <summary>
        /// This method requests the readers tag read mode info
        /// </summary>
        /// <returns>The Tag Mode</returns>
        public SupportedTagModes GetTagMode()
        {
            string response = MakeGetRequest(RequestTypes.GET_TAGMODE);

            SupportedTagModes tagMode;
            Enum.TryParse<SupportedTagModes>(response, out tagMode);
            return tagMode;
        }

        /// <summary>
        /// This method requests to see if the reader is transmitting and/or receiving tag info or not
        /// </summary>
        /// <returns>Whether the reader is on or stopped</returns>
        public string GetStatus()
        {
            return MakeGetRequest(RequestTypes.GET_STATUS);
        }

        /// <summary>
        /// This method gets the reader mute status 
        /// </summary>
        /// <returns>the mute status</returns>
        public string GetMute()
        {
            return MakeGetRequest(RequestTypes.GET_MUTE);
        }


        /// <summary>
        /// This method sends the start message to the reader and gets its response
        /// The start message type starts transmitting and receiving tag information on the ports
        /// </summary>
        /// <returns>"on" indicating that the reader has been started. Otherwise an error is thrown</returns>
        public string StartReader()
        {
            return MakeGetRequest(RequestTypes.START);
        }

        /// <summary>
        /// This method sends the stop message to the reader and gets its response
        /// The stop message type stops transmitting and receiving tag information on the ports
        /// </summary>
        /// <returns>"stop" confirming that the reader is stopped. Otherwise an error is thrown</returns>
        public string StopReader()
        {
            return MakeGetRequest(RequestTypes.STOP);
        }



        /******************************
        **************************** 
        * SETTER REQUESTS
        **************************** 
        ******************************/

        /// <summary>
        /// Sets the reader name and group name
        /// </summary>
        /// <param name="readerName">reader name to be set</param>
        /// <param name="groupName">group name to be set</param>
        /// 
        /// NOTICE: The reader MUST be stopped before a set can be called 
        /// 
        /// <returns>Returns the ack received from the reader</returns>
        public bool SetIdentity(string readerName, string groupName)
        {
            int numRequestSent = 0;

            while (numRequestSent < 3)
            {
                //ensure the reader is turned off
                if (READER_IS_ON)
                {
                    throw ReaderOnException;
                }

                string command = "com reader set identity " + readerName + " " + groupName;

                SetMute(true);

                SendMessage(apiSocket, command);

                SetMute(false);

                if (CheckForAck(RequestTypes.SET_IDENTITY) != null)
                    return true;
                else
                    numRequestSent++;
            }

            throw new Exception(RequestTypes.SET_IDENTITY + " ACK not Received");
        }


        /// <summary>
        /// Sets the reader's mode
        /// </summary>
        /// <param name="mode">the new mode to be set</param>
        /// 
        /// NOTICE: The reader MUST be stopped before a set can be called 
        /// 
        /// <returns>Returns the ack received from the reader</returns>
        public bool SetMode(SupportedModes mode)
        {
            int numRequestSent = 0;

            while (numRequestSent < 3)
            {
                //ensure the reader is turned off
                if (READER_IS_ON)
                {
                    throw ReaderOnException;
                }

                string command = "com reader set mode " + Enum.GetName(typeof(SupportedModes), mode);

                //ensure the reader is off and muted before we make the change
                //if it was on, turn it off and then back on after the change is finished
                SetMute(true);

                SendMessage(apiSocket, command);

                SetMute(false);

                if (CheckForAck(RequestTypes.SET_MODE) != null)
                    return true;
                else
                    numRequestSent++;
            }

            throw new Exception(RequestTypes.SET_MODE + " ACK not Received");
        }


        /// <summary>
        /// Sets the reader power level in dBm
        /// </summary>
        /// <param name="powerLvl">the new pwr lvl to be set</param>
        /// 
        /// NOTICE: The reader MUST be stopped before a set can be called 
        /// 
        /// <returns>Returns the ack received from the reader</returns>
        public bool SetPower(int powerLvl)
        {
            //make sure the inputted power level is valid for the reader
            if (powerLvl > 30 || powerLvl < 10) 
            {
                throw new Exception("ERROR: Cannot set the power level to this value");
            }
            else
            {
                int numRequestSent = 0;

                while (numRequestSent < 3)
                {
                    //ensure the reader is turned off
                    if (READER_IS_ON)
                    {
                        throw ReaderOnException;
                    }

                    string command = "com reader set power " + powerLvl;

                    //ensure the reader is off and muted before we make the change
                    //if it was on, turn it off and then back on after the change is finished
                    SetMute(true);

                    SendMessage(apiSocket, command);

                    SetMute(false);

                    if (CheckForAck(RequestTypes.SET_POWER) != null)
                        return true;
                    else
                        numRequestSent++;
                }

                throw new Exception(RequestTypes.SET_POWER + " ACK not Received");
            }
        }


        /// <summary>
        /// Sets a reader port name setting
        /// </summary>
        /// <param name="port">the port # we are changing</param>
        /// <param name="portName">the name we are changing it to</param>
        /// 
        /// NOTICE: The reader MUST be stopped before a set can be called 
        /// 
        /// <returns>Returns the ack received from the reader</returns>
        public bool SetSubzone(int port, string portName)
        {
            //There are only 4 ports, a port name is at most 8 characters long
            if (port >= 1 && port <= 4 && portName.Length <= 8)
            {
                int numRequestSent = 0;

                while (numRequestSent < 3)
                {
                    //ensure the reader is turned off
                    if (READER_IS_ON)
                    {
                        throw ReaderOnException;
                    }

                    string command = "com reader set subzone " + port + " " + portName;

                    //ensure the reader is off and muted before we make the change
                    //if it was on, turn it off and then back on after the change is finished
                    SetMute(true);

                    SendMessage(apiSocket, command);

                    SetMute(false);

                    if (CheckForAck(RequestTypes.SET_SUBZONE) != null)
                        return true;
                    else
                        numRequestSent++;
                }

                throw new Exception(RequestTypes.SET_SUBZONE + " ACK not Received");
            }
            else
                throw new Exception("ERROR: Invalid port number or port name.");
        }


        /// <summary>
        /// Sets the reader miss tag time in seconds
        /// </summary>
        /// <param name="missTime">the miss time to be set</param>
        /// 
        /// NOTICE: The reader MUST be stopped before a set can be called
        /// 
        /// <returns>Returns the ack received from the reader</returns>
        public bool SetMonitor(int missTime)
        {
            //miss time can be between 1 and 3000 ms
            if (missTime >= 1 && missTime <= 3000)
            {
                int numRequestSent = 0;

                while (numRequestSent < 3)
                {
                    //ensure the reader is turned off
                    if (READER_IS_ON)
                    {
                        throw ReaderOnException;
                    }

                    string command = "com reader set monitor " + missTime;

                    //ensure the reader is off and muted before we make the change
                    //if it was on, turn it off and then back on after the change is finished
                    SetMute(true);

                    SendMessage(apiSocket, command);

                    SetMute(false);

                    if (CheckForAck(RequestTypes.SET_MONITOR) != null)
                        return true;
                    else
                        numRequestSent++;
                }

                throw new Exception(RequestTypes.SET_MONITOR + " ACK not Received");
            }
            else
                throw new Exception("ERROR: Invalid reader miss tag time.");
        }


        /// <summary>
        /// Sets the reader target setting according to the Rain protocol
        /// </summary>
        /// <param name="setting">the new target setting</param>
        /// 
        /// NOTICE: The reader MUST be stopped before a set can be called
        /// 
        /// <returns>Returns the ack received from the reader</returns>
        public bool SetTarget(SupportedTargetSettings setting)
        {
            int numRequestSent = 0;

            while (numRequestSent < 3)
            {
                //ensure the reader is turned off
                if (READER_IS_ON)
                {
                    throw ReaderOnException;
                }

                string command = "com reader set target " + Enum.GetName(typeof(SupportedTargetSettings), setting).Replace("_", "-");

                //ensure the reader is off and muted before we make the change
                //if it was on, turn it off and then back on after the change is finished
                SetMute(true);

                SendMessage(apiSocket, command);

                SetMute(false);

                if (CheckForAck(RequestTypes.SET_TARGET) != null)
                    return true;
                else
                    numRequestSent++;
            }

            throw new Exception(RequestTypes.SET_TARGET + " ACK not Received");
        }


        /// <summary>
        /// Sets the session setting according to the Rain protocol
        /// </summary>
        /// <param name="readMode">the new read mode to be set</param>
        /// 
        /// NOTICE: The reader MUST be stopped before a set can be called
        /// 
        /// <returns>Returns the ack received from the reader</returns>
        public bool SetReadMode(SupportedReadModes readMode)
        {
            int numRequestSent = 0;

            while (numRequestSent < 3)
            {
                //ensure the reader is turned off
                if (READER_IS_ON)
                {
                    throw ReaderOnException;
                }

                string command = "com reader set readmode " + Enum.GetName(typeof(SupportedReadModes), readMode);

                //ensure the reader is off and muted before we make the change
                //if it was on, turn it off and then back on after the change is finished
                SetMute(true);

                SendMessage(apiSocket, command);

                SetMute(false);

                if (CheckForAck(RequestTypes.SET_READMODE) != null)
                    return true;
                else
                    numRequestSent++;
            }

            throw new Exception(RequestTypes.SET_READMODE + " ACK not Received");
        }


        /// <summary>
        /// Sets the additional data information to read from each tag
        /// </summary>
        /// <param name="tagMode">the new tag mode to be set</param>
        /// 
        /// NOTICE: The reader MUST be stopped before a set can be called
        /// 
        /// <returns>Returns the ack received from the reader</returns>
        public bool SetTagMode(SupportedTagModes tagMode)
        {
            int numRequestSent = 0;

            while (numRequestSent < 3)
            {
                //ensure the reader is turned off
                if (READER_IS_ON)
                {
                    throw ReaderOnException;
                }

                string command = "com reader set tagmode " + Enum.GetName(typeof(SupportedTagModes), tagMode);

                //ensure the reader is off and muted before we make the change
                //if it was on, turn it off and then back on after the change is finished
                SetMute(true);

                SendMessage(apiSocket, command);

                SetMute(false);

                if (CheckForAck(RequestTypes.SET_TAGMODE) != null)
                    return true;
                else
                    numRequestSent++;
            }

            throw new Exception(RequestTypes.SET_TAGMODE + " ACK not Received");
        }


        /// <summary>
        /// This method mutes or unmutes the reader
        /// </summary>
        /// <param name="muting">true (on) to mute, false (off) to unmute</param>
        /// <returns>Returns the ack received from the reader</returns>
        public bool SetMute(bool muting)
        {
            int numRequestSent = 0;

            while (numRequestSent < 3)
            {
                if (muting)
                {
                    SendMessage(apiSocket, "com reader set mute on");
                }
                else //if we are unmuting
                {
                    SendMessage(apiSocket, "com reader set mute off");
                }

                if (CheckForAck(RequestTypes.SET_MUTE) != null)
                    return true;
                else
                    numRequestSent++;
            }

            throw new Exception(RequestTypes.SET_MUTE + " ACK not Received");
        }
        
    }
}
