using UnityEngine;
using UnityEngine.UI;
using shared;
using System;

public class SelfBattleHeading : MonoBehaviour {
    private int speciesId = -1;
    public Image avatar;
    public Slider hpBar;
    public Slider mpBar;
    public BuffActiveCountDown buffActiveCountDown;

    public void reset() {
        speciesId = -1;
        avatar.sprite = null;
    }

    public void SetCharacter(CharacterDownsync chd) {
        if (speciesId == chd.SpeciesId) {
            return;
        }
        var newChConfig = Battle.characters[chd.SpeciesId];
        AvatarUtil.SetAvatar1(avatar, newChConfig);

        hpBar.SetValueWithoutNotify((float)chd.Hp/ newChConfig.Hp);
        if (0 >= newChConfig.Mp) {
            mpBar.gameObject.SetActive(false);
            mpBar.SetValueWithoutNotify(0);
        } else {
            mpBar.gameObject.SetActive(true);
            mpBar.SetValueWithoutNotify((float)chd.Mp / newChConfig.Mp);
        }

        if (null != chd.BuffList && 0 < chd.BuffList.Count) {
            // TODO: Handle multiple slots.
            buffActiveCountDown.updateData(chd.BuffList[0]);
        }
    }
}
