namespace backend.Battle;

using System.Threading;

public class Watchdog {
    private Timer timer;
    private int interval; // in milliseconds

    public Watchdog(int interval, TimerCallback callback) {
        timer = new Timer(callback);
    }

    public void Stop() {
        timer.Change(Timeout.Infinite, Timeout.Infinite);
    }

    public void Kick() {
        timer.Change(this.interval, Timeout.Infinite);
    }

    ~Watchdog() {
        timer.Dispose();
    }
}
