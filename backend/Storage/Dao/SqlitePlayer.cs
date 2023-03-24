using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace backend.Storage.Dao;

[Table("player")]
public partial class SqlitePlayer {
    public int id { get; set; } = 0;
    public string name { get; set; } = null!;
}
