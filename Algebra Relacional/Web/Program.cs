using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using ARWeb;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Acceso a wwwroot/js/app.js (SQLite, editor, guardado...)
builder.Services.AddScoped<ARWeb.Interop.Js>();

await builder.Build().RunAsync();
