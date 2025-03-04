using System.Windows;
using JobManagementApp.ViewModels;

namespace JobManagementApp
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            this.DataContext = MainViewModel.Instance;
        }
    }
}

