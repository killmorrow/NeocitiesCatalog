using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using MediaManager.Models;

namespace MediaManager.Services;

public class AnimeService
{
    private readonly HttpClient _httpClient;

    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public AnimeService()
    {
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("MediaManager/1.0");
        // Kitsu aceita JSON padrão da API deles
        _httpClient.DefaultRequestHeaders.Accept.Add(
            new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/vnd.api+json"));
    }

    public async Task<Anime?> CreateAnimeAsync(
        string title,
        double ratingValue,
        bool manga,
        string animeType,
        string dateWatched,
        string review,
        string jsonPath)
    {
        var animeInfo = await SearchKitsuAsync(title);

        if (animeInfo == null)
            return null;

        var anime = new Anime
        {
            Id = CreateSlug(title),
            Title = title,
            Episodes = animeInfo.Episodes,
            Rating = ratingValue,
            DateWatched = dateWatched,
            AnimeType = animeType,
            Manga = manga,
            Review = review,
            Poster = animeInfo.Poster
        };

        await AddToJsonAsync(anime, jsonPath);

        return anime;
    }

    private async Task<KitsuAnimeInfo?> SearchKitsuAsync(string title)
    {
        try
        {
            var encodedTitle = Uri.EscapeDataString(title);
            // URL de busca da Kitsu API v1
            var url = $"https://kitsu.io/api/edge/anime?filter[text]={encodedTitle}&page[limit]=1";

            Console.WriteLine();
            Console.WriteLine("=== KITSU API ===");
            Console.WriteLine($"Searching: {title}");
            Console.WriteLine($"URL: {url}");

            using var response = await _httpClient.GetAsync(url);

            Console.WriteLine($"Status: {(int)response.StatusCode} {response.StatusCode}");

            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine($"Kitsu returned {(int)response.StatusCode}");
                return null;
            }

            var json = await response.Content.ReadAsStringAsync();

            var data = JsonSerializer.Deserialize<KitsuResponse>(json, _jsonOptions);

            if (data?.Data == null || data.Data.Count == 0)
            {
                Console.WriteLine("Kitsu returned zero results.");
                return null;
            }

            var found = data.Data[0];
            var attributes = found.Attributes;

            var poster = attributes?.PosterImage?.Large 
                         ?? attributes?.PosterImage?.Original 
                         ?? attributes?.PosterImage?.Medium 
                         ?? "";

            var episodes = attributes?.EpisodeCount?.ToString() ?? "";

            Console.WriteLine($"Found: {attributes?.CanonicalTitle}");
            Console.WriteLine($"Episodes: {episodes}");
            Console.WriteLine($"Poster: {poster}");

            return new KitsuAnimeInfo
            {
                Episodes = episodes,
                Poster = poster
            };
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Kitsu ERROR: {ex}");
            return null;
        }
    }

    private async Task AddToJsonAsync(Anime anime, string jsonPath)
    {
        Dictionary<string, List<Anime>> animesByYear;

        if (!File.Exists(jsonPath))
        {
            animesByYear = new Dictionary<string, List<Anime>>();
        }
        else
        {
            var existingJson = await File.ReadAllTextAsync(jsonPath);

            if (string.IsNullOrWhiteSpace(existingJson))
            {
                animesByYear = new Dictionary<string, List<Anime>>();
            }
            else
            {
                animesByYear = JsonSerializer.Deserialize<Dictionary<string, List<Anime>>>(existingJson, _jsonOptions)
                               ?? new Dictionary<string, List<Anime>>();
            }
        }

        var year = !string.IsNullOrWhiteSpace(anime.DateWatched) && anime.DateWatched.Length >= 4
            ? anime.DateWatched.Substring(0, 4)
            : "Sem Data";

        if (!animesByYear.ContainsKey(year))
        {
            animesByYear[year] = new List<Anime>();
        }

        animesByYear[year].Add(anime);

        foreach (var group in animesByYear)
        {
            group.Value.Sort((a, b) => string.Compare(b.DateWatched, a.DateWatched, StringComparison.Ordinal));
        }

        var output = JsonSerializer.Serialize(animesByYear, _jsonOptions);
        await File.WriteAllTextAsync(jsonPath, output);

        Console.WriteLine($"Anime saved to {jsonPath}");
    }

    private static string CreateSlug(string title)
    {
        var chars = title
            .ToLowerInvariant()
            .Select(c => char.IsLetterOrDigit(c) ? c : '-')
            .ToArray();

        var slug = new string(chars);

        while (slug.Contains("--"))
        {
            slug = slug.Replace("--", "-");
        }

        return slug.Trim('-');
    }

    // Classes de mapeamento para o JSON da Kitsu API
    private class KitsuResponse
    {
        [JsonPropertyName("data")]
        public List<KitsuItem> Data { get; set; } = new();
    }

    private class KitsuItem
    {
        [JsonPropertyName("attributes")]
        public KitsuAttributes? Attributes { get; set; }
    }

    private class KitsuAttributes
    {
        [JsonPropertyName("canonicalTitle")]
        public string? CanonicalTitle { get; set; }

        [JsonPropertyName("episodeCount")]
        public int? EpisodeCount { get; set; }

        [JsonPropertyName("posterImage")]
        public KitsuPosterImage? PosterImage { get; set; }
    }

    private class KitsuPosterImage
    {
        [JsonPropertyName("tiny")]
        public string? Tiny { get; set; }

        [JsonPropertyName("small")]
        public string? Small { get; set; }

        [JsonPropertyName("medium")]
        public string? Medium { get; set; }

        [JsonPropertyName("large")]
        public string? Large { get; set; }

        [JsonPropertyName("original")]
        public string? Original { get; set; }
    }

    private class KitsuAnimeInfo
    {
        public string Episodes { get; set; } = "";
        public string Poster { get; set; } = "";
    }
}