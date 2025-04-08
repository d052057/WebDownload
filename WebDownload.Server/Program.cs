//using Microsoft.Extensions.DependencyInjection;
using WebDownload.Server.Services;
using WebDownload.Server;
using Microsoft.Extensions.FileProviders;
//using System;
using WebDownload.Server.Hubs;
var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
builder.Services.AddSignalR();
builder.Services.AddSwaggerGen();
builder.Services.AddHttpClient();
builder.Services.AddOpenApi();
builder.Services.AddScoped<IDownloadService, DownloadService>();
//builder.Services.ConfigureSwagger();
builder.Services.AddCors();
builder.Services.Configure<ApplicationSettings>(builder.Configuration.GetSection("ApplicationSettings"));

var app = builder.Build();
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
else {
    app.UseHsts();
}
app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();
app.MapHub<DownloadHub>("/downloadHub");
app.MapFallbackToFile("/index.html");

app.Run();