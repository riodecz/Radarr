using NzbDrone.Core.Messaging.Commands;

namespace NzbDrone.Core.ImportLists
{
    public class ImportListSyncCommand : Command
    {
        public override bool SendUpdatesToClient => true;

        public int ListId = 0;
    }
}
