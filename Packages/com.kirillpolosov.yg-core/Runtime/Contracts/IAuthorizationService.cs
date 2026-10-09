using System;
using System.Threading.Tasks;

namespace YgCore.Contracts
{
    /// <summary>Авторизация игрока на платформе. Игра работает и с гостем, вход необязателен.</summary>
    public interface IAuthorizationService
    {
        event EventHandler<AuthorizationProfileChangedEventArgs> ProfileChanged;

        bool IsPlatformAvailable { get; }

        /// <summary>Текущий профиль, никогда не null (для гостя это <see cref="AuthorizationProfile.Guest"/>).</summary>
        AuthorizationProfile CurrentProfile { get; }

        /// <summary>Открывает окно входа. Denied: после окна игрок остался гостем (не вошёл).</summary>
        Task<PlatformServiceResult<AuthorizationProfile>> SignInAsync();
    }
}
