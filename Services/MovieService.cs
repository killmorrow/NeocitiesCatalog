using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using MediaManager.Models;

namespace MediaManager.Services;

public class MovieService
{
    private readonly string _tmdbApiKey;

    private readonly HttpClient _httpClient;

    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public MovieService(string tmdbApiKey)
    {
        _tmdbApiKey = tmdbApiKey;
        _httpClient = new HttpClient();

        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(
            "MediaManager/1.0");
    }

    public async Task<Movie?> CreateMovieAsync(
        string title,
        double rating,
        bool cinema,
        string dateWatched,
        string review,
        string jsonPath)
    {
        var tmdbMovie = await SearchTmdbAsync(title, cinema);

        if (tmdbMovie == null)
            return null;

        var savedTitle = string.IsNullOrWhiteSpace(tmdbMovie.Title)
            ? ParseTrailingYear(title).Title
            : tmdbMovie.Title;

        var movie = new Movie
        {
            Id = CreateSlug(savedTitle),
            Title = savedTitle,
            Year = tmdbMovie.Year,
            Rating = rating,
            Cinema = cinema,
            DateWatched = dateWatched,
            Review = review,
            Poster = tmdbMovie.Poster,
            Director = tmdbMovie.Director
        };

        await AddToJsonAsync(movie, jsonPath);

        return movie;
    }

    private async Task<Movie?> SearchTmdbAsync(
        string title,
        bool cinema)
    {
        try
        {
            var (searchTitle, requestedYear) = ParseTrailingYear(title);

            Console.WriteLine();
            Console.WriteLine($"=== TMDB: {title} ===");

            var results = await SearchTmdbResultsAsync(
                searchTitle,
                "pt-BR",
                requestedYear);
            var found = FindBestTitleMatch(
                searchTitle,
                requestedYear,
                results);

            // Tenta também o título internacional quando a busca localizada
            // não trouxer um resultado que combine suficientemente com a entrada.
            if (found == null)
            {
                results = await SearchTmdbResultsAsync(
                    searchTitle,
                    "en-US",
                    requestedYear);
                found = FindBestTitleMatch(
                    searchTitle,
                    requestedYear,
                    results);
            }

            if (found == null)
            {
                Console.WriteLine("No sufficiently close movie title found in TMDB.");
                return null;
            }

            var posterUrl = "";

            if (!string.IsNullOrWhiteSpace(found.PosterPath))
            {
                posterUrl =
                    $"https://image.tmdb.org/t/p/w500" +
                    found.PosterPath;
            }

            var director = "";

            if (found.Id > 0)
            {
                director =
                    await GetDirectorAsync(found.Id);
            }

            var year = "";

            if (!string.IsNullOrWhiteSpace(found.ReleaseDate) &&
                found.ReleaseDate.Length >= 4)
            {
                year = found.ReleaseDate.Substring(0, 4);
            }

            Console.WriteLine("TMDB movie found.");

            return new Movie
            {
                Title = found.Title ?? found.OriginalTitle ?? searchTitle,
                Year = year,
                Poster = posterUrl,
                Director = director
            };
        }
        catch (Exception ex)
        {
            Console.WriteLine(
                $"TMDB error: {ex.Message}");

            return null;
        }
    }

    private async Task<List<TmdbMovie>> SearchTmdbResultsAsync(
        string title,
        string language,
        string? year)
    {
        var encodedTitle = Uri.EscapeDataString(title);
        var url =
            "https://api.themoviedb.org/3/search/movie" +
            $"?api_key={_tmdbApiKey}" +
            $"&query={encodedTitle}" +
            $"&language={language}" +
            (string.IsNullOrWhiteSpace(year)
                ? ""
                : $"&year={Uri.EscapeDataString(year)}");

        using var response = await _httpClient.GetAsync(url);

        if (!response.IsSuccessStatusCode)
        {
            Console.WriteLine(
                $"TMDB returned {(int)response.StatusCode}");
            return new List<TmdbMovie>();
        }

        var json = await response.Content.ReadAsStringAsync();
        var data = JsonSerializer.Deserialize<TmdbResponse>(json, _jsonOptions);

        return data?.Results ?? new List<TmdbMovie>();
    }

    private static TmdbMovie? FindBestTitleMatch(
        string requestedTitle,
        string? requestedYear,
        IEnumerable<TmdbMovie> candidates)
    {
        var requestedWords = GetTitleWords(requestedTitle);

        if (requestedWords.Count == 0)
            return null;

        if (!string.IsNullOrWhiteSpace(requestedYear))
        {
            candidates = candidates.Where(candidate =>
                candidate.ReleaseDate?.StartsWith(
                    requestedYear,
                    StringComparison.Ordinal) == true);
        }

        var bestMatch = candidates
            .Select(candidate =>
            {
                var localizedTitleScore = CalculateTitleSimilarity(
                    requestedWords,
                    candidate.Title);
                var originalTitleScore = CalculateTitleSimilarity(
                    requestedWords,
                    candidate.OriginalTitle);

                return new
                {
                    Movie = candidate,
                    Score = Math.Max(localizedTitleScore, originalTitleScore),
                    MatchedTitle = localizedTitleScore >= originalTitleScore
                        ? candidate.Title ?? candidate.OriginalTitle
                        : candidate.OriginalTitle ?? candidate.Title
                };
            })
            .OrderByDescending(match => match.Score)
            .ThenByDescending(match => match.Movie.Popularity)
            .FirstOrDefault();

        if (bestMatch == null || bestMatch.Score < 0.75)
            return null;

        bestMatch.Movie.Title = bestMatch.MatchedTitle;
        return bestMatch.Movie;
    }

