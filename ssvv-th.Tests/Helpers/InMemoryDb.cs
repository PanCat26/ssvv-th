using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using ssvv_th.Data;

namespace ssvv_th.Tests.Helpers
{
    public static class InMemoryDb
    {
        public static LibraryDbContext Create()
        {
            var options = new DbContextOptionsBuilder<LibraryDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options;

            var context = new LibraryDbContext(options);
            context.Database.EnsureCreated();
            return context;
        }
    }
}
