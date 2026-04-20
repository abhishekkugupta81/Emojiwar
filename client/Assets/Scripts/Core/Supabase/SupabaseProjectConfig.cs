using UnityEngine;

namespace EmojiWar.Client.Core.Supabase
{
    [CreateAssetMenu(fileName = "SupabaseProjectConfig", menuName = "EmojiWar/Core/Supabase Project Config")]
    public sealed class SupabaseProjectConfig : ScriptableObject
    {
        public const string LocalPlaceholderAnonKey = "replace-with-local-anon-key";

        [SerializeField] private string projectUrl = string.Empty;
        [SerializeField] private string anonKey = string.Empty;
        [SerializeField] private string functionsPath = "/functions/v1/";

        public string ProjectUrl => projectUrl;
        public string AnonKey => anonKey;
        public string FunctionsPath => functionsPath;
        public bool IsConfigured =>
            !string.IsNullOrWhiteSpace(projectUrl) &&
            !string.IsNullOrWhiteSpace(anonKey) &&
            anonKey != LocalPlaceholderAnonKey;

        public string BuildFunctionUrl(string functionName)
        {
            return $"{projectUrl.TrimEnd('/')}{functionsPath}{functionName}";
        }

        public string BuildAuthUrl(string relativePath)
        {
            return $"{projectUrl.TrimEnd('/')}/auth/v1/{relativePath.TrimStart('/')}";
        }

        public string BuildRestUrl(string relativePath)
        {
            return $"{projectUrl.TrimEnd('/')}/rest/v1/{relativePath.TrimStart('/')}";
        }
    }
}