    private static (string Title, string? Year) ParseTrailingYear(string title)
    {
        var trimmedTitle = title.Trim();
        var match = Regex.Match(
            trimmedTitle,
            @"^(?<title>.+?)\s+(?:\((?<year>\d{4})\)|(?<yearWithoutParentheses>\d{4}))$");

        if (!match.Success)
            return (trimmedTitle, null);

        var year = match.Groups["year"].Success
            ? match.Groups["year"].Value
            : match.Groups["yearWithoutParentheses"].Value;

        var cleanTitle = Regex.Replace(
            match.Groups["title"].Value,
            @"[\s\-,:]+$",
            "").Trim();

        return (cleanTitle, year);
    }

    private static double CalculateTitleSimilarity(
        HashSet<string> requestedWords,
        string? candidateTitle)
    {
        var candidateWords = GetTitleWords(candidateTitle);

        if (candidateWords.Count == 0)
            return 0;

        var sharedWordCount = requestedWords.Intersect(candidateWords).Count();

        if (sharedWordCount == 0)
            return 0;

        var precision = (double)sharedWordCount / candidateWords.Count;
        var recall = (double)sharedWordCount / requestedWords.Count;

        return 2 * precision * recall / (precision + recall);
    }

    private static HashSet<string> GetTitleWords(string? title)
    {
        var words = new HashSet<string>(StringComparer.Ordinal);

        if (string.IsNullOrWhiteSpace(title))
            return words;

        var normalizedTitle = title.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalizedTitle.Length);

        foreach (var character in normalizedTitle)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) ==
                UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            builder.Append(
                char.IsLetterOrDigit(character)
                    ? char.ToLowerInvariant(character)
                    : ' ');
        }

        foreach (var word in builder.ToString().Split(
                     ' ',
                     StringSplitOptions.RemoveEmptyEntries))
        {
            words.Add(word);
        }

        return words;
    }

    private async Task<string> GetDirectorAsync(int movieId)
    {
        try
        {
            var url =
                $"https://api.themoviedb.org/3/movie/{movieId}/credits" +
                $"?api_key={_tmdbApiKey}" +
                "&language=pt-BR";

            using var response =
                await _httpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode)
                return "";

            var json =
                await response.Content.ReadAsStringAsync();

            var data =
                JsonSerializer.Deserialize<TmdbCreditsResponse>(
                    json,
                    _jsonOptions);

            if (data?.Crew == null)
                return "";

            var director =
                data.Crew.FirstOrDefault(person =>
                    string.Equals(
                        person.Job,
                        "Director",
                        StringComparison.OrdinalIgnoreCase));

            return director?.Name ?? "";
        }
        catch
        {
            return "";
        }
    }

    private async Task AddToJsonAsync(
        Movie movie,
        string jsonPath)
    {
        Dictionary<string, List<Movie>> moviesByYear;

        var existingJson =
            await File.ReadAllTextAsync(jsonPath);

        if (string.IsNullOrWhiteSpace(existingJson))
        {
            moviesByYear =
                new Dictionary<string, List<Movie>>();
        }
        else
        {
            moviesByYear =
                JsonSerializer.Deserialize<
                    Dictionary<string, List<Movie>>>(
                    existingJson,
                    _jsonOptions)
                ?? new Dictionary<string, List<Movie>>();
        }

        var year =
            !string.IsNullOrWhiteSpace(movie.DateWatched) &&
            movie.DateWatched.Length >= 4
                ? movie.DateWatched.Substring(0, 4)
                : "Sem Data";

        if (!moviesByYear.ContainsKey(year))
        {
            moviesByYear[year] =
                new List<Movie>();
        }

        moviesByYear[year].Add(movie);

        foreach (var group in moviesByYear)
        {
            group.Value.Sort((a, b) =>
                string.Compare(
                    b.DateWatched,
                    a.DateWatched,
                    StringComparison.Ordinal));
        }

        var output =
            JsonSerializer.Serialize(
                moviesByYear,
                _jsonOptions);

        await File.WriteAllTextAsync(
            jsonPath,
            output);

        Console.WriteLine(
            $"Movie saved to {jsonPath}");
    }

    private static string CreateSlug(string title)
    {
        var chars = title
            .ToLowerInvariant()
            .Select(c =>
                char.IsLetterOrDigit(c)
                    ? c
                    : '-')
            .ToArray();

        var slug = new string(chars);

        while (slug.Contains("--"))
        {
            slug = slug.Replace("--", "-");
        }

        return slug.Trim('-');
    }

    private class TmdbResponse
    {
        public List<TmdbMovie> Results { get; set; } = new();
    }

    private class TmdbMovie
    {
        public int Id { get; set; }

        public string? Title { get; set; }

        [JsonPropertyName("original_title")]
        public string? OriginalTitle { get; set; }

        public double Popularity { get; set; }

        [JsonPropertyName("poster_path")]
        public string? PosterPath { get; set; }

        [JsonPropertyName("release_date")]
        public string? ReleaseDate { get; set; }
    }

    private class TmdbCreditsResponse
    {
        public List<TmdbCrewMember> Crew { get; set; } = new();
    }

    private class TmdbCrewMember
    {
        public string? Name { get; set; }

        public string? Job { get; set; }
    }
}
