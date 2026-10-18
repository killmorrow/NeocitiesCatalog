using System.Text.Json.Serialization;
namespace MediaManager.Models;

public class Movie
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public string Year { get; set; } = "";
    public double Rating { get; set; }
    public string DateWatched { get; set; } = "";
    public bool Cinema { get; set; }
    public string Review { get; set; } = "";
    public string Poster { get; set; } = "";

    [JsonIgnore]
    public string Director { get; set; } = "";
}