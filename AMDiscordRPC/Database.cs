using Amazon.Runtime.Internal.Transform;
using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Linq;
using static AMDiscordRPC.Globals;

namespace AMDiscordRPC
{
    internal class Database
    {
        private static SQLiteConnection sqlite;
        public static readonly Dictionary<string, string> sqlMap = new Dictionary<string, string>()
        {
            //{"coverTable", "album TEXT PRIMARY KEY NOT NULL, source TEXT, redirURL TEXT DEFAULT 'https://music.apple.com/home', artistRedirURL TEXT DEFAULT 'https://music.apple.com/home', artistSource TEXT, animated BOOLEAN CHECK (animated IN (0,1)) DEFAULT NULL, streamURL TEXT, animatedURL TEXT" },
            {"coverTableNew", "coverID INTEGER PRIMARY KEY AUTOINCREMENT, staticCoverURL TEXT NOT NULL, isAnimated BOOLEAN CHECK (isAnimated IN (0,1)) DEFAULT NULL, streamURL TEXT, animatedURL TEXT"},
            {"artistTable", "artistID INTEGER PRIMARY KEY AUTOINCREMENT, artistName TEXT NOT NULL, artistRedirURL TEXT DEFAULT 'https://music.apple.com/home', artistProfileSource TEXT"},
            {"albumTable", "albumID INTEGER PRIMARY KEY AUTOINCREMENT, albumName TEXT NOT NULL, albumURL TEXT UNIQUE, isSingle BOOLEAN CHECK (isSingle IN (0,1)), coverID INTEGER, artistID INTEGER, FOREIGN KEY (coverID) REFERENCES coverTableNew(coverID), FOREIGN KEY (artistID) REFERENCES artistTable(artistID)"},
            {"songTable", "songTitle TEXT, songURL TEXT, albumID INTEGER, artistID INTEGER, FOREIGN KEY (albumID) REFERENCES albumTable(albumID), FOREIGN KEY (artistID) REFERENCES artistTable(artistID)"},
            {"creds", "S3_accessKey TEXT, S3_secretKey TEXT, S3_serviceURL TEXT, S3_bucketName TEXT, S3_bucketURL TEXT, S3_isSpecificKey BOOLEAN CHECK (S3_isSpecificKey IN (0,1)), FFmpegPath TEXT" },
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
            ExecuteNonQueryCommand($"UPDATE coverTable SET ({string.Join(", ", data.GetNotNullKeys())}) = ({string.Join(", ", data.GetNotNullValues())}) WHERE album = '{data.album}'");
        }

        public static void CheckAndInsertAlbum(string album)
        {
            if (ExecuteScalarCommand($"SELECT album from coverTable WHERE album = '{album}'") == null)
                ExecuteNonQueryCommand($"INSERT INTO coverTable(album) VALUES ('{album}')");
        }

        public static SQLCoverResponse GetAlbumDataFromSQL(string album)
        {
            using (SQLiteDataReader reader = ExecuteReaderCommand($"SELECT * FROM coverTable WHERE album = '{album}' LIMIT 1"))
            {
                while (reader.Read())
                {
                    return new SQLCoverResponse(
                        reader.GetString(0),
                        ((!reader.IsDBNull(1)) ? reader.GetString(1) : null),
                        reader.GetString(2),
                        ((!reader.IsDBNull(3)) ? reader.GetBoolean(3) : null),
                        ((!reader.IsDBNull(4)) ? reader.GetString(4) : null),
                        ((!reader.IsDBNull(5)) ? reader.GetString(5) : null),
                        reader.GetString(6)
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

        public static object ExecuteScalarCommand(string command)
        {
            try
            {
                SQLiteCommand cmd = new SQLiteCommand($@"{command}", sqlite);
                return cmd.ExecuteScalar();
            }
            catch (Exception ex)
            {
                log.Debug($"An error occured while executing command: {ex}");
                return null;
            }
        }

        public static SQLiteDataReader ExecuteReaderCommand(string command)
        {
            try
            {
                SQLiteCommand cmd = new SQLiteCommand($@"{command}", sqlite);
                return cmd.ExecuteReader();
            }
            catch (Exception ex)
            {
                log.Debug($"An error occured while executing command: {ex}");
                return null;
            }
        }

        public static int ExecuteNonQueryCommand(string command)
        {
            try
            {
                SQLiteCommand cmd = new SQLiteCommand($@"{command}", sqlite);
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

        public class ForeignKey
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
    }
}
