using shared;
using UnityEngine;
using UnityEngine.EventSystems;

public class StoryLevelCell : AbstractSprOnlySingleSelectCell, IPointerClickHandler {
    public int levelId;
    public int selectedIdx;
    public bool isLocked = false;
    public SpriteRenderer locked;
    public StoryLevelSelectGroup selectGroup;

    private int realtimeBtnLevel = 0;
    private int cachedBtnLevel = 0;
    private bool btnEdgeTriggerLock = false;

    // Start is called before the first frame update
    void Start() {
        
    }

    // Update is called once per frame
    void Update() {

    }

    public void UpdateByLevelProgress(PlayerLevelProgress progress) {
        if (null == progress) {
            gameObject.SetActive(false);
            isLocked = true;
        } else {
            gameObject.SetActive(true);
            if (0 >= progress.RemainingDependencies.Count) {
                locked.transform.localScale = Vector3.zero;
                isLocked = false;
            } else {
                locked.transform.localScale = Vector3.one;
                isLocked = true;
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
        /*
         [WARNING] 
        
         If this is not called, then go make sure that there's a "Physics2DRaycaster" attached to "Camera.main" with a larger than 0 interaction quota!
         
         Why using physics raycast to detect click on a Sprite? Reference https://stackoverflow.com/questions/41391708/how-to-detect-click-touch-events-on-ui-and-gameobjects
         */
        _triggerEdge(true);
    }
}
