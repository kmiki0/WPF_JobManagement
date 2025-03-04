using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.ObjectModel;
using System.Windows.Input;
using JobManagementApp.Commands;
using JobManagementApp.Views;
using JobManagementApp.Services;
using System.Windows;
using JobManagementApp.Models;
using System.ComponentModel;

namespace JobManagementApp.ViewModels
{
    public class JobListItemViewModel : INotifyPropertyChanged
    {
        // イベント処理
        private readonly JobListItemCommand _command;

        // シナリオ
        public string Scenario { get; set; }
        // 枝番
        public string Eda { get; set; }
        // ジョブID
        private string _id { get; set; }
        public string Id {
            get => _id;
            set
            {
                _id = value;
                OnPropertyChanged(nameof(Id));
            }
        }
        // ジョブ名
        private string _name { get; set; }
        public string Name {
            get => _name;
            set
            {
                _name = value;
                OnPropertyChanged(nameof(Name));
            }
        }
        // ジョブ実行方法
        private emExecution _execution { get; set; }
        public emExecution Execution {
            get => _execution;
            set
            {
                _execution = value;
                OnPropertyChanged(nameof(Execution));
            }
        }
        // 実行ボタンの活性・非活性
        private bool _jobBoolean{ get; set; }
        public bool JobBoolean {
            get => _jobBoolean;
            set
            {
                _jobBoolean = value;
                OnPropertyChanged(nameof(JobBoolean));
            }
        }
        // ジョブステータス
        private emStatus _status { get; set; }
        public emStatus Status {
            get => _status;
            set
            {
                _status = value;
                OnPropertyChanged(nameof(Status));
            }
        }
        // 更新日付
        private string _updateDate { get; set; }
        public string UpdateDate {
            get => _updateDate;
            set
            {
                _updateDate = value;
                OnPropertyChanged(nameof(UpdateDate));
            }
        }
        // シナリオグループ（これで、TreeView上のシナリオのボタンやテキストを非表示にする）
        public bool IsScenarioGroup { get; set; }
        // 入れ子リスト
        public ObservableCollection<JobListItemViewModel> Children { get; set; }

        // 実行ボタン
        public ICommand RunCommand { get; set; }
        // 詳細ボタン
        public ICommand DetailCommand { get; set; }
        // ログボタン
        public ICommand LogCommand { get; set; }

        // Init
        public JobListItemViewModel()
        {
            // 初期値 セット
            IsScenarioGroup = false;
            UpdateDate = "";
            Children = new ObservableCollection<JobListItemViewModel>();

            // コマンド 初期化
            _command = new JobListItemCommand();

            // コマンド 画面処理にセット
            RunCommand = new RelayCommand(_command.RunButton_Click);
            DetailCommand = new RelayCommand(_command.DetailButton_Click);
            LogCommand = new RelayCommand(_command.LogButton_Click);
        }

        // VM変更検知
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

}
