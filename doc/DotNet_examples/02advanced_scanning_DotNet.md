# Advanced Scanning .NET/C# Example

This application shows how you can stream profile data from multiple scan heads in a manner that allows for real time processing of the data. Multiple threads are created to break up the work of reading in new profile data and acting upon it. Configuration of the scan heads is basically the same as previous examples, except that all scanners are set to a smaller scan window size to allow for faster streaming of data.
>[!NOTE]  
>To simplify reading the code, this example breaks with convention and defines the system parameters before they are called instead of defining them at the beginning of the function.   
## Building
Solution files for Microsoft Visual Studio are provided in their own directory and can be used for Visual Studio's build system. 

## Execution
The `JoeScan.Pinchot.dll` library file must be placed into the same directory as the built executable or into `C:\Windows\System32` in order for the application to run.  

Application usage is shown below.  
`Usage: ./02AdvancedScanning SERIAL`  

This application takes one command line argument, `SERIAL`, for the numeric
serial numbers of the scan head to connect to.

Example using scan heads 20211 and 20212.  
`02AdvancedScanning 20211 20212`  
## Namespace
Include the `JoeScan.Pinchot` and the following C++ libs: 
```
using JoeScan.Pinchot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;  

namespace _02AdvancedScanning
{
``` 

## Class
Define the class, and then parameters for the scan system, the scan head serial number, a cancellation token, and a counter for the profiles.
```
    class Program
    {
        private static ScanSystem _scanSystem;
        private static readonly List<uint> ScanHeadSerialNumbers = new List<uint>();
        private static readonly CancellationTokenSource StopTakingProfilesTokenSource = new CancellationTokenSource();
        private static readonly CancellationToken StopTakingProfilesToken = StopTakingProfilesTokenSource.Token;

        private static int _profilesCount = 0;
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
### Create a scan system 
The first call to the API creates a scan system software object. This object manages groupings of scan heads, telling them when to start and stop scanning.  
```
            _scanSystem = new ScanSystem();
```
#### Create and configure the scan heads
>[!NOTE]  
>The scan heads do not store the configuration data. The configuration data is sent to the scan head when you start scanning. This allows you to swap a scan head without configuring it first. 

Create a scan head for each serial number passed in on the command line and configure each one with the same parameters. Note that there is nothing stopping users from configuring each scan head independently.

Many of the settings directly related to the operation of the cameras and lasers can be found in the `ScanHeadConfiguration` class. Refer to the API documentation for specific details regarding each field.  
```
            var id = 0U;
            foreach (var serialNumber in ScanHeadSerialNumbers)
            {
                _scanSystem.CreateScanHead(serialNumber, id++);
            }

            var configuration = new ScanHeadConfiguration
            {
                ScanPhaseOffset = 0,
                LaserDetectionThreshold = 120,
                SaturationThreshold = 800,
                SaturatedPercentage = 30
            };
            configuration.SetCameraExposureTime(10000, 47000, 900000);
            configuration.SetLaserOnTime(100, 100, 1000);

            var scanWindow = ScanWindow.CreateScanWindowRectangular(20.0, -20.0, -20.0, 20.0);

            foreach (var scanHead in _scanSystem.ScanHeads)
            {
                scanHead.Configure(configuration);
                scanHead.SetWindow(scanWindow);
                scanHead.SetAlignment(0, 0, 0, ScanHeadOrientation.CableIsUpstream);
            }
```

### Connect to the scan system and copy the configuration to the scan heads
Now that the scan heads are configured, we'll connect to the heads. Errors display if the system fails to connect or if some scan heads fail to connect. 
```
            var scanHeadsThatFailedToConnect = _scanSystem.Connect(TimeSpan.FromSeconds(3));
            foreach (var scanHead in scanHeadsThatFailedToConnect)
            {
                Console.WriteLine($"Failed to connect to scan head {scanHead.SerialNumber}.");
            }

            if (scanHeadsThatFailedToConnect.Count > 0) return;
