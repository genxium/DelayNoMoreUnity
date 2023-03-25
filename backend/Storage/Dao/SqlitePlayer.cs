using System.ComponentModel.DataAnnotations.Schema;
using shared;
namespace backend.Storage.Dao;

[Table("player")]
public partial class SqlitePlayer {
    public int id { get; set; } = shared.Battle.INVALID_DEFAULT_PLAYER_ID;
    public string name { get; set; } = null!;
}
