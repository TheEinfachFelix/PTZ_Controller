using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
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

        DataBinding _Binding = new DataBinding()
        {
            TallyCollor = Brushes.Black,
            ContrName = "not Loaded",
            ContrIP = "not Loaded",
            GenA = "0"

        };

        public MainWindow()
        {
            try
            {
                DataContext = _Binding;

                InitializeComponent();
   
                LoadJson();
               
                _tabItems = new List<TabItem>(); // initialize tabItem array

                // create add tab
                _tabAdd = new TabItem();
                _tabAdd.Header = "+";
                _tabItems.Add(_tabAdd);

                tabDynamic.DataContext = _tabItems;// bind tab control
                tabDynamic.SelectedIndex = 0;

                // load tabs from settings
                foreach (var i in settings[0].Controller)
                {
                    this.AddTabItem();
                }
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
    
            // für jedes Neue Tab
            tab.Header = string.Format("Controller {0}", controllerINIT - 1);
            tab.Name = string.Format("tab{0}", controllerINIT);
            tab.HeaderTemplate = tabDynamic.FindResource("TabHeader") as DataTemplate;
            tab.ContentTemplate = tabDynamic.FindResource("TabContend") as DataTemplate;
            tab.Content = _Binding; // this is the key responsibel for the binding

            // Für das Menue, wird nur einmal ausgeführt
            if (controllerINIT == 1)
            {
                tab.Header = "Menue";
                tab.HeaderTemplate = tabDynamic.FindResource("TabHeaderUdel") as DataTemplate;
                tab.ContentTemplate = tabDynamic.FindResource("TabMenue") as DataTemplate;
            }

            // insert tab item right before the last (+) tab item
            _tabItems.Insert(_tabItems.Count - 1, tab); 
            
            return tab;
        }
        
        private void tabDynamic_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            TabItem tab = (sender as TabControl).SelectedItem as TabItem;
            if (tab == null) return;

            if (tab.Equals(_tabAdd))
            {
                if (settings[0].Controller.Count <= _tabItems.Count-1) // detects if there are no settings
                    CreateNewController();

                (sender as TabControl).DataContext = null;// clear tab control binding

                TabItem newTab = this.AddTabItem();// bind tab control

                (sender as TabControl).DataContext = _tabItems;

                (sender as TabControl).SelectedItem = newTab;// select newly added tab item
            }
            else
            {
                // Calculate the controller Index 
                controllerNR = _tabItems.IndexOf(tab);
                if(controllerNR != 0)
                    controllerNR = _tabItems.IndexOf(tab)-1;

                Update();
            }
        }

        private void Cam_Changed(object sender, SelectionChangedEventArgs e)
        {
            int tabNR = (sender as TabControl).SelectedIndex;

            if ((sender as TabControl).SelectedItem as TabItem == null) return;

            //////////////// Update Cam NR ////////////////
            camNR = tabNR;
            if (camNR != 0)
                camNR = tabNR - 1;

            Update();

            Console.WriteLine($"cam NR {camNR}");
        }

        private void btnDelete_Click(object sender, RoutedEventArgs e)
        {
            try{
                string tabName = (sender as Button).CommandParameter.ToString();
    
                var item = tabDynamic.Items.Cast<TabItem>().Where(i => i.Name.Equals(tabName)).SingleOrDefault();
    
                TabItem tab = item as TabItem;
    
                if (tab != null)
                {
                    if (MessageBox.Show(string.Format("Are you sure you want to remove the tab '{0}'?", tab.Header.ToString()),"Remove Tab", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                    {
                                  // get selected tab
                        TabItem selectedTab = tabDynamic.SelectedItem as TabItem;

                                  // clear tab control binding
                        tabDynamic.DataContext = null;
                        settings[0].Controller.RemoveAt(_tabItems.IndexOf(tab) - 1);
                        
                        _tabItems.Remove(tab);
            
                                  // bind tab control
                        tabDynamic.DataContext = _tabItems;
            
                                  // select previously selected tab. if that is removed then select first tab
                        if (selectedTab == null || selectedTab.Equals(tab))
                        {
                            tabDynamic.SelectedItem = _tabItems[0];
                        }  
                    }
                }
            }  
            catch (Exception ex)
            {

                MessageBox.Show($"Ein Fehler ist aufgetreten: {ex.Message}");
            }
        }

        private void Update()
        {
            //catch Errors if no settings are avalebile
            if(settings[0].Controller.Count == 0 )
                return;
            // set collors for talli light
            _Binding.TallyCollor = Brushes.White;
            if (settings[0].Controller[controllerNR].Cams[camNR].Tally)
                _Binding.TallyCollor = Brushes.PaleVioletRed;

            // set other values
            _Binding.ContrName = settings[0].Controller[controllerNR].Name;
            _Binding.ContrIP = settings[0].Controller[controllerNR].Ip;
            _Binding.GenA = settings[0].GeneralSettings.a.ToString();

        }

        private void textChangedEventHandler(object sender, TextChangedEventArgs args)
        {
            settings[0].Controller[controllerNR].Name = _Binding.ContrName;
            settings[0].Controller[controllerNR].Ip = _Binding.ContrIP;
            settings[0].GeneralSettings.a = int.Parse(_Binding.GenA);
        }

        private void tally_Click(object sender, RoutedEventArgs e)
        {
            // update the settings
            (settings[0].Controller[controllerNR].Cams[camNR].Tally) = !(settings[0].Controller[controllerNR].Cams[camNR].Tally);
            
            Update();

            Console.WriteLine($"ComToCam: tally switch on controller {controllerNR} cam {camNR} to the value {settings[0].Controller[controllerNR].Cams[camNR].Tally}");
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
            string json = JsonConvert.SerializeObject(settings,Formatting.Indented);
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


    public class DataBinding : INotifyPropertyChanged
    {

        private SolidColorBrush _TallyCollor;
        public SolidColorBrush TallyCollor
        {
            get { return _TallyCollor; }
            set
            {
                _TallyCollor = value;
                NotifyPropertyChanged();
            }
        }

        private String _ContrIP;
        public String ContrIP
        {
            get { return _ContrIP; }
            set
            {
                _ContrIP = value;
                NotifyPropertyChanged();
            }
        }

        private String _ContrName;
        public String ContrName
        {
            get { return _ContrName; }
            set
            {
                _ContrName = value;
                NotifyPropertyChanged();
            }
        }

        private String _GenA;
        public String GenA
        {
            get { return _GenA; }
            set
            {
                _GenA = value;
                NotifyPropertyChanged();
            }
        }




        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void NotifyPropertyChanged([CallerMemberName] String propertyName = "")
        {
            var handler = PropertyChanged;
            if (handler != null)
                handler(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}

/*
 TODO:

- X [5] Fix Binding
- X [9] Template updating
- X [3] tally push binding update
- X [7] subtabs updating
- X [3] tab create dublicate error fix
- X [5] master settings
- X [1] Content for settings
- X [2] save to Json
- X [5] tab cloning / fake cloning 
- X [2] catch if json not long enave
- X [3] remov controller from json
- X [2] autospawn tabs
- X [2] Fix json delete

-   [ ] Controlls get Status 

Controlls:
-   [ ] Tally Blink
-   [ ] Gamma
-   [ ] Flip / Mirror
-   [ ] White ballance
-   [ ] Beleuchtung
-   [ ] Iris
-   [ ] Gain
-   [ ] Backlight
-   [ ] MM_Detect
-   [ ] Best View



Visca:
-   [5] add coms mqttnet
-   [2] Discover Cams

 */