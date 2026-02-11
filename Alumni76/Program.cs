using Alumni76.Data;
using Alumni76.Models;
using Alumni76.Utilities;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using OfficeOpenXml;
using System.Reflection;

var builder = WebApplication.CreateBuilder(args);

// --- 1. DATABASE SETUP ---
var activeConnectionName = builder.Configuration.GetValue<string>("AppSettings:ActiveConnectionName")!;
var connectionString = builder.Configuration.GetConnectionString(activeConnectionName);

// Future-proofing the storage config
string containerKey = (activeConnectionName == "AzureDbConnection") ? "AzureContainerName" : "AzureContainerName_Dev";
// This will look for the key in StorageSettings. If not found, it won't crash the WHOLE app yet.
var storageSection = builder.Configuration.GetSection("StorageSettings");
string? activeContainerName = storageSection[containerKey];

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)));


builder.Services.AddDistributedMemoryCache(); // Required for session
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30); // How long until the user is logged out
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

// --- 2. AUTHENTICATION & SECURITY ---
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "AlumniAuthCookie";
        options.LoginPath = "/Index";
        options.AccessDeniedPath = "/Index";
        options.ExpireTimeSpan = TimeSpan.FromMinutes(30);
        options.SlidingExpiration = true;
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"));
});

// --- 3. CORE SERVICES ---
builder.Services.AddRazorPages();
builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
builder.Services.AddSingleton<ITimeProvider, UtcTimeProvider>();

// --- 4. APP SETTINGS & BUILD INFO ---
var version = Assembly.GetExecutingAssembly().GetName().Version;
string buildNumber = version?.Revision.ToString() ?? "0";
string gitHash = "N/A";
string gitHashPath = Path.Combine(AppContext.BaseDirectory, "githash.txt");
if (File.Exists(gitHashPath)) { try { gitHash = File.ReadAllText(gitHashPath).Trim(); } catch { } }

builder.Services.Configure<AppSettings>(options =>
{
    builder.Configuration.GetSection("AppSettings").Bind(options);
    options.BuildNumber = buildNumber;
    options.GitCommitHash = gitHash;
});

// Communication Services and Email
var acsConnectionString = builder.Configuration.GetValue<string>("AzureCommunicationService:ConnectionString");

if (string.IsNullOrEmpty(acsConnectionString))
{
    throw new InvalidOperationException("Azure Communication Service ConnectionString not found in configuration.");
}

// Register the EmailClient as a Singleton (It MUST be registered using the Connection String)
builder.Services.AddSingleton(new Azure.Communication.Email.EmailClient(acsConnectionString));

// Register your custom EmailService wrapper (This uses the EmailClient we just registered)
builder.Services.AddScoped<IEmailService, EmailService>();

var app = builder.Build();

// --- 5. MIDDLEWARE PIPELINE ---
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseSession();
app.UseAuthentication();
app.UseAuthorization();

app.MapRazorPages();

app.Run();