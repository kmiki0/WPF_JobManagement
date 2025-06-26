using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using JobManagementApp.Models;
using JobManagementApp.Manager;

namespace JobManagementApp.Manager
{
    /// <summary>
    /// FileWatcherManagerインターフェース - 簡略化版
    /// 継続監視は行わず、互換性維持のためのインターフェースのみ提供
    /// </summary>
    public interface IFileWatcherManager : IDisposable
    {
        // 互換性のため残しているが、実際の機能は最小限
        FileSystemWatcher GetSingleWatcher(string path);
        FileSystemWatcher GetMultiWatcher(string path);

        void AddSingleWatcher(string path, FileSystemWatcher watcher);
        void AddMultiWatcher(string path, FileSystemWatcher watcher);
        void AddLogInfo(string path, LogInfo logInfo);
        void RemoveMultiWatcher(List<FileInfo> fileInfos, LogInfo logInfo);
        void RemoveAllWhereJobId(string jobId);

        IReadOnlyDictionary<string, FileSystemWatcher> GetAllSingleWatchers();
        IReadOnlyDictionary<string, FileSystemWatcher> GetAllMultiWatchers();
        IReadOnlyDictionary<string, LogInfo> GetAllLogInfos();

        // 新しいメソッド: ファイル情報の取得のみ
        List<FileInfo> GetAvailableFiles(string directoryPath, string filePattern, DateTime fromDate, DateTime toDate, int maxCount);
        bool IsFileAccessible(string filePath);
    }

    /// <summary>
    /// FileWatcherManager実装 - 簡略化版
    /// FileWatcherによる継続監視は行わず、必要最小限の機能のみ提供
    /// </summary>
    public class FileWatcherManager : IFileWatcherManager
    {
        // 互換性のため維持しているが、実際は使用しない
        private readonly Dictionary<string, FileSystemWatcher> _watchers = new Dictionary<string, FileSystemWatcher>();
        private readonly Dictionary<string, FileSystemWatcher> _multiWatchers = new Dictionary<string, FileSystemWatcher>();
        private readonly Dictionary<string, LogInfo> _logInfos = new Dictionary<string, LogInfo>();
        
        private bool _disposed = false;

        /// <summary>
        /// コンストラクタ
        /// </summary>
        public FileWatcherManager()
        {
            LogFile.WriteLog("FileWatcherManager: 簡略化版で初期化しました（継続監視は無効）");
        }

        #region 互換性維持のためのメソッド（実際の監視は行わない）

        /// <summary>
        /// 単一ファイル監視取得（互換性のみ、実際の監視は行わない）
        /// </summary>
        public FileSystemWatcher GetSingleWatcher(string path)
        {
            if (_disposed || string.IsNullOrEmpty(path))
                return null;

            _watchers.TryGetValue(path, out var watcher);
            return watcher;
        }

        /// <summary>
        /// 複数ファイル監視取得（互換性のみ、実際の監視は行わない）
        /// </summary>
        public FileSystemWatcher GetMultiWatcher(string path)
        {
            if (_disposed || string.IsNullOrEmpty(path))
                return null;

            _multiWatchers.TryGetValue(path, out var watcher);
            return watcher;
        }

        /// <summary>
        /// 単一ファイル監視追加（互換性のみ、実際の監視は開始しない）
        /// </summary>
        public void AddSingleWatcher(string path, FileSystemWatcher watcher)
        {
            if (_disposed || string.IsNullOrEmpty(path) || watcher == null)
                return;

            try
            {
                // 監視は開始せず、参照のみ保持
                watcher.EnableRaisingEvents = false;
                _watchers[path] = watcher;
                
                LogFile.WriteLog($"FileWatcherManager: 単一ファイル監視を登録しました（監視無効）: {path}");
            }
            catch (Exception ex)
            {
                ErrLogFile.WriteLog($"AddSingleWatcher エラー: {ex.Message}");
            }
        }

        /// <summary>
        /// 複数ファイル監視追加（互換性のみ、実際の監視は開始しない）
        /// </summary>
        public void AddMultiWatcher(string path, FileSystemWatcher watcher)
        {
            if (_disposed || string.IsNullOrEmpty(path) || watcher == null)
                return;

            try
            {
                // 監視は開始せず、参照のみ保持
                watcher.EnableRaisingEvents = false;
                _multiWatchers[path] = watcher;
                
                LogFile.WriteLog($"FileWatcherManager: 複数ファイル監視を登録しました（監視無効）: {path}");
            }
            catch (Exception ex)
            {
                ErrLogFile.WriteLog($"AddMultiWatcher エラー: {ex.Message}");
            }
        }

        /// <summary>
        /// ログ情報追加
        /// </summary>
        public void AddLogInfo(string path, LogInfo logInfo)
        {
            if (_disposed || string.IsNullOrEmpty(path) || logInfo == null)
                return;

            try
            {
                _logInfos[path] = logInfo;
                LogFile.WriteLog($"FileWatcherManager: ログ情報を登録しました: {path}");
            }
            catch (Exception ex)
            {
                ErrLogFile.WriteLog($"AddLogInfo エラー: {ex.Message}");
            }
        }

