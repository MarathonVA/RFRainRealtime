using System;

/*
 * Created by Matt Hay on 5/19/21
 */
namespace RFRain.Realtime
{
    /// <summary>
    /// this class holds all relevent tag information
    /// it can optionally hold EPC and User values based on the tag mode
    /// </summary>
    public class TagInfo
    {
        //all tag info variables that will be received for a tag event 
        public string GroupName { get; private set; }
        public string ReaderName { get; private set; }
        public string ReaderResponseMode { get; private set; } //a.k.a. an alertType
        public string TagNumber { get; private set; }
        public int TagSize { get; private set; }
        public string DetectStat { get; private set; }
        public string Subzone { get; private set; }
        public int RSSI { get; private set; }
        public string UTC { get; private set; }
        public int TagReadCount { get; private set; }

        //optional values - start out as null
        public string TID { get; private set; } = null;
        public string EPC { get; private set; } = null;
        public string User { get; private set; } = null;
        public string DataInfo { get; private set; } = null;

        /// <summary>
        /// TagInfo constructor will parse out the message and get each tag variable
        /// </summary>
        /// <param name="message">the tag message</param>
        public TagInfo(string message)
        {
            //divides the full message into each individual message ack (removes ack as well)
            //string[] splitByAck = message.Split("ack ");
            string[] splitByAck = message.Split("\n");
            //check what each message consists of and parse accordingly
            foreach (string msg in splitByAck)
            {
                if(msg.Contains("taginfo"))
                {
                    parseTagInfo(msg);
                }
                else if(msg.Contains("tid"))
                {
                    parseTid(msg);
                }
                else if(msg.Contains("epc"))
                {
                    parseEPC(msg);
                }
                else if(msg.Contains("user"))
                {
                    parseUser(msg);
                }
                else if(msg.Contains("datainfo"))
                {
                    parseDataInfo(msg);
                }
            }
        }

        /// <summary>
        /// Parses all of the related information from the tag info message
        /// </summary>
        /// <param name="msg">the tag message</param>
        private void parseTagInfo(String msg)
        {
            try
            {
                //splits to an array of no less than 9 elements
                string[] fields = msg.Split(" ");

                //fields[0] will equal: "ack"
                GroupName = fields[1].Substring(fields[1].IndexOf("=") + 1); //group name must cut off the beginning of the ack for this one
                ReaderName = fields[2]; //reader name 
                ReaderResponseMode = fields[3]; //mode
                TagNumber = fields[4]; //tagnumb
                TagSize = int.Parse(fields[5]); //size
                DetectStat = fields[6]; //detectStat
                Subzone = fields[7]; //subzone
                RSSI = int.Parse(fields[8]); //rssi
                UTC = fields[9]; //utc
                TagReadCount = int.Parse(fields[10]); //count
            }
            catch (Exception ex) { return; }     
        }


        /// <summary>
        /// Parses the tid info from its message
        /// </summary>
        /// <param name="msg">the ack containing the tid</param>
        private void parseTid(String msg)
        {
            TID = msg.Substring(msg.IndexOf("=") + 1);
        }

        /// <summary>
        /// Parses the epc info from its message
        /// </summary>
        /// <param name="msg">the ack containing the epc</param>
        private void parseEPC(String msg)
        {
            EPC = msg.Substring(msg.IndexOf("=") + 1);
        }

        /// <summary>
        /// Parses the user info from its message
        /// </summary>
        /// <param name="msg">the ack containing the user</param>
        private void parseUser(String msg)
        {
            User = msg.Substring(msg.IndexOf("=") + 1);
        }

        /// <summary>
        /// Parses the data info from its message
        /// </summary>
        /// <param name="msg">the ack containing the data</param>
        private void parseDataInfo(String msg)
        {
            DataInfo = msg.Substring(msg.IndexOf("=") + 1);
        }

        /// <summary>
        /// Puts together all tag info for a tag event in a string
        /// </summary>
        /// <returns>string containing all tag info</returns>
        public override string ToString()
        {
            string str
                = "Group Name: " + GroupName + "\n"
                + "Reader Name: " + ReaderName + "\n"
                + "Mode/AlertType: " + ReaderResponseMode + "\n"
                + "Tag Num: " + TagNumber + "\n"
                + "Tag Size: " + TagSize + "\n"
                + "Detectstat: " + DetectStat + "\n"
                + "Subzone: " + Subzone + "\n"
                + "RSSI: " + RSSI + "\n"
                + "UTC: " + UTC + "\n"
                + "Count: " + TagReadCount + "\n";

            if(TID != null)
                str += "TID: " + TID + "\n";
            if (EPC != null)
                str += "EPC: " + EPC + "\n";
            if (User != null)
                str += "USER: " + User + "\n";
            if (DataInfo != null)
                str += "Data Info: " + DataInfo + "\n";

            return str;
        }
    }
}
