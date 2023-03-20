using UnityEngine;
using shared;
using static shared.CharacterState;
using System;
using System.Collections.Generic;

public class CharacterAnimController : MonoBehaviour {
    protected static HashSet<CharacterState>  INTERRUPT_WAIVE_SET = new HashSet<CharacterState> { 
        Idle1,
        Walking,
        InAirIdle1NoJump,
        InAirIdle1ByJump,
        BlownUp1,
        LayDown1,
        GetUp1,
        Dashing,
        OnWall
    };

    Dictionary<CharacterState, AnimationClip> lookUpTable;

    // Start is called before the first frame update
    void Start() {
        lookUpTable = new Dictionary<CharacterState, AnimationClip>();
        var animator = this.gameObject.GetComponent<Animator>();
        foreach (AnimationClip clip in animator.runtimeAnimatorController.animationClips) {
            CharacterState chState;
            Enum.TryParse(clip.name, out chState);
            lookUpTable[chState] = clip;
        }
    }

    // Update is called once per frame
    void Update() {

    }

    public void updateCharacterAnim(PlayerDownsync rdfPlayer, PlayerDownsync prevRdfPlayer, bool forceAnimSwitch, CharacterConfig chConfig) {
        // As this function might be called after many frames of a rollback, it's possible that the playing animation was predicted, different from "prevRdfPlayer.CharacterState" but same as "newCharacterState". More granular checks are needed to determine whether we should interrupt the playing animation.  

        var newCharacterState = rdfPlayer.CharacterState;

        var animator = this.gameObject.GetComponent<Animator>();
        // Update directions
        if (0 > rdfPlayer.DirX) {
            this.gameObject.transform.localScale = new Vector3(-1.0f, this.gameObject.transform.localScale.y);
        } else if (0 < rdfPlayer.DirX) {
            this.gameObject.transform.localScale = new Vector3(+1.0f, this.gameObject.transform.localScale.y);
        }
        if (OnWall == newCharacterState || TurnAround == newCharacterState) {
            if (0 < rdfPlayer.OnWallNormX) {
                this.gameObject.transform.localScale = new Vector3(-1.0f, this.gameObject.transform.localScale.y);
            } else {
                this.gameObject.transform.localScale = new Vector3(+1.0f, this.gameObject.transform.localScale.y);
            }
        }

        var newAnimName = newCharacterState.ToString();
        int targetLayer = 0; // We have only 1 layer, i.e. the baseLayer, playing at any time
        int targetClipIdx = 0; // We have only 1 frame anim playing at any time
        var curClip = animator.GetCurrentAnimatorClipInfo(targetLayer)[targetClipIdx].clip;
        var playingAnimName = curClip.name;

        if (playingAnimName.Equals(newAnimName) && INTERRUPT_WAIVE_SET.Contains(newCharacterState)) {
            return;
        }

        if (INTERRUPT_WAIVE_SET.Contains(newCharacterState)) {
            animator.Play(newAnimName, targetLayer);
            return;
        }

        var targetClip = lookUpTable[newCharacterState];
        var frameIdxInAnim = rdfPlayer.FramesInChState;
        if (InAirIdle1ByJump == newCharacterState) {
            frameIdxInAnim = chConfig.InAirIdleFrameIdxTurningPoint + (frameIdxInAnim - chConfig.InAirIdleFrameIdxTurningPoint) % chConfig.InAirIdleFrameIdxTurnedCycle; // TODO: Anyway to avoid using division here?
        }
        var fromTime = (frameIdxInAnim / targetClip.frameRate); // TODO: Anyway to avoid using division here?
        animator.Play(newAnimName, targetLayer, fromTime);
    }
}
