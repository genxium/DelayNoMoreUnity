using UnityEngine;
using shared;

public class BGMSource : MonoBehaviour {
    public AudioSource audioSource;
    public AudioClip[] presetBgms;

    // Start is called before the first frame update
    void Start() {
        audioSource = GetComponent<AudioSource>();
    }

    // Update is called once per frame
    void Update() {

    }

    public void Play() {
        if (audioSource.isPlaying) return;
        audioSource.Play();
    }

    public void Stop() {
        audioSource.Stop();
    }

    public void PlaySpecifiedBgm(int id) {
        if (Battle.BGM_NO_CHANGE == id) return;
        int idx = id - 1;
        if (0 > idx || idx >= presetBgms.Length) {
            return;
        }
        var targetClip = presetBgms[idx];
        if (targetClip == audioSource.clip) return;
        
        audioSource.Stop();
        audioSource.clip = targetClip;
        audioSource.Play();
    }
}
