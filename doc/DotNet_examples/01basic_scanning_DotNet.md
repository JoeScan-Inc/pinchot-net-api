# Basic Scanning .NET/C# Example

This application shows the fundamentals of streaming profile data from scan heads up through the client API and into your own code. Each scan head is configured before scanning using generous settings that should guarantee that valid profile data is obtained. Following configuration, a limited number of profiles will be collected before halting the scan and disconnecting from the scan heads.
>[!NOTE]  
>To simplify reading the code, this example breaks with convention and defines the system parameters before they are called instead of defining them at the beginning of the function. 

## Building
Solution files for Microsoft Visual Studio are provided in their own directory and can be used for Visual Studio's build system. 

## Execution
The `JoeScan.Pinchot.dll` library file must be placed into the same directory as the built executable or into `C:\Windows\System32` in order for the application to run.  

Application usage is shown below.  
`Usage: ./01BasicScanning SERIAL`  

This application takes one command line argument, `SERIAL`, for the numeric
serial numbers of the scan heads.

Example using scan head 20211.
```
01BasicScanning 20211
```
Example using scan heads 20211 and 20212.  
`01BasicScanning 20211 20212`  

## Namespace
Include the `JoeScan.Pinchot` and the following C++ libs: 
```
using JoeScan.Pinchot;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;  

namespace _01BasicScanning
{
```
## Class
Define the namespace and the class, and then parameters for the scan system and the scan head serial number.
```

    class Program
    {
        private static ScanSystem _scanSystem;
        private static uint _scanHeadSerialNumber;
```
## Main
This function creates a scan system software object and a scan head software object. Next, it defines the scan head configuration and connects to the scan system. When the system is connected, it queries the status of the scan heads, collects 1000 profiles from each scan head, and then disconnects. Once the scan system is disconnected, it checks for errors and cleans up the memory allocation.  
```
        private static void Main(string[] args)
        {
```
### Display the API version
Display the API version to console output for visual confirmation of the version being used for this example. 

```
            Console.WriteLine($"{nameof(JoeScan.Pinchot)}: {VersionInformation.Version}");
```
### Serial Numbers
Copy the serial number(s) passed in through the command line.
```
            if (args.Length == 0)
            {
                Console.WriteLine("Must provide a scan head serial number as argument.");
                return;
            }

            foreach (var argument in args)
            {
                if (!uint.TryParse(argument, out var scanHeadSerialNumber))
                {
                    Console.WriteLine($"Argument {argument} cannot be parsed as a uint.");
                    return;
                }

                ScanHeadSerialNumbers.Add(scanHeadSerialNumber);
            }
```
### Create a scan system and the scan heads
The first call to the API creates a scan system software object. This object manages groupings of scan heads, telling them when to start and stop scanning.  
```
            _scanSystem = new ScanSystem();
```
Create a scan head software object for each serial number and associate it with the scan system we just created. We will also assign each scan head a user defined ID starting at zero. We will use the IDs as an index for associating profile data with a given scan head. 
    
```
            var id = 0U;
            foreach (var serialNumber in ScanHeadSerialNumbers)
            {
                _scanSystem.CreateScanHead(serialNumber, id++);
            }
```
#### Configure the scan heads
>[!NOTE]  
>The scan heads do not store the configuration data. The configuration data is sent to the scan head when you start scanning. This allows you to swap a scan head without configuring it first. 

Now that we have successfully created the required software objects needed to interface with the scan heads and the scan system, we can configure the scan heads.  
Many of the settings directly related to the operation of the cameras and lasers can be found in the `ScanHeadConfiguration` class. Refer to the API documentation for specific details regarding each field.  
For this example application, we'll just use the same configuration settings from the "00ConfigureAndConnect" example. The only real difference here is that we will be applying this configuration to multiple scan heads, using a "for" loop to configure each scan head.  
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

            foreach (var scanHead in _scanSystem.ScanHeads)
            {
                scanHead.Configure(configuration);
```
To illustrate that each scan head can be configured independently, we'll alternate between two different scan window sizes for each scan head. We will leave the other options the same only for the sake of convenience; these can be independently configured as needed.
```
                var scanWindow = scanHead.ID % 2 == 0
                    ? ScanWindow.CreateScanWindowRectangular(20.0, -20.0, -20.0, 20.0)
                    : ScanWindow.CreateScanWindowRectangular(30.0, -30.0, -30.0, 30.0);
                scanHead.SetWindow(scanWindow);
                scanHead.SetAlignment(0, 0, 0, ScanHeadOrientation.CableIsUpstream);
            }
