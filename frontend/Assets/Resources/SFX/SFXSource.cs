using System.Collections.Generic;
using UnityEngine;

public class SFXSource : MonoBehaviour {
    public int score;
    public float maxDistanceInWorld;
    public AudioSource audioSource;
    public Dictionary<string, AudioClip> audioClipDict;

    // Start is called before the first frame update
    void Start() {
        audioSource = GetComponent<AudioSource>();
    }
}
