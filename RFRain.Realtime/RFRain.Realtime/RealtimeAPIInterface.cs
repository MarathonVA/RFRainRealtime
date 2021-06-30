using System;
using System.Collections.Generic;
using System.Text;

/*
 * Created by Matt Hay on 5/18/21
 */
namespace RFRain.Realtime
{
    /// <summary>
    /// This enum contains the modes supported by the reader
    /// It's used to check for ACK and to send a SetMode request
    /// </summary>
    public enum SupportedModes
    {
        Discover,
        AutoCheckIn,
        ServerMode,
        ServerModeEnhanced,
        WriteMode
    }

    /// <summary>
    /// This enum contains the target settings supported by the reader
    /// It's used to check for ACK and to send a SetTarget request
    /// </summary>
    public enum SupportedTargetSettings
    {
        Target_A,
        Target_B,
        Target_AB,
        Target_BA
    }

    /// <summary>
    /// This enum contains the read modes supported by the reader
    /// It's used to check for ACK and to send a SetReadMode request
    /// </summary>
    public enum SupportedReadModes
    {
        S0,
        S1,
        S2,
        S3
    }

    /// <summary>
    /// This enum contains the tag modes supported by the reader
    /// It's used to check for ACK and to send a SetTagMode request
    /// </summary>
    public enum SupportedTagModes
    {
        EMBEDDED_TID_MEM,
        EMBEDDED_EPC_TID_MEM,
        EMBEDDED_ALL
    }  

    public interface RealtimeAPIInterface
    {
        /// <summary>
        /// event will fire when a tag is received 
        /// </summary>
        public event EventHandler<TagInfo> TagEvent;

        /// <summary>
        /// This method connects the client to the reader.
        /// It will receive the connection ack and also send a request for and receive the reader status 
        /// The client will remain connected to the reader until Disconnect is called (or an error occurs)
        /// </summary>
        /// <returns>true if a connection was made</returns>
        public bool Connect();

        /// <summary>
        /// This method disconnects the client from the reader
        /// (Closes out the socket connection)
        /// </summary>
        public void Disconnect();

        /// <summary>
        /// This method sends the start message to the reader and gets its response
        /// The start message type starts transmitting and receiving tag information on the ports
        /// </summary>
        /// <returns>"on" indicating that the reader has been started. Otherwise an error is thrown</returns>
        public string StartReader();

        /// <summary>
        /// This method sends the stop message to the reader and gets its response
        /// The stop message type stops transmitting and receiving tag information on the ports
        /// </summary>
        /// <returns>"stop" confirming that the reader is stopped. Otherwise an error is thrown</returns>
        public string StopReader();

        /// <summary>
        /// This method gets the reader mute status 
        /// </summary>
        /// <returns>the mute status</returns>
        public string GetMute();

        /// <summary>
        /// Gets Reader ID
        /// </summary>
        /// <returns>The ID of the reader</returns>
        public string GetID();

        /// <summary>
        /// This method requests the reader name and group name
        /// </summary>
        /// <returns>The reader and group names</returns>
        public string GetIdentity();

        /// <summary>
        /// This method requests the reader mode
        /// </summary>
        /// <returns>The Mode</returns>
        public SupportedModes GetMode();

        /// <summary>
        /// This method requests the readers current region settings
        /// </summary>
        /// <returns>the region</returns>
        public string GetRegion();

        /// <summary>
        /// This method requests the readers current power settings
        /// </summary>
        /// <returns>The Power level in dBm</returns>
        public int GetPower();

        /// <summary>
        /// This method requests the readers current port settings
        /// </summary>
        /// <returns>All 4 subzones</returns>
        public string GetSubzones();

        /// <summary>
        /// This method requests the amount of seconds before a tag is set to miss
        /// </summary>
        /// <returns>the Monitor Value</returns>
        public int GetMonitor();

