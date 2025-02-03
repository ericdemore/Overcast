using Microsoft.AspNetCore.Mvc;
using TMDbLib.Client;
using TMDbLib.Objects.Search;
using TMDbLib.Objects.Movies;
using TMDbLib.Objects.TvShows;
using Overcast.Models;
using System.Threading.Tasks;
using System.Linq;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;

namespace Overcast.Controllers
{
    public class ComparisonController : Controller
    {
        private readonly TMDbClient _client;
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;

        public ComparisonController(IConfiguration configuration, IHttpClientFactory httpClientFactory)
        {
            _apiKey = configuration["TMDb:ApiKey"];
            _client = new TMDbClient(_apiKey);
            _httpClient = httpClientFactory.CreateClient();
            _httpClient.BaseAddress = new Uri("https://api.themoviedb.org/3/");
        }

        public IActionResult Index()
        {
            return View(new MediaComparisonModel());
        }

        private SearchBase FindBestMatch(string query, IList<SearchBase> results)
        {
            query = query.ToLower().Trim();
            
            var exactMatch = results.FirstOrDefault(r => 
                (r is SearchMovie movie && movie.Title.ToLower() == query) ||
                (r is SearchTv tv && tv.Name.ToLower() == query));
            
            if (exactMatch != null)
                return exactMatch;

            var startsWithMatch = results.FirstOrDefault(r =>
                (r is SearchMovie movie && movie.Title.ToLower().StartsWith(query)) ||
                (r is SearchTv tv && tv.Name.ToLower().StartsWith(query)));

            if (startsWithMatch != null)
                return startsWithMatch;

            var containsMatch = results.FirstOrDefault(r =>
                (r is SearchMovie movie && movie.Title.ToLower().Contains(query)) ||
                (r is SearchTv tv && tv.Name.ToLower().Contains(query)));

            return containsMatch;
        }

        private async Task<List<TMDbLib.Objects.Movies.Cast>> GetTvShowAggregateCastAsync(int tvShowId)
        {
            try
            {
                var response = await _httpClient.GetAsync($"tv/{tvShowId}/aggregate_credits?api_key={_apiKey}");
                response.EnsureSuccessStatusCode();
                
                var content = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(content);
                var castArray = doc.RootElement.GetProperty("cast");
                
                var cast = new List<TMDbLib.Objects.Movies.Cast>();
                
                foreach (var actor in castArray.EnumerateArray())
                {
                    var roles = actor.GetProperty("roles").EnumerateArray()
                        .Select(r => r.GetProperty("character").GetString())
                        .Where(c => !string.IsNullOrEmpty(c))
                        .Distinct()
                        .ToList();

                    cast.Add(new TMDbLib.Objects.Movies.Cast
                    {
                        Id = actor.GetProperty("id").GetInt32(),
                        Name = actor.GetProperty("name").GetString(),
                        Character = string.Join(" / ", roles),
                        ProfilePath = actor.GetProperty("profile_path").GetString()
                    });
                }
                
                return cast;
            }
            catch
            {
                // Fallback to regular credits if aggregate credits fail
                var tvShow = await _client.GetTvShowAsync(tvShowId);
                if (tvShow?.NumberOfSeasons == null || tvShow.NumberOfSeasons <= 0)
                    return new List<TMDbLib.Objects.Movies.Cast>();

                var allCastMembers = new Dictionary<int, TMDbLib.Objects.Movies.Cast>();

                // Get main show credits
                var mainCredits = await _client.GetTvShowCreditsAsync(tvShowId);
                if (mainCredits?.Cast != null)
                {
                    foreach (var cast in mainCredits.Cast)
                    {
                        allCastMembers[cast.Id] = new TMDbLib.Objects.Movies.Cast
                        {
                            Id = cast.Id,
                            Name = cast.Name,
                            Character = cast.Character,
                            ProfilePath = cast.ProfilePath
                        };
                    }
                }

                // Get credits for each season as backup
                for (int season = 1; season <= tvShow.NumberOfSeasons; season++)
                {
                    try
                    {
                        var seasonCredits = await _client.GetTvSeasonCreditsAsync(tvShowId, season);
                        if (seasonCredits?.Cast != null)
                        {
                            foreach (var cast in seasonCredits.Cast)
                            {
                                if (!allCastMembers.ContainsKey(cast.Id))
                                {
                                    allCastMembers[cast.Id] = new TMDbLib.Objects.Movies.Cast
                                    {
                                        Id = cast.Id,
                                        Name = cast.Name,
                                        Character = cast.Character,
                                        ProfilePath = cast.ProfilePath
                                    };
                                }
                                else if (allCastMembers[cast.Id].Character != cast.Character)
                                {
                                    var existingCharacter = allCastMembers[cast.Id].Character;
                                    var newCharacter = cast.Character;
                                    if (!string.IsNullOrEmpty(existingCharacter) && !string.IsNullOrEmpty(newCharacter) &&
                                        !existingCharacter.Contains(newCharacter))
                                    {
                                        allCastMembers[cast.Id].Character = $"{existingCharacter} / {newCharacter}";
                                    }
                                }
                            }
                        }
                    }
                    catch
                    {
                        continue;
                    }
                }

                return allCastMembers.Values.ToList();
            }
        }

