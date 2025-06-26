using JobManagementApp.Commands;
using JobManagementApp.Helpers;
using JobManagementApp.Manager;
using JobManagementApp.Models;
using JobManagementApp.Views;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;

namespace JobManagementApp.ViewModels
{
    public class JobLogViewModel : INotifyPropertyChanged
    {
        public Window window { get; set; }

        // イベント処理
        public JobLogCommand _command;

        // シナリオ
        public string Scenario { get; set; }
        // 枝番
        public string Eda { get; set; }
        // ジョブID
        public string Id { get; set; }
        
        // コピー先フォルダパス
        private string _toCopyFolderPath { get; set; }
        public string ToCopyFolderPath {
            get => _toCopyFolderPath;
            set
            {
                _toCopyFolderPath = value;
                OnPropertyChanged(nameof(ToCopyFolderPath));
            }
        }
        
        // 一時保存フォルダパス
        private string _tempSavePath { get; set; }
        public string TempSavePath {
            get => _tempSavePath;
            set
            {
                _tempSavePath = value;
                OnPropertyChanged(nameof(TempSavePath));
            }
        }
        
        // MainWindowから取得する検索範囲（表示用）
        private string _displaySearchFromDate;
        public string DisplaySearchFromDate 
        {
            get => _displaySearchFromDate;
            set
            {
                _displaySearchFromDate = value;
                OnPropertyChanged(nameof(DisplaySearchFromDate));
            }
        }
        
        private string _displaySearchToDate;
        public string DisplaySearchToDate 
        {
            get => _displaySearchToDate;
            set
            {
                _displaySearchToDate = value;
                OnPropertyChanged(nameof(DisplaySearchToDate));
            }
        }
        
        // ログ一覧
        private ObservableCollection<JobLogItemViewModel> _logs { get; set; }
        public ObservableCollection<JobLogItemViewModel> Logs {
            get => _logs;
            set
            {
                _logs = value;
                OnPropertyChanged(nameof(Logs));
            }
        }

        // ダウンロード結果サマリー
        private string _downloadSummary { get; set; }
        public string DownloadSummary {
            get => _downloadSummary;
            set
            {
                _downloadSummary = value;
                OnPropertyChanged(nameof(DownloadSummary));
            }
        }

        // ダウンロード進行状況表示
        private bool _isDownloading { get; set; }
        public bool IsDownloading {
            get => _isDownloading;
            set
            {
                _isDownloading = value;
                OnPropertyChanged(nameof(IsDownloading));
            }
        }

        // ダウンロード進行率
        private int _downloadProgress { get; set; }
        public int DownloadProgress {
            get => _downloadProgress;
            set
            {
                _downloadProgress = value;
                OnPropertyChanged(nameof(DownloadProgress));
            }
        }

        // 一時保存フォルダ　更新ボタン
        public ICommand TempFolderUpdateCommand { get; set; }
        // フォルダボタン
        public ICommand FolderCommand { get; set; }
        // 閉じるボタン
        public ICommand CloseCommand { get; set; }
        // ジョブ追加ボタン
        public ICommand AddLogCommand { get; set; }

        /// <summary>
        /// コンストラクタ
        /// </summary>
        public JobLogViewModel(IJobLogModel IF, string scenario, string eda)
        {
            // 初期値 セット
            this.Scenario = scenario;
            this.Eda = eda;
            this.IsDownloading = true;
            this.DownloadProgress = 0;
            this.DownloadSummary = "📥 ダウンロードを開始しています...";

            // MainWindowから値 セット
            UpdateSearchDateDisplay();

            // コマンド 初期化
            _command = new JobLogCommand(this, IF);
            TempFolderUpdateCommand = new RelayCommand(_command.TempFolderButton_Click);
            AddLogCommand = new RelayCommand(_command.AddLogButton_Click);
            FolderCommand = new RelayCommand(_command.FolderButton_Click);
            CloseCommand = new RelayCommand(_command.CloseButton_Click);

            // 進行状況イベントの購読
            _command.ProgressChanged += OnDownloadProgressChanged;
        }

