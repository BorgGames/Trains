using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Trains.Persistence;
using Trains.Web.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddDbContext<TrainsDbContext>(options => {
    var cs = builder.Configuration.GetConnectionString("Trains");
    if (string.IsNullOrWhiteSpace(cs))
        throw new InvalidOperationException("Missing connection string 'ConnectionStrings:Trains'.");
    options.UseNpgsql(cs);
});

builder.Services
    .AddIdentity<IdentityUser, IdentityRole>(options => options.SignIn.RequireConfirmedAccount = false)
    .AddEntityFrameworkStores<TrainsDbContext>()
    .AddDefaultTokenProviders();

builder.Services.AddDataProtection().PersistKeysToDbContext<TrainsDbContext>();

builder.Services.ConfigureApplicationCookie(options => {
    options.LoginPath = "/account/login";
    options.LogoutPath = "/account/logout";
});

builder.Services.AddSingleton<PuzzleSvgRenderer>();
builder.Services.AddScoped<PuzzleCatalog>();
builder.Services.AddScoped<PuzzleProgressStore>();
builder.Services.AddScoped<PuzzleVotingStore>();
builder.Services.AddScoped<PuzzleSubmissionService>();
builder.Services.AddSingleton<PlayPayloadProtector>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.Use(async (context, next) => {
    if (!context.Response.Headers.ContainsKey("X-Content-Type-Options"))
        context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    if (!context.Response.Headers.ContainsKey("X-Frame-Options"))
        context.Response.Headers["X-Frame-Options"] = "DENY";
    if (!context.Response.Headers.ContainsKey("Referrer-Policy"))
        context.Response.Headers["Referrer-Policy"] = "no-referrer";
    if (!context.Response.Headers.ContainsKey("Content-Security-Policy")) {
        context.Response.Headers["Content-Security-Policy"] =
            "default-src 'self'; " +
            "base-uri 'self'; " +
            "form-action 'self'; " +
            "frame-ancestors 'none'; " +
            "object-src 'none'; " +
            "script-src 'self'";
    }

    await next();
});

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();
app.MapRazorPages()
   .WithStaticAssets();

await using (var scope = app.Services.CreateAsyncScope()) {
    var db = scope.ServiceProvider.GetRequiredService<TrainsDbContext>();
    var svg = scope.ServiceProvider.GetRequiredService<PuzzleSvgRenderer>();
    await PuzzleSeeder.EnsureCreatedAndSeedAsync(db, svg, CancellationToken.None);
}

app.Run();
