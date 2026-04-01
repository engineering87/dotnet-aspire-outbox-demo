using Microsoft.EntityFrameworkCore;
using ReceiverService.Data;
using ReceiverService.Messaging;
using ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);

// Config condivisa
builder.AddServiceDefaults();

// DB (Postgres; Aspire fornirà la connString)
var connectionString = builder.Configuration.GetConnectionString("receiverdb")
    ?? builder.Configuration.GetConnectionString("ReceiverDb")
    ?? "Host=receiverdb;Database=receiverdb;Username=postgres;Password=postgres";

builder.Services.AddDbContext<ReceiverDbContext>(options =>
    options.UseNpgsql(connectionString));

// Consumer Artemis
builder.Services.AddHostedService<ArtemisEventConsumerHostedService>();

// Controller
builder.Services.AddControllers();

var app = builder.Build();

app.UseServiceDefaults();

app.UseHttpsRedirection();

app.MapControllers();

// Database initialization (development only)
// WARNING: EnsureDeleted/EnsureCreated are used **strictly for demo purposes**
// to recreate a clean database on each startup.
// For a production-ready implementation, remove these calls and rely on
// EF Core migrations to manage schema evolution.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ReceiverDbContext>();
    db.Database.EnsureDeleted();
    db.Database.EnsureCreated();
}

app.Run();