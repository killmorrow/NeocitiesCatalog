using System.Collections.Generic;

namespace MediaManager.Models;

public class Book
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public string Author { get; set; } = "";
    public string Year { get; set; } = "";
    public double Rating { get; set; }
    public string DateRead { get; set; } = "";
    public bool Physical { get; set; }

    public List<string> Tags { get; set; } = new();

    public string Review { get; set; } = "";
    public string Cover { get; set; } = "";
}