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

        private static T PerformRestGetRequest<T>(string endpoint, RestClient client)
        {
            var req = new RestRequest(endpoint, Method.Get);
            req.AddHeader("Content-Type", "application/json");
            var response = client.ExecuteAsync(req).Result;
            if (!response.IsSuccessful)
            {
                throw new WebException($"REST GET {endpoint} to failed: {response.StatusCode} {response.ErrorMessage}");
            }
            return JsonConvert.DeserializeObject<T>(response.Content);
        }

        private void PerformRestPostRequest(string endpoint, object body)
        {
            var req = new RestRequest(endpoint, Method.Post);
            string data = JsonConvert.SerializeObject(body);
            req.AddStringBody(data, "application/json");
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

        internal RestChannelAlignment GetChannelAlignmentData()
        {
            return PerformRestGetRequest<RestChannelAlignment>("tests/channel-alignment");
        }

        internal RestDefectMapList GetDefectMapList()
        {
            return PerformRestGetRequest<RestDefectMapList>("files/defect");
        }

        internal RestDefectMap GetDefectMap(string defectFile)
        {
            return PerformRestGetRequest<RestDefectMap>($"files/defect/{defectFile}");
        }

        internal void DeleteDefectMaps()
        {
            const string ep = "files/defect";
            foreach (string map in GetDefectMapList().DefectMaps)
            {
                PerformRestDeleteRequest($"{ep}/{map}");
            }
        }

        internal RestMappleCorrections GetMappleCorrections()
        {
            try
            {
                return PerformRestGetRequest<RestMappleCorrections>("/files/corrections/correction.json");
            }
            catch
            {
                return null;
            }
        }

        internal RestLaserCameraExposureTimes GetExposureTimes()
        {
            return PerformRestGetRequest<RestLaserCameraExposureTimes>("config/exposure-times");
        }

        internal RestMappleList GetMappleList()
        {
            return PerformRestGetRequest<RestMappleList>("files/mapples");
        }

        internal RestPowerSensors GetPowerData()
        {
            return PerformRestGetRequest<RestPowerSensors>("sensors/power");
        }

        internal RestTemperatureSensors GetTemperatureData()
        {
            return PerformRestGetRequest<RestTemperatureSensors>("sensors/temperature");
        }

        internal static RestTemperatureSensors GetTemperatureData(RestClient client)
        {
            return PerformRestGetRequest<RestTemperatureSensors>("sensors/temperature", client);
        }

        internal RestUuids GetUuids()
        {
            return PerformRestGetRequest<RestUuids>("uuids");
        }

        internal RestMapping GetMapping()
        {
            return PerformRestGetRequest<RestMapping>("mapping");
        }

        internal RestMappleCorrectionShift GetMappleCorrectionShift()
        {
            return PerformRestGetRequest<RestMappleCorrectionShift>("config/mapple-correct-shift-down");
        }

        internal void SetCameraLaserExposureTimes(uint cameraStart, uint cameraEnd, uint laserStart, uint laserEnd)
        {
            PerformRestPostRequest("config/exposure-times", new { cameraStart, cameraEnd, laserStart, laserEnd });
        }

        internal void SetMappleCorrectionShift(bool enabled)
        {
            PerformRestPostRequest("config/mapple-correct-shift-down", new { enabled });
        }

        internal void LoadDefectMaps(IEnumerable<RestDefectMap> jsonDefectMaps)
        {
            foreach (RestDefectMap map in jsonDefectMaps)
            {
                PerformRestPostRequest("files/defect", map);
            }
        }
    }
}
