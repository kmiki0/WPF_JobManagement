using JobManagementApp.BaseClass;
using JobManagementApp.Helpers;
using JobManagementApp.Manager;
using JobManagementApp.Models;
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
    public class MainCommand : JobCommandArgument
    {
        private readonly MainViewModel _vm;
        private readonly IMainModel _if;

        public MainCommand(MainViewModel VM, IMainModel IF)
        {
            _vm = VM;
            _if = IF;
        }

        /// <summary> 
        /// ユーザー保存　押下イベント
        /// </summary> 
        public void SaveUserButton_Click(object _)
        {
            if (_if.SaveCacheUser(_vm.UserId))
            {
                MessageBox.Show("キャッシュに保存しました。");
            }
            else
            {
                MessageBox.Show("キャッシュに保存に失敗しました。");
            }
        }

        /// <summary> 
        /// 画面更新　押下イベント
        /// </summary> 
        public void RefreshButton_Click(object _)
        {
            // 受信出来次第、画面更新
            _if.RefreshJobList().ContinueWith(x =>
            {
                _vm.Jobs = x.Result;
            });
        }

        /// <summary> 
        /// ジョブ追加　押下イベント
        /// </summary> 
        public void NewJobButton_Click(object _)
        {
            var vm = new JobDetailViewModel();
            //vm.RequestClose += DetailWindow_RequestClose;
            var detailWindow = new JobDetailWindow(vm);
            var window = detailWindow as Window;
            // ウィンドウの表示位置　調整
            WindowHelper.SetWindowLocation(ref window);
            vm.window = detailWindow;
            detailWindow.DataContext = vm;
            detailWindow.ShowDialog();
        }
    }
}
