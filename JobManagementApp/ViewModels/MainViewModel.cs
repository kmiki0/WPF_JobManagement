using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.ObjectModel;
using JobManagementApp.Models;
using JobManagementApp.Services;
using System.Data;
using System.ComponentModel;
using System.Windows.Data;
using JobManagementApp.Helpers;
using System.Windows.Threading;
using System.Windows.Input;
using JobManagementApp.Manager;
using System.Windows;
using JobManagementApp.Commands;

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

        // ユーザー保存 ボタン
        public ICommand CacheUserCommand { get; set; }
        // 画面更新ボタン
        public ICommand RefreshCommand { get; set; }
        // ジョブ追加　ボタン
        public ICommand NewJobCommand { get; set; }

        /// <summary>
        /// Init
        /// </summary>
        public MainViewModel(IMainModel IF)
        {
            // ボタンイベント 初期化
            _command = new MainCommand(this, IF);

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

        // vm変更するとき
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