        /// <summary>
        /// ダウンロード進行状況の更新
        /// </summary>
        private void OnDownloadProgressChanged(string fileName, string destPath, int totalSize, int progress)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (progress >= 100)
                {
                    // 個別ファイル完了時の処理
                    var completedFile = Logs?.FirstOrDefault(x => x.FileName == System.IO.Path.GetFileName(fileName));
                    if (completedFile != null)
                    {
                        completedFile.ObserverStatus = emObserverStatus.SUCCESS;
                        completedFile.CopyPercent = "100 %";
                        completedFile.Size = totalSize.ToString("N0") + " KB";
                    }
                }

                // 全体の進行率を計算
                UpdateOverallProgress();
            });
        }

        /// <summary>
        /// 全体のダウンロード進行率を更新
        /// </summary>
        private void UpdateOverallProgress()
        {
            if (Logs == null || Logs.Count == 0) return;

            var totalFiles = Logs.Count;
            var completedFiles = Logs.Count(x => x.ObserverStatus == emObserverStatus.SUCCESS);
            var errorFiles = Logs.Count(x => x.ObserverStatus == emObserverStatus.ERROR);
            var processedFiles = completedFiles + errorFiles;

            DownloadProgress = totalFiles > 0 ? (processedFiles * 100) / totalFiles : 0;

            // 全て完了したらダウンロード状態を終了
            if (processedFiles >= totalFiles)
            {
                IsDownloading = false;
            }
        }

        /// <summary>
        /// ダウンロード完了後にサマリーを更新するメソッド
        /// </summary>
        public void UpdateDownloadSummary()
        {
            try
            {
                if (Logs == null || Logs.Count == 0)
                {
                    DownloadSummary = "📭 ダウンロード対象のファイルがありません";
                    IsDownloading = false;
                    return;
                }

                var totalCount = Logs.Count;
                var successCount = Logs.Count(x => x.ObserverStatus == emObserverStatus.SUCCESS);
                var errorCount = Logs.Count(x => x.ObserverStatus == emObserverStatus.ERROR);
                var pendingCount = Logs.Count(x => x.ObserverStatus == emObserverStatus.OBSERVER);

                if (pendingCount > 0)
                {
                    DownloadSummary = $"📥 ダウンロード中... ({successCount + errorCount}/{totalCount})";
                    IsDownloading = true;
                }
                else if (errorCount == 0)
                {
                    DownloadSummary = $"✅ 全{totalCount}ファイルのダウンロードが完了しました";
                    IsDownloading = false;
                }
                else if (successCount > 0)
                {
                    DownloadSummary = $"⚠️ {successCount}成功, {errorCount}エラー (全{totalCount}ファイル)";
                    IsDownloading = false;
                }
                else
                {
                    DownloadSummary = $"❌ 全{totalCount}ファイルのダウンロードに失敗しました";
                    IsDownloading = false;
                }

                // 進行率も更新
                UpdateOverallProgress();

                LogFile.WriteLog($"ダウンロードサマリー更新: 成功{successCount}, エラー{errorCount}, 全{totalCount}");
            }
            catch (Exception ex)
            {
                ErrLogFile.WriteLog($"UpdateDownloadSummary エラー: {ex.Message}");
                DownloadSummary = "❌ サマリー更新エラー";
                IsDownloading = false;
            }
        }

        /// <summary>
        /// ダウンロード統計情報を取得
        /// </summary>
        public DownloadStatistics GetDownloadStatistics()
        {
            if (Logs == null)
            {
                return new DownloadStatistics();
            }

            return new DownloadStatistics
            {
                TotalFiles = Logs.Count,
                SuccessFiles = Logs.Count(x => x.ObserverStatus == emObserverStatus.SUCCESS),
                ErrorFiles = Logs.Count(x => x.ObserverStatus == emObserverStatus.ERROR),
                PendingFiles = Logs.Count(x => x.ObserverStatus == emObserverStatus.OBSERVER),
                TotalSize = CalculateTotalSize(),
                ErrorMessages = Logs.Where(x => x.ObserverStatus == emObserverStatus.ERROR)
                                   .Select(x => $"{x.FileName}: {x.ErrorMessage}")
                                   .ToArray()
            };
        }

        /// <summary>
        /// 総ファイルサイズを計算
        /// </summary>
        private long CalculateTotalSize()
        {
            long totalSize = 0;
            foreach (var log in Logs.Where(x => x.ObserverStatus == emObserverStatus.SUCCESS))
            {
                if (!string.IsNullOrEmpty(log.Size) && log.Size.Contains("KB"))
                {
                    var sizeStr = log.Size.Replace("KB", "").Replace(",", "").Trim();
                    if (long.TryParse(sizeStr, out long size))
                    {
                        totalSize += size;
                    }
                }
            }
            return totalSize;
        }

        /// <summary> 
        /// ViewModel　再作成
        /// </summary> 
        public static void RecreateViewModel(JobParamModel job)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                try
                {
                    var oldWindow = WindowHelper.GetJobLogWindow();

                    // 新しく生成
                    var newVm = new JobLogViewModel(new JobLogModel(), job.Scenario, job.Eda);
                    var newWindow = new JobLogWindow
                    {
                        Left = oldWindow?.Left ?? 100,
                        Top = oldWindow?.Top ?? 100,
                        DataContext = newVm,
                    };
                    newVm.window = newWindow; 
                    newWindow.Show();

                    oldWindow?.Close();
                }
                catch (Exception ex)
                {
                    ErrLogFile.WriteLog($"RecreateViewModel エラー: {ex.Message}");
                    MessageBox.Show("ログウィンドウの再作成に失敗しました。", "エラー", 
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            });
        }

        /// <summary>
        /// MainWindowから検索範囲を取得して表示用プロパティに設定
        /// </summary>
        public void UpdateSearchDateDisplay()
        {
            try
            {
                this.DisplaySearchFromDate = MainViewModel.Instance.SearchFromDate ?? "未設定";
                this.DisplaySearchToDate = MainViewModel.Instance.SearchToDate ?? "未設定";
            }
            catch (Exception ex)
            {
                ErrLogFile.WriteLog($"UpdateSearchDateDisplay エラー: {ex.Message}");
                this.DisplaySearchFromDate = "取得エラー";
                this.DisplaySearchToDate = "取得エラー";
            }
        }

        /// <summary>
        /// リソースのクリーンアップ
        /// </summary>
        public void Dispose()
        {
            try
            {
                // イベント購読の解除
                if (_command != null)
                {
                    _command.ProgressChanged -= OnDownloadProgressChanged;
                }

                // ウィンドウ参照をクリア
                window = null;
            }
            catch (Exception ex)
            {
                ErrLogFile.WriteLog($"JobLogViewModel.Dispose エラー: {ex.Message}");
            }
        }

        // vm変更するとき
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #region 内部クラス

        /// <summary>
        /// ダウンロード統計情報
        /// </summary>
        public class DownloadStatistics
        {
            public int TotalFiles { get; set; }
            public int SuccessFiles { get; set; }
            public int ErrorFiles { get; set; }
            public int PendingFiles { get; set; }
            public long TotalSize { get; set; }
            public string[] ErrorMessages { get; set; }

            public DownloadStatistics()
            {
                ErrorMessages = new string[0];
            }

            public bool HasErrors => ErrorFiles > 0;
            public bool IsCompleted => PendingFiles == 0;
            public double SuccessRate => TotalFiles > 0 ? (double)SuccessFiles / TotalFiles * 100 : 0;
        }

        #endregion
    }
}