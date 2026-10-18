using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Avalonia.Controls;
using Avalonia.Interactivity;
using MediaManager.Services;
using System.Text.Json;
using MediaManager.Models;

namespace MediaManager;

public partial class MainWindow : Window
{
    private BookService _bookService;
    private GameService _gameService;
    private MovieService _movieService;
    private readonly AnimeService _animeService;
    private Configuration _configuration = new();


    private string GamesJsonPath =
        @"C:\Users\T-GAMER\Desktop\neocities-killmorrow\logs\jogos\games.json";

    private string MoviesJsonPath =
        @"C:\Users\T-GAMER\Desktop\neocities-killmorrow\logs\filmes\movies.json";

    private string AnimesJsonPath =
        @"C:\Users\T-GAMER\Desktop\neocities-killmorrow\logs\animes\animes.json";
    private string BooksJsonPath =
        @"C:\Users\T-GAMER\Desktop\neocities-killmorrow\logs\livros\books.json";

    private string ConfigurationFile = "configuration.json";


    private readonly string _coversDirectory;


    public MainWindow()
    {
        InitializeComponent();

        _loadConfiguration();

        _bookService = new BookService(_configuration.GoogleBooksApiKey);
        _gameService = new GameService(_configuration.RawgApiKey);
        _movieService = new MovieService(_configuration.TmdbApiKey);
        _animeService = new AnimeService();

        var jsonDirectory = Path.GetDirectoryName(
            Path.GetFullPath(BooksJsonPath));

        _coversDirectory = Path.Combine(
            jsonDirectory ?? AppContext.BaseDirectory,
            "covers");
    }

        private void _loadConfiguration()
{
    if (!File.Exists(ConfigurationFile))
        return;

    try
    {
        var json = File.ReadAllText(ConfigurationFile);

        var configuration =
            JsonSerializer.Deserialize<Configuration>(json);

        if (configuration == null)
            return;

        _configuration = configuration;

        if (!string.IsNullOrWhiteSpace(configuration.BooksJsonPath))
            BooksJsonPath = configuration.BooksJsonPath;

        if (!string.IsNullOrWhiteSpace(configuration.MoviesJsonPath))
            MoviesJsonPath = configuration.MoviesJsonPath;

        if (!string.IsNullOrWhiteSpace(configuration.GamesJsonPath))
            GamesJsonPath = configuration.GamesJsonPath;

        if (!string.IsNullOrWhiteSpace(configuration.AnimesJsonPath))
            AnimesJsonPath = configuration.AnimesJsonPath;
    }
    catch
    {
        // Mantém os caminhos padrão caso a configuração seja inválida.
    }
}


    private void Books_Click(object? sender, RoutedEventArgs e)
    {
        MainMenu.IsVisible = false;
        BooksForm.IsVisible = true;

        BookStatus.Text = "";
    }

    private void Games_Click(object? sender, RoutedEventArgs e)
    {
        MainMenu.IsVisible = false;
        GamesForm.IsVisible = true;

        GameStatus.Text = "";
    }

    private void Movies_Click(object? sender, RoutedEventArgs e)
    {
        MainMenu.IsVisible = false;
        MoviesForm.IsVisible = true;

        MovieStatus.Text = "";
    }

    private void Animes_Click(object? sender, RoutedEventArgs e)
    {
        MainMenu.IsVisible = false;
        AnimesForm.IsVisible = true;

        AnimeStatus.Text = "";
    }

    private void Configuration_Click(object? sender, RoutedEventArgs e)
    {
        MainMenu.IsVisible = false;
        ConfigurationForm.IsVisible = true;

        LoadConfigurationFields();
    }

    private void Back_Click(object? sender, RoutedEventArgs e)
    {
        BooksForm.IsVisible = false;
        GamesForm.IsVisible = false;
        MoviesForm.IsVisible = false;
        AnimesForm.IsVisible = false;
        ConfigurationForm.IsVisible = false;
        MainMenu.IsVisible = true;
    }


