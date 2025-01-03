using shared;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using UnityEngine;
using static shared.CharacterState;

public class CharacterAnimController : MonoBehaviour {
    private string MATERIAL_REF_FLASH_INTENSITY = "_FlashIntensity";
    private float DAMAGED_FLASH_INTENSITY = 0.4f;
    private static int DAMAGED_FLASH_SCALE_FRAMES = 2;
    private static int DEFAULT_FRAMES_TO_FLASH_DAMAGED = (Battle.DEFAULT_FRAMES_TO_SHOW_DAMAGED >> DAMAGED_FLASH_SCALE_FRAMES);
    private static int DEFAULT_FRAMES_TO_SHOW_DAMAGED_REDUCTION = (Battle.DEFAULT_FRAMES_TO_SHOW_DAMAGED - DEFAULT_FRAMES_TO_FLASH_DAMAGED);

    public SpriteRenderer spr;
    private Material material;

    public int score;
    private uint speciesId = Battle.SPECIES_NONE_CH;
    public uint GetSpeciesId() {
        return speciesId;
    }

    public void SetSpeciesId(uint val) {
        speciesId = val;
    }

    public Vector3 scaleHolder = new Vector3();
    public Vector3 positionHolder = new Vector3();

    private bool hasIdle1Charging = false;
    private bool hasWalkingCharging = false;
    private bool hasOnWallCharging = false;
    private bool hasInAirIdleCharging = false;
    private bool hasCrouchCharging = false;

    public static ImmutableDictionary<uint, Color> ELE_DAMAGED_COLOR = ImmutableDictionary.Create<uint, Color>()
        .Add(Battle.ELE_NONE, Color.white)
        .Add(Battle.ELE_FIRE, new Color(191 / 255f, 5 / 255f, 0 / 255f))
        .Add(Battle.ELE_THUNDER, new Color(191 / 255f, 171 / 255f, 43 / 255f))
        ;

    protected static HashSet<CharacterState> INTERRUPT_WAIVE_SET = new HashSet<CharacterState> {
        Idle1,
        Walking,
        InAirWalking,
        InAirIdle1NoJump,
        InAirIdle1ByJump,
        InAirIdle1ByWallJump,
        BlownUp1,
        LayDown1,
        GetUp1,
        OnWallIdle1
    };

    Dictionary<CharacterState, AnimationClip> lookUpTable;
    public void resetLookupTable() {
        if (null != lookUpTable) {
            lookUpTable.Clear();
        }
    }

    private Animator getMainAnimator() {
        return gameObject.GetComponent<Animator>();
    }

