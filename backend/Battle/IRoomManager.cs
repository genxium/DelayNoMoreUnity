namespace backend.Battle;
public interface IRoomManager {
    bool Push(int newScore, Room room);
    
    Room? Pop();
    Room? GetRoom(int roomId);
}