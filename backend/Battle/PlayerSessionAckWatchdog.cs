namespace backend.Battle;

using System.Threading;

public class PlayerSessionAckWatchdog {
    private readonly Timer timer;
    private readonly int interval; // in milliseconds
    private readonly string onTickMsg;
    private readonly ILogger _logger;

    public PlayerSessionAckWatchdog(int aInterval, CancellationTokenSource aCancellationTokenSource, string aOnTickMsg, ILoggerFactory loggerFactory) {
        interval = aInterval;
        onTickMsg = aOnTickMsg;
        _logger = loggerFactory.CreateLogger<PlayerSessionAckWatchdog>();
        timer = new Timer(new TimerCallback((s) => {
            _logger.LogWarning(onTickMsg);
            if (null == s) {
                return;
            }
            var cancellationTokenSource = (CancellationTokenSource)s;
            if (null == cancellationTokenSource) return;
            if (cancellationTokenSource.Token.IsCancellationRequested) return;
            cancellationTokenSource.Cancel();
        }), aCancellationTokenSource, aInterval, Timeout.Infinite);
    }

    public void Stop() {
        timer.Change(Timeout.Infinite, Timeout.Infinite);
    }

    public void Kick() {
        timer.Change(interval, Timeout.Infinite);
    }

    ~PlayerSessionAckWatchdog() {
        timer.Dispose();
    }
}
