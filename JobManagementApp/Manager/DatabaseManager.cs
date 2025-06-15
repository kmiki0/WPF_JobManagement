using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DICSSLORA.ACmnFunc;
using DICSSLORA.ACmnIni;
using DICSSLORA.ACmnLog;
using DICSSLORA.ACmnOra;

namespace JobManagementApp.Manager
{
    /// <summary>
    /// データベース管理クラス - 完全版リソース管理修正
    /// IDisposableを実装してリソースリークを防止
    /// </summary>
    public class DatabaseManager : IDisposable
    {
        #region フィールドとプロパティ

        public DICSSLORA.ACmnIni.clsMngIniFile pobjIniFile { get; private set; }
        public DICSSLORA.ACmnLog.clsMngLogFile pobjActLog { get; private set; }
        public DICSSLORA.ACmnLog.clsMngLogFile pobjErrLog { get; private set; }
        public DICSSLORA.ACmnOra.clsMngOracle pobjOraDb { get; private set; }

        private static DatabaseManager _instance;
        private static readonly object _lock = new object();
        private bool _disposed = false;
        private readonly object _connectionLock = new object();

        // 接続状態管理
        private bool _isInitialized = false;
        private bool _isConnected = false;
        private DateTime _lastConnectionCheck = DateTime.MinValue;
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

                // Oracleクライアントの初期化
                InitializeOracleClient();

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
        /// Oracleクライアントの初期化
        /// </summary>
        private void InitializeOracleClient()
        {
            try
            {
                var dataSource = pobjIniFile.pGetItemString("DB", "DATA_SOURCE");
                var userId = pobjIniFile.pGetItemString("DB", "USER_ID");
                var password = pobjIniFile.pGetItemString("DB", "PASSWORD");
                var retrySleep = pobjIniFile.pGetItemInt("DB", "RTRY_SLEEP");
                var retryCount = pobjIniFile.pGetItemInt("DB", "RTRY_CNT");

                // 接続情報の検証
                if (string.IsNullOrWhiteSpace(dataSource) || 
                    string.IsNullOrWhiteSpace(userId) || 
                    string.IsNullOrWhiteSpace(password))
                {
                    throw new InvalidOperationException("データベース接続情報が不完全です");
                }

                // ORACLEインスタンス生成 
                pobjOraDb = new DICSSLORA.ACmnOra.clsMngOracle(
                    dataSource, userId, password, retrySleep, retryCount,
                    pobjActLog, pobjErrLog, clsMngOracle.peOraCom.ODP);

                if (pobjOraDb == null)
                {
                    throw new InvalidOperationException("Oracleインスタンスの生成に失敗しました");
                }

                LogFile.WriteLog($"Oracleクライアントを正常に初期化しました (DataSource: {dataSource})");
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Oracleクライアントの初期化に失敗しました", ex);
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

                // ORACLE初期化
                if (!pobjOraDb.pInitOra())
                {
                    ErrLogFile.WriteLog("ORACLE 初期化に失敗しました");
                    return false;
                }

                // ORACLE接続
                if (!pobjOraDb.pOpenOra())
                {
                    ErrLogFile.WriteLog("ORACLE 接続に失敗しました");
                    return false;
                }

                // オープンクローズ制御を有効化
                pobjOraDb.pOpenCloseCtrl = true;
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
            if (_disposed || !_isInitialized || pobjOraDb == null)
                return false;

            try
            {
                // 定期的な接続チェック（頻繁なチェックを避ける）
                if (DateTime.Now - _lastConnectionCheck < _connectionCheckInterval)
                {
                    return _isConnected;
                }

                lock (_connectionLock)
                {
                    // 簡単な接続確認クエリを実行
                    var testTable = new System.Data.DataTable();
                    var testResult = pobjOraDb.pSelectOra("SELECT 1 FROM DUAL", ref testTable);
                    
                    _lastConnectionCheck = DateTime.Now;
                    _isConnected = testResult && testTable.Rows.Count > 0;
                    
                    if (!_isConnected)
                    {
                        ErrLogFile.WriteLog("データベース接続が無効になっています");
                    }
                    
                    return _isConnected;
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
                if (pobjOraDb != null && _isConnected)
                {
                    pobjOraDb.pOpenCloseCtrl = false;
                    pobjOraDb.pCloseOra();
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
                if (pobjIniFile == null)
                    return null;

                return new DatabaseConfiguration
                {
                    DataSource = pobjIniFile.pGetItemString("DB", "DATA_SOURCE"),
                    UserId = pobjIniFile.pGetItemString("DB", "USER_ID"),
                    RetrySleep = pobjIniFile.pGetItemInt("DB", "RTRY_SLEEP"),
                    RetryCount = pobjIniFile.pGetItemInt("DB", "RTRY_CNT")
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

                        // その他のリソース解放
                        // 注意: ログファイルは他のクラスからも参照されている可能性があるため、
                        // 実際のクローズは慎重に行う必要がある
                        pobjOraDb = null;
                        pobjIniFile = null;
                        
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