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

namespace JobManagementApp.Manager
{
    /// <summary>
    /// データベース管理クラス - Oracle.ManagedDataAccess.Client対応版
    /// IDisposableを実装してリソースリークを防止
    /// </summary>
    public class DatabaseManager : IDisposable
    {
        #region フィールドとプロパティ

        public DICSSLORA.ACmnIni.clsMngIniFile pobjIniFile { get; private set; }
        public DICSSLORA.ACmnLog.clsMngLogFile pobjActLog { get; private set; }
        public DICSSLORA.ACmnLog.clsMngLogFile pobjErrLog { get; private set; }
        
        // Oracle.ManagedDataAccess.Client用
        private OracleConnection _oracleConnection;
        private string _connectionString;

        private static DatabaseManager _instance;
        private static readonly object _lock = new object();
        private bool _disposed = false;
        private readonly object _connectionLock = new object();

        // 接続状態管理
        private bool _isInitialized = false;
        private bool _isConnected = false;
        private DateTime _lastConnectionCheck = DateTime.MinValue;
        private readonly TimeSpan _connectionCheckInterval = TimeSpan.FromMinutes(5);

        // 設定情報
        private string _dataSource;
        private string _userId;
        private string _password;
        private int _retrySleep;
        private int _retryCount;

        #endregion

        #region コンストラクタ・インスタンス管理

