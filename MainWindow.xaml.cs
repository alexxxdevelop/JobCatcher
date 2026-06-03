using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using System.Linq;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Text.RegularExpressions;
using System.Globalization;
using System.Threading;
using System.Windows.Input;
using System.Web;
using System.Collections.Specialized;
using System.Diagnostics;
using Libs;
using Web;
using MimeKit;
using MailKit.Net.Smtp;
using Newtonsoft.Json;
using Org.BouncyCastle.Utilities.Collections;

namespace JobCatcher
{
    public partial class MainWindow : Window
    {
        Properties.Settings config = Properties.Settings.Default;
        List<Profile> prs = new List<Profile>();

        bool stop = false;
        DispatcherTimer timerWork = new DispatcherTimer();
        DispatcherTimer timerFl = new DispatcherTimer();
        DispatcherTimer timerUpdateResume = new DispatcherTimer();
        Task taskWork;
        Task taskFl;
        Task taskUpdateResume;
        List<Vacancy> flVacancies = new List<Vacancy>();
        List<Vacancy> workVacancies = new List<Vacancy>();
        string xcsrftoken = "";
        Stopwatch sw;

        public MainWindow()
        {
            InitializeComponent();
        }

        #region Иконка в трее
        private System.Windows.Forms.NotifyIcon trayIcon = null;
        private System.Windows.Controls.ContextMenu trayMenu = null;
        private WindowState fCurrentWindowState = WindowState.Normal;
        public WindowState CurrentWindowState
        {
            get { return fCurrentWindowState; }
            set { fCurrentWindowState = value; }
        }
        private bool fCanClose = false;
        public bool CanClose
        {
            get { return fCanClose; }
            set { fCanClose = value; }
        }

        /// <summary>
        /// Переопределяет обработку первичной инициализации приложения
        /// </summary>
        /// <param name="e"></param>
        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            CreateTrayIcon();
        }

        /// <summary>
        /// Создание иконки
        /// </summary>
        /// <returns></returns>
        private bool CreateTrayIcon()
        {
            bool result = false;
            if (trayIcon == null)
            {
                trayIcon = new System.Windows.Forms.NotifyIcon();
                trayIcon.Icon = JobCatcher.Properties.Resources.icon;
                trayIcon.Text = this.Title;
                trayMenu = Resources["trayMenu"] as System.Windows.Controls.ContextMenu;
                //Поведение иконки при щелчке мыши
                trayIcon.Click += delegate(object sender, EventArgs e)
                {
                    if ((e as System.Windows.Forms.MouseEventArgs).Button == System.Windows.Forms.MouseButtons.Left)
                    {
                        ShowHideMainWindow(sender, null);
                    }
                    else
                    {
                        trayMenu.IsOpen = true;
                        Activate();
                    }
                };
                result = true;
            }
            else
            {
                result = true;
            }
            trayIcon.Visible = true;
            return result;
        }

        /// <summary>
        /// Показывает или скрывает главное окно
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ShowHideMainWindow(object sender, RoutedEventArgs e)
        {
            trayMenu.IsOpen = false;
            if (IsVisible)
            {
                Hide();
                (trayMenu.Items[0] as System.Windows.Controls.MenuItem).Header = "Показать";
            }
            else
            {
                Show();
                (trayMenu.Items[0] as System.Windows.Controls.MenuItem).Header = "Скрыть";
                WindowState = CurrentWindowState;
                Activate();
            }
        }

        /// <summary>
        /// Переопределяет встроенную реакцию на изменение состояния окна
        /// </summary>
        /// <param name="e"></param>
        protected override void OnStateChanged(EventArgs e)
        {
            base.OnStateChanged(e);
            if (this.WindowState == System.Windows.WindowState.Minimized && config.minimTray)
            {
                //Сворачиваем в трей, если окно свернуто и выбрана галочка
                Hide();
                (trayMenu.Items[0] as System.Windows.Controls.MenuItem).Header = "Показать";
            }
            else
            {
                CurrentWindowState = WindowState;
            }
        }

