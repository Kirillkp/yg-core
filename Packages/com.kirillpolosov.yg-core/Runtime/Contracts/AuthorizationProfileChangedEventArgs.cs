using System;

namespace YgCore.Contracts
{
    /// <summary>Данные события «профиль игрока изменился» (вход, выход, смена аккаунта).</summary>
    public sealed class AuthorizationProfileChangedEventArgs : EventArgs
    {
        public AuthorizationProfileChangedEventArgs(AuthorizationProfile profile)
        {
            Profile = profile;
        }

        /// <summary>Новый профиль.</summary>
        public AuthorizationProfile Profile { get; }
    }
}
