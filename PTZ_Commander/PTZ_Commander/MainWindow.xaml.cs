using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
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
using Newtonsoft.Json.Linq;

using uPLibrary.Networking.M2Mqtt;
using uPLibrary.Networking.M2Mqtt.Messages;

namespace PTZ_Comander

{
    

    public partial class MainWindow : Window
    {        
        private List<TabItem> _tabItems;
        private TabItem _tabAdd;
        public List<Settings> settings;
        int controllerNR = 0;
        int camNR = 0;
        int controllerINIT = 0;

        Vector Cam_Input_Defauld_Offset = new Vector(205,-54);
        Vector m_Cam_Input_Stick_Pos = new Vector();

        public DataBinding _Binding = new DataBinding()
        {
            TallyCollor = Brushes.Black,
            ContrName = "not Loaded",
            ContrIP = "not Loaded",
            GenA = "0",


            Cam_Name = "Diese cam",

            Cam_Input_MoEye_X = 816, //With
            Cam_Input_MoEye_Y = 212, //Hight
            Cam_Input_Joystick_Size = 140,

            Cam_Input_Stick_Size = 80,
            Cam_Input_Eye_Size = 15,

            Cam_Input_Window_Stick_Left = 20,
            Cam_Input_Window_Stick_Top = 250,
            Cam_Input_Window_Eye_Left = 20,
            Cam_Input_Window_Eye_Top = 20,

            Cam_Input_Speed_X = 8,
            Cam_Input_Speed_Y = 8,
            Cam_Pos_X = 0,
            Cam_Pos_Y = 0,

            Cam_Zoom = 0,
            Cam_Fokus = 0,

            Cam_Tally_Collor = Brushes.White,
            Cam_Tally_Blink_Collor = Brushes.White
        };
        private MQTT myMQTT = new MQTT();

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
////////////Console.WriteLine(settings[0].ToString());
                tabDynamic.DataContext = _tabItems;// bind tab control
                
                Update();

                tabDynamic.SelectedIndex = 0;

                // load tabs from settings
                foreach (var i in settings[0].Controller)
                {
                    this.AddTabItem();
                }

                // set cam input positions
                Reset_Stick_Position();
                Update_Eye_Position();


                ///////////////////////////////////////// MQTT Discover

                //my_MQTT_Handler = new MQTT_Handler();
                //Subscribe("visca/PTZ-01");
                //Send_msg("visca/ping","");
                //myMQTT.MQTT_Discover();
                //Console.WriteLine();

            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        ~MainWindow()
        {
            Store();
            SaveJson();
        }

        public TabItem AddTabItem()
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

        /////////////////////////// Tab Change
        private void tabDynamic_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            Store();

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
            Store();

            int tabNR = (sender as TabControl).SelectedIndex;

            if ((sender as TabControl).SelectedItem as TabItem == null) return;

            //////////////// Update Cam NR ////////////////
            camNR = tabNR;
            if (camNR != 0)
                camNR = tabNR - 1;

            Update();

            Console.WriteLine($"cam NR {camNR}");
        }

        /// Diese Funktion braucht updates vom Json
        private void Update()
        {
            //catch Errors if no settings are avalebile
            if(settings[0].Controller.Count == 0 )
                return;

            Controller TheController = settings[0].Controller[controllerNR];
            Cam TheCam = TheController.Cams[camNR];

            /////////////////// set collors for talli light
            Cam_Tally_Update_Collors();

            ///////////////// set Bindings
            _Binding.ContrName = TheController.Name;
            _Binding.ContrIP = TheController.Ip;
            _Binding.GenA = settings[0].GeneralSettings.a.ToString();
            _Binding.MQTT_Await_Requ_Duration = settings[0].GeneralSettings.MQTT_Await_Requ_Duration;
            _Binding.MQTT_Discover_Duration = settings[0].GeneralSettings.MQTT_Discover_Duration;

            
            ////////////////

            _Binding.Cam_Name = TheCam.Name;

            _Binding.Cam_Input_Speed_X = TheCam.Speed_X;
            _Binding.Cam_Input_Speed_Y = TheCam.Speed_Y;
            _Binding.Cam_Pos_X = TheCam.X;
            _Binding.Cam_Pos_Y = TheCam.Y;

            _Binding.Cam_Zoom = TheCam.Zoom;
            _Binding.Cam_Fokus = TheCam.Focus;

            ////////////////////
            _Binding.MQTT_ID = TheController.MQTT_ID;
            _Binding.MQTT_Input = TheController.MQTT_Input;
            _Binding.MQTT_Output = TheController.MQTT_Output;
        }

