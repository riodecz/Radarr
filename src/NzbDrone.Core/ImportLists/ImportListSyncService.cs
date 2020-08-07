using System.Collections.Generic;
using System.Linq;
using NLog;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.ImportLists.ImportExclusions;
using NzbDrone.Core.ImportLists.ListMovies;
using NzbDrone.Core.Messaging.Commands;
using NzbDrone.Core.MetadataSource;
using NzbDrone.Core.Movies;

namespace NzbDrone.Core.ImportLists
{
    public interface IFetchImportList
    {
    }

    public class ImportListSyncService : IFetchImportList, IExecute<ImportListSyncCommand>
    {
        private readonly Logger _logger;
        private readonly IImportListFactory _importListFactory;
        private readonly IImportListStatusService _importListStatusService;
        private readonly IMovieService _movieService;
        private readonly IAddMovieService _addMovieService;
        private readonly IListMovieService _listMovieService;
        private readonly ISearchForNewMovie _movieSearch;
        private readonly IConfigService _configService;
        private readonly IImportExclusionsService _exclusionService;

        public ImportListSyncService(IImportListFactory importListFactory,
                                      IImportListStatusService importListStatusService,
                                      IMovieService movieService,
                                      IAddMovieService addMovieService,
                                      IListMovieService listMovieService,
                                      ISearchForNewMovie movieSearch,
                                      IConfigService configService,
                                      IImportExclusionsService exclusionService,
                                      Logger logger)
        {
            _importListFactory = importListFactory;
            _importListStatusService = importListStatusService;
            _movieService = movieService;
            _addMovieService = addMovieService;
            _listMovieService = listMovieService;
            _movieSearch = movieSearch;
            _exclusionService = exclusionService;
            _logger = logger;
            _configService = configService;
        }

        private ImportListFetchResult GetListMovies()
        {
            var movies = new List<ListMovie>();
            var anyFailure = false;

            var importLists = _importListFactory.GetAvailableProviders();
            var blockedLists = _importListStatusService.GetBlockedProviders().ToDictionary(v => v.ProviderId, v => v);

            foreach (var list in importLists)
            {
                if (blockedLists.TryGetValue(list.Definition.Id, out ImportListStatus blockedListStatus))
                {
                    _logger.Debug("Temporarily ignoring list {0} till {1} due to recent failures.", list.Definition.Name, blockedListStatus.DisabledTill.Value.ToLocalTime());
                    anyFailure |= true; //Ensure we don't clean if a list is down
                    continue;
                }

                var result = list.Fetch();

                if (!result.AnyFailure)
                {
                    // TODO some opportunity to bulk map here if we had the tmdbIds
                    result.Movies.ToList().ForEach(x =>
                    {
                        // TODO some logic to avoid mapping everything (if its a tmdb in the db use the existing movie, etc..)
                        MapMovieReport(x);
                    });

                    movies.AddRange(result.Movies);
                    _listMovieService.SyncMoviesForList(result.Movies.ToList(), list.Definition.Id);
                }

                anyFailure |= result.AnyFailure;
            }

            _logger.Debug("Found {0} movies from list(s) {1}", movies.Count, string.Join(", ", importLists.Select(l => l.Definition.Name)));

            return new ImportListFetchResult
            {
                Movies = movies.DistinctBy(x =>
                {
                    if (x.TmdbId != 0)
                    {
                        return x.TmdbId.ToString();
                    }

                    if (x.ImdbId.IsNotNullOrWhiteSpace())
                    {
                        return x.ImdbId;
                    }

                    return x.Title;
                }).ToList(),
                AnyFailure = anyFailure
            };
        }

        private void ProcessMovieReport(ImportListDefinition importList, ListMovie report, List<ImportExclusion> listExclusions, List<Movie> moviesToAdd)
        {
            if (report.TmdbId == 0 || !importList.EnableAuto)
            {
                return;
            }

            // Check to see if movie in DB
            var existingMovie = _movieService.FindByTmdbId(report.TmdbId);

            if (existingMovie != null)
            {
                _logger.Debug("{0} [{1}] Rejected, Movie Exists in DB", report.TmdbId, report.Title);
                return;
            }

            // Check to see if movie excluded
            var excludedMovie = listExclusions.Where(s => s.TmdbId == report.TmdbId).SingleOrDefault();

            if (excludedMovie != null)
            {
                _logger.Debug("{0} [{1}] Rejected due to list exlcusion", report.TmdbId, report.Title);
                return;
            }

            // Append Artist if not already in DB or already on add list
            if (moviesToAdd.All(s => s.TmdbId != report.TmdbId))
            {
                var monitored = importList.ShouldMonitor;

                moviesToAdd.Add(new Movie
                {
                    Monitored = monitored,
                    RootFolderPath = importList.RootFolderPath,
                    ProfileId = importList.ProfileId,
                    MinimumAvailability = importList.MinimumAvailability,
                    Tags = importList.Tags,
                    AddOptions = new AddMovieOptions
                    {
                        SearchForMovie = monitored,
                    }
                });
            }
        }

