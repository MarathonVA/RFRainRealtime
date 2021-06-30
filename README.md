# RFRain-Realtime-Interface

## Description
The .NET Realtime Communication API consists of a .NET SDK for RFRain Readers and various helper/test classes to test its functionality. 
### The API
The RFRain Realtime API receives tag events and responses from readers either over a TCP or UDP connection and sends commands to the reader to get or set its properties. The API supports async await since most of its commands require a send and an ACK that are not completely synchronous. The API project contains its interface, class, and TagInfo helper method that assists it in reading in tags from a reader.

### Helper Classes
There are 3 different helper classes included that can be run alongside the API. 
1. The Test App
     - The Test App is a simple class that users can create API objects to test the APIs functionality with. A user can use or modify test scenarios for both TCP and UDP connections to test out sending specific commands and receiving responses and tag events from either a test server or an actual reader
     
2. The TCP Test Server
     - The TCP Test Server serves as a dummy reader for users to test out the APIs functionality with over a TCP connection
     
3. The UDP Test Server
     - The UDP Test Server serves as a dummy reader for users to test out the APIs functionality with over a UDP connection


## Getting Started
To get the project files for the API, simply download the .zip folder "RFRainRealtime Release 1.0" and unzip it. You can then open the visual studio project by selecting the .sln file, which will open the solution containing each project. 


## Using the API 
Using the API is simple! First, pull down the code from GitHub and import the RFRain.Realtime project wherever it's needed. Once it is inside of your solution, create a RealtimeAPIInterface object and use its constructor to pass in the desired IP address and port, then specify whether the API will be connecting over a TCP connection or not (true or false). 
<br />&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;Ex: This code creates an API connecting to a specific address and port over a TCP connection <br />&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;`RealtimeAPIInterface realTimeAPI = new RFRainRealtimeAPI("68.131.66.114", 5002, true);`

Wherever the API object is constructed, a tag event method must also be created and added to the event object in the API.
For example, here is a method that simply prints out all of the tag info to the command line when a tag event occurs:
````
public static void API_TagEvent(object sender, TagInfo info)
{
  Console.WriteLine("////////////\n TAG EVENT\n///////////");
  Console.WriteLine(info.ToString());
}
````
Here is the line of code that adds the method to the event object (it is best to put this line immediately after constructing the API):
`realTimeAPI.TagEvent += API_TagEvent;`

Finally, commands can be sent to the connected reader by using any of the API's helper methods. All commands that request information from the reader will automatically mute and unmute the reader during the request. All commands that set properties on the reader will automatically start and stop the reader, along with muting and unmuting it. 

## Using the Helper Classes
1. The **Test App** project can be used as needed, as long as the RFRain.Realtime project is in the same solution as it. It has a main method containing 3 test cases that can be individually used for testing different scenarios. Any of these scenarios can be modified as needed to test whatever functionality is desired. 

2. The **TCP Test Server** project can be opened and run without any extra steps. It will immediately start listening for commands and sending tag events if *status* variable is set to true.

3. The **UDP Test Server** project can be opened and run without any extra steps, just like the TCP Test Server. It will immediately start listening for commands and sending tag events if *status* variable is set to true.


## API Methods
There are numerous get and set commands that can be sent to the reader:
#### Connection Methods
* Connect - Connects the API socket to the reader
* Disconnect - Disconnects the API socket from the reader

#### Get Commands
* GetMute - Gets the reader mute status 
* GetID - Gets the Reader ID
* GetIdentity - Requests the Reader Name and Group Name
* GetMode - Requests the Reader Mode
* GetRegion - Requests the reader's current region settings
* GetPower - Requests the reader's current power settings
* GetSubzones - Requests the reader's currents port settings (its subzones)
* GetMonitor - Requests the amount of seconds before a tag is set to miss (the monitor value)
* GetTarget - Requests the reader's target type
* GetReadMode - Requests the reader's tag session info (its read mode)
* GetTagMode - Requests the reader's tag read mode info (its tag mode)
* GetStatus - Requests to see if the reader is transmitting and/or receiving tag info or not 

#### Set Commands
* StartReader - Starts the reader
* StopReader - Stops the reader
* SetIdentity - Sets the reader name and group name - Params: the new reader name and group name
* SetMode - Sets the reader's mode - Param: one of the 5 supported modes (an enum value defined in RealtimeAPIInterface)
* SetPower - Sets the reader power level (in dBm) - Param: an int representing the new power level
* SetSubzone - Sets a reader port name setting - Params: the port to be modified and its new name 
* SetMonitor - Sets the reader miss tag time (in seconds) - Param: the new miss time 
* SetTarget - Sets the reader target setting according to the Rain protocol - Param: one of the 4 supported targets (an enum value defined in RealtimeAPIInterface)
* SetReadMode - Sets the session setting according to the Rain protocol - Param: one of the 4 supported read modes (an enum value defined in RealtimeAPIInterface)
* SetTagMode - Sets the additional information to read from each tag = Param: one of the 3 supported tag modes (an enum value defined in RealtimeAPIInterface)
* SetMute - Mutes or unmutes the reader - Param: true to mute, false to unmute