        /// Diese Funktion braucht updates vom Json
        private void Store()
        {
            //catch Errors if no settings are avalebile
            if (settings[0].Controller.Count == 0)
                return;

            Controller TheController = settings[0].Controller[controllerNR];
            Cam TheCam = TheController.Cams[camNR];

            ///////////////// store Bindings
            TheController.Name = _Binding.ContrName;
            TheController.Ip = _Binding.ContrIP;
            settings[0].GeneralSettings.a = int.Parse(_Binding.GenA);

             settings[0].GeneralSettings.MQTT_Await_Requ_Duration = _Binding.MQTT_Await_Requ_Duration;
             settings[0].GeneralSettings.MQTT_Discover_Duration = _Binding.MQTT_Discover_Duration;
            ////////////////

            TheCam.Name = _Binding.Cam_Name;

            TheCam.Speed_X = (int)_Binding.Cam_Input_Speed_X;
            TheCam.Speed_Y = (int)_Binding.Cam_Input_Speed_Y;
            TheCam.X = (int)_Binding.Cam_Pos_X;
            TheCam.Y = (int)_Binding.Cam_Pos_Y;

            TheCam.Zoom  = _Binding.Cam_Zoom;
            TheCam.Focus = _Binding.Cam_Fokus;

            ///////////////
            TheController.MQTT_ID = _Binding.MQTT_ID;
            TheController.MQTT_Input = _Binding.MQTT_Input;
            TheController.MQTT_Output = _Binding.MQTT_Output;
        }

        //////////////////////////// Cam Input

        private void Cam_Tally_Click(object sender, RoutedEventArgs e)
        {
            //Console.WriteLine("ComToCam: tally switch");
            settings[0].Controller[controllerNR].Cams[camNR].Tally = !settings[0].Controller[controllerNR].Cams[camNR].Tally;
            settings[0].Controller[controllerNR].Cams[camNR].Tally_Blink = false;
            Cam_Tally_Update_Collors();
        }

        // This Function need it own json entry
        private void Cam_Blink_Tally_Click(object sender, RoutedEventArgs e)
        {
            //Console.WriteLine("ComToCam: tally switch");
            settings[0].Controller[controllerNR].Cams[camNR].Tally_Blink = !settings[0].Controller[controllerNR].Cams[camNR].Tally_Blink;
            settings[0].Controller[controllerNR].Cams[camNR].Tally = false;
            Cam_Tally_Update_Collors();
        }

        private void Cam_Tally_Update_Collors() 
        {
            Controller TheController = settings[0].Controller[controllerNR];
            Cam TheCam = TheController.Cams[camNR];

            _Binding.Cam_Tally_Collor = Brushes.White;
            if (TheCam.Tally)
                _Binding.Cam_Tally_Collor = Brushes.PaleVioletRed;

            _Binding.Cam_Tally_Blink_Collor = Brushes.White;
            if (TheCam.Tally_Blink)
                _Binding.Cam_Tally_Blink_Collor = Brushes.PaleVioletRed;

            if (settings[0].Controller[controllerNR].Cams[camNR].Tally)
            {
                //_Binding.MQTT_Input
                //"{\"camera\":{    \"index\":0,    \"port\":0  },  \"command\":\"Call_LED_Off\", \"parameter\":{\"value\":10 }}"

                //myMQTT.MQTT_Push_Msg("esp / PTZ - 01 / push / visca / json","der" );
            }
            else if (settings[0].Controller[controllerNR].Cams[camNR].Tally_Blink)
            {
                
            }
            else
            {

            }



        }

