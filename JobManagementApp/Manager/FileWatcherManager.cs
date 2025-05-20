using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using JobManagementApp.Models;

namespace JobManagementApp.Manager
{
    public interface IFileWatcherManager : IDisposable
    {
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
    }

    public class FileWatcherManager : IFileWatcherManager
    {
        private readonly Dictionary<string, FileSystemWatcher> _watchers = new Dictionary<string, FileSystemWatcher>();
        private readonly Dictionary<string, FileSystemWatcher> _multiWatchers = new Dictionary<string, FileSystemWatcher>();
        private readonly Dictionary<string, LogInfo> _logInfos = new Dictionary<string, LogInfo>();

        // ============== 全て取得 =================
        public IReadOnlyDictionary<string, FileSystemWatcher> GetAllSingleWatchers()
        {
            return _watchers;
        }

        public IReadOnlyDictionary<string, FileSystemWatcher> GetAllMultiWatchers()
        {
            return _multiWatchers;
        }
        public IReadOnlyDictionary<string, LogInfo> GetAllLogInfos()
        {
            return _logInfos;
        }

        // ============== 単体取得 =================
        public FileSystemWatcher GetSingleWatcher(string path)
        {
            _watchers.TryGetValue(path, out var watcher);
            return watcher;
        }
        public FileSystemWatcher GetMultiWatcher(string path)
        {
            _multiWatchers.TryGetValue(path, out var watcher);
            return watcher;
        }

        // ============== 追加 =================
        public void AddSingleWatcher(string path, FileSystemWatcher watcher)
        {
            _watchers[path] = watcher;
        }
        public void AddMultiWatcher(string path, FileSystemWatcher watcher)
        {
            _multiWatchers[path] = watcher;
        }
        public void AddLogInfo(string path, LogInfo logInfo)
        {
            _logInfos[path] = logInfo;
        }

        // ============== 削除 =================
        public void RemoveMultiWatcher(List<FileInfo> fileInfos, LogInfo logInfo)
        {
            var filesToRemove = _multiWatchers.Keys.Except(fileInfos.Select(f => f.FullName)).ToList();
            foreach (var filePath in filesToRemove)
            {
                // 対象としているファイル名 検証
                var fileName = Path.GetFileName(filePath);
                if (fileName.Contains(Path.GetFileName(logInfo.LogFromPath)))
                {
                    _multiWatchers[filePath].Dispose();
                    _multiWatchers.Remove(filePath);

                    // LogInfo型の状態も更新
                    _logInfos.Remove(filePath);
                }
            }
        }
        public void RemoveAllWhereJobId(string jobId)
        {
            // _logInfos から対象のキーを探す
            var keysToRemove = _logInfos.Where(kvp => kvp.Value.JobId == jobId).Select(kvp => kvp.Key).ToList();

            foreach (var key in keysToRemove)
            { 
                if (key != null)
                {
                    // _watchers から削除
                    if (_watchers.ContainsKey(key))
                    {
                        _watchers[key].Dispose(); // リソース解放
                        _watchers.Remove(key);
                    }

                    // _multiWatchers から削除
                    if (_multiWatchers.ContainsKey(key))
                    {
                        _multiWatchers[key].Dispose(); // リソース解放
                        _multiWatchers.Remove(key);
                    }

                    // _logInfos から削除
                    _logInfos.Remove(key);
                }
            }
        }

        public void Dispose()
        {
            _watchers.Clear();
            _multiWatchers.Clear();
            _logInfos.Clear();
        }
    }
}

