using UnityEngine;
using UnityEngine.UI;
using shared;
using System;

public class SelfBattleHeading : MonoBehaviour {
    private int speciesId = -1;
    public Image avatar;
    public Slider hpBar;

    // Start is called before the first frame update
    void Start() {
        
    }

    // Update is called once per frame
    void Update() {
        
    }

    public void SetCharacter(CharacterDownsync chd) {
        if (chd.SpeciesId != speciesId) {
            speciesId = chd.SpeciesId; 
            string speciesName = shared.Battle.characters[speciesId].SpeciesName;
            string spriteSheetPath = String.Format("Characters/{0}/{0}", speciesName, speciesName);
            var sprites = Resources.LoadAll<Sprite>(spriteSheetPath);
            foreach (Sprite sprite in sprites) {
                if ("Avatar_1".Equals(sprite.name)) {
                    avatar.sprite = sprite;
                    break;
                }
            }
        }        
        
        hpBar.SetValueWithoutNotify((float)chd.Hp/chd.MaxHp); 
    }
}
