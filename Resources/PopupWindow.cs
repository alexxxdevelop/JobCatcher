using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Shapes;
using System.Windows.Media.Animation;

namespace JobCatcher.Resources
{
    public partial class PopupWindowStyled : ResourceDictionary
    {
        public PopupWindowStyled()
        {
            InitializeComponent();
        }

        private void windowFrame_Loaded(object sender, RoutedEventArgs e)
        {
            Window win = (Window)((FrameworkElement)sender).TemplatedParent;
            ((Storyboard)win.FindResource("fadeIn")).Begin(win);
        }

        private void titleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            Window win = (Window)((FrameworkElement)sender).TemplatedParent;
            win.DragMove();
        }

        private void cmdClose_Click(object sender, RoutedEventArgs e)
        {
            Window win = (Window)((FrameworkElement)sender).TemplatedParent;
            win.Close();
        }
    }
}
