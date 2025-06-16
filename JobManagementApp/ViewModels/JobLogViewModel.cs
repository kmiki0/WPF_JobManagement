using JobManagementApp.Commands;
using JobManagementApp.Helpers;
using JobManagementApp.Manager;
using JobManagementApp.Models;
using JobManagementApp.Views;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
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

        // 一時保存フォルダ　更新ボタン
        public ICommand TempFolderUpdateCommand { get; set; }
        // フォルダボタン
        public ICommand FolderCommand { get; set; }
        // 閉じるボタン
        public ICommand CloseCommand { get; set; }
        // ジョブ追加ボタン
        public ICommand AddLogCommand { get; set; }


        // シナリオと枝番に値がある場合、データ取得して画面に表示
        public JobLogViewModel(IJobLogModel IF, string scenario, string eda)
        {
            // 初期値 セット
            this.Scenario = scenario;
            this.Eda = eda;

            // MainWindowから値 セット
            UpdateSearchDateDisplay();

            // コマンド 初期化
            _command = new JobLogCommand(this, IF);
            TempFolderUpdateCommand = new RelayCommand(_command.TempFolderButton_Click);
            AddLogCommand = new RelayCommand(_command.AddLogButton_Click);
            FolderCommand = new RelayCommand(_command.FolderButton_Click);
            CloseCommand = new RelayCommand(_command.CloseButton_Click);
        }

        /// <summary> 
        /// ViewModel　再作成
        /// </summary> 
        public static void RecreateViewModel(JobParamModel job)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                var oldWindow = WindowHelper.GetJobLogWindow();

                // 新しく生成
                var newVm = new JobLogViewModel(new JobLogModel(), job.Scenario, job.Eda);
                var newWindow = new JobLogWindow
                {
                    Left = oldWindow.Left,
                    Top = oldWindow.Top,
                    DataContext = newVm,
                };
                newVm.window = newWindow; 
                newWindow.Show();

                oldWindow?.Close();
            });
        }

        // MainWindowから検索範囲を取得して表示用プロパティに設定
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

        // vm変更するとき
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
