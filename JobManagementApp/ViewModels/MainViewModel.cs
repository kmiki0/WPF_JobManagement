using System.Data;
using System.Linq;
using System.ComponentModel;
using System.Windows.Input;
using System.Collections.ObjectModel;
using JobManagementApp.Models;
using JobManagementApp.Commands;
using JobManagementApp.Helpers;
using System.Windows.Media.Animation;
using System.Threading.Tasks;
using System;
using System.Windows.Media;

namespace JobManagementApp.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private static readonly object _lock = new object();

        // イベント処理
        private readonly MainCommand _command;
           
        // シングルトン vm
        private static MainViewModel _instance;
        public static MainViewModel Instance
        {
            get
            {
                lock (_lock)
                {
                    if (_instance == null)
                    {
                        // IDataServiceのインスタンスを渡す
                        _instance = new MainViewModel(new MainModel());
                    }
                    return _instance;
                }
            }
        }

        // [検索] シナリオ
        public string _scenario { get; set; }
        public string Scenario
        {
            get { return _scenario; }
            set
            {
                _scenario = value;
                OnPropertyChanged(nameof(Scenario));
            }
        }
        // [検索] ジョブID
        public string _jobId { get; set; }
        public string JobId
        {
            get { return _jobId; }
            set
            {
                _jobId = value;
                OnPropertyChanged(nameof(JobId));
            }
        }
        // [検索] 受信元 (取得した一意のリスト)
        private Array _cmdRecv { get; set; }
        public Array cmdRecv
        {
            get { return _cmdRecv; }
            set
            {
                _cmdRecv = value;
                OnPropertyChanged(nameof(cmdRecv));
            }
        }
        private string _selectedRecv;
        public string SelectedRecv
        {
            get { return _selectedRecv; }
            set
            {
                _selectedRecv = value;
                OnPropertyChanged(nameof(SelectedRecv));
            }
        }
        // [検索] 送信元 (取得した一意のリスト)
        private Array _cmdSend { get; set; }
        public Array cmdSend
        {
            get { return _cmdSend; }
            set
            {
                _cmdSend = value;
                OnPropertyChanged(nameof(cmdSend));
            }
        }
        private string _selectedSend;
        public string SelectedSend
        {
            get { return _selectedSend; }
            set
            {
                _selectedSend = value;
                OnPropertyChanged(nameof(SelectedSend));
            }
        }

        // TreeViewの開閉トグル
        private bool _isExpanded { get; set; }
        public bool IsExpanded {
            get => _isExpanded;
            set
            {
                _isExpanded = value;
                OnPropertyChanged(nameof(IsExpanded));
                _command.TreeViewCheckBox_Toggle();
            }
        }
        // ユーザーID
        private string _userId { get; set; }
        public string UserId {
            get => _userId;
            set
            {
                _userId = value;
                OnPropertyChanged(nameof(UserId));
            }
        }
        
        // 運用処理管理Rを検索する日付の範囲指定 FROM
        private string _searchFromDate { get; set; }
        public string SearchFromDate {
            get => _searchFromDate;
            set
            {
                _searchFromDate = value;
                OnPropertyChanged(nameof(SearchFromDate));
                _command?.CheckSearchDateTime(value, true);
                // LogWindowに変更を通知
                NotifyLogWindowsOfDateChange();
            }
        }

        // 運用処理管理Rを検索する日付の範囲指定 TO
        private string _searchToDate { get; set; }
        public string SearchToDate {
            get => _searchToDate;
            set
            {
                _searchToDate = value;
                OnPropertyChanged(nameof(SearchToDate));
                _command?.CheckSearchDateTime(value, false);

                // LogWindowに変更を通知
                NotifyLogWindowsOfDateChange();
            }
        }

        // 運用処理管理R　取得時刻
        private string _displayUpdateDate { get; set; }
        public string DisplayUpdateDate {
            get => _displayUpdateDate;
            set
            {
                _displayUpdateDate = value;
                OnPropertyChanged(nameof(DisplayUpdateDate));
            }
        }
        // ジョブリスト
        private ObservableCollection<JobListItemViewModel> _jobs { get; set; }
        public ObservableCollection<JobListItemViewModel> Jobs {
            get => _jobs;
            set
            {
                _jobs = value;
                OnPropertyChanged(nameof(Jobs));
            }
        }


        // 検索欄表示 ボタン
        public ICommand AreaVisibilityCommand { get; set; }
        // クリア ボタン
        public ICommand ClearCommand { get; set; }
        // 検索 ボタン
        public ICommand SearchCommand { get; set; }
        // ユーザー保存 ボタン
        public ICommand CacheUserCommand { get; set; }
        // 画面更新ボタン
        public ICommand RefreshCommand { get; set; }
        // ジョブ追加　ボタン
        public ICommand NewJobCommand { get; set; }

        // ボタン処理可能
        private bool _isButtonEnabled;
        public bool IsButtonEnabled
        {
            get { return _isButtonEnabled; }
            set
            {
                _isButtonEnabled = value;
                OnPropertyChanged(nameof(IsButtonEnabled));
            }
        }

        /// <summary>
        /// Init
        /// </summary>
        public MainViewModel(IMainModel IF)
        {
            // 初期値セット
            this.IsButtonEnabled = true;
            
            // ボタンイベント 初期化
            _command = new MainCommand(this, IF);
            AreaVisibilityCommand = new RelayCommand(_command.SearchAreaVisibility_Toggle);
            ClearCommand = new RelayCommand(_command.ClearButton_Click);
            SearchCommand = new RelayCommand(_command.SearchButton_Click);
            CacheUserCommand = new RelayCommand(_command.SaveUserButton_Click);
            RefreshCommand = new RelayCommand(_command.RefreshButton_Click);
            NewJobCommand = new RelayCommand(_command.NewJobButton_Click);
        }

        /// <summary>
        /// 引数のリストとKey項目が同じものを更新
        /// </summary>
        public void UpdateJobs(JobListItemViewModel updateModel)
        {
            var exisItem = this.Jobs.Where(x => x.Id == updateModel.Scenario).FirstOrDefault().Children.Where(x => x.Scenario == updateModel.Scenario && x.Eda == updateModel.Eda).FirstOrDefault();
            if (exisItem != null)
            {
                exisItem.Scenario = updateModel.Scenario;
                exisItem.Eda = updateModel.Eda;
                exisItem.Id = updateModel.Id;
                exisItem.Name = updateModel.Name;
                exisItem.Execution = updateModel.Execution;
                exisItem.JobBoolean = updateModel.JobBoolean;
                exisItem.Status = updateModel.Status;
            }
        }

        /// <summary>
        /// LogWindowに変更を通知
        /// </summary>
        private void NotifyLogWindowsOfDateChange()
        {
            try
            {
                // 開いているJobLogWindowを検索して更新
                foreach (Window window in Application.Current.Windows)
                {
                    if (window is JobLogWindow logWindow && 
                        logWindow.DataContext is JobLogViewModel logVM)
                    {
                        // 両方の日付を更新
                        logVM.UpdateSearchDateDisplay();
                    }
                }
            }
            catch (Exception ex)
            {
                ErrLogFile.WriteLog($"NotifyLogWindowsOfDateChange エラー: {ex.Message}");
            }
        }

        public double _borderHeight { get; set; }
        public double BorderHeight
        {
            get { return _borderHeight; }
            set
            {
                _borderHeight = value;
                OnPropertyChanged(nameof(BorderHeight));
            }
        }

        // VM変更検知
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