        /// <summary>
        /// 複数ファイル監視削除（互換性のみ）
        /// </summary>
        public void RemoveMultiWatcher(List<FileInfo> fileInfos, LogInfo logInfo)
        {
            if (_disposed || fileInfos == null || logInfo == null)
                return;

            try
            {
                var filesToRemove = _multiWatchers.Keys
                    .Except(fileInfos.Select(f => f.FullName))
                    .ToList();
                
                foreach (var filePath in filesToRemove)
                {
                    // 対象としているファイル名 検証
                    var fileName = Path.GetFileName(filePath);
                    if (fileName.Contains(Path.GetFileName(logInfo.LogFromPath)))
                    {
                        if (_multiWatchers.ContainsKey(filePath))
                        {
                            _multiWatchers[filePath]?.Dispose();
                            _multiWatchers.Remove(filePath);
                        }

                        // LogInfo型の状態も更新
                        _logInfos.Remove(filePath);
                        
                        LogFile.WriteLog($"FileWatcherManager: 複数ファイル監視を削除しました: {filePath}");
                    }
                }
            }
            catch (Exception ex)
            {
                ErrLogFile.WriteLog($"RemoveMultiWatcher エラー: {ex.Message}");
            }
        }

        /// <summary>
        /// ジョブIDによる監視削除
        /// </summary>
        public void RemoveAllWhereJobId(string jobId)
        {
            if (_disposed || string.IsNullOrEmpty(jobId))
                return;

            try
            {
                // _logInfos から対象のキーを探す
                var keysToRemove = _logInfos
                    .Where(kvp => kvp.Value.JobId == jobId)
                    .Select(kvp => kvp.Key)
                    .ToList();

                foreach (var key in keysToRemove)
                {
                    if (key != null)
                    {
                        // _watchers から削除
                        if (_watchers.ContainsKey(key))
                        {
                            _watchers[key]?.Dispose();
                            _watchers.Remove(key);
                        }

                        // _multiWatchers から削除
                        if (_multiWatchers.ContainsKey(key))
                        {
                            _multiWatchers[key]?.Dispose();
                            _multiWatchers.Remove(key);
                        }

                        // _logInfos から削除
                        _logInfos.Remove(key);
                    }
                }

                LogFile.WriteLog($"FileWatcherManager: ジョブID {jobId} の監視を全て削除しました");
            }
            catch (Exception ex)
            {
                ErrLogFile.WriteLog($"RemoveAllWhereJobId エラー: {ex.Message}");
            }
        }

        #endregion

        #region 読み取り専用アクセスメソッド

        /// <summary>
        /// 全ての単一ファイル監視を取得
        /// </summary>
        public IReadOnlyDictionary<string, FileSystemWatcher> GetAllSingleWatchers()
        {
            return _watchers;
        }

        /// <summary>
        /// 全ての複数ファイル監視を取得
        /// </summary>
        public IReadOnlyDictionary<string, FileSystemWatcher> GetAllMultiWatchers()
        {
            return _multiWatchers;
        }

        /// <summary>
        /// 全てのログ情報を取得
        /// </summary>
        public IReadOnlyDictionary<string, LogInfo> GetAllLogInfos()
        {
            return _logInfos;
        }

        #endregion

        #region 新しい機能メソッド

        /// <summary>
        /// 利用可能なファイル一覧を取得（実際に使用する機能）
        /// </summary>
        public List<FileInfo> GetAvailableFiles(string directoryPath, string filePattern, DateTime fromDate, DateTime toDate, int maxCount)
        {
            try
            {
                if (_disposed || string.IsNullOrEmpty(directoryPath))
                    return new List<FileInfo>();

                var directory = new DirectoryInfo(directoryPath);
                if (!directory.Exists)
                {
                    ErrLogFile.WriteLog($"GetAvailableFiles: ディレクトリが存在しません - {directoryPath}");
                    return new List<FileInfo>();
                }

                var files = directory.GetFiles()
                    .Where(f => f.LastWriteTime >= fromDate && f.LastWriteTime <= toDate)
                    .Where(f => string.IsNullOrEmpty(filePattern) || f.Name.Contains(filePattern))
                    .OrderByDescending(f => f.LastWriteTime)
                    .Take(maxCount)
                    .ToList();

                LogFile.WriteLog($"GetAvailableFiles: {files.Count}個のファイルを取得しました（{directoryPath}）");
                return files;
            }
            catch (Exception ex)
            {
                ErrLogFile.WriteLog($"GetAvailableFiles エラー: {ex.Message}");
                return new List<FileInfo>();
            }
        }

        /// <summary>
        /// ファイルアクセス可能性をチェック
        /// </summary>
        public bool IsFileAccessible(string filePath)
        {
            try
            {
                if (_disposed || string.IsNullOrEmpty(filePath))
                    return false;

                if (!File.Exists(filePath))
                    return false;

                // ファイルが使用中でないかチェック
                using (var stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    return true;
                }
            }
            catch (IOException)
            {
                // ファイルが使用中
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                // アクセス権限なし
                return false;
            }
            catch (Exception ex)
            {
                ErrLogFile.WriteLog($"IsFileAccessible エラー: {ex.Message}");
                return false;
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
                    // 全ての監視を停止・破棄
                    foreach (var watcher in _watchers.Values)
                    {
                        watcher?.Dispose();
                    }

                    foreach (var watcher in _multiWatchers.Values)
                    {
                        watcher?.Dispose();
                    }

                    _watchers.Clear();
                    _multiWatchers.Clear();
                    _logInfos.Clear();

                    _disposed = true;
                    LogFile.WriteLog("FileWatcherManager: リソースを解放しました");
                }
                catch (Exception ex)
                {
                    ErrLogFile.WriteLog($"FileWatcherManager Dispose エラー: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// ファイナライザー
        /// </summary>
        ~FileWatcherManager()
        {
            Dispose(false);
        }

        #endregion
    }
}