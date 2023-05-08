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

    public void updateCharacterAnim(string newAnimName, int frameIdxInAnim, int dirX, bool spontaneousLooping, RoomDownsyncFrame rdf) {
        var animator = this.gameObject.GetComponent<Animator>();
        if (0 > dirX) {
            this.gameObject.transform.localScale = new Vector3(-1.0f, 1.0f);
        } else if (0 < dirX) {
            this.gameObject.transform.localScale = new Vector3(+1.0f, 1.0f);
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