        /// <summary>
        /// This method requests the readers target type
        /// </summary>
        /// <returns>The Target Setting</returns>
        public SupportedTargetSettings GetTarget();

        /// <summary>
        /// This method requests the readers tag session info
        /// </summary>
        /// <returns>The Read Mode</returns>
        public SupportedReadModes GetReadMode();

        /// <summary>
        /// This method requests the readers tag read mode info
        /// </summary>
        /// <returns>The Tag Mode</returns>
        public SupportedTagModes GetTagMode();

        /// <summary>
        /// This method requests to see if the reader is transmitting and/or receiving tag info or not
        /// </summary>
        /// <returns>Whether the reader is on or stopped</returns>
        public string GetStatus();




        /****************************************************************************
        * SETTERS
        * Note that the reader must be "stopped" before any of these can be called
        ****************************************************************************/


        /// <summary>
        /// Sets the reader name and group name
        /// </summary>
        /// <param name="readerName">reader name to be set</param>
        /// <param name="groupName">group name to be set</param>
        /// 
        /// NOTICE: The reader MUST be stopped before a set can be called 
        /// 
        /// <returns>Returns the ack received from the reader</returns>
        public bool SetIdentity(string readerName, string groupName);

        /// <summary>
        /// Sets the reader's mode
        /// </summary>
        /// <param name="mode">the new mode to be set</param>
        /// 
        /// NOTICE: The reader MUST be stopped before a set can be called 
        /// 
        /// <returns>Returns the ack received from the reader</returns>
        public bool SetMode(SupportedModes mode);

        /// <summary>
        /// Sets the reader power level in dBm
        /// </summary>
        /// <param name="powerLvl">the new pwr lvl to be set</param>
        /// 
        /// NOTICE: The reader MUST be stopped before a set can be called 
        /// 
        /// <returns>Returns the ack received from the reader</returns>
        public bool SetPower(int powerLvl);

        /// <summary>
        /// Sets a reader port name setting
        /// </summary>
        /// <param name="port">the port # we are changing</param>
        /// <param name="portName">the name we are changing it to</param>
        /// 
        /// NOTICE: The reader MUST be stopped before a set can be called 
        /// 
        /// <returns>Returns the ack received from the reader</returns>
        public bool SetSubzone(int port, string portName);

        /// <summary>
        /// Sets the reader miss tag time in seconds
        /// </summary>
        /// <param name="missTime">the miss time to be set</param>
        /// 
        /// NOTICE: The reader MUST be stopped before a set can be called
        /// 
        /// <returns>Returns the ack received from the reader</returns>
        public bool SetMonitor(int missTime);

        /// <summary>
        /// Sets the reader target setting according to the Rain protocol
        /// </summary>
        /// <param name="setting">the new target setting</param>
        /// 
        /// NOTICE: The reader MUST be stopped before a set can be called
        /// 
        /// <returns>Returns the ack received from the reader</returns>
        public bool SetTarget(SupportedTargetSettings setting);

        /// <summary>
        /// Sets the session setting according to the Rain protocol
        /// </summary>
        /// <param name="readMode">the new read mode to be set</param>
        /// 
        /// NOTICE: The reader MUST be stopped before a set can be called
        /// 
        /// <returns>Returns the ack received from the reader</returns>
        public bool SetReadMode(SupportedReadModes readMode);

        /// <summary>
        /// Sets the additional data information to read from each tag
        /// </summary>
        /// <param name="tagMode">the new tag mode to be set</param>
        /// 
        /// NOTICE: The reader MUST be stopped before a set can be called
        /// 
        /// <returns>Returns the ack received from the reader</returns>
        public bool SetTagMode(SupportedTagModes tagMode);

        /// <summary>
        /// This method mutes or unmutes the reader
        /// </summary>
        /// <param name="muting">true (on) to mute, false (off) to unmute</param>
        /// <returns>Returns the ack received from the reader</returns>
        public bool SetMute(bool muting);
    }
}
