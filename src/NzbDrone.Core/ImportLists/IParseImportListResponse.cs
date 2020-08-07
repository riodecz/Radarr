using System.Collections.Generic;
using NzbDrone.Core.ImportLists.ListMovies;

namespace NzbDrone.Core.ImportLists
{
    public interface IParseImportListResponse
    {
        IList<ListMovie> ParseResponse(ImportListResponse importListResponse);
    }
}
