using JobManagementApp.BaseClass;
using JobManagementApp.Services;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JobManagementApp.Commands
{
    class JobDetailCommand : JobCommandArgument
    {
        /// <summary> 
        /// シナリオ　フォーカスアウト処理
        /// </summary> 
        public void OnTextBoxLostFocus(string scenario, Action<string> setEda)
        {
            int result;

            //シナリオと枝番からデータ取得
            DataTable dt = JobService.GetMaxEda(scenario);

            if (dt.Rows.Count > 0)
            {
                result = int.Parse(dt.Rows[0]["EDA"].ToString()) + 1;
            }
            else
            {
                result = 1;
            }

            setEda(result.ToString());
        }
    }
}
