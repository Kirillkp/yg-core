using System.Threading.Tasks;

namespace YgCore.Contracts
{
    /// <summary>Сообщает платформе, что игра загружена и готова к показу игроку.</summary>
    public interface IGameReadinessService
    {
        bool IsPlatformAvailable { get; }

        /// <summary>Отправлена ли уже отметка готовности (повторно отправлять не нужно).</summary>
        bool IsReadySent { get; }

        /// <summary>Отправляет отметку готовности. Вызывать один раз, когда меню или игра реально готовы.</summary>
        Task<PlatformServiceResult> MarkReadyAsync();
    }
}