        [HttpPost]
        public async Task<IActionResult> Compare(string title1, string title2)
        {
            var model = new MediaComparisonModel
            {
                Title1 = title1,
                Title2 = title2
            };

            try
            {
                var searchTask1 = _client.SearchMultiAsync(title1 ?? string.Empty);
                var searchTask2 = _client.SearchMultiAsync(title2 ?? string.Empty);

                await Task.WhenAll(searchTask1, searchTask2);

                var searchResult1 = await searchTask1;
                var searchResult2 = await searchTask2;

                if (searchResult1?.Results == null || searchResult2?.Results == null)
                {
                    model.ErrorMessage = "Unable to search for titles. Please try again later.";
                    return View("Index", model);
                }

                var media1 = FindBestMatch(title1, searchResult1.Results);
                var media2 = FindBestMatch(title2, searchResult2.Results);

                if (media1 == null)
                    model.NotFoundTitles.Add(title1);
                if (media2 == null)
                    model.NotFoundTitles.Add(title2);

                if (model.NotFoundTitles.Any())
                {
                    if (model.NotFoundTitles.Count == 1)
                    {
                        model.ErrorMessage = $"'{model.NotFoundTitles[0]}' does not exist in the database.";
                    }
                    else
                    {
                        model.ErrorMessage = $"'{model.NotFoundTitles[0]}' and '{model.NotFoundTitles[1]}' do not exist in the database.";
                    }
                    return View("Index", model);
                }

                var commonActors = new List<CommonCastMember>();

                // Handle media1
                var cast1 = new List<TMDbLib.Objects.Movies.Cast>();
                if (media1 is SearchMovie movie1)
                {
                    var movieCredits = await _client.GetMovieCreditsAsync(movie1.Id);
                    if (movieCredits?.Cast != null)
                    {
                        cast1 = movieCredits.Cast;
                        model.Title1 = movie1.Title;
                    }
                }
                else if (media1 is SearchTv tv1)
                {
                    cast1 = await GetTvShowAggregateCastAsync(tv1.Id);
                    model.Title1 = tv1.Name;
                }

                // Handle media2
                var cast2 = new List<TMDbLib.Objects.Movies.Cast>();
                if (media2 is SearchMovie movie2)
                {
                    var movieCredits = await _client.GetMovieCreditsAsync(movie2.Id);
                    if (movieCredits?.Cast != null)
                    {
                        cast2 = movieCredits.Cast;
                        model.Title2 = movie2.Title;
                    }
                }
                else if (media2 is SearchTv tv2)
                {
                    cast2 = await GetTvShowAggregateCastAsync(tv2.Id);
                    model.Title2 = tv2.Name;
                }

                // Find common cast members
                commonActors = cast1
                    .Join(cast2,
                        actor1 => actor1.Id,
                        actor2 => actor2.Id,
                        (actor1, actor2) => new CommonCastMember
                        {
                            Id = actor1.Id,
                            Name = actor1.Name ?? "Unknown",
                            ProfilePath = !string.IsNullOrEmpty(actor1.ProfilePath)
                                ? $"https://image.tmdb.org/t/p/w200{actor1.ProfilePath}"
                                : "/images/no-profile.jpg",
                            Character1 = actor1.Character ?? "Unknown Role",
                            Character2 = actor2.Character ?? "Unknown Role",
                            Media1ReleaseDate = media1 is SearchMovie m1 
                                ? m1.ReleaseDate?.ToString("yyyy-MM-dd") ?? "Unknown Date"
                                : ((SearchTv)media1).FirstAirDate?.ToString("yyyy-MM-dd") ?? "Unknown Date",
                            Media2ReleaseDate = media2 is SearchMovie m2
                                ? m2.ReleaseDate?.ToString("yyyy-MM-dd") ?? "Unknown Date"
                                : ((SearchTv)media2).FirstAirDate?.ToString("yyyy-MM-dd") ?? "Unknown Date"
                        })
                    .ToList();

                model.CommonCast = commonActors;
            }
            catch (Exception ex)
            {
                model.ErrorMessage = "An error occurred while searching. Please try again later.";
            }

            return View("Index", model);
        }
    }
}
