using System.Collections.Generic;
using System.Linq;
using FizzWare.NBuilder;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.ImportLists;
using NzbDrone.Core.ImportLists.ImportExclusions;
using NzbDrone.Core.ImportLists.ListMovies;
using NzbDrone.Core.MetadataSource;
using NzbDrone.Core.Movies;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.ImportList
{
    [TestFixture]
    public class ImportListSearchServiceFixture : CoreTest<ImportListSyncService>
    {
        private ImportListFetchResult _importList1Fetch;
        private ImportListFetchResult _importList2Fetch;
        private List<ListMovie> _moviesList1;
        private List<Movie> _moviesList2;
        private List<ListMovie> _moviesList3;
        private List<IImportList> _importLists;
        private ImportListSyncCommand _command;

        [SetUp]
        public void Setup()
        {
            _importLists = new List<IImportList>();

            _moviesList1 = Builder<ListMovie>.CreateListOfSize(5)
                .Build().ToList();

            _moviesList2 = Builder<Movie>.CreateListOfSize(3)
                .TheFirst(1)
                .With(s => s.TmdbId = 6)
                .With(s => s.ImdbId = "6")
                .TheNext(1)
                .With(s => s.TmdbId = 7)
                .With(s => s.ImdbId = "7")
                .TheNext(1)
                .With(s => s.TmdbId = 8)
                .With(s => s.ImdbId = "8")
                .Build().ToList();

            _moviesList3 = Builder<ListMovie>.CreateListOfSize(3)
                .TheFirst(1)
                .With(s => s.TmdbId = 6)
                .With(s => s.ImdbId = "6")
                .TheNext(1)
                .With(s => s.TmdbId = 7)
                .With(s => s.ImdbId = "7")
                .TheNext(1)
                .With(s => s.TmdbId = 8)
                .With(s => s.ImdbId = "8")
                .Build().ToList();

            _importList1Fetch = new ImportListFetchResult
            {
                Movies = _moviesList1,
                AnyFailure = false
            };

            _importList2Fetch = new ImportListFetchResult
            {
                Movies = _moviesList3,
                AnyFailure = false
            };

            _command = new ImportListSyncCommand
            {
                ListId = 0
            };

            Mocker.GetMock<IImportListFactory>()
                  .Setup(v => v.GetAvailableProviders())
                  .Returns(_importLists);

            Mocker.GetMock<IImportExclusionsService>()
                  .Setup(v => v.GetAllExclusions())
                  .Returns(new List<ImportExclusion>());

            Mocker.GetMock<IImportListStatusService>()
                  .Setup(v => v.GetBlockedProviders())
                  .Returns(new List<ImportListStatus>());

            Mocker.GetMock<ISearchForNewMovie>()
                  .Setup(v => v.MapMovieToTmdbMovie(It.IsAny<Movie>()))
                  .Returns((Movie movie) => movie);

            Mocker.GetMock<IMovieService>()
                  .Setup(v => v.MovieExists(It.IsAny<Movie>()))
                  .Returns(false);
        }

        private void GivenListFailure()
        {
            _importList1Fetch.AnyFailure = true;
        }

        private void GivenCleanLevel(string cleanLevel)
        {
            Mocker.GetMock<IConfigService>()
                  .SetupGet(v => v.ListSyncLevel)
                  .Returns(cleanLevel);
        }

        private void GivenList(int id, bool enabledAuto, ImportListFetchResult fetchResult)
        {
            var importListDefinition = new ImportListDefinition { Id = id, EnableAuto = enabledAuto };

            Mocker.GetMock<IImportListFactory>()
                  .Setup(v => v.Get(id))
                  .Returns(importListDefinition);

            CreateListResult(id, enabledAuto, fetchResult);
        }

        private Mock<IImportList> CreateListResult(int i, bool enabledAuto, ImportListFetchResult fetchResult)
        {
            var id = i;

            fetchResult.Movies.ToList().ForEach(m => m.ListId = id);

            var importListDefinition = new ImportListDefinition { Id = id, EnableAuto = enabledAuto };

            var mockImportList = new Mock<IImportList>();
            mockImportList.SetupGet(s => s.Definition).Returns(importListDefinition);
            mockImportList.SetupGet(s => s.Enabled).Returns(true);
            mockImportList.SetupGet(s => s.EnableAuto).Returns(enabledAuto);
            mockImportList.Setup(s => s.Fetch()).Returns(fetchResult);

            _importLists.Add(mockImportList.Object);

            return mockImportList;
        }

        [Test]
        public void should_not_clean_library_if_config_value_disable()
        {
            GivenList(1, true, _importList1Fetch);

            GivenCleanLevel("disabled");

            Subject.Execute(_command);

            Mocker.GetMock<IMovieService>()
                  .Verify(v => v.GetAllMovies(), Times.Never());

            Mocker.GetMock<IMovieService>()
                  .Verify(v => v.UpdateMovie(new List<Movie>(), true), Times.Never());
        }

        [Test]
        public void should_log_only_on_clean_library_if_config_value_logonly()
        {
            GivenList(1, true, _importList1Fetch);

            GivenCleanLevel("logOnly");

            Mocker.GetMock<IMovieService>()
                  .Setup(v => v.GetAllMovies())
                  .Returns(_moviesList2);

            Subject.Execute(_command);

            Mocker.GetMock<IMovieService>()
                  .Verify(v => v.GetAllMovies(), Times.Once());

            Mocker.GetMock<IMovieService>()
                  .Verify(v => v.DeleteMovie(It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<bool>()), Times.Never());

            Mocker.GetMock<IMovieService>()
                  .Verify(v => v.UpdateMovie(new List<Movie>(), true), Times.Once());
        }

        [Test]
        public void should_unmonitor_on_clean_library_if_config_value_keepAndUnmonitor()
        {
            GivenList(1, true, _importList1Fetch);

            GivenCleanLevel("keepAndUnmonitor");

            Mocker.GetMock<IMovieService>()
                  .Setup(v => v.GetAllMovies())
                  .Returns(_moviesList2);

            Subject.Execute(_command);

            Mocker.GetMock<IMovieService>()
                  .Verify(v => v.GetAllMovies(), Times.Once());

            Mocker.GetMock<IMovieService>()
                  .Verify(v => v.DeleteMovie(It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<bool>()), Times.Never());

            Mocker.GetMock<IMovieService>()
                  .Verify(v => v.UpdateMovie(It.Is<List<Movie>>(s => s.Count == 3 && s.All(m => !m.Monitored)), true), Times.Once());
        }

        [Test]
        public void should_not_clean_on_clean_library_if_tmdb_match()
        {
            _importList1Fetch.Movies[0].TmdbId = 6;

            GivenList(1, true, _importList1Fetch);

            GivenCleanLevel("keepAndUnmonitor");

            Mocker.GetMock<IMovieService>()
                  .Setup(v => v.GetAllMovies())
                  .Returns(_moviesList2);

            Subject.Execute(_command);

            Mocker.GetMock<IMovieService>()
                  .Verify(v => v.UpdateMovie(It.Is<List<Movie>>(s => s.Count == 2 && s.All(m => !m.Monitored)), true), Times.Once());
        }

        [Test]
        public void should_fallback_to_imdbid_on_clean_library_if_tmdb_not_found()
        {
            _importList1Fetch.Movies[0].TmdbId = 0;
            _importList1Fetch.Movies[0].ImdbId = "6";

            GivenList(1, true, _importList1Fetch);

            GivenCleanLevel("keepAndUnmonitor");

            Mocker.GetMock<IMovieService>()
                  .Setup(v => v.GetAllMovies())
                  .Returns(_moviesList2);

            Subject.Execute(_command);

            Mocker.GetMock<IMovieService>()
                  .Verify(v => v.UpdateMovie(It.Is<List<Movie>>(s => s.Count == 2 && s.All(m => !m.Monitored)), true), Times.Once());
        }

        [Test]
        public void should_delete_movies_not_files_on_clean_library_if_config_value_logonly()
        {
            GivenList(1, true, _importList1Fetch);

            GivenCleanLevel("removeAndKeep");

            Mocker.GetMock<IMovieService>()
                  .Setup(v => v.GetAllMovies())
                  .Returns(_moviesList2);

            Subject.Execute(_command);

            Mocker.GetMock<IMovieService>()
                  .Verify(v => v.GetAllMovies(), Times.Once());

            Mocker.GetMock<IMovieService>()
                  .Verify(v => v.DeleteMovie(It.IsAny<int>(), false, It.IsAny<bool>()), Times.Exactly(3));

            Mocker.GetMock<IMovieService>()
                  .Verify(v => v.DeleteMovie(It.IsAny<int>(), true, It.IsAny<bool>()), Times.Never());

            Mocker.GetMock<IMovieService>()
                  .Verify(v => v.UpdateMovie(new List<Movie>(), true), Times.Once());
        }

        [Test]
        public void should_delete_movies_and_files_on_clean_library_if_config_value_logonly()
        {
            GivenList(1, true, _importList1Fetch);

            GivenCleanLevel("removeAndDelete");

            Mocker.GetMock<IMovieService>()
                  .Setup(v => v.GetAllMovies())
                  .Returns(_moviesList2);

            Subject.Execute(_command);

            Mocker.GetMock<IMovieService>()
                  .Verify(v => v.GetAllMovies(), Times.Once());

            Mocker.GetMock<IMovieService>()
                  .Verify(v => v.DeleteMovie(It.IsAny<int>(), false, It.IsAny<bool>()), Times.Never());

            Mocker.GetMock<IMovieService>()
                  .Verify(v => v.DeleteMovie(It.IsAny<int>(), true, It.IsAny<bool>()), Times.Exactly(3));

            Mocker.GetMock<IMovieService>()
                  .Verify(v => v.UpdateMovie(new List<Movie>(), true), Times.Once());
        }

        [Test]
        public void should_not_clean_if_list_failures()
        {
            GivenListFailure();
            GivenList(1, true, _importList1Fetch);

            GivenCleanLevel("disabled");

            Subject.Execute(_command);

            Mocker.GetMock<IMovieService>()
                  .Verify(v => v.UpdateMovie(new List<Movie>(), true), Times.Never());
        }

        [Test]
        public void should_add_new_movies_from_single_list_to_library()
        {
            GivenList(1, true, _importList1Fetch);

            GivenCleanLevel("disabled");

            Subject.Execute(_command);

            Mocker.GetMock<IAddMovieService>()
                  .Verify(v => v.AddMovies(It.Is<List<Movie>>(s => s.Count == 5), true), Times.Once());
        }

        [Test]
        public void should_add_new_movies_from_multiple_list_to_library()
        {
            GivenList(1, true, _importList1Fetch);
            GivenList(2, true, _importList2Fetch);

            GivenCleanLevel("disabled");

            Subject.Execute(_command);

            Mocker.GetMock<IAddMovieService>()
                  .Verify(v => v.AddMovies(It.Is<List<Movie>>(s => s.Count == 8), true), Times.Once());
        }

        [Test]
        public void should_add_new_movies_from_enabled_lists_to_library()
        {
            GivenList(1, true, _importList1Fetch);
            GivenList(2, false, _importList2Fetch);

            GivenCleanLevel("disabled");

            Subject.Execute(_command);

            Mocker.GetMock<IAddMovieService>()
                  .Verify(v => v.AddMovies(It.Is<List<Movie>>(s => s.Count == 5), true), Times.Once());
        }

        [Test]
        public void should_not_add_duplicate_movies_from_seperate_lists()
        {
            _importList2Fetch.Movies[0].TmdbId = 4;

            GivenList(1, true, _importList1Fetch);
            GivenList(2, true, _importList2Fetch);

            GivenCleanLevel("disabled");

            Subject.Execute(_command);

            Mocker.GetMock<IAddMovieService>()
                  .Verify(v => v.AddMovies(It.Is<List<Movie>>(s => s.Count == 7), true), Times.Once());
        }

        [Test]
        public void should_not_add_movie_from_on_exclusion_list()
        {
            GivenList(1, true, _importList1Fetch);
            GivenList(2, true, _importList2Fetch);

            GivenCleanLevel("disabled");

            Mocker.GetMock<IImportExclusionsService>()
                  .Setup(v => v.GetAllExclusions())
                  .Returns(new List<ImportExclusion> { new ImportExclusion { TmdbId = _moviesList2[0].TmdbId } });

            Subject.Execute(_command);

            Mocker.GetMock<IAddMovieService>()
                  .Verify(v => v.AddMovies(It.Is<List<Movie>>(s => s.Count == 7 && !s.Any(m => m.TmdbId == _moviesList2[0].TmdbId)), true), Times.Once());
        }

        [Test]
        public void should_not_add_movie_that_exists_in_library()
        {
            GivenList(1, true, _importList1Fetch);
            GivenList(2, true, _importList2Fetch);

            GivenCleanLevel("disabled");

            Mocker.GetMock<IMovieService>()
                 .Setup(v => v.FindByTmdbId(_moviesList2[0].TmdbId))
                 .Returns(new Movie { TmdbId = _moviesList2[0].TmdbId });

            Subject.Execute(_command);

            Mocker.GetMock<IAddMovieService>()
                  .Verify(v => v.AddMovies(It.Is<List<Movie>>(s => s.Count == 7 && !s.Any(m => m.TmdbId == _moviesList2[0].TmdbId)), true), Times.Once());
        }
    }
}
