using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Text;
using Microsoft.Data.Sqlite;

namespace doujin_manager
{
    public class DBAccessor
    {
        private static DBAccessor accessor = new();
        private string connStr;

        public static DBAccessor GetDBAccessor()
        {
            return accessor;
        }

        private DBAccessor()
        {
            string path = AppDomain.CurrentDomain.BaseDirectory + "data.db";
            try
            {
                using (new FileStream(path, FileMode.CreateNew)) { }
            }
            catch (IOException)
            {
                // data source aleady exists, passing
            }
            connStr = new SqliteConnectionStringBuilder{
                DataSource = path,
            }.ToString();
        }

        public void Initialize()
        {
            using (SqliteConnection conn = new(connStr))
            {
                conn.Open();
                SqliteCommand c = new(
                    @"create table if not exists artist(
                        id integer primary key,
                        name text not null
                      );
                      create table if not exists circle(
                        id integer primary key,
                        name text not null
                      );
                      create table if not exists books(
                        id integer primary key autoincrement,
                        title text not null,
                        artist integer not null,
                        circle integer not null,
                        date date,
                        foreign key(artist) references artist(id),
                        foreign key(circle) references circle(id)
                      );
                      create table if not exists artist_circle_rel(
                        artist_id integer not null,
                        circle_id integer not null,
                        foreign key(artist_id) references artist(id),
                        foreign key(circle_id) references circle(id),
                        primary key(artist_id, circle_id)
                      );", conn
                );
                c.ExecuteNonQuery();
            }
        }

        // insertions
        private static int getInsertedId(SqliteConnection conn)
        {
            SqliteCommand c = new("select last_insert_rowid();", conn);
            SqliteDataReader r = c.ExecuteReader();
            r.Read();
            return r.GetInt32(0);
        }

        public int InsertBook(BookModel book)
        {
            using(SqliteConnection conn = new(connStr))
            { 
                conn.Open();
                SqliteCommand c = new("insert into books (title, artist, circle, date) values (@title, @artist, @circle, @date);", conn);
                c.Parameters.AddRange(book.GetQueryParams());
                c.ExecuteNonQuery();

                return getInsertedId(conn);
            }
        }

        public int InsertArtist(ArtistModel artist)
        {
            using(SqliteConnection conn = new(connStr))
            {
                conn.Open();
                SqliteCommand c = new("insert into artist (name) values (@name);", conn);
                c.Parameters.AddWithValue("@name", artist.Name);
                c.ExecuteNonQuery();

                return getInsertedId(conn);
            }
        }

        public int InsertCircle(CircleModel circle)
        {
            using (SqliteConnection conn = new(connStr))
            {
                conn.Open();
                SqliteCommand c = new("insert into circle (name) values (@name);", conn);
                c.Parameters.AddWithValue("@name", circle.Name);
                c.ExecuteNonQuery();

                return getInsertedId(conn);
            }
        }

        public void InsertRelation(ArtistModel artist, CircleModel circle)
        {
            using(SqliteConnection conn = new(connStr))
            {
                conn.Open();
                SqliteCommand q = new("select * from artist_circle_rel where artist_id = @artist and circle_id = @circle;", conn);
                SqliteParameter[] p = new SqliteParameter[] { new("@artist", artist.Id), new("@circle", circle.Id) };
                q.Parameters.AddRange(p);
                SqliteDataReader r = q.ExecuteReader();
                if (r.HasRows)
                {
                    return;
                }
                SqliteCommand c = new("insert into artist_circle_rel (artist_id, circle_id) values (@artist, @circle);", conn);
                c.Parameters.AddRange(p);
                c.ExecuteNonQuery();
            }
        }

        // retrievals
        public List<ArtistModel> GetAllArtists()
        {
            using(SqliteConnection conn = new(connStr))
            {
                conn.Open();
                SqliteCommand c = new("select * from artist;", conn);
                SqliteDataReader r = c.ExecuteReader();

                List<ArtistModel> artists = new();
                while(r.Read())
                {
                    artists.Add(new ArtistModel { Id =  r.GetInt32("id"), Name = r.GetString("name") });
                }
                return artists;
            }
        }

