using System.Collections.Generic;
using UnityEngine;
using shared;
using System;
using Unity.Mathematics;

public class TrapAnimationController : MonoBehaviour {

    public int score;
    Dictionary<TrapState, AnimationClip> lookUpTable;
    private Animator animator;
    private SpriteRenderer spr;
    private Material material;

    private void lazyInit() {
        if (null != lookUpTable) return;
        lookUpTable = new Dictionary<TrapState, AnimationClip>();
        animator = this.gameObject.GetComponent<Animator>();
        spr = gameObject.GetComponent<SpriteRenderer>();
        foreach (AnimationClip clip in animator.runtimeAnimatorController.animationClips) {
            TrapState trapState;
            Enum.TryParse(clip.name, out trapState);
            lookUpTable[trapState] = clip;
        }
    }

    // Start is called before the first frame update
    void Start() {
        lazyInit();
    }

    public void updateAnim(TrapState newState, Trap currTrap, int frameIdxInAnim, int immediateDirX) {
        lazyInit();

        if (0 != currTrap.SpinSin && 0 != currTrap.SpinCos) {
            // [WARNING] The anchor configured in SpriteSheet must match that of currTrap.Config.SpinAnchor!
            spr.transform.localRotation = Quaternion.AngleAxis(math.atan2(currTrap.SpinSin, currTrap.SpinCos) * Mathf.Rad2Deg, Vector3.forward);
        }

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
