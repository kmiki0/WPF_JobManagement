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

        // Init
        public JobDetailViewModel()
        {
            // 初期値 セット
            this.JobBoolean = true;

            // コマンド 初期化
            _command = new JobDetailCommand();

            // コマンド 画面処理にセット
            UpdateCommand = new RelayCommand(UpdateButtonCommand);
            CloseCommand = new RelayCommand(CloseButtonCommand);
            DeleteCommand = new RelayCommand(DeleteButtonCommand);
            ScenarioLostFocusCommand = new AsyncRelayCommand(ScenarioTextLostFocusCommnad);
        }

        public JobDetailViewModel(string scenario, string eda)
        {
            // 引数をvmにセット
            this.Scenario = scenario;
            this.Eda = eda;

            // データ取得して画面に表示
            if (!string.IsNullOrEmpty(Scenario) && !string.IsNullOrEmpty(Eda))
            {
                SetJobDetailViewModel();
            }

            // コマンド 初期化
            _command = new JobDetailCommand();

            // コマンド 画面処理にセット
            UpdateCommand = new RelayCommand(UpdateButtonCommand);
            CloseCommand = new RelayCommand(CloseButtonCommand);
            DeleteCommand = new RelayCommand(DeleteButtonCommand);
        }


        //シナリオと枝番からデータ取得
        public void SetJobDetailViewModel()
        {
            //ジョブ管理の取得
            DataTable dt = JobService.GetJobManegment(Scenario, Eda);

            if (dt.Rows.Count > 0)
            {
                Scenario = dt.Rows[0]["SCENARIO"].ToString();
                Eda = dt.Rows[0]["EDA"].ToString();
                Id = dt.Rows[0]["ID"].ToString();
                Name = dt.Rows[0]["NAME"].ToString();
                SelectedExecution = (emExecution)int.Parse(dt.Rows[0]["EXECUTION"].ToString());
                ExecCommnad = dt.Rows[0]["ExecCommnad"].ToString();
                SelectedStatus = (emStatus)int.Parse(dt.Rows[0]["STATUS"].ToString());
                BeforeJob = dt.Rows[0]["BeforeJob"].ToString();
                JobBoolean = int.Parse(dt.Rows[0]["JOBBOOLEAN"].ToString()) != 0;
                Receive = dt.Rows[0]["Receive"].ToString();
                Send = dt.Rows[0]["SEND"].ToString();
                Memo = dt.Rows[0]["MEMO"].ToString().Replace("\\n", Environment.NewLine);
            }
        }

        //ボタン　クリックイベント
        // 登録ボタン
        private void UpdateButtonCommand(object parameter)
        {
            var jobManegment = new JobManegment
            {
                SCENARIO = Scenario,
                EDA = Eda,
                ID = Id,
                NAME = (Name is null) ? "" : Name,
                EXECUTION = (int)SelectedExecution,
                EXECCOMMNAD = (ExecCommnad is null) ? "" : ExecCommnad,
                STATUS = (int)SelectedStatus,
                BEFOREJOB = (BeforeJob is null) ? "" : BeforeJob,
                JOBBOOLEAN = JobBoolean ? 1 : 0,
                RECEIVE = (Receive is null) ? "" : Receive,
                SEND = (Send is null) ? "" : Send,
                MEMO = (Memo is null) ? "" : Memo.Replace(Environment.NewLine, "\\n")
            };

            if (JobService.UpdateJobManegment(jobManegment))
            {
                MessageBox.Show("ジョブ管理の更新が完了しました。");
                CloseButtonCommand(null);
            }
        }

        public event EventHandler<JobListItemViewModel> RequestClose;

        // 閉じるボタン
        private void CloseButtonCommand(object parameter)
        {
            // DetailViewModelの値をEventHandler<JobListItemViewModel>型でセット
            RequestClose?.Invoke(this, new JobListItemViewModel {
                Scenario = this.Scenario,
                Eda = this.Eda,
                Id = this.Id,
                Name = this.Name,
                Execution = this.SelectedExecution,
                JobBoolean = this.JobBoolean,
                Status = this.SelectedStatus,
            });

            if (window != null)
            {
                window.Close();
            }
        }

        // 削除ボタン
        private void DeleteButtonCommand(object parameter)
        {
            if (JobService.DeleteJobManegment(Scenario, Eda))
            {
                MessageBox.Show("ジョブ管理の論理削除フラグを立てました");
                CloseButtonCommand(null);
            }
        }

        // シナリオ　フォーカスアウト
        private async Task ScenarioTextLostFocusCommnad(object parameter)
        {
            if (Scenario != null)
            {
                // 枝番　設定
                await Task.Run(() => 
                    _command.OnTextBoxLostFocus(this.Scenario, eda => this.Eda = eda)
                );
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