        /// <summary>
        /// Переопределяет обработчик запроса выхода из приложения
        /// </summary>
        /// <param name="e"></param>
        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            base.OnClosing(e);
            if (!CanClose)
            {
                e.Cancel = true;
                CurrentWindowState = this.WindowState;
                (trayMenu.Items[0] as System.Windows.Controls.MenuItem).Header = "Показать";
                Hide();
            }
            else
            {
                trayIcon.Visible = false;
            }
        }

        /// <summary>
        /// Меню Выход
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void MenuExitClick(object sender, RoutedEventArgs e)
        {
            CanClose = true;
            this.Close();
        }
        #endregion

        #region Отображение окон
        public void ShowSettings()
        {
            Window window = new Settings();
            window.Owner = this;
            Settings windowObject = window as Settings;
            window.ShowDialog();
        }

        public void ShowPopup(Vacancy v)
        {
            Dispatcher.BeginInvoke(new Action(() =>
                {
                    Window window = new PopupWindow();
                    PopupWindow windowObject = window as PopupWindow;
                    windowObject.Title = v.Name;
                    windowObject.profileName.Text = v.ProfileName;
                    windowObject.date.Text = v.Date + ", ";
                    windowObject.city.Text = v.City + (!string.IsNullOrEmpty(v.Company) ? ", " : "");
                    windowObject.company.Text = v.Company;
                    windowObject.salary.Text = v.Salary + (!string.IsNullOrEmpty(v.Salary) ? ", " : "");
                    windowObject.panel.ToolTip = string.Format("{0}, {1}, {2}, {3}", v.Date, v.City, v.Company, v.Salary);
                    windowObject.employmentType.Text = v.Content;
                    windowObject.detail.Tag = v.Id;
                    windowObject.answer.Tag = v.Id;
                    windowObject.main = this;
                    JobHelper.popupWindows.Add(window);
                    double factor = System.Windows.PresentationSource.FromVisual(this).CompositionTarget.TransformToDevice.M11;
                    int resX = (int)(System.Windows.Forms.Screen.GetWorkingArea(new System.Drawing.Point()).Width / factor);
                    int resY = (int)(System.Windows.Forms.Screen.GetWorkingArea(new System.Drawing.Point()).Height / factor);
                    int multipX = (int)(resX / windowObject.Width);
                    int multipY = (int)(resY / windowObject.Height);
                    int x = JobHelper.popupWindows.Count / multipY + 1;
                    int y = JobHelper.popupWindows.Count % multipY;
                    if (y == 0) { y = multipY; x--; }
                    int left = resX - (int)windowObject.Width * x;
                    int top = resY - (int)windowObject.Height * y;
                    if (left > 0)
                    {
                        windowObject.Left = left;
                        windowObject.Top = top;
                        windowObject.ShowActivated = false;
                        window.Show();
                    }
                    else JobHelper.popupWindows.Remove(window);
                }));
        }

        public void ShowDetail(Vacancy v)
        {
            Window window = new DetailWindow();
            window.Owner = this;
            DetailWindow windowObject = window as DetailWindow;
            windowObject.Title = v.Name;
            windowObject.content = v.Content;
            windowObject.answer.Tag = v.Id;
            window.ShowDialog();
        }
        #endregion

        #region События окна
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            CanClose = !config.closeTray;
            timerWork.Tick += new EventHandler(timerWork_Tick);
            timerFl.Tick += new EventHandler(timerFl_Tick);
            timerUpdateResume.Tick += new EventHandler(timerUpdateResume_Tick);
            LoadConfig();
            playButton_Click(this, null);
        }

        void Window_Closing(object sender, EventArgs e)
        {
            removeWindows_Click(this, null);
            config.Save();
        }
        #endregion

        #region Таймеры
        void timerWork_Tick(object sender, EventArgs e)
        {
            if (taskWork.Status != TaskStatus.Running) { taskWork = new Task(() => StartWork()); taskWork.Start(); }
        }

        void timerFl_Tick(object sender, EventArgs e)
        {
            if (taskFl.Status != TaskStatus.Running) { taskFl = new Task(() => StartFl()); taskFl.Start(); }
        }

        void timerUpdateResume_Tick(object sender, EventArgs e)
        {
            if (taskUpdateResume.Status != TaskStatus.Running) { taskUpdateResume = new Task(() => StartUpdateResume()); taskUpdateResume.Start(); }
        }
        #endregion

        #region Панель инструментов
        private void playButton_Click(object sender, RoutedEventArgs e)
        {
            if (!SaveConfig()) return;

            playButton.Visibility = System.Windows.Visibility.Collapsed;
            pauseButton.Visibility = System.Windows.Visibility.Visible;
            stop = false;

            //taskWork = new Task(() => StartWork()); taskWork.Start();
            //timerWork.Interval = TimeSpan.FromSeconds(config.periodWork); timerWork.Start();
            taskFl = new Task(() => StartFl()); taskFl.Start();
            timerFl.Interval = TimeSpan.FromSeconds(config.periodFl); timerFl.Start();
            //taskUpdateResume = new Task(() => StartUpdateResume()); taskUpdateResume.Start();
            //timerUpdateResume.Interval = TimeSpan.FromMinutes(config.periodUpdateResume); timerUpdateResume.Start();

            Log("Запуск");
        }

        private void pauseButton_Click(object sender, RoutedEventArgs e)
        {
            playButton.Visibility = System.Windows.Visibility.Visible;
            pauseButton.Visibility = System.Windows.Visibility.Collapsed;
            stop = true;
            timerWork.Stop();
            Log("Остановлено");
        }

        private void exitButton_Click(object sender, RoutedEventArgs e)
        {
            CanClose = true;
            this.Close();
        }

        private void settingsButton_Click(object sender, RoutedEventArgs e)
        {
            ShowSettings();
        }
        #endregion

        #region События элементов
        private void log_KeyUp(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Delete || e.Key == Key.Back)
            {
                log.Clear();
            }
        }

        private void popup_Checked(object sender, RoutedEventArgs e)
        {
            config.popup = popup.IsChecked.Value;
            config.Save();
        }

        private void workEnabled_Checked(object sender, RoutedEventArgs e)
        {
            config.workEnabled = workEnabled.IsChecked.Value;
            config.Save();
        }

        private void flEnabled_Checked(object sender, RoutedEventArgs e)
        {
            config.flEnabled = flEnabled.IsChecked.Value;
            config.Save();
        }

        private void returnButton_Click(object sender, RoutedEventArgs e)
        {
            var context = NewContext();
            var set = context.Settings.First();
            var v = context.Vacancies.FirstOrDefault(x => x.Id == set.LastVacancy);
            v.Viewed = false;
            context.SaveChanges();
            flVacancies.Add(v);
            RenderFlList();
        }
        #endregion

        #region Work
        private async void saveProfileWork_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(profileWorkCombo.Text)) { MessageBox.Show("Необходимо ввести название профиля", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error); return; }
            var context = NewContext();
            var p = prs.FirstOrDefault(x => x.Name == profileWorkCombo.Text && x.Kind == "work");
            if (p == null)
            {
                p = new Profile { Name = profileWorkCombo.Text, Kind = "work" };
                prs.Add(p);
                SaveConfig();
            }
            p.Search = searchWork.Text;
            p.Answer = (bool)autoAnswerWork.IsChecked;
            p.Salary = Helper.IntParse(salaryWork.Text);
            p.Remote = (bool)remote.IsChecked;
            p.Login = loginHh.Text;
            p.Pass = passHh.Password;
            p.ResumeLinkHh = resumeLinkHh.Text;
            p.Proxy = proxyWork.Text;
            SaveConfig();
            RenderWorkCombo();
            saveProfileWork.Content = "Сохранено";
            await Task.Delay(5000);
            saveProfileWork.Content = "Сохранить профиль";
        }

        private void profileWorkCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            RenderWorkText();
            RenderWorkList();
        }

        private void deleteProfileWork_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("Удалить профиль?", "Вопрос", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                var context = NewContext();
                var p = prs.FirstOrDefault(x => x.Name == profileWorkCombo.Text && x.Kind == "work");
                if (p != null)
                {
                    prs.Remove(p);
                    SaveConfig();
                }
                RenderWorkCombo(true);
            }
        }

        private void workListGrid_MouseEnter(object sender, MouseEventArgs e)
        {
            var grid = sender as Grid;
            grid.Style = (Style)FindResource("gridBackOver");
        }

        private void workListGrid_MouseLeave(object sender, MouseEventArgs e)
        {
            var grid = sender as Grid;
            grid.Style = null;
        }

        public void detailWorkList_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var context = NewContext();
            var v = context.Vacancies.FirstOrDefault(x => x.Id == (int)button.Tag);
            v.Viewed = true;
            var set = context.Settings.First();
            set.LastVacancy = v.Id;
            context.SaveChanges();
            RenderWorkList();
        }

        private void allProfilesWork_Checked(object sender, RoutedEventArgs e)
        {
            RenderWorkList();
        }

        private void removeWindows_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var wins = JobHelper.popupWindows.ToList();
                JobHelper.popupWindows.Clear();
                foreach (var win in wins) win.Close();
            }
            catch { }
        }

        private void removeListWork_Click(object sender, RoutedEventArgs e)
        {
            var context = NewContext();
            var vs = context.Vacancies.Where(x => x.Kind == "work" && !x.Viewed);
            foreach (var v in vs) v.Viewed = true;
            context.SaveChanges();
            RenderWorkList();
        }

        public void answerWorkList_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            //button.IsEnabled = false;
            var context = NewContext();
            var v = context.Vacancies.FirstOrDefault(x => x.Id == (int)button.Tag);
            v.Viewed = true;
            var set = context.Settings.First();
            set.LastVacancy = v.Id;
            context.SaveChanges();
            var p = prs.FirstOrDefault(x => x.Id == v.ProfileId);
            Clipboard.SetText(config.letterWork.Replace("{n}", "\r\n"));
            Task.Factory.StartNew(() =>
                {
                    if (v.Site == "hh") AnswerHh(p, v);
                });
            RenderWorkList();
        }

        private void answerWorkList_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                var button = sender as Button;
                var v = workVacancies.FirstOrDefault(x => x.Id == (int)button.Tag);
                if (v.Answered) button.Visibility = System.Windows.Visibility.Collapsed;
            }
            catch { }
        }

        private void workListGrid_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            
        }
        #endregion

        #region Fl
        private async void saveProfileFl_Click(object sender, RoutedEventArgs e)
        {
            saveProfileFl.Content = "Сохраняется...";
            if (string.IsNullOrEmpty(profileFlCombo.Text)) { MessageBox.Show("Необходимо ввести название профиля", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error); return; }
            var context = NewContext();
            var p = prs.FirstOrDefault(x => x.Name == profileFlCombo.Text && x.Kind == "fl");
            if (p == null)
            {
                p = new Profile { Name = profileFlCombo.Text, Kind = "fl" };
                prs.Add(p);
                SaveConfig();
            }
            p.Search = searchFl.Text;
            p.Answer = (bool)autoAnswerFl.IsChecked;
            p.Remote = (bool)businessFl.IsChecked;
            p.Proxy = proxyFl.Text;
            p.Login = loginFreelance.Text;
            p.Pass = passFreelance.Password;
            p.LoginFlRu = loginFlRu.Text;
            p.PassFlRu = passFlRu.Password;
            p.freelanceru = (bool)freelanceru.IsChecked;
            p.flru = (bool)flru.IsChecked;
            SaveConfig();
            RenderFlCombo();
            saveProfileFl.Content = "Сохранено";
            await Task.Delay(5000);
            saveProfileFl.Content = "Сохранить профиль";
        }

        private void profileFlCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            RenderFlText();
            RenderFlList();
        }

        private void deleteProfileFl_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("Удалить профиль?", "Вопрос", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                var context = NewContext();
                var p = prs.FirstOrDefault(x => x.Name == profileFlCombo.Text && x.Kind == "fl");
                if (p != null)
                {
                    prs.Remove(p);
                    SaveConfig();
                }
                RenderFlCombo(true);
            }
        }

        private void commaFl_Loaded(object sender, RoutedEventArgs e)
        {
            var text = sender as TextBlock;
            var v = flVacancies.FirstOrDefault(x => x.Id == (int)text.Tag);
            if (string.IsNullOrEmpty(v.Company)) text.Visibility = System.Windows.Visibility.Hidden;
        }

        private void flListGrid_MouseEnter(object sender, MouseEventArgs e)
        {
            var grid = sender as Grid;
            grid.Style = (Style)FindResource("gridBackOver");
        }

        private void flListGrid_MouseLeave(object sender, MouseEventArgs e)
        {
            var grid = sender as Grid;
            grid.Style = null;
        }

        public void detailFlList_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            int id = (int)button.Tag;
            var context = NewContext();
            var v = context.Vacancies.FirstOrDefault(x => x.Id == id);
            v.Viewed = true;
            var set = context.Settings.First();
            set.LastVacancy = v.Id;
            context.SaveChanges();
            flVacancies.RemoveAll(x => x.Id == id);
            RenderFlList();
        }

        private void allProfilesFl_Checked(object sender, RoutedEventArgs e)
        {
            //RenderFlList();
        }

        private void removeListFl_Click(object sender, RoutedEventArgs e)
        {
            var context = NewContext();
            var vs = context.Vacancies.Where(x => x.Kind == "fl" && !x.Viewed);
            foreach (var v in vs) { v.Viewed = true; flVacancies.RemoveAll(x => x.Id == v.Id); }
            context.SaveChanges();
            RenderFlList();
        }

        public void answerFlList_Click(object sender, RoutedEventArgs e)
        {
            try { Clipboard.SetText(config.letterFl.Replace("{n}", "\r\n")); } catch { }
            var button = sender as Button;
            int id = (int)button.Tag;
            //button.IsEnabled = false;
            var context = NewContext();
            var v = context.Vacancies.FirstOrDefault(x => x.Id == id);
            v.Viewed = true;
            var set = context.Settings.First();
            set.LastVacancy = v.Id;
            context.SaveChanges();
            flVacancies.RemoveAll(x => x.Id == id);
            var p = prs.FirstOrDefault(x => x.Id == v.ProfileId);
            Task.Factory.StartNew(() =>
            {
                if (v.Site == "freelance") AnswerFreelance(p, v);
                if (v.Site == "flru") AnswerFlRu(p, v);
            });
            RenderFlList();
        }

        private void answerFlList_Loaded(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var v = flVacancies.FirstOrDefault(x => x.Id == (int)button.Tag);
            if (v.Answered) button.Visibility = System.Windows.Visibility.Collapsed;
        }

        private void flListGrid_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            
        }

        private void dateFlList_Loaded(object sender, RoutedEventArgs e)
        {
            var t = sender as TextBlock;
            var v = flVacancies.First(x => x.Id == (int)t.Tag);
            t.Text = Helper.TimeAgo(v.Date, false);
        }

        private void DateOld_Checked(object sender, RoutedEventArgs e)
        {
            var el = sender as RadioButton;
            config.dateSort = el.Name;
            config.Save();
            RenderFlList();
        }
        #endregion

        #region Разное
        void Log(string s, params object[] args)
        {
            if (args.Length > 0) s = string.Format(s, args);
            s = DateTime.Now.ToString("G") + "=>  " + s + "\r\n";
            Dispatcher.BeginInvoke(new Action(() =>
            {
                log.AppendText(s);
                log.ScrollToEnd();
            }));
        }

        public void LoadConfig()
        {
            prs = Deserialize<List<Profile>>("prs");
            if (prs == null) prs = new List<Profile>();

            var dc = NewContext();
            var d = DateTime.Now.AddDays(-7);
            var recs = dc.Vacancies.Where(x => x.Date < d);
            dc.Vacancies.RemoveRange(recs);
            dc.SaveChanges();
            flVacancies = dc.Vacancies.Where(x => x.Kind == "fl" && !x.Viewed).ToList();
            flList.ItemsSource = flVacancies;
            workList.ItemsSource = workVacancies;
            popup.IsChecked = config.popup;
            workEnabled.IsChecked = config.workEnabled;
            flEnabled.IsChecked = config.flEnabled;
            //RenderWorkCombo(true);
            //RenderWorkList();
            RenderFlCombo(true);
            RenderFlList();
            if (config.dateSort == "dateOld") dateOld.IsChecked = true; else dateNew.IsChecked = true;

            var context = NewContext();
            var set = context.Settings.FirstOrDefault();
            if (set == null)
            {
                context.Settings.Add(new Setting());
                context.SaveChanges();
            }
        }

        public bool SaveConfig()
        {
            Serialize(prs, "prs");

            config.Save();
            return true;
        }

        ParserWc NewParser(Profile p)
        {
            var parser = new ParserWc(p.Proxy, 5000, p.Login + ".ck");
            //parser.Fiddler = true;
            return parser;
        }

        void HeadersMain(ParserWc parser)
        {
            parser.ClearHeaders();
            parser.AddHeader("Sec-Fetch-Dest: document");
            parser.AddHeader("Sec-Fetch-Mode: navigate");
            parser.AddHeader("Sec-Fetch-Site: same-origin");
            parser.AddHeader("Sec-Fetch-User: ?1");
            parser.AddHeader("Upgrade-Insecure-Requests: 1");
        }

        void HeadersIndex(ParserWc parser)
        {
            parser.ClearHeaders();
            parser.AddHeader("X-CSRF-Token", xcsrftoken);
            parser.AddHeader("Sec-Fetch-Dest: empty");
            parser.AddHeader("Sec-Fetch-Mode: cors");
            parser.AddHeader("Sec-Fetch-Site: same-origin");
        }

        void HeadersPost(ParserWc parser)
        {
            parser.ClearHeaders();
            parser.AddHeader("Origin", "https://id.freelance.ru");
            parser.AddHeader("sec-ch-ua: \"Chromium\";v=\"146\", \"Not-A.Brand\";v=\"24\", \"YaBrowser\";v=\"26.4\", \"Yowser\";v=\"2.5\"");
            parser.AddHeader("sec-ch-ua-mobile: ?0");
            parser.AddHeader("sec-ch-ua-platform: \"Windows\"");
            parser.AddHeader("Sec-Fetch-Dest: empty");
            parser.AddHeader("Sec-Fetch-Mode: cors");
            parser.AddHeader("Sec-Fetch-Site: same-origin");
        }

        public DataContext NewContext()
        {
            return new DataContext();
        }

        public async void Serialize(object o, string fileName)
        {
            while (true)
            {
                try
                {
                    string s = JsonConvert.SerializeObject(o);
                    File.WriteAllText(string.Format("{0}{1}.json", Helper.PathCurrent, fileName), s, Encoding.UTF8);
                    break;
                }
                catch { await Task.Delay(100); }
            }
        }

        T Deserialize<T>(string fileName)
        {
            T r = default(T);

            string path = string.Format("{0}{1}.json", Helper.PathCurrent, fileName);
            if (File.Exists(path))
            {
                string s = File.ReadAllText(path, Encoding.UTF8);
                r = JsonConvert.DeserializeObject<T>(s);
            }

            return r;
        }
        #endregion

        #region Render
        void RenderWorkCombo(bool first = false)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                int index = profileWorkCombo.SelectedIndex;
                profileWorkCombo.ItemsSource = null;
                profileWorkCombo.ItemsSource = prs.Where(x => x.Kind == "work").ToList();
                profileWorkCombo.DisplayMemberPath = "Name";
                if (!first) profileWorkCombo.SelectedIndex = index;
                if (first && profileWorkCombo.Items.Count > 0) profileWorkCombo.SelectedIndex = 0;
            }));
        }

        void RenderWorkText()
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (profileWorkCombo.SelectedIndex > -1)
                {
                    var p = (Profile)profileWorkCombo.SelectedItem;
                    searchWork.Text = p.Search;
                    autoAnswerWork.IsChecked = p.Answer;
                    salaryWork.Text = p.Salary.ToString();
                    remote.IsChecked = p.Remote;
                    loginHh.Text = p.Login;
                    passHh.Password = p.Pass;
                    resumeLinkHh.Text = p.ResumeLinkHh;
                    proxyWork.Text = p.Proxy;
                }
            }));
        }

        void RenderWorkList()
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                try
                {
                    var context = NewContext();
                    if ((bool)allProfilesWork.IsChecked)
                    {
                        workVacancies = context.Vacancies.Where(x => x.Kind == "work" && !x.Viewed).ToList();
                    }
                    else if (profileWorkCombo.SelectedIndex > -1)
                    {
                        var p = (Profile)profileWorkCombo.SelectedItem;
                        workVacancies = context.Vacancies.Where(x => x.ProfileId == p.Id && !x.Viewed).ToList();
                    }
                    countWork.Text = workVacancies.Count.ToString();
                    workList.Items.Refresh();
                }
                catch { }
            }));
        }

        void RenderFlCombo(bool first = false)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                int index = profileFlCombo.SelectedIndex;
                profileFlCombo.ItemsSource = null;
                profileFlCombo.ItemsSource = prs.Where(x => x.Kind == "fl").ToList();
                profileFlCombo.DisplayMemberPath = "Name";
                if (!first) profileFlCombo.SelectedIndex = index;
                if (first && profileFlCombo.Items.Count > 0) profileFlCombo.SelectedIndex = 0;
            }));
        }

        void RenderFlText()
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (profileFlCombo.SelectedIndex > -1)
                {
                    var p = (Profile)profileFlCombo.SelectedItem;
                    searchFl.Text = p.Search;
                    autoAnswerFl.IsChecked = p.Answer;
                    businessFl.IsChecked = p.Remote;
                    proxyFl.Text = p.Proxy;
                    loginFreelance.Text = p.Login;
                    passFreelance.Password = p.Pass;
                    loginFlRu.Text = p.LoginFlRu;
                    passFlRu.Password = p.PassFlRu;
                    freelanceru.IsChecked = p.freelanceru;
                    flru.IsChecked = p.flru;
                }
            }));
        }

        void RenderFlList()
        {
            /*var context = NewContext();
            if ((bool)allProfilesFl.IsChecked)
            {
                flVacancies = context.Vacancies.Where(x => x.Kind == "fl" && !x.Viewed).OrderByDescending(x => x.Date).ToList();
            }
            else if (profileFlCombo.SelectedIndex > -1)
            {
                var p = (Profile)profileFlCombo.SelectedItem;
                flVacancies = context.Vacancies.Where(x => x.ProfileId == p.Id && !x.Viewed).OrderByDescending(x => x.Date).ToList();
            }*/
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (config.dateSort == "dateOld") flVacancies.Sort((x, y) => x.Date.CompareTo(y.Date)); else flVacancies.Sort((x, y) => y.Date.CompareTo(x.Date));
                countFl.Text = flVacancies.Count.ToString();
                flList.Items.Refresh();
            }));
        }
        #endregion

        #region Парсинг
        void StartWork()
        {
            if (config.workEnabled)
            {
                var context = NewContext();
                var ps = prs.Where(x => x.Kind == "work");
                foreach (var p in ps)
                {
                    ParseHh(p);
                }
                Dispatcher.BeginInvoke(new Action(() => { updateWork.Text = "Последнее обновление: " + DateTime.Now.ToString("G"); }));
            }
        }

        void StartFl()
        {
            if (config.flEnabled)
            {
                //Avito();
                var context = NewContext();
                var ps = prs.Where(x => x.Kind == "fl");
                foreach (var p in ps)
                {
                    if (p.freelanceru) ParseFreelance(p);
                    if (p.flru) ParseFlRu(p);
                }
                Dispatcher.BeginInvoke(new Action(() => { updateFl.Text = "Последнее обновление: " + DateTime.Now.ToString("G"); }));
            }
        }

        void StartUpdateResume()
        {
            if (config.workEnabled)
            {
                var context = NewContext();
                var ps = prs.Where(x => x.Kind == "work");
                foreach (var p in ps)
                {
                    List<Task> tasks = new List<Task>();
                    tasks.Add(Task.Factory.StartNew(() => UpdateResumeHh(p)));
                    Task.WaitAll(tasks.ToArray());
                }
            }
        }

        void Avito()
        {
            if (sw == null || sw.Elapsed.TotalHours > 1)
            {
                sw = Stopwatch.StartNew();
                Task.Factory.StartNew(async () =>
                {
                    try
                    {
                        await Dispatcher.BeginInvoke(new Action(() => { avito.Text = ""; }));
                        var p = new ParserWc();
                        p.Fiddler = true;
                        p.Go("https://www.avito.ru/krasnodarskiy_kray_yuzhnyy/kvartiry/sdam/na_dlitelnyy_srok-ASgBAgICAkSSA8gQ8AeQUg?s=104");
                        if (!string.IsNullOrEmpty(p.Error)) throw new Exception(p.Error);
                        var aa = p.SelectNodes("//div[@data-marker='catalog-serp']//a[@data-marker='item-title']");
                        if (aa != null)
                        {
                            var db = NewContext();
                            foreach (var a in aa)
                            {
                                string href = "https://www.avito.ru" + a.GetAttributeValue("href", "");
                                var rec = db.Vacancies.FirstOrDefault(x => x.Url == href);
                                if (rec == null)
                                {
                                    string body = $"Новое объявление:<br><br>{href}";
                                    await Mail("alexxxproof@gmail.com", "Avito", body);
                                    db.Vacancies.Add(new Vacancy { Url = href, ProfileId = 1, Date = DateTime.Now });
                                    db.SaveChanges();
                                }
                            }
                        }
                        await Dispatcher.BeginInvoke(new Action(() => { avito.Text = aa?.Count.ToString(); }));
                    }
                    catch (Exception ex) { await Dispatcher.BeginInvoke(new Action(() => { avito.Text = ex.Message; })); }
                });
            }
        }

        public static async Task Mail(string email, string subject, string message, List<string> attachments = null)
        {
            try
            {
                var emailMessage = new MimeMessage();

                emailMessage.From.Add(new MailboxAddress(typeof(DataContext).Namespace, "alexxx6233@yandex.ru"));
                emailMessage.To.Add(new MailboxAddress("", email));
                emailMessage.Subject = subject;

                var builder = new BodyBuilder();
                builder.HtmlBody = message;
                if (attachments != null) foreach (var a in attachments) builder.Attachments.Add(a);
                emailMessage.Body = builder.ToMessageBody();

                using (var client = new SmtpClient())
                {
                    await client.ConnectAsync("smtp.yandex.ru", 587, false);
                    await client.AuthenticateAsync("alexxx6233@yandex.ru", "Aqwsxz10");
                    await client.SendAsync(emailMessage);

                    await client.DisconnectAsync(true);
                }
            }
            catch (Exception ex) { Helper.Log(ex); }
        }
        #endregion

        #region hh
        bool AuthHh(Profile p)
        {
            bool result = false;

            try
            {
                var parser = NewParser(p);
                parser.Go("http://" + RedirectHost(parser) + "/", false);
                result = parser.Content.Contains("mainmenu_userName");
                if (!result)
                {
                    Log("{0} => Авторизация...", p.Login);
                    string _xsrf = parser.GetCookie("hh.ru", "_xsrf");
                    parser.Post("https://" + RedirectHost(parser) + "/account/login", string.Format("backUrl==%2F&failUrl=%2Faccount%2Flogin&username={0}&password={1}&remember=yes&_xsrf={2}", p.Login, p.Pass, _xsrf));
                    result = parser.Content.Contains("mainmenu_userName");
                    if (!result) Log("{0} => Ошибка авторизации", p.Login);
                    else Log("{0} => Авторизация... OK", p.Login);
                }
                if (result && !string.IsNullOrEmpty(p.ResumeLinkHh) && string.IsNullOrEmpty(p.ResumeIdHh))
                {
                    parser.Go(p.ResumeLinkHh);
                    var context = NewContext();
                    p.ResumeIdHh = parser.RegexMatch(@"'resumeId' : (\d+)").Groups[1].Value;
                    var p1 = prs.First(x => x.Id == p.Id);
                    p1.ResumeIdHh = p.ResumeIdHh;
                    context.SaveChanges(); SaveConfig();
                }
            }
            catch (Exception ex) { Log(ex.ToString()); }

            return result;
        }

        void ParseHh(Profile p)
        {
            try
            {
                if (stop) return;
                if (AuthHh(p))
                {
                    int viewed = 0;
                    var parser = NewParser(p);
                    int page = 0;
                    bool br = false;
                    while (true)
                    {
                        if (br) break;
                        string uri = string.Format("http://" + RedirectHost(parser) + "/search/vacancy?items_on_page=500&text={0}&page={1}&order_by=publication_time&search_period=7", p.Search.Replace(" ", "+").Replace("#", "%23"), page);
                        if (p.Remote) uri += "&schedule=remote";
                        if (stop) return;
                        parser.Go(uri);
                        var trs = parser.SelectNodes("//div[starts-with(@class, 'search-result-item') and @data-qa='vacancy-serp__vacancy']");
                        if (trs == null) break;
                        foreach (var tr in trs)
                        {
                            try
                            {
                                var a = tr.SelectSingleNode(".//div[@class='search-result-item__head']//a");
                                if (a != null)
                                {
                                    Thread.Sleep(100);
                                    string url = a.GetAttributeValue("href", "");
                                    var context = NewContext();
                                    var v = context.Vacancies.FirstOrDefault(x => x.ProfileId == p.Id && x.Url == url);
                                    if (v == null)
                                    {
                                        var salaryNode = tr.SelectSingleNode(".//div[@class='b-vacancy-list-salary']");
                                        string salary = salaryNode != null ? salaryNode.InnerText.Trim() : "Не указана";
                                        salary = Parser.ClearTags(salary);
                                        int salaryInt = Helper.IntParse(salary);
                                        if (salaryInt < p.Salary) { context.Vacancies.Add(new Vacancy { ProfileId = p.Id, Url = url, Viewed = true, Answered = true, Date = DateTime.Now }); context.SaveChanges(); continue; }
                                        string name = a.InnerText.Trim();
                                        string date = tr.SelectSingleNode(".//span[@class='b-vacancy-list-date']").InnerText.Trim();
                                        string city = tr.SelectSingleNode(".//span[@class='searchresult__address']").InnerText.Trim();
                                        if (city == "Киев") continue;
                                        string company = tr.SelectSingleNode(".//a[@data-qa='vacancy-serp__vacancy-employer']").InnerText.Trim();
                                        if (string.IsNullOrEmpty(company)) company = Regex.Match(tr.SelectSingleNode(".//div[@class='searchresult__placetime']").InnerText.Trim(), @"[^,]+$").Value.Trim();
                                        if (stop) return;
                                        //parser.Go(url);
                                        var divs = tr.SelectNodes(".//div[@class='search-result-item__snippet']").Select(x => x.InnerText).ToList();
                                        string content = string.Join("\r\n", divs);
                                        var v1 = context.Vacancies.FirstOrDefault(x => x.ProfileId == p.Id && x.Name == name && x.Content == content && x.Salary == salary && x.DateS == date);
                                        if (v1 != null) { context.Vacancies.Add(new Vacancy { ProfileId = p.Id, Url = url, Viewed = true, Answered = true, Date = DateTime.Now }); context.SaveChanges(); continue; }
                                        /*parser.SelectSingleNode("//div[contains(@class, 'b-vacancy-desc')]").InnerText.Trim();
                                        content = Regex.Replace(content, @"</\w+>", "\r\n");
                                        content = parser.ClearTags(content);
                                        content = content.Replace("\r\n:", ":");*/
                                        string employmentType = "";// parser.SelectSingleNode("//div[contains(@class, 'b-vacancy-employmentmode')]//div").InnerText.Trim();
                                        string vacancyId = Regex.Match(url, @"\d+").Value;
                                        var d = new DateTime();
                                        DateTime.TryParse(date, out d);
                                        v = new Vacancy
                                        {
                                            ProfileId = p.Id,
                                            Kind = "work",
                                            Site = "hh",
                                            ProfileName = p.Name,
                                            Url = url,
                                            Name = name,
                                            DateS = date,
                                            Date = d,
                                            City = city,
                                            Company = company,
                                            Salary = salary,
                                            Viewed = false,
                                            Answered = false,
                                            Content = content,
                                            VacancyId = vacancyId
                                        };
                                        if (stop) return;
                                        context.Vacancies.Add(v);
                                        context.SaveChanges();
                                        Thread.Sleep(1000);
                                        if (p.Answer) AnswerHh(p, v);
                                        RenderWorkList();
                                        if (config.popup) ShowPopup(v);
                                    }
                                    else
                                    {
                                        //if (!tr.GetAttributeValue("data-qa", "").Contains("vacancy-serp__vacancy_premium")) { br = true; break; }
                                    }
                                    Dispatcher.BeginInvoke(new Action(() => { viewedWork.Text = "Просмотрено: " + (++viewed); }));
                                }
                            }
                            catch (Exception ex) { Log(ex.ToString()); }
                        }
                        page++;
                    }
                }
            }
            catch (Exception ex) { Log(ex.ToString()); }
        }

        void AnswerHh(Profile p, Vacancy v)
        {
            /*try
            {
                var parser = NewParser(p.Login);
                string uri = string.Format("http://" + RedirectHost(parser) + "/applicant/vacancy_response/popup?vacancyId={0}&autoOpen=no&isTest=no&withoutTest=no", v.VacancyId);
                parser.Go(uri, false);
                string _xsrf = parser.GetCookie("hh.ru", "_xsrf");
                var data = new NameValueCollection();
                data.Add("vacancy_id", v.VacancyId);
                data.Add("resume_id", p.ResumeIdHh);
                data.Add("letter", config.letterWork.Replace("{n}", "\r\n"));
                data.Add("ignore_postponed", "true");
                data.Add("_xsrf", _xsrf);
                parser.Headers.Add("X-Request-ID", _xsrf);
                parser.Referer = uri;
                parser.Post("http://" + RedirectHost(parser) + "/applicant/vacancy_response/popup", data);
                var j = parser.Json();
                if (j["success"] != null && (bool)j["success"] == true)
                {
                    v.Answered = true;
                    context.SaveChanges();
                }
                RenderWorkList();
            }
            catch { }*/
            try { Process.Start(v.Url); } catch { }
        }

        void UpdateResumeHh(Profile p)
        {
            try
            {
                if (AuthHh(p))
                {
                    var parser = NewParser(p);
                    parser.Proxy = p.Proxy;
                    parser.Go(p.ResumeLinkHh);
                    var span = parser.SelectSingleNode("//span[contains(@class, 'HH-Resume-Touch-Button')]");
                    if (!span.GetAttributeValue("class", "").Contains("g-hidden"))
                    {
                        Process.Start(p.ResumeLinkHh);
                        /*string resume = Regex.Match(p.ResumeLinkHh, @"resume/([^/]+)").Groups[1].Value;
                        var data = new NameValueCollection();
                        data.Add("resume", resume);
                        data.Add("undirectable", "true");
                        var input = parser.SelectSingleNode("//input[@name='_xsrf']");
                        parser.AddHeader("X-Xsrftoken", input.GetAttributeValue("value", ""));
                        parser.AddHeader("X-Requested-With", "XMLHttpRequest");
                        parser.AddHeader("Origin", "https://rostov.hh.ru");
                        parser.Language = "en-US,en;q=0.8";
                        parser.Referer = parser.History.Last();
                        parser.Post("http://" + RedirectHost(parser) + "/applicant/resumes/touch", data);*/
                    }
                }
            }
            catch (Exception ex) { Log(ex.ToString()); }
        }

        string RedirectHost(ParserWc parser)
        {
            string r = "hh.ru";

            string redirect_host = parser.GetCookie("hh.ru", "redirect_host");
            if (!string.IsNullOrEmpty(redirect_host)) r = redirect_host;

            return r;
        }
        #endregion

        #region freelance
        bool AuthFreelance(Profile p)
        {
            bool result = false;

            try
            {
                var parser = NewParser(p);
                HeadersMain(parser);
                parser.Go("https://freelance.ru/");
                xcsrftoken = parser.SelectSingleNode("//meta[@name='csrf-token']").GetAttributeValue("content", "");
                HeadersIndex(parser);
                parser.Go("https://freelance.ru/lib/top-menu/index");
                result = parser.Contains(p.Login);
                if (!result)
                {
                    HeadersMain(parser);
                    parser.Go("https://freelance.ru/auth/login");
                    HeadersPost(parser);
                    parser.Post("https://id.freelance.ru/api/auth/login", $"{{\"identifier\":\"{p.Login}\",\"password\":\"{p.Pass}\"}}");
                    HeadersIndex(parser);
                    parser.Go("https://freelance.ru/lib/top-menu/index");
                    result = parser.Contains(p.Login);
                    if (!result) Log("{0} => Ошибка авторизации", p.Login);
                }
            }
            catch (Exception ex) { Log(ex.ToString()); }

            return result;
        }

        void ParseFreelance(Profile p)
        {
            try
            {
                var en = Encoding.GetEncoding(1251);
                if (stop) return;
                if (AuthFreelance(p))
                {
                    int viewed = 0;
                    var parser = NewParser(p);
                    parser.Referer = "https://freelance.ru/";
                    int page = 1;
                    while (true)
                    {
                        bool br = true;
                        HeadersMain(parser);
                        parser.Go("https://freelance.ru/task?q=&a=1&v=1&c%5B0%5D=116&c%5B1%5D=724&c%5B2%5D=4&page=" + page);
                        var divs = parser.SelectNodes("//div[@class='task-feed-list']/article");
                        if (divs == null || divs.Count == 0) break;
                        foreach (var div in divs)
                        {
                            try
                            {
                                var aProj = div.SelectSingleNode(".//a[@class='task-card__title-link']");
                                if (aProj != null)
                                {
                                    Thread.Sleep(100);
                                    string url = "https://freelance.ru" + aProj.GetAttributeValue("href", "");
                                    //string messages = div.SelectSingleNode(".//span[@title='Отклики']")?.InnerText?.Trim()?.Replace("&nbsp;", "");
                                    var context = NewContext();
                                    var v = context.Vacancies.FirstOrDefault(x => x.ProfileId == p.Id && x.Url == url);
                                    if (v == null)
                                    {
                                        string salary = div.SelectSingleNode(".//div[@class='task-card__budget']/span[1]")?.InnerText?.Trim();
                                        if (salary == null) salary = "";
                                        int salary_ = Helper.IntParse(salary);
                                        if (salary_ > 0 && salary_ < 10000) continue;
                                        string name = aProj.InnerText.Replace("&quot;", "\"").Trim();
                                        string date = div.SelectSingleNode(".//span[@class='task-card__foot-item']").GetAttributeValue("title", "").Trim();
                                        //date = Regex.Match(date, @"\d+\-\d+-\d+T\d+:\d+").Value;
                                        var d = new DateTime();
                                        DateTime.TryParse(date, out d);
                                        var delta = DateTime.Now - d;
                                        if (delta.TotalDays > 2) continue;

                                        var sb = new StringBuilder();
                                        string content = div.SelectSingleNode(".//p[@class='task-card__desc']")?.InnerText?.Trim();

                                        if (!string.IsNullOrEmpty(p.Search))
                                        {
                                            var ors = Regex.Split(p.Search.ToLower(), ",");
                                            bool isOr = false;
                                            foreach (string or in ors)
                                            {
                                                string or1 = or.Trim();
                                                if (content.ToLower().Contains(or1) || name.ToLower().Contains(or1)) { isOr = true; break; }
                                            }
                                            if (isOr) { context.Vacancies.Add(new Vacancy { ProfileId = p.Id, Url = url, Viewed = true, Answered = true, Date = DateTime.Now }); context.SaveChanges(); continue; }
                                        }

                                        string vacancyId = Regex.Match(url, @"\d+").Value;
                                        v = new Vacancy
                                        {
                                            ProfileId = p.Id,
                                            Kind = "fl",
                                            Site = "freelance.ru",
                                            ProfileName = p.Name,
                                            Url = url,
                                            Name = name,
                                            DateS = date,
                                            Date = d,
                                            //City = messages,
                                            //Company = businessAccount ? "Бизнес-аккаунт" : "",
                                            Salary = salary,
                                            Viewed = false,
                                            Answered = false,
                                            Content = content,
                                            VacancyId = vacancyId
                                        };
                                        if (stop) return;
                                        context.Vacancies.Add(v);
                                        context.SaveChanges();
                                        Thread.Sleep(1000);
                                        flVacancies.Add(v);
                                        RenderFlList();
                                        if (config.popup) ShowPopup(v);
                                    }
                                    /*else
                                    {
                                        //if (!div.GetAttributeValue("class", "").Contains("prio")) { br = true; break; }
                                        if (!v.Viewed)
                                        {
                                            v.City = messages;
                                            context.SaveChanges();
                                            RenderFlList();
                                        }
                                    }*/
                                    Dispatcher.BeginInvoke(new Action(() => { viewedFl.Text = "Просмотрено: " + (++viewed); }));
                                    br = false;
                                }
                            }
                            catch (Exception ex) { Log(ex.ToString()); }
                        }
                        if (br) break;
                        page++;
                    }
                }
                RenderFlList();
            }
            catch (Exception ex) { Log(ex.ToString()); }
        }

        void AnswerFreelance(Profile p, Vacancy v)
        {
            try { Process.Start(v.Url); } catch { }
        }

        bool AuthFlRu(Profile p)
        {
            bool result = false;

            try
            {
                var parser = NewParser(p);
                HeadersMain(parser);
                parser.Go("https://www.fl.ru/");
                result = parser.Contains("Выйти из аккаунта");
            }
            catch (Exception ex) { Log(ex.ToString()); }

            return result;
        }

        void ParseFlRu(Profile p)
        {
            try
            {
                if (stop) return;
                if (AuthFlRu(p))
                {
                    int viewed = 0;
                    var parser = NewParser(p);
                    parser.Referer = "https://www.fl.ru/";
                    int page = 1;
                    while (true)
                    {
                        bool br = true;
                        HeadersMain(parser);
                        parser.Go($"https://www.fl.ru/projects/page-{page}/");
                        var divs = parser.SelectNodes("//div[@id='projects-list']/div");
                        if (divs == null || divs.Count == 0) break;
                        foreach (var div in divs)
                        {
                            try
                            {
                                var aProj = div.SelectSingleNode(".//h2/a");
                                if (aProj != null)
                                {
                                    Thread.Sleep(100);
                                    string url = aProj.GetAttributeValue("href", "");
                                    if (string.IsNullOrEmpty(url) || !url.StartsWith("http")) url = "https://www.fl.ru" + url;
                                    var context = NewContext();
                                    var v = context.Vacancies.FirstOrDefault(x => x.ProfileId == p.Id && x.Url == url);
                                    if (v == null)
                                    {
                                        var salaryNode = div.SelectSingleNode(".//div[contains(@class, 'b-post__price')]");
                                        string salary = salaryNode != null ? salaryNode.InnerText.Replace("&nbsp;", " ").Trim() : "Не указана";
                                        int salary_ = Helper.IntParse(salary);
                                        if (salary_ > 0 && salary_ < 10000) continue;
                                        string name = aProj.InnerText.Replace("&quot;", "\"").Trim();
                                        var dateNode = div.SelectSingleNode(".//div[@class='b-post__txt b-post__txt_fontsize_11']/span[2]");
                                        string date = dateNode != null ? dateNode.InnerText.Trim() : DateTime.Now.ToString();
                                        var d = RussianRelativeDateParser.Parse(date);
                                        var delta = DateTime.Now - d;
                                        if (delta.TotalDays > 2) continue;
                                        var descrNode = div.SelectSingleNode(".//div[contains(@class, 'b-post__grid_descript')]");
                                        string descr = descrNode != null ? descrNode.InnerText.Replace("&#8230;", "...").Replace("&quot;", "\"").Trim() : "";
                                        if (stop) return;
                                        string content = descr;

                                        if (!string.IsNullOrEmpty(p.Search))
                                        {
                                            var ors = Regex.Split(p.Search.ToLower(), ",");
                                            bool isOr = false;
                                            foreach (string or in ors)
                                            {
                                                string or1 = or.Trim();
                                                if (descr.ToLower().Contains(or1) || content.ToLower().Contains(or1) || name.ToLower().Contains(or1)) { isOr = true; break; }
                                            }
                                            if (isOr) { context.Vacancies.Add(new Vacancy { ProfileId = p.Id, Url = url, Viewed = true, Answered = true, Date = DateTime.Now }); context.SaveChanges(); continue; }
                                        }

                                        string vacancyId = Regex.Match(url, @"\d+").Value;
                                        v = new Vacancy
                                        {
                                            ProfileId = p.Id,
                                            Kind = "fl",
                                            Site = "fl.ru",
                                            ProfileName = p.Name,
                                            Url = url,
                                            Name = name,
                                            DateS = date,
                                            Date = d,
                                            Salary = salary,
                                            Viewed = false,
                                            Answered = false,
                                            Content = content,
                                            VacancyId = vacancyId
                                        };
                                        if (stop) return;
                                        context.Vacancies.Add(v);
                                        context.SaveChanges();
                                        Thread.Sleep(1000);
                                        flVacancies.Add(v);
                                        RenderFlList();
                                        if (config.popup) ShowPopup(v);
                                    }
                                    Dispatcher.BeginInvoke(new Action(() => { viewedFl.Text = "Просмотрено: " + (++viewed); }));
                                    br = false;
                                }
                            }
                            catch (Exception ex) { Log(ex.ToString()); }
                        }
                        if (br) break;
                        page++;
                    }
                }
                RenderFlList();
            }
            catch (Exception ex) { Log(ex.ToString()); }
        }

        void AnswerFlRu(Profile p, Vacancy v)
        {
            try { Process.Start(v.Url); } catch { }
        }
        #endregion
    }
}