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
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using System.Windows.Navigation;
using System.Windows.Shapes;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using uPLibrary.Networking.M2Mqtt;
using uPLibrary.Networking.M2Mqtt.Messages;
using static System.Net.Mime.MediaTypeNames;

namespace PTZ_Comander

{
    

    public partial class MainWindow : Window
    {        
        private List<TabItem> _tabItems;
        private TabItem _tabAdd;
        public List<Settings> settings;
        private bool settings_Block = false;
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

            Cam_Input_MoEye_X = 816, //With   Diese Wert sind die Max Pan und Tilt werte der Kammera also sollte das nicht angepasst werden
            Cam_Input_MoEye_Y = 212, //Hight
            Cam_Input_Joystick_Size = 140,

            Cam_Input_Stick_Size = 80,
            Cam_Input_Eye_Size = 15,

            Cam_Input_Window_Stick_Left = 20,
            Cam_Input_Window_Stick_Top = 250,
            Cam_Input_Window_Eye_Left = 20,
            Cam_Input_Window_Eye_Top = 20,

            Cam_Input_Speed_X = 0,
            Cam_Input_Speed_Y = 0,
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
                Closing += OnWindowClosing;
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
                //myMQTT.MQTT_Discover();

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

        ////////////// Handle Window Closing magic
        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            System.Windows.Application.Current.Shutdown();
        }
        public void OnWindowClosing(object sender, CancelEventArgs e)
        {
            foreach (var Topic in myMQTT.Subed_Toppics)
            {
                myMQTT.client.Unsubscribe(new string[] { Topic });
            }
            myMQTT.client.Disconnect();
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
                Cam_Bools_Updater();
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
            Cam_Bools_Updater();
            //Console.WriteLine($"cam NR {camNR}");
        }

        /// Diese Funktion braucht updates vom Json
        private void Update()
        {
            settings_Block = true;
            //catch Errors if no settings are avalebile
            if (settings[0].Controller.Count == 0 )
                return;

            Controller TheController = settings[0].Controller[controllerNR];
            Cam TheCam = TheController.Cams[camNR];

            /////////////////// set collors for talli light
            Cam_Tally_Update_Collors();

            ///////////////// set Bindings

            _Binding.ContrIP = TheController.Ip;
            _Binding.GenA = settings[0].GeneralSettings.a.ToString();
            _Binding.MQTT_Await_Requ_Duration = settings[0].GeneralSettings.MQTT_Await_Requ_Duration;
            _Binding.MQTT_Discover_Duration = settings[0].GeneralSettings.MQTT_Discover_Duration;

            ////////////////
            _Binding.Cam_Input_Speed_X = TheCam.Speed_X;
            _Binding.Cam_Input_Speed_Y = TheCam.Speed_Y;
            _Binding.Cam_Pos_X = TheCam.X;
            _Binding.Cam_Pos_Y = TheCam.Y;

            _Binding.Cam_Name = TheCam.Name;
            _Binding.Cam_Zoom = TheCam.Zoom;
            _Binding.Cam_Fokus = TheCam.Focus;

            ////////////////////
            _Binding.MQTT_ID = TheController.MQTT_ID;
            _Binding.MQTT_Input = TheController.MQTT_Input;
            _Binding.MQTT_Output = TheController.MQTT_Output;
            //// Stick speed
            _Binding.Cam_Input_Stick_Speed_X = settings[0].GeneralSettings.Input_Stick_Speed_X;
            _Binding.Cam_Input_Stick_Speed_Y = settings[0].GeneralSettings.Input_Stick_Speed_Y;

            Update_Eye_Position();

            // update cam optics
            myMQTT.Cam_PTZF(TheController.MQTT_Input, camNR, TheCam.X, TheCam.Y, TheCam.Zoom, TheCam.Focus);
            // update cam pos
            myMQTT.Cam_PTSS(TheController.MQTT_Input, camNR, TheCam.X, TheCam.Y, TheCam.Speed_X / 8, TheCam.Speed_X / 8);

            settings_Block = false;

            /// Bools
            _Binding.Cam_X_Flip           = TheCam.X_Flip;
            _Binding.Cam_Y_Flip           = TheCam.Y_Flip;
            _Binding.Cam_Full_Speed       = TheCam.Full_Speed;
            _Binding.Cam_Auto_AF          = TheCam.Auto_AF;
            _Binding.Cam_Flip             = TheCam.Flip;
            _Binding.Cam_Mirror           = TheCam.Mirror;
            _Binding.Cam_Backlight        = TheCam.Backlight;
            _Binding.Cam_MM_Detect        = TheCam.MM_Detect;
            _Binding.Cam_IR_Output        = TheCam.IR_Output;
            _Binding.Cam_IR_CameraControl = TheCam.IR_CameraControl;
            _Binding.Cam_BestView         = TheCam.BestView;
            _Binding.Cam_Power_On         = TheCam.Power_On;
            _Binding.Cam_Power_LED        = TheCam.Power_LED;

            Cam_Bools_Updater();


        }

