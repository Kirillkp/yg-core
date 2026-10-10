// Заглушки Unity и PluginYG2 только для проверок вне редактора.
// Сигнатуры взяты из того, как к ним обращается код «Обби Потоп» (он компилировался с настоящим PluginYG2).
// В пакет этот файл не входит.
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace UnityEngine
{
    public static class Mathf
    {
        public static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);
        public static bool Approximately(float a, float b) => System.Math.Abs(a - b) < 1e-6f;
        public static int Max(int a, int b) => a > b ? a : b;
    }

    public static class Debug
    {
        public static void LogWarning(object message) { }
    }

    public static class Time
    {
        public static double realtimeSinceStartupAsDouble => Environment.TickCount64 / 1000.0;
    }

    public static class Awaitable
    {
        public static Task NextFrameAsync(CancellationToken token) => Task.Delay(1, token);
    }

    // Упрощённый JsonUtility: хватает для публичных полей простых классов.
    public static class JsonUtility
    {
        private static readonly JsonSerializerOptions Options = new JsonSerializerOptions { IncludeFields = true };
        // Как настоящий Unity: некорректный JSON даёт ArgumentException.
        public static T FromJson<T>(string json)
        {
            try { return JsonSerializer.Deserialize<T>(json, Options); }
            catch (JsonException e) { throw new ArgumentException("Ошибка разбора JSON: " + e.Message); }
        }
        public static string ToJson(object value) => JsonSerializer.Serialize(value, value.GetType(), Options);
    }
}

namespace UnityEngine.Scripting
{
    [AttributeUsage(AttributeTargets.All)]
    public sealed class PreserveAttribute : Attribute { }
}

namespace YG
{
    public partial class SavesYG { public int idSave; }

    public sealed class PlayerData { public bool auth; public string id = ""; public string name = ""; public string photo = ""; }

    public sealed class Purchase
    {
        public string id, title, description, imageURI, price, priceValue, priceCurrencyCode, currencyImageURL;
        public bool consumed;
    }

    namespace Utils.LB
    {
        public sealed class LBPlayerData { public string uniqueID, name; public int score, rank; }
        public sealed class LBData { public List<LBPlayerData> players = new List<LBPlayerData>(); }
    }

    namespace Insides
    {
        public partial class YGSendMessage { }
    }

    // Поведение PluginYG2, которое нужно мостам и сервисам. События объявлены полями, как в настоящем PluginYG2.
    public static class YG2
    {
        public static bool isSDKEnabled;
        public static object iPlatform;
        public static bool isGameplaying;
        public static bool nowAdsShow;
        public static bool isTimerAdvCompleted = true;
        public static PlayerData player = new PlayerData();
        public static SavesYG saves = new SavesYG();
        public static string lang = "ru";
        public static Purchase[] purchases = new Purchase[0];

        public static Action onGetSDKData;
        public static Action<bool> onPauseGame;
        public static Action onOpenRewardedAdv, onCloseRewardedAdv, onErrorRewardedAdv;
        public static Action<string> onRewardAdv;
        public static Action onOpenInterAdv, onErrorInterAdv;
        public static Action<bool> onCloseInterAdvWasShow;
        public static Action<Utils.LB.LBData> onGetLeaderboard;
        public static Action<string> onSwitchLang, onCorrectLang;
        public static Action onGetPayments;

        // Крючки для сценариев проверки: что должен «сделать» PluginYG2 в ответ на вызов.
        public static Action<string> OnRewardedAdvShow;
        public static Action OnInterstitialAdvShow;
        public static Action OnOpenAuthDialog;
        public static Func<Utils.LB.LBData> OnGetLeaderboard;

        // Журнал вызовов для проверок.
        public static int GameReadyCalls, GameplayStartCalls, GameplayStopCalls;
        public static List<(string id, int score)> SubmittedScores = new List<(string, int)>();

        public static void GameReadyAPI() { GameReadyCalls++; }
        public static void GameplayStart() { GameplayStartCalls++; isGameplaying = true; }
        public static void GameplayStop() { GameplayStopCalls++; isGameplaying = false; }
        public static void RewardedAdvShow(string id) { OnRewardedAdvShow?.Invoke(id); }
        public static void InterstitialAdvShow() { OnInterstitialAdvShow?.Invoke(); }
        public static void SetLeaderboard(string id, int score) { SubmittedScores.Add((id, score)); }
        public static void GetLeaderboard(string id, int max, int around, string photo)
        {
            var data = OnGetLeaderboard?.Invoke();
            if (data != null) onGetLeaderboard?.Invoke(data);
        }
        public static void OpenAuthDialog() { OnOpenAuthDialog?.Invoke(); }
        public static void GetLanguage() { }
        public static void SwitchLanguage(string language) { lang = language; onSwitchLang?.Invoke(language); }

        // Только для проверок: сбрасывает значения, но НЕ события (на события подписан мост).
        public static void ResetValues()
        {
            isSDKEnabled = false; iPlatform = null; isGameplaying = false; nowAdsShow = false;
            isTimerAdvCompleted = true; player = new PlayerData(); saves = new SavesYG(); lang = "ru";
            purchases = new Purchase[0];
            OnRewardedAdvShow = null; OnInterstitialAdvShow = null; OnOpenAuthDialog = null; OnGetLeaderboard = null;
            GameReadyCalls = GameplayStartCalls = GameplayStopCalls = 0; SubmittedScores.Clear();
        }
    }
}
