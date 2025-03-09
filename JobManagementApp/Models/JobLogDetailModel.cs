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
    public interface IJobLogDetailModel
    {

        // シナリオと枝番から、ジョブIDを取得
        Task<string> GetJobId(string scenario, string eda);
        // ジョブ関連ファイル 取得
        Task<JobLinkFile> GetJobLinkFile(string Scenario, string Eda, string FileName, string FilePath);
        // ジョブ関連ファイル 登録
        Task<bool> RegistJobLinkFile(JobLinkFile job);
        // ジョブ関連ファイル 削除
        Task<bool> DeleteJobLinkFile(JobLinkFile job);
    }

    public class JobLogDetailModel : IJobLogDetailModel
    {

        public async Task<string> GetJobId(string scenario, string eda)
        {
            return await Task.Run(() =>
            {
                return JobService.GetJobManegment(scenario, eda);
            }).ContinueWith(x =>
            {
                return x.Result?.Rows[0]["ID"].ToString();
            });
        }

        public async Task<JobLinkFile> GetJobLinkFile(string scenario, string eda, string fileName, string filePath)
        {
            return await Task.Run(() =>
            {
                return JobService.GetJobLinkFile(scenario, eda, fileName, filePath);
            }).ContinueWith(x =>
            {
                if (x.Result.Rows.Count > 0)
                {
                    return new JobLinkFile
                    {
                        SCENARIO = x.Result.Rows[0]["SCENARIO"].ToString(),
                        EDA = x.Result.Rows[0]["EDA"].ToString(),
                        JOBID = x.Result.Rows[0]["JOBID"].ToString(),
                        FILENAME = x.Result.Rows[0]["FILENAME"].ToString(),
                        OLDFILENAME = x.Result.Rows[0]["FILENAME"].ToString(),
                        FILEPATH = x.Result.Rows[0]["FILEPATH"].ToString(),
                        OLDFILEPATH = x.Result.Rows[0]["FILEPATH"].ToString(),
                        FILETYPE = int.Parse(x.Result.Rows[0]["FILETYPE"].ToString()),
                        OBSERVERTYPE = int.Parse(x.Result.Rows[0]["OBSERVERTYPE"].ToString())
                    };
                }
                else
                {
                    return new JobLinkFile();
                }
            });
        }

        /// <summary> 
        /// ジョブ関連ファイル 登録
        /// </summary> 
        public async Task<bool> RegistJobLinkFile(JobLinkFile job)
        {
            return await Task.Run(() =>
            {
                return JobService.UpdateJobLinkFile(job);
            }).ContinueWith(x =>
            {
                return x.Result;
            });
        }

        /// <summary> 
        /// ジョブ関連ファイル 削除
        /// </summary> 
        public async Task<bool> DeleteJobLinkFile(JobLinkFile job)
        {
            return await Task.Run(() =>
            {
                return JobService.DeleteJobLinkFile(job);
            }).ContinueWith(x =>
            {
                return x.Result;
            });
        }
    }
}
