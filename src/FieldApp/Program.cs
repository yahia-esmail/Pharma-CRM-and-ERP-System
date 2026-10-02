using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using PharmaERP.FieldApp;
using PharmaERP.FieldApp.UI;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Pages, services and JS interop all live in PharmaERP.FieldApp.UI; this project only hosts them.
builder.Services.AddFieldApp(builder.Configuration);

await builder.Build().RunAsync();