```
### Connect to the scan system and copy the configuration to the scan heads
Now that the scan heads are configured, we'll connect to the heads. If any scan heads fail to connect, it will write an error message to the console with the scan head serial number.  
```
            var scanHeadsThatFailedToConnect = _scanSystem.Connect(TimeSpan.FromSeconds(3));
            foreach (var scanHead in scanHeadsThatFailedToConnect)
            {
                Console.WriteLine($"Failed to connect to scan head {scanHead.SerialNumber}.");
            }

            if (scanHeadsThatFailedToConnect.Count > 0) return;
```
### Check scan head status
Once configured, we can then read the status from the scan head. 
>[!NOTE]
>Since the scan heads were configured with two different scan window sizes, they will have different maximum scan rates.
```
            foreach (var scanHead in _scanSystem.ScanHeads)
            {
                Console.WriteLine($"{scanHead.ID} has maximum scan rate of {scanHead.Status.MaxScanRate}Hz.");
            }
```
### Start Scanning
To begin scanning on all of the scan heads, all we need to do is command the scan system to start scanning. This will cause all of the scan heads associated with it to begin scanning at the specified rate and data format.
>[!NOTE]  
>The JS-50 transmits data packets for each point that include the XY position and the luminosity. To improve performance, you can transmit every other point (XY_HALF_LM_HALF) or every fourth point (XY_QUARTER_LM_QUARTER). You can also omit the luminosity data (XY_FULL, XY_HALF, XY_QUARTER). 
```
                const double rate = 400;
            const DataFormat format = DataFormat.XYFullLMFull;
            _scanSystem.StartScanning(rate, format);
```
We'll read out a small number of profiles for each scan head, servicing each one in a round robin fashion until the requested number of profiles have been obtained.
```
            const int maxProfiles = 10;
            var timeout = TimeSpan.FromSeconds(10);
            var stopwatch = Stopwatch.StartNew();
            var profiles = _scanSystem.ScanHeads.ToDictionary(scanHead => scanHead, scanHead => new List<Profile>());

            while (profiles.Values.Any(l => l.Count < maxProfiles))
            {
                foreach (var scanHead in _scanSystem.ScanHeads)
                {
                    scanHead.TryTakeNextProfile(out var profile, TimeSpan.FromMilliseconds(100),
                        new CancellationToken());

                    if (profile is null) continue;

                    profiles[scanHead].Add(profile);
                }

                if (stopwatch.Elapsed > timeout)
                {
                    Console.WriteLine($"Timed-out waiting to collect {maxProfiles} profiles.");
                    return;
                }
            }
```
### Stop Scanning
We've collected all of our data; time to stop scanning. Calling this function will cause each scan head within the entire scan system to stop scanning. Once we're done scanning, we'll process the data.
```
            _scanSystem.StopScanning();
```
Find the highest point scanned by each scan head and display the results.
```
            foreach (var scanHead in _scanSystem.ScanHeads)
            {
                var highestPoint = FindScanProfileHighestPoint(profiles[scanHead]);
                Console.WriteLine($"Highest point from scan head {scanHead.ID} is X: {highestPoint.X}\tY: {highestPoint.Y}\tBrightness: {highestPoint.Brightness}");
            }
```
Clean up the memory allocated by the scan system.  
```
            _scanSystem.Dispose();
```
### Data Analysis
This function is a small utility function used to explore profile data. In this case, it will iterate over the valid profile data and find the highest measurement in the Y axis.
```
private static Point2D FindScanProfileHighestPoint(IEnumerable<Profile> profiles)
        {
            var p = new Point2D();
            foreach (var profile in profiles)
            {
                foreach (var point in profile.GetValidXYPoints())
                {
                    if (point.Y > p.Y)
                    {
                        p = point;
                    }
                }
            }

            return p;
        }
    }
}
```