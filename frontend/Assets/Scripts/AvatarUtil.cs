using shared;
using System;
using UnityEngine;
using UnityEngine.UI;

public class AvatarUtil {
    public static bool SetAvatar1(SpriteRenderer spr, CharacterConfig chConfig) {
        string speciesName = chConfig.SpeciesName;
        // Reference https://www.codeandweb.com/texturepacker/tutorials/using-spritesheets-with-unity#how-can-i-access-a-sprite-on-a-sprite-sheet-from-code
        string spriteSheetPath = "Characters/" + speciesName  + "/" + speciesName;
        var sprites = Resources.LoadAll<Sprite>(spriteSheetPath);
        foreach (Sprite sprite in sprites) {
            if ("Avatar_1".Equals(sprite.name)) {
                spr.sprite = sprite;
                return true;
            }
        }
        return false;
    }

    public static bool SetAvatar2(SpriteRenderer spr, CharacterConfig chConfig) {
        string speciesName = chConfig.SpeciesName;
        string spriteSheetPath = "Characters/" + speciesName + "/" + speciesName;
        var sprites = Resources.LoadAll<Sprite>(spriteSheetPath);
        foreach (Sprite sprite in sprites) {
            if ("Avatar_2".Equals(sprite.name)) {
                spr.sprite = sprite;
                return true;
            }
        }
        return false;
    }

    public static bool SetAvatar1(Image img, CharacterConfig chConfig) {
        string speciesName = chConfig.SpeciesName;
        string spriteSheetPath = "Characters/" + speciesName + "/" + speciesName;
        var sprites = Resources.LoadAll<Sprite>(spriteSheetPath);
        foreach (Sprite sprite in sprites) {
            if ("Avatar_1".Equals(sprite.name)) {
                img.sprite = sprite;
                return true;
            }
        }

        return false;
    }

    public static bool SetAvatar2(Image img, CharacterConfig chConfig) {
        string speciesName = chConfig.SpeciesName;
        string spriteSheetPath = "Characters/" + speciesName + "/" + speciesName;
        var sprites = Resources.LoadAll<Sprite>(spriteSheetPath);
        foreach (Sprite sprite in sprites) {
            if ("Avatar_2".Equals(sprite.name)) {
                img.sprite = sprite;
                return true;
            }
        }
        return false;
    }
}
