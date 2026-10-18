using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using MediaManager.Models;
using MediaManager.Storage;

namespace MediaManager.Services;

public class BookService
{
    // ============================================================
    // API KEY
    // ============================================================
    private readonly string _googleBooksApiKey;


    // ============================================================
    // CAMPOS
    // ============================================================

    private readonly HttpClient _http;
    private readonly JsonStorage _storage;


    // ============================================================
    // CONSTRUTOR
    // ============================================================

    public BookService(string googleBooksApiKey)
    {
        _googleBooksApiKey = googleBooksApiKey;
        _http = new HttpClient();

        _storage = new JsonStorage();
    }


    // ============================================================
    // UTILITÁRIOS
    // ============================================================

    private static string NormalizarTexto(string texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
            return "";

        var normalizado = texto
            .ToLowerInvariant()
            .Normalize(NormalizationForm.FormD);

        var builder = new StringBuilder();

        foreach (var caractere in normalizado)
        {
            var categoria =
                CharUnicodeInfo.GetUnicodeCategory(caractere);

            if (categoria == UnicodeCategory.NonSpacingMark)
                continue;

            if (char.IsLetterOrDigit(caractere))
                builder.Append(caractere);
            else
                builder.Append(' ');
        }

        return string.Join(
            " ",
            builder
                .ToString()
                .Split(
                    ' ',
                    StringSplitOptions.RemoveEmptyEntries))
            .Trim();
    }


    private static string Slug(string texto)
    {
        return NormalizarTexto(texto)
            .Replace(" ", "-");
    }


    private static async Task EsperarAsync(int ms)
    {
        await Task.Delay(ms);
    }


    private static double PontuarTitulo(
        string tituloBuscado,
        string tituloEncontrado)
    {
        var busca = NormalizarTexto(tituloBuscado);
        var encontrado = NormalizarTexto(tituloEncontrado);

        if (string.IsNullOrWhiteSpace(busca) ||
            string.IsNullOrWhiteSpace(encontrado))
        {
            return 0;
        }

        if (busca == encontrado)
            return 1;

        var palavrasBusca = busca
            .Split(
                ' ',
                StringSplitOptions.RemoveEmptyEntries);

        var palavrasEncontradas = encontrado
            .Split(
                ' ',
                StringSplitOptions.RemoveEmptyEntries);

        var iguais = 0;

        foreach (var palavra in palavrasBusca)
        {
            if (palavrasEncontradas.Contains(palavra))
                iguais++;
        }

        if (iguais == 0)
            return 0;

        var coberturaBusca =
            (double)iguais / palavrasBusca.Length;

        var coberturaTitulo =
            (double)iguais / palavrasEncontradas.Length;

        return coberturaBusca * 0.7 +
               coberturaTitulo * 0.3;
    }


    // ============================================================
    // DOWNLOAD DA CAPA
    // ============================================================

