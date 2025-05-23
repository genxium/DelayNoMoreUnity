using UnityEngine;
using shared;
using UnityEngine.UI;

public class ParticipantSlot : MonoBehaviour {
    public Image underlyingImg;

    private void toggleUnderlyingImage(bool val) {
        if (val) {
            underlyingImg.transform.localScale = Vector3.one;
        } else {
            underlyingImg.transform.localScale = Vector3.zero;
        }
    }

    public void SetAvatar(CharacterDownsync currCharacter) {
        if (null == currCharacter || Battle.MAGIC_JOIN_INDEX_INVALID == currCharacter.JoinIndex || Battle.MAGIC_JOIN_INDEX_DEFAULT == currCharacter.JoinIndex) {
            toggleUnderlyingImage(false);
            return;
        }
        var chConfig = Battle.characters[currCharacter.SpeciesId];
        if (AvatarUtil.SetAvatar1(underlyingImg, chConfig)) {
            toggleUnderlyingImage(true);
        }
    }
}
