using System;
using System.Threading.Tasks;
using YgCore.Contracts;

namespace YgCore.Mocks
{
    /// <summary>
    /// Мок авторизации. Профиль можно менять вручную (<see cref="SetGuest"/>, <see cref="SetAuthorized"/>),
    /// а данные «игрока» задаются свойствами <see cref="PlayerId"/>, <see cref="DisplayName"/>, <see cref="PhotoUrl"/>.
    /// </summary>
    public sealed class EditorMockAuthorizationService : EditorMockPlatformService, IAuthorizationService
    {
        private AuthorizationProfile currentProfile;

        public EditorMockAuthorizationService(bool startsAuthorized = false)
        {
            currentProfile = startsAuthorized
                ? CreateAuthorizedProfile()
                : AuthorizationProfile.Guest();
        }

        public event EventHandler<AuthorizationProfileChangedEventArgs> ProfileChanged;

        public AuthorizationProfile CurrentProfile => currentProfile;

        public string PlayerId { get; set; } = "mock-player-001";

        public string DisplayName { get; set; } = "Mock Player";

        public string PhotoUrl { get; set; } = string.Empty;

        public Task<PlatformServiceResult<AuthorizationProfile>> SignInAsync()
        {
            var result = BuildResult(CreateAuthorizedProfile(), "Мок-авторизация выполнена.");
            if (result.IsSuccess)
            {
                SetProfile(result.Value);
            }

            return Task.FromResult(result);
        }

        /// <summary>Переключает профиль в гостя (как будто игрок вышел).</summary>
        public void SetGuest()
        {
            SetProfile(AuthorizationProfile.Guest());
        }

        /// <summary>Переключает профиль в вошедшего игрока.</summary>
        public void SetAuthorized()
        {
            SetProfile(CreateAuthorizedProfile());
        }

        private AuthorizationProfile CreateAuthorizedProfile()
        {
            return new AuthorizationProfile(true, PlayerId, DisplayName, PhotoUrl);
        }

        private void SetProfile(AuthorizationProfile profile)
        {
            // Событие только если профиль действительно изменился.
            if (currentProfile.HasSameIdentity(profile))
            {
                return;
            }

            currentProfile = profile;
            ProfileChanged?.Invoke(this, new AuthorizationProfileChangedEventArgs(currentProfile));
        }
    }
}
