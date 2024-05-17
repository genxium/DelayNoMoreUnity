using UnityEngine;
using shared;
using System.Collections.Generic;
using Unity.Mathematics;

public class FireballAnimController : MonoBehaviour {

    public int score;
    private static float radToAngle = 180f / math.PI;
    private Vector3 zAxis = new Vector3 (0f, 0f, 1f);
    public Dictionary<string, AnimationClip> lookUpTable;

    // Start is called before the first frame update
    void Start() {
        lookUpTable = new Dictionary<string, AnimationClip>();
        var animator = this.gameObject.GetComponent<Animator>();
        foreach (AnimationClip clip in animator.runtimeAnimatorController.animationClips) {
            lookUpTable[clip.name] = clip;
        }
    }

    public void updateAnim(string newAnimName, int frameIdxInAnim, int immediateDirX, bool spontaneousLooping, BulletConfig bulletConfig, RoomDownsyncFrame rdf, int immediateVelX, int immediateVelY) {
        var animator = gameObject.GetComponent<Animator>();
        var spr = gameObject.GetComponent<SpriteRenderer>();
        
        if (bulletConfig.RotatesAlongVelocity && 0 != immediateVelX) {
            // Rotate before flipping
            spr.transform.localRotation = Quaternion.AngleAxis(math.atan2(immediateVelY, immediateVelX)*radToAngle, zAxis);
            spr.flipX = false;
        } else {
            spr.transform.localRotation = Quaternion.AngleAxis(0, zAxis);
            if (0 > immediateDirX) {
                spr.flipX = true;
            } else if (0 < immediateDirX) {
                spr.flipX = false;
            }
        }

        int targetLayer = 0; // We have only 1 layer, i.e. the baseLayer, playing at any time
        int targetClipIdx = 0; // We have only 1 frame anim playing at any time
        var curClip = animator.GetCurrentAnimatorClipInfo(targetLayer)[targetClipIdx].clip;
        bool sameClipName = newAnimName.Equals(curClip.name);

        // Set skew of the fireball; There're floating point division, thus we'd like to minimize the number of times doing it  
        bool firstTimeUpdate = (!sameClipName) || (0 == frameIdxInAnim);
        if (firstTimeUpdate) {
            // [WARNING] Don't set "flipX" only when "firstTimeUpdate", because the fireball might be mirrored at runtime!
            float skewAngle = Mathf.Atan2(bulletConfig.DirY, bulletConfig.DirX) * Mathf.Rad2Deg;
            gameObject.transform.rotation = Quaternion.AngleAxis(spr.flipX ? -skewAngle : skewAngle, Vector3.forward);
        }

        if (spontaneousLooping && sameClipName) {
          return;
        }

        var targetClip = lookUpTable[newAnimName];
        float normalizedFromTime = (frameIdxInAnim / (targetClip.frameRate * targetClip.length)); // TODO: Anyway to avoid using division here?
        animator.Play(newAnimName, targetLayer, normalizedFromTime);
    }
}
