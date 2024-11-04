using UnityEngine;
using shared;
using System;
using System.Collections.Generic;
using Unity.Mathematics;
using TMPro;
/*
 [WARNING] 

DON'T attach a "Transform.Rotation" timeline to ANY of the animation-clips in "Fireball Prefab", otherwise it will reset "gameObject.transform.rotation & gameObject.transform.localRotation" for EVERY animation-clip upon every frame update mysterious! 

If a default rotation is needed, edit it in external animation editor!
 */
public class FireballAnimController : MonoBehaviour {
    private string MATERIAL_REF_THICKNESS = "_Thickness";
    private float MAX_DAMAGE_DEALED_INDICATOR_H = 15f;

    private Vector3 positionHolder = Vector3.zero;
    public int lookupKey;
    public int score;
    public Dictionary<String, AnimationClip> lookUpTable;
    private Material material;

    public TMP_Text damageDealedIndicator;

    private void lazyInit() {
        if (null != lookUpTable) return;
        var spr = GetComponent<SpriteRenderer>();
        material = spr.material;
        lookUpTable = new Dictionary<String, AnimationClip>();
        var animator = this.gameObject.GetComponent<Animator>();
        foreach (AnimationClip clip in animator.runtimeAnimatorController.animationClips) {
            lookUpTable[clip.name] = clip;
        }
    }

    // Start is called before the first frame update
    void Start() {
        lazyInit();
    }

    public void updateAnim(int lookupK, String newAnimName, int frameIdxInAnim, int immediateDirX, BulletConfig bulletConfig, Bullet bullet, RoomDownsyncFrame rdf, int immediateVelX, int immediateVelY, bool sameTeamAsSelf) {
        lazyInit();
        bool isActuallyExploding = (BulletState.Exploding == bullet.BlState && frameIdxInAnim <= bulletConfig.ExplosionFrames);
        if (!isActuallyExploding) {
            damageDealedIndicator.gameObject.SetActive(false);
        } else {
            damageDealedIndicator.gameObject.SetActive(true);
            positionHolder.Set(damageDealedIndicator.gameObject.transform.localPosition.x, bullet.FramesInBlState < MAX_DAMAGE_DEALED_INDICATOR_H ? bullet.FramesInBlState : MAX_DAMAGE_DEALED_INDICATOR_H, damageDealedIndicator.gameObject.transform.localPosition.z);
            damageDealedIndicator.gameObject.transform.localPosition = positionHolder;
            damageDealedIndicator.text = 0 < bullet.DamageDealed ? bullet.DamageDealed.ToString() : "";
        }
        lookupKey = lookupK;
        var animator = gameObject.GetComponent<Animator>();
        var spr = gameObject.GetComponent<SpriteRenderer>();
        
        if (bulletConfig.RotatesAlongVelocity && 0 != immediateVelX && !isActuallyExploding) {
            // Rotate before flipping
            spr.transform.localRotation = Quaternion.AngleAxis(math.atan2(immediateVelY, immediateVelX) * Mathf.Rad2Deg, Vector3.forward);
            spr.flipX = false;
        } else if (0 != bullet.SpinSin && !isActuallyExploding) {
            spr.transform.localRotation = Quaternion.AngleAxis(math.atan2(bullet.SpinSin, bullet.SpinCos) * Mathf.Rad2Deg, Vector3.forward);
            if (0 > immediateDirX) {
                spr.flipX = true;
            } else if (0 < immediateDirX) {
                spr.flipX = false;
            }
        } else {
            spr.transform.localRotation = Quaternion.AngleAxis(0, Vector3.forward);
            if (0 > immediateDirX) {
                spr.flipX = true;
            } else if (0 < immediateDirX) {
                spr.flipX = false;  
            }
        }

        int targetLayer = 0; // We have only 1 layer, i.e. the baseLayer, playing at any time
        int targetClipIdx = 0; // We have only 1 frame anim playing at any time
        var curClip = animator.GetCurrentAnimatorClipInfo(targetLayer)[targetClipIdx].clip;
        var targetClip = lookUpTable[newAnimName];
        bool sameClipName = newAnimName.Equals(curClip.name);

        if (sameTeamAsSelf) {
            material.SetFloat(MATERIAL_REF_THICKNESS, 0);
        } else {
            material.SetFloat(MATERIAL_REF_THICKNESS, 0.25f);
        }

        if (sameClipName && curClip.isLooping) {
          return;
        }

        float normalizedFromTime = (frameIdxInAnim / (targetClip.frameRate * targetClip.length)); // TODO: Anyway to avoid using division here?
        animator.Play(newAnimName, targetLayer, normalizedFromTime);
    }
}
