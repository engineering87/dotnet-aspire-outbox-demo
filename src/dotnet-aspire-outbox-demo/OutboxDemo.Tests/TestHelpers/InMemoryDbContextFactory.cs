// (c) 2025 Francesco Del Re <francesco.delre.87@gmail.com>
// This code is licensed under MIT license (see LICENSE.txt for details)
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using ReceiverService.Data;
using SenderService.Data;

namespace OutboxDemo.Tests.TestHelpers;

public static class InMemoryDbContextFactory
{
    public static SenderDbContext CreateSenderDbContext()
    {
        var options = new DbContextOptionsBuilder<SenderDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        var context = new SenderDbContext(options);
        context.Database.EnsureCreated();
        return context;
    }

    public static ReceiverDbContext CreateReceiverDbContext()
    {
        var options = new DbContextOptionsBuilder<ReceiverDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        var context = new ReceiverDbContext(options);
        context.Database.EnsureCreated();
        return context;
    }
}
