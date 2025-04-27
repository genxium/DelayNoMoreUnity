using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class TeamTowerHeading : MonoBehaviour {
    public TeamTowerHpBar selfTeamTowerHpBar;
    public GameObject otherTeamHpBars; 
    public GameObject teamHpBarPrefab;
    private Dictionary<int, TeamTowerHpBar> teamIdToHpBar; 
    public void Init(int selfBulletTeamId, Dictionary<int, int> mainTowersDict) {
        teamIdToHpBar = new Dictionary<int, TeamTowerHpBar>(); 
        selfTeamTowerHpBar.updateHpByValsAndCaps(0, 0);
        teamIdToHpBar[selfBulletTeamId] = selfTeamTowerHpBar;
        foreach (var (teamId, _) in mainTowersDict) {
            if (selfBulletTeamId == teamId) continue; 
            var thatHpBarNode = Instantiate(teamHpBarPrefab, Vector3.zero, Quaternion.identity, otherTeamHpBars.transform);
            TeamTowerHpBar thatHpBar = thatHpBarNode.GetComponent<TeamTowerHpBar>();
            teamIdToHpBar[teamId] = thatHpBar;  
        }
        this.gameObject.SetActive(true);
    }

    public void ResetSelf() {
        selfTeamTowerHpBar.updateHpByValsAndCaps(0, 0);
        foreach (Transform child in otherTeamHpBars.transform) {
            Destroy(child.gameObject);
        }
        this.gameObject.SetActive(false);
    }

    public TeamTowerHpBar GetByTeamId(int teamId) {
        return teamIdToHpBar[teamId];
    }
}
