using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Media;

namespace JobManagementApp.Models
{
    public enum emExecution
    {
        //0 : 自動起動, 1 : 運用処理管理R , 2 : コマンドプロンプト , 9 : その他
        AUTO,
        UNYO,
        CMD,
        ETC,
    }

    public enum emStatus
    {
        //0 : 待機中 , 1 : 実行中, 2 : 正常終了, 9 : 異常終了
        WAIT,
        RUN,
        SUCCESS,
        ERROR,
    }

    public enum emFileType
    {
        //0 : ログ , 1 : 受信ファイル, 2 : 送信ファイル, 3 : その他ファイル
        LOG,
        RECEIVE,
        SEND,
        ETC,
    }

    public enum emObserverStatus
    {
        //0 :取得済, 1 : 監視中, 9 : 停止中
        SUCCESS = 0,
        OBSERVER = 1,
        STOP = 9,
    }

    public class JobManegment
    {
        // シナリオ
        public string SCENARIO { get; set; }
        // 枝番
        public string EDA { get; set; }
        // ジョブID
        public string ID { get; set; }
        // ジョブ名
        public string NAME { get; set; }
        // ジョブ実行方法
        public int EXECUTION { get; set; }
        // cmd実行方法
        public string EXECCOMMNAD { get; set; }
        // ジョブステータス
        public int STATUS { get; set; }
        // 前続ジョブ
        public string BEFOREJOB { get; set; }
        // ジョブ実行可否
        public int JOBBOOLEAN { get; set; }
        // 受信先
        public string RECEIVE { get; set; }
        // 送信先
        public string SEND { get; set; }
        // メモ
        public string MEMO { get; set; }
    }

    public class JobLinkFile
    {
        // シナリオ
        public string SCENARIO { get; set; }
        // 枝番
        public string EDA { get; set; }
        // ジョブID
        public string JOBID { get; set; }
        // ファイル名
        public string FILENAME { get; set; }
        // ファイル名（以前）
        public string OLDFILENAME { get; set; }
        //ファイルパス
        public string FILEPATH { get; set; }
        //ファイルパス（以前）
        public string OLDFILEPATH { get; set; }
        //ファイルタイプ
        public int FILETYPE { get; set; }
        //ファイルカウント
        public int FILECOUNT { get; set; }
        //監視タイプ
        public int OBSERVERTYPE { get; set; }
    }

    public class JobOwenreUser
    {
        // ジョブID
        public string JobId { get; set; }
        // ユーザー
        public string UserId { get; set; }
    }


    // シナリオと枝番を元に運用処理管理Rを検索した取得項目
    public class JobUnyoCtlModel
    {
        // シナリオ
        public string Scenario { get; set; }
        // 枝番
        public string Eda { get; set; }
        // ジョブID
        public string Id { get; set; }
        // 処理フラグ
        public string SyrFlg { get; set; }
        // 更新日付
        public string UpdDt { get; set; }
    }

    public class JobParamModel
    {
        // シナリオ
        public string Scenario { get; set; }
        // 枝番
        public string Eda { get; set; }
        // ジョブID
        public string JobId { get; set; }
        // ファイル名
        public string FileName { get; set; }
        // ファイルパス
        public string FilePath { get; set; }
        // 同名ファイル個数
        public int FileCount { get; set; }
    }

}
