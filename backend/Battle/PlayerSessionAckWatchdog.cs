namespace backend.Battle;

using System.Threading;

public class PlayerSessionAckWatchdog {
    private Timer? timer;
    private readonly int interval; // in milliseconds
    private readonly string onTickMsg;
    private readonly ILogger _logger;
    public delegate void OnPlayerDisconnectedCbType(string playerId);
    private int waiveCnt;

    public PlayerSessionAckWatchdog(int aInterval, OnPlayerDisconnectedCbType cb, string playerId, string aOnTickMsg, ILoggerFactory loggerFactory) {
        interval = aInterval;
        onTickMsg = aOnTickMsg;
        _logger = loggerFactory.CreateLogger<PlayerSessionAckWatchdog>();
        timer = new Timer(new TimerCallback((s) => {
            int newWaiveCnt = Interlocked.Decrement(ref waiveCnt);
            if (0 <= newWaiveCnt) {
                return;
            }
            _logger.LogWarning(onTickMsg);
            if (null == s) {
                return;
            }
            var passedInCb = (OnPlayerDisconnectedCbType)s;
            if (null == passedInCb) return;
            passedInCb(playerId);
        }), cb, aInterval, Timeout.Infinite);
        waiveCnt = 0;
    }

    public void Stop() {
        if (null == timer) return;
        try {
            timer.Change(Timeout.Infinite, Timeout.Infinite);
            Interlocked.Exchange(ref waiveCnt, 0);
        } catch (Exception ex) {
            _logger.LogError(ex, "Exception occurred during watchdog stopping.");
        }
    }

    public void ExplicitlyDispose() {
        if (null == timer) return;
        try {
            timer.Dispose();
            timer = null;
        } catch (Exception ex) {
            _logger.LogError(ex, "Timer couldn't be disposed for more than once");
        }
    }

    public void Kick() {
        if (null == timer) return;
        timer.Change(interval, Timeout.Infinite);
        Interlocked.Exchange(ref waiveCnt, 0);
    }

    public void KickWithOneoffInterval(int oneOffInterval) {
        if (null == timer) return;
        timer.Change(oneOffInterval, Timeout.Infinite);
        Interlocked.Exchange(ref waiveCnt, 0);
    }

    public int incWaiveCnt() {
        return Interlocked.Exchange(ref waiveCnt, 1);
    }

    ~PlayerSessionAckWatchdog() {
        ExplicitlyDispose();
    }
}
