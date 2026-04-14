using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Linq;
using static AMDiscordRPC.Globals;

namespace AMDiscordRPC
{
    public class Database
    {
        private static SQLiteConnection sqlite;
        public static readonly Dictionary<string, string> sqlMap = new Dictionary<string, string>()
        {
            {"coverTable", "album TEXT PRIMARY KEY NOT NULL, source TEXT, redirURL TEXT DEFAULT 'https://music.apple.com/home', artistRedirURL TEXT DEFAULT 'https://music.apple.com/home', artistSource TEXT, animated BOOLEAN CHECK (animated IN (0,1)) DEFAULT NULL, streamURL TEXT, animatedURL TEXT" }, // will be replaced
            {"coverTableNew", "coverID INTEGER PRIMARY KEY AUTOINCREMENT, staticCoverURL TEXT NOT NULL UNIQUE, isAnimated BOOLEAN CHECK (isAnimated IN (0,1)) DEFAULT NULL, streamURL TEXT, animatedURL TEXT"},
            {"artistTable", "artistID INTEGER PRIMARY KEY AUTOINCREMENT, artistName TEXT NOT NULL, artistRedirURL TEXT DEFAULT 'https://music.apple.com/home', artistProfileSource TEXT"},
            {"albumTable", "albumID INTEGER PRIMARY KEY AUTOINCREMENT, albumName TEXT NOT NULL, albumURL TEXT UNIQUE, isSingle BOOLEAN CHECK (isSingle IN (0,1)), coverID INTEGER, artistID INTEGER, FOREIGN KEY (coverID) REFERENCES coverTableNew(coverID), FOREIGN KEY (artistID) REFERENCES artistTable(artistID)"},
            {"songTable", "songTitle TEXT, songURL TEXT UNIQUE, albumID INTEGER, artistID INTEGER, FOREIGN KEY (albumID) REFERENCES albumTable(albumID), FOREIGN KEY (artistID) REFERENCES artistTable(artistID)"},
            {"creds", "S3_accessKey TEXT, S3_secretKey TEXT, S3_serviceURL TEXT, S3_bucketName TEXT, S3_bucketURL TEXT, S3_isSpecificKey BOOLEAN CHECK (S3_isSpecificKey IN (0,1)), FFmpegPath TEXT, LastFMToken TEXT" },
            {"logs", "timestamp INTEGER, type TEXT, occuredAt TEXT, message TEXT" },
            {"clientSettings", "smallImage INTEGER"}
        };

        private static void InitDatabase()
        {
            try
            {
                sqlite = new SQLiteConnection("Data Source=AMDiscordRPC.db");
                sqlite.Open();
                log.Debug("Database connection successful.");
            }
            catch (Exception e)
            {
                sqlite = null;
                log.Error($"An error occured while connecting to database: {e}");
            }
        }

        public static void CheckDatabaseIntegrity()
        {
            InitDatabase();
            if (sqlite != null)
            {
                try
                {
                    CheckForeignKeys();
                    CheckTables();
                    CheckColumns();
                }
                catch (Exception e)
                {
                    log.Error($"An error occured in Integrity Checks. {e}");
                }
            }
        }

        private static void CreateDatabase()
        {
            foreach (var item in sqlMap)
            {
                ExecuteNonQueryCommand($"CREATE TABLE IF NOT EXISTS {item.Key}({item.Value})");
            }
        }

        // Note: source, streamurl, animated, animatedUrl can be stored in external table so we decrease the file size of database
        private static void CheckForeignKeys()
        {
            ExecuteNonQueryCommand("PRAGMA foreign_keys = on");
            if (ExecuteScalarCommand("PRAGMA foreign_keys").ToString() == "1") log.Debug("Foreign Keys enabled.");
            else throw new Exception("Foreign Keys are not supported / somehow unable to enable.");
        }