        /// <summary>
        /// プライベートコンストラクタ（シングルトンパターン）
        /// </summary>
        private DatabaseManager()
        {
            try
            {
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
                // INIファイルの初期化
                InitializeIniFile();

                // ログファイルの初期化
                InitializeLogFiles();

                // Oracle接続の初期化
                InitializeOracleConnection();

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
        /// INIファイルの初期化
        /// </summary>
        private void InitializeIniFile()
        {
            try
            {
                pobjIniFile = new clsMngIniFile(clsDefineCnst.pcnstININAME);
                var result = pobjIniFile.pGetInfo();
                
                if (!result)
                {
                    throw new InvalidOperationException("INIファイルの読み込みに失敗しました");
                }

                LogFile.WriteLog($"INIファイルを正常に読み込みました: {clsDefineCnst.pcnstININAME}");
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("INIファイルの初期化に失敗しました", ex);
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

                LogFile.WriteLog("ログファイルインスタンスを正常に初期化しました");
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("ログファイルの初期化に失敗しました", ex);
            }
        }

        /// <summary>
        /// Oracle接続の初期化
        /// </summary>
        private void InitializeOracleConnection()
        {
            try
            {
                _dataSource = pobjIniFile.pGetItemString("DB", "DATA_SOURCE");
                _userId = pobjIniFile.pGetItemString("DB", "USER_ID");
                _password = pobjIniFile.pGetItemString("DB", "PASSWORD");
                _retrySleep = pobjIniFile.pGetItemInt("DB", "RTRY_SLEEP");
                _retryCount = pobjIniFile.pGetItemInt("DB", "RTRY_CNT");

                // 接続情報の検証
                if (string.IsNullOrWhiteSpace(_dataSource) || 
                    string.IsNullOrWhiteSpace(_userId) || 
                    string.IsNullOrWhiteSpace(_password))
                {
                    throw new InvalidOperationException("データベース接続情報が不完全です");
                }

                // 接続文字列の構築
                var builder = new OracleConnectionStringBuilder
                {
                    DataSource = _dataSource,
                    UserID = _userId,
                    Password = _password,
                    ConnectionTimeout = 30,
                    CommandTimeout = 300
                };
                
                _connectionString = builder.ConnectionString;

                LogFile.WriteLog($"Oracle接続を正常に初期化しました (DataSource: {_dataSource})");
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Oracle接続の初期化に失敗しました", ex);
            }
        }

        #endregion

        #region 接続管理メソッド

        /// <summary>
        /// データベース接続の確立
        /// </summary>
        public bool EstablishConnection()
        {
            if (_disposed)
            {
                ErrLogFile.WriteLog("DatabaseManager は既に破棄されています");
                return false;
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

                    // 既に接続済みの場合は健全性チェック
                    if (_isConnected && IsConnectionHealthy())
                    {
                        LogFile.WriteLog("データベース接続は既に確立されています");
                        return true;
                    }

                    // 新規接続またはリトライ
                    return TryConnect();
                }
                catch (Exception ex)
                {
                    ErrLogFile.WriteLog($"EstablishConnection エラー: {ex.Message}");
                    return false;
                }
            }
        }

        /// <summary>
        /// 接続試行
        /// </summary>
        private bool TryConnect()
        {
            try
            {
                LogFile.WriteLog("データベース接続を開始します");

                // 既存の接続をクローズ
                CloseConnectionSafely();

                // 新しい接続を作成
                _oracleConnection = new OracleConnection(_connectionString);
                _oracleConnection.Open();

                _isConnected = true;
                _lastConnectionCheck = DateTime.Now;

                LogFile.WriteLog("データベース接続が正常に確立されました");
                return true;
            }
            catch (Exception ex)
            {
                ErrLogFile.WriteLog($"TryConnect エラー: {ex.Message}");
                _isConnected = false;
                return false;
            }
        }

        /// <summary>
        /// データベース接続の健全性をチェック
        /// </summary>
        public bool IsConnectionHealthy()
        {
            if (_disposed || !_isInitialized || _oracleConnection == null)
                return false;

            try
            {
                // 定期的な接続チェック（頻繁なチェックを避ける）
                if (DateTime.Now - _lastConnectionCheck < _connectionCheckInterval)
                {
                    return _isConnected && _oracleConnection.State == ConnectionState.Open;
                }

                lock (_connectionLock)
                {
                    // 簡単な接続確認クエリを実行
                    if (_oracleConnection.State != ConnectionState.Open)
                    {
                        _isConnected = false;
                        return false;
                    }

                    using (var cmd = new OracleCommand("SELECT 1 FROM DUAL", _oracleConnection))
                    {
                        var result = cmd.ExecuteScalar();
                        _lastConnectionCheck = DateTime.Now;
                        _isConnected = result != null;
                        
                        if (!_isConnected)
                        {
                            ErrLogFile.WriteLog("データベース接続が無効になっています");
                        }
                        
                        return _isConnected;
                    }
                }
            }
            catch (Exception ex)
            {
                ErrLogFile.WriteLog($"接続状態確認エラー: {ex.Message}");
                _isConnected = false;
                return false;
            }
        }

        /// <summary>
        /// データベース接続の再確立
        /// </summary>
        public bool TryReconnect()
        {
            if (_disposed)
            {
                ErrLogFile.WriteLog("DatabaseManager は既に破棄されています");
                return false;
            }

            lock (_connectionLock)
            {
                try
                {
                    LogFile.WriteLog("データベース再接続を試行します");

                    // 既存の接続をクローズ
                    CloseConnectionSafely();

                    // 新規接続を試行
                    var result = TryConnect();
                    
                    if (result)
                    {
                        LogFile.WriteLog("データベース再接続が成功しました");
                    }
                    else
                    {
                        ErrLogFile.WriteLog("データベース再接続に失敗しました");
                    }
                    
                    return result;
                }
                catch (Exception ex)
                {
                    ErrLogFile.WriteLog($"TryReconnect エラー: {ex.Message}");
                    return false;
                }
            }
        }

        /// <summary>
        /// 安全な接続クローズ
        /// </summary>
        private void CloseConnectionSafely()
        {
            try
            {
                if (_oracleConnection != null && _isConnected)
                {
                    if (_oracleConnection.State == ConnectionState.Open)
                    {
                        _oracleConnection.Close();
                    }
                    _oracleConnection.Dispose();
                    _oracleConnection = null;
                    LogFile.WriteLog("データベース接続を安全にクローズしました");
                }
            }
            catch (Exception ex)
            {
                ErrLogFile.WriteLog($"接続クローズエラー: {ex.Message}");
            }
            finally
            {
                _isConnected = false;
            }
        }

        #endregion

        #region データアクセスメソッド

        /// <summary>
        /// SELECTクエリを実行してDataTableを返す
        /// </summary>
        public bool ExecuteSelect(string sql, List<OracleParameter> parameters, ref DataTable dataTable)
        {
            if (_disposed || !_isConnected || _oracleConnection == null)
            {
                ErrLogFile.WriteLog("データベース接続が無効です");
                return false;
            }

            try
            {
                using (var cmd = new OracleCommand(sql, _oracleConnection))
                {
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

                LogFile.WriteLog($"SELECT実行成功: {dataTable.Rows.Count}件取得");
                return true;
            }
            catch (Exception ex)
            {
                ErrLogFile.WriteLog($"ExecuteSelect エラー: {ex.Message}");
                ErrLogFile.WriteLog($"SQL: {sql}");
                return false;
            }
        }

        /// <summary>
        /// INSERT/UPDATE/DELETEクエリを実行
        /// </summary>
        public bool ExecuteNonQuery(string sql, List<OracleParameter> parameters, bool useTransaction = true)
        {
            if (_disposed || !_isConnected || _oracleConnection == null)
            {
                ErrLogFile.WriteLog("データベース接続が無効です");
                return false;
            }

            OracleTransaction transaction = null;
            try
            {
                if (useTransaction)
                {
                    transaction = _oracleConnection.BeginTransaction();
                }

                using (var cmd = new OracleCommand(sql, _oracleConnection))
                {
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

                    LogFile.WriteLog($"NonQuery実行成功: {rowsAffected}行影響");
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
                        LogFile.WriteLog("トランザクションをロールバックしました");
                    }
                    catch (Exception rollbackEx)
                    {
                        ErrLogFile.WriteLog($"ロールバックエラー: {rollbackEx.Message}");
                    }
                }

                ErrLogFile.WriteLog($"ExecuteNonQuery エラー: {ex.Message}");
                ErrLogFile.WriteLog($"SQL: {sql}");
                return false;
            }
            finally
            {
                transaction?.Dispose();
            }
        }

        /// <summary>
        /// スカラー値を取得
        /// </summary>
        public object ExecuteScalar(string sql, List<OracleParameter> parameters)
        {
            if (_disposed || !_isConnected || _oracleConnection == null)
            {
                ErrLogFile.WriteLog("データベース接続が無効です");
                return null;
            }

            try
            {
                using (var cmd = new OracleCommand(sql, _oracleConnection))
                {
                    // パラメータを追加
                    if (parameters != null)
                    {
                        cmd.Parameters.AddRange(parameters.ToArray());
                    }

                    var result = cmd.ExecuteScalar();
                    LogFile.WriteLog($"ExecuteScalar実行成功");
                    return result;
                }
            }
            catch (Exception ex)
            {
                ErrLogFile.WriteLog($"ExecuteScalar エラー: {ex.Message}");
                ErrLogFile.WriteLog($"SQL: {sql}");
                return null;
            }
        }

        #endregion

        #region 統計・監視メソッド

        /// <summary>
        /// 接続統計情報の取得
        /// </summary>
        public ConnectionStatistics GetConnectionStatistics()
        {
            return new ConnectionStatistics
            {
                IsInitialized = _isInitialized,
                IsConnected = _isConnected,
                IsDisposed = _disposed,
                LastConnectionCheck = _lastConnectionCheck,
                InstanceCreationTime = DateTime.Now // 実際にはインスタンス作成時刻を保存すべき
            };
        }

        /// <summary>
        /// データベース設定情報の取得（機密情報除く）
        /// </summary>
        public DatabaseConfiguration GetDatabaseConfiguration()
        {
            try
            {
                return new DatabaseConfiguration
                {
                    DataSource = _dataSource,
                    UserId = _userId,
                    RetrySleep = _retrySleep,
                    RetryCount = _retryCount
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

                        // データベース接続のクローズ
                        CloseConnectionSafely();

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
            public bool IsConnected { get; set; }
            public bool IsDisposed { get; set; }
            public DateTime LastConnectionCheck { get; set; }
            public DateTime InstanceCreationTime { get; set; }
        }

        /// <summary>
        /// データベース設定情報
        /// </summary>
        public class DatabaseConfiguration
        {
            public string DataSource { get; set; }
            public string UserId { get; set; }
            public int RetrySleep { get; set; }
            public int RetryCount { get; set; }
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
                var config = GetDatabaseConfiguration();
                
                LogFile.WriteLog($"DatabaseManager Debug Info:");
                LogFile.WriteLog($"  Initialized: {stats.IsInitialized}");
                LogFile.WriteLog($"  Connected: {stats.IsConnected}");
                LogFile.WriteLog($"  Disposed: {stats.IsDisposed}");
                LogFile.WriteLog($"  DataSource: {config?.DataSource}");
                LogFile.WriteLog($"  UserId: {config?.UserId}");
                LogFile.WriteLog($"  RetryCount: {config?.RetryCount}");
            }
            catch (Exception ex)
            {
                ErrLogFile.WriteLog($"LogDebugInfo エラー: {ex.Message}");
            }
        }

        #endregion
    }
}