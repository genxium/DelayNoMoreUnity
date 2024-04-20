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
        var newChConfig = Battle.characters[chd.SpeciesId];
        if (chd.SpeciesId != speciesId) {
            speciesId = chd.SpeciesId; 
            string speciesName = newChConfig.SpeciesName;
            string spriteSheetPath = String.Format("Characters/{0}/{0}", speciesName, speciesName);
            var sprites = Resources.LoadAll<Sprite>(spriteSheetPath);
            if (null == sprites || newChConfig.UseIsolatedAvatar) {
                var sprite = Resources.Load<Sprite>(String.Format("Characters/{0}/Avatar_1", speciesName));
                if (null != sprite) {
                    avatar.sprite = sprite;
                }
            } else {
                foreach (Sprite sprite in sprites) {
                    if ("Avatar_1".Equals(sprite.name)) {
                        avatar.sprite = sprite;
                        break;
                    }
                }
            }
        }
        
        hpBar.SetValueWithoutNotify((float)chd.Hp/ newChConfig.Hp);
        if (0 >= newChConfig.Mp) {
            mpBar.gameObject.SetActive(false);
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
