using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Epubinator.Client;
using Epubinator.Client.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

// Singletons — WASM has no server request scope, singletons persist for the app lifetime
builder.Services.AddSingleton<EpubReaderService>();
builder.Services.AddSingleton<EpubLibraryService>();
builder.Services.AddSingleton<ReadingProgressService>();
builder.Services.AddSingleton<ThemeService>();

await builder.Build().RunAsync();
