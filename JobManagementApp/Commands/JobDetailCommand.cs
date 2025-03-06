using JobManagementApp.BaseClass;
using JobManagementApp.Models;
using JobManagementApp.Services;
using JobManagementApp.ViewModels;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace JobManagementApp.Commands
{
    class JobDetailCommand : JobCommandArgument
    {
        private readonly JobDetailViewModel _vm;
        private readonly IJobDetailModel _if;

        public JobDetailCommand(JobDetailViewModel VM, IJobDetailModel IF)
        {
            _vm = VM;
            _if = IF;
        }

        /// <summary> 
        /// シナリオ　フォーカスアウト処理
        /// </summary> 
        public void LoadViewModel()
        {
            _if.GetJobManegment(_vm.Scenario, _vm.Eda).ContinueWith(x =>
            {
                // 画面項目に設定
                _vm.Scenario = x.Result.SCENARIO;
                _vm.Eda = x.Result.EDA;
                _vm.Id = x.Result.ID;
                _vm.Name = x.Result.NAME;
                _vm.SelectedExecution = (emExecution)x.Result.EXECUTION;
                _vm.ExecCommnad = x.Result.EXECCOMMNAD;
                _vm.SelectedStatus = (emStatus)x.Result.STATUS;
                _vm.BeforeJob = x.Result.BEFOREJOB;
                _vm.JobBoolean = x.Result.JOBBOOLEAN != 0;
                _vm.Receive = x.Result.RECEIVE;
                _vm.Send = x.Result.SEND;
                _vm.Memo = x.Result.MEMO;
            });
        }

        /// <summary> 
        /// シナリオ　フォーカスアウト処理
        /// </summary> 
        public void UpdateButton_Click(object _)
        {
            // テーブル型に格納
            var job = new JobManegment
            {
                SCENARIO = _vm.Scenario,
                EDA = _vm.Eda,
                ID = _vm.Id,
                NAME = (_vm.Name is null) ? "" : _vm.Name,
                EXECUTION = (int)_vm.SelectedExecution,
                EXECCOMMNAD = (_vm.ExecCommnad is null) ? "" : _vm.ExecCommnad,
                STATUS = (int)_vm.SelectedStatus,
                BEFOREJOB = (_vm.BeforeJob is null) ? "" : _vm.BeforeJob,
                JOBBOOLEAN = _vm.JobBoolean ? 1 : 0,
                RECEIVE = (_vm.Receive is null) ? "" : _vm.Receive,
                SEND = (_vm.Send is null) ? "" : _vm.Send,
                MEMO = (_vm.Memo is null) ? "" : _vm.Memo.Replace(Environment.NewLine, "\\n")
            };

            // 登録処理 実施
            _if.UpdateJobManegment(job).ContinueWith(x =>
            {
                if (x.Result)
                {
                    MessageBox.Show("ジョブ管理の更新が完了しました。");
                    // 自身を閉じる
                    CloseButton_Click(null);
                }
                else
                {
                    MessageBox.Show("ジョブ管理の更新に失敗しました。");
                }
            });
        }

        /// <summary> 
        /// シナリオ　フォーカスアウト処理
        /// </summary> 
        public void CloseButton_Click(object _)
        {
            // DetailViewModelの値をEventHandler<JobListItemViewModel>型でセット
            //_vm.RequestClose?.Invoke(_vm, new JobListItemViewModel
            //{
            //    Scenario = _vm.Scenario,
            //    Eda = _vm.Eda,
            //    Id = _vm.Id,
            //    Name = _vm.Name,
            //    Execution = _vm.SelectedExecution,
            //    JobBoolean = _vm.JobBoolean,
            //    Status = _vm.SelectedStatus,
            //});

            if (_vm.window != null)
            {
                _vm.window.Close();
            }
        }

        /// <summary> 
        /// シナリオ　フォーカスアウト処理
        /// </summary> 
        public void DeleteButton_Click(object _)
        {
            // 削除処理 実施
            _if.DeleteJobManegment(_vm.Scenario, _vm.Eda).ContinueWith(x =>
            {
                if (x.Result)
                {
                    MessageBox.Show("ジョブ管理の論理削除フラグを立てました");
                    // 自身を閉じる
                    CloseButton_Click(null);
                }
                else
                {
                    MessageBox.Show("ジョブ管理の削除に失敗しました。");
                }
            });
        }

        /// <summary> 
        /// シナリオ　フォーカスアウト処理
        /// </summary> 
        public void ScenarioTextBox_LostFocus(object _)
        {
            _if.GetNewEda(_vm.Scenario).ContinueWith(x => 
            {
                _vm.Eda = x.Result.ToString();
            });
        }
    }
}
