using JobManagementApp.Models;
using JobManagementApp.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace JobManagementApp.Views
{
    public partial class JobDetailWindow : Window
    {
        public JobDetailWindow(JobDetailViewModel vm)
        {
            InitializeComponent();
            // ViewModelのRequestCloseイベントでウィンドウを閉じる
            vm.RequestClose += (s, e) => this?.Close();
        }
    }
}
