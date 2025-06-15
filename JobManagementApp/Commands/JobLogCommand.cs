using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using JobManagementApp.Views;
using JobManagementApp.ViewModels;
using JobManagementApp.Models;
using JobManagementApp.Manager;
using JobManagementApp.Helpers;
using JobManagementApp.BaseClass;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace JobManagementApp.Commands
{
    public class JobLogCommand : JobCommandArgument
    {
        public MultiFileWatcher _multiFileWatcher;

        private readonly IFileWatcherManager _fw = App.ServiceProvider.GetRequiredService<IFileWatcherManager>();

        private readonly JobLogViewModel _vm;
        private readonly IJobLogModel _if;
        
        // ファイル監視状態を管理するためのコレクション
        private readonly ConcurrentDictionary<string, FileWatchingInfo> _watchingFiles;
        
        public JobLogCommand(JobLogViewModel VM, IJobLogModel IF)
        {
            _vm = VM;
            _if = IF;
            _watchingFiles = new ConcurrentDictionary<string, FileWatchingInfo>();

            Init();
        }

        private static ConcurrentDictionary<string, SemaphoreSlim> fileSemaphores;
        private Dictionary<string, FileSystemWatcher> watchers;

        /// <summary> 
        /// 初期化
        /// </summary> 
        private void Init()
        {
            fileSemaphores = new ConcurrentDictionary<string, SemaphoreSlim>();
            watchers = new Dictionary<string, FileSystemWatcher>();

            // キャッシュ読み込み
            UserFileManager manager = new UserFileManager();
            _vm.TempSavePath = manager.GetCache(manager.CacheKey_FilePath);

            // ジョブID 読み込み
            LoadJobId();

            // ログ一覧 読み込み
            LoadLogList();
        }

        // ログ一覧 読み込み
        public void LoadLogList()
        {
            var logList = new List<JobLogItemViewModel>();

            _if.GetJobLinkFile(_vm.Scenario, _vm.Eda).ContinueWith(x =>
            {
                // ジョブごとにUIを生成する
                foreach (JobLinkFile job in x.Result)
                {
                    // enum変換
                    emFileType fileType = Enum.TryParse(job.FILETYPE.ToString(), out fileType) ? fileType : emFileType.LOG;

                    var item = new JobLogItemViewModel {
                        Scenario = job.SCENARIO,
                        Eda = job.EDA,
                        Id = job.JOBID,
                        FilePath = job.FILEPATH,
                        FileName = job.FILENAME,
                        DisplayFileName = job.FILENAME,
                        FileType = fileType,
                        FileCount = job.FILECOUNT,
                        ObserverType = job.OBSERVERTYPE,
                        ObserverStatus = emObserverStatus.OBSERVER
                    };
                    logList.Add(item);
                }
                // 画面項目にセット
                _vm.Logs = new ObservableCollection<JobLogItemViewModel>(logList);

                // 関連ファイルの監視を開始
                StartMonitoring();
            });
        }

        /// <summary> 
        /// vmのジョブIDに値セット
        /// </summary> 
        public void LoadJobId()
        {
            // シナリオと枝番からジョブIDを検索
            _if.GetJobManegment(_vm.Scenario, _vm.Eda).ContinueWith(x =>
            {
                _vm.Id = x.Result.ID;
            });
        }

        /// <summary> 
        /// パス保存ボタン クリック処理
        /// </summary> 
        public void TempFolderButton_Click(object prm)
        {
            string path = prm.ToString().Trim();

            // 文字列の最後の文字が「¥」の場合、切り取る
            if (path.EndsWith("\\"))
            {
                path = path.Substring(0, path.Length - 1);
            }

            // 入力されたパスが正しいかチェック 
            if (Directory.Exists(path))
            {
                // 正しい場合、キャッシュに保存
                if (_if.SaveCachePath(path))
                {
                    // vmの値を更新
                    _vm.TempSavePath = path;
                    MessageBox.Show("キャッシュに保存しました。", "メッセージ", MessageBoxButton.OK, MessageBoxImage.Information, MessageBoxResult.OK, MessageBoxOptions.DefaultDesktopOnly);
                }
            }
            else
            {
                MessageBox.Show("入力されているフォルダ名が正しくありません。", "メッセージ", MessageBoxButton.OK, MessageBoxImage.Warning, MessageBoxResult.OK, MessageBoxOptions.DefaultDesktopOnly);
            }
        }

        /// <summary> 
        /// ログ追加ボタン クリック処理
        /// </summary> 
        public void AddLogButton_Click(object prm)
        {
            var jobPrm = prm as JobParamModel;

            // ViewModel 生成
            JobLogDetailViewModel vm = new JobLogDetailViewModel(new JobLogDetailModel(), jobPrm.Scenario, jobPrm.Eda);
            // 返却用のCloseイベント 上書き
            vm.RequestClose += LogDetailWindow_RequestClose;
            JobLogDetailWindow logDetailWindow = new JobLogDetailWindow(vm);
            var window = logDetailWindow as System.Windows.Window;
            // ウィンドウの表示位置　調整
            WindowHelper.SetWindowLocation(ref window);
            vm.window = logDetailWindow;
            logDetailWindow.DataContext = vm;
            logDetailWindow.ShowDialog();
        }
        
        /// <summary> 
        /// ログ編集ウィンドウ Closeイベント
        /// </summary> 
        private void LogDetailWindow_RequestClose(object sender, JobParamModel e)
        {
            // MainViewModelに通知するための処理を追加
            JobLogViewModel.RecreateViewModel(e);
        }

        /// <summary> 
        /// ファイルを開くボタン クリック処理
        /// </summary> 
        public void FolderButton_Click(object prm)
        {
            string path = prm?.ToString();
            //　空の場合、セーブしているフォルダ名 + yyyyMMdd + 機能IDでパス作成
            if (string.IsNullOrEmpty(path))
            {
                var yyyyMMdd = DateTime.Now.ToString("yyyyMMdd");
                path = Path.Combine(_vm.TempSavePath, yyyyMMdd);
                path = Path.Combine(path, _vm.Id); 
            }

            // フォルダが存在しない場合は作成
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }

            if (!string.IsNullOrEmpty(path))
            {
                Process.Start("explorer.exe", path);
            }
            else
            {
                MessageBox.Show("フォルダが生成されていません。", "メッセージ", MessageBoxButton.OK, MessageBoxImage.Warning, MessageBoxResult.OK, MessageBoxOptions.DefaultDesktopOnly);
            }
        }

        /// <summary> 
        /// 閉じるボタン クリック処理
        /// </summary> 
        public void CloseButton_Click(object prm)
        {
            if (!(prm is null))
            {
                Window window = prm as Window;

                if (window != null)
                {
                    window.Close();
                }
            }
        }

        // ==================================
        // 　ログ監視 - 改善版
        // ==================================
        public async Task StartMonitoring()
        {
            var _multiFileWatcher = new MultiFileWatcher(_vm.Logs.ToList(), _vm.TempSavePath, DateTime.Parse(MainViewModel.Instance.SearchFromDate));

            // イベント ファイルコピー時
            _multiFileWatcher.ProgressChanged += OnFileProgressChanged;

            // 監視開始
            await _multiFileWatcher.StartMonitoring();
        }

        /// <summary>
        /// ファイル進行状況変更イベント - 改善版
        /// </summary>
        private void OnFileProgressChanged(string filePath, string destPath, int totalSize, int percent)
        {
            try
            {
                var logInfo = _fw.GetAllLogInfos().Values.FirstOrDefault(x => x.LogFromPath == filePath);
                if (logInfo == null) return;

                var watchingKey = GetWatchingKey(logInfo, filePath);
                var watchingInfo = _watchingFiles.GetOrAdd(watchingKey, _ => new FileWatchingInfo(logInfo, filePath));

                if (logInfo.IsMultiFile)
                {
                    HandleMultiFileProgress(watchingInfo, filePath, destPath, totalSize, percent);
                }
                else
                {
                    HandleSingleFileProgress(watchingInfo, filePath, destPath, totalSize, percent);
                }
            }
            catch (Exception ex)
            {
                ErrLogFile.WriteLog($"OnFileProgressChanged エラー: {ex.Message}");
            }
        }

        /// <summary>
        /// シングルファイルの進行状況処理
        /// </summary>
        private void HandleSingleFileProgress(FileWatchingInfo watchingInfo, string filePath, string destPath, int totalSize, int percent)
        {
            var templateLog = FindTemplateLog(watchingInfo.OriginalLogInfo);
            if (templateLog != null)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    UpdateLogItemUI(templateLog, filePath, destPath, totalSize, percent);
                });
            }
        }

        /// <summary>
        /// マルチファイルの進行状況処理 - 正しい仕様実装
        /// </summary>
        private void HandleMultiFileProgress(FileWatchingInfo watchingInfo, string filePath, string destPath, int totalSize, int percent)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                var templateLog = FindTemplateLog(watchingInfo.OriginalLogInfo);
                if (templateLog == null) return;

                // 実際のファイル名から基本ファイル名を抽出
                var actualFileName = Path.GetFileName(filePath);
                var baseFileName = ExtractBaseFileName(actualFileName);

                // このファイルが元のテンプレートファイル名と一致するかチェック
                if (!baseFileName.Equals(templateLog.FileName, StringComparison.OrdinalIgnoreCase))
                    return;

                // この実ファイルが既に処理されているかチェック
                var existingFileLog = FindExistingFileLog(templateLog, actualFileName);

                if (existingFileLog != null)
                {
                    // 既存エントリを更新
                    UpdateLogItemUI(existingFileLog, filePath, destPath, totalSize, percent);
                    
                    if (percent >= 100)
                    {
                        existingFileLog.LineCount = GetLineCount(destPath, existingFileLog).ToString() + " 件";
                    }
                }
                else
                {
                    // 新しいファイルの場合
                    if (IsTemplateNotYetOverwritten(templateLog))
                    {
                        // 初回検出：テンプレートを実ファイル情報で上書き
                        OverwriteTemplateWithActualFile(templateLog, filePath, destPath, totalSize, percent);
                        
                        if (percent >= 100)
                        {
                            templateLog.LineCount = GetLineCount(destPath, templateLog).ToString() + " 件";
                        }
                    }
                    else
                    {
                        // 2回目以降：新しいエントリを追加
                        var newFileLog = CreateNewFileLogEntry(templateLog, filePath, destPath, totalSize, percent);
                        
                        if (newFileLog.ObserverStatus == emObserverStatus.SUCCESS)
                        {
                            newFileLog.LineCount = GetLineCount(destPath, newFileLog).ToString() + " 件";
                        }

                        var logList = _vm.Logs.ToList();
                        // テンプレートの次に挿入
                        var templateIndex = logList.IndexOf(templateLog);
                        if (templateIndex >= 0)
                        {
                            logList.Insert(templateIndex + 1, newFileLog);
                        }
                        else
                        {
                            logList.Add(newFileLog);
                        }
                        
                        _vm.Logs = new ObservableCollection<JobLogItemViewModel>(logList);
                    }
                }

                // 不要になったエントリの削除
                CleanupObsoleteFileEntries(templateLog);
            });
        }

        /// <summary>
        /// テンプレートログを検索
        /// </summary>
        private JobLogItemViewModel FindTemplateLog(LogInfo logInfo)
        {
            var baseFileName = Path.GetFileName(logInfo.LogFromPath);
            return _vm.Logs.FirstOrDefault(x => 
                x.FileName == baseFileName && 
                x.DisplayFileName == baseFileName &&
                x.FileType != emFileType.LOG); // ログファイル以外がマルチファイル対象
        }

        /// <summary>
        /// 既存のファイルログエントリを検索（テンプレートが実ファイル化されている場合も含む）
        /// </summary>
        private JobLogItemViewModel FindExistingFileLog(JobLogItemViewModel templateLog, string actualFileName)
        {
            // テンプレート自体が既にこの実ファイル名になっている場合
            if (templateLog.DisplayFileName == actualFileName)
            {
                return templateLog;
            }

            // テンプレート以外で同じ実ファイル名のエントリを検索
            return _vm.Logs.FirstOrDefault(x => 
                x.FileName == templateLog.FileName && 
                x.DisplayFileName == actualFileName &&
                x != templateLog);
        }

        /// <summary>
        /// テンプレートがまだ実ファイルで上書きされていないかチェック
        /// </summary>
        private bool IsTemplateNotYetOverwritten(JobLogItemViewModel templateLog)
        {
            // DisplayFileNameとFileNameが同じ場合は、まだテンプレート状態
            return templateLog.DisplayFileName == templateLog.FileName;
        }

        /// <summary>
        /// テンプレートを実ファイル情報で上書き
        /// </summary>
        private void OverwriteTemplateWithActualFile(JobLogItemViewModel templateLog, string filePath, string destPath, int totalSize, int percent)
        {
            var actualFileName = Path.GetFileName(filePath);
            
            // テンプレートの基本情報は保持し、実ファイル固有の情報のみ更新
            templateLog.DisplayFileName = actualFileName;
            templateLog.Size = totalSize.ToString("N0") + " KB";
            templateLog.UpdateDate = File.GetLastWriteTime(filePath).ToString("yyyy/MM/dd HH:mm:ss");
            templateLog.CopyPercent = percent.ToString() + " %";
            templateLog.ObserverStatus = percent >= 100 ? emObserverStatus.SUCCESS : emObserverStatus.OBSERVER;
        }

        /// <summary>
        /// 新しいファイルログエントリを作成
        /// </summary>
        private JobLogItemViewModel CreateNewFileLogEntry(JobLogItemViewModel templateLog, string filePath, string destPath, int totalSize, int percent)
        {
            var actualFileName = Path.GetFileName(filePath);
            
            return new JobLogItemViewModel
            {
                Scenario = templateLog.Scenario,
                Eda = templateLog.Eda,
                Id = templateLog.Id,
                FilePath = templateLog.FilePath, // 元のパスを保持
                FileName = templateLog.FileName, // 元のファイル名を保持
                DisplayFileName = actualFileName, // 実際のファイル名を表示
                FileType = templateLog.FileType,
                FileCount = templateLog.FileCount,
                ObserverType = templateLog.ObserverType,
                Size = totalSize.ToString("N0") + " KB",
                UpdateDate = File.GetLastWriteTime(filePath).ToString("yyyy/MM/dd HH:mm:ss"),
                CopyPercent = percent.ToString() + " %",
                ObserverStatus = percent >= 100 ? emObserverStatus.SUCCESS : emObserverStatus.OBSERVER,
            };
        }

        /// <summary>
        /// ログアイテムUIを更新（シンプル版）
        /// </summary>
        private void UpdateLogItemUI(JobLogItemViewModel logItem, string filePath, string destPath, int totalSize, int percent)
        {
            if (logItem == null) return;

            // 基本的な情報を更新（DisplayFileNameは変更しない）
            logItem.Size = totalSize.ToString("N0") + " KB";
            logItem.CopyPercent = percent.ToString() + " %";
            logItem.UpdateDate = File.GetLastWriteTime(filePath).ToString("yyyy/MM/dd HH:mm:ss");
            logItem.ObserverStatus = percent >= 100 ? emObserverStatus.SUCCESS : emObserverStatus.OBSERVER;
        }

        /// <summary>
        /// 基本ファイル名を抽出（日付プレフィックスを除去）
        /// </summary>
        private string ExtractBaseFileName(string fileName)
        {
            // yyyymmddhhmmss_ファイル名.拡張子 から ファイル名.拡張子 を抽出
            var match = Regex.Match(fileName, @"^\d{14}_?(.+)$");
            return match.Success ? match.Groups[1].Value : fileName;
        }

        /// <summary>
        /// 監視キーを生成
        /// </summary>
        private string GetWatchingKey(LogInfo logInfo, string filePath)
        {
            return $"{logInfo.JobId}_{Path.GetFileName(logInfo.LogFromPath)}_{Path.GetFileName(filePath)}";
        }

        /// <summary>
        /// 廃止されたファイルエントリをクリーンアップ
        /// </summary>
        private void CleanupObsoleteFileEntries(JobLogItemViewModel templateLog)
        {
            try
            {
                var currentWatchers = _fw.GetAllMultiWatchers().Keys.ToHashSet();
                
                // テンプレートと同じFileNameを持つエントリで、現在監視されていないものを削除
                var logsToRemove = _vm.Logs
                    .Where(x => x.FileName == templateLog.FileName && 
                               x.DisplayFileName != x.FileName && // テンプレート状態ではない
                               !currentWatchers.Contains(Path.Combine(x.FilePath, x.DisplayFileName)))
                    .ToList();

                if (logsToRemove.Any())
                {
                    var logList = _vm.Logs.ToList();
                    foreach (var logToRemove in logsToRemove)
                    {
                        logList.Remove(logToRemove);
                    }
                    _vm.Logs = new ObservableCollection<JobLogItemViewModel>(logList);
                    
                    LogFile.WriteLog($"廃止されたファイルエントリ {logsToRemove.Count} 件を削除しました");
                }
            }
            catch (Exception ex)
            {
                ErrLogFile.WriteLog($"CleanupObsoleteFileEntries エラー: {ex.Message}");
            }
        }

        /// <summary>
        /// ファイルの行数を取得
        /// </summary>
        private int GetLineCount(string filePath, JobLogItemViewModel log)
        {
            int lineCount = 0;

            try
            {
                // 一時ファイルにコピーして行数をカウント
                string tempFilePath = Path.GetTempFileName();
                File.Copy(filePath, tempFilePath, true);

                // 行数をカウント
                lineCount = File.ReadLines(tempFilePath).Count();

                // 一時ファイルを削除
                File.Delete(tempFilePath);
            }
            catch (Exception ex)
            {
                ErrLogFile.WriteLog($"GetLineCount エラー: {ex.Message}");
            }

            return lineCount;
        }

        #region 内部クラス

        /// <summary>
        /// ファイル監視情報を管理するクラス
        /// </summary>
        private class FileWatchingInfo
        {
            public LogInfo OriginalLogInfo { get; }
            public string ActualFilePath { get; }
            public DateTime StartTime { get; }

            public FileWatchingInfo(LogInfo originalLogInfo, string actualFilePath)
            {
                OriginalLogInfo = originalLogInfo;
                ActualFilePath = actualFilePath;
                StartTime = DateTime.Now;
            }
        }

        #endregion
    }
}