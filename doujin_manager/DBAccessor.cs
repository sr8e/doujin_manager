using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
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
                      );
                      create table if not exists book_artist_rel(
                        book_id integer not null,
                        artist_id integer not null,
                        foreign key(book_id) references books(id),
                        foreign key(artist_id) references artist(id),
                        primary key(book_id, artist_id)
                      );", conn
                );
                c.ExecuteNonQuery();
            }
        }

        // insertions
        private static int getInsertedId(SqliteConnection conn)
        {
            return getInsertedId(conn, null);
        }
        private static int getInsertedId(SqliteConnection conn, SqliteTransaction? t)
        {
            SqliteCommand c = new("select last_insert_rowid();", conn, t);
            SqliteDataReader r = c.ExecuteReader();
            r.Read();
            return r.GetInt32(0);
        }

        public int InsertBook(BookModel book)
        {
            using(SqliteConnection conn = new(connStr))
            { 
                conn.Open();
                using (SqliteTransaction t = conn.BeginTransaction())
                {
                    SqliteCommand c = new("insert into books (title, circle, date) values (@title, @circle, @date);", conn, t);
                    c.Parameters.AddRange(book.GetQueryParams());
                    c.ExecuteNonQuery();
                    book.Id = getInsertedId(conn, t);

                    insertRelation(conn, t, book);

                    t.Commit();
                }

                return book.Id;
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

        private void insertRelation(SqliteConnection conn, SqliteTransaction t, BookModel book)
        {
            SqliteCommand q = new("select * from artist_circle_rel where artist_id = @artist and circle_id = @circle;", conn, t);
            SqliteCommand cc = new("insert into artist_circle_rel (artist_id, circle_id) values (@artist, @circle);", conn, t);
            SqliteCommand cb = new("insert into book_artist_rel (book_id, artist_id) values(@book, @artist);", conn, t);

            SqliteParameter pa = new() { ParameterName = "@artist" };
            SqliteParameter pb = new() { ParameterName = "@book", Value = book.Id };
            SqliteParameter pc = new() { ParameterName = "@circle", Value = book.Circle.Id };

            q.Parameters.Add(pa);
            q.Parameters.Add(pc);
            cc.Parameters.Add(pa);
            cc.Parameters.Add(pc);
            cb.Parameters.Add(pa);
            cb.Parameters.Add(pb);

            foreach (ArtistModel ar in book.Artists)
            {
                pa.Value = ar.Id;
                // insert book relation first
                cb.ExecuteNonQuery();

                // check if artist-circle relation already exists
                SqliteDataReader r = q.ExecuteReader();
                if (!r.HasRows)
                {
                    cc.ExecuteNonQuery();
                }
                r.Close();
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
                    @"select books.id, title, artist.id as artist, artist.name as aname, circle, circle.name as cname, date 
                      from book_artist_rel 
                      inner join books on book_id = books.id 
                      inner join artist on artist_id = artist.id
                      inner join circle on books.circle = circle.id;", conn);

                ModelDict<int, BookModel> bookDict = new();
                ModelDict<int, ArtistModel> artistDict = new();
                ModelDict<int, CircleModel> circleDict = new();

                SqliteDataReader r = c.ExecuteReader();
                while(r.Read())
                {
                    int bookId = r.GetInt32("id");
                    int artistId = r.GetInt32("artist");
                    int circleId = r.GetInt32("circle");

                    ArtistModel ar = artistDict.GetOrNull(artistId) ?? artistDict.Add(artistId, new ArtistModel { Id = artistId, Name = r.GetString("aname") });
                    CircleModel ci = circleDict.GetOrNull(circleId) ?? circleDict.Add(circleId, new CircleModel { Id = circleId, Name = r.GetString("cname") });

                    BookModel b = bookDict.GetOrNull(bookId) ?? bookDict.Add(bookId, new BookModel
                    {
                        Id = bookId,
                        Title = r.GetString("title"),
                        Artists = new List<ArtistModel>(),
                        Circle = ci,
                        Date = r.IsDBNull("date") ? null: DateOnly.FromDateTime(r.GetDateTime("date"))
                    });
                    b.Artists.Add(ar);
                }
                return bookDict.Values.ToList();

            }
        }

        public List<BookModel> GetBooksOfArtist(ArtistModel artist) {
            using(SqliteConnection conn =new(connStr))
            {
                conn.Open();
                SqliteCommand c = new(
                    @"select books.id, title, circle, date, circle.name as cname from book_artist_rel
                      inner join books on book_id = books.id
                      inner join circle on books.circle = circle.id 
                      where book_artist_rel.artist_id = @artist;", conn);
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
                        Circle = ci,
                        Date = r.IsDBNull("date") ? null : DateOnly.FromDateTime(r.GetDateTime("date"))
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
                SqliteCommand c = new("select count(artist_id) as count, artist_id from book_artist_rel group by artist_id;", conn);
                SqliteDataReader r = c.ExecuteReader();

                Dictionary<int, int> count = new();

                while(r.Read())
                {
                    count.Add(r.GetInt32("artist_id"), r.GetInt32("count"));
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
