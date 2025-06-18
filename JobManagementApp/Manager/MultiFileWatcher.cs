using JobManagementApp.Models;
using JobManagementApp.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace JobManagementApp.Manager
{
    public class MultiFileWatcher
    {
        private IFileWatcherManager _fw = App.ServiceProvider.GetRequiredService<IFileWatcherManager>();

        private FileCopyProgress _fileCopyProgress = new FileCopyProgress();

        private bool _isStopped = false;
        private string _copyBasePath; // コピー先 フォルダ
        private readonly List<JobLogItemViewModel> _logs;
        private readonly DateTime _fromDateTime;
        private readonly DateTime _toDateTime;

        public event Action<string, string, int, int> ProgressChanged;

        public async Task StartMonitoring()
        {
            var tasks = new List<Task>();

            // 非同期処理
            foreach (JobLogItemViewModel log in _logs)
            {
                // watcher 受渡用の型にセット
                var logInfo = new LogInfo
                {
                    JobId = log.Id,
                    LogFromPath = Path.Combine(log.FilePath, log.FileName),
                    FileCount = log.FileCount,
                };

                // 使用するFileWatcherの分岐
                if (log.FileType == emFileType.LOG)
                {
                    // 通常用FileWatcherを使用
                    logInfo.IsMultiFile = false;
                    tasks.Add(AddFileToWatch(logInfo));
                }
                else
                {
                    // 複数用FileWatcherを使用
                    logInfo.IsMultiFile = true;
                    tasks.Add(AddMultiFileToWatch(logInfo));
                }
            }

            // 全てのタスクが完了するまで待機
            await Task.WhenAll(tasks).ContinueWith(async (x) => {

                if (x.IsCompleted)
                {
                    // _watchersの中身に対して並列ダウンロード処理を実行
                    var downloadTasks = new List<Task>();

                    foreach (var info in _fw.GetAllLogInfos().Values)
                    {
                        downloadTasks.Add(HandleFileCopy(info));
                    }

                    // ダウンロード処理の完了を待つ
                    await Task.WhenAll(downloadTasks);
                }
            });
        }


        public MultiFileWatcher(List<JobLogItemViewModel> logs, string copyPath, DateTime fromDateTime, DateTime toDateTime)
        {
            // 初期値 セット
            _copyBasePath = copyPath;
            _logs = logs;
            _fromDateTime = fromDateTime;
            _toDateTime = toDateTime;

            // 返すイベント
            _fileCopyProgress.ProgressChanged += (fileName, destPath, totalSize, progress) =>
            {
                if (!_isStopped)
                {
                    ProgressChanged?.Invoke(fileName, destPath, totalSize, progress);
                }
            };
        }

        // FileWatcherに登録
        private async Task AddFileToWatch(LogInfo info)
        {
            var watcher = new FileSystemWatcher(Path.GetDirectoryName(info.LogFromPath))
            {
                Filter = Path.GetFileName(info.LogFromPath),
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size,
            };

            watcher.Changed += async (sender, e) => await OnChanged(info);
            watcher.EnableRaisingEvents = true;


            if (info.IsMultiFile)
            {
                // 複数用
                _fw.AddMultiWatcher(info.LogFromPath, watcher);
                _fw.AddLogInfo(info.LogFromPath, info);
                await HandleFileCopy(info);
            }
            else
            {
                // 通常用
                _fw.AddSingleWatcher(info.LogFromPath, watcher);
                _fw.AddLogInfo(info.LogFromPath, info);
            }
        }

        /// <summary>
        /// (複数ファイル用) FileWatcherに登録 - 初回即座コピー対応版
        /// </summary>
        /// <param name="info">ログ情報</param>
        private async Task AddMultiFileToWatch(LogInfo info)
        {
            // （前方一致）ファイル名
            var watcher = new FileSystemWatcher(Path.GetDirectoryName(info.LogFromPath))
            {
                Filter = $"*{Path.GetFileName(info.LogFromPath)}",
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size,
            };

            watcher.Changed += async (sender, e) => await OnMultiFileChanged(info);
            watcher.EnableRaisingEvents = true;

            _fw.AddSingleWatcher(info.LogFromPath, watcher);

            // 初回は既存ファイルを即座にコピー
            await InitialCopyExistingFiles(info);
        }

        /// <summary>
        /// 初回のみ：既存ファイルを即座にコピー
        /// </summary>
        private async Task InitialCopyExistingFiles(LogInfo info)
        {
            try
            {
                // 既存の対象ファイルを取得
                List<FileInfo> existingFiles = GetLatestFiles(info.LogFromPath, info.FileCount);
                
                // 各ファイルを即座にコピー（待機なし）
                foreach (FileInfo file in existingFiles)
                {
                    var fileLogInfo = new LogInfo
                    {
                        JobId = info.JobId,
                        LogFromPath = file.FullName,
                        FileCount = info.FileCount,
                        IsMultiFile = true
                    };
                    
                    // コピー
                    await HandleFileCopy(fileLogInfo);
                    
                    // FileWatcher登録のみ（コピーなし）
                    RegisterFileWatcherOnly(fileLogInfo);
                }
            }
            catch (Exception ex)
            {
                ErrLogFile.WriteLog($"InitialCopyExistingFiles エラー: {ex.Message}");
            }
        }

        /// <summary>
        /// FileWatcher登録のみ
        /// </summary>
        private void RegisterFileWatcherOnly(LogInfo info)
        {
            var watcher = new FileSystemWatcher(Path.GetDirectoryName(info.LogFromPath))
            {
                Filter = Path.GetFileName(info.LogFromPath),
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size,
            };

            watcher.Changed += async (sender, e) => await OnChanged(info);
            watcher.EnableRaisingEvents = true;

            if (info.IsMultiFile)
            {
                _fw.AddMultiWatcher(info.LogFromPath, watcher);
                _fw.AddLogInfo(info.LogFromPath, info);
            }
            else
            {
                _fw.AddSingleWatcher(info.LogFromPath, watcher);
                _fw.AddLogInfo(info.LogFromPath, info);
            }
        }

        private static readonly ConcurrentDictionary<string, SemaphoreSlim> fileSemaphores = new ConcurrentDictionary<string, SemaphoreSlim>();

        private async Task OnChanged(LogInfo info)
        {
            if (_isStopped) return;

            var key = info.LogFromPath;
            var semaphore = fileSemaphores.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));

            await semaphore.WaitAsync();// 多重実行防止
            try
            {
                // ① 検知直後のファイル情報 取得
                FileInfo beforeFile = await GetFileInfoWithRetry(info.LogFromPath);

                await Task.Delay(2000); // 2秒待機

                // ② 検知してから数秒後のファイル情報 取得
                FileInfo afterFile = await GetFileInfoWithRetry(info.LogFromPath);

                // ①と②のファイル情報を比較して、同じであればジョブ終了とする
                if (beforeFile.Length == afterFile.Length && beforeFile.LastWriteTime == afterFile.LastWriteTime)
                {
                    await HandleFileCopy(info);
                }
            }
                finally
            {
                semaphore.Release();

                // 使用後に辞書から削除（メモリリーク防止）
                if (semaphore.CurrentCount == 1)
                {
                    fileSemaphores.TryRemove(key, out _);
                }
            }
        }

        private async Task<FileInfo> GetFileInfoWithRetry(string path, int retryCount = 5, int delayMs = 500)
        {
            //for (int i = 0; i < retryCount; i++)
            //{
            try
            {
                var fileInfo = new FileInfo(path);
                using (var stream = fileInfo.Open(FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    // ファイルが開けたらロックされていないと判断
                    return fileInfo;
                }
            }
            catch (IOException)
            {
                await Task.Delay(delayMs);
            }
            //}

            throw new IOException($"ファイル {path} にアクセスできませんでした。");
        }

        private async Task OnMultiFileChanged(LogInfo info)
        {
            if (_isStopped) return;

            // 最新の5件のファイルを取得して監視対象を更新
            List<FileInfo> latestFiles = GetLatestFiles(info.LogFromPath, info.FileCount);

            foreach (FileInfo file in latestFiles)
            {
                //if (!_multiWatchers.ContainsKey(file.FullName))
                if (_fw.GetMultiWatcher(file.FullName) is null)
                {
                    var logInfo = new LogInfo{
                        JobId = info.JobId,
                        LogFromPath = file.FullName,
                        FileCount = info.FileCount,
                        IsMultiFile = true
                    };
                    await AddFileToWatch(logInfo);
                }
            }

            // 古いファイルの監視を解除
            _fw.RemoveMultiWatcher(latestFiles, info);
        }

        // 指定した件数分、ファイル情報を取得
        public List<FileInfo> GetLatestFiles(string fullPath, int fileCount)
        {
            var directory = new DirectoryInfo(Path.GetDirectoryName(fullPath));
            return directory.GetFiles()
                .Where(t => t.LastWriteTime >= _fromDateTime && 
                        t.LastWriteTime <= _toDateTime)  // 🆕 ToDateでも絞り込み
                .Where(f => f.Name.Contains(Path.GetFileName(fullPath)))
                .OrderByDescending(f => f.LastWriteTime)
                .Take(fileCount)
                .ToList();
        }

        private async Task HandleFileCopy(LogInfo info)
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
            if (!Directory.Exists(copyPath)) Directory.CreateDirectory(copyPath);

            // コピー元ファイル
            string fromFilePath = info.LogFromPath;
            string toFilePath = Path.Combine(copyPath, Path.GetFileName(info.LogFromPath));

            // コピー実施
            await _fileCopyProgress.CopyFile(fromFilePath, toFilePath);
        }
    }
}