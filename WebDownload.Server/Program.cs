using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.FileProviders;
using WebDownload.Server.Models;
using WebDownload.Server.Services;
using WebDownload.Server.Hubs;
using Google.Cloud.Translation.V2;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

if (builder.Environment.IsDevelopment())
{
    builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);
}

// 2. ADD SERVICES TO THE CONTAINER
builder.Services.Configure<VoiceoverSettings>(builder.Configuration.GetSection("Voiceover"));
builder.Services.AddDbContext<DBWebDownload>(o => o
    .UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"))
    .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking));
builder.Services.AddSingleton<FfmpegRunner>();
builder.Services.AddSingleton<VoiceAnalysisService>();
builder.Services.AddSingleton<MediaPathResolver>();
builder.Services.AddSingleton<VoiceoverJobRegistry>();
builder.Services.AddSingleton<VoiceoverService>();
builder.Services.AddSingleton<VoiceoverJobRunner>();
builder.Services.AddScoped<MediaBrowseService>();
//splitter
builder.Services.Configure<SplitterSettings>(builder.Configuration.GetSection("Splitter"));
builder.Services.AddSingleton<DeviceDetector>();
builder.Services.AddSingleton<DemucsRunner>();
builder.Services.AddSingleton<SplitterService>();
builder.Services.AddSingleton<SplitterJobRunner>();
builder.Services.AddScoped<MediaTreeService>();
//voice swap: split (Demucs) -> convert the voice (Applio, outside process) -> mix (ffmpeg)
builder.Services.Configure<VoiceSwapSettings>(builder.Configuration.GetSection("VoiceSwap"));
builder.Services.AddSingleton<StemSeparator>();
builder.Services.AddSingleton<ApplioRunner>();
builder.Services.AddSingleton<VoiceSwapService>();
builder.Services.AddSingleton<VoiceSwapJobRunner>();
//
// menus (movies, videos, musics, ...) come from the MediaMenu table
builder.Services.AddMemoryCache();
builder.Services.Configure<MenuSettings>(builder.Configuration.GetSection("MediaMenu"));
builder.Services.AddScoped<MenuService>();

builder.Services.AddControllers();
builder.Services.AddSignalR();
builder.Services.AddHttpClient();
builder.Services.AddOpenApi();
builder.Services.AddScoped<IDownloadService, DownloadService>();
builder.Services.AddSingleton<ITranslationJobTracker, TranslationJobTracker>();
builder.Services.AddCors();
builder.Services.Configure<ApplicationSettings>(builder.Configuration.GetSection("ApplicationSettings"));
builder.Services.Configure<SubtitleSettings>(builder.Configuration.GetSection("Subtitle"));
builder.Services.Configure<YtDlpSettings>(builder.Configuration.GetSection("YtDlp"));

// 3. INITIALIZE GOOGLE TRANSLATION SAFELY
var apiKey = builder.Configuration["GoogleCloud:ApiKey"];
if (string.IsNullOrWhiteSpace(apiKey))
{
    // Provide a clearer exception message explaining where to add it in Production/IIS
    throw new InvalidOperationException(
        "GoogleCloud:ApiKey is missing. Ensure it is defined in appsettings.Local.json (Dev) or appsettings.json (IIS/Production).");
}

var translationClient = TranslationClient.CreateFromApiKey(apiKey);
builder.Services.AddSingleton(translationClient);
builder.Services.AddScoped<ISubtitleTranslationService, SubtitleTranslationService>();


// App-level paths (media drive, URL prefixes, hub endpoints) all come from "ApplicationSettings".
var appSettings = builder.Configuration.GetSection("ApplicationSettings").Get<ApplicationSettings>() ?? new ApplicationSettings();

// 4. FORWARDED HEADERS
var forwardedHeadersOptions = new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost
};
forwardedHeadersOptions.KnownNetworks.Clear();
forwardedHeadersOptions.KnownProxies.Clear();

// 5. BUILD THE APP
var app = builder.Build();
app.UseForwardedHeaders(forwardedHeadersOptions);

// 6. MIDDLEWARE PIPELINE
if (!string.IsNullOrWhiteSpace(appSettings.PathBase))
{
    app.UsePathBase("/" + appSettings.PathBase.Trim('/'));
}
app.UseStaticFiles();

var mediaRequestPath = "/" + appSettings.MediaRequestPath.Trim('/');
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(appSettings.MediaDrive),
    RequestPath = mediaRequestPath
});
app.UseDirectoryBrowser(new DirectoryBrowserOptions
{
    FileProvider = new PhysicalFileProvider(appSettings.MediaDrive),
    RequestPath = mediaRequestPath
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
    .AllowCredentials();
});

app.UseDefaultFiles();
app.MapStaticAssets();

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}
app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();
app.MapHub<DownloadHub>(appSettings.DownloadHubPath);
app.MapHub<SplitterHub>(appSettings.SplitterHubPath);
app.MapHub<VoiceSwapHub>(appSettings.VoiceSwapHubPath);
app.MapHub<ConvertHub>(appSettings.ConvertHubPath);
app.MapFallbackToFile("/index.html");

app.Run();
