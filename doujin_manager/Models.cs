using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;

namespace doujin_manager
{
    public class ArtistModel : IComparable
    {
        public int Id { get; set; } = -1;
        public required string Name { get; set; }

        public override string ToString()
        {
            return Name;
        }

        public int CompareTo(object? other)
        {
            return Name.CompareTo(other?.ToString());
        }
    }

    public class CircleModel : IComparable
    {
        public int Id { get; set; } = -1;
        public required string Name { get; set; }
        public override string ToString()
        {
            return Name;
        }

        public int CompareTo(object? other)
        {
            return Name.CompareTo(other?.ToString());
        }
    }

    public class BookModel
    {
        public int Id { get; set; } = -1;
        public required string Title { get; set; }
        public List<ArtistModel> Artists { get; set; }
        public required CircleModel Circle { get; set; }
        public DateOnly? Date { get; set; }
        public string? DateStr
        {
            get
            {
                return Date?.ToString("yyyy-MM-dd");
            }
        }

        public string ArtistsStr
        {
            get
            {
                return string.Join(", ", Artists);
            }
        }

        public SqliteParameter[] GetQueryParams()
        {
            return new SqliteParameter[]{
                new("@title", Title),
                new("@circle", Circle.Id),
                new("@date", DateStr != null ? DateStr : DBNull.Value),
            };
        }
    }
}
