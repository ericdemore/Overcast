using System.Collections.Generic;

namespace Overcast.Models
{
    public class MediaComparisonModel
    {
        public string Title1 { get; set; }
        public string Title2 { get; set; }
        public List<CommonCastMember> CommonCast { get; set; } = new();
        public string ErrorMessage { get; set; }
        public List<string> NotFoundTitles { get; set; } = new();
    }

    public class CommonCastMember
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string ProfilePath { get; set; }
        public string Character1 { get; set; }
        public string Character2 { get; set; }
        public string Media1ReleaseDate { get; set; }
        public string Media2ReleaseDate { get; set; }
    }
}
