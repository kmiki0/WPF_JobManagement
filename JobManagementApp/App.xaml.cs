using System;
using System.Collections.Generic;
using System.Linq;
using System.Data;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using JobManagementApp.Manager;

namespace JobManagementApp
{

    public partial class App : Application
    {
        [STAThread]
        public static void Main()
        {
            var app = new App();
            app.InitializeComponent();
            app.Run();
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            StartDB();
        }

        private void StartDB()
        {
            try
            {
                // DatabaseManager インスタンスの取得
                var pobjOraDb = DatabaseManager.Instance.pobjOraDb;

                // ORACLEセッションの確立
                if (pobjOraDb.pInitOra() == false) 
                {
                    ErrLogFile.WriteLog("ORACLE 初期化エラー");
                    throw new Exception();
                }

                // DBオープン
                if (pobjOraDb.pOpenOra() == false)
                {
                    ErrLogFile.WriteLog("ORACLE オープンエラー");
                    throw new Exception();
                }

                // オープンクローズを制御する
                pobjOraDb.pOpenCloseCtrl = true;
            }
            catch (Exception e)
            {
                throw e;
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            // アプリケーション終了時にリソースを解放
            DatabaseManager.Instance.pobjOraDb.pOpenCloseCtrl = false;
            DatabaseManager.Instance.pobjOraDb.pCloseOra();
            GC.Collect();
            base.OnExit(e);
        }

    }
}
