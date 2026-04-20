using System.Text;
using UnityEngine.Networking;

namespace EmojiWar.Client.Core.Supabase
{
    public sealed class SupabaseFunctionClient
    {
        private readonly SupabaseProjectConfig config;

        public SupabaseFunctionClient(SupabaseProjectConfig config)
        {
            this.config = config;
        }

        public UnityWebRequest BuildJsonRequest(string functionName, string jsonPayload, string accessToken = "")
        {
            var request = new UnityWebRequest(config.BuildFunctionUrl(functionName), UnityWebRequest.kHttpVerbPOST);
            var body = Encoding.UTF8.GetBytes(jsonPayload);
            request.uploadHandler = new UploadHandlerRaw(body);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("apikey", config.AnonKey);
            request.SetRequestHeader("Authorization", $"Bearer {(string.IsNullOrWhiteSpace(accessToken) ? config.AnonKey : accessToken)}");

            return request;
        }
    }
}
