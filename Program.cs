using DLLTesting;
using DLLTesting.StartGGLibrary;
using static DLLTesting.PayoutCalculator;
using LocalsPayoutTables;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");
builder.Services.AddBlazorBootstrap();

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

// On Azure the API is served from the same site under /api. When running locally, wwwroot/appsettings.Development.json
// points this at the Functions host (Api project) instead.
var apiBaseAddress = builder.Configuration["ApiBaseAddress"] ?? builder.HostEnvironment.BaseAddress;
builder.Services.AddScoped(sp => new TournamentDataService(new HttpClient { BaseAddress = new Uri(apiBaseAddress) }));

await builder.Build().RunAsync();
