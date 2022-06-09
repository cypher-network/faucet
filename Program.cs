using Faucet;
using Faucet.Cryptography;
using Faucet.Data;
using Faucet.Extensions;
using Faucet.Hubs;
using Faucet.Ledger;
using Faucet.Persistence;
using Faucet.Services;
using Faucet.Wallet;
using Microsoft.AspNetCore.Server.Kestrel.Https;
using Serilog;


var config = new ConfigurationBuilder()

    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json"), false, true)
    .AddCommandLine(args)
    .Build();

const string logSectionName = "Log";
if (config.GetSection(logSectionName) != null)
{
    Log.Logger = new LoggerConfiguration()
        .WriteTo.File(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "faucet.log"))
        .ReadFrom.Configuration(config, logSectionName)
        .CreateLogger();
}
else
{
    throw new Exception($"No \"{logSectionName}\" section found in appsettings.json");
}

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();
builder.Services.AddSignalR();
builder.Services.AddDataKeysProtection(config);
builder.Services.AddSingleton<IFaucetSystem, FaucetSystem>();
builder.Services.AddSingleton<IUnitOfWork>(sp =>
{
    var unitOfWork = new UnitOfWork("storedb", Log.Logger);
    return unitOfWork;
});
builder.Services.AddSingleton<IWalletSession, WalletSession>();
builder.Services.AddSingleton<IBlockchain, Blockchain>();
builder.Services.AddTransient<ICrypto, Crypto>();
builder.Services.AddTransient<IWallet, Wallet>();
builder.Services.AddSingleton(sp =>
{
    var url = config?["HttpEndPoint"];
    var dataServices = new DataService(sp.GetService<IHostApplicationLifetime>(), sp.GetService<IHttpClientFactory>(), url, Log.Logger);
    return dataServices;
});
builder.Services.AddSingleton<IBackgroundWorkerQueue, BackgroundWorkerQueue>();
builder.Services.AddSingleton(Log.Logger);
builder.Services.AddHostedService<LongRunningService>();
builder.Services.AddHttpClient();
builder.Services.AddLogging(loggingBuilder =>
{
    loggingBuilder.AddSerilog(dispose: true);
});
builder.Logging.AddSerilog();
builder.Services.AddLettuceEncrypt();

var app = builder.Build();

app.Lifetime.ApplicationStarted.Register(() =>
{
    app.Services.GetService<IUnitOfWork>();
    app.Services.GetService<IBlockchain>();
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
app.UseEndpoints(endpoints =>
{
    endpoints.MapHub<MinerHub>("/miner");
});
app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

app.Run();