        /// Diese Funktion braucht updates vom Json
        private void Store()
        {

            //catch Errors if no settings are avalebile
            if (settings[0].Controller.Count == 0)
                return;
            if (settings_Block)
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

            //// Stick speed
            settings[0].GeneralSettings.Input_Stick_Speed_X = _Binding.Cam_Input_Stick_Speed_X;
            settings[0].GeneralSettings.Input_Stick_Speed_Y = _Binding.Cam_Input_Stick_Speed_Y;

            /// Bools
            TheCam.X_Flip           = _Binding.Cam_X_Flip;
            TheCam.Y_Flip           = _Binding.Cam_Y_Flip;
            TheCam.Full_Speed       = _Binding.Cam_Full_Speed;
            TheCam.Auto_AF          = _Binding.Cam_Auto_AF;
            TheCam.Flip             = _Binding.Cam_Flip;
            TheCam.Mirror           = _Binding.Cam_Mirror;
            TheCam.Backlight        = _Binding.Cam_Backlight;
            TheCam.MM_Detect        = _Binding.Cam_MM_Detect;
            TheCam.IR_Output        = _Binding.Cam_IR_Output;
            TheCam.IR_CameraControl = _Binding.Cam_IR_CameraControl;
            TheCam.BestView         = _Binding.Cam_BestView;
            TheCam.Power_On         = _Binding.Cam_Power_On;
            TheCam.Power_LED        = _Binding.Cam_Power_LED;
        }


        private void Cam_Button_Handler(object sender, RoutedEventArgs e)
        {
            //Console.WriteLine((sender as Button).Content);
            switch ((sender as Button).Content)
            {
                case "AF":
                    myMQTT.MQTT_Push_Msg("esp/PTZ-01/push/visca/json", "{\"camera\":{    \"index\":0,    \"port\":" + camNR.ToString() + "  },  \"command\":\"Focus_Auto\"}");
                    break;
                case "Tally":
                    //Console.WriteLine("ComToCam: tally switch");
                    settings[0].Controller[controllerNR].Cams[camNR].Tally = !settings[0].Controller[controllerNR].Cams[camNR].Tally;
                    settings[0].Controller[controllerNR].Cams[camNR].Tally_Blink = false;
                    Cam_Tally_Update_Collors();
                    break;
                case "Tally Blink":
                    //Console.WriteLine("ComToCam: tally switch");
                    settings[0].Controller[controllerNR].Cams[camNR].Tally_Blink = !settings[0].Controller[controllerNR].Cams[camNR].Tally_Blink;
                    settings[0].Controller[controllerNR].Cams[camNR].Tally = false;
                    Cam_Tally_Update_Collors();
                    break;
            }
        }

