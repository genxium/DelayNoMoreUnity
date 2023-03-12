using UnityEngine;
using System;
using shared;
using static shared.Battle;

public class MapController : MonoBehaviour {
    int roomCapacity = 2;
    GameObject[] playersArr;
    public GameObject characterPrefab;

    int[] lastIndividuallyConfirmedInputFrameId;
    ulong[] lastIndividuallyConfirmedInputList;
    PlayerDownsync selfPlayerInfo = null;
    FrameRingBuffer<InputFrameDownsync> inputBuffer = null;

    // Start is called before the first frame update
    void Start() {
        playersArr = new GameObject[roomCapacity];
        lastIndividuallyConfirmedInputFrameId = new int[roomCapacity];
        Array.Fill<int>(lastIndividuallyConfirmedInputFrameId, -1);
        lastIndividuallyConfirmedInputList = new ulong[roomCapacity];
        Array.Fill<ulong>(lastIndividuallyConfirmedInputList, 0);
        spawnPlayerNode(0, 1024, -512);
        inputBuffer = new FrameRingBuffer<InputFrameDownsync>(512);
    }

    // Update is called once per frame
    void Update() {

    }

    void spawnPlayerNode(int joinIndex, int vx, int vy) {
        GameObject newPlayerNode = Instantiate(characterPrefab, new Vector3(vx, vy, 0), Quaternion.identity);
        playersArr[joinIndex] = newPlayerNode;
    }

    (ulong, ulong) getOrPrefabInputFrameUpsync(int inputFrameId, bool canConfirmSelf) {
        if (
          // null == ctrl ||
          null == selfPlayerInfo
        ) {
            String msg = String.Format("noDelayInputFrameId={0:D} couldn't be generated due to either ctrl or selfPlayerInfo is null", inputFrameId);
            throw new ArgumentException(msg);
        }

        ulong previousSelfInput = 0,
          currSelfInput = 0;
        int joinIndex = selfPlayerInfo.JoinIndex;
        ulong selfJoinIndexMask = ((ulong)1 << (joinIndex - 1));
        var (_, existingInputFrame) = inputBuffer.GetByFrameId(inputFrameId);
        var (_, previousInputFrameDownsync) = inputBuffer.GetByFrameId(inputFrameId - 1);
        previousSelfInput = (null == previousInputFrameDownsync ? 0 : previousInputFrameDownsync.InputList[joinIndex - 1]);
        if (
          null != existingInputFrame
          &&
          (true != canConfirmSelf)
        ) {
            return (previousSelfInput, existingInputFrame.InputList[joinIndex - 1]);
        }

        ulong[] prefabbedInputList = new ulong[roomCapacity];
        Array.Fill<ulong>(prefabbedInputList, 0);
        for (int k = 0; k < roomCapacity; ++k) {
            if (null != existingInputFrame) {
                // When "null != existingInputFrame", it implies that "true == canConfirmSelf" here, we just have to assign "prefabbedInputList[(joinIndex-1)]" specifically and copy all others
                prefabbedInputList[k] = existingInputFrame.InputList[k];
            } else if (lastIndividuallyConfirmedInputFrameId[k] <= inputFrameId) {
                prefabbedInputList[k] = lastIndividuallyConfirmedInputList[k];
                // Don't predict "btnA & btnB"!
                prefabbedInputList[k] = (prefabbedInputList[k] & 15);
            } else if (null != previousInputFrameDownsync) {
                // When "self.lastIndividuallyConfirmedInputFrameId[k] > inputFrameId", don't use it to predict a historical input!
                prefabbedInputList[k] = previousInputFrameDownsync.InputList[k];
                // Don't predict "btnA & btnB"!
                prefabbedInputList[k] = (prefabbedInputList[k] & 15);
            }
        }

        // [WARNING] Do not blindly use "selfJoinIndexMask" here, as the "actuallyUsedInput for self" couldn't be confirmed while prefabbing, otherwise we'd have confirmed a wrong self input by "_markConfirmationIfApplicable()"!
        ulong initConfirmedList = 0;
        if (null != existingInputFrame) {
            // When "null != existingInputFrame", it implies that "true == canConfirmSelf" here
            initConfirmedList = (existingInputFrame.ConfirmedList | selfJoinIndexMask);
        }
        // currSelfInput = ctrl.getEncodedInput(); // When "null == existingInputFrame", it'd be safe to say that the realtime "ctrl.getEncodedInput()" is for the requested "inputFrameId"
        prefabbedInputList[(joinIndex - 1)] = currSelfInput;
        while (inputBuffer.EdFrameId <= inputFrameId) {
            // Fill the gap
            int gapInputFrameId = inputBuffer.EdFrameId;
            inputBuffer.DryPut();
            var (_, ifdHolder) = inputBuffer.GetByFrameId(gapInputFrameId);
            if (null == ifdHolder) {
                // Lazy heap alloc 
                InputFrameDownsync prefabbedInputFrameDownsync = new InputFrameDownsync();
                prefabbedInputFrameDownsync.InputFrameId = gapInputFrameId;
                for (int k = 0; k < roomCapacity; ++k) {
                    prefabbedInputFrameDownsync.InputList.Add(prefabbedInputList[k]);
                }
                prefabbedInputFrameDownsync.ConfirmedList = initConfirmedList;
                inputBuffer.SetByFrameId(prefabbedInputFrameDownsync, gapInputFrameId);
            } else {
                ifdHolder.InputFrameId = gapInputFrameId;
                for (int k = 0; k < roomCapacity; ++k) {
                    ifdHolder.InputList[k] = prefabbedInputList[k];
                }
                ifdHolder.ConfirmedList = initConfirmedList;
            }
        }

        return (previousSelfInput, currSelfInput);
    }
}
