using System.Collections.Generic;

namespace MediaManager.Models;

public class Game
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public string Link { get; set; } = "";
    public string Hours { get; set; } = "";
    public double Rating { get; set; }
    public string DatePlayed { get; set; } = "";
    public bool Owned { get; set; }
    public string Review { get; set; } = "";
    public string Poster { get; set; } = "";
}