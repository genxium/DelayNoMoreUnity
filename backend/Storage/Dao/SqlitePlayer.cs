using System;
namespace backend.Storage.Dao;

public partial class SqlitePlayer {
    public int id { get; set; } = 0;
    public string name { get; set; } = null!;
}
