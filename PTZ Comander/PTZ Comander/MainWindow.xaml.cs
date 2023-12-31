using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
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
using System.Windows.Navigation;
using System.Windows.Shapes;
using Newtonsoft.Json;

namespace PTZ_Comander
{
    

    public partial class MainWindow : Window
    {
        private List<TabItem> _tabItems;
        private TabItem _tabAdd;
        List<Settings> settings;
        int controllerNR = 0;
        object tally;

        public MainWindow()
        {
            try
            {
                InitializeComponent();
   
                LoadJson();
                // initialize tabItem array
                _tabItems = new List<TabItem>();
 
                // add a tabItem with + in header 
                _tabAdd = new TabItem();
                _tabAdd.Header = "+";

                // tabAdd.MouseLeftButtonUp += new MouseButtonEventHandler(tabAdd_MouseLeftButtonUp);
  
                _tabItems.Add(_tabAdd);
   
                // add first tab
                this.AddTabItem();
  
                // bind tab control
                tabDynamic.DataContext = _tabItems;
  
                tabDynamic.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        ~MainWindow()
        {
            SaveJson();
        }


        private TabItem AddTabItem()
        {
            int count = _tabItems.Count;
            int insertCount = count;
   
        // create new tab item
            TabItem tab = new TabItem();
    

            tab.Header = string.Format("Controller {0}", count-1);
            tab.Name = string.Format("tab{0}", count);  // hier werden doppelt names erstellt das ist das problem ich weiß noch nicht wir ich es fix 
            tab.HeaderTemplate = tabDynamic.FindResource("TabHeader") as DataTemplate;
            tab.ContentTemplate = tabDynamic.FindResource("TabContend") as DataTemplate;

            if(count == 1)
            {
                tab.Header = "Meune";
                tab.Name = string.Format("tab{0}", count);  // hier werden doppelt names erstellt das ist das problem ich weiß noch nicht wir ich es fix 
                tab.HeaderTemplate = tabDynamic.FindResource("TabHeaderUdel") as DataTemplate;
                tab.ContentTemplate = tabDynamic.FindResource("TabMenue") as DataTemplate;
            }
            
            //tab.MouseDoubleClick += new MouseButtonEventHandler(tab_MouseDoubleClick);

            // add controls to tab item, this case I added just a textbox
            TextBox txt = new TextBox();
            txt.Name = "txt";
    
            tab.Content = txt;
    
               // insert tab item right before the last (+) tab item
            _tabItems.Insert(insertCount - 1, tab);

            //tally = tab.FindName("tally");

            return tab;
        }
        
        /*
        private void tabAdd_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
        // clear tab control binding
            tabDynamic.DataContext = null;

            TabItem tab = this.AddTabItem();
    
        // bind tab control
            tabDynamic.DataContext = _tabItems;

        // select newly added tab item
            tabDynamic.SelectedItem = tab;
        }

           private void tab_MouseDoubleClick(object sender, MouseButtonEventArgs e)
           {
               TabItem tab = sender as TabItem;
  
               TabProperty dlg = new TabProperty();
   
              // get existing header text
              dlg.txtTitle.Text = tab.Header.ToString();
  
              if (dlg.ShowDialog() == true)
              {
                  // change header text
                  tab.Header = dlg.txtTitle.Text.Trim();
        }*/

        private void tabDynamic_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            TabItem tab = tabDynamic.SelectedItem as TabItem;
            if (tab == null) return;

            if (tab.Equals(_tabAdd))
            {
                if (settings[0].Controller.Count <= _tabItems.Count-1)
                    CreateNewController();

                // clear tab control binding
                tabDynamic.DataContext = null;

                TabItem newTab = this.AddTabItem();

                // bind tab control
                tabDynamic.DataContext = _tabItems;

                // select newly added tab item
                tabDynamic.SelectedItem = newTab;
                
            }
            else
            {
                controllerNR = _tabItems.IndexOf(tab);
                if(controllerNR != 0)
                {
                    controllerNR = _tabItems.IndexOf(tab)-1;
                }
                if (tally != null) 
                { 
                    tally_Update(tally);
                }
            }
            
            Console.WriteLine(controllerNR);
            //Console.WriteLine(settings[0].Controller[controllerNR].Cams[settings[0].Controller[settings[0].GeneralSettings.LastViewed].LastViewed].Tally);
        }

        private void btnDelete_Click(object sender, RoutedEventArgs e)
        {
            try{
                string tabName = (sender as Button).CommandParameter.ToString();
    
                var item = tabDynamic.Items.Cast<TabItem>().Where(i => i.Name.Equals(tabName)).SingleOrDefault();
    
                TabItem tab = item as TabItem;
    
                if (tab != null)
                {
                    /*if (_tabItems.Count < 3)
                    {
                        MessageBox.Show("Cannot remove last tab.");
                    }
                    else 
                    */
                    if (MessageBox.Show(string.Format("Are you sure you want to remove the tab '{0}'?", tab.Header.ToString()),"Remove Tab", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                    {
                                  // get selected tab
                        TabItem selectedTab = tabDynamic.SelectedItem as TabItem;

                                  // clear tab control binding
                        tabDynamic.DataContext = null;
            
                        _tabItems.Remove(tab);
            
                                  // bind tab control
                        tabDynamic.DataContext = _tabItems;
            
                                  // select previously selected tab. if that is removed then select first tab
                        if (selectedTab == null || selectedTab.Equals(tab))
                        {
                            selectedTab = _tabItems[0];
                        }
                        tabDynamic.SelectedItem = selectedTab;
                    }
                }
            }  
            catch 
            {
                MessageBox.Show("Ein Fehler ist aufgetreten");
            }
        }


        private void tally_Update(object psender)
        {
            (psender as Button).Background = Brushes.White;
            if (settings[0].Controller[controllerNR].Cams[0].Tally)
                (psender as Button).Background = Brushes.PaleVioletRed;

        }

        private void tally_Click(object sender, RoutedEventArgs e)
        {
            settings[0].Controller[controllerNR].Cams[settings[0].Controller[settings[0].GeneralSettings.LastViewed].LastViewed].Tally = !settings[0].Controller[controllerNR].Cams[settings[0].Controller[settings[0].GeneralSettings.LastViewed].LastViewed].Tally;

            tally_Update(sender);
            
            tally = sender;

            Console.WriteLine("ComToCam: tally switch");
        }


        public void LoadJson()
        {
            using (StreamReader r = new StreamReader("file.json"))
            {
                string json = r.ReadToEnd();
                settings = JsonConvert.DeserializeObject<List<Settings>>(json);
            }
        }

        public void SaveJson()
        {
            string json = JsonConvert.SerializeObject(settings);
            File.WriteAllText("file.json", json);
            Console.WriteLine("Saved Json!");

        }

        public void CreateNewController()
        {
            object helper;
            using (StreamReader r = new StreamReader("rawController.json"))
            {
                string json = r.ReadToEnd();
                helper = JsonConvert.DeserializeObject<List<Controller>>(json);
                IList h = (IList)helper;
                settings[0].Controller.Add((Controller)h[0]);

            }

        }

        public class GeneralSettings
        {
            public int LastViewed { get; set; }
            public int a { get; set; }
            public int b { get; set; }
        }

        public class Cam
        {
            public string Name { get; set; }
            public bool Tally { get; set; }
            public int Zoom { get; set; }
            public int Focus { get; set; }
            public int X { get; set; }
            public int Y { get; set; }
        }

        public class Controller
        {
            public string Name { get; set; }
            public string Ip { get; set; }
            public int LastViewed { get; set; }
            public IList<Cam> Cams { get; set; }
        }

        public class Settings
        {
            public GeneralSettings GeneralSettings { get; set; }
            public IList<Controller> Controller { get; set; }
        }










    }
}



/*
 TODO:

-   [7] subtabs updating
-   [5] master settings
- X [2] save to Json
-   [5] tab cloning / fake cloning 
-   [2] catch if json not long enave
 
 */