        private static void CheckTables()
        {
            SQLiteDataReader data = ExecuteReaderCommand("PRAGMA table_list");
            Dictionary<string, int> tablesAndColumns = new Dictionary<string, int>();
            List<string> missingTables = new List<string>();

            while (data.Read())
            {
                tablesAndColumns.Add(data.GetString(1), data.GetInt32(3));
            }

            foreach (var item in sqlMap.Keys.ToArray())
            {
                if (!tablesAndColumns.ContainsKey(item)) missingTables.Add(item);
            }

            if (missingTables.Count == sqlMap.Keys.Count)
            {
                log.Info("Creating database.");
                CreateDatabase();
            }
            else if (missingTables.Count != 0)
            {
                log.Warn($"These tables are missing: {string.Join(", ", missingTables)} creating them.");
                foreach (var item in missingTables)
                {
                    ExecuteNonQueryCommand($"CREATE TABLE IF NOT EXISTS {item}({sqlMap[item]})");
                }
            }
            else log.Debug("No missing table found.");
        }

        public static void UpdateAlbum(SQLCoverResponse data)
        {
            ExecuteNonQueryCommand($"UPDATE coverTable SET ({string.Join(", ", data.GetNotNullKeys())}) = ({string.Join(", ", data.GetNotNullValues())}) WHERE album = @album", new[] { new SQLiteParameter("@album", data.album)});
        }

        public static void InsertAlbum(SQLCoverResponse data)
        {
            if (ExecuteScalarCommand($"SELECT album from coverTable WHERE album = @album", new[] { new SQLiteParameter("@album", data.album) }) == null)
                ExecuteNonQueryCommand($@"INSERT INTO coverTable(album, {string.Join(", ", data.GetNotNullKeys())}) VALUES (@album, {string.Join(", ", data.GetNotNullValues())})", new[] {new SQLiteParameter("@album", data.album)});
        }

        public static int InsertAlbumNew(SQLAlbumData data)
        {
            using (SQLiteDataReader reader = ExecuteReaderCommand($"SELECT albumID FROM albumTable WHERE albumURL = @albumURL", new[] { new SQLiteParameter("@albumURL", data.albumURL) }))
            {
                if (!reader.HasRows)
                {
                    return Convert.ToInt32(ExecuteScalarCommand($@"INSERT INTO albumTable(albumName, albumURL, isSingle, coverID, artistID) VALUES (@albumName, @albumURL, @isSingle, @coverID, @artistID); SELECT last_insert_rowid();", new[] { new SQLiteParameter("@albumName", data.albumName), new SQLiteParameter("@albumURL", data.albumURL), new SQLiteParameter("@isSingle", data.isSingle), new SQLiteParameter("@coverID", data.coverID), new SQLiteParameter("@artistID", data.artistID) }));
                }
                else
                {
                    reader.Read();
                    return reader.GetInt32(0);
                }
            }
        }

        public static int InsertArtist(SQLArtistData data)
        {
            using (SQLiteDataReader reader = ExecuteReaderCommand($"SELECT artistID FROM artistTable WHERE artistRedirUrl = @artistRedirURL", new[] { new SQLiteParameter("@artistRedirURL", data.artistRedirURL) }))
            {
                if (!reader.HasRows)
                {
                    return Convert.ToInt32(ExecuteScalarCommand($@"INSERT INTO artistTable(artistName, artistRedirURL, artistProfileSource) VALUES (@artistName, @artistRedirURL, @artistProfileSource); SELECT last_insert_rowid();", new[] { new SQLiteParameter("@artistName", data.artistName), new SQLiteParameter("@artistRedirURL", data.artistRedirURL), new SQLiteParameter("@artistProfileSource", data.artistProfileSource) }));
                }
                else
                {
                    reader.Read();
                    return reader.GetInt32(0);
                }
            }
        }

