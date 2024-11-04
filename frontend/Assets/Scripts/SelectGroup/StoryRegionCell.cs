using shared;
using SuperTiled2Unity;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Tilemaps;

public class StoryRegionCell : AbstractSprOnlySingleSelectCell, IPointerClickHandler {
    public int regionId;
    public int selectedIdx;
    public bool isLocked = false;
    public bool isShadowed = false;

    public SpriteRenderer locked;
    public StoryRegionSelectGroup selectGroup;

    private int realtimeBtnLevel = 0;
    private int cachedBtnLevel = 0;
    private bool btnEdgeTriggerLock = false;

    private SuperTileLayer tileLayer;
    private Vector2 centerOffset;
    public Vector2 GetCenterOffset() {
        return centerOffset;
    }

    // Start is called before the first frame update
    void Start() {
        
    }

    // Update is called once per frame
    void Update() {

    }

    public void UpdateByRegionProgress(PlayerRegionProgress progress) {
        if (null == progress) {
            gameObject.SetActive(false);
            tileLayer.gameObject.SetActive(false);
            isLocked = true;
            isShadowed = true;
        } else {
            gameObject.SetActive(true);
            tileLayer.gameObject.SetActive(true);
            var tilemapComp = tileLayer.GetComponent<Tilemap>();
            var tmr = tileLayer.GetComponent<TilemapRenderer>();
            var material = tmr.material;
            if (0 >= progress.RemainingDependencies.Count) {
                locked.transform.localScale = Vector3.zero;
                isLocked = false;
                isShadowed = false;
                material.SetInt("_GrayOut", 0);
                tilemapComp.color = Color.white;
            } else {
                locked.transform.localScale = Vector3.one;
                isLocked = true;
                isShadowed = false;
                material.SetInt("_GrayOut", 1);
                if (3 < progress.RemainingDependencies.Count) {
                    isShadowed = true;
                    tilemapComp.color = Color.black;
                }
            }
        }
    }

    private void _triggerEdge(bool rising) {
        realtimeBtnLevel = (rising ? 1 : 0);
        if (!btnEdgeTriggerLock && (1 - realtimeBtnLevel) == cachedBtnLevel) {
            cachedBtnLevel = realtimeBtnLevel;
            btnEdgeTriggerLock = true;
        }

        if (rising) {
            /*
            DOGetter<Vector3> getter = () => gameObject.transform.localScale;
            DOSetter<Vector3> setter = (x) => gameObject.transform.localScale = x;
            DOTween.Sequence()
                .Append(DOTween.To(getter, setter, 0.8f * Vector3.one, 0.5f))
                .Append(DOTween.To(getter, setter, 1.0f * Vector3.one, 0.5f));
            */
            selectGroup.onCellSelected(selectedIdx);
        }
    }

    public void OnPointerClick(PointerEventData evt) {
        if (isShadowed) return;
        /*
         [WARNING] 
        
         If this is not called, then go make sure that there's a "Physics2DRaycaster" attached to "Camera.main" with a larger than 0 interaction quota!
         
         Why using physics raycast to detect click on a Sprite? Reference https://stackoverflow.com/questions/41391708/how-to-detect-click-touch-events-on-ui-and-gameobjects
         */
        _triggerEdge(true);
    }

    public void SetTiledBindings(int aSelectedIdx, int aRegionId, SuperTileLayer aTileLayer, StoryRegionSelectGroup aSelectGroup) {
        selectedIdx = aSelectedIdx;
        regionId = aRegionId;
        tileLayer = aTileLayer;
        selectGroup = aSelectGroup;
    }

    public override void setSelected(bool val) {
        var tmr = tileLayer.GetComponent<TilemapRenderer>();
        var material = tmr.material;
        if (val) {
            material.SetFloat("_FlashIntensity", 0f);
        } else {
            material.SetFloat("_FlashIntensity", 0.5f);
        }
    }
}