        private void SyncAll()
        {
            var result = GetListMovies();

            //if there are no lists that are enabled for automatic import then dont do anything
            if (_importListFactory.GetAvailableProviders().Where(a => ((ImportListDefinition)a.Definition).EnableAuto).Empty())
            {
                _logger.Info("No lists are enabled for auto-import.");
                return;
            }

            var listedMovies = result.Movies.ToList();

            if (!result.AnyFailure)
            {
                CleanLibrary(listedMovies);
            }

            var importExclusions = _exclusionService.GetAllExclusions();
            var moviesToAdd = new List<Movie>();

            foreach (var movie in listedMovies)
            {
                var importList = _importListFactory.Get(movie.ListId);

                if (movie.TmdbId != 0)
                {
                    ProcessMovieReport(importList, movie, importExclusions, moviesToAdd);
                }
            }

            if (moviesToAdd.Any())
            {
                _logger.Info($"Adding {moviesToAdd.Count()} movies from your auto enabled lists to library");
            }

            _addMovieService.AddMovies(moviesToAdd, true);
        }

        private void MapMovieReport(ListMovie report)
        {
            var mappedMovie = _movieSearch.MapMovieToTmdbMovie(new Movie { Title = report.Title, TmdbId = report.TmdbId, ImdbId = report.ImdbId, Year = report.Year });

            if (mappedMovie != null)
            {
                report.TmdbId = mappedMovie.TmdbId;
                report.ImdbId = mappedMovie.ImdbId;
                report.Title = mappedMovie.Title;
                report.SortTitle = mappedMovie?.SortTitle;
                report.Year = mappedMovie.Year;
                report.Overview = mappedMovie.Overview;
                report.Ratings = mappedMovie.Ratings;
                report.Studio = mappedMovie.Studio;
                report.Certification = mappedMovie.Certification;
                report.Collection = mappedMovie.Collection;
                report.Status = mappedMovie.Status;
                report.Images = mappedMovie.Images;
                report.Website = mappedMovie.Website;
                report.YouTubeTrailerId = mappedMovie.YouTubeTrailerId;
                report.Translations = mappedMovie.Translations;
                report.InCinemas = mappedMovie.InCinemas;
                report.PhysicalRelease = mappedMovie.PhysicalRelease;
                report.DigitalRelease = mappedMovie.DigitalRelease;
                report.Genres = mappedMovie.Genres;
            }
        }

        public void Execute(ImportListSyncCommand message)
        {
            SyncAll();
        }

        private void CleanLibrary(List<ListMovie> listMovies)
        {
            var moviesToUpdate = new List<Movie>();

            if (_configService.ListSyncLevel == "disabled")
            {
                return;
            }

            var moviesInLibrary = _movieService.GetAllMovies();
            foreach (var movie in moviesInLibrary)
            {
                var movieExists = listMovies.Any(c => c.TmdbId == movie.TmdbId || c.ImdbId == movie.ImdbId);

                if (!movieExists)
                {
                    switch (_configService.ListSyncLevel)
                    {
                        case "logOnly":
                            _logger.Info("{0} was in your library, but not found in your lists --> You might want to unmonitor or remove it", movie);
                            break;
                        case "keepAndUnmonitor":
                            _logger.Info("{0} was in your library, but not found in your lists --> Keeping in library but Unmonitoring it", movie);
                            movie.Monitored = false;
                            moviesToUpdate.Add(movie);
                            break;
                        case "removeAndKeep":
                            _logger.Info("{0} was in your library, but not found in your lists --> Removing from library (keeping files)", movie);
                            _movieService.DeleteMovie(movie.Id, false);
                            break;
                        case "removeAndDelete":
                            _logger.Info("{0} was in your library, but not found in your lists --> Removing from library and deleting files", movie);
                            _movieService.DeleteMovie(movie.Id, true);

                            //TODO: for some reason the files are not deleted in this case... any idea why?
                            break;
                        default:
                            break;
                    }
                }
            }

            _movieService.UpdateMovie(moviesToUpdate, true);
        }
    }
}
