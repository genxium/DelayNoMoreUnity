public class AllSettingsSelectGroup : SettingsSelectGroup {
    public AbstractSingleSelectCell logoutBtn; 
    public void toggleLogoutBtnInteractability(bool val) {
        logoutBtn.gameObject.SetActive(val);
    }
}
