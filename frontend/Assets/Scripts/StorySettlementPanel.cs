using TMPro;
using UnityEngine;
using UnityEngine.UI;
using shared;
using DG.Tweening;

public class StorySettlementPanel : SettlementPanel {
    public Image avatar;
    public TMP_Text usedSecondsTmp;
    public TMP_Text usedTicksTmp;

    public void SetTimeUsed(int usedTicks) {
        int secs = usedTicks / Battle.BATTLE_DYNAMICS_FPS;
        int ticksMod = usedTicks - secs*Battle.BATTLE_DYNAMICS_FPS;

        string secsStr = string.Format("{0:d2}", secs);
        string ticksStr = string.Format("{0:d2}", ticksMod);

        usedSecondsTmp.text = secsStr;
        usedTicksTmp.text = ticksStr;
    }
    public override void PlaySettlementAnim(bool success) {
        if (success) {
            failed.gameObject.SetActive(false);
            finished.gameObject.SetActive(true);
            finished.gameObject.transform.localScale = Vector3.one;
            finished.gameObject.transform.DOScale(1f * Vector3.one, 0.7f);
        } else {
            finished.gameObject.SetActive(false);
            failed.gameObject.SetActive(true);
            failed.gameObject.transform.localScale = Vector3.one;
            failed.gameObject.transform.DOScale(1f * Vector3.one, 0.7f);
        }
    }

    public void SetCharacter(CharacterDownsync chd) {
        var chConfig = Battle.characters[chd.SpeciesId];
        AvatarUtil.SetAvatar1(avatar, chConfig);
    }
}
