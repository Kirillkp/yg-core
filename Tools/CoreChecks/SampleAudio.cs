// Образец подключения модуля Audio: игровой наследник AudioDirector, который знает про события игры.
// Этот же код приведён в Documentation~/modules/audio.md. Он компилируется и проверяется в AudioChecks.cs,
// поэтому образец в документации всегда рабочий.
using System;
using UnityEngine;
using YgCore.Audio;
using YgCore.Core;

// Идентификаторы звуков задаёт игра своим enum. Числа попадают в настройки директора в сцене.
public enum SampleSfx
{
    Click = 0,
    Coin = 1,
    Hit = 2,
    Splash = 3,
    DangerLoop = 100,
    BubblesLoop = 101
}

// События игры, на которые реагирует звук. В настоящей игре это её сервисы (бегун, монеты, вода).
public sealed class SampleGameEvents
{
    public event Action CoinCollected;
    public event Action<int> LivesChanged;
    public event Action<bool> Flooded;

    public bool IsFinalFlood { get; set; }
    public bool IsDanger { get; set; }

    public void RaiseCoin() => CoinCollected?.Invoke();
    public void RaiseLives(int lives) => LivesChanged?.Invoke(lives);
    public void RaiseFlood(bool isFinal) => Flooded?.Invoke(isFinal);
}

public sealed class SampleAudioDirector : AudioDirector
{
    private SampleGameEvents game;
    private int lastLives = 3;
    private float coinPitch = 1f;
    private float lastCoinTime = -10f;

    // Свой Initialize принимает сервисы игры и вызывает базовый.
    public void Initialize(
        IGameplayStateService state,
        IPauseCoordinator pause,
        IGameSettingsService settings,
        SampleGameEvents gameEvents)
    {
        game = gameEvents;
        Initialize(state, pause, settings);
    }

    // Подписка на события игры. Вызывается базовым классом при инициализации.
    protected override void OnInitialized()
    {
        game.CoinCollected += OnCoin;
        game.LivesChanged += OnLives;
        game.Flooded += OnFlooded;
    }

    protected override void OnDestroy()
    {
        if (game != null)
        {
            game.CoinCollected -= OnCoin;
            game.LivesChanged -= OnLives;
            game.Flooded -= OnFlooded;
        }

        base.OnDestroy();
    }

    // При входе в забег сбрасываем счётчик жизней.
    protected override void OnAppStateEntered(AppState state)
    {
        if (state == AppState.Run)
        {
            lastLives = 3;
        }
    }

    // При финальном затоплении пузыри продолжают звучать на экране результата.
    protected override bool KeepLoopOnResult(int loopId)
    {
        return loopId == (int)SampleSfx.BubblesLoop && game.IsFinalFlood;
    }

    // В момент опасности музыка чуть ускоряется, но только во время забега.
    protected override float GetMusicPitch()
    {
        return game != null && game.IsDanger && StateService.CurrentState == AppState.Run ? 1.06f : 1f;
    }

    private void OnCoin()
    {
        // Цепочка монет повышает тон, пауза в 0,9 секунды сбрасывает цепочку.
        coinPitch = Time.unscaledTime - lastCoinTime < 0.9f ? Mathf.Min(coinPitch + 0.06f, 1.5f) : 1f;
        lastCoinTime = Time.unscaledTime;
        Play((int)SampleSfx.Coin, coinPitch);
    }

    private void OnLives(int lives)
    {
        if (lives < lastLives)
        {
            Play((int)SampleSfx.Hit);
            if (lives == 1)
            {
                StartLoop((int)SampleSfx.DangerLoop);
            }
        }

        lastLives = lives;
    }

    private void OnFlooded(bool isFinal)
    {
        if (isFinal)
        {
            Play((int)SampleSfx.Splash);
            StartLoop((int)SampleSfx.BubblesLoop);
            StopLoop((int)SampleSfx.DangerLoop);
        }
    }
}
