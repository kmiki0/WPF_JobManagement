using JobManagementApp.Manager;
using JobManagementApp.Services;
using JobManagementApp.ViewModels;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JobManagementApp.Models
{
    public interface IJobLogModel
    {

        // ユーザー保存
        bool SaveCachePath(string path);
        // ジョブ管理テーブル 取得
        Task<JobManegment> GetJobManegment(string scenario, string eda);
        // ジョブ関連ファイル 取得
        Task<List<JobLinkFile>> GetJobLinkFile(string scenario, string eda);


        //// シナリオから登録可能な枝番 取得
        //Task<int> GetNewEda(string scenario);
        //// ジョブ管理テーブル 更新
        //Task<bool> UpdateJobManegment(JobManegment job);
        //// ジョブ管理テーブル 削除
        //Task<bool> DeleteJobManegment(string scenario, string eda);
    }

    public class JobLogModel : IJobLogModel
    {
        /// <summary> 
        /// パス保存
        /// </summary> 
        public bool SaveCachePath(string path)
        {
            bool result = false;

            try
            {
                if (!string.IsNullOrEmpty(path?.ToString()))
                {
                    // キャッシュに保存
                    UserFileManager manager = new UserFileManager();
                    manager.SaveUserFilePath(manager.CacheKey_FilePath, path.ToString());
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
        /// ジョブ管理テーブル ジョブID取得
        /// </summary> 
        public async Task<JobManegment> GetJobManegment(string scenario, string eda)
        {
            return await Task.Run(() =>
            {
                return JobService.GetJobManegment(scenario, eda);
            }).ContinueWith(x =>
            {
                if (x.Result.Rows.Count > 0)
                {
                    // 取得内容をテーブル型にセットして返す
                    return new JobManegment
                    {
                        ID = x.Result.Rows[0]["ID"].ToString(),
                    };
                }
                else
                {
                    // 値取れない場合、空を返す
                    return new JobManegment();
                }
            });
        }

        /// <summary> 
        /// シナリオと枝番から、ジョブ関連ファイル 取得
        /// </summary> 
        public async Task<List<JobLinkFile>> GetJobLinkFile(string scenario, string eda)
        {
            return await Task.Run(() =>
            {
                return JobService.GetJobLinkFile(scenario, eda);
            }).ContinueWith(x =>
            {
                if (x.Result.Rows.Count > 0)
                {
                    var logList = new List<JobLinkFile>();

                    // ログごとに格納
                    foreach (DataRow row in x.Result.Rows)
                    {
                        var item = new JobLinkFile{
                            SCENARIO = row["SCENARIO"].ToString(),
                            EDA = row["EDA"].ToString(),
                            FILEPATH = row["FILEPATH"].ToString(),
                            FILENAME = row["FILENAME"].ToString(),
                            FILETYPE = int.Parse(row["FILETYPE"].ToString()),
                            OBSERVERTYPE = int.Parse(row["OBSERVERTYPE"].ToString()),
                        };
                        logList.Add(item);
                    }
                    return logList;
                }
                else
                {
                    return new List<JobLinkFile>();
                }
            });
        }




        ///// <summary> 
        ///// シナリオから登録可能な枝番 取得
        ///// </summary> 
        //public async Task<int> GetNewEda(string scenario)
        //{
        //    return await Task.Run(() =>
        //    {
        //        // 枝番の最大値 取得
        //        return JobService.GetMaxEda(scenario);;
        //    }).ContinueWith(x =>
        //    {
        //        if (x.Result.Rows.Count > 0)
        //        {
        //            // 取得出来れば 枝番 +1 
        //            return int.Parse(x.Result.Rows[0]["EDA"].ToString()) + 1;
        //        }
        //        else
        //        {
        //            // データない場合 初期値 1
        //            return 1;
        //        }
        //    });
        //}



        ///// <summary> 
        ///// ジョブ管理テーブル 更新
        ///// </summary> 
        //public async Task<bool> UpdateJobManegment(JobManegment job)
        //{
        //    return await Task.Run(() => 
        //    { 
        //        return JobService.UpdateJobManegment(job); 
        //    }).ContinueWith(x => 
        //    {
        //        return x.Result;
        //    });
        //}

        ///// <summary> 
        ///// ジョブ管理テーブル 削除
        ///// </summary> 
        //public async Task<bool> DeleteJobManegment(string scenario, string eda)
        //{
        //    return await Task.Run(() => 
        //    { 
        //        return JobService.DeleteJobManegment(scenario, eda); 
        //    }).ContinueWith(x => 
        //    {
        //        return x.Result;
        //    });
        //}

    }
}
