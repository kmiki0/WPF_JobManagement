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
        public string Id { get; set; }
        // ジョブ名
        public string Name { get; set; }
        // cmd実行方法
        public string ExecCommnad { get; set; }
        // 前続ジョブ
        public string BeforeJob { get; set; }
        // ジョブ実行可否
        public bool JobBoolean { get; set; }
        // 受信先
        public string Receive { get; set; }
        // 送信先
        public string Send { get; set; }
        // メモ
        public string Memo { get; set; }
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

        /// <summary>
        /// Init（新規）
        /// </summary>
        public JobDetailViewModel(IJobDetailModel IF)
        {
            // 初期値 セット
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
            // 引数をvmにセット
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
