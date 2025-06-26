using JobManagementApp.Models;
using JobManagementApp.ViewModels;
using JobManagementApp.Manager;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace JobManagementApp.Manager
{
    /// <summary>
    /// 一回だけのファイルダウンロード専用クラス
    /// FileWatcherは使わず、初期表示時のみファイルをダウンロードする
    /// </summary>
    public class InitialFileDownloader : IDisposable
    {
        private FileCopyProgress _fileCopyProgress = new FileCopyProgress();
        private bool _disposed = false;

        private string _copyBasePath; // コピー先 フォルダ
        private readonly List<JobLogItemViewModel> _logs;
        private readonly DateTime _fromDateTime;
        private readonly DateTime _toDateTime;

        public event Action<string, string, int, int> ProgressChanged;

        /// <summary>
        /// コンストラクタ
        /// </summary>
        /// <param name="logs">ログ情報のリスト</param>
        /// <param name="copyPath">コピー先のパス</param>
        /// <param name="fromDateTime">検索開始日時</param>
        /// <param name="toDateTime">検索終了日時</param>
        public InitialFileDownloader(List<JobLogItemViewModel> logs, string copyPath, DateTime fromDateTime, DateTime toDateTime)
        {
            _copyBasePath = copyPath;
            _logs = logs ?? new List<JobLogItemViewModel>();
            _fromDateTime = fromDateTime;
            _toDateTime = toDateTime;

            // 進行状況イベントの転送
            _fileCopyProgress.ProgressChanged += (fileName, destPath, totalSize, progress) =>
            {
                if (!_disposed)
                {
                    ProgressChanged?.Invoke(fileName, destPath, totalSize, progress);
                }
            };
        }

        /// <summary>
        /// 一回だけのダウンロード実行
        /// </summary>
        public async Task StartDownload()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(InitialFileDownloader));
            }

            var tasks = new List<Task>();

            try
            {
                LogFile.WriteLog($"InitialFileDownloader: {_logs.Count}個のファイルのダウンロードを開始します");

                // 各ログファイルを並列でダウンロード
                foreach (JobLogItemViewModel log in _logs)
                {
                    var logInfo = new LogInfo
                    {
                        JobId = log.Id,
                        LogFromPath = Path.Combine(log.FilePath, log.FileName),
                        FileCount = log.FileCount,
                        IsMultiFile = log.FileType != emFileType.LOG
                    };

                    if (log.FileType == emFileType.LOG)
                    {
                        // 通常用（単一ファイル）
                        tasks.Add(DownloadSingleFile(logInfo));
                    }
                    else
                    {
                        // 複数ファイル用
                        tasks.Add(DownloadMultipleFiles(logInfo));
                    }
                }

                // 全てのタスクが完了するまで待機
                await Task.WhenAll(tasks);

                LogFile.WriteLog($"InitialFileDownloader: 全{tasks.Count}個のダウンロードタスクが完了しました");
            }
            catch (Exception ex)
            {
                ErrLogFile.WriteLog($"StartDownload エラー: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 単一ファイルのダウンロード
        /// </summary>
        private async Task DownloadSingleFile(LogInfo logInfo)
        {
            try
            {
                if (!File.Exists(logInfo.LogFromPath))
                {
                    ErrLogFile.WriteLog($"DownloadSingleFile: ファイルが見つかりません - {logInfo.LogFromPath}");
                    return;
                }

                await HandleFileCopy(logInfo);
            }
            catch (Exception ex)
            {
                ErrLogFile.WriteLog($"DownloadSingleFile エラー ({logInfo.LogFromPath}): {ex.Message}");
            }
        }

        /// <summary>
        /// 複数ファイルのダウンロード
        /// </summary>
        private async Task DownloadMultipleFiles(LogInfo logInfo)
        {
            try
            {
                // 既存の対象ファイルを取得
                List<FileInfo> existingFiles = GetLatestFiles(logInfo.LogFromPath, logInfo.FileCount);
                
                if (existingFiles.Count == 0)
                {
                    LogFile.WriteLog($"DownloadMultipleFiles: 対象ファイルが見つかりません - {logInfo.LogFromPath}");
                    return;
                }

                // 並列処理用のタスクリスト
                var copyTasks = new List<Task>();
                
                // 各ファイルを並列ダウンロード
                foreach (FileInfo file in existingFiles)
                {
                    var fileLogInfo = new LogInfo
                    {
                        JobId = logInfo.JobId,
                        LogFromPath = file.FullName,
                        FileCount = logInfo.FileCount,
                        IsMultiFile = true
                    };
                    
                    copyTasks.Add(HandleFileCopy(fileLogInfo));
                }
                
                // 全てのコピーを並列実行
                if (copyTasks.Count > 0)
                {
                    await Task.WhenAll(copyTasks);
                    LogFile.WriteLog($"DownloadMultipleFiles: {copyTasks.Count}個のファイルのダウンロードが完了しました");
                }
            }
            catch (Exception ex)
            {
                ErrLogFile.WriteLog($"DownloadMultipleFiles エラー: {ex.Message}");
            }
        }

        /// <summary>
        /// 指定した件数分、最新のファイル情報を取得
        /// </summary>
        public List<FileInfo> GetLatestFiles(string fullPath, int fileCount)
        {
            try
            {
                var directory = new DirectoryInfo(Path.GetDirectoryName(fullPath));
                
                if (!directory.Exists)
                {
                    ErrLogFile.WriteLog($"GetLatestFiles: ディレクトリが存在しません - {directory.FullName}");
                    return new List<FileInfo>();
                }

                var files = directory.GetFiles()
                    .Where(t => t.LastWriteTime >= _fromDateTime && 
                            t.LastWriteTime <= _toDateTime)
                    .Where(f => f.Name.Contains(Path.GetFileName(fullPath)))
                    .OrderByDescending(f => f.LastWriteTime)
                    .Take(fileCount)
                    .ToList();
                
                LogFile.WriteLog($"GetLatestFiles: {files.Count}個のファイルを取得しました ({Path.GetFileName(fullPath)})");
                return files;
            }
            catch (Exception ex)
            {
                ErrLogFile.WriteLog($"GetLatestFiles エラー: {ex.Message}");
                return new List<FileInfo>();
            }
        }

        /// <summary>
        /// ファイルコピー処理
        /// </summary>
        private async Task HandleFileCopy(LogInfo info)
        {
            try
            {
                // コピー先フォルダパスがセットされていない場合、カレントディレクトリをセット
                if (string.IsNullOrEmpty(_copyBasePath))
                {
                    _copyBasePath = AppDomain.CurrentDomain.BaseDirectory;
                }

                // 日付のコピーフォルダパス 作成
                string todayCopyPath = Path.Combine(_copyBasePath, DateTime.Now.ToString("yyyyMMdd"));
                string copyPath = Path.Combine(todayCopyPath, info.JobId);

                // コピー先フォルダが存在しない場合、フォルダ 作成
                if (!Directory.Exists(copyPath)) 
                {
                    Directory.CreateDirectory(copyPath);
                }

                // コピー元ファイル
                string fromFilePath = info.LogFromPath;
                string toFilePath = Path.Combine(copyPath, Path.GetFileName(info.LogFromPath));

                // ファイルが既に存在し、同じサイズの場合はスキップ
                if (File.Exists(toFilePath))
                {
                    var sourceInfo = new FileInfo(fromFilePath);
                    var destInfo = new FileInfo(toFilePath);
                    
                    if (sourceInfo.Length == destInfo.Length && 
                        Math.Abs((sourceInfo.LastWriteTime - destInfo.LastWriteTime).TotalSeconds) < 2)
                    {
                        LogFile.WriteLog($"HandleFileCopy: ファイルは既に最新です - {Path.GetFileName(fromFilePath)}");
                        return;
                    }
                }

                // コピー実施
                await _fileCopyProgress.CopyFile(fromFilePath, toFilePath);
                LogFile.WriteLog($"HandleFileCopy: ファイルコピー完了 - {Path.GetFileName(fromFilePath)}");
            }
            catch (Exception ex)
            {
                ErrLogFile.WriteLog($"HandleFileCopy エラー: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// ダウンロード統計情報を取得
        /// </summary>
        public DownloadStatistics GetStatistics()
        {
            try
            {
                return new DownloadStatistics
                {
                    TotalTargetFiles = _logs.Count,
                    ProcessedFiles = 0, // 実際の処理では適切にカウント
                    SuccessFiles = 0,   // 実際の処理では適切にカウント
                    ErrorFiles = 0,     // 実際の処理では適切にカウント
                    StartTime = DateTime.Now,
                    EndTime = DateTime.Now
                };
            }
            catch (Exception ex)
            {
                ErrLogFile.WriteLog($"GetStatistics エラー: {ex.Message}");
                return new DownloadStatistics();
            }
        }

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
                    // イベントハンドラーをクリア
                    ProgressChanged = null;

                    // FileCopyProgressの解放
                    _fileCopyProgress?.Dispose();

                    _disposed = true;
                    LogFile.WriteLog("InitialFileDownloader: リソースを解放しました");
                }
                catch (Exception ex)
                {
                    ErrLogFile.WriteLog($"InitialFileDownloader Dispose エラー: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// ファイナライザー
        /// </summary>
        ~InitialFileDownloader()
        {
            Dispose(false);
        }

        #endregion

        #region 内部クラス

        /// <summary>
        /// ダウンロード統計情報
        /// </summary>
        public class DownloadStatistics
        {
            public int TotalTargetFiles { get; set; }
            public int ProcessedFiles { get; set; }
            public int SuccessFiles { get; set; }
            public int ErrorFiles { get; set; }
            public DateTime StartTime { get; set; }
            public DateTime EndTime { get; set; }

            public TimeSpan Duration => EndTime - StartTime;
            public double SuccessRate => TotalTargetFiles > 0 ? (double)SuccessFiles / TotalTargetFiles * 100 : 0;
            public bool IsCompleted => ProcessedFiles >= TotalTargetFiles;
        }

        #endregion
    }
}