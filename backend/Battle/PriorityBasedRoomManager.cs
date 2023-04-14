namespace backend.Battle;
public class PriorityBasedRoomManager : IRoomManager {
    private Mutex mux;
    public PriorityQueue<Room, float> pq;
    public Dictionary<int, Room> dict;

    private ILogger<PriorityBasedRoomManager> _logger;
    private ILoggerFactory _loggerFactory;
    public PriorityBasedRoomManager(ILogger<PriorityBasedRoomManager> logger, ILoggerFactory loggerFactory) {
        _logger = logger;
        _loggerFactory = loggerFactory;

        mux = new Mutex();

        int initialCountOfRooms = 32;
        pq = new PriorityQueue<Room, float>(initialCountOfRooms);
        dict = new Dictionary<int, Room>();

        for (int i = 0; i < initialCountOfRooms; i++) {
            int roomCapacity = 2;
            Room r = new Room(_loggerFactory, i+1, roomCapacity);
            Push(0, r);
        }
    }

    public Room? GetRoom(int roomId) {
        Room? r = null;
        dict.TryGetValue(roomId, out r);
        return r;
    }

    public Room? Pop() {
        Room? r = null;
        mux.WaitOne();
        try {
            if (0 < pq.Count) {
                r = pq.Dequeue();
                dict.Remove(r.id);
            }
        } finally {
            mux.ReleaseMutex();
        }
        return r;
    }

    public bool Push(float newScore, Room r) {
        mux.WaitOne();
        try {
            pq.Enqueue(r, newScore);
            dict.Add(r.id, r);
        } finally {
            mux.ReleaseMutex();
        }
        return true;
    }

    ~PriorityBasedRoomManager() {
        mux.Dispose();
    }
}