    private async void GenerateBook_Click(
        object? sender,
        RoutedEventArgs e)
    {
        BookStatus.Text = "";

        if (string.IsNullOrWhiteSpace(BookTitle.Text))
        {
            BookStatus.Text = "Please enter a title.";
            return;
        }


        if (!double.TryParse(
                BookRating.Text,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var rating))
        {
            BookStatus.Text = "Invalid rating.";
            return;
        }


        if (rating < 0 || rating > 5)
        {
            BookStatus.Text = "Rating must be between 0 and 5.";
            return;
        }


        var tags = new List<string>();

        if (!string.IsNullOrWhiteSpace(BookTags.Text))
        {
            foreach (var tag in BookTags.Text.Split(','))
            {
                var cleanTag = tag.Trim();

                if (!string.IsNullOrWhiteSpace(cleanTag))
                    tags.Add(cleanTag);
            }
        }


        var dateRead = BookDate.SelectedDate?
            .Date
            .ToString("yyyy-MM-dd") ?? "";


        if (!File.Exists(BooksJsonPath))
        {
            BookStatus.Text =
                $"books.json not found:\n{BooksJsonPath}";

            return;
        }


        try
        {
            BookStatus.Text = "Searching for book...";


            var book = await _bookService.CreateBookAsync(
                BookTitle.Text,
                tags,
                rating,
                BookPhysical.IsChecked == true,
                dateRead,
                BookReview.Text ?? "",
                BooksJsonPath,
                _coversDirectory);


            if (book == null)
            {
                BookStatus.Text =
                    "Book not found in the APIs.";

                return;
            }


            BookStatus.Text =
                $"Book added!\n\n" +
                $"{book.Title}\n" +
                $"{book.Author}\n" +
                $"{book.Year}";


            ClearBookForm();
        }
        catch (Exception ex)
        {
            BookStatus.Text =
                $"Error:\n{ex.Message}";
        }
    }

        private void ClearBookForm()
    {
        BookTitle.Text = "";
        BookTags.Text = "";
        BookRating.Text = "";
        BookPhysical.IsChecked = false;
        BookDate.SelectedDate = null;
        BookReview.Text = "";
    }


    private async void GenerateGame_Click(
        object? sender,
        RoutedEventArgs e)
    {
        GameStatus.Text = "";

        if (string.IsNullOrWhiteSpace(GameTitle.Text))
        {
            GameStatus.Text = "Please enter a title.";
            return;
        }


        if (!double.TryParse(
                GameRating.Text,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var rating))
        {
            GameStatus.Text = "Invalid rating.";
            return;
        }

        if (string.IsNullOrWhiteSpace(GameHours.Text))
        {
            GameStatus.Text = "Please enter the hours played.";
            return;
        }


        if (rating < 0 || rating > 5)
        {
            GameStatus.Text = "Rating must be between 0 and 5.";
            return;
        }


        var datePlayed = GameDate.SelectedDate?
            .Date
            .ToString("yyyy-MM-dd") ?? "";


        if (!File.Exists(GamesJsonPath))
        {
            GameStatus.Text =
                $"games.json not found:\n{GamesJsonPath}";

            return;
        }


        try
        {
            GameStatus.Text = "Searching for game...";

            var game = await _gameService.CreateGameAsync(
                GameTitle.Text,
                GameHours.Text,
                rating,
                OwnedGame.IsChecked == true,
                datePlayed,
                GameReview.Text ?? "",
                GamesJsonPath);


            if (game == null)
            {
                GameStatus.Text =
                    "Game not found in the APIs.";

                return;
            }

            GameStatus.Text =
                $"Game added!\n\n" +
                $"{game.Title}\n" +
                $"{game.Link}";

            ClearGameForm();
        }
        catch (Exception ex)
        {
            GameStatus.Text =
                $"Error:\n{ex.Message}";
        }
    }
    private void ClearGameForm()
    {
        GameTitle.Text = "";
        GameRating.Text = "";
        GameHours.Text = "";
        OwnedGame.IsChecked = false;
        GameDate.SelectedDate = null;
        GameReview.Text = "";
    }


    private async void GenerateMovie_Click(
        object? sender,
        RoutedEventArgs e)
    {
        GameStatus.Text = "";

        if (string.IsNullOrWhiteSpace(MovieTitle.Text))
        {
            MovieStatus.Text = "Please enter a title.";
            return;
        }


        if (!double.TryParse(
                MovieRating.Text,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var rating))
        {
            MovieStatus.Text = "Invalid rating.";
            return;
        }


        if (rating < 0 || rating > 5)
        {
            MovieStatus.Text = "Rating must be between 0 and 5.";
            return;
        }


        var dateWatched = MovieDate.SelectedDate?
            .Date
            .ToString("yyyy-MM-dd") ?? "";


        if (!File.Exists(MoviesJsonPath))
        {
            MovieStatus.Text =
                $"movies.json not found:\n{MoviesJsonPath}";

            return;
        }


        try
        {
            MovieStatus.Text = "Searching for movie...";

            var movie = await _movieService.CreateMovieAsync(
                MovieTitle.Text,
                rating,
                Cinema.IsChecked == true,
                dateWatched,
                MovieReview.Text ?? "",
                MoviesJsonPath);


            if (movie == null)
            {
                MovieStatus.Text =
                    "Movie not found in the APIs.";

                return;
            }

            MovieStatus.Text =
                $"Movie added!\n\n" +
                $"{movie.Title}\n" +
                $"{movie.Director}";

            ClearMovieForm();
        }
        catch (Exception ex)
        {
            MovieStatus.Text =
                $"Error:\n{ex.Message}";
        }
    }


