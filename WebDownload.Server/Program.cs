using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.FileProviders;
using WebDownload.Server.Models;
using WebDownload.Server.Services;
using WebDownload.Server;
using WebDownload.Server.Hubs;
using Google.Cloud.Translation.V2;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection;
var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
builder.Services.AddSignalR();
builder.Services.AddSwaggerGen();
builder.Services.AddHttpClient();
builder.Services.AddOpenApi();
builder.Services.AddScoped<IDownloadService, DownloadService>();
builder.Services.AddScoped<ITranslationService, TranslationService>();
builder.Services.AddSingleton<ITranslationJobTracker, TranslationJobTracker>();
//builder.Services.ConfigureSwagger();
builder.Services.AddCors();
builder.Services.Configure<ApplicationSettings>(builder.Configuration.GetSection("ApplicationSettings"));
builder.Services.Configure<SubtitleSettings>(builder.Configuration.GetSection("Subtitle"));
var app = builder.Build();
if (builder.Environment.IsDevelopment())
{
    builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);
}
var apiKey = builder.Configuration["GoogleCloud:ApiKey"];
if (string.IsNullOrWhiteSpace(apiKey))
{
    throw new InvalidOperationException(
        "GoogleCloud:ApiKey is missing. Set it in appsettings.Local.json.");
}

var translationClient = TranslationClient.CreateFromApiKey(apiKey);

builder.Services.AddSingleton(translationClient);

builder.Services.AddScoped<ISubtitleTranslationService, SubtitleTranslationService>();



// Must run first: trust X-Forwarded-Proto/For/Host from the reverse proxy
// so HTTPS redirection, HSTS, and Request.Scheme/Host are correct.
var forwardedHeadersOptions = new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost
};
// Default only trusts 127.0.0.1. If the reverse proxy is on another
// host/container, either add its IP to KnownProxies, or (only if the proxy
// is not internet-reachable directly, e.g. behind a firewall/same docker
// network) clear the lists so any proxy IP is trusted:
forwardedHeadersOptions.KnownNetworks.Clear();
forwardedHeadersOptions.KnownProxies.Clear();
app.UseForwardedHeaders(forwardedHeadersOptions);

// This app is reached under a '/webdownload' virtual path in your hosting
// setup (see SignalrService and SubtitleDashboard on the client, which both
// assume that prefix). UsePathBase makes the app aware of that prefix
// itself instead of relying entirely on an external reverse proxy to add
// or strip it - it's a no-op if a proxy in front already stripped the
// prefix before the request reaches Kestrel, and it correctly strips it
// here if the full '/webdownload/...' path arrives unchanged. This must
// run before UseStaticFiles/MapControllers/MapHub below.
app.UsePathBase("/webdownload");

app.UseStaticFiles();
string MediaDrive = builder.Configuration.GetValue("ApplicationSettings:MediaDrive", "*") ?? @"c:/medias";
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(MediaDrive),
    RequestPath = "/medias"
});
app.UseDirectoryBrowser(new DirectoryBrowserOptions
{
    FileProvider = new PhysicalFileProvider(MediaDrive),
    RequestPath = "/medias"
});
var corsUrls = builder.Configuration.GetSection("CorsUrls:AllowedOrigins").Get<string[]>();
if (corsUrls == null)
{
    throw new InvalidOperationException("CorsUrls:AllowedOrigins configuration section is missing or empty.");
}
app.UseCors(opt =>
{
    opt
    .WithOrigins(corsUrls)
    .AllowAnyHeader()
    .AllowAnyMethod()
    .AllowCredentials()
    ;
});
app.UseDefaultFiles();
app.MapStaticAssets();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwaggerUI(
        options =>
        {
            options.SwaggerEndpoint("../openapi/v1.json", "version 1");
        });
}
else
{
    app.UseHsts();
}
app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();
app.MapHub<DownloadHub>("/downloadHub");
app.MapFallbackToFile("/index.html");

app.Run();