// --------------------------------------------------------------------------------------------------------------------
// OutcoldSolutions (http://outcoldsolutions.com)
// --------------------------------------------------------------------------------------------------------------------
namespace OutcoldSolutions.GoogleMusic.Models
{
    using SQLite;

    [Table("UserPlaylist")]
    public class UserPlaylistEntity
    {
        [PrimaryKey, AutoIncrement]
        public int PlaylistId { get; set; }

        public string ProviderPlaylistId { get; set; }

        public string Title { get; set; }

        public string TitleNorm { get; set; }
    }
}
