# Configure and Connect .NET/C# Example

This example application demonstrates how to configure, connect, and disconnect from a single scan head. Functions and data structures from `JoeScan.Pinchot` will be introduced for configuring the scan head. Following successful configuration, the application will connect to the scan head, print out its current status, and then disconnect.  
>[!NOTE]  
>To simplify reading the code, this example breaks with convention and defines the system parameters before they are called instead of defining them at the beginning of the function.  
## Building
Solution files for Microsoft Visual Studio are provided in their own directory and can be used for Visual Studio's build system. 

## Execution
The `JoeScan.Pinchot.dll` library file must be placed into the same directory as the built executable or into `C:\Windows\System32` in order for the application to run.

Application usage is shown below.
```
Usage: ./00ConfigureAndConnect SERIAL
```

This application takes one command line argument, `SERIAL`, for the numeric
serial number of the scan head.  

Example using scan head 20211.
```
00ConfigureAndConnect 20211
```

## Namespace
Include the `JoeScan.Pinchot`and the following C# lib: 
```
using JoeScan.Pinchot;
using System;

namespace _00ConfigureAndConnect
{
```
## Class
Define the class and the parameters for the scan system and the scan head serial number.
```

    class Program
    {
        private static ScanSystem _scanSystem;
        private static uint _scanHeadSerialNumber;
```
## Main
This function creates a scan system software object and a scan head software object. Next, it defines the scan head configuration and connects to the scan system. When the system is connected, it queries the scan head to get it's status, prints the status, and then disconnects. Once the scan system is disconnected, it checks for errors and cleans up the memory allocation.  
```
        private static void Main(string[] args)
        {
```
### Display the API version
Display the API version on the console output.  
```
            Console.WriteLine($"{nameof(JoeScan.Pinchot)}: {VersionInformation.Version}");
```
### Serial number
Copy the serial number of the scan head from the command line.
```
            if (args.Length == 0)
            {
                Console.WriteLine("Must provide a scan head serial number as argument.");
                return;
            }

            if (!uint.TryParse(args[0], out _scanHeadSerialNumber))
            {
                Console.WriteLine($"Argument {args[0]} cannot be parsed as a uint.");
                return;
            }
```
### Create a scan system and the scan heads
The first call to the API creates a scan system software object. This object manages groupings of scan heads, telling them when to start and stop scanning.  
```
            _scanSystem = new ScanSystem();
```
Create a scan head software object for the specified serial number and associate it with the scan system we just created. We'll also asign it a user defined ID that can be used within the application as an optional identifier. Note that at this point, we haven't connected with the physical scan head yet.
    
```
            var scanHead = _scanSystem.CreateScanHead(_scanHeadSerialNumber, 1);
```
#### Configure the scan head and copy the configuration to the scan head
>[!NOTE]  
>The scan heads do not store the configuration data. The configuration data is sent to the scan head when you start scanning. This allows you to swap a scan head without configuring it first. 

Now that we have successfully created the required software objects needed to interface with the scan head and the scan system it is associated with, we can begin to configure the scan head.  

Many of the settings related to the operation of the cameras and lasers can be found in the `ScanHeadConfiguration` class. Refer to the API documentation for specific details regarding each field. For this example, we'll use large values to ensure we are able to connect and scan.
```
var configuration = new ScanHeadConfiguration
            {
                ScanPhaseOffset = 0,
                LaserDetectionThreshold = 120,
                SaturationThreshold = 800,
                SaturatedPercentage = 30
            };
            configuration.SetCameraExposureTime(10000, 47000, 900000);
            configuration.SetLaserOnTime(100, 100, 1000);
            scanHead.Configure(configuration);
```
Setting the proper scan window size can be crucial to successful scanning as it allows users to limit the region of interest for scanning. For example, you can filter out other sources of light that could cause interference. 
>[!NOTE]
>There is an inverse relationship between the scan window size and the maximum scan rate for a system. Large scan windows require more processing, which reduces the maximum scan rate of a system.  

For this example, we will use the 60" x 60" maximum scan window size. The scan window is a rectangle defined by the four sides (`WindowTop`, `WindowBottom`, `WindowLeft`, `WindowRight`). The origin is centered in the field of view, so the X and Y values can range from -30 to 30.  
 ```
            var scanWindow = ScanWindow.CreateScanWindowRectangular(30.0, -30.0, -30.0, 30.0);
            scanHead.SetWindow(scanWindow);

```
The SetAlignment function corrects alignment issues and transforms the data from the scan head based coordinate system to the mill coordinate system. For this example, we'll assume that the scan head is mounted perfectly such that the laser is pointed directly at the scan target.
>[!NOTE]
>A calibration fixture is used to set the scan head alignment in an actual system.
```
            scanHead.SetAlignment(0, 0, 0, ScanHeadOrientation.CableIsUpstream);
```
### Connect to the scan head
We've now successfully configured the scan head. Now comes the time to connect to the physical scan head and transmit the configuration values.
```
            var scanHeadsThatFailedToConnect = _scanSystem.Connect(TimeSpan.FromSeconds(3));
            if (scanHeadsThatFailedToConnect.Count > 0)
            {
                Console.WriteLine("Failed to connect to scan system.");
                return;
            }
```
### Retrieve the scan head status
Now that we are connected, we can query the scan head to get it's current status. Note that the status is updated periodically by the scan head, and calling this function multiple times will provide the last reported status of the scan head.
```
            PrintScanHeadStatus(scanHead.Status);
 ```
 This is the point where we could command the scan system to start scanning and obtain profile data from the scan heads associated with it. This will be the focus of a later example.
###  Stop scanning and clean up the allocated memory
We've accomplished what we set out to do for this example. Now it's time to bring down our system.

```
            _scanSystem.Disconnect();
```
Clean up the memory allocated by the scan system.  
```
            _scanSystem.Dispose();
```
## Print the Scan Head Status
Prints the `ScanHeadStatus` of the provided `ScanHead` to the console. The status includes the time stamp, the encoder value, the temperature, the maximum scan rate, and the number of profiles sent.
```
        static void PrintScanHeadStatus(ScanHeadStatus status)
        {
            Console.WriteLine("ScanHeadStatus");
            Console.WriteLine($"{nameof(status.GlobalTime)}: {status.GlobalTime}ns");

            foreach (var encoderValue in status.EncoderValues)
            {
                Console.WriteLine($"{encoderValue.Key}: {encoderValue.Value}");
            }

            foreach (var temperature in status.Temperatures)
            {
                Console.WriteLine($"{temperature.Key}: {temperature.Value} Degrees C");
            }

            Console.WriteLine($"{nameof(status.MaxScanRate)}: {status.MaxScanRate}Hz");
            Console.WriteLine($"{nameof(status.ProfilesSentCount)}: {status.ProfilesSentCount}");
        }
    }
}
```
