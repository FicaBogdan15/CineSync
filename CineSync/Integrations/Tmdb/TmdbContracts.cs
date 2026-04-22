using System.Text.Json.Serialization;

namespace CineSync.Integrations.Tmdb
{
    public sealed class TmdbMovieDetails
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("title")]
        public string Title { get; set; } = string.Empty;

        [JsonPropertyName("overview")]
        public string? Overview { get; set; }

        [JsonPropertyName("release_date")]
        public string? ReleaseDate { get; set; }

        [JsonPropertyName("poster_path")]
        public string? PosterPath { get; set; }

        [JsonPropertyName("genres")]
        public List<TmdbMovieGenre> Genres { get; set; } = new();
    }

    public sealed class TmdbMovieGenre
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;
    }

    public sealed class TmdbMovieCredits
    {
        [JsonPropertyName("cast")]
        public List<TmdbCastMember> Cast { get; set; } = new();

        [JsonPropertyName("crew")]
        public List<TmdbCrewMember> Crew { get; set; } = new();
    }

    public sealed class TmdbCastMember
    {
        [JsonPropertyName("id")]
        public int PersonId { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("character")]
        public string? Character { get; set; }

        [JsonPropertyName("order")]
        public int Order { get; set; }
    }

    public sealed class TmdbCrewMember
    {
        [JsonPropertyName("id")]
        public int PersonId { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("job")]
        public string? Job { get; set; }
    }

}