        private void checkbox_Handler(object sender, RoutedEventArgs e)
        {
            //Console.WriteLine((sender as CheckBox).Content);
            string this_MQTT_Topic = settings[0].Controller[controllerNR].MQTT_Input;
            Store();
            switch ((sender as CheckBox).Content)
            {
                case "X_Flip":
                    break;
                case "Y_Flip":
                    break;
                case "Flip":
                    if (_Binding.Cam_Flip)
                    {
                        myMQTT.Cam_Simple_Com(this_MQTT_Topic, camNR, "Flip_On");
                    }
                    else
                    {
                        myMQTT.Cam_Simple_Com(this_MQTT_Topic, camNR, "Flip_Off");
                    }
                    break;//worx
                case "Mirror":
                    if (_Binding.Cam_Mirror)
                    {
                        myMQTT.Cam_Simple_Com(this_MQTT_Topic, camNR, "Mirror_On");
                    }
                    else
                    {
                        myMQTT.Cam_Simple_Com(this_MQTT_Topic, camNR, "Mirror_Off");
                    }
                    break;//worx
                case "Power_LED":
                    if (_Binding.Cam_Power_LED)
                    {
                        myMQTT.Cam_Simple_Com(this_MQTT_Topic, camNR, "Power_LED_On");
                    }
                    else
                    {
                        myMQTT.Cam_Simple_Com(this_MQTT_Topic, camNR, "Power_LED_Off");
                    }
                    break;//worx
                case "Power_On":
                    if (_Binding.Cam_Power_On)
                    {
                        myMQTT.Cam_Simple_Com(this_MQTT_Topic, camNR, "Power_On");
                    }
                    else
                    {
                        myMQTT.Cam_Simple_Com(this_MQTT_Topic, camNR, "Power_Off");
                    }
                    break;//irgendwie refocust die kammera dann
                case "IR_CameraControl":
                    if (_Binding.Cam_IR_CameraControl)
                    {
                        myMQTT.Cam_Simple_Com(this_MQTT_Topic, camNR, "IR_CameraControl_On");
                    }
                    else
                    {
                        myMQTT.Cam_Simple_Com(this_MQTT_Topic, camNR, "IR_CameraControl_Off");
                    }
                    break;
                case "IR_Output":
                    if (_Binding.Cam_IR_Output)
                    {
                        myMQTT.Cam_Simple_Com(this_MQTT_Topic, camNR, "IR_Output_On");
                    }
                    else
                    {
                        myMQTT.Cam_Simple_Com(this_MQTT_Topic, camNR, "IR_Output_Off");
                    }
                    break;
                case "Full_Speed":
                    break;
                case "Auto_AF":
                    break;
                case "Backlight":
                    if (_Binding.Cam_Backlight)
                    {
                        myMQTT.Cam_Simple_Com(this_MQTT_Topic, camNR, "Backlight_On");
                    }
                    else
                    {
                        myMQTT.Cam_Simple_Com(this_MQTT_Topic, camNR, "Backlight_Off");
                    }
                    break;
                case "MM_Detect":
                    if (_Binding.Cam_MM_Detect)
                    {
                        myMQTT.Cam_Simple_Com(this_MQTT_Topic, camNR, "MM_Detect_On");
                    }
                    else
                    {
                        myMQTT.Cam_Simple_Com(this_MQTT_Topic, camNR, "MM_Detect_Off");
                    }
                    break;
                case "BestView":
                    if (_Binding.Cam_BestView)
                    {
                        myMQTT.Cam_Simple_Com(this_MQTT_Topic, camNR, "BestView_On");
                    }
                    else
                    {
                        myMQTT.Cam_Simple_Com(this_MQTT_Topic, camNR, "BestView_Stop");
                    }
                    break; // braucht n value

                default:
                    Console.WriteLine("Checkbox error. Not found in code Behind. Content is:" + (sender as CheckBox).Content);
                    break;
            }
            
        }

