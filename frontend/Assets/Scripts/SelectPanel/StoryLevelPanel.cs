using UnityEngine;
using UnityEngine.UI; // Required when Using UI elements.
using UnityEngine.SceneManagement;
using System;
using Story;
using SuperTiled2Unity;
using shared;
using System.Collections.Generic;
using UnityEngine.InputSystem;

public class StoryLevelPanel : MonoBehaviour {
    public UISoundSource uiSoundSource;
    public GameObject levelCellPrefab;
    public Image cancelBtn;
    public StoryModeCharacterSelectPanel characterSelectPanel;
    public StoryLvInfoPanel lvInfoPanel;
    public float cameraSpeed = 100;

    public StoryRegionPanel regionPanel;

    private int selectionPhase = 0;
    private int selectedLevelIdx = -1;
    private string selectedLevelName = null;
    private StoryLevelSelectGroup levels;
    private GameObject regionLevels = null;

    protected int spaceOffsetX;
    protected int spaceOffsetY;
    protected float cameraCapMinX, cameraCapMaxX, cameraCapMinY, cameraCapMaxY;
    protected float effectivelyInfinitelyFar;

    private bool isInCameraAutoTracking = false;

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
        Debug.Log("StoryLevelSelectPanel reset");
        Camera.main.orthographicSize = 256;

        if (null != regionLevels) {
            Destroy(regionLevels);
        }

        isDragging = false;
        levels = GetComponent<StoryLevelSelectGroup>();

        int regionId = PlayerStoryProgressManager.Instance.GetCurrentRegionId();
        var regionName = StoryConstants.REGION_NAMES[regionId];

        string regionLevelsPrefabPath = String.Format("LevelMaps/{0}Levels/map", regionName);
        var regionLevelsPrefab = Resources.Load(regionLevelsPrefabPath) as GameObject;
        regionLevels = GameObject.Instantiate(regionLevelsPrefab, this.gameObject.transform);

        var superMap = regionLevels.GetComponent<SuperMap>();
        int mapWidth = superMap.m_Width, tileWidth = superMap.m_TileWidth, mapHeight = superMap.m_Height, tileHeight = superMap.m_TileHeight;
        spaceOffsetX = ((mapWidth * tileWidth) >> 1);
        spaceOffsetY = ((mapHeight * tileHeight) >> 1);

        calcCameraCaps();

        var grid = regionLevels.GetComponentInChildren<Grid>();
        foreach (Transform child in grid.transform) {
            switch (child.gameObject.name) {
                case "Levels":
                    var newCellDataList = new List<(int, float, float)>();
                    foreach (Transform levelChild in child) {
                        var levelTileObj = levelChild.GetComponent<SuperObject>();
                        var tileProps = levelChild.GetComponent<SuperCustomProperties>();
                        CustomProperty levelId;
                        tileProps.TryGetCustomProperty("levelId", out levelId);

                        int levelIdVal = null == levelId || levelId.IsEmpty ? StoryConstants.LEVEL_NONE : levelId.GetValueAsInt();

                        var (cx, cy) = Battle.TiledLayerPositionToCollisionSpacePosition(levelTileObj.m_X + .5f*levelTileObj.m_Width, levelTileObj.m_Y + .5f * levelTileObj.m_Height, spaceOffsetX, spaceOffsetY);
                        var (wx, wy) = Battle.CollisionSpacePositionToWorldPosition(cx, cy, spaceOffsetX, spaceOffsetY);

                        newCellDataList.Add((levelIdVal, wx, wy));
                        
                        // TODO: By now I have to enable the import of all colliders to see the "inMapCollider: EdgeCollider2D" component, then remove unused components here :(
                        Destroy(levelChild.GetComponent<EdgeCollider2D>());
                        Destroy(levelChild.GetComponent<BoxCollider2D>());
                        Destroy(levelChild.GetComponent<SuperColliderComponent>());
                    }

                    newCellDataList.Sort(delegate ((int, float, float) lhs, (int, float, float) rhs) {
                        return Math.Sign(lhs.Item1 - rhs.Item1);
                    });

                    var newCellsList = new List<StoryLevelCell>();
                    foreach (var (levelIdVal, wx, wy) in newCellDataList) {
                        var levelCell = GameObject.Instantiate(levelCellPrefab, regionLevels.gameObject.transform);

                        newPosHolder.Set(wx, wy, +5);
                        levelCell.transform.position = newPosHolder;

                        var storyLevelCell = levelCell.GetComponent<StoryLevelCell>();
                        storyLevelCell.levelId = levelIdVal;
                        storyLevelCell.selectedIdx = newCellsList.Count;
                        storyLevelCell.selectGroup = levels;
                        newCellsList.Add(storyLevelCell);
                    }

                    levels.ResetCells(newCellsList.ToArray());
                    break;
            }
        }

