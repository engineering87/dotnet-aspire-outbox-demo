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

builder.Services.AddScoped<IntegrationEventProcessor>();
builder.Services.AddHostedService<ArtemisEventConsumerHostedService>();

builder.Services.AddControllers();

var app = builder.Build();

app.UseServiceDefaults();

app.UseHttpsRedirection();

app.MapControllers();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ReceiverDbContext>();
    db.Database.EnsureCreated();
}

app.Run();