        private void Cam_Bools_Updater()
        {
            string this_MQTT_Topic = settings[0].Controller[controllerNR].MQTT_Input;

            if (_Binding.Cam_Flip)
            {
                myMQTT.Cam_Simple_Com(this_MQTT_Topic, camNR, "Flip_On");
            }
            else
            {
                myMQTT.Cam_Simple_Com(this_MQTT_Topic, camNR, "Flip_Off");
            }

            if (_Binding.Cam_Mirror)
            {
                myMQTT.Cam_Simple_Com(this_MQTT_Topic, camNR, "Mirror_On");
            }
            else
            {
                myMQTT.Cam_Simple_Com(this_MQTT_Topic, camNR, "Mirror_Off");
            }

            if (_Binding.Cam_Power_LED)
            {
                myMQTT.Cam_Simple_Com(this_MQTT_Topic, camNR, "Power_LED_On");
            }
            else
            {
                myMQTT.Cam_Simple_Com(this_MQTT_Topic, camNR, "Power_LED_Off");
            }

            if (_Binding.Cam_Power_On)
            {
                myMQTT.Cam_Simple_Com(this_MQTT_Topic, camNR, "Power_On");
            }
            else
            {
                myMQTT.Cam_Simple_Com(this_MQTT_Topic, camNR, "Power_Off");
            }

            if (_Binding.Cam_IR_CameraControl)
            {
                myMQTT.Cam_Simple_Com(this_MQTT_Topic, camNR, "IR_CameraControl_On");
            }
            else
            {
                myMQTT.Cam_Simple_Com(this_MQTT_Topic, camNR, "IR_CameraControl_Off");
            }

            if (_Binding.Cam_IR_Output)
            {
                myMQTT.Cam_Simple_Com(this_MQTT_Topic, camNR, "IR_Output_On");
            }
            else
            {
                myMQTT.Cam_Simple_Com(this_MQTT_Topic, camNR, "IR_Output_Off");
            }

            if (_Binding.Cam_Backlight)
            {
                myMQTT.Cam_Simple_Com(this_MQTT_Topic, camNR, "Backlight_On");
            }
            else
            {
                myMQTT.Cam_Simple_Com(this_MQTT_Topic, camNR, "Backlight_Off");
            }

            if (_Binding.Cam_MM_Detect)
            {
                myMQTT.Cam_Simple_Com(this_MQTT_Topic, camNR, "MM_Detect_On");
            }
            else
            {
                myMQTT.Cam_Simple_Com(this_MQTT_Topic, camNR, "MM_Detect_Off");
            }

            if (_Binding.Cam_BestView)
            {
                myMQTT.Cam_Simple_Com(this_MQTT_Topic, camNR, "BestView_On");
            }
            else
            {
                myMQTT.Cam_Simple_Com(this_MQTT_Topic, camNR, "BestView_Stop");
            }
        }
        //////////////////////////// Cam Input

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
                myMQTT.MQTT_Push_Msg("esp/PTZ-01/push/visca/json", "{\"camera\":{    \"index\":0,    \"port\":"+ camNR.ToString() + "  },  \"command\":\"Call_LED_On\",     \"parameter\":{\"value\":10 }}");
            }
            else if (settings[0].Controller[controllerNR].Cams[camNR].Tally_Blink)
            {
                myMQTT.MQTT_Push_Msg("esp/PTZ-01/push/visca/json", "{\"camera\":{    \"index\":0,    \"port\":" + camNR.ToString() + "  },  \"command\":\"Call_LED_Blink\", \"parameter\":{\"value\":10 }}");
            }
            else
            {
                myMQTT.MQTT_Push_Msg("esp/PTZ-01/push/visca/json", "{\"camera\":{    \"index\":0,    \"port\":" + camNR.ToString() + "  },  \"command\":\"Call_LED_Off\",   \"parameter\":{\"value\":10 }}");
            }
        }

        private void Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            Cam TheCam = settings[0].Controller[controllerNR].Cams[camNR];
            if ((sender as Slider).Name == "Fokus")
            {
                myMQTT.MQTT_Push_Msg(settings[0].Controller[controllerNR].MQTT_Input, "{\"camera\": {\"index\": 0,\"port\": " + camNR.ToString() + "},\"command\": \"Focus_Manual\",\"timeout\": 0, \"debug\": 1}");
            }
            Store();
            myMQTT.Cam_PTZF(settings[0].Controller[controllerNR].MQTT_Input, camNR, TheCam.X, TheCam.Y, TheCam.Zoom, TheCam.Focus);
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
                _Binding.Cam_Pos_X += Cam_Input_Stick_Pos.X * (_Binding.Cam_Input_Stick_Speed_X / 8); //calc cam pos
                _Binding.Cam_Pos_Y += (Cam_Input_Stick_Pos.Y * -1) * (_Binding.Cam_Input_Stick_Speed_Y / 8); //
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

            Store();

            Cam TheCam = settings[0].Controller[controllerNR].Cams[camNR];

            myMQTT.Cam_PTSS(settings[0].Controller[controllerNR].MQTT_Input, camNR, TheCam.X, TheCam.Y, TheCam.Speed_X, TheCam.Speed_X);
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

    }