        public static void InsertSong(SQLSongData data)
        {
            if (ExecuteScalarCommand($"SELECT songTitle FROM songTable WHERE songURL = @songURL AND albumID = @albumID AND artistID = @artistID", new[] { new SQLiteParameter("@songURL", data.songURL), new SQLiteParameter("@albumID", data.albumID), new SQLiteParameter("@artistID", data.artistID) }) == null)
                ExecuteNonQueryCommand($@"INSERT INTO songTable(songTitle, songURL, albumID, artistID) VALUES (@songTitle, @songURL, @albumID, @artistID)", new[] { new SQLiteParameter("@songTitle", data.songTitle), new SQLiteParameter("@songURL", data.songURL), new SQLiteParameter("@albumID", data.albumID), new SQLiteParameter("@artistID", data.artistID) });
        }

        public static int InsertCover(SQLCoverData data)
        {
            using (SQLiteDataReader reader = ExecuteReaderCommand($"SELECT coverID FROM coverTableNew WHERE staticCoverURL = @staticCoverURL", new[] { new SQLiteParameter("@staticCoverURL", data.staticCoverURL) }))
            {
                if (!reader.HasRows)
                {
                    return Convert.ToInt32(ExecuteScalarCommand($@"INSERT INTO coverTableNew(staticCoverURL, isAnimated, streamURL, animatedURL) VALUES (@staticCoverURL, @isAnimated, @streamURL, @animatedURL); SELECT last_insert_rowid();", new[] { new SQLiteParameter("@staticCoverURL", data.staticCoverURL), new SQLiteParameter("@isAnimated", data.isAnimated), new SQLiteParameter("@streamURL", data.streamURL), new SQLiteParameter("@animatedURL", data.animatedURL) }));
                }
                else
                {
                    reader.Read();
                    return reader.GetInt32(0);
                }
            }
        }

        public static void InsertNew(SQLSongResponse SQLSongResponse)
        {
            using (var transaction = sqlite.BeginTransaction())
            {
                try
                {
                    int coverID = InsertCover(SQLSongResponse.cover);
                    int artistID = InsertArtist(SQLSongResponse.artist);
                    SQLSongResponse.album.coverID = coverID;
                    SQLSongResponse.album.artistID = artistID;
                    int albumID = InsertAlbumNew(SQLSongResponse.album);
                    SQLSongResponse.song.albumID = albumID;
                    SQLSongResponse.song.artistID = artistID;
                    InsertSong(SQLSongResponse.song);
                    transaction.Commit();
                }
                catch (Exception ex)
                {
                    log.Error($"An error occured while inserting data: {ex}");
                    transaction.Rollback();
                }
            }
        }

        public static SQLCoverResponse GetAlbumDataFromSQL(string album)
        {
            using (SQLiteDataReader reader = ExecuteReaderCommand($"SELECT * FROM coverTable WHERE album = @album LIMIT 1", new[] { new SQLiteParameter("@album", album) }))
            {
                while (reader.Read())
                {
                    return new SQLCoverResponse(
                        reader.GetString(0),
                        !reader.IsDBNull(1) ? reader.GetString(1) : null,
                        reader.GetString(2),
                        !reader.IsDBNull(3) ? reader.GetBoolean(3) : null,
                        !reader.IsDBNull(4) ? reader.GetString(4) : null,
                        !reader.IsDBNull(5) ? reader.GetString(5) : null,
                        reader.GetString(6)
                        );
                }
            }
            return null;
        }

