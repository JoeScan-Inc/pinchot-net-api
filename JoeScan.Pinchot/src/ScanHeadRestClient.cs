// Copyright(c) JoeScan Inc. All Rights Reserved.
//
// Licensed under the BSD 3 Clause License. See LICENSE.txt in the project
// root for license information.

using Newtonsoft.Json;
using RestSharp;
using System;
using System.Collections.Generic;
using System.Net;

namespace JoeScan.Pinchot
{
    public partial class ScanHead
    {
        private RestClient restClient;
        private RestClient RestClient
        {
            get
            {
                if (IpAddress == null)
                {
                    throw new InvalidOperationException($"{nameof(RestClient)} is invalid when {nameof(IpAddress)} is null.");
                }

                if (restClient == null)
                {
                    restClient = new RestClient($"http://{IpAddress}:8080/");
                }

                return restClient;
            }
        }

        private T PerformRestGetRequest<T>(string endpoint)
        {
            var req = new RestRequest(endpoint, Method.Get);
            req.AddHeader("Content-Type", "application/json");
            var response = RestClient.ExecuteAsync(req).Result;
            if (!response.IsSuccessful)
            {
                throw new WebException($"REST GET {endpoint} to {IpAddress} failed: {response.StatusCode} {response.ErrorMessage}");
            }
            return JsonConvert.DeserializeObject<T>(response.Content);
        }

        private void PerformRestPostRequest(string endpoint, object body)
        {
            var req = new RestRequest(endpoint, Method.Post);
            req.AddStringBody(body as string, "application/json");
            var response = RestClient.ExecuteAsync(req).Result;
            if (!response.IsSuccessful)
            {
                throw new WebException($"REST POST {endpoint} to {IpAddress} failed: {response.StatusCode} {response.ErrorMessage}");
            }
        }

        private void PerformRestDeleteRequest(string endpoint)
        {
            var req = new RestRequest(endpoint, Method.Delete);
            var response = RestClient.ExecuteAsync(req).Result;
            if (!response.IsSuccessful)
            {
                throw new WebException($"REST DELETE {endpoint} to {IpAddress} failed: {response.StatusCode} {response.ErrorMessage}");
            }
        }

        internal void RestartServer()
        {
            var req = new RestRequest("restart", Method.Get);
            var response = RestClient.ExecuteAsync(req).Result;
            if (!response.IsSuccessful)
            {
                throw new WebException($"Restarting scan head at {IpAddress} failed: {response.StatusCode} {response.ErrorMessage}");
            }
        }

        internal ScanHeadChannelAlignment GetChannelAlignmentData()
        {
            return PerformRestGetRequest<ScanHeadChannelAlignment>("tests/channel-alignment");
        }

        internal ScanHeadDefectMapList GetDefectMapList()
        {
            return PerformRestGetRequest<ScanHeadDefectMapList>("files/defect");
        }

        internal void DeleteDefectMaps()
        {
            const string ep = "files/defect";
            foreach (string map in GetDefectMapList().DefectMaps)
            {
                PerformRestDeleteRequest($"{ep}/{map}");
            }
        }

        internal ScanHeadLaserCameraExposureTimes GetExposureTimes()
        {
            return PerformRestGetRequest<ScanHeadLaserCameraExposureTimes>("config/exposure-times");
        }

        internal ScanHeadMappleList GetMappleList()
        {
            return PerformRestGetRequest<ScanHeadMappleList>("files/mapples");
        }

        internal ScanHeadPowerSensors GetPowerData()
        {
            return PerformRestGetRequest<ScanHeadPowerSensors>("sensors/power");
        }

        internal ScanHeadTemperatureSensors GetTemperatureData()
        {
            return PerformRestGetRequest<ScanHeadTemperatureSensors>("sensors/temperature");
        }

        internal ScanHeadUuids GetUuids()
        {
            return PerformRestGetRequest<ScanHeadUuids>("uuids");
        }

        internal ScanHeadMapping GetMapping()
        {
            return PerformRestGetRequest<ScanHeadMapping>("mapping");
        }

        internal void SetCameraLaserExposureTimes(uint cameraStart, uint cameraEnd, uint laserStart, uint laserEnd)
        {
            PerformRestPostRequest("config/exposure-times", new { cameraStart, cameraEnd, laserStart, laserEnd });
        }

        internal void LoadDefectMaps(IEnumerable<string> jsonDefectMaps)
        {
            foreach (string map in jsonDefectMaps)
            {
                PerformRestPostRequest("files/defect", map);
            }
        }
    }
}
