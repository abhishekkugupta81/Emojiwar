using System;

namespace EmojiWar.Client.Core.Session
{
    [Serializable]
    public sealed class GuestSessionState
    {
        public string UserId = string.Empty;
        public string DisplayName = string.Empty;
        public string AccessToken = string.Empty;
        public string RefreshToken = string.Empty;
        public bool IsAnonymous;
        public long ExpiresAtUnix;

        public bool HasSession =>
            !string.IsNullOrWhiteSpace(UserId) &&
            !string.IsNullOrWhiteSpace(AccessToken);
    }
}