        levels.postCancelledCallback = OnCancelBtnClicked;
        levels.levelPostConfirmedCallback = (int selectedIdx) => {
            selectedLevelIdx = selectedIdx;
            var levelCell = levels.cells[selectedIdx];
            var storyLevelCell = levelCell.GetComponent<StoryLevelCell>();
            selectedLevelName = StoryConstants.LEVEL_NAMES[storyLevelCell.levelId];
            selectionPhase = 1;
            isInCameraAutoTracking = false;
            characterSelectPanel.gameObject.SetActive(true);
            toggleUIInteractability(true);
            var lvProgress = storyLevelCell.GetCellProgress();
            lvInfoPanel.refreshLvInfo(selectedLevelName, storyLevelCell.GetIsLocked(), lvProgress);
            lvInfoPanel.toggleUIInteractability(true);
            lvInfoPanel.gameObject.SetActive(true);
            //Debug.LogFormat("StoryLevelSelectPanel levelPostConfirmedCallback, now selectedLevelIdx={0}, selectedLevelName={1}", selectedLevelIdx, selectedLevelName);
        };

        levels.levelPostCursorMovedCallback = (int selectedIdx) => {
            selectedLevelIdx = selectedIdx;
            var levelCell = levels.cells[selectedIdx];
            var storyLevelCell = levelCell.GetComponent<StoryLevelCell>();
            selectedLevelName = StoryConstants.LEVEL_NAMES[storyLevelCell.levelId];
            isInCameraAutoTracking = true;
            var lvProgress = storyLevelCell.GetCellProgress();
            lvInfoPanel.refreshLvInfo(selectedLevelName, storyLevelCell.GetIsLocked(), lvProgress);
            lvInfoPanel.toggleUIInteractability(false);
            //Debug.LogFormat("StoryLevelSelectPanel levelPostCursorMovedCallback, now selectedLevelIdx={0}, selectedLevelName={1}", selectedLevelIdx, selectedLevelName);
        };

        var levelProgressList = PlayerStoryProgressManager.Instance.LoadLevelsUnderCurrentRegion();
        for (int i = 0; i < levels.cells.Length; i++) {
            // [WARNING] For now we require that "levels.cells" and "levelProgressList" are perfectly aligned, i.e. no "levelId" matching
            var cell = levels.cells[i] as StoryLevelCell;
            cell.UpdateByLevelProgress(levelProgressList[i]);
        }
        characterSelectPanel.gameObject.SetActive(false);
        characterSelectPanel.SetCallbacks((v) => allConfirmed((uint)v), OnCancelBtnClicked);
        var levelsUnderRegion = StoryConstants.LEVEL_UNDER_REGION[PlayerStoryProgressManager.Instance.GetCurrentRegionId()];
        if (-1 == selectedLevelIdx) {
            selectedLevelIdx = levelsUnderRegion.IndexOf(PlayerStoryProgressManager.Instance.GetCurrentLevelId());
            selectedLevelName = StoryConstants.LEVEL_NAMES[PlayerStoryProgressManager.Instance.GetCurrentLevelId()];
        }

        var activeLevelCell = levels.cells[selectedLevelIdx];
        var activeStoryLevelCell = activeLevelCell.GetComponent<StoryLevelCell>();
        var lvProgress = activeStoryLevelCell.GetCellProgress();
        lvInfoPanel.refreshLvInfo(selectedLevelName, activeStoryLevelCell.GetIsLocked(), lvProgress);
        lvInfoPanel.toggleUIInteractability(false);
        lvInfoPanel.gameObject.SetActive(true);

        cameraTeleport();
        selectionPhase = 0;

