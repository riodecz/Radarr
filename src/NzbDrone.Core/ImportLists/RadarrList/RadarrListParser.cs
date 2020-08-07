using System.Collections.Generic;
using Newtonsoft.Json;
using NzbDrone.Common.Extensions;
using NzbDrone.Common.Http;
using NzbDrone.Core.ImportLists.ListMovies;
using NzbDrone.Core.ImportLists.TMDb;

namespace NzbDrone.Core.ImportLists.RadarrList
{
    public class RadarrListParser : IParseImportListResponse
    {
        public RadarrListParser()
        {
        }

        public IList<ListMovie> ParseResponse(ImportListResponse netMovieImporterResponse)
        {
            var importResponse = netMovieImporterResponse;

            var movies = new List<ListMovie>();

            if (!PreProcess(importResponse))
            {
                return movies;
            }

            var jsonResponse = JsonConvert.DeserializeObject<List<MovieResultResource>>(importResponse.Content);

            // no movies were return
            if (jsonResponse == null)
            {
                return movies;
            }

            return jsonResponse.SelectList(m => new ListMovie { TmdbId = m.Id });
        }

        protected virtual bool PreProcess(ImportListResponse importListResponse)
        {
            try
            {
                var error = JsonConvert.DeserializeObject<RadarrErrors>(importListResponse.HttpResponse.Content);

                if (error != null && error.Errors != null && error.Errors.Count != 0)
                {
                    throw new RadarrListException(error);
                }
            }
            catch (JsonSerializationException)
            {
                //No error!
            }

            if (importListResponse.HttpResponse.StatusCode != System.Net.HttpStatusCode.OK)
            {
                throw new HttpException(importListResponse.HttpRequest, importListResponse.HttpResponse);
            }

            return true;
        }
    }
}
