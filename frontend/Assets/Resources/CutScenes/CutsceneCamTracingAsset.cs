using UnityEngine;
using UnityEngine.Playables;

[System.Serializable]
public class CutsceneCamTracingAsset : PlayableAsset {
    [SerializeField] public ExposedReference<Camera> refCutsceneCam;
    [SerializeField] public ExposedReference<GameObject> refTracingTarget;

    public override Playable CreatePlayable(PlayableGraph graph, GameObject owner) {
        var resolver = graph.GetResolver();
        var behaviour = new CutsceneCamTracingBehaviour();
        behaviour.setReferenceFromScript(refCutsceneCam.Resolve(resolver), refTracingTarget.Resolve(resolver));
        return ScriptPlayable<CutsceneCamTracingBehaviour>.Create(graph, behaviour);
    }
}
