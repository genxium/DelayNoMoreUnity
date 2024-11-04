using UnityEngine;
using UnityEngine.Tilemaps;

public class ParallaxEffect : MonoBehaviour {
    // Reference https://blog.yarsalabs.com/parallax-effect-in-unity-2d/
    private Vector3 newPosHolder = new Vector3();

    private float _startingPos;
    private float _lengthOfSprite;
    private float _halfLengthOfSprite;
    private Camera mainCam; 

    private float xParallax;

    public void SetParallaxAmount(float theXParallax, Camera theMainCam) {
        xParallax = theXParallax;
        mainCam = theMainCam;
    }

    // Start is called before the first frame update
    void Start() {
        //Getting the starting X position of sprite.
        _startingPos = transform.position.x;
        //Getting the length of the sprites.
        _lengthOfSprite = GetComponent<TilemapRenderer>().bounds.size.x;
        _halfLengthOfSprite = 0.5f*_lengthOfSprite;
    }

    // Update is called once per frame
    void Update() {
        Vector3 camPos = mainCam.transform.position;
        float allegedNewX = camPos.x * (1 - xParallax);
        if (allegedNewX > _startingPos + _halfLengthOfSprite) {
            _startingPos += _lengthOfSprite;
        } else if (allegedNewX + _halfLengthOfSprite < _startingPos ) {
            _startingPos -= _lengthOfSprite;
        }

        float d = camPos.x * xParallax;

        newPosHolder.Set(_startingPos + d, transform.position.y, transform.position.z);

        transform.position = newPosHolder;
    }
}