        toggleUIInteractability(true);
    }

    public void toggleUIInteractability(bool enabled) {
        switch (selectionPhase) {
            case 0:
                levels.toggleUIInteractability(enabled);
                characterSelectPanel.toggleUIInteractability(!enabled);
                cancelBtn.gameObject.SetActive(enabled);
                if (enabled) {
                    var selectedCell = levels.cells[selectedLevelIdx] as StoryLevelCell;
                    selectedCell.setSelected(true);
                    levels.drySetSelectedIdx(selectedLevelIdx);
                }
                break;
            case 1:
                levels.toggleUIInteractability(!enabled);
                characterSelectPanel.toggleUIInteractability(enabled);
                cancelBtn.gameObject.SetActive(enabled);
                break;
            case 2:
                levels.toggleUIInteractability(false);
                characterSelectPanel.toggleUIInteractability(false);
                cancelBtn.gameObject.SetActive(false);
                break;
        }
    }

    public void OnCancelBtnClicked() {
        if (null != uiSoundSource) {
            uiSoundSource.PlayCancel();
        }
        if (null != lvInfoPanel) {
            lvInfoPanel.toggleUIInteractability(false);
        }
        Debug.Log("StoryLevelSelectPanel OnCancelBtnClicked at selectionPhase=" + selectionPhase);
        if (0 < selectionPhase) {
            ResetSelf();
        } else {
            toggleUIInteractability(false);
            if (null != regionLevels) {
                Destroy(regionLevels);
            }
            if (null != lvInfoPanel) {
                lvInfoPanel.gameObject.SetActive(false);
            }
            gameObject.SetActive(false);
            PlayerStoryProgressManager.Instance.SetView(PlayerStoryModeSelectView.Region);
            regionPanel.gameObject.SetActive(true);
            regionPanel.ResetSelf();

        }
    }

    public void allConfirmed(uint selectedSpeciesId) {
        Debug.Log("StoryLevelSelectPanel allConfirmed at selectedSpeciesId=" + selectedSpeciesId + ", selectedLevelName=" + selectedLevelName);
        try {
            characterSelectPanel.toggleUIInteractability(false);
            cancelBtn.gameObject.SetActive(false);
            selectionPhase = 2;
            toggleUIInteractability(false);
            characterSelectPanel.SetCallbacks(null, null);
            levels.postCancelledCallback = null;
            levels.levelPostConfirmedCallback = null;

            PlayerStoryProgressManager.Instance.SetCachedForOfflineMap(selectedSpeciesId, selectedLevelName, lvInfoPanel.getFinishedLvOption());
            SceneManager.LoadScene("OfflineMapScene", LoadSceneMode.Single);
        } catch (Exception ex) {
            Debug.LogError(ex);
            ResetSelf();
        }
    }

    protected Vector3 newPosHolder = new Vector3();
    protected Vector2 camDiffDstHolder = new Vector2();
    protected void calcCameraCaps() {
        int paddingX = (int)(Camera.main.orthographicSize * Camera.main.aspect);
        int paddingY = (int)(Camera.main.orthographicSize);
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
        var camOldPos = Camera.main.transform.position;
        var dst = levels.cells[selectedLevelIdx].transform.position;
        camDiffDstHolder.Set(dst.x - camOldPos.x, dst.y - camOldPos.y);

        // Immediately teleport
        var stepLength = Time.deltaTime * cameraSpeed;
        if (stepLength > camDiffDstHolder.magnitude) {
            newPosHolder.Set(dst.x, dst.y, camOldPos.z);
        } else {    
            var newMapPosDiff2 = camDiffDstHolder.normalized * stepLength;
            newPosHolder.Set(camOldPos.x + newMapPosDiff2.x, camOldPos.y + newMapPosDiff2.y, camOldPos.z);
        }
        clampToMapBoundary(ref newPosHolder);

        Camera.main.transform.position = newPosHolder;
    }


    protected void cameraTeleport() {
        isInCameraAutoTracking = false;
        var camOldPos = Camera.main.transform.position;
        var dst = levels.cells[selectedLevelIdx].transform.position;

        // Immediately teleport
        newPosHolder.Set(dst.x, dst.y, camOldPos.z);
        clampToMapBoundary(ref newPosHolder);
        Camera.main.transform.position = newPosHolder;
    }

    private bool isDragging = false;
    private Vector2 dragStartingPos = Vector2.zero;
    public void OnPointerPress(InputAction.CallbackContext context) {
        if (0 != selectionPhase) return;
        isInCameraAutoTracking = false;
        bool rising = context.ReadValueAsButton();
        if (rising) {
            if (!isDragging) {
                //Debug.Log("StoryLevelPanel: Pointer press registers");
                isDragging = true;
                dragStartingPos = Vector2.zero;
            }
        } else {
            //Debug.LogFormat("StoryLevelPanel: Pointer press ends by context phase={0}", context.phase);
            isDragging = false;
            dragStartingPos = Vector2.zero;
        }
    }
    public void OnPointerDrag(InputAction.CallbackContext context) {
        if (0 != selectionPhase) return;
        isInCameraAutoTracking = false;
        if (isDragging && context.performed) {
            var positionInWindowSpace = context.ReadValue<Vector2>();
            var posInMainCamViewport = Camera.main.ScreenToViewportPoint(positionInWindowSpace);
            bool isWithinCamera = (0f <= posInMainCamViewport.x && posInMainCamViewport.x <= 1f && 0f <= posInMainCamViewport.y && posInMainCamViewport.y <= 1f);
            if (!isWithinCamera) {
                //Debug.LogFormat("StoryLevelPanel: Pointer press ends by posInMainCamViewport={0}", posInMainCamViewport);
                isDragging = false;
                dragStartingPos = Vector2.zero;
            } else {
                if (Vector2.zero == dragStartingPos) {
                    dragStartingPos = positionInWindowSpace;
                    //Debug.LogFormat("StoryLevelPanel: Pointer press starts at dragStartingPos={0}", dragStartingPos);
                } else {
                    var deltaInWindowSpace = positionInWindowSpace - dragStartingPos;
                    // Debug.LogFormat("Pointer Held Down - delta in window space = {0}", deltaInWindowSpace);
                    camDiffDstHolder = deltaInWindowSpace; // TODO: convert "deltaInWindowSpace" to world space vector
                    var camOldPos = Camera.main.transform.position;
                    newPosHolder.Set(camOldPos.x - camDiffDstHolder.x, camOldPos.y - camDiffDstHolder.y, camOldPos.z);
                    clampToMapBoundary(ref newPosHolder);
                    Camera.main.transform.position = newPosHolder;
                    dragStartingPos = positionInWindowSpace;
                }
            }
        }
    }
}
