// (c) 2025 Francesco Del Re <francesco.delre.87@gmail.com>
// This code is licensed under MIT license (see LICENSE.txt for details)
using Microsoft.EntityFrameworkCore;
using SenderService.Data;
using SenderService.Outbox;
using ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);

// Shared configuration (health checks, swagger, etc.)
builder.AddServiceDefaults();

// Database (Postgres; Aspire will provide the connection string)
var connectionString = builder.Configuration.GetConnectionString("senderdb")
    ?? builder.Configuration.GetConnectionString("SenderDb")
    ?? "Host=senderdb;Database=senderdb;Username=postgres;Password=postgres";

builder.Services.AddDbContext<SenderDbContext>(options =>
    options.UseNpgsql(connectionString));

// Outbox & AMQP publisher
builder.Services.AddScoped<IEventPublisher, ArtemisEventPublisher>();
builder.Services.AddHostedService<OutboxPublisherHostedService>();

// MVC Controllers
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
    var db = scope.ServiceProvider.GetRequiredService<SenderDbContext>();
    db.Database.EnsureDeleted();
    db.Database.EnsureCreated();
}

app.Run();