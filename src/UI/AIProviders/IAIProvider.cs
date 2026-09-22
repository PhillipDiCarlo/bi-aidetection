using System.Threading;
using System.Threading.Tasks;
using static AITool.AITOOL;

namespace AITool.AIProviders
{
    /// <summary>
    /// One implementation per AI backend. Given an image and the server to send it to, returns the
    /// raw predictions. Implementations are responsible for updating the ClsURLItem's error counters
    /// and LastResultMessage the same way the original inline code did.
    /// </summary>
    public interface IAIProvider
    {
        Task<ClsAIServerResponse> DetectAsync(ClsImageQueueItem CurImg, ClsURLItem AiUrl, Camera cam, CancellationToken ct);
    }
}
