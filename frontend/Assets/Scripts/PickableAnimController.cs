using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PickableAnimController : MonoBehaviour {
    public int score;
    public Dictionary<string, AnimationClip> lookUpTable;
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

    public void updateAnim(string newAnimName) {
        lazyInit();
        var animator = gameObject.GetComponent<Animator>();
        var spr = gameObject.GetComponent<SpriteRenderer>();

        int targetLayer = 0; // We have only 1 layer, i.e. the baseLayer, playing at any time
        int targetClipIdx = 0; // We have only 1 frame anim playing at any time
        var curClip = animator.GetCurrentAnimatorClipInfo(targetLayer)[targetClipIdx].clip;
        bool sameClipName = newAnimName.Equals(curClip.name);
        if (sameClipName) {
            return;
        }

        var targetClip = lookUpTable[newAnimName];
        animator.Play(newAnimName, targetLayer);
    }
}
