using System;
using System.Net;
using System.Threading;

/*
 * Created by Matt Hay on 5/18/21
 */
namespace RFRain.Realtime.RTTestApp
{
    class RTTestApp
    {      
        //use Thread.Sleep(int millis) to wait for tag events to come in 
        public static void Main()
        {
            API_Reader_Test_TCP();
            //API_Test_Scenario_TCP(); 
            //API_Test_Scenario_UDP(); 
        }

        /// <summary>
        /// tag event handler - fires when a tag event happens 
        /// </summary>
        /// <param name="sender">sender of tag info</param>
        /// <param name="info">tag info </param>
        public static void API_TagEvent(object sender, TagInfo info)
        {
            Console.WriteLine("////////////\n TAG EVENT\n///////////");
            Console.WriteLine(info.ToString());
        }

        /// <summary>
        /// this test scenario can be run with a connection to a reader OR the RTTestServer Project
        /// </summary>
        public static void API_Reader_Test_TCP()
        {
            //MODIFY THE IP ADDRESS AND PORT TO FIT YOUR TESTING PURPOSES BEFORE RUNNING
            RealtimeAPIInterface realTimeAPI = new RFRainRealtimeAPI("127.0.0.1", 11111, true);

            //add our method so that its called when the tag event happens  
            realTimeAPI.TagEvent += API_TagEvent;

            ConnectToReader(realTimeAPI);

            RequestID(realTimeAPI);

            RequestMode(realTimeAPI);

            Thread.Sleep(5000); //waiting a bit to take in more tag events

            RequestPower(realTimeAPI);

            StopReader(realTimeAPI);

            RequestReadMode(realTimeAPI);

            Thread.Sleep(3000);

            StartReader(realTimeAPI);

            Thread.Sleep(3000);

            RequestRegion(realTimeAPI);

            DisconnectReader(realTimeAPI);
            Console.WriteLine("Normal Termination");
        }

        /// <summary>
        /// Use this test scenario alongside the RTTestServer Project
        /// it can also be tested with any reader as long as it is okay for the readers properties to be changed
        /// </summary>
        public static void API_Test_Scenario_TCP()
        {
            //This ip and port info corresponds with the test server
            IPHostEntry ipHostInfo = Dns.GetHostEntry(Dns.GetHostName());//name of host running the program
            IPAddress ip_address = ipHostInfo.AddressList[0];//get host ip
            RealtimeAPIInterface realTimeAPI = new RFRainRealtimeAPI(ip_address.ToString(), 11111, true);

            //add our method so that its called when the tag event happens  
            realTimeAPI.TagEvent += API_TagEvent;

            ConnectToReader(realTimeAPI);

            Console.WriteLine("\n///////////////////////////////////////////////////////\n" +
                "Testing Sending Requests and Receiving Responses/ACKs\n" +
                "///////////////////////////////////////////////////////\n");

            RequestID(realTimeAPI);

            RequestIdentity(realTimeAPI);

            RequestMode(realTimeAPI);

            RequestRegion(realTimeAPI);

            RequestMonitor(realTimeAPI);

            RequestMute(realTimeAPI);

            RequestPower(realTimeAPI);

            RequestReadMode(realTimeAPI);

            RequestStatus(realTimeAPI);

            RequestSubzones(realTimeAPI);

            RequestTagMode(realTimeAPI);

            RequestTarget(realTimeAPI);

            Console.WriteLine("\n/////////////////////////////////////////////////\n" +
                "Testing Tag Events Sending Alongside Responses\n" +
                "/////////////////////////////////////////////////\n");

            StartReader(realTimeAPI);

            RequestID(realTimeAPI);

            RequestIdentity(realTimeAPI);

            RequestMode(realTimeAPI);

            RequestMonitor(realTimeAPI);

            RequestMute(realTimeAPI);

            RequestPower(realTimeAPI);

            Thread.Sleep(5000); //sleeping a bit to see some tags come in a pack

            RequestReadMode(realTimeAPI);

            RequestRegion(realTimeAPI);

            RequestStatus(realTimeAPI);

            RequestSubzones(realTimeAPI);

            Thread.Sleep(5000); //sleeping a bit to see some tags come in a pack

            RequestTagMode(realTimeAPI);

            RequestTarget(realTimeAPI);

            StopReader(realTimeAPI);

            Console.WriteLine("\n/////////////////////////////////////////////////\n" +
                "Testing Set Commands\n" +
                "/////////////////////////////////////////////////\n");

            RequestMode(realTimeAPI);
            SetMode(realTimeAPI, SupportedModes.ServerMode);
            RequestMode(realTimeAPI);

            RequestIdentity(realTimeAPI);
            SetIdentity(realTimeAPI, "gName", "rName");
            RequestIdentity(realTimeAPI);

            RequestSubzones(realTimeAPI);
            SetSubzone(realTimeAPI, 3, "NewPort");
            RequestSubzones(realTimeAPI);

            Console.WriteLine("\n////////////////////////\n" +
                "Testing Errors\n" +
                "////////////////////////\n");

            try
            {
                SetPower(realTimeAPI, 100); //too large of a value
            }
            catch (Exception e) { Console.WriteLine(e + "\n"); }

            try
            {
                SetSubzone(realTimeAPI, 12, "ABCDEFGHIJKLMNOP"); //non-existent port and too long of a name
            }
            catch (Exception e) { Console.WriteLine(e + "\n"); }

            try
            {
                SetMonitor(realTimeAPI, 0); //too low of a number
            }
            catch (Exception e) { Console.WriteLine(e + "\n"); }


            Console.WriteLine("Normal Termination");
        }

