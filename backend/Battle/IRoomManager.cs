namespace backend.Battle;
public interface IRoomManager {
    bool Push(float newScore, Room room);
    
    Room? Pop();
    Room? GetRoom(int roomId);
}
