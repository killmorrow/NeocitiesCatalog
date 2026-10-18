using System.Text.Json.Serialization;
namespace MediaManager.Models;

public class Anime
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public string Episodes { get; set; } = "";
    public double Rating { get; set; }
    public string DateWatched { get; set; } = "";

    public string AnimeType { get; set; } = "";
    public bool Manga { get; set; }
    public string Review { get; set; } = "";
    public string Poster { get; set; } = "";
}