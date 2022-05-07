using Faucet.Data;
using Faucet.Services;
using Faucet.Wallet;

var config = new ConfigurationBuilder()

    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json"), false, true)
    .AddCommandLine(args)
    .Build();

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();
builder.Services.AddSingleton<IWalletSession, WalletSession>();
builder.Services.AddTransient<IWallet, Wallet>();
builder.Services.AddSingleton(sp =>
{
    var url = config?["HttpEndPoint"];
    var dataServices = new DataService(sp.GetService<IHostApplicationLifetime>(), sp.GetService<IHttpClientFactory>(), url);
    return dataServices;
});
builder.Services.AddSingleton<IBackgroundWorkerQueue, BackgroundWorkerQueue>();
builder.Services.AddHostedService<LongRunningService>();
builder.Services.AddHttpClient();

var app = builder.Build();

app.Lifetime.ApplicationStarted.Register(() =>
{
    app.Services.GetService<IWalletSession>();
});

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles();

app.UseRouting();

app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

app.Run();