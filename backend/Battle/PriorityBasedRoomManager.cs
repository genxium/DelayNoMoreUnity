﻿namespace backend.Battle;
public class PriorityBasedRoomManager : IRoomManager {
    private Mutex mux;
    public PriorityQueue<Room, int> pq;
    public Dictionary<int, Room> dict;

    private ILogger<PriorityBasedRoomManager> _logger;
    private ILoggerFactory _loggerFactory;
    public PriorityBasedRoomManager(ILogger<PriorityBasedRoomManager> logger, ILoggerFactory loggerFactory) {
        _logger = logger;
        _loggerFactory = loggerFactory;

        mux = new Mutex();

        int initialCountOfRooms = 32;
        pq = new PriorityQueue<Room, int>(initialCountOfRooms);
        dict = new Dictionary<int, Room>();

        for (int i = 0; i < initialCountOfRooms; i++) {
            int roomCapacity = 2;
            Room r = new Room(_loggerFactory);
            r.Id = i + 1;
            r.Capacity = roomCapacity;            
            r.OnDismissed();
            this.Push(0, r);
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
                dict.Remove(r.Id);
            }
        } finally {
            mux.WaitOne();
        }
        return r;
    }

    public bool Push(int newScore, Room r) {
        mux.WaitOne();
        try {
            pq.Enqueue(r, newScore);
            dict.Add(r.Id, r);
        } finally {
            mux.ReleaseMutex();
        }
        return true;
    }

    ~PriorityBasedRoomManager() {
        mux.Dispose();
    }
}