    private void lazyInit() {
        if (null != lookUpTable && 0 < lookUpTable.Count) return;
        spr = GetComponent<SpriteRenderer>();
        material = spr.material;

        lookUpTable = new Dictionary<CharacterState, AnimationClip>();
        var animator = getMainAnimator();
        foreach (AnimationClip clip in animator.runtimeAnimatorController.animationClips) {
            CharacterState chState;
            Enum.TryParse(clip.name, out chState);
            switch (chState) {
            case Idle1Charging:
                hasIdle1Charging = true;
            break;
            case WalkingAtk1Charging:
                hasWalkingCharging = true;
            break;
            case OnWallAtk1Charging:
                hasOnWallCharging = true;
            break;
            case InAirAtk1Charging:
                hasInAirIdleCharging = true;
            break;
            case CrouchAtk1Charging:
                hasCrouchCharging = true;
            break;
            default:
            break;
            } 
            lookUpTable[chState] = clip;
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
        if (OnWallIdle1 == newCharacterState || OnWallAtk1 == newCharacterState || TurnAround == newCharacterState) {
            if (0 < rdfCharacter.OnWallNormX) {
                scaleHolder.Set(-1.0f, 1.0f, this.gameObject.transform.localScale.z);
                this.gameObject.transform.localScale = scaleHolder;
            } else {
                scaleHolder.Set(+1.0f, 1.0f, this.gameObject.transform.localScale.z);
                this.gameObject.transform.localScale = scaleHolder;
            }
        }

        float flashIntensity = 0;
        var remainingFramesToFlash = (rdfCharacter.FramesSinceLastDamaged > DEFAULT_FRAMES_TO_SHOW_DAMAGED_REDUCTION) ? (rdfCharacter.FramesSinceLastDamaged - DEFAULT_FRAMES_TO_SHOW_DAMAGED_REDUCTION) : 0; // Such that the rate of remaining frame reduction is the same as BATTLE_DYNAMICS_FPS
        var frameIdxToPlayDef1Atked = (Battle.DEFAULT_FRAMES_TO_SHOW_DAMAGED - rdfCharacter.FramesSinceLastDamaged);
        if (0 > frameIdxToPlayDef1Atked) frameIdxToPlayDef1Atked = 0;
        var midway = (DEFAULT_FRAMES_TO_FLASH_DAMAGED >> 1);
        if (null != prevRdfCharacter && prevRdfCharacter.Hp <= rdfCharacter.Hp) {
            flashIntensity = 0;
        } if (remainingFramesToFlash >= midway) {
           flashIntensity = DAMAGED_FLASH_INTENSITY*(DEFAULT_FRAMES_TO_FLASH_DAMAGED - remainingFramesToFlash) / midway; 
        } else {
           flashIntensity = DAMAGED_FLASH_INTENSITY*remainingFramesToFlash / midway;
        }
        material.SetFloat(MATERIAL_REF_FLASH_INTENSITY, flashIntensity);
        material.SetInt("_EleDamageFlash", 0);
        material.SetColor("_FlashColor", ELE_DAMAGED_COLOR[Battle.ELE_NONE]);
        if (0 < flashIntensity) {
            if (Battle.ELE_FIRE == rdfCharacter.DamageElementalAttrs) {
                material.SetInt("_EleDamageFlash", 1);
                material.SetColor("_FlashColor", ELE_DAMAGED_COLOR[Battle.ELE_FIRE]);
            } else if (Battle.ELE_THUNDER == rdfCharacter.DamageElementalAttrs) {
                material.SetInt("_EleDamageFlash", 1);
                material.SetColor("_FlashColor", ELE_DAMAGED_COLOR[Battle.ELE_THUNDER]);
            }
        }
        var effNewChState = newCharacterState;
        if (chConfig.HasBtnBCharging && Battle.BTN_B_HOLDING_RDF_CNT_THRESHOLD_2 <= rdfCharacter.BtnBHoldingRdfCount) {
            switch (newCharacterState) {
            case Idle1:
            if (hasIdle1Charging) {
                effNewChState = Idle1Charging;
            }
            break;
            case Walking:
            if (hasWalkingCharging) {
                effNewChState = WalkingAtk1Charging;
            }
            break;
            case InAirIdle1NoJump:
            case InAirIdle1ByJump:
            case InAirIdle1ByWallJump:
            if (hasInAirIdleCharging) {
                effNewChState = InAirAtk1Charging;
            }
            break;
            case OnWallIdle1:
            if (hasOnWallCharging) {
                effNewChState = OnWallAtk1Charging;
            }
            break;
            case CrouchIdle1:
            if (hasCrouchCharging) {
                effNewChState = CrouchAtk1Charging;
            }
            break;
            default:
            break;
            }
        }
        int targetLayer = 0; // We have only 1 layer, i.e. the baseLayer, playing at any time
        int targetClipIdx = 0; // We have only 1 frame anim playing at any time
        var curClip = animator.GetCurrentAnimatorClipInfo(targetLayer)[targetClipIdx].clip;
        var playingAnimName = curClip.name;
        if (!lookUpTable.ContainsKey(effNewChState)) {
            throw new Exception(chConfig.SpeciesName + " does not have effNewChState = " + effNewChState);
        }
        var targetClip = lookUpTable[effNewChState];
        if (null == chConfig.LoopingChStates || !chConfig.LoopingChStates.ContainsKey(effNewChState)) {
            if (playingAnimName.Equals(targetClip.name) && INTERRUPT_WAIVE_SET.Contains(effNewChState)) {
                return;
            }

            if (INTERRUPT_WAIVE_SET.Contains(newCharacterState)) {
                animator.Play(targetClip.name, targetLayer);
                return;
            }

            var frameIdxInAnim = (Def1Atked1 == newCharacterState ? frameIdxToPlayDef1Atked : rdfCharacter.FramesInChState);
            float normalizedFromTime = (frameIdxInAnim / (targetClip.frameRate * targetClip.length)); // TODO: Anyway to avoid using division here?
            animator.Play(targetClip.name, targetLayer, normalizedFromTime);
        } else {
            var totRdfCnt = (targetClip.frameRate * targetClip.length);
            int animLoopingRdfOffset = chConfig.LoopingChStates[effNewChState];
            var frameIdxInAnim = rdfCharacter.FramesInChState;
            if (frameIdxInAnim > animLoopingRdfOffset) {    
                var frameIdxInAnimFloat = animLoopingRdfOffset + ((frameIdxInAnim - animLoopingRdfOffset) % (totRdfCnt - animLoopingRdfOffset));
                float normalizedFromTime = (frameIdxInAnimFloat / totRdfCnt); // TODO: Anyway to avoid using division here?
                animator.Play(targetClip.name, targetLayer, normalizedFromTime);
            } else {
                float normalizedFromTime = (frameIdxInAnim / totRdfCnt); // TODO: Anyway to avoid using division here?
                animator.Play(targetClip.name, targetLayer, normalizedFromTime);
            }
        }
    }

    public void pause(bool toPause) {
        var mainAnimator = getMainAnimator();
        if (toPause) {
            mainAnimator.speed = 0f;
        } else {
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