        public static SQLRPCResponse GetSongFromDB(string song, string album, string artist)
        {
            string cmd = @"
            SELECT
                IIF(coverTableNew.isAnimated = 1, coverTableNew.animatedURL, coverTableNew.staticCoverURL) as coverURL,
                artistTable.artistRedirURL as artistRedirURL,
                artistTable.artistProfileSource as artistProfileSource,
                albumTable.albumURL,
                songTable.songURL
            FROM songTable
                INNER JOIN albumTable on albumTable.albumID = songTable.albumID
                INNER JOIN artistTable on artistTable.artistID = albumTable.artistID
                INNER JOIN coverTableNew on coverTableNew.coverID = albumTable.coverID
            WHERE
                artistTable.artistName = @artist
                AND songTable.songTitle = @song
                AND albumTable.albumName = @album
            LIMIT 1;
            ";
            using (SQLiteDataReader reader = ExecuteReaderCommand(cmd, new[] { new SQLiteParameter("@album", album), new SQLiteParameter("@song", song), new SQLiteParameter("@artist", artist) }))
            {
                if (!reader.HasRows) return null;
                while (reader.Read())
                {
                    return new SQLRPCResponse(
                            reader.IsDBNull(0) ? null : reader.GetString(0),
                            reader.IsDBNull(1) ? null : reader.GetString(1),
                            reader.IsDBNull(2) ? null : reader.GetString(2),
                            reader.IsDBNull(3) ? null : reader.GetString(3),
                            reader.IsDBNull(4) ? null : reader.GetString(4)
                    );
                }
            }
            return null;
        }

        private static void CheckColumns()
        {
            foreach (var table in sqlMap.Keys)
            {
                SQLiteDataReader data = ExecuteReaderCommand($"SELECT * FROM sqlite_master");
                Dictionary<string, ColumnInfo> tableData = new Dictionary<string, ColumnInfo>();

                while (data.Read())
                {
                    if (data.GetString(0) == "table" && data.GetString(2) == table)
                    { 
                        string sqlStr = string.Join("(", data.GetString(4).Split(new[] { "CREATE TABLE " }, StringSplitOptions.None)[1].Split('(').Skip(1)).TrimEnd(1);
                        var temp = ConvertSQLStringToColumnInfo(sqlStr);
                        foreach (var keyValuePair in temp)
                        {
                            //log.Debug($"{keyValuePair.Key}, autoIncrement: {keyValuePair.Value.isAutoIncrementing}, defaultValue: {keyValuePair.Value.defaultValue}, foreignKey: [Key: {keyValuePair.Value.foreignKey?.key}, refColumn: {keyValuePair.Value.foreignKey?.refColumn}, refTable: {keyValuePair.Value.foreignKey?.refTable}], nullCheck: {keyValuePair.Value.nullCheck}, primaryKey: {keyValuePair.Value.primaryKey}, type: {keyValuePair.Value.type}");
                            tableData.Add(keyValuePair.Key, keyValuePair.Value);
                        }
                    }
                }

                foreach (var item in ConvertSQLStringToColumnInfo(sqlMap[table]))
                {
                    ColumnInfo column = (tableData.Keys.Contains(item.Key) ? tableData[item.Key] : null);
                    string SQLInfo = Array.Find(sqlMap[table].Split(new[] { ", " }, StringSplitOptions.None), s => s.Contains(item.Key));
                    if (!item.Value.Equals(column) && column != null)
                    {
                        log.Debug($"Corrupted/Outdated column:{SQLInfo.Split(' ')[0]} found.");
                        if (!item.Value.primaryKey && ((item.Value.nullCheck && item.Value.defaultValue != null) || !item.Value.nullCheck) && item.Value.foreignKey == null)
                        {
                            ExecuteNonQueryCommand($"ALTER TABLE {table} DROP COLUMN {SQLInfo.Split(' ')[0]}");
                            ExecuteNonQueryCommand($"ALTER TABLE {table} ADD COLUMN {SQLInfo}");
                            log.Info($"Recreated column: {SQLInfo.Split(' ')[0]}");
                        }
                        else
                        {
                            // Recovery functionality will be added next release.
                        }
                    }
                    else if (column == null && !item.Value.primaryKey)
                    {
                        ExecuteNonQueryCommand($"ALTER TABLE {table} ADD COLUMN {SQLInfo}");
                    }
                }
            }
        }

