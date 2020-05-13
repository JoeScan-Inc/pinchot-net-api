# .NET/C# Examples

The .NET/C# DevKit contains a Visual Studio Solution file with example projects. The JS-50 API has been streamlined to simplify implementing a scanning system. The three example applications demonstrate how to configure and connect to a single scan head, how to collect data from multiple scan heads, and how to stream data from multiple scan heads for real-time processing. 

We recommend that you follow along the examples in Visual Studio while reading the documentation and experimenting with the provided sample code. Before you begin, you should download and install the JS-50 DevKit for .NET/C# from the Downloads section. 
>[!NOTE]
>The code for the examples has been simplified to show a specific area of interest and does not include proper error checking. As such, the example code is not ready for production purposes and should be seen as an illustration for a specific concept only. It is up to you, the developer, to catch exceptions and handle error situations appropriately. Your application should react gracefully to those conditions and make no assumptions about undocumented behavior.

1. Configure and Connect  
    This example application demonstrates how to configure, connect, and disconnect from a single scan head. Functions and data structures from `JoeScan.Pinchot` are introduced for configuring the scan head. Following successful configuration, the application will connect to the scan head, print out its current status, and then disconnect.  
2. Basic Scanning 
   This application shows the fundamentals of streaming profile data from scan heads up through the client API and into your own code. Each scan head is configured before scanning using generous settings that should guarantee that valid profile data is obtained. Following configuration, a limited number of profiles will be collected before halting the scan and disconnecting from the scan heads.
3. Advanced Scanning  
   This application shows how you can stream profile data from multiple scan heads in a manner that allows for real time processing of the data. Multiple threads are created to break up the work of reading in new profile data and acting upon it. Configuration of the scan heads is basically the same as previous examples, except that all scanners are set to a smaller scan window size to allow for faster streaming of data.

