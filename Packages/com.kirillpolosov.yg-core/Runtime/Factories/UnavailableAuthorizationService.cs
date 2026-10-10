using System;
using System.Threading.Tasks;
using YgCore.Contracts;

namespace YgCore.Factories
{
    /// <summary>Заглушка авторизации без платформы: игрок всегда гость, вход недоступен.</summary>
    public sealed class UnavailableAuthorizationService : IAuthorizationService
    {
        public event EventHandler<AuthorizationProfileChangedEventArgs> ProfileChanged
        {
            add { }
            remove { }
        }

        public bool IsPlatformAvailable => false;

        public AuthorizationProfile CurrentProfile { get; } = AuthorizationProfile.Guest();

        public Task<PlatformServiceResult<AuthorizationProfile>> SignInAsync()
        {
            return Task.FromResult(
                PlatformServiceResult<AuthorizationProfile>.Unavailable(
                    "Авторизация недоступна на этой платформе."));
        }
    }
}