        private static Dictionary<string, ColumnInfo> ConvertSQLStringToColumnInfo(string sqlStr)
        {
            Dictionary<string, ColumnInfo> columnsMap = new Dictionary<string, ColumnInfo>();
            string[] columns = sqlStr.Split(new[] { ", " }, StringSplitOptions.None);
            foreach (var column in columns)
            {
                string[] splitStr = column.Split(' ');
                if (splitStr[0] == "FOREIGN") continue;
                columnsMap.Add(splitStr[0], new ColumnInfo(
                    splitStr[1],
                    column.Contains("NOT NULL"),
                    (column.Contains("DEFAULT")) ? column.Split(new[] { "DEFAULT " }, StringSplitOptions.None)[1] : null, //This is not a proper way to do this but it works for now (DEFAULT value must be on the last section of the SQL Command)
                    column.Contains("PRIMARY KEY"),
                    column.Contains("AUTOINCREMENT"),
                    (sqlStr.Contains($"FOREIGN KEY ({splitStr[0]})")) ? new ForeignKey(splitStr[0], columns.Where(a => a.Contains($"FOREIGN KEY ({splitStr[0]})")).First().Split(new[] { "REFERENCES " }, StringSplitOptions.None)[1].Split('(')[0], columns.Where(a => a.Contains($"FOREIGN KEY ({splitStr[0]})")).First().Split(new[] { "REFERENCES " }, StringSplitOptions.None)[1].Split('(')[1].Split(')')[0]) : null
                ));
            }
            return columnsMap;
        }

        public static object ExecuteScalarCommand(string command, SQLiteParameter[] parameters = null)
        {
            try
            {
                SQLiteCommand cmd = new SQLiteCommand(command, sqlite);
                if (parameters != null) cmd.Parameters.AddRange(parameters);
                return cmd.ExecuteScalar();
            }
            catch (Exception ex)
            {
                log.Debug($"An error occured while executing command: {ex}");
                return null;
            }
        }

        public static SQLiteDataReader ExecuteReaderCommand(string command, SQLiteParameter[] parameters = null)
        {
            try
            {
                SQLiteCommand cmd = new SQLiteCommand(command, sqlite);
                if (parameters != null) cmd.Parameters.AddRange(parameters);
                return cmd.ExecuteReader();
            }
            catch (Exception ex)
            {
                log.Debug($"An error occured while executing command: {ex}");
                return null;
            }
        }

        public static int ExecuteNonQueryCommand(string command, SQLiteParameter[] parameters = null)
        {
            try
            {
                SQLiteCommand cmd = new SQLiteCommand(command, sqlite);
                if (parameters != null) cmd.Parameters.AddRange(parameters);
                return cmd.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                log.Debug($"An error occured while executing command: {ex}");
                return -1;
            }
        }

        private class ColumnInfo
        {
            public string type { get; set; }
            public bool nullCheck { get; set; }
            public string defaultValue { get; set; }
            public bool primaryKey { get; set; }
            public bool isAutoIncrementing { get; set; }
            public ForeignKey foreignKey { get; set; }

            public ColumnInfo(string type, bool nullCheck, string defaultValue, bool primaryKey, bool isAutoIncrementing, ForeignKey foreignKey = null)
            {
                this.type = type;
                this.nullCheck = nullCheck;
                this.defaultValue = defaultValue;
                this.primaryKey = primaryKey;
                this.isAutoIncrementing = isAutoIncrementing;
                this.foreignKey = foreignKey;
            }

            public override bool Equals(object obj)
            {
                return obj is ColumnInfo other &&
                       type == other.type &&
                       nullCheck == other.nullCheck &&
                       defaultValue == other.defaultValue &&
                       primaryKey == other.primaryKey &&
                       isAutoIncrementing == other.isAutoIncrementing &&
                       foreignKey == other.foreignKey;
            }
        }

        private class ForeignKey
        {
            public string key { get; set; }
            public string refTable { get; set; }
            public string refColumn { get; set; }

            public ForeignKey(string key, string refTable, string refColumn)
            {
                this.key = key;
                this.refTable = refTable;
                this.refColumn = refColumn;
            }
        }

