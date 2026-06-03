using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Threading.Tasks;
using System.Text.RegularExpressions;

namespace JobCatcher
{
    public partial class DetailWindow : Window
    {
        Properties.Settings config = Properties.Settings.Default;
        public string content;
        MainWindow main;

        public DetailWindow()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            main = this.Owner as MainWindow;
            text.Text = content;
            answer.IsEnabled = true;
        }

        private void answer_Click(object sender, RoutedEventArgs e)
        {
            main.answerWorkList_Click(sender, e);
            this.Close();
        }
    }
}