        /// <summary>
        /// Use this test scenario for UDP connections
        /// </summary>
        public static void API_Test_Scenario_UDP()
        {
            //This ip and port info corresponds with the test server   -- MODIFY IP AND PORT FIRST TO FIT YOUR TESTING PURPOSES
            RealtimeAPIInterface realTimeAPI = new RFRainRealtimeAPI("127.0.0.1", 11111, false);
            //add our method so that its called when the tag event happens  
            realTimeAPI.TagEvent += API_TagEvent;

            ConnectToReader(realTimeAPI);

            RequestID(realTimeAPI);

            RequestIdentity(realTimeAPI);

            RequestMode(realTimeAPI);

            RequestRegion(realTimeAPI);

            DisconnectReader(realTimeAPI);
            Console.WriteLine("Normal Termination");
        }


        /*********************
         * *****************
         * HELPER METHODS
         * *****************
         *********************/

        /// <summary>
        /// Connect to the reader
        /// </summary>
        /// <param name="realTimeAPI">api object</param>
        public static void ConnectToReader(RealtimeAPIInterface realTimeAPI)
        {
            Console.WriteLine("CONNECTING TO READER");
            if(realTimeAPI.Connect())
                Console.WriteLine("Connection ACK Received\n");
            else
                Console.WriteLine("WARNING: No Connection ACK Received\n");
        }

        /// <summary>
        /// Disconnect from the reader
        /// </summary>
        /// <param name="realTimeAPI">api object</param>
        public static void DisconnectReader(RealtimeAPIInterface realTimeAPI)
        {
            Console.WriteLine("DISCONNECTING");
            realTimeAPI.Disconnect();
        }

        /// <summary>
        /// Start the reader
        /// </summary>
        /// <param name="realTimeAPI">api object</param>
        public static void StartReader(RealtimeAPIInterface realTimeAPI)
        {
            Console.WriteLine("STARTING READER");
            Console.WriteLine("StartReader ACK Received: " + realTimeAPI.StartReader() + "\n");
        }

        /// <summary>
        /// Stop the reader
        /// </summary>
        /// <param name="realTimeAPI">api object</param>
        public static void StopReader(RealtimeAPIInterface realTimeAPI)
        {
            Console.WriteLine("STOPPING READER");
            Console.WriteLine("StopReader ACK Received: " + realTimeAPI.StopReader() + "\n");
        }


        /***********************************************************
        * Use these methods to make different requests (gets/sets) 
        ***********************************************************/

        /*****************
        * REQUESTS/GETS
        *****************/

        public static void RequestID(RealtimeAPIInterface realTimeAPI)
        {
            Console.WriteLine("REQUESTING ID");
            Console.WriteLine("RequestID ACK Received: " + realTimeAPI.GetID() + "\n");
        }

        public static void RequestIdentity(RealtimeAPIInterface realTimeAPI)
        {
            Console.WriteLine("REQUESTING IDENTITY");
            Console.WriteLine("RequestIdentity ACK Received: " + realTimeAPI.GetIdentity() + "\n");
        }

        public static void RequestMode(RealtimeAPIInterface realTimeAPI)
        {
            Console.WriteLine("REQUESTING MODE");
            Console.WriteLine("RequestMode ACK Received: " + realTimeAPI.GetMode() + "\n");
        }

        public static void RequestRegion(RealtimeAPIInterface realTimeAPI)
        {
            Console.WriteLine("REQUESTING REGION");
            Console.WriteLine("RequestRegion ACK Received: " + realTimeAPI.GetRegion() + "\n");
        }

        public static void RequestPower(RealtimeAPIInterface realTimeAPI)
        {
            Console.WriteLine("REQUESTING POWER");
            Console.WriteLine("RequestPower ACK Received: " + realTimeAPI.GetPower() + "\n");
        }

