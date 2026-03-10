# Epubinator

Epubinator is a **client-only Blazor WebAssembly PWA** for reading EPUB books with a focus on readability and offline usage.

- No server, no user accounts
- Books stay in the browser
- Reading progress and settings persist locally

## What It Does

- Upload `.epub` files from the side menu
- Keep a local library of uploaded books
- Open and switch books from the library
- Render chapter content in a clean reading view
- Navigate chapters with previous/next controls
- Remember where you stopped reading
  - Chapter index
  - Scroll position within chapter
- Customize reading appearance
  - Light and dark theme
  - Font family
  - Font size
- Work as a PWA (installable, offline-capable)
- Show an in-app update prompt when a new service worker version is available

## Storage Model (Client-Only)

All data is stored in the browser:

- **IndexedDB (Dexie)**
  - EPUB binaries
  - Book metadata
- **localStorage**
  - Reader settings
  - Per-book reading progress

No backend service is required.

## Tech Stack

- .NET 10
- Blazor WebAssembly (PWA template)
- Bootstrap 5
- VersOne.Epub (EPUB parsing)
- AngleSharp (HTML sanitization/processing)
- Dexie.js (IndexedDB wrapper via JS interop)

## Project Layout

- `src/Epubinator.Client` - Blazor WebAssembly app
- `src/Epubinator.Client/Components` - Reader/menu/settings/update UI
- `src/Epubinator.Client/Services` - EPUB parsing, library, progress, theme services
- `src/Epubinator.Client/wwwroot/js/interop.js` - IndexedDB/theme/scroll/service-worker interop
- `src/Epubinator.Client/wwwroot/css/app.css` - Theming and reader styles
- `.github/workflows/deploy.yml` - GitHub Pages deployment pipeline

## Run Locally

From repository root:

```bash
dotnet restore src/Epubinator.Client/Epubinator.Client.csproj
dotnet run --project src/Epubinator.Client/Epubinator.Client.csproj
```

Then open the local URL printed by `dotnet run`.

## Build / Publish

```bash
dotnet build src/Epubinator.Client/Epubinator.Client.csproj
dotnet publish src/Epubinator.Client/Epubinator.Client.csproj -c Release -o publish
```

Publish output is generated in `publish/`.

## GitHub Pages

Deployment is done by GitHub Actions:

1. On push to `main`, workflow restores and publishes the app.
2. Workflow uploads `publish/wwwroot` as the Pages artifact.
3. Deployment step publishes to GitHub Pages.

Base path is configured for project pages deployment at:

- `/Epubinator/`

## Notes

- This is a privacy-first, local-first reader. Data does not sync across devices.
- Browser storage behavior may vary by browser/platform.
