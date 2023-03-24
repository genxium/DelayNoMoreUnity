using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using backend.Storage.Dao;

namespace backend.Storage;

public class DevEnvResourcesSqliteContext : DbContext {

    public DevEnvResourcesSqliteContext(DbContextOptions<DevEnvResourcesSqliteContext> options) : base(options) {
        
    }


    public DbSet<SqlitePlayer> players { get; } = null!;
}

 