using System.Text;
using UnityEngine.Networking;

namespace EmojiWar.Client.Core.Supabase
{
    public sealed class SupabaseRestClient
    {
        private readonly SupabaseProjectConfig config;

        public SupabaseRestClient(SupabaseProjectConfig config)
        {
            this.config = config;
        }

        public UnityWebRequest BuildSelectRequest(string table, string queryString, string accessToken)
        {
            var request = UnityWebRequest.Get(config.BuildRestUrl($"{table}?{queryString}"));
            ApplyDefaultHeaders(request, accessToken);
            return request;
        }

        public UnityWebRequest BuildInsertRequest(string table, string jsonPayload, string accessToken)
        {
            var request = BuildJsonMutationRequest(config.BuildRestUrl(table), UnityWebRequest.kHttpVerbPOST, jsonPayload, accessToken);
            request.SetRequestHeader("Prefer", "return=representation");
            return request;
        }

        public UnityWebRequest BuildPatchRequest(string table, string filterQuery, string jsonPayload, string accessToken)
        {
            var request = BuildJsonMutationRequest(config.BuildRestUrl($"{table}?{filterQuery}"), "PATCH", jsonPayload, accessToken);
            request.SetRequestHeader("Prefer", "return=representation");
            return request;
        }

        private UnityWebRequest BuildJsonMutationRequest(string url, string method, string jsonPayload, string accessToken)
        {
            var request = new UnityWebRequest(url, method);
            var body = Encoding.UTF8.GetBytes(jsonPayload);
            request.uploadHandler = new UploadHandlerRaw(body);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            ApplyDefaultHeaders(request, accessToken);
            return request;
        }

        private void ApplyDefaultHeaders(UnityWebRequest request, string accessToken)
        {
            request.SetRequestHeader("apikey", config.AnonKey);
            request.SetRequestHeader("Authorization", $"Bearer {accessToken}");
            request.SetRequestHeader("Accept", "application/json");
        }
    }
}
