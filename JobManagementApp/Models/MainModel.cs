using JobManagementApp.Manager;
using JobManagementApp.ViewModels;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JobManagementApp.Models
{
    public interface IMainModel
    {
        // ユーザー保存
        bool SaveCacheUser(string userId);
        // 画面のジョブリスト　再取得
        Task<ObservableCollection<JobListItemViewModel>> RefreshJobList();
    }

    /// <summary> 
    /// ユーザー保存　押下イベント
    /// </summary> 
    public class MainModel : IMainModel
    {
        public bool SaveCacheUser(string userId)
        {
            bool result = false;

            try
            {
                if (!string.IsNullOrEmpty(userId?.ToString()))
                {
                    // キャッシュに保存
                    UserFileManager manager = new UserFileManager();
                    manager.SaveUserFilePath(manager.CacheKey_UserId, userId.ToString());
                    result = true;
                }
            }
            catch (Exception)
            {
                return false;
            }

            return result;
        }

        /// <summary> 
        /// 画面更新　押下イベント
        /// </summary> 
        public async Task<ObservableCollection<JobListItemViewModel>> RefreshJobList()
        {
            return new ObservableCollection<JobListItemViewModel>();
        }
    }
}
