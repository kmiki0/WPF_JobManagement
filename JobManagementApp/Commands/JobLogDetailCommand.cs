using JobManagementApp.Helpers;
using JobManagementApp.Models;
using JobManagementApp.Services;
using JobManagementApp.ViewModels;
using JobManagementApp.Views;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace JobManagementApp.Commands
{
    public class JobLogDetailCommand
    {
        /// <summary> 
        /// 更新ボタン　押下イベント
        /// </summary> 
        public void UpdateButtonCommand(JobLinkFile job)
        {
            // 物理削除
            if (JobService.DeleteJobLinkFile(job))
            {
                // 登録
                if (JobService.UpdateJobLinkFile(job))
                {
                    MessageBox.Show("ジョブ関連ファイルの更新が完了しました。");
                }
            }
        }

        /// <summary> 
        /// 閉じるボタン　押下イベント
        /// </summary> 
        public void CloseButtonCommand(Window window)
        {
            if (window != null)
            {
                window.Close();
            }
        }

        /// <summary> 
        /// 削除ボタン　押下イベント
        /// </summary> 
        public void DeleteButtonCommand(JobLinkFile job)
        {
            if (JobService.DeleteJobLinkFile(job))
            {
                MessageBox.Show("関連ファイルの削除が完了しました。");
            }
        }
    }
}
