using UnityEngine;

public class TeamRibbon : MonoBehaviour {
    // Start is called before the first frame update
    void Start() {
        
    }

    // Update is called once per frame
    void Update() {
        
    }

    public void setBulletTeamId(int bulletTeamId) {
        var renderer = gameObject.GetComponent<SpriteRenderer>();
        switch (bulletTeamId) {
            case 1:
                renderer.color = Color.red;
                break;
            case 2:
                renderer.color = Color.blue;
                break;
            case 3:
                renderer.color = Color.cyan;
                break;
            case 4:
                renderer.color = Color.yellow;
                break;
            default:
                break;
        }
    }
}
