namespace MediaManager.Models;

public class Configuration
{
    public string BooksJsonPath { get; set; } = "";
    public string MoviesJsonPath { get; set; } = "";
    public string GamesJsonPath { get; set; } = "";
    public string AnimesJsonPath { get; set; } = "";

    public string GoogleBooksApiKey { get; set; } = "";
    public string TmdbApiKey { get; set; } = "";
    public string RawgApiKey { get; set; } = "";
}
