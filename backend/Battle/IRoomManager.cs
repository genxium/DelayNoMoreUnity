namespace backend.Battle;
public interface IRoomManager {
    Room? Peek(int roomId);
    bool Put(Room room);
    Room? Pop();
    Room? PopAny(int roomId);
}
