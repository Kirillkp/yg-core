namespace YgCore.Contracts
{
    /// <summary>Профиль игрока на платформе. Неизменяемый. Для гостя поля идентификации пустые.</summary>
    public sealed class AuthorizationProfile
    {
        public AuthorizationProfile(
            bool isAuthorized,
            string playerId = null,
            string displayName = null,
            string photoUrl = null)
        {
            IsAuthorized = isAuthorized;

            // Пустые строки вместо null, чтобы вызывающему коду не нужны были проверки.
            PlayerId = playerId ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            PhotoUrl = photoUrl ?? string.Empty;
        }

        /// <summary>Вошёл ли игрок в аккаунт платформы.</summary>
        public bool IsAuthorized { get; }

        public string PlayerId { get; }

        public string DisplayName { get; }

        public string PhotoUrl { get; }

        /// <summary>Профиль гостя (игрок не вошёл).</summary>
        public static AuthorizationProfile Guest()
        {
            return new AuthorizationProfile(false);
        }

        /// <summary>Совпадают ли все поля профиля. Нужно, чтобы не рассылать событие о том же профиле.</summary>
        public bool HasSameIdentity(AuthorizationProfile other)
        {
            return other != null
                && IsAuthorized == other.IsAuthorized
                && PlayerId == other.PlayerId
                && DisplayName == other.DisplayName
                && PhotoUrl == other.PhotoUrl;
        }
    }
}
