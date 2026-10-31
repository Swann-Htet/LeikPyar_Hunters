// True while a full panel (Settings, Upgrades) is open, or a training page waits for Enter: the game must not react to look, move or interact keys then.
public static class UiLock
{
    public static bool Open => UpgradePanel.IsOpen || SettingsPanel.IsOpen || GuidePanel.IsOpen || LetterViewer.IsOpen || TutorialMode.Frozen;
}
