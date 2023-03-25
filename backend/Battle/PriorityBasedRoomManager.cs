namespace backend.Battle;
public class PriorityBasedRoomManager {
    private Mutex mux = new Mutex();
    public PriorityQueue<Room, int> pq = new PriorityQueue<Room, int>();
    public Dictionary<int, Room> dict = new Dictionary<int, Room>();

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
            mux.WaitOne();
        }
        return true;
    }
    ~PriorityBasedRoomManager() {
        mux.Dispose();
    }
}
