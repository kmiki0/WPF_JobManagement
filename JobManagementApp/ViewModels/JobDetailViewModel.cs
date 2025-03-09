using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.ObjectModel;
using System.Windows.Input;
using JobManagementApp.Commands;
using JobManagementApp.Models;
using JobManagementApp.Services;
using System.Windows;
using System.ComponentModel;
using System.Data;

namespace JobManagementApp.ViewModels
{
    public class JobDetailViewModel : INotifyPropertyChanged 
    {
        // イベント処理
        private readonly JobDetailCommand _command;

        // JobDetailWindow
        public Window window;

        // Closeイベント 上書き
        public event EventHandler<JobListItemViewModel> RequestClose;

        // シナリオ
        public string Scenario { get; set; }
        // 枝番
        private string _eda;
        public string Eda
        {
            get { return _eda; }
            set
            {
                _eda = value;
                OnPropertyChanged(nameof(Eda));
            }
        }
        // ジョブID
        private string _id { get; set; }
        public string Id
        {
            get { return _id; }
            set
            {
                _id = value;
                OnPropertyChanged(nameof(Id));
            }
        }
        // ジョブ名
        private string _name { get; set; }
        public string Name
        {
            get { return _name; }
            set
            {
                _name = value;
                OnPropertyChanged(nameof(Name));
            }
        }
        // cmd実行方法
        private string _execCommnad { get; set; }
        public string ExecCommnad
        {
            get { return _execCommnad; }
            set
            {
                _execCommnad = value;
                OnPropertyChanged(nameof(ExecCommnad));
            }
        }
        // 前続ジョブ
        private string _beforeJob { get; set; }
        public string BeforeJob
        {
            get { return _beforeJob; }
            set
            {
                _beforeJob = value;
                OnPropertyChanged(nameof(BeforeJob));
            }
        }
        // ジョブ実行可否
        private bool _jobBoolean { get; set; }
        public bool JobBoolean
        {
            get { return _jobBoolean; }
            set
            {
                _jobBoolean = value;
                OnPropertyChanged(nameof(JobBoolean));
            }
        }
        // 受信先
        private string _receive { get; set; }
        public string Receive
        {
            get { return _receive; }
            set
            {
                _receive = value;
                OnPropertyChanged(nameof(Receive));
            }
        }
        // 送信先
        private string _send { get; set; }
        public string Send
        {
            get { return _send; }
            set
            {
                _send = value;
                OnPropertyChanged(nameof(Send));
            }
        }
        // メモ
        private string _memo { get; set; }
        public string Memo
        {
            get { return _memo; }
            set
            {
                _memo = value;
                OnPropertyChanged(nameof(Memo));
            }
        }
        // ジョブ実行方法 コンボボックス
        public Array cmbExecution => Enum.GetValues(typeof(emExecution));
        private emExecution _selectedExecution;
        public emExecution SelectedExecution
        {
            get { return _selectedExecution; }
            set
            {
                _selectedExecution = value;
                OnPropertyChanged(nameof(SelectedExecution));
            }
        }
        // ジョブステータス コンボボックス
        public Array cmbStatus => Enum.GetValues(typeof(emStatus));
        private emStatus _selectedStatus;
        public emStatus SelectedStatus
        {
            get { return _selectedStatus; }
            set
            {
                _selectedStatus = value;
                OnPropertyChanged(nameof(SelectedStatus));
            }
        }

        // 登録ボタン
        public ICommand UpdateCommand { get; set; }
        // 削除ボタン
        public ICommand DeleteCommand { get; set; }
        // 閉じるボタン
        public ICommand CloseCommand { get; set; }
        // シナリオ　フォーカスアウト　
        public ICommand ScenarioLostFocusCommand { get; }

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
        /// Init（新規）
        /// </summary>
        public JobDetailViewModel(IJobDetailModel IF)
        {
            // 初期値セット
            this.IsButtonEnabled = true;
            this.JobBoolean = true;

            // コマンド 初期化
            _command = new JobDetailCommand(this, IF);
            UpdateCommand = new RelayCommand(_command.UpdateButton_Click);
            CloseCommand = new RelayCommand(_command.CloseButton_Click);
            DeleteCommand = new RelayCommand(_command.DeleteButton_Click);
            ScenarioLostFocusCommand = new RelayCommand(_command.ScenarioTextBox_LostFocus);
        }

        /// <summary>
        /// Init（編集）
        /// </summary>
        public JobDetailViewModel(IJobDetailModel IF, string scenario, string eda)
        {
            // 初期値セット
            this.IsButtonEnabled = true;
            this.Scenario = scenario;
            this.Eda = eda;

            // コマンド 初期化
            _command = new JobDetailCommand(this, IF);
            UpdateCommand = new RelayCommand(_command.UpdateButton_Click);
            CloseCommand = new RelayCommand(_command.CloseButton_Click);
            DeleteCommand = new RelayCommand(_command.DeleteButton_Click);

            // データ取得して画面に表示
            if (!string.IsNullOrEmpty(this.Scenario) && !string.IsNullOrEmpty(this.Eda))
            {
                _command.LoadViewModel();
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
