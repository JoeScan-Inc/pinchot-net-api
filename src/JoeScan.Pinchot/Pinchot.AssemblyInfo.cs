using System.Reflection;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("SensorDefectMapper"),
           InternalsVisibleTo("MapplerHmi"),
           InternalsVisibleTo("JSAlign"),
           InternalsVisibleTo("SensorTester"),
           InternalsVisibleTo("JoeScan.Pinchot.Tests.UnitTests"),
           InternalsVisibleTo("JoeScan.Pinchot.Tests.FunctionalTests"),
           InternalsVisibleTo("JSEnvironmentalTestApplication"),
           InternalsVisibleTo("LignaDemo")]

[assembly: AssemblyCompany("JoeScan Inc.")]
[assembly: AssemblyCopyright("JoeScan 2020")]
[assembly: AssemblyDescription("Pinchot is the API for interfacing with JS-50 model scan heads. For Pinchot documentation and examples, and for the JCam API for JS-25 model scan heads, please see api.joescan.com.\r\n")]
[assembly: AssemblyFileVersion("13.1.7.0")]
[assembly: AssemblyVersion("13.1.7.0")]
[assembly: AssemblyInformationalVersion("13.1.7")]
[assembly: AssemblyProduct("JoeScan.Pinchot")]
[assembly: AssemblyTitle("JoeScan.Pinchot")]
[assembly: AssemblyMetadata("RepositoryUrl", "https://github.com/JoeScan-Inc/pinchot-net-api")]