```
### Check the status of each scan head
Once configured, we can then read the status from the scan head. If each scan head was configured with a different scan window, they'll each have a different maximum scan rate.
```
            foreach (var scanHead in _scanSystem.ScanHeads)
            {
                Console.WriteLine($"{scanHead.ID} has maximum scan rate of {scanHead.Status.MaxScanRate}Hz.");
            }
```
### Start scanning
To begin scanning on all of the scan heads, all we need to do is command the scan system to start scanning. This will cause all of the scan heads associated with it to begin scanning at the specified rate and data format.
```
            const double rate = 900;
            const DataFormat format = DataFormat.XYFullLMFull;
            _scanSystem.StartScanning(rate, format);
```
### Create a thread for each scan head
To improve performance, we'll create a thread for each scan head. This allows the CPU load of reading out profiles to be distributed across all the cores available on the system rather than keeping the heavy lifting in an application within a single process.
```
            var threads = new List<Thread>();
            foreach (var scanHead in _scanSystem.ScanHeads)
            {
                var thread = new Thread(() => Receiver(scanHead));
                thread.Start();
                threads.Add(thread);
            }
```
Put this thread to sleep until the total scan time is done.
```
            Thread.Sleep(1000);
```
### Stop scanning
Calling `ScanSystem.StopScanning()` will return immediately rather than blocking until the scan heads have fully stopped. As a consequence, we will need to add a small delay before the scan heads begin sending new status updates.
```
            _scanSystem.StopScanning();
            Thread.Sleep(2000);
```
### Verify all sent profiles were received
We can verify that we received all of the profiles sent by the scan heads by reading each scan head's status message and summing up the number of profiles that were sent. If everything went well and the CPU load didn't exceed what the system can manage, this value should be equal to the number of profiles we received in this application.
```
            var expectedProfilesCount = _scanSystem.ScanHeads.Sum(scanHead => scanHead.Status.ProfilesSentCount);
            Console.WriteLine(
                $"Number of profiles received: {_profilesCount}, number of profiles expected: {expectedProfilesCount}");

            // Wait for each thread to terminate before disposing resources.
            foreach (var thread in threads)
            {
                thread.Join();
            }
```
###  Free resources
If we end up with an error from the API, we can get some additional diagnostics by looking at the error code.
```
            _scanSystem.Dispose();
        }
```
### Data Analysis Function
This function is a small utility function used to explore profile data. In this case, it will iterate over the valid profile data and find the highest measurement in the Y axis.
```
        private static Point2D FindScanProfileHighestPoint(IEnumerable<Profile> profiles)
        {
            var p = new Point2D();
            foreach (var profile in profiles)
            {
                foreach (var point in profile.RawPoints)
                {
                    if (point.Y > p.Y)
                    {
                        p = point;
                    }
                }
            }

            return p;
        }
```
### Data Receiver Function
This function receives profile data from a given scan head. We start a thread for each scan head to pull out the data as fast as possible.
```
        private static void Receiver(ScanHead scanHead)
        {
            var profiles = new List<Profile>();
            try
            {
                while (scanHead.TryTakeNextProfile(out var profile, TimeSpan.FromMilliseconds(5000), StopTakingProfilesToken))
                {
                    Interlocked.Increment(ref _profilesCount);
                    profiles.Add(profile);

                    if (profiles.Count < 100) continue;
```
For this example, we'll grab some profiles and then act on the data before repeating this process again. Note that for high performance applications, printing to the console while receiving data should be avoided as it can add significant latency. This example only prints to the console to provide some illustrative feedback to the user, indicating that data is actively being worked on in multiple threads.
```
                    var highestPoint = FindScanProfileHighestPoint(profiles);
                    Console.WriteLine(
                        $"Highest point from scan head {scanHead.ID} is X: {highestPoint.X}\tY: {highestPoint.Y}\tBrightness: {highestPoint.Brightness}");
                    profiles = new List<Profile>();
                }
            }
```
#### Operation Cancelled Exception
This exception is thrown by BlockingCollection.TryTake when canceled. You do not need to handle it.
```
            catch (OperationCanceledException)
        }
    }
}
```