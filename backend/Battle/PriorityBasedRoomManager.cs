namespace backend.Battle;
using shared;

public class PriorityBasedRoomManager : IRoomManager {
    private Mutex mux;
    protected Dictionary<int, Room> deck;
    protected KvPriorityQueue<int, Room> pq;
    protected KvPriorityQueue<int, Room>.ValScore roomScore = (x) => {
        return (int)Math.Ceiling(x.calRoomScore());
    };

    private ILogger<PriorityBasedRoomManager> _logger;
    private ILoggerFactory _loggerFactory;
    public PriorityBasedRoomManager(ILogger<PriorityBasedRoomManager> logger, ILoggerFactory loggerFactory) {
        _logger = logger;
        _loggerFactory = loggerFactory;

        mux = new Mutex();

        int initialCountOfRooms = 8;
        deck = new Dictionary<int, Room>();
        pq = new KvPriorityQueue<int, Room>(initialCountOfRooms, roomScore);
        for (int i = 0; i < initialCountOfRooms; i++) {
            int roomCapacity = 2;
            Room r = new Room(this, _loggerFactory, i+1, roomCapacity);
            Put(r);
        }
    }

    public Room? Peek(int roomId) {
        mux.WaitOne();
        try {
            var r = pq.Peek(roomId);
            if (null != r) return r;
            if (deck.ContainsKey(roomId)) {
                return deck[roomId];
            } else {
                return null;
            }
        } finally {
            mux.ReleaseMutex();
        }
    }

    public Room? Pop() {
        mux.WaitOne();
        try {
            var r = pq.Pop();
            if (null != r) {
                deck.Add(r.id, r);
            }
            return r;
        } finally {
            mux.ReleaseMutex();
        }
    }

    public Room? PopAny(int roomId) {
        mux.WaitOne();
        try {
            var r = pq.PopAny(roomId);
            if (null != r) {
                deck.Add(r.id, r);
            }
            return r;
        } finally {
            mux.ReleaseMutex();
        }
    }

    public bool Put(Room r) {
        mux.WaitOne();
        try {
            if (deck.ContainsKey(r.id)) {
                deck.Remove(r.id); 
            }
            return pq.Put(r.id, r);
        } finally {
            mux.ReleaseMutex();
        }
    }

    ~PriorityBasedRoomManager() {
        mux.Dispose();
    }
}
