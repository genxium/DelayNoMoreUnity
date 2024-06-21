using shared;
using System;
using System.Collections.Generic;
using UnityEngine;
using static shared.CharacterState;

public class CharacterAnimController : MonoBehaviour {
    private string MATERIAL_REF_FLASH_INTENSITY = "_FlashIntensity";
    private float DAMAGED_FLASH_INTENSITY = 0.4f;
    
    private Material material;

    public int score;
    public int speciesId = Battle.SPECIES_NONE_CH;

    public Animator lowerPart, upperPart;

    public Vector3 scaleHolder = new Vector3();
    public Vector3 positionHolder = new Vector3();

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
        OnWallIdle1
    };

    Dictionary<CharacterState, AnimationClip> lookUpTable;
    Dictionary<string, AnimationClip> lowerLookUpTable;

    private Animator getMainAnimator() {
        return (null == upperPart ? this.gameObject.GetComponent<Animator>() : upperPart);
    }

    private void lazyInit() {
        if (null != lookUpTable && null != lowerLookUpTable) return;
        var spr = GetComponent<SpriteRenderer>();
        material = spr.material;

        lookUpTable = new Dictionary<CharacterState, AnimationClip>();
        var animator = getMainAnimator();
        foreach (AnimationClip clip in animator.runtimeAnimatorController.animationClips) {
            CharacterState chState;
            Enum.TryParse(clip.name, out chState);
            lookUpTable[chState] = clip;
        }

        lowerLookUpTable = new Dictionary<string, AnimationClip>();
        if (null != lowerPart) {
            foreach (AnimationClip clip in lowerPart.runtimeAnimatorController.animationClips) {
                lowerLookUpTable[clip.name] = clip;
            }
        }
    }
    // Start is called before the first frame update
    void Start() {
        lazyInit();
    }

    public void updateCharacterAnim(CharacterDownsync rdfCharacter, CharacterState newCharacterState, CharacterDownsync prevRdfCharacter, bool forceAnimSwitch, CharacterConfig chConfig) {
        lazyInit();
        // [WARNING] Being frozen might invoke this function with "newCharacterState != rdfCharacter.ChState" 

        // As this function might be called after many frames of a rollback, it's possible that the playing animation was predicted, different from "prevRdfCharacter.CharacterState" but same as "newCharacterState". More granular checks are needed to determine whether we should interrupt the playing animation.  
        speciesId = chConfig.SpeciesId;
        var animator = getMainAnimator();
        // Update directions
        if (0 > rdfCharacter.DirX) {
            scaleHolder.Set(-1.0f, 1.0f, this.gameObject.transform.localScale.z);
            this.gameObject.transform.localScale = scaleHolder;
        } else if (0 < rdfCharacter.DirX) {
            scaleHolder.Set(+1.0f, 1.0f, this.gameObject.transform.localScale.z);
            this.gameObject.transform.localScale = scaleHolder;
        }
        if (OnWallIdle1 == newCharacterState || TurnAround == newCharacterState) {
            if (0 < rdfCharacter.OnWallNormX) {
                scaleHolder.Set(-1.0f, 1.0f, this.gameObject.transform.localScale.z);
                this.gameObject.transform.localScale = scaleHolder;
            } else {
                scaleHolder.Set(+1.0f, 1.0f, this.gameObject.transform.localScale.z);
                this.gameObject.transform.localScale = scaleHolder;
            }
        }

        float flashIntensity = 0;
        if (rdfCharacter.FramesSinceLastDamaged >= (Battle.DEFAULT_FRAMES_TO_SHOW_DAMAGED >> 1)) {
           flashIntensity = DAMAGED_FLASH_INTENSITY*(Battle.DEFAULT_FRAMES_TO_SHOW_DAMAGED-rdfCharacter.FramesSinceLastDamaged)/ (Battle.DEFAULT_FRAMES_TO_SHOW_DAMAGED >> 1); 
        } else {
           flashIntensity = DAMAGED_FLASH_INTENSITY*rdfCharacter.FramesSinceLastDamaged/(Battle.DEFAULT_FRAMES_TO_SHOW_DAMAGED >> 1); 
        }
        material.SetFloat(MATERIAL_REF_FLASH_INTENSITY, flashIntensity);

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
        float normalizedFromTime = (frameIdxInAnim / (targetClip.frameRate * targetClip.length)); // TODO: Anyway to avoid using division here?
        animator.Play(newAnimName, targetLayer, normalizedFromTime);

    }

    /*
     [WARNING] I once considered the use of "multi-layer animation", yet failed to find well documented steps to efficiently edit and preview the layers simultaneously. If budget permits I'd advance the workflow directly into using Skeletal Animation.
     */
    public void updateTwoPartsCharacterAnim(CharacterDownsync rdfCharacter, CharacterState newCharacterState, CharacterDownsync prevRdfCharacter, bool forceAnimSwitch, CharacterConfig chConfig, float effectivelyInfinitelyFar) {
        lazyInit();
        // [WARNING] Being frozen might invoke this function with "newCharacterState != rdfCharacter.ChState"

        // Update directions
        if (0 > rdfCharacter.DirX) {
            scaleHolder.Set(-1.0f, 1.0f, this.gameObject.transform.localScale.z);
            this.gameObject.transform.localScale = scaleHolder;
        } else if (0 < rdfCharacter.DirX) {
            scaleHolder.Set(+1.0f, 1.0f, this.gameObject.transform.localScale.z);
            this.gameObject.transform.localScale = scaleHolder;
        }
        if (OnWallIdle1 == newCharacterState || TurnAround == newCharacterState) {
            if (0 < rdfCharacter.OnWallNormX) {
                scaleHolder.Set(-1.0f, 1.0f, this.gameObject.transform.localScale.z);
                this.gameObject.transform.localScale = scaleHolder;
            } else {
                scaleHolder.Set(+1.0f, 1.0f, this.gameObject.transform.localScale.z);
                this.gameObject.transform.localScale = scaleHolder;
            }
        }

        int targetLayer = 0; // We have only 1 layer, i.e. the baseLayer, playing at any time
        int targetClipIdx = 0; // We have only 1 frame anim playing at any time
        // Hide lower part when necessary
        if (Battle.INVALID_FRAMES_IN_CH_STATE == rdfCharacter.LowerPartFramesInChState) {
            positionHolder.Set(effectivelyInfinitelyFar, effectivelyInfinitelyFar, lowerPart.gameObject.transform.position.z); 
            lowerPart.gameObject.transform.localPosition = positionHolder;
        } else {
            positionHolder.Set(0, 0, lowerPart.gameObject.transform.position.z); 
            lowerPart.gameObject.transform.localPosition = positionHolder;
            var lowerNewAnimName = "WalkingLowerPart"; 
            switch (newCharacterState) {
            case Atk1:
            case Atk4:
                lowerNewAnimName = "StandingLowerPart";
            break;
            default:
            break;
            }
            var lowerFrameIdxInAnim = rdfCharacter.LowerPartFramesInChState;
            var lowerTargetClip = lowerLookUpTable[lowerNewAnimName];
            float lowerNormalizedFromTime = (lowerFrameIdxInAnim / (lowerTargetClip.frameRate * lowerTargetClip.length)); // TODO: Anyway to avoid using division here?
            lowerPart.Play(lowerNewAnimName, targetLayer, lowerNormalizedFromTime);
        }

        var upperNewAnimName = newCharacterState.ToString();
        var curClip = upperPart.GetCurrentAnimatorClipInfo(targetLayer)[targetClipIdx].clip;
        var upperPlayingAnimName = curClip.name;

        if (upperPlayingAnimName.Equals(upperNewAnimName) && INTERRUPT_WAIVE_SET.Contains(newCharacterState)) {
            return;
        }

        if (INTERRUPT_WAIVE_SET.Contains(newCharacterState)) {
            upperPart.Play(upperNewAnimName, targetLayer);
            return;
        }

        var upperTargetClip = lookUpTable[newCharacterState];
        var upperFrameIdxInAnim = rdfCharacter.FramesInChState;
        float upperNormalizedFromTime = (upperFrameIdxInAnim / (upperTargetClip.frameRate * upperTargetClip.length)); // TODO: Anyway to avoid using division here?
        upperPart.Play(upperNewAnimName, targetLayer, upperNormalizedFromTime);
    }

    public void pause(bool toPause) {
        var mainAnimator = getMainAnimator();
        if (toPause) {
            if (null != lowerPart) {
                lowerPart.speed = 0f;
            }
            mainAnimator.speed = 0f;
        } else {
            if (null != lowerPart) {
                lowerPart.speed = 1f;
            }
            mainAnimator.speed = 1f;
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
    - scaling each sprite in the sprite-sheet w.r.t. the chosen pivot requires a knowledge of the pivot-locations in the meta data, and 
    - scaling the vertex positions in "object space" is fine, but it's difficult for me to superpose it before feeding to "vertex shader" -- thus not "one-pass". 

    Seems to me like the only other approaches that satisfy the above criterions are "Blurred Buffer" and "Jump Flood" as described by https://alexanderameye.github.io/notes/rendering-outlines/.
    */
}
