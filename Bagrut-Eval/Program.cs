using Azure.Communication.Email; // Required for EmailClient
using Azure.Identity;
using Azure.Storage.Blobs;
using Bagrut_Eval.Data;
using Bagrut_Eval.Models;
using Bagrut_Eval.Pages.Metrics;
using Bagrut_Eval.Utilities;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration; // Required for configuration reading
using Microsoft.Extensions.DependencyInjection;
using OfficeOpenXml;
using System.Reflection;


var builder = WebApplication.CreateBuilder(args);

// --- 1. DATABASE CONTEXT SETUP ---
var activeConnectionName = builder.Configuration.GetValue<string>("AppSettings:ActiveConnectionName")!;
var connectionString = builder.Configuration.GetConnectionString(activeConnectionName);

string containerKey = activeConnectionName == "AzureDbConnection" ? "AzureContainerName" : "AzureContainerName_Dev";
string activeContainerName = builder.Configuration.GetValue<string>($"StorageSettings:{containerKey}")
    ?? throw new InvalidOperationException($"Configuration Error: StorageSettings:{containerKey} is missing or empty.");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)));

// --- 2. CONFIGURATION ---
builder.Services.Configure<AppSettings>(builder.Configuration.GetSection("AppSettings"));
// NOTE: The obsolete 'builder.Services.Configure<EmailSettings>(...)' line is correctly REMOVED.

// --- 3. AZURE COMMUNICATION SERVICES (ACS) SETUP ---
// Read Connection String
var acsConnectionString = builder.Configuration.GetValue<string>("AzureCommunicationService:ConnectionString");

if (string.IsNullOrEmpty(acsConnectionString))
{
    throw new InvalidOperationException("Azure Communication Service ConnectionString not found in configuration.");
}

// Register the EmailClient as a Singleton (It MUST be registered using the Connection String)
builder.Services.AddSingleton(new Azure.Communication.Email.EmailClient(acsConnectionString));

// Register your custom EmailService wrapper (This uses the EmailClient we just registered)
builder.Services.AddScoped<IEmailService, EmailService>();




// --- 4. AZURE BLOB STORAGE SETUP ---
builder.Services.Configure<StorageOptions>(options =>
{
    options.ContainerName = activeContainerName;
});

var storageAccountName = builder.Configuration.GetValue<string>("CloudStorage:StorageAccountName")
    ?? throw new InvalidOperationException("Configuration Error: 'CloudStorage:StorageAccountName' is missing.");
var serviceUri = new Uri($"https://{storageAccountName}.blob.core.windows.net");

// Register BlobServiceClient as a Singleton using token-based auth
builder.Services.AddSingleton(new BlobServiceClient(serviceUri, new DefaultAzureCredential()));
builder.Services.Configure<StorageOptions>(options =>
{
    options.ContainerName = activeContainerName; 
});
builder.Services.AddScoped<IStorageService, AzureBlobStorageService>(); 



//  APP SERVICES & AUTHENTICATION ---
builder.Services.AddRazorPages();

builder.Services.AddSingleton<ITimeProvider, UtcTimeProvider>();

builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});
builder.Services.AddHttpContextAccessor();

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, options =>
    {
        options.Cookie.Name = "MinimumCookie";
        options.LoginPath = "/Index";
        options.AccessDeniedPath = "/Index";
        // Sets the absolute maximum time the cookie is valid 
        options.ExpireTimeSpan = TimeSpan.FromHours(1);
        options.SlidingExpiration = true;
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("RequireAdminRole", policy => policy.RequireRole("Admin"));
    options.AddPolicy("RequireSeniorRole", policy => policy.RequireRole("Senior", "Admin"));
    options.AddPolicy("RequireEvaluatorRole", policy => policy.RequireRole("Evaluator", "Senior", "Admin"));
});

builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
builder.Services.AddControllersWithViews();

//
//  Version and Build number
//
var assembly = Assembly.GetExecutingAssembly();
var assemblyVersion = assembly.GetName().Version;

// This will be the number of days since Jan 1, 2000.
//string automatedBuildNumber = assemblyVersion != null ? assemblyVersion.Build.ToString() : "0";

// se .Revision to get a number that changes on every build/compile
string automatedBuildNumber = assemblyVersion != null ? assemblyVersion.Revision.ToString() : "0";

string gitHash = "N/A";
string gitHashPath = Path.Combine(AppContext.BaseDirectory, "githash.txt");

if (File.Exists(gitHashPath))
{
    try
    {
        // Read the hash from the file created by the MSBuild task
        gitHash = File.ReadAllText(gitHashPath).Trim();
    }
    catch { /* Ignore error if file access fails */ }
}

builder.Services.Configure<AppSettings>(options =>
{
    builder.Configuration.GetSection("AppSettings").Bind(options);

    options.BuildNumber = automatedBuildNumber;   // Override/set the BuildNumber property
    options.GitCommitHash = gitHash;
});



var app = builder.Build();

DownloadMetrics.ServiceProvider = app.Services;

// --- HTTP REQUEST PIPELINE ---
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseSession();
app.UseRouting();
//  Execution order is critical
app.UseAuthentication();     // 1. Authentication loads the user identity
app.UseAuthorization();      // 2. Authorization sets up user roles/claims
app.UseMaintenanceMode();    // 3. Maintenance, set in appsettubg,json, allow bypass test users


app.MapRazorPages();

app.Run();