    private void ClearMovieForm()
    {
        MovieTitle.Text = "";
        MovieRating.Text = "";
        Cinema.IsChecked = false;
        MovieDate.SelectedDate = null;
        MovieReview.Text = "";
    }


    private async void GenerateAnime_Click(
        object? sender,
        RoutedEventArgs e)
    {
        AnimeStatus.Text = "";

        if (string.IsNullOrWhiteSpace(AnimeTitle.Text))
        {
            AnimeStatus.Text = "Please enter a title.";
            return;
        }


        if (!double.TryParse(
                AnimeRating.Text,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var rating))
        {
            AnimeStatus.Text = "Invalid rating.";
            return;
        }


        if (rating < 0 || rating > 5)
        {
            AnimeStatus.Text = "Rating must be between 0 and 5.";
            return;
        }


        var dateWatched = AnimeDate.SelectedDate?
            .Date
            .ToString("yyyy-MM-dd") ?? "";


        if (!File.Exists(AnimesJsonPath))
        {
            AnimeStatus.Text =
                $"animes.json not found:\n{AnimesJsonPath}";

            return;
        }


        try
        {
            AnimeStatus.Text = "Searching for anime...";

            var anime = await _animeService.CreateAnimeAsync(
                AnimeTitle.Text,
                rating,
                Manga.IsChecked == true,
                Dubbed.IsChecked == true ? "Dubbed" : "Subbed",
                dateWatched,
                AnimeReview.Text ?? "",
                AnimesJsonPath);


            if (anime == null)
            {
                AnimeStatus.Text =
                    "Anime not found in the APIs.";

                return;
            }

            AnimeStatus.Text =
                $"Anime added!\n\n" +
                $"{anime.Title}\n" +
                $"{anime.Episodes}";

            ClearAnimeForm();
        }
        catch (Exception ex)
        {
            AnimeStatus.Text =
                $"Error:\n{ex.Message}";
        }
    }

    private void ClearAnimeForm()
    {
        AnimeTitle.Text = "";
        AnimeRating.Text = "";
        Manga.IsChecked = false;
        AnimeDate.SelectedDate = null;
        AnimeReview.Text = "";
        Dubbed.IsChecked = false;
        Subbed.IsChecked = false;
    }

    
    private void ConfigurationSave_Click(
    object? sender,
    RoutedEventArgs e)
{
    BooksJsonPath = ConfigurationBooksJsonPath.Text ?? "";
    MoviesJsonPath = ConfigurationMoviesJsonPath.Text ?? "";
    GamesJsonPath = ConfigurationGamesJsonPath.Text ?? "";
    AnimesJsonPath = ConfigurationAnimesJsonPath.Text ?? "";

    _configuration.GoogleBooksApiKey = ConfigurationGoogleBooksApiKey.Text ?? "";
    _configuration.TmdbApiKey = ConfigurationTmdbApiKey.Text ?? "";
    _configuration.RawgApiKey = ConfigurationRawgApiKey.Text ?? "";

    _configuration.BooksJsonPath = BooksJsonPath;
    _configuration.MoviesJsonPath = MoviesJsonPath;
    _configuration.GamesJsonPath = GamesJsonPath;
    _configuration.AnimesJsonPath = AnimesJsonPath;

    var json = JsonSerializer.Serialize(
        _configuration,
        new JsonSerializerOptions
        {
            WriteIndented = true
        });

    File.WriteAllText(ConfigurationFile, json);

    _bookService = new BookService(_configuration.GoogleBooksApiKey);
    _gameService = new GameService(_configuration.RawgApiKey);
    _movieService = new MovieService(_configuration.TmdbApiKey);

    ConfigurationStatus.Text = "Configuration saved!";
}

private void LoadConfigurationFields()
{
    ConfigurationBooksJsonPath.Text = BooksJsonPath;
    ConfigurationMoviesJsonPath.Text = MoviesJsonPath;
    ConfigurationGamesJsonPath.Text = GamesJsonPath;
    ConfigurationAnimesJsonPath.Text = AnimesJsonPath;
    ConfigurationGoogleBooksApiKey.Text = _configuration.GoogleBooksApiKey;
    ConfigurationTmdbApiKey.Text = _configuration.TmdbApiKey;
    ConfigurationRawgApiKey.Text = _configuration.RawgApiKey;
}

}