        public static void RequestSubzones(RealtimeAPIInterface realTimeAPI)
        {
            Console.WriteLine("REQUESTING SUBZONES");
            Console.WriteLine("RequestSubzones ACK Received: " + realTimeAPI.GetSubzones() + "\n");
        }

        public static void RequestMonitor(RealtimeAPIInterface realTimeAPI)
        {
            Console.WriteLine("REQUESTING MONITOR");
            Console.WriteLine("RequestMonitor ACK Received: " + realTimeAPI.GetMonitor() + "\n");
        }

        public static void RequestTarget(RealtimeAPIInterface realTimeAPI)
        {
            Console.WriteLine("REQUESTING TARGET");
            Console.WriteLine("RequestTarget ACK Received: " + realTimeAPI.GetTarget() + "\n");
        }

        public static void RequestReadMode(RealtimeAPIInterface realTimeAPI)
        {
            Console.WriteLine("REQUESTING READMODE");
            Console.WriteLine("RequestReadMode ACK Received: " + realTimeAPI.GetReadMode() + "\n");
        }

        public static void RequestTagMode(RealtimeAPIInterface realTimeAPI)
        {
            Console.WriteLine("REQUESTING TAGMODE");
            Console.WriteLine("RequestTagMode ACK Received: " + realTimeAPI.GetTagMode() + "\n");
        }

        public static void RequestStatus(RealtimeAPIInterface realTimeAPI)
        {
            Console.WriteLine("REQUESTING STATUS");
            Console.WriteLine("RequestStatus ACK Received: " + realTimeAPI.GetStatus() + "\n");
        }

        public static void RequestMute(RealtimeAPIInterface realTimeAPI)
        {
            Console.WriteLine("REQUESTING MUTE STATUS");
            Console.WriteLine("RequestMute ACK Received: " + realTimeAPI.GetMute() + "\n");
        }

        /*****************
         * SET REQUESTS
         *****************/

        public static void SetIdentity(RealtimeAPIInterface realTimeAPI, string readName, string groupName)
        {
            Console.WriteLine("ATTEMPTING TO SET IDENTITY");
            if (realTimeAPI.SetIdentity(readName, groupName))
                Console.WriteLine("SetIdentity ACK Received\n");
        }

        public static void SetMode(RealtimeAPIInterface realTimeAPI, SupportedModes mode)
        {
            Console.WriteLine("ATTEMPTING TO SET MODE");
            if (realTimeAPI.SetMode(mode))
                Console.WriteLine("SetMode ACK Received\n");
        }

        public static void SetPower(RealtimeAPIInterface realTimeAPI, int power)
        {
            Console.WriteLine("ATTEMPTING TO SET POWER");
            if (realTimeAPI.SetPower(power))
                Console.WriteLine("SetPower ACK Received\n");
        }

        public static void SetSubzone(RealtimeAPIInterface realTimeAPI, int portNum, string port)
        {
            Console.WriteLine("ATTEMPTING TO SET SUBZONE");
            if (realTimeAPI.SetSubzone(portNum, port))
                Console.WriteLine("SetSubzone ACK Received\n");
        }

        public static void SetMonitor(RealtimeAPIInterface realTimeAPI, int time)
        {
            Console.WriteLine("ATTEMPTING TO SET MONITOR/MISS TAG TIME");
            if (realTimeAPI.SetMonitor(time))
                Console.WriteLine("SetMonitor ACK Received\n");
        }

        public static void SetTarget(RealtimeAPIInterface realTimeAPI, SupportedTargetSettings setting)
        {
            Console.WriteLine("ATTEMPTING TO SET TARGET");
            if (realTimeAPI.SetTarget(setting))
                Console.WriteLine("SetTarget ACK Received\n");
        }

        public static void SetReadMode(RealtimeAPIInterface realTimeAPI, SupportedReadModes readMode)
        {
            Console.WriteLine("ATTEMPTING TO SET READ MODE");
            if (realTimeAPI.SetReadMode(readMode))
                Console.WriteLine("SetReadMode ACK Received\n");
        }

        public static void SetTagMode(RealtimeAPIInterface realTimeAPI, SupportedTagModes tagMode)
        {
            Console.WriteLine("ATTEMPTING TO SET TAG MODE");
            if (realTimeAPI.SetTagMode(tagMode))
                Console.WriteLine("SetTagMode ACK Received\n");
        }

        public static void SetMute(RealtimeAPIInterface realTimeAPI, bool muting)
        {
            Console.WriteLine("ATTEMPTING TO SET MUTE STATUS");
            if (realTimeAPI.SetMute(muting))
                Console.WriteLine("SetMute ACK Received\n");
        }
    }

}
