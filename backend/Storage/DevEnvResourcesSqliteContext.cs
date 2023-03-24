using Microsoft.EntityFrameworkCore;
using backend.Storage.Dao;

namespace backend.Storage;

public class DevEnvResourcesSqliteContext : DbContext {

    public DevEnvResourcesSqliteContext(DbContextOptions<DevEnvResourcesSqliteContext> options) : base(options) {
        
    }


    public DbSet<SqlitePlayer> Players { get; set; } = null!;
}

 