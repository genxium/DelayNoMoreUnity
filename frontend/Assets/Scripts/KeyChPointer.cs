using shared;
using System;
using UnityEngine;

public class KeyChPointer : MonoBehaviour {
    private uint speciesId = Battle.SPECIES_NONE_CH;
    public TeamRibbon teamRibbon;
    public SpriteRenderer avatar;
    public int score;
    public float FIXED_VIEWPORT_PADDING = 0.1f;
    private Vector3 zAxis = new Vector3(0f, 0f, 1f);

    public void SetProps(CharacterDownsync chd, float wx, float wy, Vector3 posInMainCamViewport, out float xInCamViewPort, out float yInCamViewPort) {
        xInCamViewPort = posInMainCamViewport.x;
        yInCamViewPort = posInMainCamViewport.y;
        if (0 >= posInMainCamViewport.z) {
            return;
        }

        var newChConfig = Battle.characters[chd.SpeciesId];
        if (chd.SpeciesId != speciesId) {
            speciesId = chd.SpeciesId;
            string speciesName = newChConfig.SpeciesName;
            string spriteSheetPath = String.Format("Characters/{0}/{0}", speciesName, speciesName);
            var sprites = Resources.LoadAll<Sprite>(spriteSheetPath);
            foreach (Sprite sprite in sprites) {
                if ("Avatar_2".Equals(sprite.name)) {
                    avatar.sprite = sprite;
                    break;
                }
            }
        }

        teamRibbon.setBulletTeamId(chd.BulletTeamId);

        if (0f > xInCamViewPort) {
            xInCamViewPort = FIXED_VIEWPORT_PADDING;
            if (0f > yInCamViewPort) {
                yInCamViewPort = FIXED_VIEWPORT_PADDING;
            } else if (1f < yInCamViewPort) {
                yInCamViewPort = 1 - FIXED_VIEWPORT_PADDING;
            }
            teamRibbon.gameObject.transform.localRotation = Quaternion.AngleAxis(90, zAxis);
        } else if (1f < xInCamViewPort) {
            xInCamViewPort = 1f - FIXED_VIEWPORT_PADDING;
            if (0f > yInCamViewPort) {
                yInCamViewPort = FIXED_VIEWPORT_PADDING;
            } else if (1f < yInCamViewPort) {
                yInCamViewPort = 1 - FIXED_VIEWPORT_PADDING;
            }
            teamRibbon.gameObject.transform.localRotation = Quaternion.AngleAxis(-90, zAxis);
        } else {
            if (0f > yInCamViewPort) {
                yInCamViewPort = FIXED_VIEWPORT_PADDING;
                teamRibbon.gameObject.transform.localRotation = Quaternion.AngleAxis(180, zAxis);
            } else if (1f < yInCamViewPort) {
                yInCamViewPort = 1 - FIXED_VIEWPORT_PADDING;
                teamRibbon.gameObject.transform.localRotation = Quaternion.AngleAxis(0, zAxis);
            }
        }
    }
}
