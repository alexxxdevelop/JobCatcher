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
using Libs;

namespace JobCatcher
{
    public partial class PopupWindow : Window
    {
        public MainWindow main;

        public PopupWindow()
        {
            InitializeComponent();
        }

        private void detail_Click(object sender, RoutedEventArgs e)
        {
            main.detailFlList_Click(sender, e);
            this.Close();
        }

        private void answer_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var v = main.NewContext().Vacancies.FirstOrDefault(x => x.Id == (int)button.Tag);
            if (v.Kind == "work") main.answerWorkList_Click(sender, e);
            else if (v.Kind == "fl") main.answerFlList_Click(sender, e);
            this.Close();
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            JobHelper.popupWindows.Remove(this);
        }
    }
}
