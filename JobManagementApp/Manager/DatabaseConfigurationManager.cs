using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using JobManagementApp.Manager;

namespace JobManagementApp.Configuration
{
    /// <summary>
    /// データベース設定情報クラス - スキーマ指定対応版
    /// </summary>
    public class DatabaseSettings
    {
        public string Name { get; set; }
        public bool IsDefault { get; set; }
        public string DataSource { get; set; }
        public string UserId { get; set; }
        public string Password { get; set; }
        public string Schema { get; set; } 
        public int ConnectionTimeout { get; set; } = 30;
        public int CommandTimeout { get; set; } = 300;
        public int RetrySleep { get; set; } = 1000;
        public int RetryCount { get; set; } = 3;
        public string Description { get; set; }
        
        /// <summary>
        /// Oracle接続文字列を生成
        /// </summary>
        public string GetConnectionString()
        {
            return $"Data Source={DataSource};User Id={UserId};Password={Password};Connection Timeout={ConnectionTimeout};";
        }
        
        /// <summary>
        /// 設定の妥当性をチェック
        /// </summary>
        public bool IsValid()
        {
            return !string.IsNullOrWhiteSpace(DataSource) &&
                   !string.IsNullOrWhiteSpace(UserId) &&
                   !string.IsNullOrWhiteSpace(Password) &&
                   ConnectionTimeout > 0 &&
                   CommandTimeout > 0 &&
                   RetrySleep >= 0 &&
                   RetryCount >= 0;
        }

        /// <summary>
        /// スキーマが設定されているかチェック
        /// </summary>
        public bool HasSchema()
        {
            return !string.IsNullOrWhiteSpace(Schema);
        }

        /// <summary>
        /// スキーマ変更用のSQL文を生成
        /// </summary>
        public string GetSchemaChangeSQL()
        {
            if (!HasSchema())
                return null;
            
            return $"ALTER SESSION SET CURRENT_SCHEMA = {Schema}";
        }
    }

    /// <summary>
    /// データベース設定管理クラス - XML設定ファイル対応（スキーマ指定対応版）
    /// </summary>
    public class DatabaseConfigurationManager
    {
        private static DatabaseConfigurationManager _instance;
        private static readonly object _lock = new object();
        
        private readonly Dictionary<string, DatabaseSettings> _databases;
        private DatabaseSettings _defaultDatabase;
        private readonly string _configFilePath;

