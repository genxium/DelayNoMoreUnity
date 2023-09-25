using System.Collections.Generic;
using UnityEngine;

public class TrapAnimationController : MonoBehaviour {

    public int score;
    Dictionary<string, AnimationClip> lookUpTable;
    private void lazyInit() {
        if (null != lookUpTable) return;
        lookUpTable = new Dictionary<string, AnimationClip>();
        var animator = this.gameObject.GetComponent<Animator>();
        foreach (AnimationClip clip in animator.runtimeAnimatorController.animationClips) {
            lookUpTable[clip.name] = clip;
        }
    }

    // Start is called before the first frame update
    void Start() {
        lazyInit();
    }

    public void updateAnim(string newAnimName, int frameIdxInAnim, int immediateDirX, bool spontaneousLooping) {
        lazyInit();
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

        var targetClip = lookUpTable[newAnimName];
        float normalizedFromTime = (frameIdxInAnim / (targetClip.frameRate * targetClip.length)); // TODO: Anyway to avoid using division here?
        animator.Play(newAnimName, targetLayer, normalizedFromTime);
    }
}
