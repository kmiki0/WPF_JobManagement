using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Oracle.ManagedDataAccess.Client;
using Oracle.ManagedDataAccess.Types;
using DICSSLORA.ACmnFunc;
using DICSSLORA.ACmnIni;
using DICSSLORA.ACmnLog;
using JobManagementApp.Configuration;

namespace JobManagementApp.Manager
{
    /// <summary>
    /// データベース管理クラス - XML設定・複数データベース対応版
    /// </summary>
    public class DatabaseManager : IDisposable
    {
        #region フィールドとプロパティ

        public DICSSLORA.ACmnLog.clsMngLogFile pobjActLog { get; private set; }
        public DICSSLORA.ACmnLog.clsMngLogFile pobjErrLog { get; private set; }
        
        // 複数データベース接続管理
        private readonly Dictionary<string, OracleConnection> _connections;
        private readonly Dictionary<string, DatabaseSettings> _databaseSettings;
        private string _currentDatabaseName;
        private DatabaseSettings _currentDatabase;

        private static DatabaseManager _instance;
        private static readonly object _lock = new object();
        private bool _disposed = false;
        private readonly object _connectionLock = new object();

        // 接続状態管理
        private bool _isInitialized = false;
        private readonly Dictionary<string, bool> _connectionStates;
        private readonly Dictionary<string, DateTime> _lastConnectionChecks;
        private readonly TimeSpan _connectionCheckInterval = TimeSpan.FromMinutes(5);

        #endregion

        #region コンストラクタ・インスタンス管理

        /// <summary>
        /// プライベートコンストラクタ（シングルトンパターン）
        /// </summary>
        private DatabaseManager()
        {
            try
            {
                _connections = new Dictionary<string, OracleConnection>(StringComparer.OrdinalIgnoreCase);
                _databaseSettings = new Dictionary<string, DatabaseSettings>(StringComparer.OrdinalIgnoreCase);
                _connectionStates = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
                _lastConnectionChecks = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);

                InitializeComponents();
                LogFile.WriteLog("DatabaseManager インスタンスを正常に作成しました");
            }
            catch (Exception ex)
            {
                ErrLogFile.WriteLog($"DatabaseManager初期化エラー: {ex.Message}");
                throw new InvalidOperationException("DatabaseManagerの初期化に失敗しました", ex);
            }
        }

