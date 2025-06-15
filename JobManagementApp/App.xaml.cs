using System;
using System.Windows;
using System.Windows.Threading;
using System.Threading.Tasks;
using JobManagementApp.Manager;
using Microsoft.Extensions.DependencyInjection;
using JobManagementApp.ViewModels;
using System.Diagnostics;
using System.IO;
using System.Reflection;

namespace JobManagementApp
{
    /// <summary>
    /// アプリケーションクラス - 完全版安全性修正
    /// グローバル例外処理、リソース管理、ログ機能を強化
    /// </summary>
    public partial class App : Application
    {
        #region フィールドとプロパティ

        public static IServiceProvider ServiceProvider { get; private set; }
        
        // アプリケーション状態管理
        private static bool _isShuttingDown = false;
        private static bool _isInitialized = false;
        private static readonly object _lockObject = new object();
        
        // 統計情報
        private static DateTime _startTime;
        private static int _totalExceptions = 0;
        private static int _handledExceptions = 0;
        
        // アプリケーション情報
        private static readonly string _applicationName = "JobManagementApp";
        private static readonly string _applicationVersion = GetApplicationVersion();

        #endregion

        #region メインエントリーポイント

        /// <summary>
        /// アプリケーションのメインエントリーポイント
        /// </summary>
        [STAThread]
        public static void Main()
        {
            var app = new App();
            
            try
            {
                _startTime = DateTime.Now;
                LogApplicationStartup();
                
                app.InitializeComponent();
                var exitCode = app.Run();
                
                LogApplicationShutdown(exitCode);
                Environment.Exit(exitCode);
            }
            catch (Exception ex)
            {
                HandleCriticalException(ex, "アプリケーション開始時の致命的エラー");
                Environment.Exit(1);
            }
        }

        #endregion

        #region アプリケーションライフサイクル

        /// <summary>
        /// アプリケーション開始時の処理
        /// </summary>
        protected override void OnStartup(StartupEventArgs e)
        {
            try
            {
                LogFile.WriteLog($"{_applicationName} v{_applicationVersion} を開始します");
                
                // 基本初期化
                base.OnStartup(e);

                // グローバル例外ハンドラー設定（最優先）
                SetupGlobalExceptionHandlers();

                // 依存性注入設定
                ConfigureServices();

                // データベース初期化
                InitializeDatabase();

                // アプリケーション設定
                ConfigureApplication();

                _isInitialized = true;
                LogFile.WriteLog("アプリケーションの初期化が完了しました");
            }
            catch (Exception ex)
            {
                HandleCriticalException(ex, "アプリケーション初期化エラー");
                Shutdown(1);
            }
        }

        /// <summary>
        /// アプリケーション終了時の処理
        /// </summary>
        protected override void OnExit(ExitEventArgs e)
        {
            try
            {
                lock (_lockObject)
                {
                    if (_isShuttingDown)
                        return;
                    
                    _isShuttingDown = true;
                }

                LogFile.WriteLog("アプリケーション終了処理を開始します");

                // リソースのクリーンアップ
                PerformCleanup();

                // 統計情報の出力
                LogApplicationStatistics();

                LogFile.WriteLog("アプリケーション終了処理が完了しました");
            }
            catch (Exception ex)
            {
                // 終了処理でのエラーは最小限のログ出力のみ
                LogCriticalError($"終了処理エラー: {ex.Message}");
            }
            finally
            {
                base.OnExit(e);
            }
        }

        /// <summary>
        /// セッション終了時の処理（OSシャットダウン等）
        /// </summary>
        protected override void OnSessionEnding(SessionEndingCancelEventArgs e)
        {
            try
            {
                LogFile.WriteLog($"セッション終了要求: {e.ReasonSessionEnding}");
                
                // 緊急クリーンアップ
                PerformEmergencyCleanup();
                
                base.OnSessionEnding(e);
            }
            catch (Exception ex)
            {
                LogCriticalError($"セッション終了処理エラー: {ex.Message}");
            }
        }

        #endregion

        #region 例外処理設定

