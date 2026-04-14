using Amazon.Runtime.Endpoints;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using static AMDiscordRPC.Globals;

namespace AMDiscordRPC
{
    internal class Cloudflare : Globals.CloudflareTypes
    {
        public static HttpClient CfHttpClient = new HttpClient();

        public static async void SetToken()
        {
            CfHttpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {CfAccountCredentials.api_token}");
            await VerifyToken();
        }

        private static async Task VerifyToken()
        {
            var response = await CfHttpClient.GetAsync($"{endpointV4}/accounts/{CfAccountCredentials.account_id}/tokens/verify");
            Response deserialized = JsonConvert.DeserializeObject<Response>(await response.Content.ReadAsStringAsync());
            if (deserialized.result.status == "active")
                log.Info("Token valid and active");
            else 
                log.Error("Token is not valid.");
        }

        public static async Task<Response> ListBuckets()
        {
            CfHttpClient.DefaultRequestHeaders.Add("cf-r2-jurisdiction", "eu");
            var response = await CfHttpClient.GetAsync($"{endpointV4}/accounts/{CfAccountCredentials.account_id}/r2/buckets");
            Response deserialized = JsonConvert.DeserializeObject<Response>(await response.Content.ReadAsStringAsync());
            if (deserialized.success)
                deserialized.result = JsonConvert.DeserializeObject<List<Bucket>>(deserialized.result.buckets.ToString());
            return deserialized;
        }
    }
}
