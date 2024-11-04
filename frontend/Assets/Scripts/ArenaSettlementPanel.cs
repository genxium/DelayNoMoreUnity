using UnityEngine;
using shared;
using DG.Tweening;
using Google.Protobuf.Collections;
using UnityEngine.UI;

public class ArenaSettlementPanel : SettlementPanel {
    public HorizontalLayoutGroup columns;
    public GameObject arenaSettlementColumnPrefab;

    public override void PlaySettlementAnim(bool success) {
        if (success) {
            failed.gameObject.SetActive(false);
            finished.gameObject.SetActive(true);
            finished.gameObject.transform.localScale = Vector3.one;
            finished.gameObject.transform.DOScale(1f * Vector3.one, 0.7f);
        } else {
            finished.gameObject.SetActive(false);
            failed.gameObject.SetActive(true);
            failed.gameObject.transform.localScale = Vector3.one;
            failed.gameObject.transform.DOScale(1f * Vector3.one, 0.7f);
        }
    }

    public void SetCharacters(RepeatedField<CharacterDownsync> chdList) {
        foreach (var column in columns.GetComponentsInChildren<ArenaSettlementColumn>()) {
            Destroy(column.gameObject);
        }
        
        foreach (var chd in chdList) {
            var newColumn = Instantiate(arenaSettlementColumnPrefab, columns.transform);
            var content = newColumn.GetComponent<ArenaSettlementColumn>();
            content.SetCharacter(chd);
        }
    }
}