////////////////////////// MQTT
    public class MQTT
    {
        public MqttClient client;
        List<Cam_Ping_Json> MQTT_Discover_Helper = new List<Cam_Ping_Json> { };
        String MQTT_Recive_Helper;
        public List<string> Subed_Toppics = new List<string>(); // ich Glaube das hat keinen zweck

        DateTime lastExecution = DateTime.Now;

        /////////// PTZF sachen
        private System.Threading.Timer Cam_PTZF_Timer;
        private string Cam_PTSS_Helper_Topic = "";
        private string Cam_PTSS_Helper_Msg   = "";
        private string Cam_PTZF_Helper_Topic = "";
        private string Cam_PTZF_Helper_Msg = "";

        public MQTT() 
        {
            client = new MqttClient("192.168.178.116");
            string clientId = Guid.NewGuid().ToString();
            client.Connect(clientId);

            //////// start timer for ptzf
            Cam_PTZF_Timer = new System.Threading.Timer(Cam_PTZF_Timer_Callback, null, 0, 500);
        }

        /// <summary>
        /// Eine Einfache funktion Die Pan Tilt Zoom und Focus setzt und das alles in einer mqtt msg
        /// </summary>
        /// <param name="Topic">Die Topic auf der der Controller ist</param>
        /// <param name="CamPort">der Port des Controllers auf dem die Camera hängt</param>
        /// <param name="Pan"> Pan  [0-816]</param>
        /// <param name="Tilt">Tilt [0-212]</param>
        /// <param name="Zoom">Zoom [0-2885]</param>
        /// <param name="Focus">Focus [0-Unbekannt]</param>
        public void Cam_PTSS(string Topic, int CamPort, int Pan,int Tilt, int Pan_Speed, int Tilt_Speed)
        {
            Cam_PTSS_Helper_Topic = Topic;
            //Cam_PTSS_Helper_Msg = "{\"camera\": { \"index\": 0, \"port\": " + CamPort + " },\"command\": \"PTZF_Direct\",\"parameter\": {\"pan\": " + Pan.ToString() + ",\"tilt\": " + Tilt.ToString() + ",\"zoom\": " + Zoom + ",\"focus\": " + Focus + "},\"timeout\": 0, \"debug\":1}";
            Cam_PTSS_Helper_Msg = "{\"camera\": { \"index\": 0, \"port\": " + CamPort + " },\"command\": \"PT_Direct\"  ,\"parameter\":{ \"pan_speed\":" + Pan_Speed + ", \"tilt_speed\":" + Tilt_Speed + ",\r\n    \"pan\":" + Pan.ToString() + ",    \"tilt\":" + Tilt.ToString() +     "},\"timeout\": 0, \"debug\":1}";
        }
        public void Cam_PTZF(string Topic, int CamPort, int Pan, int Tilt, int Zoom, int Focus)
        {
            Cam_PTZF_Helper_Topic = Topic;
            Cam_PTZF_Helper_Msg = "{\"camera\": { \"index\": 0, \"port\": " + CamPort + " },\"command\": \"PTZF_Direct\",\"parameter\": {\"pan\": " + Pan.ToString() + ",\"tilt\": " + Tilt.ToString() + ",\"zoom\": " + Zoom + ",\"focus\": " + Focus + "},\"timeout\": 0, \"debug\":1}";
            //Cam_PTZF_Helper_Msg = "{\"camera\": { \"index\": 0, \"port\": " + CamPort + " },\"command\": \"PT_Direct\"  ,\"parameter\":{ \"pan_speed\":0, \"tilt_speed\":0,\r\n    \"pan\":" + Pan.ToString() + ",    \"tilt\":" + Tilt.ToString() +     "},\"timeout\": 0, \"debug\":1}";
        }

        public void Cam_Simple_Com(string Topic, int CamPort, string command)
        {
            lastExecution = DateTime.MinValue;
            MQTT_Push_Msg(Topic, "{\"camera\": {\"index\": 0,\"port\": "+CamPort.ToString()+ "},\"command\": \"" + command+ "\",\"timeout\": 0,\"debug\": 1}");
            //Console.WriteLine("{\"camera\": {\"index\": 0,\"port\": " + CamPort + "},\"command\": \"" + command + "\",\"timeout\": 0,\"debug\": 1}");
        }

        private void Cam_PTZF_Timer_Callback(Object stateInfo) 
        {
            if (Cam_PTSS_Helper_Topic != "")
            {
                lastExecution = DateTime.MinValue;
                MQTT_Push_Msg(Cam_PTSS_Helper_Topic, Cam_PTSS_Helper_Msg);
                Cam_PTSS_Helper_Topic = "";
                Cam_PTSS_Helper_Msg = "";
                //Console.WriteLine("PTSS");
            }
            if (Cam_PTZF_Helper_Topic != "")
            {
                lastExecution = DateTime.MinValue;
                MQTT_Push_Msg(Cam_PTZF_Helper_Topic, Cam_PTZF_Helper_Msg);
                Cam_PTZF_Helper_Topic = "";
                Cam_PTZF_Helper_Msg = "";
                //Console.WriteLine("PTZF");
            }
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

            //this.AddTabItem();
            //settings[0].Controller[-1].MQTT_ID = helper.id.ToString();
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
            if ((DateTime.Now - lastExecution).TotalMilliseconds <= 50) 
            {
                return;
            }

            client.Publish(Topic, Encoding.UTF8.GetBytes(Payload), MqttMsgBase.QOS_LEVEL_EXACTLY_ONCE, false);
            lastExecution = DateTime.Now;
            //Console.WriteLine(Payload);
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

            Thread.Sleep(20);//settings[0].GeneralSettings.MQTT_Await_Requ_Duration

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
        public int Input_Stick_Speed_X { get; set; }
        public int Input_Stick_Speed_Y { get; set; }
        public int a { get; set; }
        public int b { get; set; }
    }

    public class Cam
    {
        public string Name { get; set; }
        public bool Tally { get; set; }
        public bool Tally_Blink { get; set; }
        public bool X_Flip { get; set; }
        public bool Y_Flip { get; set; }
        public bool Full_Speed { get; set; }
        public bool Auto_AF { get; set; }
        public bool Flip { get; set; }
        public bool Mirror { get; set; }
        public bool Backlight { get; set; }
        public bool MM_Detect { get; set; }
        public bool Power_LED { get; set; }
        public bool IR_Output { get; set; }
        public bool IR_CameraControl { get; set; }
        public bool BestView { get; set; }
        public bool Power_On { get; set; }
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

        public override string ToString()
        {
            return "\nthe Return of Settings.toString() :->\n" + JsonConvert.SerializeObject(this, Formatting.Indented);
        }
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

        private int _Cam_Input_Stick_Speed_X;
        public int Cam_Input_Stick_Speed_X
        {
            get { return _Cam_Input_Stick_Speed_X; }
            set
            {
                _Cam_Input_Stick_Speed_X = value;
                NotifyPropertyChanged();
            }
        }

        private int _Cam_Input_Stick_Speed_Y;
        public int Cam_Input_Stick_Speed_Y
        {
            get { return _Cam_Input_Stick_Speed_Y; }
            set
            {
                _Cam_Input_Stick_Speed_Y = value;
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

        ///////////////////////////// Bools for cam
        
        private bool _Cam_X_Flip;
        public bool Cam_X_Flip
        {
            get { return _Cam_X_Flip; }
            set
            {
                _Cam_X_Flip = value;
                NotifyPropertyChanged();
            }
        }

        private bool _Cam_Y_Flip;
        public bool Cam_Y_Flip
        {
            get { return _Cam_Y_Flip; }
            set
            {
                _Cam_Y_Flip = value;
                NotifyPropertyChanged();
            }
        }

        private bool _Cam_Full_Speed;
        public bool Cam_Full_Speed
        {
            get { return _Cam_Full_Speed; }
            set
            {
                _Cam_Full_Speed = value;
                NotifyPropertyChanged();
            }
        }
        private bool _Cam_Auto_AF;
        public bool Cam_Auto_AF
        {
            get { return _Cam_Auto_AF; }
            set
            {
                _Cam_Auto_AF = value;
                NotifyPropertyChanged();
            }
        }
        private bool _Cam_Flip;
        public bool Cam_Flip
        {
            get { return _Cam_Flip; }
            set
            {
                _Cam_Flip = value;
                NotifyPropertyChanged();
            }
        }
        private bool _Cam_Mirror;
        public bool Cam_Mirror
        {
            get { return _Cam_Mirror; }
            set
            {
                _Cam_Mirror = value;
                NotifyPropertyChanged();
            }
        }
        private bool _Cam_Backlight;
        public bool Cam_Backlight
        {
            get { return _Cam_Backlight; }
            set
            {
                _Cam_Backlight = value;
                NotifyPropertyChanged();
            }
        }
        private bool _Cam_MM_Detect;
        public bool Cam_MM_Detect
        {
            get { return _Cam_MM_Detect; }
            set
            {
                _Cam_MM_Detect = value;
                NotifyPropertyChanged();
            }
        }
        private bool _Cam_Power_LED;
        public bool Cam_Power_LED
        {
            get { return _Cam_Power_LED; }
            set
            {
                _Cam_Power_LED = value;
                NotifyPropertyChanged();
            }
        }
        private bool _Cam_IR_Output;
        public bool Cam_IR_Output
        {
            get { return _Cam_IR_Output; }
            set
            {
                _Cam_IR_Output = value;
                NotifyPropertyChanged();
            }
        }
        private bool _Cam_IR_CameraControl;
        public bool Cam_IR_CameraControl
        {
            get { return _Cam_IR_CameraControl; }
            set
            {
                _Cam_IR_CameraControl = value;
                NotifyPropertyChanged();
            }
        }
        private bool _Cam_BestView;
        public bool Cam_BestView
        {
            get { return _Cam_BestView; }
            set
            {
                _Cam_BestView = value;
                NotifyPropertyChanged();
            }
        }
        private bool _Cam_Power_On;
        public bool Cam_Power_On
        {
            get { return _Cam_Power_On; }
            set
            {
                _Cam_Power_On = value;
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
-   [2] Find max Focus value
-   [1] Fix Manuel Focus
- X [2] Fix Tab change cam pos bug
-   [ ] überkopf modus
-   [ ] Speed mode
-   [ ] AF ON OFF

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
- X [5] add coms mqttnet
- X [2] Discover Cams
-   [3] Add Nice Popup for response to discover
-   [3] Write Discovered data into json
-   [2] error for no resopnse
-   [2] beim erwarten von requests auch vorzeitig abbrechen können
-   [3] unlimited discover indem das ganze parsing in MQTT_Discover_Received passiert / die emfangenen daten endlich weiter parsen

dotnet build PTZ_Commander.csproj -c Release





           X "X_Flip":false, -> flip der x achse
           X "Y_Flip":false, -> flip der y achse
            "Full_Speed":false, -> immer ptzf
            "Auto_AF":true, -> Timer for af -> braucht noch ein intervall
           X "Flip":false, -> image flip
           X "Mirror":false, -> image mirror
            "Backlight":false, 
            "MM_Detect":false,
            
           X "IR_Output":false,
           X "IR_CameraControl":false,
            "BestView":false,
           X "Power_On":true,
           X "Power_LED":false,
 */