        public class SQLCoverResponse
        {
            public string album { get; set; }
            public string source { get; set; }
            public string redirURL { get; set; }
            public bool? animated { get; set; }
            public string streamURL { get; set; }
            public string animatedURL { get; set; }
            public string artistRedirURL { get; set; }


            public SQLCoverResponse(string album = null, string source = null, string redirURL = null, bool? animated = null, string streamURL = null, string animatedURL = null, string artistRedirURL = null)
            {
                this.album = album;
                this.source = source;
                this.redirURL = redirURL;
                this.animated = animated;
                this.streamURL = streamURL;
                this.animatedURL = animatedURL;
                this.artistRedirURL = artistRedirURL;
            }

            public List<string> GetNotNullKeys()
            {
                return GetType().GetProperties().Where(s => s.GetValue(this) != null && s.GetValue(this) != this.album).Select(p => p.Name).ToList();
            }

            public List<object> GetNotNullValues()
            {
                return GetType().GetProperties().Where(s => s.GetValue(this) != null && s.GetValue(this) != this.album).Select(p => (p.PropertyType == typeof(string)) ? $"'{p.GetValue(this)}'" : p.GetValue(this)).ToList();
            }

        }

        public class SQLSongResponse
        {
            public SQLCoverData? cover;
            public SQLAlbumData? album;
            public SQLArtistData? artist;
            public SQLSongData? song;

            public SQLSongResponse(SQLCoverData cover, SQLAlbumData album, SQLArtistData artist, SQLSongData song)
            {
                this.cover = cover;
                this.album = album;
                this.artist = artist;
                this.song = song;
            }
        }
        public class SQLRPCResponse
        {
            public string? coverURL { get; set; }
            public string? artistRedirURL { get; set; }
            public string? artistProfileSource { get; set; }
            public string? albumURL { get; set; }
            public string? songURL { get; set; }

            public SQLRPCResponse(string? coverURL = null, string? artistRedirURL = null, string? artistProfileSource = null, string? albumURL = null, string? songURL = null)
            {
                this.coverURL = coverURL;
                this.artistRedirURL = artistRedirURL;
                this.artistProfileSource = artistProfileSource;
                this.albumURL = albumURL;
                this.songURL = songURL;
            }
        }

        public class SQLCoverData
        {
            public int id;
            public string staticCoverURL;
            public bool? isAnimated;
            public string? streamURL;
            public string? animatedURL;

            public SQLCoverData(int id, string staticCoverURL, bool? isAnimated, string? streamURL, string? animatedURL)
            {
                this.id = id;
                this.staticCoverURL = staticCoverURL;
                this.isAnimated = isAnimated;
                this.streamURL = streamURL;
                this.animatedURL = animatedURL;
            }
        }

        public class SQLArtistData
        {
            public int id;
            public string artistName;
            public string artistRedirURL;
            public string? artistProfileSource;

            public SQLArtistData(int id, string artistName, string artistRedirURL, string? artistProfileSource)
            {
                this.id = id;
                this.artistName = artistName;
                this.artistRedirURL = artistRedirURL;
                this.artistProfileSource = artistProfileSource;
            }
        }

        public class SQLAlbumData
        {
            public int id;
            public string albumName;
            public string albumURL;
            public bool isSingle;
            public int? coverID; // Foreign Key
            public int? artistID; // Foreign Key

            public SQLAlbumData(int id, string albumName, string albumURL, bool isSingle, int coverID, int artistID)
            {
                this.id = id;
                this.albumName = albumName;
                this.albumURL = albumURL;
                this.isSingle = isSingle;
                this.coverID = coverID;
                this.artistID = artistID;
            }
        }

        public class SQLSongData
        {
            public string songTitle;
            public string songURL;
            public int? albumID; // Foreign Key
            public int? artistID; // Foreign Key

            public SQLSongData(string songTitle, string songURL, int albumID, int artistID)
            {
                this.songTitle = songTitle;
                this.songURL = songURL;
                this.albumID = albumID;
                this.artistID = artistID;
            }
        }
    }
}
