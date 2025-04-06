using UnityEngine;
using UnityEngine.UI; // Required when Using UI elements.
using UnityEngine.SceneManagement;
using System;
using Story;
using SuperTiled2Unity;
using shared;
using System.Collections.Generic;
using UnityEngine.InputSystem;
using static FrontendOnlyGeometry;

public class StoryRegionPanel : MonoBehaviour {
    public UISoundSource uiSoundSource;
    public Image cancelBtn;
    public float cameraSpeed = 100;
    public GameObject storyRegionCellPrefab;
    public StoryLevelPanel levelPanel;
    public Sprite lockedSprite;
    public GameObject underlyingMapPrefab;
    public Camera gameplayCamera;
    public Camera cutSceneCamera;

    private int selectionPhase = 0;
    private int selectedRegionIdx = -1;
    private StoryRegionSelectGroup regions;

    protected int spaceOffsetX;
    protected int spaceOffsetY;
    protected float cameraCapMinX, cameraCapMaxX, cameraCapMinY, cameraCapMaxY;
    protected float effectivelyInfinitelyFar;

    private bool isInCameraAutoTracking = false;

    private GameObject underlyingMap;

    void Start() {
        // [WARNING] Reenable default physics engine such that when back from OfflineMap, the click of StoryCell can still work.
        Physics.autoSimulation = true;
        Physics2D.simulationMode = SimulationMode2D.Update;
        Application.targetFrameRate = 60;
        Application.SetStackTraceLogType(LogType.Log, StackTraceLogType.None);
        Application.SetStackTraceLogType(LogType.Warning, StackTraceLogType.None);
    }

    void OnEnable() {
        ResetSelf();
    }

    void Update() {
        if (isInCameraAutoTracking) {
            cameraTrack();
        }
    }

    public void ResetSelf() {
        Debug.Log("StoryRegionPanel reset");
        gameplayCamera.orthographicSize = 384;
        if (null != underlyingMap) {
            Destroy(underlyingMap);
        }

        isDragging = false;
        regions = GetComponent<StoryRegionSelectGroup>();
        var currentView = PlayerStoryProgressManager.Instance.GetCurrentView();
        if (PlayerStoryModeSelectView.Level == currentView) {
            gameObject.SetActive(false);
            levelPanel.gameObject.SetActive(true);
            return;
        }

        levelPanel.gameObject.SetActive(false);
        underlyingMap = GameObject.Instantiate(underlyingMapPrefab, this.gameObject.transform);

        var grid = underlyingMap.GetComponentInChildren<Grid>();
        var superMap = underlyingMap.GetComponent<SuperMap>();
        int mapWidth = superMap.m_Width, tileWidth = superMap.m_TileWidth, mapHeight = superMap.m_Height, tileHeight = superMap.m_TileHeight;
        spaceOffsetX = ((mapWidth * tileWidth) >> 1);
        spaceOffsetY = ((mapHeight * tileHeight) >> 1);

        calcCameraCaps();

        Dictionary<string, SuperTileLayer> nameToLayer = new Dictionary<string, SuperTileLayer>();
        foreach (SuperTileLayer layer in grid.GetComponentsInChildren<SuperTileLayer>()) {
            nameToLayer.Add(layer.name, layer);
        }

        var regionProgressDict = PlayerStoryProgressManager.Instance.LoadRegions();
        foreach (Transform child in grid.transform) {
            switch (child.gameObject.name) {
                case "RegionColliders":
                    var newCellsList = new List<StoryRegionCell>();
                    foreach (Transform regionChild in child) {
                        var collider2D = regionChild.GetComponent<PolygonCollider2D>();
                        var levelTileObj = regionChild.GetComponent<SuperObject>();
                        var tileProps = regionChild.GetComponent<SuperCustomProperties>();
                        CustomProperty regionId;
                        tileProps.TryGetCustomProperty("regionId", out regionId);

                        int regionIdVal = null == regionId || regionId.IsEmpty ? StoryConstants.REGION_NONE : regionId.GetValueAsInt();

                        var (wx, wy) = TiledLayerPositionToWorldPosition(levelTileObj.m_X + .5f*levelTileObj.m_Width, levelTileObj.m_Y + .5f * levelTileObj.m_Height);

                        float wxOffset = 0;
                        float wyOffset = 0;

                        int ptsCount = 0;
                        foreach (var p in collider2D.points) {
                            wxOffset += p.x;
                            wyOffset += p.y;
                            ++ptsCount;
                        }
                        wxOffset /= ptsCount;
                        wyOffset /= ptsCount;

                        var regionCellObj = GameObject.Instantiate(storyRegionCellPrefab, underlyingMap.gameObject.transform);
                        var regionCell = regionCellObj.GetComponent<StoryRegionCell>();

                        newPosHolder.Set(wx+wxOffset, wy+wyOffset, +5);
                        regionCell.transform.position = newPosHolder;

                        var layer = nameToLayer[regionChild.name];
                        regionCell.SetTiledBindings(regionIdVal - 1, regionIdVal, layer, regions);
                        var polygon2DCollider = regionCell.GetComponent<PolygonCollider2D>();
                        polygon2DCollider.points = collider2D.points;
                        polygon2DCollider.offset = new Vector2(-wxOffset, -wyOffset);

                        var regionProgress = regionProgressDict[regionIdVal];
                        regionCell.UpdateByRegionProgress(regionProgress);

                        if (regionIdVal != PlayerStoryProgressManager.Instance.GetCurrentRegionId()) {
                            regionCell.setSelected(false);
                        } else {
                            regionCell.setSelected(true);
                        }

                        newCellsList.Add(regionCell);
                    }

                    newCellsList.Sort(delegate (StoryRegionCell lhs, StoryRegionCell rhs) {
                        return Math.Sign(lhs.regionId - rhs.regionId);
                    });

                    regions.ResetCells(newCellsList.ToArray());
                    Destroy(child.gameObject); // Delete the whole ObjectLayer
                    break;
            }
        }

        regions.postCancelledCallback = OnCancelBtnClicked;
        regions.regionPostConfirmedCallback = (int selectedIdx) => {
            selectedRegionIdx = selectedIdx;
            Debug.LogFormat("StoryRegionPanel regionPostConfirmedCallback, now selectedRegionIdx={0}", selectedRegionIdx);
            toggleUIInteractability(false);
            if (null != underlyingMap) {
                Destroy(underlyingMap);
            }

            gameObject.SetActive(false);
            PlayerStoryProgressManager.Instance.SetView(PlayerStoryModeSelectView.Level);
            levelPanel.gameObject.SetActive(true);
            levelPanel.ResetSelf();
        };

        regions.regionPostCursorMovedCallback = (int selectedIdx) => {
            selectedRegionIdx = selectedIdx;
            isInCameraAutoTracking = true;
            Debug.LogFormat("StoryRegionPanel regionPostCursorMovedCallback, now selectedRegionIdx={0}", selectedRegionIdx);
        };

        if (-1 == selectedRegionIdx) {
            selectedRegionIdx = PlayerStoryProgressManager.Instance.GetCurrentRegionId()-1;
        }
        cameraTeleport();
        selectionPhase = 0;

        toggleUIInteractability(true);
    }

