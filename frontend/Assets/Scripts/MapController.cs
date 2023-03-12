using UnityEngine;
using shared;

public class MapController : MonoBehaviour {
    int roomCapacity = 2;
    GameObject[] playersArr;
    public GameObject characterPrefab;
    
    FrameRingBuffer<InputFrameDownsync> inputBuffer;

    // Start is called before the first frame update
    void Start() {
        playersArr = new GameObject[roomCapacity];
        spawnPlayerNode(0, 1024, -512);
    }

    // Update is called once per frame
    void Update() {

    }

    void spawnPlayerNode(int joinIndex, int vx, int vy) {
        GameObject newPlayerNode = Instantiate(characterPrefab, new Vector3(vx, vy, 0), Quaternion.identity);
        playersArr[joinIndex] = newPlayerNode;
    }

}
