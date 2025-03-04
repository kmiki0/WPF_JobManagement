using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DICSSLORA.ACmnFunc;
using DICSSLORA.ACmnIni;
using DICSSLORA.ACmnLog;
using DICSSLORA.ACmnOra;

namespace JobManagementApp.Manager
{
    public class DatabaseManager
    {
        public DICSSLORA.ACmnIni.clsMngIniFile pobjIniFile; // INIファイル
        public DICSSLORA.ACmnLog.clsMngLogFile pobjActLog; // 操作ログ
        public DICSSLORA.ACmnLog.clsMngLogFile pobjErrLog; // エラーログ
        
        //public DICSSLORA.ACmnOra.clsMngOracle pobjOraDb; // データベース
        public string pstrCurDir;    // カレントディレクトリ

#pragma warning disable IDE0044 // 読み取り専用修飾子を追加します
        private static DatabaseManager _instance;
#pragma warning restore IDE0044 
        private static readonly object _lock = new object();

        public DICSSLORA.ACmnOra.clsMngOracle pobjOraDb { get; private set; }

        private DatabaseManager()
        {
            var pobjIniFile = new clsMngIniFile(clsDefineCnst.pcnstININAME);

            // 読み込みエラー
            pobjIniFile.pGetInfo();

            string logPath = "C:\\it_ap\\Log\\nomal.log";
            string errPath = "C:\\it_ap\\Log\\nomal.log";

            // 操作ログインスタンス生成 
            pobjActLog = new clsMngLogFile(logPath, pobjIniFile.pGetItemInt("NORMAL_LOG", "LOG_LEVEL"));
            // エラーログインスタンス生成
            pobjErrLog = new clsMngLogFile(errPath, pobjIniFile.pGetItemInt("ERROR_LOG", "LOG_LEVEL"));


            //カレントディレクトリ取得
            pstrCurDir = pobjIniFile.pGetItemString("PATH", "CURRENT_DIR");


            // ORACLEインスタンス生成 
            pobjOraDb = new DICSSLORA.ACmnOra.clsMngOracle(
                pobjIniFile.pGetItemString("DB", "DATA_SOURCE"),
                pobjIniFile.pGetItemString("DB", "USER_ID"),
                pobjIniFile.pGetItemString("DB", "PASSWORD"),
                pobjIniFile.pGetItemInt("DB", "RTRY_SLEEP"),
                pobjIniFile.pGetItemInt("DB", "RTRY_CNT"),
                pobjActLog, pobjErrLog, clsMngOracle.peOraCom.ODP);
        }

        // インスタンスを取得するためのプロパティ
        public static DatabaseManager Instance
        {
            get
            {
                lock (_lock)
                {
                    return _instance ?? new DatabaseManager();
                }
            }
        }
    }

}