        /// <summary>
        /// シングルトンインスタンスの取得
        /// </summary>
        public static DatabaseManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                        {
                            _instance = new DatabaseManager();
                        }
                    }
                }
                return _instance;
            }
        }

        #endregion

        #region 初期化メソッド

        /// <summary>
        /// コンポーネントの初期化
        /// </summary>
        private void InitializeComponents()
        {
            try
            {
                // ログファイルの初期化
                InitializeLogFiles();

                // XML設定ファイルからデータベース設定を読み込み
                LoadDatabaseSettings();

                _isInitialized = true;
                LogFile.WriteLog("DatabaseManager コンポーネントの初期化が完了しました");
            }
            catch (Exception ex)
            {
                ErrLogFile.WriteLog($"InitializeComponents エラー: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// ログファイルの初期化
        /// </summary>
        private void InitializeLogFiles()
        {
            try
            {
                // 操作ログインスタンス生成 
                pobjActLog = LogFile.Instance;
                if (pobjActLog == null)
                {
                    throw new InvalidOperationException("操作ログインスタンスの取得に失敗しました");
                }

                // エラーログインスタンス生成
                pobjErrLog = ErrLogFile.Instance;
                if (pobjErrLog == null)
                {
                    throw new InvalidOperationException("エラーログインスタンスの取得に失敗しました");
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("ログファイルの初期化に失敗しました", ex);
            }
        }

        /// <summary>
        /// XML設定ファイルからデータベース設定を読み込み
        /// </summary>
        private void LoadDatabaseSettings()
        {
            try
            {
                var configManager = DatabaseConfigurationManager.Instance;
                
                // 設定の妥当性チェック
                if (!configManager.ValidateConfiguration())
                {
                    throw new InvalidOperationException("データベース設定の検証に失敗しました");
                }

                // 全データベース設定を読み込み
                foreach (var dbSettings in configManager.GetAllDatabases())
                {
                    _databaseSettings[dbSettings.Name] = dbSettings;
                    _connectionStates[dbSettings.Name] = false;
                    _lastConnectionChecks[dbSettings.Name] = DateTime.MinValue;
                    
                    LogFile.WriteLog($"データベース設定を登録しました: {dbSettings.Name} -> {dbSettings.Address}:{dbSettings.Port}/{dbSettings.ServiceName}");
                }

                // デフォルトデータベースを設定
                _currentDatabase = configManager.GetDefaultDatabase();
                _currentDatabaseName = _currentDatabase.Name;

                // 設定情報をログに出力
                configManager.LogConfigurationInfo();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("データベース設定の読み込みに失敗しました", ex);
            }
        }

        #endregion

        #region 接続管理メソッド

        /// <summary>
        /// データベース接続の確立（デフォルトデータベース）
        /// </summary>
        public bool EstablishConnection()
        {
            return EstablishConnection(_currentDatabaseName);
        }

        /// <summary>
        /// 指定されたデータベースへの接続を確立
        /// </summary>
        public bool EstablishConnection(string databaseName)
        {
            if (_disposed)
            {
                ErrLogFile.WriteLog("DatabaseManager は既に破棄されています");
                return false;
            }

            if (string.IsNullOrWhiteSpace(databaseName))
            {
                databaseName = _currentDatabaseName;
            }

            lock (_connectionLock)
            {
                try
                {
                    if (!_isInitialized)
                    {
                        ErrLogFile.WriteLog("DatabaseManager が初期化されていません");
                        return false;
                    }

                    if (!_databaseSettings.ContainsKey(databaseName))
                    {
                        ErrLogFile.WriteLog($"指定されたデータベース設定が見つかりません: {databaseName}");
                        return false;
                    }

                    // 既に接続済みの場合は健全性チェック
                    if (IsConnectionHealthy(databaseName))
                    {
                        LogFile.WriteLog($"データベース接続は既に確立されています: {databaseName}");
                        return true;
                    }

                    // 新規接続またはリトライ
                    return TryConnect(databaseName);
                }
                catch (Exception ex)
                {
                    ErrLogFile.WriteLog($"EstablishConnection エラー ({databaseName}): {ex.Message}");
                    return false;
                }
            }
        }

        /// <summary>
        /// 指定されたデータベースへの接続試行
        /// </summary>
        private bool TryConnect(string databaseName)
        {
            try
            {
                LogFile.WriteLog($"データベース接続を開始します: {databaseName}");

                var dbSettings = _databaseSettings[databaseName];

                // 既存の接続をクローズ
                CloseConnectionSafely(databaseName);

                // 接続文字列を手動で作成
                var connectionString = BuildConnectionString(dbSettings);

                // 新しい接続を作成
                var connection = new OracleConnection(connectionString);
                connection.Open();

                _connections[databaseName] = connection;
                _connectionStates[databaseName] = true;
                _lastConnectionChecks[databaseName] = DateTime.Now;

                LogFile.WriteLog($"データベース接続が正常に確立されました: {databaseName} -> {dbSettings.Address}:{dbSettings.Port}/{dbSettings.ServiceName}");
                LogFile.WriteLog($"データベース接続が正常に確立されました: {databaseName} -> {dbSettings.Address}:{dbSettings.Port}/{dbSettings.ServiceName}");
                return true;
            }
            catch (Exception ex)
            {
                ErrLogFile.WriteLog($"TryConnect エラー ({databaseName}): {ex.Message}");
                _connectionStates[databaseName] = false;
                return false;
            }
        }

        /// <summary>
        /// 接続文字列を手動で構築
        /// </summary>
        private string BuildConnectionString(DatabaseSettings dbSettings)
        {
            try
            {
                // Oracle接続文字列を手動で作成

                var connectionString = $"Data Source={dbSettings.Address}:{dbSettings.Port}/{dbSettings.ServiceName};" +
                                     $"User Id={dbSettings.UserId};" +
                                     $"Password={dbSettings.Password};" +
                                     $"Connection Timeout={dbSettings.ConnectionTimeout};" +
                                     $"Validate Connection=true;" +
                                     $"Pooling=true;" +
                                     $"Min Pool Size=1;" +
                                     $"Max Pool Size=10;";

                LogFile.WriteLog($"接続文字列を生成しました : Data Source={dbSettings.Address}:{dbSettings.Port}/{dbSettings.ServiceName};User Id={dbSettings.UserId};...");
                return connectionString;
            }
            catch (Exception ex)
            {
                ErrLogFile.WriteLog($"接続文字列の構築エラー: {ex.Message}");
                throw new InvalidOperationException("接続文字列の構築に失敗しました", ex);
            }
        }

        /// <summary>
        /// データベース接続の健全性をチェック
        /// </summary>
        public bool IsConnectionHealthy(string databaseName = null)
        {
            if (_disposed || !_isInitialized)
                return false;

            if (string.IsNullOrWhiteSpace(databaseName))
            {
                databaseName = _currentDatabaseName;
            }

            if (!_connections.ContainsKey(databaseName) || !_connectionStates.ContainsKey(databaseName))
                return false;

            try
            {
                // 定期的な接続チェック（頻繁なチェックを避ける）
                if (DateTime.Now - _lastConnectionChecks[databaseName] < _connectionCheckInterval)
                {
                    return _connectionStates[databaseName] && 
                           _connections[databaseName]?.State == ConnectionState.Open;
                }

                lock (_connectionLock)
                {
                    var connection = _connections[databaseName];
                    
                    // 簡単な接続確認クエリを実行
                    if (connection?.State != ConnectionState.Open)
                    {
                        _connectionStates[databaseName] = false;
                        return false;
                    }

                    using (var cmd = new OracleCommand("SELECT 1 FROM DUAL", connection))
                    {
                        var result = cmd.ExecuteScalar();
                        _lastConnectionChecks[databaseName] = DateTime.Now;
                        _connectionStates[databaseName] = result != null;
                        
                        if (!_connectionStates[databaseName])
                        {
                            ErrLogFile.WriteLog($"データベース接続が無効になっています: {databaseName}");
                        }
                        
                        return _connectionStates[databaseName];
                    }
                }
            }
            catch (Exception ex)
            {
                ErrLogFile.WriteLog($"接続状態確認エラー ({databaseName}): {ex.Message}");
                _connectionStates[databaseName] = false;
                return false;
            }
        }

        /// <summary>
        /// データベース接続の再確立
        /// </summary>
        public bool TryReconnect(string databaseName = null)
        {
            if (_disposed)
            {
                ErrLogFile.WriteLog("DatabaseManager は既に破棄されています");
                return false;
            }

            if (string.IsNullOrWhiteSpace(databaseName))
            {
                databaseName = _currentDatabaseName;
            }

            lock (_connectionLock)
            {
                try
                {
                    LogFile.WriteLog($"データベース再接続を試行します: {databaseName}");

                    // 既存の接続をクローズ
                    CloseConnectionSafely(databaseName);

                    // 新規接続を試行
                    var result = TryConnect(databaseName);
                    
                    if (result)
                    {
                        LogFile.WriteLog($"データベース再接続が成功しました: {databaseName}");
                    }
                    else
                    {
                        ErrLogFile.WriteLog($"データベース再接続に失敗しました: {databaseName}");
                    }
                    
                    return result;
                }
                catch (Exception ex)
                {
                    ErrLogFile.WriteLog($"TryReconnect エラー ({databaseName}): {ex.Message}");
                    return false;
                }
            }
        }

        /// <summary>
        /// 安全な接続クローズ
        /// </summary>
        private void CloseConnectionSafely(string databaseName)
        {
            try
            {
                if (_connections.ContainsKey(databaseName) && _connections[databaseName] != null)
                {
                    var connection = _connections[databaseName];
                    if (connection.State == ConnectionState.Open)
                    {
                        connection.Close();
                    }
                    connection.Dispose();
                    _connections[databaseName] = null;
                    LogFile.WriteLog($"データベース接続を安全にクローズしました: {databaseName}");
                }
            }
            catch (Exception ex)
            {
                ErrLogFile.WriteLog($"接続クローズエラー ({databaseName}): {ex.Message}");
            }
            finally
            {
                _connectionStates[databaseName] = false;
            }
        }

        /// <summary>
        /// 現在使用するデータベースを切り替え
        /// </summary>
        public bool SwitchDatabase(string databaseName)
        {
            try
            {
                if (!_databaseSettings.ContainsKey(databaseName))
                {
                    ErrLogFile.WriteLog($"指定されたデータベース設定が見つかりません: {databaseName}");
                    return false;
                }

                _currentDatabaseName = databaseName;
                _currentDatabase = _databaseSettings[databaseName];

                LogFile.WriteLog($"使用データベースを切り替えました: {databaseName}");
                return true;
            }
            catch (Exception ex)
            {
                ErrLogFile.WriteLog($"データベース切り替えエラー: {ex.Message}");
                return false;
            }
        }

        #endregion

        #region データアクセスメソッド

        /// <summary>
        /// SELECTクエリを実行してDataTableを返す（デフォルトDB）
        /// </summary>
        public bool ExecuteSelect(string sql, List<OracleParameter> parameters, ref DataTable dataTable)
        {
            return ExecuteSelect(sql, parameters, ref dataTable, _currentDatabaseName);
        }

        /// <summary>
        /// SELECTクエリを実行してDataTableを返す（指定DB）
        /// </summary>
        public bool ExecuteSelect(string sql, List<OracleParameter> parameters, ref DataTable dataTable, string databaseName)
        {
            if (_disposed || !_isInitialized)
            {
                ErrLogFile.WriteLog("DatabaseManager が無効な状態です");
                return false;
            }

            if (string.IsNullOrWhiteSpace(databaseName))
            {
                databaseName = _currentDatabaseName;
            }

            if (!_connections.ContainsKey(databaseName) || !_connectionStates[databaseName])
            {
                ErrLogFile.WriteLog($"データベース接続が無効です: {databaseName}");
                return false;
            }

            try
            {
                var connection = _connections[databaseName];
                using (var cmd = new OracleCommand(sql, connection))
                {
                    cmd.CommandTimeout = _databaseSettings[databaseName].CommandTimeout;

                    // パラメータを追加
                    if (parameters != null)
                    {
                        cmd.Parameters.AddRange(parameters.ToArray());
                    }

                    using (var adapter = new OracleDataAdapter(cmd))
                    {
                        dataTable = new DataTable();
                        adapter.Fill(dataTable);
                    }
                }

                LogFile.WriteLog($"SELECT実行成功 ({databaseName}): {dataTable.Rows.Count}件取得");
                return true;
            }
            catch (Exception ex)
            {
                ErrLogFile.WriteLog($"ExecuteSelect エラー ({databaseName}): {ex.Message}");
                ErrLogFile.WriteLog($"SQL: {sql}");
                return false;
            }
        }

        /// <summary>
        /// INSERT/UPDATE/DELETEクエリを実行（デフォルトDB）
        /// </summary>
        public bool ExecuteNonQuery(string sql, List<OracleParameter> parameters, bool useTransaction = true)
        {
            return ExecuteNonQuery(sql, parameters, useTransaction, _currentDatabaseName);
        }

        /// <summary>
        /// INSERT/UPDATE/DELETEクエリを実行（指定DB）
        /// </summary>
        public bool ExecuteNonQuery(string sql, List<OracleParameter> parameters, bool useTransaction, string databaseName)
        {
            if (_disposed || !_isInitialized)
            {
                ErrLogFile.WriteLog("DatabaseManager が無効な状態です");
                return false;
            }

            if (string.IsNullOrWhiteSpace(databaseName))
            {
                databaseName = _currentDatabaseName;
            }

            if (!_connections.ContainsKey(databaseName) || !_connectionStates[databaseName])
            {
                ErrLogFile.WriteLog($"データベース接続が無効です: {databaseName}");
                return false;
            }

            OracleTransaction transaction = null;
            try
            {
                var connection = _connections[databaseName];

                if (useTransaction)
                {
                    transaction = connection.BeginTransaction();
                }

                using (var cmd = new OracleCommand(sql, connection))
                {
                    cmd.CommandTimeout = _databaseSettings[databaseName].CommandTimeout;

                    if (transaction != null)
                    {
                        cmd.Transaction = transaction;
                    }

                    // パラメータを追加
                    if (parameters != null)
                    {
                        cmd.Parameters.AddRange(parameters.ToArray());
                    }

                    var rowsAffected = cmd.ExecuteNonQuery();
                    
                    if (transaction != null)
                    {
                        transaction.Commit();
                    }

                    LogFile.WriteLog($"NonQuery実行成功 ({databaseName}): {rowsAffected}行影響");
                    return true;
                }
            }
            catch (Exception ex)
            {
                if (transaction != null)
                {
                    try
                    {
                        transaction.Rollback();
                        LogFile.WriteLog($"トランザクションをロールバックしました ({databaseName})");
                    }
                    catch (Exception rollbackEx)
                    {
                        ErrLogFile.WriteLog($"ロールバックエラー ({databaseName}): {rollbackEx.Message}");
                    }
                }

                ErrLogFile.WriteLog($"ExecuteNonQuery エラー ({databaseName}): {ex.Message}");
                ErrLogFile.WriteLog($"SQL: {sql}");
                return false;
            }
            finally
            {
                transaction?.Dispose();
            }
        }

        /// <summary>
        /// スカラー値を取得（デフォルトDB）
        /// </summary>
        public object ExecuteScalar(string sql, List<OracleParameter> parameters)
        {
            return ExecuteScalar(sql, parameters, _currentDatabaseName);
        }

        /// <summary>
        /// スカラー値を取得（指定DB）
        /// </summary>
        public object ExecuteScalar(string sql, List<OracleParameter> parameters, string databaseName)
        {
            if (_disposed || !_isInitialized)
            {
                ErrLogFile.WriteLog("DatabaseManager が無効な状態です");
                return null;
            }

            if (string.IsNullOrWhiteSpace(databaseName))
            {
                databaseName = _currentDatabaseName;
            }

            if (!_connections.ContainsKey(databaseName) || !_connectionStates[databaseName])
            {
                ErrLogFile.WriteLog($"データベース接続が無効です: {databaseName}");
                return null;
            }

            try
            {
                var connection = _connections[databaseName];
                using (var cmd = new OracleCommand(sql, connection))
                {
                    cmd.CommandTimeout = _databaseSettings[databaseName].CommandTimeout;

                    // パラメータを追加
                    if (parameters != null)
                    {
                        cmd.Parameters.AddRange(parameters.ToArray());
                    }

                    var result = cmd.ExecuteScalar();
                    LogFile.WriteLog($"ExecuteScalar実行成功 ({databaseName})");
                    return result;
                }
            }
            catch (Exception ex)
            {
                ErrLogFile.WriteLog($"ExecuteScalar エラー ({databaseName}): {ex.Message}");
                ErrLogFile.WriteLog($"SQL: {sql}");
                return null;
            }
        }

        #endregion

        #region 統計・監視メソッド

        /// <summary>
        /// 現在のデータベース名を取得
        /// </summary>
        public string GetCurrentDatabaseName()
        {
            return _currentDatabaseName;
        }

        /// <summary>
        /// 利用可能なデータベース名の一覧を取得
        /// </summary>
        public IEnumerable<string> GetAvailableDatabaseNames()
        {
            return _databaseSettings.Keys.ToArray();
        }

        /// <summary>
        /// 接続統計情報の取得
        /// </summary>
        public ConnectionStatistics GetConnectionStatistics()
        {
            return new ConnectionStatistics
            {
                IsInitialized = _isInitialized,
                IsDisposed = _disposed,
                CurrentDatabase = _currentDatabaseName,
                DatabaseCount = _databaseSettings.Count,
                ConnectedDatabases = _connectionStates.Where(kvp => kvp.Value).Select(kvp => kvp.Key).ToArray(),
                LastConnectionChecks = new Dictionary<string, DateTime>(_lastConnectionChecks)
            };
        }

        /// <summary>
        /// データベース設定情報の取得（機密情報除く）
        /// </summary>
        public DatabaseConfiguration GetDatabaseConfiguration(string databaseName = null)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(databaseName))
                {
                    databaseName = _currentDatabaseName;
                }

                if (!_databaseSettings.ContainsKey(databaseName))
                {
                    return null;
                }

                var settings = _databaseSettings[databaseName];
                return new DatabaseConfiguration
                {
                    Name = settings.Name,
                    DataSource = $"{settings.Address}:{settings.Port}/{settings.ServiceName}",
                    UserId = settings.UserId,
                    ConnectionTimeout = settings.ConnectionTimeout,
                    CommandTimeout = settings.CommandTimeout,
                    RetrySleep = settings.RetrySleep,
                    RetryCount = settings.RetryCount,
                    Description = settings.Description
                    // パスワードは含めない
                };
            }
            catch (Exception ex)
            {
                ErrLogFile.WriteLog($"GetDatabaseConfiguration エラー: {ex.Message}");
                return null;
            }
        }

        #endregion

        #region IDisposable実装

        /// <summary>
        /// リソースの解放
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// リソースの解放（詳細）
        /// </summary>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed && disposing)
            {
                try
                {
                    lock (_connectionLock)
                    {
                        LogFile.WriteLog("DatabaseManager のリソース解放を開始します");

                        // 全てのデータベース接続をクローズ
                        foreach (var databaseName in _connections.Keys.ToArray())
                        {
                            CloseConnectionSafely(databaseName);
                        }

                        _connections.Clear();
                        _databaseSettings.Clear();
                        _connectionStates.Clear();
                        _lastConnectionChecks.Clear();

                        _isInitialized = false;
                        _disposed = true;

                        LogFile.WriteLog("DatabaseManager のリソース解放が完了しました");
                    }
                }
                catch (Exception ex)
                {
                    // 最後の手段: Windowsイベントログに記録
                    try
                    {
                        System.Diagnostics.EventLog.WriteEntry("JobManagementApp", 
                            $"DatabaseManager Dispose エラー: {ex.Message}", 
                            System.Diagnostics.EventLogEntryType.Error);
                    }
                    catch
                    {
                        // 何もできない
                    }
                }
            }
        }

        /// <summary>
        /// ファイナライザー
        /// </summary>
        ~DatabaseManager()
        {
            Dispose(false);
        }

        #endregion

        #region 内部クラス

        /// <summary>
        /// 接続統計情報
        /// </summary>
        public class ConnectionStatistics
        {
            public bool IsInitialized { get; set; }
            public bool IsDisposed { get; set; }
            public string CurrentDatabase { get; set; }
            public int DatabaseCount { get; set; }
            public string[] ConnectedDatabases { get; set; }
            public Dictionary<string, DateTime> LastConnectionChecks { get; set; }
        }

        /// <summary>
        /// データベース設定情報
        /// </summary>
        public class DatabaseConfiguration
        {
            public string Name { get; set; }
            public string DataSource { get; set; }
            public string UserId { get; set; }
            public int ConnectionTimeout { get; set; }
            public int CommandTimeout { get; set; }
            public int RetrySleep { get; set; }
            public int RetryCount { get; set; }
            public string Description { get; set; }
        }

        #endregion

        #region テスト・デバッグ用メソッド

        /// <summary>
        /// 強制的なインスタンスリセット（テスト用のみ）
        /// </summary>
        internal static void ResetInstanceForTesting()
        {
            lock (_lock)
            {
                _instance?.Dispose();
                _instance = null;
            }
        }

        /// <summary>
        /// デバッグ情報の出力
        /// </summary>
        public void LogDebugInfo()
        {
            try
            {
                var stats = GetConnectionStatistics();
                
                LogFile.WriteLog($"DatabaseManager Debug Info:");
                LogFile.WriteLog($"  Initialized: {stats.IsInitialized}");
                LogFile.WriteLog($"  Current Database: {stats.CurrentDatabase}");
                LogFile.WriteLog($"  Total Databases: {stats.DatabaseCount}");
                LogFile.WriteLog($"  Connected Databases: {string.Join(", ", stats.ConnectedDatabases)}");
                
                foreach (var dbName in GetAvailableDatabaseNames())
                {
                    var config = GetDatabaseConfiguration(dbName);
                    LogFile.WriteLog($"  [{dbName}] {config?.DataSource} (User: {config?.UserId})");
                }
            }
            catch (Exception ex)
            {
                ErrLogFile.WriteLog($"LogDebugInfo エラー: {ex.Message}");
            }
        }

        #endregion
    }
}