    private async Task<string> BaixarCapaAsync(
        string url,
        string nomeLivro,
        string pastaCapas)
    {
        if (string.IsNullOrWhiteSpace(url))
            return "";

        try
        {
            Directory.CreateDirectory(pastaCapas);

            var nomeArquivo =
                Slug(nomeLivro) + ".jpg";

            var caminhoArquivo =
                Path.Combine(
                    pastaCapas,
                    nomeArquivo);

            if (File.Exists(caminhoArquivo))
            {
                Console.WriteLine(
                    $"   📁 Capa já existe: covers/{nomeArquivo}");

                return $"covers/{nomeArquivo}";
            }

            // Goodreads costuma entregar thumbnails.
            // Tentamos transformar a URL em uma versão maior.

            var urlsParaTentar =
                new List<string>();

            var urlGrande = url;

urlGrande = Regex.Replace(
    urlGrande,
    @"\._[^.]+_\.",
    ".");

urlGrande = Regex.Replace(
    urlGrande,
    @"_SX\d+_SY\d+_",
    "");

urlGrande = Regex.Replace(
    urlGrande,
    @"_SX\d+_",
    "");

urlGrande = Regex.Replace(
    urlGrande,
    @"_SY\d+_",
    "");

urlsParaTentar.Add(urlGrande);

            // URL original como fallback.
            if (urlGrande != url)
                urlsParaTentar.Add(url);

            foreach (var urlTentativa in urlsParaTentar)
            {
                try
                {
                    Console.WriteLine(
                        $"   🖼️ Tentando capa: {urlTentativa}");

                    using var request =
                        new HttpRequestMessage(
                            HttpMethod.Get,
                            urlTentativa);

                    request.Headers.TryAddWithoutValidation(
                        "User-Agent",
                        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) " +
                        "AppleWebKit/537.36 Chrome/120 Safari/537.36");

                    request.Headers.TryAddWithoutValidation(
                        "Referer",
                        "https://www.goodreads.com/");

                    var resposta =
                        await _http.SendAsync(request);

                    if (!resposta.IsSuccessStatusCode)
                        continue;

                    var buffer =
                        await resposta.Content
                            .ReadAsByteArrayAsync();

                    if (buffer.Length < 5000)
                        continue;

                    await File.WriteAllBytesAsync(
                        caminhoArquivo,
                        buffer);

                    Console.WriteLine(
                        $"   ✅ Capa baixada " +
                        $"({Math.Round(buffer.Length / 1024.0)} KB)");

                    Console.WriteLine(
                        $"   💾 Capa salva: covers/{nomeArquivo}");

                    return $"covers/{nomeArquivo}";
                }
                catch
                {
                    // Tenta a próxima URL.
                }
            }

            Console.WriteLine(
                "   ⚠️ Não foi possível baixar uma capa válida");

            return "";
        }
        catch (Exception error)
        {
            Console.WriteLine(
                $"   ⚠️ Erro ao salvar capa: {error.Message}");

            return "";
        }
    }


    // ============================================================
    // GOODREADS
    // ============================================================

    private async Task<GoodreadsData?> BuscarGoodreadsAsync(
        string nomeLivro)
    {
        try
        {
            var url =
                "https://www.goodreads.com/book/auto_complete" +
                "?format=json" +
                "&q=" +
                Uri.EscapeDataString(nomeLivro);

            using var request =
                new HttpRequestMessage(
                    HttpMethod.Get,
                    url);

            request.Headers.TryAddWithoutValidation(
                "User-Agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) " +
                "AppleWebKit/537.36 Chrome/120 Safari/537.36");

            request.Headers.TryAddWithoutValidation(
                "Accept",
                "application/json");

            var resposta =
                await _http.SendAsync(request);

            if (!resposta.IsSuccessStatusCode)
            {
                Console.WriteLine(
                    $"   ⚠️ Goodreads HTTP {(int)resposta.StatusCode}");

                return null;
            }

            var json =
                await resposta.Content.ReadAsStringAsync();

            using var document =
                JsonDocument.Parse(json);

            if (document.RootElement.ValueKind !=
                JsonValueKind.Array)
            {
                return null;
            }

            GoodreadsData? melhorLivro = null;
            double melhorPontuacao = 0;

            foreach (var livro in
                     document.RootElement.EnumerateArray())
            {
                if (!livro.TryGetProperty(
                        "title",
                        out var titleElement))
                {
                    continue;
                }

                var title =
                    titleElement.GetString() ?? "";

                if (string.IsNullOrWhiteSpace(title))
                    continue;

                var pontuacao =
                    PontuarTitulo(
                        nomeLivro,
                        title);

                if (pontuacao > melhorPontuacao)
                {
                    melhorPontuacao = pontuacao;

                    var author = "";

                    if (livro.TryGetProperty(
                            "author",
                            out var authorElement))
                    {
                        if (authorElement.ValueKind ==
                            JsonValueKind.Object &&
                            authorElement.TryGetProperty(
                                "name",
                                out var authorName))
                        {
                            author =
                                authorName.GetString() ?? "";
                        }
                    }

                    var imageUrl = "";

                    if (livro.TryGetProperty(
                            "imageUrl",
                            out var imageElement))
                    {
                        imageUrl =
                            imageElement.GetString() ?? "";
                    }

                    melhorLivro = new GoodreadsData
                    {
                        Title = title,
                        Author = author,
                        ImageUrl = imageUrl
                    };
                }
            }

            // Não aceita resultados ruins.
            if (melhorLivro == null ||
                melhorPontuacao < 0.70)
            {
                return null;
            }

            Console.WriteLine(
                $"   📖 Goodreads: \"{melhorLivro.Title}\" " +
                $"({Math.Round(melhorPontuacao * 100)}%)");

            return melhorLivro;
        }
        catch (Exception error)
        {
            Console.WriteLine(
                $"   ⚠️ Goodreads falhou: {error.Message}");

            return null;
        }
    }


    // ============================================================
    // GOOGLE BOOKS
    // ============================================================

    private async Task<JsonElement?> ConsultarGoogleAsync(
        string query)
    {
        var url =
            "https://www.googleapis.com/books/v1/volumes" +
            "?q=" +
            Uri.EscapeDataString(query) +
            "&maxResults=40" +
            "&key=" +
            _googleBooksApiKey;

        try
        {
            var resposta =
                await _http.GetAsync(url);

            if (!resposta.IsSuccessStatusCode)
            {
                Console.WriteLine(
                    $"   ⚠️ Google HTTP {(int)resposta.StatusCode}");

                return null;
            }

            var json =
                await resposta.Content.ReadAsStringAsync();

            using var document =
                JsonDocument.Parse(json);

            return document.RootElement.Clone();
        }
        catch (Exception error)
        {
            Console.WriteLine(
                $"   ⚠️ Google falhou: {error.Message}");

            return null;
        }
    }


    private static GoogleData ExtrairDadosGoogle(
        JsonElement livro)
    {
        var info =
            livro.GetProperty("volumeInfo");

        var author = "";

        if (info.TryGetProperty(
                "authors",
                out var authors))
        {
            var nomes =
                authors
                    .EnumerateArray()
                    .Select(
                        x => x.GetString() ?? "")
                    .Where(
                        x => !string.IsNullOrWhiteSpace(x))
                    .ToList();

            author =
                string.Join(", ", nomes);
        }

        var year = "";

        if (info.TryGetProperty(
                "publishedDate",
                out var publishedDateElement))
        {
            var publishedDate =
                publishedDateElement.GetString() ?? "";

            if (publishedDate.Length >= 4)
                year = publishedDate[..4];
        }

        var cover = "";

        if (info.TryGetProperty(
                "imageLinks",
                out var imageLinks))
        {
            if (imageLinks.TryGetProperty(
                    "large",
                    out var large))
            {
                cover =
                    large.GetString() ?? "";
            }

            if (string.IsNullOrWhiteSpace(cover) &&
                imageLinks.TryGetProperty(
                    "medium",
                    out var medium))
            {
                cover =
                    medium.GetString() ?? "";
            }

            if (string.IsNullOrWhiteSpace(cover) &&
                imageLinks.TryGetProperty(
                    "small",
                    out var small))
            {
                cover =
                    small.GetString() ?? "";
            }

            if (string.IsNullOrWhiteSpace(cover) &&
                imageLinks.TryGetProperty(
                    "thumbnail",
                    out var thumbnail))
            {
                cover =
                    thumbnail.GetString() ?? "";
            }
        }

        cover =
            cover.Replace(
                "http:",
                "https:",
                StringComparison.OrdinalIgnoreCase);

        return new GoogleData
        {
            Author = author,
            Year = year,
            Cover = cover
        };
    }


    private async Task<GoogleData?> BuscarGoogleBooksAsync(
        string nomeLivro)
    {
        // IMPORTANTE:
        // sempre usamos o título original do livros.md / GUI.

        var consultas =
            new List<string>
            {
                $"intitle:{nomeLivro}",
                nomeLivro
            };

        var normalizado =
            NormalizarTexto(nomeLivro);

        if (normalizado !=
            nomeLivro.ToLowerInvariant())
        {
            consultas.Add(normalizado);
        }

        var palavrasIgnoradas =
            new HashSet<string>
            {
                "o",
                "a",
                "os",
                "as",
                "um",
                "uma",
                "de",
                "do",
                "da",
                "dos",
                "das",
                "e",
                "em",
                "no",
                "na",
                "nos",
                "nas"
            };

        var palavras =
            normalizado
                .Split(
                    ' ',
                    StringSplitOptions.RemoveEmptyEntries)
                .Where(
                    palavra =>
                        palavra.Length >= 3 &&
                        !palavrasIgnoradas.Contains(palavra))
                .ToList();

        if (palavras.Count >= 2)
        {
            consultas.Add(
                string.Join(
                    " ",
                    palavras.Take(8)));
        }

        var semNao =
            palavras
                .Where(
                    palavra => palavra != "nao")
                .ToList();

        if (semNao.Count >= 2 &&
            string.Join(" ", semNao) !=
            string.Join(" ", palavras))
        {
            consultas.Add(
                string.Join(
                    " ",
                    semNao.Take(8)));
        }

        var consultasUnicas =
            consultas.Distinct().ToList();

        JsonElement? melhorLivro = null;
        double melhorPontuacao = 0;

        foreach (var consulta in consultasUnicas)
        {
            Console.WriteLine(
                $"   🔍 Google: \"{consulta}\"");

            var dados =
                await ConsultarGoogleAsync(consulta);

            if (!dados.HasValue)
                continue;

            if (!dados.Value.TryGetProperty(
                    "items",
                    out var items))
            {
                continue;
            }

            foreach (var livro in
                     items.EnumerateArray())
            {
                if (!livro.TryGetProperty(
                        "volumeInfo",
                        out var info))
                {
                    continue;
                }

                var titulo = "";

                if (info.TryGetProperty(
                        "title",
                        out var tituloElement))
                {
                    titulo =
                        tituloElement.GetString() ?? "";
                }

                if (string.IsNullOrWhiteSpace(titulo))
                    continue;

                var pontuacao =
                    PontuarTitulo(
                        nomeLivro,
                        titulo);

                if (pontuacao >= 0.70 &&
                    pontuacao > melhorPontuacao)
                {
                    melhorPontuacao = pontuacao;
                    melhorLivro = livro.Clone();
                }
            }

            if (melhorPontuacao >= 0.90)
                break;

            await EsperarAsync(250);
        }

        if (!melhorLivro.HasValue ||
            melhorPontuacao < 0.70)
        {
            return null;
        }

        var tituloMelhor =
            melhorLivro.Value
                .GetProperty("volumeInfo")
                .GetProperty("title")
                .GetString() ?? "";

        Console.WriteLine(
            $"   📖 Google: \"{tituloMelhor}\" " +
            $"({Math.Round(melhorPontuacao * 100)}%)");

        return ExtrairDadosGoogle(
            melhorLivro.Value);
    }


    // ============================================================
    // OPEN LIBRARY
    // ============================================================

    private async Task<OpenLibraryData?> BuscarOpenLibraryAsync(
        string nomeLivro)
    {
        var consultas =
            new List<string>
            {
                nomeLivro,
                NormalizarTexto(nomeLivro)
            };

        var palavras =
            NormalizarTexto(nomeLivro)
                .Split(
                    ' ',
                    StringSplitOptions.RemoveEmptyEntries)
                .Where(
                    palavra => palavra.Length >= 3)
                .ToList();

        if (palavras.Count >= 2)
        {
            consultas.Add(
                string.Join(
                    " ",
                    palavras.Take(8)));
        }

        foreach (var consulta in consultas.Distinct())
        {
            try
            {
                var url =
                    "https://openlibrary.org/search.json" +
                    "?q=" +
                    Uri.EscapeDataString(consulta) +
                    "&limit=20";

                var resposta =
                    await _http.GetAsync(url);

                if (!resposta.IsSuccessStatusCode)
                    continue;

                var json =
                    await resposta.Content.ReadAsStringAsync();

                using var document =
                    JsonDocument.Parse(json);

                if (!document.RootElement.TryGetProperty(
                        "docs",
                        out var docs))
                {
                    continue;
                }

                JsonElement? melhorLivro = null;
                double melhorPontuacao = 0;

                foreach (var livro in
                         docs.EnumerateArray())
                {
                    if (!livro.TryGetProperty(
                            "title",
                            out var titleElement))
                    {
                        continue;
                    }

                    var title =
                        titleElement.GetString() ?? "";

                    if (string.IsNullOrWhiteSpace(title))
                        continue;

                    var pontuacao =
                        PontuarTitulo(
                            nomeLivro,
                            title);

                    if (pontuacao >= 0.70 &&
                        pontuacao > melhorPontuacao)
                    {
                        melhorPontuacao = pontuacao;
                        melhorLivro = livro.Clone();
                    }
                }

                if (melhorLivro.HasValue &&
                    melhorPontuacao >= 0.70)
                {
                    var tituloMelhor = "";

                    if (melhorLivro.Value.TryGetProperty(
                            "title",
                            out var tituloElement))
                    {
                        tituloMelhor =
                            tituloElement.GetString() ?? "";
                    }

                    Console.WriteLine(
                        $"   📖 Open Library: \"{tituloMelhor}\" " +
                        $"({Math.Round(melhorPontuacao * 100)}%)");

                    return ExtrairDadosOpenLibrary(
                        melhorLivro.Value);
                }
            }
            catch
            {
                // Continua tentando outras consultas.
            }

            await EsperarAsync(250);
        }

        return null;
    }


    private static OpenLibraryData ExtrairDadosOpenLibrary(
        JsonElement livro)
    {
        var author = "";

        if (livro.TryGetProperty(
                "author_name",
                out var authors))
        {
            var nomes =
                authors
                    .EnumerateArray()
                    .Select(
                        x => x.GetString() ?? "")
                    .Where(
                        x => !string.IsNullOrWhiteSpace(x))
                    .ToList();

            author =
                string.Join(", ", nomes);
        }

        var year = "";

        if (livro.TryGetProperty(
                "first_publish_year",
                out var firstPublishYear))
        {
            year =
                firstPublishYear.ToString();
        }

        var cover = "";

        if (livro.TryGetProperty(
                "cover_i",
                out var coverId))
        {
            var id =
                coverId.ToString();

            if (!string.IsNullOrWhiteSpace(id))
            {
                cover =
                    "https://covers.openlibrary.org/b/id/" +
                    id +
                    "-L.jpg";
            }
        }

        return new OpenLibraryData
        {
            Author = author,
            Year = year,
            Cover = cover
        };
    }


    // ============================================================
    // BUSCA PRINCIPAL
    // ============================================================

    private async Task<BookApiData> BuscarLivroAsync(
        string nomeLivro)
    {
        Console.WriteLine("");
        Console.WriteLine(
            $"🔎 Buscando: {nomeLivro}");


        // --------------------------------------------------------
        // GOODREADS
        // --------------------------------------------------------

        var goodreads =
            await BuscarGoodreadsAsync(nomeLivro);

        if (goodreads != null)
        {
            Console.WriteLine(
                "   ✅ Goodreads");


            // ----------------------------------------------------
            // GOOGLE
            // ----------------------------------------------------

            var google =
                await BuscarGoogleBooksAsync(nomeLivro);

            if (google != null)
            {
                return new BookApiData
                {
                    Author =
                        !string.IsNullOrWhiteSpace(
                            goodreads.Author)
                            ? goodreads.Author
                            : google.Author,

                    Year =
                        google.Year,

                    Cover =
                        google.Cover
                };
            }


            // ----------------------------------------------------
            // OPEN LIBRARY
            // ----------------------------------------------------

            Console.WriteLine(
                "   🔄 Google não encontrou. " +
                "Tentando Open Library...");

            var openLibrary =
                await BuscarOpenLibraryAsync(nomeLivro);

            if (openLibrary != null)
            {
                return new BookApiData
                {
                    Author =
                        !string.IsNullOrWhiteSpace(
                            goodreads.Author)
                            ? goodreads.Author
                            : openLibrary.Author,

                    Year =
                        openLibrary.Year,

                    Cover =
                        !string.IsNullOrWhiteSpace(
                            openLibrary.Cover)
                            ? openLibrary.Cover
                            : goodreads.ImageUrl
                };
            }


            // ----------------------------------------------------
            // SÓ GOODREADS
            // ----------------------------------------------------

            return new BookApiData
            {
                Author =
                    goodreads.Author,

                Year =
                    "",

                Cover =
                    goodreads.ImageUrl
            };
        }


        // --------------------------------------------------------
        // GOOGLE DIRETO
        // --------------------------------------------------------

        var googleDireto =
            await BuscarGoogleBooksAsync(nomeLivro);

        if (googleDireto != null)
        {
            return new BookApiData
            {
                Author =
                    googleDireto.Author,

                Year =
                    googleDireto.Year,

                Cover =
                    googleDireto.Cover
            };
        }


        // --------------------------------------------------------
        // OPEN LIBRARY
        // --------------------------------------------------------

        Console.WriteLine(
            "   🔄 Tentando Open Library...");

        var openLibraryDireto =
            await BuscarOpenLibraryAsync(nomeLivro);

        if (openLibraryDireto != null)
        {
            return new BookApiData
            {
                Author =
                    openLibraryDireto.Author,

                Year =
                    openLibraryDireto.Year,

                Cover =
                    openLibraryDireto.Cover
            };
        }


        // --------------------------------------------------------
        // NADA ENCONTRADO
        // --------------------------------------------------------

        Console.WriteLine(
            "   ❌ Nenhum resultado confiável encontrado");

        return new BookApiData
        {
            Author = "",
            Year = "",
            Cover = ""
        };
    }


    // ============================================================
    // CREATE BOOK
    // ============================================================

    public async Task<Book?> CreateBookAsync(
    string title,
    List<string> tags,
    double rating,
    bool physical,
    string dateRead,
    string review,
    string jsonPath,
    string coversDirectory)
{
    var dadosLivro = await BuscarLivroAsync(title);

    var cover = "";

    if (!string.IsNullOrWhiteSpace(dadosLivro.Cover))
    {
        cover = await BaixarCapaAsync(
            dadosLivro.Cover,
            title,
            coversDirectory);
    }

    var livro = new Book
    {
        Id = Slug(title),
        Title = title.Trim(),
        Author = dadosLivro.Author,
        Year = dadosLivro.Year,
        Rating = rating,
        DateRead = dateRead,
        Physical = physical,
        Tags = tags,
        Review = review,
        Cover = cover
    };

    // ============================================================
    // LÊ O BOOKS.JSON EXISTENTE
    // ============================================================

    if (!File.Exists(jsonPath))
    {
        throw new FileNotFoundException(
            $"books.json not found:\n{jsonPath}");
    }

    var json = await File.ReadAllTextAsync(jsonPath);

    Dictionary<string, List<Book>> livrosPorAno;

    if (string.IsNullOrWhiteSpace(json))
    {
        livrosPorAno = new Dictionary<string, List<Book>>();
    }
    else
    {
        livrosPorAno =
            JsonSerializer.Deserialize<
                Dictionary<string, List<Book>>>(
                json,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                })
            ?? new Dictionary<string, List<Book>>();
    }

    // ============================================================
    // DESCOBRE O ANO DA LEITURA
    // ============================================================

    var anoLeitura =
        !string.IsNullOrWhiteSpace(livro.DateRead) &&
        livro.DateRead.Length >= 4
            ? livro.DateRead[..4]
            : "Sem Data";

    if (!livrosPorAno.TryGetValue(
            anoLeitura,
            out var lista))
    {
        lista = new List<Book>();
        livrosPorAno[anoLeitura] = lista;
    }

    // ============================================================
    // ADICIONA O LIVRO
    // ============================================================

    lista.Add(livro);

    // ============================================================
    // ORDENA
    // ============================================================

    foreach (var grupo in livrosPorAno.Values)
    {
        grupo.Sort((a, b) =>
            string.Compare(
                b.DateRead,
                a.DateRead,
                StringComparison.Ordinal));
    }

    // ============================================================
    // GERA O NOVO JSON
    // ============================================================

    var options = new JsonSerializerOptions
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    var novoJson = JsonSerializer.Serialize(
        livrosPorAno,
        options);

    // ============================================================
    // SOBRESCREVE EXATAMENTE O ARQUIVO INFORMADO
    // ============================================================

    await File.WriteAllTextAsync(
        jsonPath,
        novoJson,
        Encoding.UTF8);

    return livro;
}


    // ============================================================
    // CLASSES AUXILIARES
    // ============================================================

    private class GoodreadsData
    {
        public string Title { get; set; } = "";
        public string Author { get; set; } = "";
        public string ImageUrl { get; set; } = "";
    }


    private class GoogleData
    {
        public string Author { get; set; } = "";
        public string Year { get; set; } = "";
        public string Cover { get; set; } = "";
    }


    private class OpenLibraryData
    {
        public string Author { get; set; } = "";
        public string Year { get; set; } = "";
        public string Cover { get; set; } = "";
    }


    private class BookApiData
    {
        public string Author { get; set; } = "";
        public string Year { get; set; } = "";
        public string Cover { get; set; } = "";
    }
}
