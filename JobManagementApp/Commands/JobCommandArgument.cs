using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JobManagementApp.BaseClass
{
    // 引数クラス シナリオと枝番
    public class JobCommandArgument
    {
        public string scenario { get; set; }
        public string eda { get; set; }

        protected static JobCommandArgument ConvertParameter(object parameter)
        {
            var result = new JobCommandArgument();

            var tuple = parameter as Tuple<object, object>;
            if (tuple != null)
            {
                result.scenario = tuple.Item1?.ToString();
                result.eda = tuple.Item2?.ToString();
            }

            return result;
        }
    }
}
