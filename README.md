# Neocities Catalog

A personal media library manager built with **C# and Avalonia UI** to organize books, movies, games, and anime in one place.

The application runs locally on your computer and stores its data in JSON files, which can be used to display your collection on a personal website.

You'll get a json file which you'll need to upload to your website, along a js script for them to load. This is the one I use to load the movies page, just as an example.

<details>
<summary>check the code</summary>
```javascript

function formatarData(dataIso) {

  if (!dataIso) return "SEM DATA";

  const [ano, mes, dia] = dataIso.split('-');
  const dataObj = new Date(ano, mes - 1, dia); 

  const diasSemana = ['DOM', 'SEG', 'TER', 'QUA', 'QUI', 'SEX', 'SAB'];

  return `${diasSemana[dataObj.getDay()]} ${dia}.${mes}.${ano}`;

}

function gerarEstrelas(nota) {

  const cheias = Math.floor(nota);
  const vazias = 5 - cheias;

  return '<span class="star filled">★</span>'.repeat(cheias) +
         '<span class="star empty">☆</span>'.repeat(vazias);

}

async function carregarFilmes() {

  try {

    const resposta = await fetch('filmes/movies.json');
    const moviesData = await resposta.json();

    const container = document.getElementById('container-dos-filmes');
    const navAnos = document.getElementById('nav-anos');

    const anosAssistidos = Object.keys(moviesData).sort((a, b) => b - a);

    anosAssistidos.forEach(ano => {

      const btn = document.createElement('a');

      btn.href = `#ano-${ano}`;
      btn.textContent = ano;
      btn.className = 'btn-ano';

      navAnos.appendChild(btn);

    });

    anosAssistidos.forEach(ano => {

      const section = document.createElement('section');

      section.id = `ano-${ano}`;
      section.className = 'ano-section';

      const stats = document.createElement('div');

      stats.className = 'stats-box';
      stats.innerHTML = `⌚ ${moviesData[ano].length} itens registrados em ${ano}.`;

      section.appendChild(stats);

      moviesData[ano].forEach(filme => {

        const article = document.createElement('article');

        article.className = 'filme-item';

        const dataFormatada = formatarData(filme.dateWatched);
        const estrelas = gerarEstrelas(filme.rating);
        const textoReview = filme.review.replace(/\n/g, '<br>');

        article.innerHTML = `

          <div class="poster-container">

            <div class="poster-box">
                  ${filme.poster
                    ? `<img class="roxo" src="${filme.poster}" alt="Capa">`
                    : `<div class="sem-capa">Sem Imagem</div>`
                  }
                </div>

                ${filme.cinema ? `<div class="cinema-marker">🎟 CINEMA</div>` : ''}

            </div>

          <div class="conteudo-filme">

            <div class="data-visto">${dataFormatada}</div>

            <div class="cabecalho-filme">

              <h3>${filme.title}</h3>

              <span class="ano">(${filme.year})</span>

              <div class="estrelas">${estrelas}</div>

            </div>

            <div class="caixa-review">
              ${textoReview}
            </div>

          </div>

        `;

        section.appendChild(article);

      });

      container.appendChild(section);

    });

  } catch (erro) {

    console.error("Erro:", erro);

  }

}

carregarFilmes();

document.getElementById('btn-topo').addEventListener('click', () => {

  window.scrollTo({

    top: 0,
    behavior: 'smooth'

  });

});
```

</details>


## Features

- **Media management:** organize books, movies, games, and anime.
- **JSON storage:** save and load collection data using JSON files.
- **API integration:** retrieve media information through Google Books, TMDB, and RAWG APIs.
- **Personal catalog:** keep your media collection organized locally and use its data on a personal website.

## Tech Stack

- C#
- .NET
- Avalonia UI
- JSON
- Google Books API
- The Movie Database (TMDB) API
- RAWG Video Games Database API
- Jikan API

## Getting Started

### Requirements

- [.NET SDK](https://dotnet.microsoft.com/download)
- API keys for the external services you want to use

### Running the application

Clone the repository:

```bash
git clone https://github.com/YOUR_USERNAME/NeocitiesCatalog.git
cd NeocitiesCatalog
```

Restore dependencies and run the application:

```bash
dotnet restore
dotnet run
```

If the repository contains multiple projects, run the command from the directory containing the appropriate `.csproj` file.

## Configuration

Configure your JSON storage paths and API credentials using the application's configuration file and the appropriate local environment variables.

**Do not commit API keys, passwords, or other secrets to the repository.** Use environment variables or another local secrets mechanism to provide credentials.

## Project Status

This project began as a local desktop application for managing a personal media collection. Its JSON-based storage also supports integration with a separate personal website.

---

Built as a personal project to explore C#, desktop application development, API integration, and data persistence.


## Features

- **Media management:** organize books, movies, games, and anime.
- **JSON storage:** save and load collection data using JSON files.
- **API integration:** retrieve media information through Google Books, TMDB, and RAWG APIs.
- **Personal catalog:** keep your media collection organized locally and use its data on a personal website.

## Tech Stack

- C#
- .NET
- Avalonia UI
- JSON
- Google Books API
- The Movie Database (TMDB) API
- RAWG Video Games Database API

## Getting Started

### Requirements

- [.NET SDK](https://dotnet.microsoft.com/download)
- API keys for the external services you want to use

### Running the application

Clone the repository:

```bash
git clone https://github.com/killmorrow/NeocitiesCatalog.git
cd NeocitiesCatalog
```

Restore dependencies and run the application:

```bash
dotnet restore
dotnet run
```

If the repository contains multiple projects, run the command from the directory containing the appropriate `.csproj` file.

## Configuration

Configure your JSON storage paths and API credentials using the application's configuration file and the appropriate local environment variables.

**Do not commit API keys, passwords, or other secrets to the repository.** Use environment variables or another local secrets mechanism to provide credentials.

## Project Status

This project began as a local desktop application for managing a personal media collection. Its JSON-based storage also supports integration with a separate personal website.

---

Built as a personal project to explore C#, desktop application development, API integration, and data persistence.

