using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.DependencyInjection;
using UrgentCare.Analysis.Distributions;
using UrgentCare.Core.Distributions;
using UrgentCare.Web;
using UrgentCare.Web.Session;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");

builder.Services.AddSingleton<BundledScenarios>();
builder.Services.AddSingleton<IDistributionFactory, MathNetDistributionFactory>();

await builder.Build().RunAsync();
