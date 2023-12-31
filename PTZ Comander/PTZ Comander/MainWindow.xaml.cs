using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
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
        int camNR = 0;
        int controllerINIT = 0;

        Tally _Tally = new Tally()
        {
            Collor = Brushes.White,
        };

        public MainWindow()
        {
            try
            {
                InitializeComponent();
   
                LoadJson();
                
                _tabItems = new List<TabItem>(); // initialize tabItem array

                _tabAdd = new TabItem();// add a tabItem with + in header 
                _tabAdd.Header = "+";

                _tabItems.Add(_tabAdd);

                this.AddTabItem();// add first tab

                tabDynamic.DataContext = _tabItems;// bind tab control
  
                tabDynamic.SelectedIndex = 0;

                this.DataContext = _Tally;
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
            controllerINIT++;
            TabItem tab = new TabItem();// create new tab item
    
            tab.Header = string.Format("Controller {0}", controllerINIT - 1);
            tab.Name = string.Format("tab{0}", controllerINIT);
            tab.HeaderTemplate = tabDynamic.FindResource("TabHeader") as DataTemplate;
            tab.ContentTemplate = tabDynamic.FindResource("TabContend") as DataTemplate;
            
            if(controllerINIT == 1)
            {
                tab.Header = "Meune";
                tab.Name = string.Format("tab{0}", controllerINIT);  // hier werden doppelt names erstellt das ist das problem ich weiß noch nicht wir ich es fix 
                tab.HeaderTemplate = tabDynamic.FindResource("TabHeaderUdel") as DataTemplate;
                tab.ContentTemplate = tabDynamic.FindResource("TabMenue") as DataTemplate;
            }

            TextBox txt = new TextBox(); // add controls to tab item, this case I added just a textbox
            txt.Name = "txt";

            tab.Content = txt;

            _tabItems.Insert(_tabItems.Count - 1, tab); // insert tab item right before the last (+) tab item
            
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
            TabItem tab = (sender as TabControl).SelectedItem as TabItem;
            if (tab == null) return;

            if (tab.Equals(_tabAdd))
            {
                if (settings[0].Controller.Count <= _tabItems.Count-1)
                    CreateNewController();

                (sender as TabControl).DataContext = null;// clear tab control binding

                TabItem newTab = this.AddTabItem();// bind tab control

                (sender as TabControl).DataContext = _tabItems;

                (sender as TabControl).SelectedItem = newTab;// select newly added tab item
            }
            else
            {
                controllerNR = _tabItems.IndexOf(tab);
                if(controllerNR != 0)
                    controllerNR = _tabItems.IndexOf(tab)-1;

                tally_Update();

            }
        }

        private void Cam_Changed(object sender, SelectionChangedEventArgs e)
        {
            //Console.WriteLine("tabchage");
            int tabNR = (sender as TabControl).SelectedIndex;

            if ((sender as TabControl).SelectedItem as TabItem == null) return;

            //////////////// Update Cam NR ////////////////
            camNR = tabNR;
            if (camNR != 0)
                camNR = tabNR - 1;

            tally_Update();
            //Console.WriteLine($"Tab ID: {(sender as TabControl).SelectedIndex.ToString()}");
            //Console.WriteLine($"cam NR {camNR}");
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


        private void tally_Update()
        {
            //Console.WriteLine(_Tally.Collor.ToString());
            _Tally.Collor = Brushes.White;
            if (settings[0].Controller[controllerNR].Cams[camNR].Tally)
                _Tally.Collor = Brushes.PaleVioletRed;

            //Console.WriteLine(_Tally.Collor.ToString());
        }

        private void tally_Click(object sender, RoutedEventArgs e)
        {
            (settings[0].Controller[controllerNR].Cams[camNR].Tally) = !(settings[0].Controller[controllerNR].Cams[camNR].Tally);
            tally_Update();
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
            Console.WriteLine("Saved Json to file!");
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
    }

    public class Tally
    {
        public SolidColorBrush Collor { get; set; }
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



/*
 TODO:

- X [9] Template updating
-   [3] tally push binding update
-   [7] subtabs updating
- X [3] tab create dublicate error fix
- x [5] master settings
-   [1] Content for settings
- X [2] save to Json
- X [5] tab cloning / fake cloning 
- X [2] catch if json not long enave
-   [3] remov controller from json
-   [2] autospawn tabs


 */