    public void toggleUIInteractability(bool enabled) {
        switch (selectionPhase) {
            case 0:
                regions.toggleUIInteractability(enabled);
                cancelBtn.gameObject.SetActive(enabled);
                if (enabled) {
                    var selectedCell = regions.cells[selectedRegionIdx] as StoryRegionCell;
                    selectedCell.setSelected(true);
                }
                break;
            case 1:
                regions.toggleUIInteractability(!enabled);
                cancelBtn.gameObject.SetActive(enabled);
                break;
            case 2:
                regions.toggleUIInteractability(false);
                cancelBtn.gameObject.SetActive(false);
                break;
        }
    }

    public void OnCancelBtnClicked() {
        if (null != uiSoundSource) {
            uiSoundSource.PlayCancel();
        }
        Debug.Log("StoryRegionPanel OnCancelBtnClicked at selectionPhase=" + selectionPhase);
        if (0 < selectionPhase) {
            ResetSelf();
        } else {
            toggleUIInteractability(false);
            SceneManager.LoadScene("LoginScene", LoadSceneMode.Single);
        }
    }

    protected Vector3 newPosHolder = new Vector3();
    protected Vector2 camDiffDstHolder = new Vector2();
    protected void calcCameraCaps() {
        int paddingX = (int)(gameplayCamera.orthographicSize * gameplayCamera.aspect);
        int paddingY = (int)(gameplayCamera.orthographicSize);
        cameraCapMinX = 0 + paddingX;
        cameraCapMaxX = (spaceOffsetX << 1) - paddingX;

        cameraCapMinY = -(spaceOffsetY << 1) + paddingY;
        cameraCapMaxY = 0 - paddingY;

        effectivelyInfinitelyFar = 4f * Math.Max(spaceOffsetX, spaceOffsetY);
    }

