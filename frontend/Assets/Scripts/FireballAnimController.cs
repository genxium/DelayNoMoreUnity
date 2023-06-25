using UnityEngine;
using shared;
using System.Collections.Generic;

public class FireballAnimController : MonoBehaviour {

    public int score;
    Dictionary<string, AnimationClip> lookUpTable;

    // Start is called before the first frame update
    void Start() {
        lookUpTable = new Dictionary<string, AnimationClip>();
        var animator = this.gameObject.GetComponent<Animator>();
        foreach (AnimationClip clip in animator.runtimeAnimatorController.animationClips) {
            lookUpTable[clip.name] = clip;
        }
    }

    public void updateAnim(string newAnimName, int frameIdxInAnim, int immediateDirX, bool spontaneousLooping, BulletConfig bulletConfig, RoomDownsyncFrame rdf) {
        var animator = gameObject.GetComponent<Animator>();
        var spr = gameObject.GetComponent<SpriteRenderer>();

        if (0 > immediateDirX) {
            spr.flipX = true;
        } else if (0 < immediateDirX) {
            spr.flipX = false;
        }

        int targetLayer = 0; // We have only 1 layer, i.e. the baseLayer, playing at any time
        int targetClipIdx = 0; // We have only 1 frame anim playing at any time
        var curClip = animator.GetCurrentAnimatorClipInfo(targetLayer)[targetClipIdx].clip;
        bool sameClipName = newAnimName.Equals(curClip.name);
        if (spontaneousLooping && sameClipName) {
          return;
        }

        // Set skew of the fireball; There're floating point division, thus we'd like to minimize the number of times doing it  
        bool firstTimeUpdate = !sameClipName || (0 == frameIdxInAnim);
        if (firstTimeUpdate) {
            // [WARNING] Don't set "flipX" only when "firstTimeUpdate", because the fireball might be mirrored at runtime!
            float skewAngle = Mathf.Atan2(bulletConfig.DirY, bulletConfig.DirX) * Mathf.Rad2Deg;
            gameObject.transform.rotation = Quaternion.AngleAxis(-skewAngle, Vector3.forward);
        }

        var targetClip = lookUpTable[newAnimName];
        var fromTime = (frameIdxInAnim / targetClip.frameRate); // TODO: Anyway to avoid using division here?
        animator.Play(newAnimName, targetLayer, fromTime);
    }
}
