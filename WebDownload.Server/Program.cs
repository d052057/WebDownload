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
//

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
app.MapHub<DownloadHub>("/downloadHub");
app.MapHub<SplitterHub>("/splitterHub");
app.MapHub<ConvertHub>("/convertHub");
app.MapFallbackToFile("/index.html");

app.Run();