    protected void clampToMapBoundary(ref Vector3 posHolder) {
        float newX = posHolder.x, newY = posHolder.y, newZ = posHolder.z;
        if (newX > cameraCapMaxX) newX = cameraCapMaxX;
        if (newX < cameraCapMinX) newX = cameraCapMinX;
        if (newY > cameraCapMaxY) newY = cameraCapMaxY;
        if (newY < cameraCapMinY) newY = cameraCapMinY;
        posHolder.Set(newX, newY, newZ);
    }

    protected void cameraTrack() {
        var camOldPos = gameplayCamera.transform.position;
        var dstCell = regions.cells[selectedRegionIdx] as StoryRegionCell;
        var centerOffset = dstCell.GetCenterOffset();
        var dst = dstCell.transform.position;
        camDiffDstHolder.Set(dst.x + centerOffset.x - camOldPos.x, dst.y + centerOffset.y - camOldPos.y);

        // Immediately teleport
        var stepLength = Time.deltaTime * cameraSpeed;
        if (stepLength > camDiffDstHolder.magnitude) {
            newPosHolder.Set(dst.x + centerOffset.x, dst.y + centerOffset.y, camOldPos.z);
        } else {    
            var newMapPosDiff2 = camDiffDstHolder.normalized * stepLength;
            newPosHolder.Set(camOldPos.x + newMapPosDiff2.x, camOldPos.y + newMapPosDiff2.y, camOldPos.z);
        }
        clampToMapBoundary(ref newPosHolder);

        gameplayCamera.transform.position = newPosHolder;
    }


    protected void cameraTeleport() {
        isInCameraAutoTracking = false;
        var camOldPos = gameplayCamera.transform.position;
        var dstCell = regions.cells[selectedRegionIdx] as StoryRegionCell;
        var centerOffset = dstCell.GetCenterOffset();
        var dst = dstCell.transform.position;

        // Immediately teleport
        newPosHolder.Set(dst.x + centerOffset.x, dst.y + centerOffset.y, camOldPos.z);
        clampToMapBoundary(ref newPosHolder);
        gameplayCamera.transform.position = newPosHolder;
    }

    private bool isDragging = false;
    private Vector2 dragStartingPos = Vector2.zero;

    public void OnPointerPress(InputAction.CallbackContext context) {
        if (0 != selectionPhase) return;
        isInCameraAutoTracking = false;
        bool rising = context.ReadValueAsButton();
        if (rising) {
            if (!isDragging) {
                //Debug.Log("StoryRegionPanel: Pointer press registers");
                isDragging = true;
                dragStartingPos = Vector2.zero;
            }
        } else {
            //Debug.LogFormat("StoryRegionPanel: Pointer press ends by context phase={0}", context.phase);
            isDragging = false;
            dragStartingPos = Vector2.zero;
        }
    }

    public void OnPointerDrag(InputAction.CallbackContext context) {
        if (0 != selectionPhase) return;
        isInCameraAutoTracking = false;
        if (isDragging && context.performed) {
            var positionInWindowSpace = context.ReadValue<Vector2>();
            var posInMainCamViewport = gameplayCamera.ScreenToViewportPoint(positionInWindowSpace);
            bool isWithinCamera = (0f <= posInMainCamViewport.x && posInMainCamViewport.x <= 1f && 0f <= posInMainCamViewport.y && posInMainCamViewport.y <= 1f);
            if (!isWithinCamera) {
                //Debug.LogFormat("StoryRegionPanel: Pointer press ends by posInMainCamViewport={0}", posInMainCamViewport);
                isDragging = false;
                dragStartingPos = Vector2.zero;
            } else {
                if (Vector2.zero == dragStartingPos) {
                    dragStartingPos = positionInWindowSpace;
                    //Debug.LogFormat("StoryRegionPanel: Pointer press starts at dragStartingPos={0}", dragStartingPos);
                } else {
                    var deltaInWindowSpace = positionInWindowSpace - dragStartingPos;
                    // Debug.LogFormat("Pointer Held Down - delta in window space = {0}", deltaInWindowSpace);
                    camDiffDstHolder = deltaInWindowSpace; // TODO: convert "deltaInWindowSpace" to world space vector
                    var camOldPos = gameplayCamera.transform.position;
                    newPosHolder.Set(camOldPos.x - camDiffDstHolder.x, camOldPos.y - camDiffDstHolder.y, camOldPos.z);
                    clampToMapBoundary(ref newPosHolder);
                    gameplayCamera.transform.position = newPosHolder;
                    dragStartingPos = positionInWindowSpace;
                }
            }
        }
    }
}
