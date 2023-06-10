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

    public void updateAnim(string newAnimName, int frameIdxInAnim, int dirX, bool spontaneousLooping, RoomDownsyncFrame rdf) {
        var animator = gameObject.GetComponent<Animator>();
        var spr = gameObject.GetComponent<SpriteRenderer>();
        if (0 > dirX) {
            spr.flipX = true;
        } else if (0 < dirX) {
            spr.flipX = false;
        }

        int targetLayer = 0; // We have only 1 layer, i.e. the baseLayer, playing at any time
        int targetClipIdx = 0; // We have only 1 frame anim playing at any time
        var curClip = animator.GetCurrentAnimatorClipInfo(targetLayer)[targetClipIdx].clip;
        if (spontaneousLooping && newAnimName.Equals(curClip.name)) {
          return;
        }

        var targetClip = lookUpTable[newAnimName];
        var fromTime = (frameIdxInAnim / targetClip.frameRate); // TODO: Anyway to avoid using division here?
        animator.Play(newAnimName, targetLayer, fromTime);
    }
}