        /// <summary>
        /// グローバル例外ハンドラーの設定
        /// </summary>
        private void SetupGlobalExceptionHandlers()
        {
            try
            {
                // UIスレッドの未処理例外
                DispatcherUnhandledException += OnDispatcherUnhandledException;

                // バックグラウンドスレッドの未処理例外
                AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;

                // Taskの未処理例外
                TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

                LogFile.WriteLog("グローバル例外ハンドラーを設定しました");
            }
            catch (Exception ex)
            {
                LogCriticalError($"例外ハンドラー設定エラー: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// UIスレッド未処理例外ハンドラー
        /// </summary>
        private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            try
            {
                var handled = HandleUIException(e.Exception);
                e.Handled = handled;

                if (handled)
                {
                    _handledExceptions++;
                    LogFile.WriteLog("UIスレッド例外を処理しました - アプリケーションは継続します");
                }
                else
                {
                    LogCriticalError("致命的なUIスレッド例外 - アプリケーションを終了します");
                    Shutdown(1);
                }
            }
            catch (Exception ex)
            {
                LogCriticalError($"例外ハンドラー内でエラー: {ex.Message}");
                e.Handled = false;
            }
        }

        /// <summary>
        /// バックグラウンドスレッド未処理例外ハンドラー
        /// </summary>
        private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            try
            {
                var exception = e.ExceptionObject as Exception;
                var isTerminating = e.IsTerminating;

                LogCriticalError($"バックグラウンドスレッド未処理例外 (終了予定: {isTerminating}): {exception?.Message}");
                LogCriticalError($"スタックトレース: {exception?.StackTrace}");

                if (isTerminating)
                {
                    PerformEmergencyCleanup();
                }
            }
            catch (Exception ex)
            {
                // 最後の手段
                EventLog.WriteEntry(_applicationName, $"例外ハンドラーエラー: {ex.Message}", EventLogEntryType.Error);
            }
        }

        /// <summary>
        /// Task未処理例外ハンドラー
        /// </summary>
        private void OnUnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
        {
            try
            {
                LogCriticalError($"Task未処理例外: {e.Exception?.Message}");
                
                foreach (var innerEx in e.Exception?.InnerExceptions ?? new Exception[0])
                {
                    LogCriticalError($"内部例外: {innerEx.Message}");
                }

                e.SetObserved(); // 例外を観測済みとしてマーク
                _handledExceptions++;
            }
            catch (Exception ex)
            {
                LogCriticalError($"Task例外ハンドラーエラー: {ex.Message}");
            }
        }

        #endregion

        #region 初期化メソッド

        /// <summary>
        /// 依存性注入の設定
        /// </summary>
        private void ConfigureServices()
        {
            try
            {
                var services = new ServiceCollection();
                
                // コアサービスの登録
                services.AddSingleton<IFileWatcherManager, FileWatcherManager>();
                
                // 将来の拡張用
                // services.AddSingleton<ILoggingService, LoggingService>();
                // services.AddSingleton<IConfigurationService, ConfigurationService>();
                
                ServiceProvider = services.BuildServiceProvider();
                
                LogFile.WriteLog("依存性注入コンテナを設定しました");
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("依存性注入の設定に失敗しました", ex);
            }
        }

        /// <summary>
        /// データベース初期化
        /// </summary>
        private void InitializeDatabase()
        {
            try
            {
                LogFile.WriteLog("データベース初期化を開始します");

                var databaseManager = DatabaseManager.Instance;
                
                if (databaseManager == null)
                {
                    throw new InvalidOperationException("DatabaseManagerの取得に失敗しました");
                }

                // 接続確立
                if (!databaseManager.EstablishConnection())
                {
                    throw new InvalidOperationException("データベース接続の確立に失敗しました");
                }

                // 接続状態の確認
                if (!databaseManager.IsConnectionHealthy())
                {
                    LogFile.WriteLog("データベース接続の再試行を実行します");
                    
                    if (!databaseManager.TryReconnect())
                    {
                        throw new InvalidOperationException("データベース再接続に失敗しました");
                    }
                }

                LogFile.WriteLog("データベース初期化が完了しました");
            }
            catch (Exception ex)
            {
                ErrLogFile.WriteLog($"データベース初期化エラー: {ex.Message}");
                throw new InvalidOperationException("データベース初期化に失敗しました", ex);
            }
        }

        /// <summary>
        /// アプリケーション設定
        /// </summary>
        private void ConfigureApplication()
        {
            try
            {
                // プロセス優先度の設定
                Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.Normal;

                // ガベージコレクション設定の最適化
                GCSettings.LatencyMode = GCLatencyMode.Interactive;

                // アプリケーションタイトルの設定
                if (MainWindow != null)
                {
                    MainWindow.Title = $"{_applicationName} v{_applicationVersion}";
                }

                LogFile.WriteLog("アプリケーション設定が完了しました");
            }
            catch (Exception ex)
            {
                ErrLogFile.WriteLog($"アプリケーション設定エラー: {ex.Message}");
                // 設定エラーは致命的ではないので続行
            }
        }

        #endregion

        #region リソース管理

        /// <summary>
        /// 通常のクリーンアップ処理
        /// </summary>
        private void PerformCleanup()
        {
            try
            {
                LogFile.WriteLog("リソースのクリーンアップを開始します");

                // FileWatcherManagerの解放
                CleanupFileWatcherManager();

                // DatabaseManagerの解放
                CleanupDatabaseManager();

                // ServiceProviderの解放
                CleanupServiceProvider();

                // 一時ファイルのクリーンアップ
                CleanupTemporaryFiles();

                // ガベージコレクション実行
                PerformGarbageCollection();

                LogFile.WriteLog("リソースのクリーンアップが完了しました");
            }
            catch (Exception ex)
            {
                ErrLogFile.WriteLog($"クリーンアップエラー: {ex.Message}");
            }
        }

        /// <summary>
        /// 緊急クリーンアップ処理
        /// </summary>
        private void PerformEmergencyCleanup()
        {
            try
            {
                LogFile.WriteLog("緊急クリーンアップを実行します");

                // 最小限の重要なリソース解放のみ
                try
                {
                    DatabaseManager.Instance?.Dispose();
                }
                catch { }

                try
                {
                    ServiceProvider?.GetService<IFileWatcherManager>()?.Dispose();
                }
                catch { }

                LogFile.WriteLog("緊急クリーンアップが完了しました");
            }
            catch
            {
                // 緊急時は例外を握りつぶす
            }
        }

        /// <summary>
        /// FileWatcherManagerのクリーンアップ
        /// </summary>
        private void CleanupFileWatcherManager()
        {
            try
            {
                var fileWatcherManager = ServiceProvider?.GetService<IFileWatcherManager>();
                if (fileWatcherManager is IDisposable disposableFileWatcher)
                {
                    disposableFileWatcher.Dispose();
                    LogFile.WriteLog("FileWatcherManagerを解放しました");
                }
            }
            catch (Exception ex)
            {
                ErrLogFile.WriteLog($"FileWatcherManager解放エラー: {ex.Message}");
            }
        }

        /// <summary>
        /// DatabaseManagerのクリーンアップ
        /// </summary>
        private void CleanupDatabaseManager()
        {
            try
            {
                var databaseManager = DatabaseManager.Instance;
                if (databaseManager != null)
                {
                    if (databaseManager is IDisposable disposableDb)
                    {
                        disposableDb.Dispose();
                        LogFile.WriteLog("DatabaseManagerを解放しました");
                    }
                }
            }
            catch (Exception ex)
            {
                ErrLogFile.WriteLog($"DatabaseManager解放エラー: {ex.Message}");
            }
        }

        /// <summary>
        /// ServiceProviderのクリーンアップ
        /// </summary>
        private void CleanupServiceProvider()
        {
            try
            {
                if (ServiceProvider is IDisposable disposableServiceProvider)
                {
                    disposableServiceProvider.Dispose();
                    LogFile.WriteLog("ServiceProviderを解放しました");
                }
            }
            catch (Exception ex)
            {
                ErrLogFile.WriteLog($"ServiceProvider解放エラー: {ex.Message}");
            }
        }

        /// <summary>
        /// 一時ファイルのクリーンアップ
        /// </summary>
        private void CleanupTemporaryFiles()
        {
            try
            {
                var tempPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TempFiles");
                if (Directory.Exists(tempPath))
                {
                    var tempFiles = Directory.GetFiles(tempPath, "*.*", SearchOption.AllDirectories);
                    var deletedCount = 0;
                    
                    foreach (var file in tempFiles)
                    {
                        try
                        {
                            if (File.GetLastWriteTime(file).AddHours(24) < DateTime.Now)
                            {
                                File.Delete(file);
                                deletedCount++;
                            }
                        }
                        catch
                        {
                            // 個別ファイルの削除エラーは無視
                        }
                    }
                    
                    if (deletedCount > 0)
                    {
                        LogFile.WriteLog($"一時ファイル {deletedCount} 件を削除しました");
                    }
                }
            }
            catch (Exception ex)
            {
                ErrLogFile.WriteLog($"一時ファイルクリーンアップエラー: {ex.Message}");
            }
        }

        /// <summary>
        /// ガベージコレクションの実行
        /// </summary>
        private void PerformGarbageCollection()
        {
            try
            {
                var beforeMemory = GC.GetTotalMemory(false);
                
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
                
                var afterMemory = GC.GetTotalMemory(false);
                var freedMemory = beforeMemory - afterMemory;
                
                LogFile.WriteLog($"ガベージコレクション完了 (解放メモリ: {freedMemory / 1024.0 / 1024.0:F2} MB)");
            }
            catch (Exception ex)
            {
                ErrLogFile.WriteLog($"ガベージコレクションエラー: {ex.Message}");
            }
        }

        #endregion

        #region 例外処理ヘルパー

        /// <summary>
        /// UI例外の処理
        /// </summary>
        private bool HandleUIException(Exception exception)
        {
            try
            {
                _totalExceptions++;
                
                var errorMessage = $"UIスレッド例外: {exception.Message}";
                var fullError = $"{errorMessage}\nスタックトレース: {exception.StackTrace}";

                // ログに記録
                ErrLogFile.WriteLog(fullError);

                // 重要度に応じて処理を分岐
                if (IsCriticalException(exception))
                {
                    ShowCriticalErrorDialog(exception);
                    return false; // アプリケーション終了
                }
                else
                {
                    ShowRecoverableErrorDialog(exception);
                    return true; // 処理継続
                }
            }
            catch (Exception ex)
            {
                LogCriticalError($"UI例外処理エラー: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 致命的例外の判定
        /// </summary>
        private bool IsCriticalException(Exception exception)
        {
            return exception is OutOfMemoryException ||
                   exception is StackOverflowException ||
                   exception is AccessViolationException ||
                   exception is SEHException ||
                   (exception is InvalidOperationException && exception.Message.Contains("データベース"));
        }

        /// <summary>
        /// 致命的例外の処理
        /// </summary>
        private void HandleCriticalException(Exception exception, string context)
        {
            try
            {
                var errorMessage = $"{context}: {exception?.Message}";
                var fullError = $"{errorMessage}\nスタックトレース: {exception?.StackTrace}";

                // 複数の手段でログを残す
                LogCriticalError(fullError);
                
                // Windowsイベントログにも記録
                EventLog.WriteEntry(_applicationName, fullError, EventLogEntryType.Error);

                // 致命的エラーダイアログの表示
                ShowCriticalErrorDialog(exception, context);
            }
            catch
            {
                // 最後の手段: デバッグ出力
                Debug.WriteLine($"致命的エラー: {exception?.Message}");
            }
        }

        #endregion

        #region ダイアログ表示

        /// <summary>
        /// 致命的エラーダイアログの表示
        /// </summary>
        private void ShowCriticalErrorDialog(Exception exception, string context = null)
        {
            try
            {
                var message = context != null 
                    ? $"{context}\n\n{exception.Message}\n\nアプリケーションを終了します。"
                    : $"致命的なエラーが発生しました。\n\n{exception.Message}\n\nアプリケーションを終了します。";
                
                MessageBox.Show(message, "致命的エラー", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch
            {
                // ダイアログ表示に失敗した場合は何もしない
            }
        }

        /// <summary>
        /// 回復可能エラーダイアログの表示
        /// </summary>
        private void ShowRecoverableErrorDialog(Exception exception)
        {
            try
            {
                var message = $"エラーが発生しましたが、アプリケーションは継続できます。\n\n{exception.Message}\n\n詳細はログファイルを確認してください。";
                MessageBox.Show(message, "エラー", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch
            {
                // ダイアログ表示に失敗した場合は何もしない
            }
        }

        #endregion

        #region ログ出力

        /// <summary>
        /// アプリケーション開始ログ
        /// </summary>
        private static void LogApplicationStartup()
        {
            try
            {
                LogFile.WriteLog("=".PadLeft(50, '='));
                LogFile.WriteLog($"{_applicationName} v{_applicationVersion} を開始しました");
                LogFile.WriteLog($"開始日時: {DateTime.Now:yyyy/MM/dd HH:mm:ss}");
                LogFile.WriteLog($"プロセスID: {Process.GetCurrentProcess().Id}");
                LogFile.WriteLog($"ワーキングディレクトリ: {Environment.CurrentDirectory}");
                LogFile.WriteLog($"CLRバージョン: {Environment.Version}");
                LogFile.WriteLog($"OSバージョン: {Environment.OSVersion}");
                LogFile.WriteLog("=".PadLeft(50, '='));
            }
            catch
            {
                // ログ出力エラーは無視
            }
        }

        /// <summary>
        /// アプリケーション終了ログ
        /// </summary>
        private static void LogApplicationShutdown(int exitCode)
        {
            try
            {
                var elapsed = DateTime.Now - _startTime;
                LogFile.WriteLog("=".PadLeft(50, '='));
                LogFile.WriteLog($"{_applicationName} を終了しました");
                LogFile.WriteLog($"終了日時: {DateTime.Now:yyyy/MM/dd HH:mm:ss}");
                LogFile.WriteLog($"実行時間: {elapsed.TotalMinutes:F2} 分");
                LogFile.WriteLog($"終了コード: {exitCode}");
                LogFile.WriteLog("=".PadLeft(50, '='));
            }
            catch
            {
                // ログ出力エラーは無視
            }
        }

        /// <summary>
        /// アプリケーション統計ログ
        /// </summary>
        private static void LogApplicationStatistics()
        {
            try
            {
                var elapsed = DateTime.Now - _startTime;
                LogFile.WriteLog("--- アプリケーション統計 ---");
                LogFile.WriteLog($"実行時間: {elapsed.TotalHours:F2} 時間");
                LogFile.WriteLog($"発生例外数: {_totalExceptions} 件");
                LogFile.WriteLog($"処理済み例外数: {_handledExceptions} 件");
                LogFile.WriteLog($"プロセスメモリ使用量: {Process.GetCurrentProcess().WorkingSet64 / 1024.0 / 1024.0:F2} MB");
            }
            catch
            {
                // ログ出力エラーは無視
            }
        }

        /// <summary>
        /// 致命的エラーログ
        /// </summary>
        private static void LogCriticalError(string message)
        {
            try
            {
                ErrLogFile.WriteLog($"[CRITICAL] {message}");
                Debug.WriteLine($"[CRITICAL] {message}");
            }
            catch
            {
                // 最後の手段
                try
                {
                    EventLog.WriteEntry(_applicationName, message, EventLogEntryType.Error);
                }
                catch
                {
                    // 何もできない
                }
            }
        }

        #endregion

        #region ユーティリティメソッド

        /// <summary>
        /// アプリケーションバージョンの取得
        /// </summary>
        private static string GetApplicationVersion()
        {
            try
            {
                var assembly = Assembly.GetExecutingAssembly();
                var version = assembly.GetName().Version;
                return $"{version.Major}.{version.Minor}.{version.Build}";
            }
            catch
            {
                return "Unknown";
            }
        }

        /// <summary>
        /// アプリケーションの安全な再起動
        /// </summary>
        public static void SafeRestart()
        {
            try
            {
                LogFile.WriteLog("アプリケーションの再起動を開始します");
                
                // 現在のアプリケーションのクリーンアップ
                if (Current is App currentApp)
                {
                    currentApp.PerformCleanup();
                }
                
                // 新しいプロセスを開始
                var currentExecutable = Process.GetCurrentProcess().MainModule.FileName;
                Process.Start(currentExecutable);
                
                // 現在のプロセスを終了
                Current?.Shutdown(0);
            }
            catch (Exception ex)
            {
                HandleCriticalException(ex, "アプリケーション再起動エラー");
            }
        }

        /// <summary>
        /// メモリ使用量の取得
        /// </summary>
        public static long GetMemoryUsage()
        {
            try
            {
                return Process.GetCurrentProcess().WorkingSet64;
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// アプリケーション状態の取得
        /// </summary>
        public static ApplicationStatus GetApplicationStatus()
        {
            return new ApplicationStatus
            {
                IsInitialized = _isInitialized,
                IsShuttingDown = _isShuttingDown,
                StartTime = _startTime,
                TotalExceptions = _totalExceptions,
                HandledException = _handledExceptions,
                MemoryUsageMB = GetMemoryUsage() / 1024.0 / 1024.0
            };
        }

        #endregion

        #region 内部クラス

        /// <summary>
        /// アプリケーション状態情報
        /// </summary>
        public class ApplicationStatus
        {
            public bool IsInitialized { get; set; }
            public bool IsShuttingDown { get; set; }
            public DateTime StartTime { get; set; }
            public int TotalExceptions { get; set; }
            public int HandledException { get; set; }
            public double MemoryUsageMB { get; set; }
            public TimeSpan Uptime => DateTime.Now - StartTime;
        }

        #endregion
    }
}