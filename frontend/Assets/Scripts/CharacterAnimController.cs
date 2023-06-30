using UnityEngine;
using shared;
using static shared.CharacterState;
using System;
using System.Collections.Generic;
using DG.Tweening;

public class CharacterAnimController : MonoBehaviour {
    public InplaceHpBar hpBar;
    public TeamRibbon teamRibbon;
    private string MATERIAL_REF_THICKNESS = "_Thickness";
    private float DAMAGED_THICKNESS = 1.5f;
    private float DAMAGED_BLINK_SECONDS_HALF = 0.2f;

    protected static HashSet<CharacterState> INTERRUPT_WAIVE_SET = new HashSet<CharacterState> {
        Idle1,
        Walking,
        InAirIdle1NoJump,
        InAirIdle1ByJump,
        InAirIdle1ByWallJump,
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

    public void updateCharacterAnim(CharacterDownsync rdfCharacter, CharacterDownsync prevRdfCharacter, bool forceAnimSwitch, CharacterConfig chConfig) {
        // As this function might be called after many frames of a rollback, it's possible that the playing animation was predicted, different from "prevRdfCharacter.CharacterState" but same as "newCharacterState". More granular checks are needed to determine whether we should interrupt the playing animation.  
        try {
            var newCharacterState = rdfCharacter.CharacterState;

            Animator animator = gameObject.GetComponent<Animator>();
            // Update directions
            if (0 > rdfCharacter.DirX) {
                this.gameObject.transform.localScale = new Vector3(-1.0f, 1.0f);
            } else if (0 < rdfCharacter.DirX) {
                this.gameObject.transform.localScale = new Vector3(+1.0f, 1.0f);
            }
            if (OnWall == newCharacterState || TurnAround == newCharacterState) {
                if (0 < rdfCharacter.OnWallNormX) {
                    this.gameObject.transform.localScale = new Vector3(-1.0f, 1.0f);
                } else {
                    this.gameObject.transform.localScale = new Vector3(+1.0f, 1.0f);
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
            var frameIdxInAnim = rdfCharacter.FramesInChState;
            if (InAirIdle1ByJump == newCharacterState || InAirIdle1ByWallJump == newCharacterState) {
                frameIdxInAnim = chConfig.InAirIdleFrameIdxTurningPoint + (frameIdxInAnim - chConfig.InAirIdleFrameIdxTurningPoint) % chConfig.InAirIdleFrameIdxTurnedCycle; // TODO: Anyway to avoid using division here?
            }
            float normalizedFromTime = (frameIdxInAnim / (targetClip.frameRate * targetClip.length)); // TODO: Anyway to avoid using division here?
            animator.Play(newAnimName, targetLayer, normalizedFromTime);
        } finally {
            if (null != prevRdfCharacter && prevRdfCharacter.Hp > rdfCharacter.Hp) {
                // Some characters, and almost all traps wouldn't have an "attacked state", hence showing their damaged animation by shader.
                var spr = gameObject.GetComponent<SpriteRenderer>();
                var material = spr.material;
                DOTween.Sequence().Append(
                    DOTween.To(() => material.GetFloat(MATERIAL_REF_THICKNESS), x => material.SetFloat(MATERIAL_REF_THICKNESS, x), DAMAGED_THICKNESS, DAMAGED_BLINK_SECONDS_HALF))
                    .Append(DOTween.To(() => material.GetFloat(MATERIAL_REF_THICKNESS), x => material.SetFloat(MATERIAL_REF_THICKNESS, x), 0f, DAMAGED_BLINK_SECONDS_HALF));
            }
        }
    }

    /*
    There're certainly many approaches to outline around a sprite, thus a sprite-sequence-animation, the approach used here is simplest in terms of not being mind tweaking because I'm so new to shaders -- yet not necessarily the best.     

    The "offset in 4-directions" approach satisfies all of my needs below.
    - No additional node needed 
    - One-pass 
    - Works on any type of edge, including sharp corners
    - Exactly 1 pixel per direction 

    In contrast I've also considered "scaling by a factor then color the bigger image and superpose it onto the original". It turns out not easy because 
    - scaling each sprite in the sprite-sheet w.r.t. the chosen pivot requries a knowledge of the pivot-locations in the meta data, and 
    - scaling the vertex positions in "object space" is fine, but it's difficult for me to superpose it before feeding to "vertex shader" -- thus not "one-pass". 

    Seems to me like the only other approaches that satisfy the above criterions are "Blurred Buffer" and "Jump Flood" as described by https://alexanderameye.github.io/notes/rendering-outlines/.
    */
}
