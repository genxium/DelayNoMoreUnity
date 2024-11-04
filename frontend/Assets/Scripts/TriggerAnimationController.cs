using System.Collections.Generic;
using UnityEngine;
using shared;
using System;
using Unity.Mathematics;

public class TriggerAnimationController : MonoBehaviour {

    public int score;
    Dictionary<TriggerState, AnimationClip> lookUpTable;
    private Animator animator;
    private SpriteRenderer spr;
    private Material material;

    private void lazyInit() {
        if (null != lookUpTable) return;
        lookUpTable = new Dictionary<TriggerState, AnimationClip>();
        animator = this.gameObject.GetComponent<Animator>();
        spr = gameObject.GetComponent<SpriteRenderer>();
        foreach (AnimationClip clip in animator.runtimeAnimatorController.animationClips) {
            TriggerState triggerState;
            Enum.TryParse(clip.name, out triggerState);
            lookUpTable[triggerState] = clip;
        }
    }

    // Start is called before the first frame update
    void Start() {
        lazyInit();
    }

    public void updateAnim(TriggerState newState, Trigger currTrigger, int frameIdxInAnim, int immediateDirX) {
        lazyInit();

        if (0 > immediateDirX) {
            spr.flipX = true;
        } else if (0 < immediateDirX) {
            spr.flipX = false;  
        }

        int targetLayer = 0; // We have only 1 layer, i.e. the baseLayer, playing at any time
        int targetClipIdx = 0; // We have only 1 frame anim playing at any time
        var curClip = animator.GetCurrentAnimatorClipInfo(targetLayer)[targetClipIdx].clip;
        var targetClip = lookUpTable[newState];
        float normalizedFromTime = (frameIdxInAnim / (targetClip.frameRate * targetClip.length)); // TODO: Anyway to avoid using division here?
        animator.Play(targetClip.name, targetLayer, normalizedFromTime);
    }
}
