using UnityEngine;
using UnityEngine.Playables;
using System;
using UnityEngine.Timeline;

public class CutsceneManager : MonoBehaviour {
    public Camera mainCam, gameplayCam, cutsceneCam;
    private Vector3 gameplayCamOldPos = Vector3.zero;
    public float effectivelyInfinitelyFar;
    protected GameObject loadCutscenePrefab(string name) {
        string path = "Cutscenes/" + name + "/" + name;
        return Resources.Load(path) as GameObject;
    }

    private string playingCutsceneName = null;
    private PlayableDirector playingDirector = null;
    public void clear() {
        playingDirector = null;
        playingCutsceneName = null;
        foreach (Transform child in this.gameObject.transform) {
            GameObject.Destroy(child.gameObject);
        } 
        if (null != gameplayCam && Vector3.zero != gameplayCamOldPos) {
            gameplayCam.transform.position = gameplayCamOldPos;
            gameplayCamOldPos = Vector3.zero;
        }
    }

    public bool playOrContinue(string cutsceneName) {
        if (String.IsNullOrEmpty(cutsceneName)) {
            Debug.LogWarning("Couldn't play null cutsceneName");
            return false;
        }
        if (null != playingCutsceneName && playingCutsceneName.Equals(cutsceneName)) {
            playingDirector.Play();
            if (playingDirector.time >= playingDirector.duration) {
                return false;
            } else {
                return true;
            }
        } else {
            clear();
            var cutscenePrefab = loadCutscenePrefab(cutsceneName);
            if (null == cutscenePrefab) {
                Debug.LogWarning("Couldn't play null cutscenePrefab for: " + cutsceneName);
                return false;
            }
            if (null != gameplayCam) {
                gameplayCamOldPos = gameplayCam.transform.position;
                gameplayCam.transform.position = new Vector3(-effectivelyInfinitelyFar, -effectivelyInfinitelyFar, 1024);
            }
            playingCutsceneName = cutsceneName;

            // Assign default values to the prefab such that when "CutsceneCamTracingAsset.CreatePlayable(...)" is called, it has proper initial values
            var playableDirectorInPrefab = cutscenePrefab.GetComponent<PlayableDirector>();
            var playableAssetInPrefab = playableDirectorInPrefab.playableAsset;
            foreach (var output in playableAssetInPrefab.outputs) {
                if ("CutsceneCamTracer".Equals(output.streamName)) {
                    PlayableTrack trackAsset = (PlayableTrack)output.sourceObject;
                    foreach (var clip in trackAsset.GetClips()) {
                        switch (clip.asset) {
                            case CutsceneCamTracingAsset camTracerAsset:
                                playableDirectorInPrefab.SetReferenceValue(camTracerAsset.refCutsceneCam.exposedName, cutsceneCam);
                                break;
                            default:
                                break;
                        }
                    }
                    break;
                }
            }

            var cutsceneObj = Instantiate(cutscenePrefab, new Vector3(0, 0, -1f), Quaternion.identity, this.transform);
            playingDirector = cutsceneObj.GetComponent<PlayableDirector>();
            /**
             [WARNING] Make sure that "playingDirector.Play()" is called immediately after attaching the cutscenePrefab, otherwise the cutscene camera position will flash at (0, 0, YOUR_INIT_Z) for 1 frame which is odd to watch!
             */
            playingDirector.Play();
            return true;
        }
    }
}
