# JoeScan Pinchot .NET API
The JoeScan Pinchot .NET API is the .NET interface to JoeScan JS-50
scan heads. This API allows users to develop software to run on a desktop
computer and control scan heads that are connected to the same network.
If writing software to control classic JoeScan products such as the JS-20 or
JS-25, please download the supporting software [here](http://help.joescan.com/display/ds/downloads).

## Support
For direct support for the JoeScan Pinchot API, please reach out to your
JoeScan company representative and we will provide assistance as soon as
possible. The [GitHub](https://github.com/JoeScan-Inc/pinchot-net-api) page for this project is also monitored by developers
within JoeScan and can be used to post issues and open pull requests.

## Building Source
The Pinchot API is open sourced on [GitHub](https://github.com/JoeScan-Inc/pinchot-net-api). In order to build the API and software
examples in Windows 10 or 11, the following tools should be installed:

* Visual Studio 2022 with the .NET 6 tools(".NET desktop development" workload in Visual Studio Installer)
* [GitVersion](https://gitversion.net/docs/usage/cli/installation) command line tool
* [PowerShell Core](https://github.com/PowerShell/PowerShell#get-powershell)

Make sure both `dotnet` and `pwsh` are in your `PATH` as some build scripts reference them.

## Links
* [API Portal](http://api.joescan.com)
* [Precompiled Release](http://api.joescan.com/release)
* [GitHub Source](https://github.com/JoeScan-Inc/pinchot-net-api)
* [Homepage](https://joescan.com)
* [NuGet Package](https://www.nuget.org/packages/JoeScan.Pinchot)
* [License](https://github.com/JoeScan-Inc/pinchot-net-api/blob/master/LICENSE.txt)