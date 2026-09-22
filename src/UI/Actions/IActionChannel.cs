using System.Threading.Tasks;

namespace AITool.Actions
{
    /// <summary>
    /// One implementation per thing that can happen when a camera triggers or cancels:
    /// call a URL, publish MQTT, send Telegram/Pushover, play a sound, run a program, ...
    /// ClsTriggerActionQueue runs every registered channel whose ShouldRun() says yes, in order.
    /// </summary>
    public interface IActionChannel
    {
        string Name { get; }

        /// <summary>Whether this channel applies to the queue item (camera setting on, trigger vs cancel, pause state).</summary>
        bool ShouldRun(ClsTriggerActionQueueItem AQI);

        /// <summary>Performs the action. Returns false on error. Does its own logging.</summary>
        Task<bool> RunAsync(ClsTriggerActionQueueItem AQI, string CurSrv);
    }
}
