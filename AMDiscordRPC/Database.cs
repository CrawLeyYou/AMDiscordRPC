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
        private static readonly Dictionary<string, string> sqlMap = new Dictionary<string, string>()
        {
            {"coverTable", "album TEXT PRIMARY KEY NOT NULL, source TEXT, redirURL TEXT DEFAULT 'https://music.apple.com/home', animated BOOLEAN CHECK (animated IN (0,1)) DEFAULT 0, streamURL TEXT, animatedURL TEXT" },
            {"creds", "S3_accessKey TEXT, S3_secretKey TEXT, S3_serviceURL TEXT, S3_bucketName TEXT, S3_isSpecificKey BOOLEAN CHECK (S3_isSpecificKey IN (0,1)), S3_test TEXT" },
            {"logs", "timestamp INTEGER, type TEXT, occuredAt TEXT, message TEXT" }
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
                CheckColumns("coverTable");
                try
                {
                    //CheckForeignKeys(); we don't have use case for relationships rn so no need to waste resources on this check
                    CheckTablesAndColumns();
                    
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

        private static void CheckForeignKeys()
        {
            ExecuteNonQueryCommand("PRAGMA foreign_keys = on");
            if (ExecuteScalarCommand("PRAGMA foreign_keys") == "1") log.Debug("Foreign Keys enabled.");
            else throw new Exception("Foreign Keys are not supported / somehow unable to enable.");
        }

        private static void CheckTablesAndColumns()
        {
            SQLiteDataReader data = ExecuteReaderCommand("PRAGMA table_list");
            Dictionary<string, int> tablesAndColumns = new Dictionary<string, int>();
            List<string> missingTables = new List<string>();
            List<string> missingColumns = new List<string>();

            while (data.Read())
            {
                tablesAndColumns.Add(data.GetString(1), data.GetInt32(3));
            }

            foreach (var item in sqlMap.Keys.ToArray())
            {
                if (tablesAndColumns.ContainsKey(item))
                {
                    if (sqlMap[item].Split(new[] { ", " }, StringSplitOptions.None).Length != tablesAndColumns[item])
                        missingColumns.Add(item);
                }
                else missingTables.Add(item);
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
                    ExecuteNonQueryCommand($"CREATE TABLE IF NO EXISTS {item}({sqlMap[item]})");
                }
            }
            else log.Debug("No missing table found.");

            if (missingColumns.Count != 0)
            {
                log.Debug($"Missing columns found in: {string.Join(", ", missingColumns)}");
            }
        }

        private static void CheckColumns(string table)
        {
            SQLiteDataReader data = ExecuteReaderCommand($"PRAGMA table_info({table})");
            Dictionary<string, ColumnInfo> tableData = new Dictionary<string, ColumnInfo>();

            while (data.Read())
            {
               tableData.Add(data.GetString(1), new ColumnInfo(data.GetString(2), data.GetBoolean(3), (!data.IsDBNull(4)) ? data.GetString(4) : null, data.GetBoolean(5)));
            }
            log.Debug(tableData["animatedURL"].defaultValue);
            log.Debug(ConvertSQLStringToColumnInfo(sqlMap["coverTable"])["animatedURL"].defaultValue);
        }

        private static Dictionary<string, ColumnInfo> ConvertSQLStringToColumnInfo(string sqlStr)
        {
            Dictionary<string, ColumnInfo> columnsMap = new Dictionary<string, ColumnInfo>();
            string[] columns = sqlStr.Split(new[] { ", " }, StringSplitOptions.None);
            foreach (var column in columns)
            {
                string[] splitStr = column.Split(' ');
                columnsMap.Add(splitStr[0], new ColumnInfo(
                    splitStr[1],
                    column.Contains("NOT NULL"),
                    (column.Contains("DEFAULT")) ? column.Split(new[] { "DEFAULT " }, StringSplitOptions.None)[1] : null, //This is not a proper way to do this but it works for now (DEFAULT value must be on the last section of the SQL Command)
                    column.Contains("PRIMARY KEY")
                ));
            }
            return columnsMap;
        }

        public static string ExecuteScalarCommand(string command)
        {
            try
            {
                SQLiteCommand cmd = new SQLiteCommand($@"{command}", sqlite);
                return cmd.ExecuteScalar().ToString();
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

            public ColumnInfo(string type, bool nullCheck, string defaultValue, bool primaryKey)
            {
                this.type = type;
                this.nullCheck = nullCheck;
                this.defaultValue = defaultValue;
                this.primaryKey = primaryKey;
            }
        }
    }
}
