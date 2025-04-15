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
                    manager.SaveCache(manager.CacheKey_FilePath, path.ToString());
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
                            FILECOUNT = int.Parse(row["FILECOUNT"].ToString()),
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
    }
}