        private void Cam_Input_Joystick_Mouse_Move(object sender, MouseEventArgs e)
        {
            double StickWindowCenter = _Binding.Cam_Input_Joystick_Size * 0.5; //With

            Vector Cam_Input_Stick_Pos = e.GetPosition(tabDynamic) - new Point(_Binding.Cam_Input_Window_Stick_Left, _Binding.Cam_Input_Window_Stick_Top)
                                         - new Vector(StickWindowCenter, StickWindowCenter);
            //Tab Offset
            Cam_Input_Stick_Pos.X -= Cam_Input_Defauld_Offset.X;
            Cam_Input_Stick_Pos.Y += Cam_Input_Defauld_Offset.Y;

            //Normalize coords
            Cam_Input_Stick_Pos /= StickWindowCenter;

            if (Cam_Input_Stick_Pos.Length > 1.0)
                Cam_Input_Stick_Pos.Normalize();

            //Console.WriteLine($"Distance {Cam_Input_Knob_Pos.Length} Angle {Math.Atan2(Cam_Input_Knob_Pos.Y, Cam_Input_Knob_Pos.X)}");
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                _Binding.Cam_Pos_X +=  Cam_Input_Stick_Pos.X       * (_Binding.Cam_Input_Speed_X / 8); //calc cam pos
                _Binding.Cam_Pos_Y += (Cam_Input_Stick_Pos.Y * -1) * (_Binding.Cam_Input_Speed_Y / 8);
                m_Cam_Input_Stick_Pos = Cam_Input_Stick_Pos; // set value to stick pos

                Update_Stick_Position();
                Update_Eye_Position();
            }
            else
            {
                Reset_Stick_Position();
            }
        }

        void Update_Stick_Position()
        {
            double fKnobRadius = _Binding.Cam_Input_Stick_Size * 0.5;
            double StickWindowCenter = _Binding.Cam_Input_Joystick_Size * 0.5;

            _Binding.Cam_Input_Stick_Left = _Binding.Cam_Input_Window_Stick_Left + m_Cam_Input_Stick_Pos.X * StickWindowCenter + StickWindowCenter - fKnobRadius;
            _Binding.Cam_Input_Stick_Top  = _Binding.Cam_Input_Window_Stick_Top  + m_Cam_Input_Stick_Pos.Y * StickWindowCenter + StickWindowCenter - fKnobRadius;
        }

        void Reset_Stick_Position()
        {
            double fKnobRadius = _Binding.Cam_Input_Stick_Size * 0.5;
            double StickWindowCenter = _Binding.Cam_Input_Joystick_Size * 0.5;

            _Binding.Cam_Input_Stick_Left = _Binding.Cam_Input_Window_Stick_Left + StickWindowCenter - fKnobRadius;
            _Binding.Cam_Input_Stick_Top = _Binding.Cam_Input_Window_Stick_Top + StickWindowCenter - fKnobRadius;
        }

        private void Cam_Input_MotionEye_Mouse_Move(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                _Binding.Cam_Pos_X = e.GetPosition(tabDynamic).X - _Binding.Cam_Input_Window_Eye_Left                             - Cam_Input_Defauld_Offset.X;
                _Binding.Cam_Pos_Y = _Binding.Cam_Input_Window_Eye_Top + _Binding.Cam_Input_MoEye_Y - e.GetPosition(tabDynamic).Y - Cam_Input_Defauld_Offset.Y;
                Update_Eye_Position();

            }
        }

        public void Update_Eye_Position()
        {
            if (_Binding.Cam_Pos_X < 0)  // Links
                _Binding.Cam_Pos_X = 0;
            if (_Binding.Cam_Pos_X > _Binding.Cam_Input_MoEye_X)  // Right
                _Binding.Cam_Pos_X = _Binding.Cam_Input_MoEye_X;
            if (_Binding.Cam_Pos_Y < 0)  // Oben
                _Binding.Cam_Pos_Y = 0;
            if (_Binding.Cam_Pos_Y > _Binding.Cam_Input_MoEye_Y)  // Unten
                _Binding.Cam_Pos_Y = _Binding.Cam_Input_MoEye_Y;

            double pOffset = _Binding.Cam_Input_Eye_Size * 0.5;
            _Binding.Cam_Input_Eye_Left = _Binding.Cam_Input_Window_Eye_Left + _Binding.Cam_Pos_X - pOffset;
            _Binding.Cam_Input_Eye_Top = _Binding.Cam_Input_Window_Eye_Top + _Binding.Cam_Input_MoEye_Y - _Binding.Cam_Pos_Y - pOffset;
        }

        private void Cam_Button_Step(object sender, RoutedEventArgs e)
        {
            double Content = int.Parse((string)(sender as Button).Content) / 2 + 0.5;

            switch ((sender as Button).Name.Substring(0, 1))
            {
                case "U": //hoch
                    _Binding.Cam_Pos_Y += Content;
                    break;

                case "D": //Runter
                    _Binding.Cam_Pos_Y -= Content;
                    break;

                case "L": //Links
                    _Binding.Cam_Pos_X -= Content;
                    break;

                case "R": //Rechs
                    _Binding.Cam_Pos_X += Content;
                    break;
            }

            switch ((sender as Button).Name.Substring(1, 1))
            {
                case "U": //hoch
                    _Binding.Cam_Pos_Y += Content;
                    break;

                case "D": //Runter
                    _Binding.Cam_Pos_Y -= Content;
                    break;

                case "L": //Links
                    _Binding.Cam_Pos_X -= Content;
                    break;

                case "R": //Rechs
                    _Binding.Cam_Pos_X += Content;
                    break;
            }
            Update_Eye_Position();
        }

        private void Cam_Position_Change(object sender, TextChangedEventArgs e)
        {
            Update_Eye_Position();
        }


        //////////////////////////// Json
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

        ////////////////////////// MQTT

    }

    public class MQTT:MainWindow
    {
        MqttClient client;
        List<Cam_Ping_Json> MQTT_Discover_Helper = new List<Cam_Ping_Json> { };
        String MQTT_Recive_Helper;
        List<string> Subed_Toppics = new List<string>(); // ich Glaube das hat keinen zweck


        public MQTT() 
        {

            client = new MqttClient("192.168.178.116");
            string clientId = Guid.NewGuid().ToString();
            client.Connect(clientId);
        }

        ~MQTT()
        {
            foreach (var Topic in Subed_Toppics)
            {
                client.Unsubscribe(new string[] { Topic });
            }
            client.Disconnect();
        }

        /// <summary>
        /// Discovers all the cams and Returns a list of all the Returned Objects
        /// </summary>
        /// <returns></returns>
        public void MQTT_Discover()
        {
            //////// Send Request
            string strValue = Convert.ToString("Das ist der Ping von meinem code");

            client.Publish("visca/ping", Encoding.UTF8.GetBytes(strValue), MqttMsgBase.QOS_LEVEL_EXACTLY_ONCE, false);

            //////// Sub
            client.MqttMsgPublishReceived += MQTT_Discover_Received;

            Subed_Toppics.Add("visca/PTZ-01");
            ////////////////////////////////client.Subscribe(new string[] { "visca/PTZ-01" }, new byte[] { MqttMsgBase.QOS_LEVEL_EXACTLY_ONCE });

            /*
            //Thread.Sleep(settings[0].GeneralSettings.MQTT_Discover_Duration);

            //client.Unsubscribe(new string[] { "visca/PTZ-01" });
            //client.MqttMsgPublishReceived -= MQTT_Discover_Received;

            //if (MQTT_Discover_Helper == new List<Cam_Ping_Json> {})
            //{
            //    return new List<Cam_Ping_Json> {};
            //}
            //var helper = MQTT_Discover_Helper;
            //MQTT_Discover_Helper = new List<Cam_Ping_Json> {};
            //return helper;
            */
        }
        private void MQTT_Discover_Received(object sender, MqttMsgPublishEventArgs e)
        {
            /////// Cleaning the input
            String Json = Encoding.UTF8.GetString(e.Message);
            Json = "[" + Json + "]";
            Json = Json.Replace("mqtt-json", "mqttjson");

            /////// Generate Obj and add it to list
            Cam_Ping_Json helper = JsonConvert.DeserializeObject<List<Cam_Ping_Json>>(Json)[0];

            this.AddTabItem();
            settings[0].Controller[-1].MQTT_ID = helper.id.ToString();
            Console.WriteLine(helper.ToString());


            MQTT_Discover_Helper.Add(helper);
        }

        /// <summary>
        /// Send Payload to Topic but does not await response
        /// </summary>
        /// <param name="Topic"></param>
        /// <param name="Payload"></param>
        public void MQTT_Push_Msg(string Topic, string Payload)
        {
            //client.Publish(Topic, Encoding.UTF8.GetBytes(Convert.ToString(Payload)), MqttMsgBase.QOS_LEVEL_EXACTLY_ONCE, false);
            client.Publish("esp/PTZ-01/push/visca/json", Encoding.UTF8.GetBytes("dsafe"), MqttMsgBase.QOS_LEVEL_EXACTLY_ONCE, false);
        }


        /// <summary>
        /// Send Payload to Topic and Return the Response
        /// </summary>
        /// <param name="Topic"></param>
        /// <param name="Payload"></param>
        /// <returns></returns>
        public string MQTT_Send_Msg(string inptTopic, string otptTopic, string Payload)
        {
            //////// Send Request
            string strValue = Convert.ToString(Payload);

            client.Publish(inptTopic, Encoding.UTF8.GetBytes(strValue), MqttMsgBase.QOS_LEVEL_EXACTLY_ONCE, false);

            //////// Sub
            client.MqttMsgPublishReceived += MQTT_Response_Received;

            Subed_Toppics.Add(inptTopic);
            client.Subscribe(new string[] { inptTopic }, new byte[] { MqttMsgBase.QOS_LEVEL_EXACTLY_ONCE });

            Thread.Sleep(settings[0].GeneralSettings.MQTT_Await_Requ_Duration);

            client.Unsubscribe(new string[] { inptTopic });
            client.MqttMsgPublishReceived -= MQTT_Response_Received;

            if (MQTT_Recive_Helper == null)
            {
                return "";
            }
            var helper = MQTT_Recive_Helper;
            MQTT_Recive_Helper = null;
            return helper;
        }
        private void MQTT_Response_Received(object sender, MqttMsgPublishEventArgs e)
        {
            MQTT_Recive_Helper = Encoding.UTF8.GetString(e.Message);
        }
    }

    ///////////////////////////////// Json
    public class GeneralSettings
    {
        public int LastViewed { get; set; }
        public int MQTT_Discover_Duration { get; set; }
        public int MQTT_Await_Requ_Duration { get; set; }
        public int a { get; set; }
        public int b { get; set; }
    }

    public class Cam
    {
        public string Name { get; set; }
        public bool Tally { get; set; }
        public bool Tally_Blink { get; set; }
        public int Zoom { get; set; }
        public int Focus { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public int Speed_X { get; set; }
        public int Speed_Y { get; set; }
    }

    public class Controller
    {
        public string Name { get; set; }
        public string Ip { get; set; }
        public string MQTT_ID { get; set; }
        public string MQTT_Input { get; set; }
        public string MQTT_Output { get; set; }
        public int LastViewed { get; set; }
        public IList<Cam> Cams { get; set; }
    }

    public class Settings
    {
        public GeneralSettings GeneralSettings { get; set; }
        public IList<Controller> Controller { get; set; }

        public override string ToString()
        {
            return "\nthe Return of Settings.toString() :->\n" + JsonConvert.SerializeObject(this, Formatting.Indented);
        }
    }


    ///////////////////////////////// Binding
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

        private string _Cam_Name;
        public string Cam_Name
        {
            get { return _Cam_Name; }
            set
            {
                _Cam_Name = value;
                NotifyPropertyChanged();
            }
        }

        ////////////////////////////////////// Sizes of Fields //////////////////////////////////////
        private double _Cam_Input_MoEye_X;
        public double Cam_Input_MoEye_X
        {
            get { return _Cam_Input_MoEye_X; }
            set
            {
                _Cam_Input_MoEye_X = value;
                NotifyPropertyChanged();
            }
        }

        private double _Cam_Input_MoEye_Y;
        public double Cam_Input_MoEye_Y
        {
            get { return _Cam_Input_MoEye_Y; }
            set
            {
                _Cam_Input_MoEye_Y = value;
                NotifyPropertyChanged();
            }
        }
        //////////////////////////////////////
        private double _Cam_Input_Joystick_Size;
        public double Cam_Input_Joystick_Size
        {
            get { return _Cam_Input_Joystick_Size; }
            set
            {
                _Cam_Input_Joystick_Size = value;
                NotifyPropertyChanged();
            }
        }

        ////////////////////////////////////// Size of Input //////////////////////////////////////
        private double _Cam_Input_Stick_Size;
        public double Cam_Input_Stick_Size
        {
            get { return _Cam_Input_Stick_Size; }
            set
            {
                _Cam_Input_Stick_Size = value;
                NotifyPropertyChanged();
            }
        }
        //////////////////////////////////////
        private double _Cam_Input_Eye_Size;
        public double Cam_Input_Eye_Size
        {
            get { return _Cam_Input_Eye_Size; }
            set
            {
                _Cam_Input_Eye_Size = value;
                NotifyPropertyChanged();
            }
        }

        ////////////////////////////////////// Position of Input //////////////////////////////////////
        private double _Cam_Input_Stick_Left;
        public double Cam_Input_Stick_Left
        {
            get { return _Cam_Input_Stick_Left; }
            set
            {
                _Cam_Input_Stick_Left = value;
                NotifyPropertyChanged();
            }
        }

        private double _Cam_Input_Stick_Top;
        public double Cam_Input_Stick_Top
        {
            get { return _Cam_Input_Stick_Top; }
            set
            {
                _Cam_Input_Stick_Top = value;
                NotifyPropertyChanged();
            }
        }
        //////////////////////////////////////

        private double _Cam_Input_Eye_Left;
        public double Cam_Input_Eye_Left
        {
            get { return _Cam_Input_Eye_Left; }
            set
            {
                _Cam_Input_Eye_Left = value;
                NotifyPropertyChanged();
            }
        }

        private double _Cam_Input_Eye_Top;
        public double Cam_Input_Eye_Top
        {
            get { return _Cam_Input_Eye_Top; }
            set
            {
                _Cam_Input_Eye_Top = value;
                NotifyPropertyChanged();
            }
        }

        ////////////////////////////////////// Position of Fields //////////////////////////////////////
        private double _Cam_Input_Window_Stick_Left;
        public double Cam_Input_Window_Stick_Left
        {
            get { return _Cam_Input_Window_Stick_Left; }
            set
            {
                _Cam_Input_Window_Stick_Left = value;
                NotifyPropertyChanged();
            }
        }

        private double _Cam_Input_Window_Stick_Top;
        public double Cam_Input_Window_Stick_Top
        {
            get { return _Cam_Input_Window_Stick_Top; }
            set
            {
                _Cam_Input_Window_Stick_Top = value;
                NotifyPropertyChanged();
            }
        }
        //////////////////////////////////////
        private double _Cam_Input_Window_Eye_Left;
        public double Cam_Input_Window_Eye_Left
        {
            get { return _Cam_Input_Window_Eye_Left; }
            set
            {
                _Cam_Input_Window_Eye_Left = value;
                NotifyPropertyChanged();
            }
        }

        private double _Cam_Input_Window_Eye_Top;
        public double Cam_Input_Window_Eye_Top
        {
            get { return _Cam_Input_Window_Eye_Top; }
            set
            {
                _Cam_Input_Window_Eye_Top = value;
                NotifyPropertyChanged();
            }
        }

        ////////////////////////////////////// Other //////////////////////////////////////
        private double _Cam_Input_Speed_X;
        public double Cam_Input_Speed_X
        {
            get { return _Cam_Input_Speed_X; }
            set
            {
                _Cam_Input_Speed_X = value;
                NotifyPropertyChanged();
            }
        }
        
        private double _Cam_Input_Speed_Y;
        public double Cam_Input_Speed_Y
        {
            get { return _Cam_Input_Speed_Y; }
            set
            {
                _Cam_Input_Speed_Y = value;
                NotifyPropertyChanged();
            }
        }

        private double _Cam_Pos_X;
        public double Cam_Pos_X
        {
            get { return _Cam_Pos_X; }
            set
            {
                _Cam_Pos_X = Math.Round(value, 1);
                NotifyPropertyChanged();
            }
        }

        private double _Cam_Pos_Y;
        public double Cam_Pos_Y
        {
            get { return _Cam_Pos_Y; }
            set
            {
                _Cam_Pos_Y = Math.Round(value, 1);
                NotifyPropertyChanged();
            }
        }
        ////////////////////////////////////// Optics //////////////////////////////////////
        private int _Cam_Zoom;
        public int Cam_Zoom
        {
            get { return _Cam_Zoom; }
            set
            {
                _Cam_Zoom = value;
                NotifyPropertyChanged();
            }
        }

        private int _Cam_Fokus;
        public int Cam_Fokus
        {
            get { return _Cam_Fokus; }
            set
            {
                _Cam_Fokus = value;
                NotifyPropertyChanged();
            }
        }

        private SolidColorBrush _Cam_Tally_Collor;
        public SolidColorBrush Cam_Tally_Collor
        {
            get { return _Cam_Tally_Collor; }
            set
            {
                _Cam_Tally_Collor = value;
                NotifyPropertyChanged();
            }
        }

        private SolidColorBrush _Cam_Tally_Blink_Collor;
        public SolidColorBrush Cam_Tally_Blink_Collor
        {
            get { return _Cam_Tally_Blink_Collor; }
            set
            {
                _Cam_Tally_Blink_Collor = value;
                NotifyPropertyChanged();
            }
        }

        ////////////////////////////////////// MQTT //////////////////////////////////////
        private int _MQTT_Discover_Duration;
        public int MQTT_Discover_Duration
        {
            get { return _MQTT_Discover_Duration; }
            set
            {
                _MQTT_Discover_Duration = value;
                NotifyPropertyChanged();
            }
        }

        private int _MQTT_Await_Requ_Duration;
        public int MQTT_Await_Requ_Duration
        {
            get { return _MQTT_Await_Requ_Duration; }
            set
            {
                _MQTT_Await_Requ_Duration = value;
                NotifyPropertyChanged();
            }
        }


        private string _MQTT_ID;
        public string MQTT_ID
        {
            get { return _MQTT_ID; }
            set
            {
                _MQTT_ID = value;
                NotifyPropertyChanged();
            }
        }

        private string _MQTT_Input;
        public string MQTT_Input
        {
            get { return _MQTT_Input; }
            set
            {
                _MQTT_Input = value;
                NotifyPropertyChanged();
            }
        }

        private string _MQTT_Output;
        public string MQTT_Output
        {
            get { return _MQTT_Output; }
            set
            {
                _MQTT_Output = value;
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

    ///////////////////////////////// MQTT 
    public class MqttJson
    {
        public string input { get; set; }
        public string output { get; set; }
    }

    public class Plugins
    {
        public MqttJson mqttjson { get; set; }
    }

    public class Cam_Ping_Json
    {
        public long id { get; set; }
        public string ipv4 { get; set; }
        public string ipv6g { get; set; }
        public string ipv6l { get; set; }
        public Plugins plugins { get; set; }
        public string visca { get; set; }

        public override string ToString() 
        { 
        return "\nthe Return of Cam_Ping_Json.toString() :->\n" + JsonConvert.SerializeObject(this, Formatting.Indented);
        }
        /*
         * {"id":13984767495476,
         * "ipv4":"192.168.178.119",
         * "ipv6g":"::",
         * "ipv6l":"::",
         * "plugins":
         * { "mqtt-json":
         * { "input":"esp/PTZ-01/push/visca/json",
         * "output":"esp/PTZ-01/get/visca/json"} },
         * "visca":"pong"}
        */
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
- X [3] Auto Save Cam Settings
-   [4] Motion EYE Hintergrundbild
- X [3] Add json stuff
-   [3] Map Discoverd data to json data

Controlls:
-   [X] Tally Blink
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
- X [2] Discover Cams
-   [3] Add Nice Popup for response to discover
-   [3] Write Discovered data into json
-   [2] error for no resopnse
-   [2] beim erwarten von requests auch vorzeitig abbrechen können
-   [3] unlimited discover indem das ganze parsing in MQTT_Discover_Received passiert

 */