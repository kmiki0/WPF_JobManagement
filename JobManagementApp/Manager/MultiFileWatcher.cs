using JobManagementApp.Models;
using JobManagementApp.ViewModels;
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
    public class MultiFileWatcher : IDisposable
    {
        private readonly Dictionary<string, FileSystemWatcher> _watchers = new Dictionary<string, FileSystemWatcher>();
        private readonly Dictionary<string, FileSystemWatcher> _multiWatchers = new Dictionary<string, FileSystemWatcher>();
        private readonly Dictionary<string, SemaphoreSlim> _fileSemaphores = new Dictionary<string, SemaphoreSlim>();
        private readonly FileCopyProgress _fileCopyProgress = new FileCopyProgress();
        private bool _isStopped = false;
        private string _copyBasePath; // コピー先 フォルダ
        private readonly string _fileNamePrefix;

        public event Action<string, int, int> ProgressChanged;

        public MultiFileWatcher(List<JobLogItemViewModel> logs, string copyDirectoryPath)
        {
            // コピー先フォルダ（元）
            _copyBasePath = copyDirectoryPath;

            // 非同期処理
            foreach (JobLogItemViewModel log in logs)
            {
                string fullPath = Path.Combine(log.FilePath, log.FileName);
                // 使用するFileWatcherの分岐
                if (log.FileType == emFileType.LOG)
                {
                    // 通常用FileWatcherを使用
                    Task.Run(() => AddFileToWatch(fullPath));
                }
                else
                {
                    // 複数用FileWatcherを使用
                    Task.Run(() => AddMultiFileToWatch(fullPath, log.FileCount));
                }
            }

            _fileCopyProgress.ProgressChanged += (fileName, totalSize, progress) =>
            {
                if (!_isStopped)
                {
                    ProgressChanged?.Invoke(fileName, totalSize, progress);
                }
            };
        }


        // FileWatcherに登録
        private async Task AddFileToWatch(string fullPath, bool isMulti = false)
        {
            var watcher = new FileSystemWatcher(Path.GetDirectoryName(fullPath))
            {
                Filter = Path.GetFileName(fullPath),
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size,
            };

            watcher.Changed += async (sender, e) => await OnChanged(fullPath);
            watcher.EnableRaisingEvents = true;

            if (isMulti)
            {
                // 複数用
                _multiWatchers[fullPath] = watcher;
            }
            else
            {
                // 通常用
                _watchers[fullPath] = watcher;
            }

            await OnChanged(fullPath);
        }

        /// <summary>
        /// (複数ファイル用) FileWatcherに登録 
        /// </summary>
        /// <param name="fullPath">対象のファイルパス</param>
        /// <param name="fileCount">範囲検索するファイル数</param>
        private async Task AddMultiFileToWatch(string fullPath, int fileCount)
        {
            // （前方一致）ファイル名
            var watcher = new FileSystemWatcher(Path.GetDirectoryName(fullPath))
            {
                Filter = $"*{Path.GetFileName(fullPath)}",
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size,
            };

            watcher.Changed += async (sender, e) => await OnMultiFileChanged(fullPath, fileCount);
            watcher.EnableRaisingEvents = true;

            _watchers[fullPath] = watcher;

            // 初回は登録
            await OnMultiFileChanged(fullPath, fileCount);
        }


        // ファイルの変更を検知して、数秒後のファイルを比較して変更がなければコピーする
        // コピー先のフォルダを指定するために、「ジョブID」が必要
        // LineConuntの判断するために、「FileType」が必要
        private async Task OnChanged(string fullPath)
        {
            if (_isStopped) return;

            // ① 検知直後のファイル情報 取得
            FileInfo beforeFile = new FileInfo(fullPath);
            await Task.Delay(3000); // 3秒待機
            // ② 検知してから数秒後のファイル情報 取得
            FileInfo afterFile = new FileInfo(fullPath);

            // ①と②のファイル情報を比較して、同じであればジョブ終了とする
            if (beforeFile.Length == afterFile.Length && beforeFile.LastWriteTime == afterFile.LastWriteTime)
            {
                // コピー先フォルダパスがセットされていない場合、カレントディレクトリをセット
                if (string.IsNullOrEmpty(_copyBasePath))
                {
                    _copyBasePath = AppDomain.CurrentDomain.BaseDirectory;
                }

                // 日付のコピーフォルダパス 作成
                var todayCopyPath = Path.Combine(_copyBasePath, DateTime.Now.ToString("yyyyMMdd"));
                // 機能ID 付与
                var copyPath = Path.Combine(todayCopyPath, "PGID");
                
                // コピー先フォルダが存在しない場合、フォルダ 作成
                if (!Directory.Exists(copyPath)) Directory.CreateDirectory(copyPath); 

                // コピー元ファイル
                string parentFilePath = fullPath;
                string copyFilePath = Path.Combine(copyPath, Path.GetFileName(fullPath));

                // コピー実施
                await _fileCopyProgress.CopyFile(parentFilePath, copyFilePath);
            }
        }

        private async Task OnMultiFileChanged(string fullPath, int fileCount)
        {
            if (_isStopped) return;

            // 最新の5件のファイルを取得して監視対象を更新
            var latestFiles = GetLatestFiles(fullPath, fileCount);

            foreach (var file in latestFiles)
            {
                if (!_multiWatchers.ContainsKey(file.FullName))
                {
                    await AddFileToWatch(file.FullName, true);
                }
            }

            // 古いファイルの監視を解除
            var filesToRemove = _multiWatchers.Keys.Except(latestFiles.Select(f => f.FullName)).ToList();
            foreach (var filePath in filesToRemove)
            {
                _multiWatchers[filePath].Dispose();
                _multiWatchers.Remove(filePath);
            }
        }

        // 指定した件数分、ファイル情報を取得
        public List<FileInfo> GetLatestFiles(string fullPath, int fileCount)
        {
            var directory = new DirectoryInfo(Path.GetDirectoryName(fullPath));
            return directory.GetFiles()
                .Where(f => f.Name.Contains(Path.GetFileName(fullPath)))
                .OrderByDescending(f => f.LastWriteTime)
                .Take(fileCount)
                .ToList();
        }

        public void Dispose()
        {
            foreach (var watcher in _watchers.Values)
            {
                watcher.Dispose();
            }

            foreach (var multiWatchers in _multiWatchers.Values)
            {
                multiWatchers.Dispose();
            }
        }
    
    }

}
