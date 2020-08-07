namespace NzbDrone.Core.ImportLists
{
    public enum CleanLibraryLevels
    {
        Disabled,
        LogOnly,
        KeepAndUnmonitor,
        RemoveAndKeep,
        RemoveAndDelete
    }
}