        /// <summary>
        /// シングルトンインスタンス
        /// </summary>
        public static DatabaseConfigurationManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                        {
                            _instance = new DatabaseConfigurationManager();
                        }
                    }
                }
                return _instance;
            }
        }

        /// <summary>
        /// プライベートコンストラクタ
        /// </summary>
        private DatabaseConfigurationManager()
        {
            _databases = new Dictionary<string, DatabaseSettings>(StringComparer.OrdinalIgnoreCase);
            _configFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "DatabaseConfig.xml");
            
            LoadConfiguration();
        }

        /// <summary>
        /// XML設定ファイルから設定を読み込み
        /// </summary>
        private void LoadConfiguration()
        {
            try
            {
                LogFile.WriteLog($"データベース設定ファイルを読み込み中: {_configFilePath}");

                if (!File.Exists(_configFilePath))
                {
                    throw new FileNotFoundException($"設定ファイルが見つかりません: {_configFilePath}");
                }

                var xmlDoc = XDocument.Load(_configFilePath);
                var databasesElement = xmlDoc.Root?.Element("Databases");
                
                if (databasesElement == null)
                {
                    throw new InvalidOperationException("XML設定ファイルの形式が正しくありません: Databases要素が見つかりません");
                }

                _databases.Clear();
                _defaultDatabase = null;

                foreach (var dbElement in databasesElement.Elements("Database"))
                {
                    var settings = ParseDatabaseElement(dbElement);
                    
                    if (settings.IsValid())
                    {
                        _databases[settings.Name] = settings;
                        
                        // デフォルトデータベースの設定
                        if (settings.IsDefault || _defaultDatabase == null)
                        {
                            _defaultDatabase = settings;
                        }
                        
                        var schemaInfo = settings.HasSchema() ? $" (スキーマ: {settings.Schema})" : "";
                        LogFile.WriteLog($"データベース設定を読み込みました: {settings.Name} ({settings.DataSource}){schemaInfo}");
                    }
                    else
                    {
                        ErrLogFile.WriteLog($"無効なデータベース設定をスキップしました: {settings.Name}");
                    }
                }

                if (_databases.Count == 0)
                {
                    throw new InvalidOperationException("有効なデータベース設定が見つかりませんでした");
                }

                LogFile.WriteLog($"データベース設定の読み込みが完了しました。設定数: {_databases.Count}");
            }
            catch (Exception ex)
            {
                ErrLogFile.WriteLog($"データベース設定の読み込みエラー: {ex.Message}");
                throw new InvalidOperationException("データベース設定の読み込みに失敗しました", ex);
            }
        }

        /// <summary>
        /// XML要素からデータベース設定を解析 - スキーマ対応版
        /// </summary>
        private DatabaseSettings ParseDatabaseElement(XElement dbElement)
        {
            try
            {
                var settings = new DatabaseSettings
                {
                    Name = GetAttributeValue(dbElement, "name", ""),
                    IsDefault = GetAttributeValue(dbElement, "default", "false").Equals("true", StringComparison.OrdinalIgnoreCase),
                    DataSource = GetElementValue(dbElement, "DataSource", ""),
                    UserId = GetElementValue(dbElement, "UserId", ""),
                    Password = GetElementValue(dbElement, "Password", ""),
                    Schema = GetElementValue(dbElement, "Schema", ""),  // 🆕 スキーマ要素を追加
                    ConnectionTimeout = GetElementValueAsInt(dbElement, "ConnectionTimeout", 30),
                    CommandTimeout = GetElementValueAsInt(dbElement, "CommandTimeout", 300),
                    RetrySleep = GetElementValueAsInt(dbElement, "RetrySleep", 1000),
                    RetryCount = GetElementValueAsInt(dbElement, "RetryCount", 3),
                    Description = GetElementValue(dbElement, "Description", "")
                };

                return settings;
            }
            catch (Exception ex)
            {
                ErrLogFile.WriteLog($"データベース設定の解析エラー: {ex.Message}");
                return new DatabaseSettings { Name = "InvalidConfig" };
            }
        }

        /// <summary>
        /// XML要素の属性値を取得
        /// </summary>
        private string GetAttributeValue(XElement element, string attributeName, string defaultValue)
        {
            return element.Attribute(attributeName)?.Value ?? defaultValue;
        }

        /// <summary>
        /// XML要素の値を取得
        /// </summary>
        private string GetElementValue(XElement parent, string elementName, string defaultValue)
        {
            return parent.Element(elementName)?.Value?.Trim() ?? defaultValue;
        }

        /// <summary>
        /// XML要素の値を整数として取得
        /// </summary>
        private int GetElementValueAsInt(XElement parent, string elementName, int defaultValue)
        {
            var value = GetElementValue(parent, elementName, defaultValue.ToString());
            return int.TryParse(value, out int result) ? result : defaultValue;
        }

        /// <summary>
        /// デフォルトデータベース設定を取得
        /// </summary>
        public DatabaseSettings GetDefaultDatabase()
        {
            if (_defaultDatabase == null)
            {
                throw new InvalidOperationException("デフォルトデータベースが設定されていません");
            }
            return _defaultDatabase;
        }

        /// <summary>
        /// 指定された名前のデータベース設定を取得
        /// </summary>
        public DatabaseSettings GetDatabase(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return GetDefaultDatabase();
            }

            if (_databases.TryGetValue(name, out DatabaseSettings settings))
            {
                return settings;
            }

            throw new ArgumentException($"指定されたデータベース設定が見つかりません: {name}");
        }

        /// <summary>
        /// 利用可能なデータベース名の一覧を取得
        /// </summary>
        public IEnumerable<string> GetAvailableDatabaseNames()
        {
            return _databases.Keys.ToArray();
        }

        /// <summary>
        /// 全てのデータベース設定を取得
        /// </summary>
        public IEnumerable<DatabaseSettings> GetAllDatabases()
        {
            return _databases.Values.ToArray();
        }

        /// <summary>
        /// データベース設定が存在するかチェック
        /// </summary>
        public bool HasDatabase(string name)
        {
            return !string.IsNullOrWhiteSpace(name) && _databases.ContainsKey(name);
        }

        /// <summary>
        /// 設定ファイルを再読み込み
        /// </summary>
        public void ReloadConfiguration()
        {
            try
            {
                LogFile.WriteLog("データベース設定を再読み込みします");
                LoadConfiguration();
                LogFile.WriteLog("データベース設定の再読み込みが完了しました");
            }
            catch (Exception ex)
            {
                ErrLogFile.WriteLog($"データベース設定の再読み込みエラー: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 設定情報をログに出力（パスワードは除く）- スキーマ対応版
        /// </summary>
        public void LogConfigurationInfo()
        {
            try
            {
                LogFile.WriteLog("=== データベース設定情報 ===");
                LogFile.WriteLog($"設定ファイル: {_configFilePath}");
                LogFile.WriteLog($"データベース数: {_databases.Count}");
                
                foreach (var db in _databases.Values)
                {
                    var schemaInfo = db.HasSchema() ? $" スキーマ: {db.Schema}" : " スキーマ: 未設定";
                    LogFile.WriteLog($"  [{db.Name}] {db.DataSource} (ユーザー: {db.UserId}{schemaInfo})" + 
                                   (db.IsDefault ? " [デフォルト]" : "") +
                                   (!string.IsNullOrEmpty(db.Description) ? $" - {db.Description}" : ""));
                }
                LogFile.WriteLog("==============================");
            }
            catch (Exception ex)
            {
                ErrLogFile.WriteLog($"設定情報のログ出力エラー: {ex.Message}");
            }
        }

        /// <summary>
        /// 設定の妥当性をチェック
        /// </summary>
        public bool ValidateConfiguration()
        {
            try
            {
                if (_databases.Count == 0)
                {
                    ErrLogFile.WriteLog("データベース設定が1つも見つかりません");
                    return false;
                }

                if (_defaultDatabase == null)
                {
                    ErrLogFile.WriteLog("デフォルトデータベースが設定されていません");
                    return false;
                }

                foreach (var db in _databases.Values)
                {
                    if (!db.IsValid())
                    {
                        ErrLogFile.WriteLog($"無効なデータベース設定: {db.Name}");
                        return false;
                    }
                }

                LogFile.WriteLog("データベース設定の検証が完了しました");
                return true;
            }
            catch (Exception ex)
            {
                ErrLogFile.WriteLog($"設定検証エラー: {ex.Message}");
                return false;
            }
        }
    }
}