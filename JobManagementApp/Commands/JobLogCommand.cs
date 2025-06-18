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

           // MainWindowから検索範囲を取得
           _vm.UpdateSearchDateDisplay();

            // ログ一覧 読み込み
            LoadLogList();
        }

        // ログ一覧 読み込み - ToDate対応版
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
        // 　ログ監視 - ToDate対応版
        // ==================================
        public async Task StartMonitoring()
        {
            try
            {
                // MainWindowのFromDateとToDateを取得
                DateTime fromDate;
                DateTime toDate;
                
                try
                {
                    fromDate = DateTime.Parse(MainViewModel.Instance.SearchFromDate);
                    toDate = DateTime.Parse(MainViewModel.Instance.SearchToDate);
                }
                catch (Exception ex)
                {
                    // 日付解析に失敗した場合のフォールバック
                    LogFile.WriteLog($"StartMonitoring: 日付解析エラー - {ex.Message}");
                    fromDate = DateTime.Parse(MainViewModel.Instance.SearchFromDate);
                    toDate = DateTime.Now.AddHours(1); // 1時間後をデフォルト
                }
                
                // MultiFileWatcherにToDateも渡す
                var _multiFileWatcher = new MultiFileWatcher(
                    _vm.Logs.ToList(), 
                    _vm.TempSavePath, 
                    fromDate,
                    toDate
                );

                // イベント ファイルコピー時
                _multiFileWatcher.ProgressChanged += OnFileProgressChanged;
                
                // 監視開始
                await _multiFileWatcher.StartMonitoring();
            }
            catch (Exception ex)
            {
                ErrLogFile.WriteLog($"StartMonitoring エラー: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// ファイル進行状況変更イベント
        /// </summary>
        private void OnFileProgressChanged(string filePath, string destPath, int totalSize, int percent)
        {
            try
            {
                LogInfo logInfo = null;
                
                // 1. 完全一致で検索
                logInfo = _fw.GetAllLogInfos().Values.FirstOrDefault(x => 
                    string.Equals(Path.GetFullPath(x.LogFromPath), Path.GetFullPath(filePath), StringComparison.OrdinalIgnoreCase));
                
                // 2. 完全一致で見つからない場合、ファイル名ベースで検索
                if (logInfo == null)
                {
                    var targetFileName = Path.GetFileName(filePath);
                    logInfo = _fw.GetAllLogInfos().Values.FirstOrDefault(x => 
                        string.Equals(Path.GetFileName(x.LogFromPath), targetFileName, StringComparison.OrdinalIgnoreCase));
                }
                
                // 3. マルチファイルの場合、ベースファイル名で検索
                if (logInfo == null)
                {
                    var baseFileName = ExtractBaseFileName(Path.GetFileName(filePath));
                    logInfo = _fw.GetAllLogInfos().Values.FirstOrDefault(x => 
                    {
                        var registeredBaseFileName = ExtractBaseFileName(Path.GetFileName(x.LogFromPath));
                        return string.Equals(registeredBaseFileName, baseFileName, StringComparison.OrdinalIgnoreCase);
                    });
                }
                
                // 4. それでも見つからない場合、JobIDベースで検索
                if (logInfo == null)
                {
                    var directory = Path.GetDirectoryName(filePath);
                    logInfo = _fw.GetAllLogInfos().Values.FirstOrDefault(x => 
                        string.Equals(Path.GetDirectoryName(x.LogFromPath), directory, StringComparison.OrdinalIgnoreCase));
                }
                
                if (logInfo == null) { return; }

                var watchingKey = GetWatchingKey(logInfo, filePath);
                var watchingInfo = _watchingFiles.GetOrAdd(watchingKey, _ => new FileWatchingInfo(logInfo, filePath));

                if (logInfo.IsMultiFile)
                {
                    // マルチファイル処理
                    HandleMultiFileProgress(watchingInfo, filePath, destPath, totalSize, percent);
                }
                else
                {
                    // シングルファイル処理
                    HandleSingleFileProgress(watchingInfo, filePath, destPath, totalSize, percent);
                }
            }
            catch (Exception ex)
            {
                ErrLogFile.WriteLog($"OnFileProgressChanged エラー: {ex.Message}");
                ErrLogFile.WriteLog($"対象ファイル: {filePath}");
            }
        }

        /// <summary>
        /// 基本ファイル名を抽出（日付プレフィックスを除去）- 強化版
        /// </summary>
        private string ExtractBaseFileName(string fileName)
        {
            try
            {
                // ファイル名が空の場合
                if (string.IsNullOrEmpty(fileName))
                {
                    return fileName;
                }

                // パターン1: yyyymmddhhmmss_ファイル名.拡張子
                var match1 = System.Text.RegularExpressions.Regex.Match(fileName, @"^\d{14}_(.+)$");
                if (match1.Success)
                {
                    var result = match1.Groups[1].Value;
                    return result;
                }

                // パターン2: yyyymmddhhmmssファイル名.拡張子（アンダーバーなし）
                var match2 = System.Text.RegularExpressions.Regex.Match(fileName, @"^\d{14}(.+)$");
                if (match2.Success)
                {
                    var result = match2.Groups[1].Value;
                    return result;
                }

                // パターン3: yyyymmdd_ファイル名.拡張子
                var match3 = System.Text.RegularExpressions.Regex.Match(fileName, @"^\d{8}_(.+)$");
                if (match3.Success)
                {
                    var result = match3.Groups[1].Value;
                    return result;
                }

                return fileName;
            }
            catch (Exception ex)
            {
                ErrLogFile.WriteLog($"ExtractBaseFileName エラー: {ex.Message}");
                return fileName; // エラー時は元のファイル名を返す
            }
        }

        private void OnFileProgressChanged(string filePath, string destPath, int totalSize, int percent)
        {
            try
            {
                var logInfo = _fw.GetAllLogInfos().Values.FirstOrDefault(x => x.LogFromPath == filePath);
                if (logInfo == null) 
                {
                    LogFile.WriteLog($"OnFileProgressChanged: LogInfoが見つかりません - {filePath}");
                    return;
                }

                var watchingKey = GetWatchingKey(logInfo, filePath);
                var watchingInfo = _watchingFiles.GetOrAdd(watchingKey, _ => new FileWatchingInfo(logInfo, filePath));

                if (logInfo.IsMultiFile)
                {
                    // マルチファイル処理
                    HandleMultiFileProgress(watchingInfo, filePath, destPath, totalSize, percent);
                }
                else
                {
                    // シングルファイル処理
                    HandleSingleFileProgress(watchingInfo, filePath, destPath, totalSize, percent);
                }
            }
            catch (Exception ex)
            {
                ErrLogFile.WriteLog($"OnFileProgressChanged エラー: {ex.Message}");
            }
        }

        /// <summary>
        /// UIスレッドセーフな更新を実行
        /// </summary>
        private void InvokeOnUIThread(Action action)
        {
            if (Application.Current.Dispatcher.CheckAccess())
            {
                // 既にUIスレッドの場合は直接実行
                action();
            }
            else
            {
                // UIスレッド以外からの場合はDispatcher経由
                Application.Current.Dispatcher.Invoke(action);
            }
        }

        /// <summary>
        /// シングルファイルの進行状況処理
        /// </summary>
        private void HandleSingleFileProgress(FileWatchingInfo watchingInfo, string filePath, string destPath, int totalSize, int percent)
        {
            try
            {
                var templateLog = FindTemplateLog(watchingInfo.OriginalLogInfo);
                if (templateLog != null)
                {
                    // UI更新
                    InvokeOnUIThread(() =>
                    {
                        // コピー先フォルダパスを設定
                        _vm.ToCopyFolderPath = Path.GetDirectoryName(destPath);
                        
                        UpdateLogItemUIDirectly(templateLog, filePath, destPath, totalSize, percent);
                    });
                }
            }
            catch (Exception ex)
            {
                ErrLogFile.WriteLog($"HandleSingleFileProgress エラー: {ex.Message}");
            }
        }

        /// <summary>
        /// マルチファイルの進行状況処理 - UIスレッド修正版
        /// </summary>
        private void HandleMultiFileProgress(FileWatchingInfo watchingInfo, string filePath, string destPath, int totalSize, int percent)
        {
            try
            {
                InvokeOnUIThread(() =>
                {
                    // コピー先フォルダパスを設定
                    _vm.ToCopyFolderPath = Path.GetDirectoryName(destPath);
                    
                    var templateLog = FindTemplateLog(watchingInfo.OriginalLogInfo);
                    if (templateLog == null) 
                    {
                        return;
                    }

                    // 実際のファイル名から基本ファイル名を抽出
                    var actualFileName = Path.GetFileName(filePath);
                    var baseFileName = ExtractBaseFileName(actualFileName);

                    // このファイルが元のテンプレートファイル名と一致するかチェック
                    if (!baseFileName.Equals(templateLog.FileName, StringComparison.OrdinalIgnoreCase))
                    {
                        return;
                    }

                    // この実ファイルが既に処理されているかチェック
                    var existingFileLog = FindExistingFileLog(templateLog, actualFileName);

                    if (existingFileLog != null)
                    {
                        // 既存エントリを更新
                        UpdateLogItemUIDirectly(existingFileLog, filePath, destPath, totalSize, percent);
                        
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
                            OverwriteTemplateWithActualFileDirectly(templateLog, filePath, destPath, totalSize, percent);
                            
                            if (percent >= 100)
                            {
                                templateLog.LineCount = GetLineCount(destPath, templateLog).ToString() + " 件";
                            }
                        }
                        else
                        {
                            // 2回目以降：新しいエントリを追加（昇順で挿入）
                            var newFileLog = CreateNewFileLogEntry(templateLog, filePath, destPath, totalSize, percent);
                            
                            if (newFileLog.ObserverStatus == emObserverStatus.SUCCESS)
                            {
                                newFileLog.LineCount = GetLineCount(destPath, newFileLog).ToString() + " 件";
                            }

                            // 昇順になるよう挿入位置を計算
                            var insertIndex = FindInsertPositionForAscendingOrder(templateLog, actualFileName);
                            
                            // 計算された位置に挿入
                            if (insertIndex >= 0 && insertIndex <= _vm.Logs.Count)
                            {
                                _vm.Logs.Insert(insertIndex, newFileLog);
                            }
                            else
                            {
                                _vm.Logs.Add(newFileLog);
                            }
                        }
                    }

                    // 不要になったエントリの削除
                    CleanupObsoleteFileEntries(templateLog);
                });
            }
            catch (Exception ex)
            {
                ErrLogFile.WriteLog($"HandleMultiFileProgress エラー: {ex.Message}");
            }
        }

        /// <summary>
        /// ファイル名昇順になるよう挿入位置を計算
        /// </summary>
        private int FindInsertPositionForAscendingOrder(JobLogItemViewModel templateLog, string newFileName)
        {
            try
            {
                // 同じ基本ファイル名（テンプレート）に関連するエントリをすべて取得
                var relatedEntries = _vm.Logs
                    .Select((log, index) => new { Log = log, Index = index })
                    .Where(x => x.Log.FileName == templateLog.FileName)
                    .OrderBy(x => x.Index)
                    .ToList();

                // 関連エントリがない場合
                if (!relatedEntries.Any())
                {
                    return _vm.Logs.Count;
                }

                // 挿入位置を探す
                for (int i = 0; i < relatedEntries.Count; i++)
                {
                    var currentEntry = relatedEntries[i];
                    var currentDisplayFileName = currentEntry.Log.DisplayFileName;
                    
                    // 文字列比較で新しいファイル名が現在のエントリより小さい場合
                    if (string.Compare(newFileName, currentDisplayFileName, StringComparison.OrdinalIgnoreCase) < 0)
                    {
                        return currentEntry.Index;
                    }
                }

                // すべてのエントリより大きい場合は、最後の関連エントリの次に挿入
                var lastRelatedEntry = relatedEntries.Last();
                var insertIndex = lastRelatedEntry.Index + 1;
                
                return insertIndex;
            }
            catch (Exception ex)
            {
                // エラー時は安全に最後に追加
                return _vm.Logs.Count;
            }
        }


        /// <summary>
        /// プロパティを直接更新
        /// </summary>
        private void UpdateLogItemUIDirectly(JobLogItemViewModel logItem, string filePath, string destPath, int totalSize, int percent)
        {
            logItem.Size = totalSize.ToString("N0") + " KB";
            logItem.CopyPercent = percent.ToString() + " %";
            logItem.UpdateDate = File.GetLastWriteTime(filePath).ToString("yyyy/MM/dd HH:mm:ss");
            logItem.ObserverStatus = percent >= 100 ? emObserverStatus.SUCCESS : emObserverStatus.OBSERVER;
        }

        /// <summary>
        /// テンプレートログを実ファイル情報で直接上書き 
        /// </summary>
        private void OverwriteTemplateWithActualFileDirectly(JobLogItemViewModel templateLog, string filePath, string destPath, int totalSize, int percent)
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
        /// テンプレートログを検索 - 修正版（基本ファイル名で検索）
        /// </summary>
        private JobLogItemViewModel FindTemplateLog(LogInfo logInfo)
        {
            try
            {
                // 実際のファイルパスから基本ファイル名を抽出
                var actualFileName = Path.GetFileName(logInfo.LogFromPath); // "20250616120000_sample.tsv"
                var baseFileName = ExtractBaseFileName(actualFileName);     // "sample.tsv"
                var candidates = _vm.Logs.Where(x => x.FileName == baseFileName).ToList();
                
                // FileNameが基本ファイル名と一致するものを「元テンプレート」として扱う
                var result = _vm.Logs.FirstOrDefault(x => 
                    x.FileName == baseFileName);       
                
                return result;
            }
            catch (Exception ex)
            {
                ErrLogFile.WriteLog($"FindTemplateLog エラー: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 元テンプレートを検索（上書き状態に関係なく）
        /// </summary>
        private JobLogItemViewModel FindOriginalTemplate(string baseFileName)
        {
            try
            {
                // FileNameが基本ファイル名と一致する最初のエントリを「元テンプレート」
                // 複数ある場合は最初の1件
                var result = _vm.Logs.FirstOrDefault(x => 
                    x.FileName == baseFileName &&
                    x.FileType != emFileType.LOG);
                
                return result;
            }
            catch (Exception ex)
            {
                ErrLogFile.WriteLog($"FindOriginalTemplate エラー: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 既存のファイルログエントリを検索（テンプレートが実ファイル化されている場合も含む）- 修正版
        /// </summary>
        private JobLogItemViewModel FindExistingFileLog(JobLogItemViewModel templateLog, string actualFileName)
        {
            try
            {
                // パターン1: テンプレート自体が既にこの実ファイル名になっている場合
                if (templateLog.DisplayFileName == actualFileName)
                {
                    return templateLog;
                }

                // パターン2: 同じ基本ファイル名で、同じ実ファイル名の別エントリを検索（2回目以降に追加されたエントリ）
                var result = _vm.Logs.FirstOrDefault(x => 
                    x.FileName == templateLog.FileName &&
                    x.DisplayFileName == actualFileName &&
                    x != templateLog);

                return result;
            }
            catch (Exception ex)
            {
                ErrLogFile.WriteLog($"FindExistingFileLog エラー: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// テンプレートがまだ実ファイルで上書きされていないかチェック - 詳細ログ版
        /// </summary>
        private bool IsTemplateNotYetOverwritten(JobLogItemViewModel templateLog)
        {
            try
            {
                // DisplayFileNameとFileNameが同じ場合は、まだテンプレート状態
                bool isTemplate = templateLog.DisplayFileName == templateLog.FileName;
                
                return isTemplate;
            }
            catch (Exception ex)
            {
                ErrLogFile.WriteLog($"IsTemplateNotYetOverwritten エラー: {ex.Message}");
                return true; // エラー時は安全側でテンプレート状態とみなす
            }
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
        /// 基本ファイル名を抽出（日付プレフィックスを除去）- 強化版
        /// </summary>
        private string ExtractBaseFileName(string fileName)
        {
            try
            {
                // ファイル名が空の場合
                if (string.IsNullOrEmpty(fileName))
                {
                    return fileName;
                }

                // パターン1: yyyymmddhhmmss_ファイル名.拡張子
                var match1 = Regex.Match(fileName, @"^\d{14}_(.+)$");
                if (match1.Success)
                {
                    var result = match1.Groups[1].Value;
                    return result;
                }

                // パターン2: yyyymmddhhmmssファイル名.拡張子（アンダーバーなし）
                var match2 = Regex.Match(fileName, @"^\d{14}(.+)$");
                if (match2.Success)
                {
                    var result = match2.Groups[1].Value;
                    return result;
                }

                // どのパターンにも一致しない場合は元のファイル名をそのまま返す
                return fileName;
            }
            catch (Exception ex)
            {
                ErrLogFile.WriteLog($"ExtractBaseFileName エラー: {ex.Message}");
                return fileName; // エラー時は元のファイル名を返す
            }
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

                // ファイルエントリ削除
                if (logsToRemove.Any())
                {
                    var logList = _vm.Logs.ToList();
                    foreach (var logToRemove in logsToRemove)
                    {
                        logList.Remove(logToRemove);
                    }
                    _vm.Logs = new ObservableCollection<JobLogItemViewModel>(logList);
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