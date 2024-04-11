using UnityEngine;
using UnityEngine.UI;
using shared;

public class PlayerWaitingPanel : MonoBehaviour {
    private int lastParticipantChangeId = Battle.TERMINATING_RENDER_FRAME_ID;
    public Image backButton;
    public GameObject playerSlotPrefab;
    public HorizontalLayoutGroup participantSlots;
    private bool inited = false;
    private int capacity;

    // Start is called before the first frame update
    void Start() {

    }

    // Update is called once per frame
    void Update() {

    }

    private void toggleBackButton(bool val) {
        if (val) {
            backButton.transform.localScale = Vector3.one;
        } else {
            backButton.transform.localScale = Vector3.zero;
        }
    }

    public void InitPlayerSlots(int roomCapacity) {
        toggleBackButton(true);
        if (inited) return;
        for (int i = 0; i < roomCapacity; i++) {
            Instantiate(playerSlotPrefab, Vector3.zero, Quaternion.identity, participantSlots.transform);
        }
        capacity = roomCapacity;
        inited = true;
    }

    public void OnParticipantChange(RoomDownsyncFrame rdf) {
        if (lastParticipantChangeId >= rdf.ParticipantChangeId)
        lastParticipantChangeId = rdf.ParticipantChangeId;
        int nonEmptyCnt = 0;
        var playerSlots = participantSlots.GetComponentsInChildren<ParticipantSlot>();
        for (int i = 0; i < playerSlots.Length; i++) {
            playerSlots[i].SetAvatar(rdf.PlayersArr[i]);
            if (null != rdf.PlayersArr[i] && Battle.TERMINATING_PLAYER_ID != rdf.PlayersArr[i].Id && 0 < (i & 1)) {
                playerSlots[i].gameObject.transform.localScale = new Vector3(-1.0f, 1.0f, 1.0f); 
            } else {
                playerSlots[i].gameObject.transform.localScale = new Vector3(1.0f, 1.0f, 1.0f);
            }

            if (null != rdf.PlayersArr[i] && Battle.TERMINATING_PLAYER_ID != rdf.PlayersArr[i].Id) {
                ++nonEmptyCnt;
            }
        }

        if (nonEmptyCnt == capacity) {
            toggleBackButton(false);
        }
    }

    public void OnBackButtonClicked(OnlineMapController map) {
        Debug.Log("PlayerWaitingPanel.OnBackButtonClicked");
        map.onWaitingInterrupted();
    }
}
