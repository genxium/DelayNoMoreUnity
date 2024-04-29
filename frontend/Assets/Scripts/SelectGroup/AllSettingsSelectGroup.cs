using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine;

public class AllSettingsSelectGroup : SettingsSelectGroup {
    public AbstractSingleSelectCell logoutBtn; 
    public void toggleLogoutBtnInteractability(bool val) {
        logoutBtn.gameObject.SetActive(val);
    }
}
