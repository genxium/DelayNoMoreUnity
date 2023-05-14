using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using shared;

public class PlayerWaitingPanel : MonoBehaviour {
    private int lastParticipantChangeId = shared.Battle.TERMINATING_RENDER_FRAME_ID; 
    public GameObject playerSlotPrefab;
    public HorizontalLayoutGroup participantSlots;

    // Start is called before the first frame update
    void Start() {

    }

    // Update is called once per frame
    void Update() {

    }

    public void InitPlayerSlots(int roomCapacity) {
        for (int i = 0; i < roomCapacity; i++) {
            Instantiate(playerSlotPrefab, new Vector3(0, 0, 0), Quaternion.identity, participantSlots.transform);
        } 
    }

    public void OnParticipantChange(RoomDownsyncFrame rdf) {
        if (lastParticipantChangeId >= rdf.ParticipantChangeId)
        lastParticipantChangeId = rdf.ParticipantChangeId;
        var playerSlots = participantSlots.GetComponentsInChildren<ParticipantSlot>();
        for (int i = 0; i < playerSlots.Length;i++) {
            playerSlots[i].SetAvatar(rdf.PlayersArr[i]);
            if (null != rdf.PlayersArr[i] && Battle.TERMINATING_PLAYER_ID == rdf.PlayersArr[i].Id) {
                if (0 < (i & 1)) {
                    playerSlots[i].gameObject.transform.localScale = new Vector3(-1.0f, 1.0f); 
                }
            } 
        }
    }

    public void OnBackButtonClicked() {
        // TODO: Actually we should go back to "CharacterSelection"
        SceneManager.LoadScene("LoginScene", LoadSceneMode.Single);
    }
}
