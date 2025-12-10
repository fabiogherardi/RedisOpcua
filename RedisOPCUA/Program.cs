using Microsoft.AspNetCore.SignalR;
using StackExchange.Redis;
using RedisOPCUA.Services;
using RedisOPCUA.Hubs;
using RedisOPCUA.Utils;


var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();
builder.Services.AddSignalR();

// Carica ini
Ini.Load("wwwroot\\monitor.ini"); ;

//----------------------------
// Abilita servizi Opcua
//----------------------------
string connectionStringRedis = Ini.RedisAddress;


builder.Services.AddSingleton<ConnectionMultiplexer>(sp =>
    ConnectionMultiplexer.Connect(connectionStringRedis)            // locahost:6379 è inidirizzo e porta di default di Redis
);
// Hosted Service che ascolta Redis e invia dati via SignalR
builder.Services.AddHostedService<RedisMonitorService>();




//----------------------------
// Abilita servizi Opcua
//----------------------------
builder.Services.AddSingleton<OpcUaMonitorService>();
builder.Services.AddHostedService(provider => provider.GetRequiredService<OpcUaMonitorService>());




//----------------------------
// Servizi vari
//----------------------------
builder.WebHost.UseUrls("http://localhost:5053", "https://localhost:7135");

var app = builder.Build();

// --------------------------
// Middleware
// --------------------------
if (!app.Environment.IsDevelopment())
{
   
    app.UseHsts();
}

// Non aggiungiamo UseHttpsRedirection per permettere HTTP su 5053
app.UseStaticFiles();
app.UseRouting();
app.UseAuthorization();

// --------------------------
// Route MVC
// --------------------------
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}"
);

// --------------------------
// SignalR Hub
// --------------------------
app.MapHub<DataHub>("/DataHub");

// --------------------------
// Run
// --------------------------
app.Run();