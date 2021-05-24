// Copyright(c) JoeScan Inc. All Rights Reserved.
//
// Licensed under the BSD 3 Clause License. See LICENSE.txt in the project
// root for license information.

using Newtonsoft.Json;
using RestSharp;
using System.Collections.Generic;
using System.IO;
using System.Net;

namespace JoeScan.Pinchot
{
    public partial class ScanHead
    {
        internal void PerformRestFileDownload(string endpoint, Stream writer)
        {
            PerformRestFileDownload(endpoint, writer, IPAddress);
        }

        internal static void PerformRestFileDownload(string endpoint, Stream writer, IPAddress ip)
        {
            var client = new RestClient($"http://{ip}:8080/");
            var req = new RestRequest(endpoint)
            {
                // avoid buffering by writing directly to the stream
                ResponseWriter = resStream =>
                {
                    using (resStream)
                    {
                        resStream.CopyTo(writer);
                    }
                }
            };

            client.DownloadData(req);
        }

        private static T PerformRestGetRequest<T>(string endpoint, IPAddress ip)
        {
            var client = new RestClient($"http://{ip}:8080/");
            client.UseJson();
            var req = new RestRequest(endpoint);
            req.AddHeader("Content-Type", "application/json");

            var response = client.Execute(req);
            if (!response.IsSuccessful)
            {
                throw new WebException($"REST GET {endpoint} to {ip} failed: {response.StatusCode} {response.ErrorMessage}");
            }

            return JsonConvert.DeserializeObject<T>(response.Content);
        }

        private static void PerformRestPostRequest(string endpoint, object body, IPAddress ip)
        {
            var client = new RestClient($"http://{ip}:8080/");
            client.UseJson();
            var req = new RestRequest(endpoint)
            {
                Method = Method.POST
            };
            req.AddHeader("Content-Type", "application/json");
            req.AddJsonBody(body);
            var response = client.Execute(req);
            if (!response.IsSuccessful)
            {
                throw new WebException($"REST POST {endpoint} to {ip} failed: {response.StatusCode} {response.ErrorMessage}");
            }
        }

        internal ScanHeadChannelAlignment GetChannelAlignmentData()
        {
            return GetChannelAlignmentData(IPAddress);
        }

        internal static ScanHeadChannelAlignment GetChannelAlignmentData(IPAddress ip)
        {
            return PerformRestGetRequest<ScanHeadChannelAlignment>("tests/channel-alignment", ip);
        }

        internal ScanHeadDefectMapList GetDefectMapList()
        {
            return GetDefectMapList(IPAddress);
        }

        internal static ScanHeadDefectMapList GetDefectMapList(IPAddress ip)
        {
            return PerformRestGetRequest<ScanHeadDefectMapList>("files/defect", ip);
        }

        internal ScanHeadLaserCameraExposureTimes GetExposureTimes()
        {
            return GetExposureTimes(IPAddress);
        }

        internal static ScanHeadLaserCameraExposureTimes GetExposureTimes(IPAddress ip)
        {
            return PerformRestGetRequest<ScanHeadLaserCameraExposureTimes>("config/exposure-times", ip);
        }

        internal ScanHeadMappleList GetMappleList()
        {
            return GetMappleList(IPAddress);
        }

        internal static ScanHeadMappleList GetMappleList(IPAddress ip)
        {
            return PerformRestGetRequest<ScanHeadMappleList>("files/mapples", ip);
        }

        internal ScanHeadPowerSensors GetPowerData()
        {
            return GetPowerData(IPAddress);
        }

        internal static ScanHeadPowerSensors GetPowerData(IPAddress ip)
        {
            return PerformRestGetRequest<ScanHeadPowerSensors>("sensors/power", ip);
        }

        internal ScanHeadTemperatureSensors GetTemperatureData()
        {
            return GetTemperatureData(IPAddress);
        }

        internal static ScanHeadTemperatureSensors GetTemperatureData(IPAddress ip)
        {
            return PerformRestGetRequest<ScanHeadTemperatureSensors>("sensors/temperature", ip);
        }

        internal ScanHeadUuids GetUuids()
        {
            return GetUuids(IPAddress);
        }

        internal static ScanHeadUuids GetUuids(IPAddress ip)
        {
            return PerformRestGetRequest<ScanHeadUuids>("uuids", ip);
        }

        internal ScanHeadMapping GetMapping()
        {
            return GetMapping(IPAddress);
        }

        internal static ScanHeadMapping GetMapping(IPAddress ip)
        {
            return PerformRestGetRequest<ScanHeadMapping>("mapping", ip);
        }

        internal void SetCameraLaserExposureTimes(uint cameraStart, uint cameraEnd, uint laserStart, uint laserEnd)
        {
            SetCameraLaserExposureTimes(cameraStart, cameraEnd, laserStart, laserEnd, IPAddress);
        }

        internal static void SetCameraLaserExposureTimes(uint cameraStart, uint cameraEnd, uint laserStart, uint laserEnd, IPAddress ip)
        {
            PerformRestPostRequest("config/exposure-times", new { cameraStart, cameraEnd, laserStart, laserEnd }, ip);
        }

        internal void LoadDefectMaps(IEnumerable<string> jsonDefectMaps)
        {
            LoadDefectMaps(jsonDefectMaps, IPAddress);
        }

        internal static void LoadDefectMaps(IEnumerable<string> jsonDefectMaps, IPAddress ip)
        {
            foreach (string map in jsonDefectMaps)
            {
                PerformRestPostRequest("files/defect", map, ip);
            }
        }

        internal IEnumerable<string> DownloadMappleFiles(string dstDir)
        {
            return DownloadMappleFiles(dstDir, IPAddress);
        }

        internal static IEnumerable<string> DownloadMappleFiles(string dstDir, IPAddress ip)
        {
            var mapplePaths = GetMappleList(ip).Mapples;
            foreach (string mp in mapplePaths)
            {
                yield return DownloadMappleFile(mp, dstDir, ip);
            }
        }

        internal string DownloadMappleFile(string fileName, string dstDir)
        {
            return DownloadMappleFile(fileName, dstDir, IPAddress);
        }

        internal static string DownloadMappleFile(string fileName, string dstDir, IPAddress ip)
        {
            string dstPath = Path.Combine(dstDir, fileName);
            using (var writer = File.Create(dstPath))
            {
                PerformRestFileDownload($"files/mapples/{fileName}", writer, ip);
            }
            return dstPath;
        }
    }
}
