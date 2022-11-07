// Copyright(c) JoeScan Inc. All Rights Reserved.
//
// Licensed under the BSD 3 Clause License. See LICENSE.txt in the project
// root for license information.

using System.Reflection;

[assembly: AssemblyProduct("JoeScan.Pinchot")]
[assembly: AssemblyTitle("JoeScan.Pinchot")]
[assembly: AssemblyCompany("JoeScan Inc.")]
[assembly: AssemblyCopyright("Copyright © JoeScan 2022")]
[assembly: AssemblyTrademark("JoeScan")]

[assembly: AssemblyDescription("Pinchot is the API for interfacing with JS-50 model scan heads. For Pinchot documentation and examples, and for the JCam API for JS-25 model scan heads, please see api.joescan.com.")]
[assembly: AssemblyMetadata("RepositoryUrl", "https://github.com/JoeScan-Inc/pinchot-net-api")]

#if DEBUG
[assembly: AssemblyConfiguration("Debug")]
#else
[assembly: AssemblyConfiguration("Release")]
#endif
