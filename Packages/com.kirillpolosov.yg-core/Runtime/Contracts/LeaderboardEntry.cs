namespace YgCore.Contracts
{
    /// <summary>Одна строка таблицы лидеров. Неизменяемая.</summary>
    public sealed class LeaderboardEntry
    {
        public LeaderboardEntry(string playerId, string displayName, long score, int rank)
        {
            PlayerId = playerId;
            DisplayName = displayName;
            Score = score;
            Rank = rank;
        }

        public string PlayerId { get; }

        public string DisplayName { get; }

        /// <summary>Очки. Тип long, чтобы не упереться в переполнение.</summary>
        public long Score { get; }

        /// <summary>Место в таблице в том виде, как его вернула платформа.</summary>
        public int Rank { get; }
    }
}