        public List<BookModel> GetAllBooks()
        {
            using(SqliteConnection conn = new(connStr))
            {
                conn.Open();
                SqliteCommand c = new(
                    @"select books.id, title, artist, circle, date, artist.name as aname, circle.name as cname from books
                      inner join artist on books.artist = artist.id 
                      inner join circle on books.circle = circle.id;", conn);
                
                List<BookModel> books = new();
                ModelDict<int, ArtistModel> artistDict = new();
                ModelDict<int, CircleModel> circleDict = new();

                SqliteDataReader r = c.ExecuteReader();
                while(r.Read())
                {
                    int artistId = r.GetInt32("artist");
                    int circleId = r.GetInt32("circle");

                    ArtistModel ar = artistDict.GetOrNull(artistId) ?? artistDict.Add(artistId, new ArtistModel { Id = artistId, Name = r.GetString("aname") });
                    CircleModel ci = circleDict.GetOrNull(circleId) ?? circleDict.Add(circleId, new CircleModel { Id = circleId, Name = r.GetString("cname") });

                    books.Add(new BookModel
                    {
                        Id = r.GetInt32("id"),
                        Title = r.GetString("title"),
                        Artist = ar,
                        Circle = ci,
                        Date = r.IsDBNull("date") ? null: DateOnly.FromDateTime(r.GetDateTime("date"))
                    });
                }
                return books;

            }
        }

        public List<BookModel> GetBooksOfArtist(ArtistModel artist) {
            using(SqliteConnection conn =new(connStr))
            {
                conn.Open();
                SqliteCommand c = new(
                    @"select books.id, title, artist, circle, date, circle.name as cname from books 
                      inner join circle on books.circle = circle.id 
                      where books.artist = @artist;", conn);
                c.Parameters.AddWithValue("@artist", artist.Id);
                SqliteDataReader r = c.ExecuteReader();
                   
                List<BookModel> books = new();
                ModelDict<int, CircleModel> circleDict = new();
                while(r.Read()) {
                    int circleId = r.GetInt32("circle");
                    CircleModel ci = circleDict.GetOrNull(circleId) ?? circleDict.Add(circleId, new CircleModel { Id =  circleId, Name = r.GetString("cname") });

                    books.Add(new BookModel
                    {
                        Id = r.GetInt32("id"),
                        Title = r.GetString("title"),
                        Artist = artist,
                        Circle = ci,
                        Date = DateOnly.FromDateTime(r.GetDateTime("date"))
                    });
                }
                return books;
            }
        }

        public List<ArtistModel> GetArtistsLike(string prefix)
        {
            using(SqliteConnection conn = new(connStr))
            {
                conn.Open();
                SqliteCommand c = new("select * from artist where name like @prefix;", conn);
                c.Parameters.AddWithValue("@prefix", prefix + "%");

                SqliteDataReader r = c.ExecuteReader();
                List<ArtistModel> artists = new();
                
                while(r.Read())
                {
                    int id = r.GetInt32("id");
                    string name = r.GetString("name");
                    artists.Add(new ArtistModel { Id =id, Name = name });
                }
                return artists;
            }
        }

        public List<CircleModel> GetCirclesLike(string prefix)
        {
            using (SqliteConnection conn = new(connStr))
            {
                conn.Open();
                SqliteCommand c = new("select * from circle where name like @prefix;", conn);
                c.Parameters.AddWithValue("@prefix", prefix + "%");

                SqliteDataReader r = c.ExecuteReader();
                List<CircleModel> circles = new();

                while (r.Read())
                {
                    int id = r.GetInt32("id");
                    string name = r.GetString("name");
                    circles.Add(new CircleModel { Id = id, Name = name });
                }
                return circles;
            }
        }

        public List<ArtistModel> GetRelatedArtists(CircleModel circle)
        {
            using (SqliteConnection conn = new(connStr))
            {
                conn.Open();
                SqliteCommand c = new(
                    @"select artist.id, artist.name from artist 
                      inner join artist_circle_rel on artist.id = artist_circle_rel.artist_id 
                      where artist_circle_rel.circle_id = @circle;", conn);
                c.Parameters.AddWithValue("@circle", circle.Id);
                SqliteDataReader r = c.ExecuteReader();
                List<ArtistModel> artists = new();

                while (r.Read())
                {
                    int id=r.GetInt32("id");
                    string name = r.GetString("name");
                    artists.Add(new ArtistModel { Id = id, Name = name });  
                }
                return artists;
            }
        }

        public List<CircleModel> GetRelatedCircles(ArtistModel artist)
        {
            using (SqliteConnection conn = new(connStr))
            {
                conn.Open();
                SqliteCommand c = new(
                    @"select circle.id, circle.name from circle 
                      inner join artist_circle_rel on circle.id = artist_circle_rel.circle_id 
                      where artist_circle_rel.artist_id = @artist;", conn);
                c.Parameters.AddWithValue("@artist", artist.Id);
                SqliteDataReader r = c.ExecuteReader();
                List<CircleModel> circles = new();

                while (r.Read())
                {
                    int id = r.GetInt32("id");
                    string name = r.GetString("name");
                    circles.Add(new CircleModel { Id = id, Name = name });
                }
                return circles;
            }
        }

        public Dictionary<int, int> GetBookCountOfArtist()
        {
            using(SqliteConnection conn = new(connStr))
            {
                conn.Open();
                SqliteCommand c = new("select count(artist) as count, artist from books group by artist;", conn);
                SqliteDataReader r = c.ExecuteReader();

                Dictionary<int, int> count = new();

                while(r.Read())
                {
                    count.Add(r.GetInt32("artist"), r.GetInt32("count"));
                }

                return count;
            }
        }
    }

    // Dictionary wrapper
    internal class ModelDict<Tkey, TValue>: Dictionary<Tkey, TValue> where Tkey: notnull where TValue: class{

        public new TValue Add(Tkey key,  TValue value)
        {
            base.Add(key, value);
            return value;
        }
        public TValue? GetOrNull(Tkey key)
        {
            return TryGetValue(key, out TValue? value) ? value : null;
        }
    }
}
