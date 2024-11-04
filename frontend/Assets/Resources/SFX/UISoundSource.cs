using UnityEngine;

public class UISoundSource : MonoBehaviour {
    public const int CURSOR = 0;
    public const int POSITIVE = 1;
    public const int NEGATIVE = 2;
    public const int CANCEL = 3;

    private AudioSource audioSource;
    public AudioClip[] audioClips;

    // Start is called before the first frame update
    void Start() {
        audioSource = GetComponent<AudioSource>();
    }
    public void PlayCursor() {
        audioSource.PlayOneShot(audioClips[CURSOR]);
    }

    public void PlayPositive() {
        audioSource.PlayOneShot(audioClips[POSITIVE]);
    }

    public void PlayNegative() {
        audioSource.PlayOneShot(audioClips[NEGATIVE]);
    }

    public void PlayCancel() {
        audioSource.PlayOneShot(audioClips[CANCEL]);
    }
}
