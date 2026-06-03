using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using Libs;

namespace JobCatcher
{
    public partial class Settings : Window
    {
        MainWindow main;
        Properties.Settings config = Properties.Settings.Default;

        public Settings()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            main = this.Owner as MainWindow;

            //Общие
            periodWork.Text = config.periodWork.ToString();
            periodFl.Text = config.periodFl.ToString();
            periodUpdateResume.Text = config.periodUpdateResume.ToString();
            letterWork.AppendText(config.letterWork.Replace("{n}", "\r\n"));
            letterFl.AppendText(config.letterFl.Replace("{n}", "\r\n"));

            //Сеть
            proxyCheck.IsChecked = config.proxyBool;
            proxyText.Text = config.proxyText;

            //Интерфейс
            minimCheck.IsChecked = config.minimTray;
            closeCheck.IsChecked = config.closeTray;
        }

        private void tree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            TreeViewItem node = e.NewValue as TreeViewItem;
            tab.SelectedIndex = node.TabIndex;
        }

        private void okButton_Click(object sender, RoutedEventArgs e)
        {
            applyButton_Click(this, null);
            this.Close();
        }

        private void applyButton_Click(object sender, RoutedEventArgs e)
        {
            //Общие
            double d = Helper.DoubleParse(periodWork.Text); if (d != 0) config.periodWork = d;
            d = Helper.DoubleParse(periodFl.Text); if (d != 0) config.periodFl = d;
            d = Helper.DoubleParse(periodUpdateResume.Text); if (d != 0) config.periodUpdateResume = d;
            string s = new TextRange(letterWork.Document.ContentStart, letterWork.Document.ContentEnd).Text.Trim();
            config.letterWork = s;
            s = new TextRange(letterFl.Document.ContentStart, letterFl.Document.ContentEnd).Text.Trim();
            config.letterFl = s;

            //Сеть
            config.proxyBool = (bool)proxyCheck.IsChecked;
            if ((bool)proxyCheck.IsChecked) { config.proxyText = proxyText.Text; }

            //Интерфейс
            config.minimTray = (bool)minimCheck.IsChecked;
            config.closeTray = (bool)closeCheck.IsChecked;
            main.CanClose = config.closeTray;

            config.Save();
        }

        private void cancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
