using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using StudySprint;
using StudySprint.Services;

var builder =
    WebAssemblyHostBuilder.CreateDefault(args);

builder.RootComponents.Add<App>("#app");

builder.RootComponents.Add<HeadOutlet>(
    "head::after");

builder.Services.AddScoped(
    _ => new HttpClient());

builder.Services.AddScoped<AppSession>();

builder.Services.AddScoped<SupabaseService>();

builder.Services.AddScoped<AuthStorageService>();

await builder.Build().RunAsync();