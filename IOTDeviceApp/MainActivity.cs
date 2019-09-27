using Android;
using Android.App;
using Android.Content;
using Android.Locations;
using Android.OS;
using Android.Runtime;
using Android.Support.V4.App;
using Android.Support.V4.Content;
using Android.Support.V7.App;
using Android.Views;
using Android.Widget;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Client.Options;
using MQTTnet.Client.Receiving;
using MQTTnet.Client.Subscribing;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;


namespace IOTDeviceApp
{
    [Activity(Label = "@string/app_name", Theme = "@style/AppTheme.NoActionBar", MainLauncher = true)]
    public class MainActivity : AppCompatActivity, ILocationListener
    {
        Location currentLocation;
        LocationManager locationManager;

        string locationProvider;
        LoactionMessage loactionMessage;
        IntentFilter intentFilter;
        Intent batteryIntent;
        TextView txtTemp, txtUpdatedTime, txtLocation, txtDeviceId;
        private IMqttClient mqttClient;
        private string topic;
        int intervalSecond = 17;
        int locationIntervalSecond = 12;
        IOTMesage iOTMesage;
        string deviceID = "Device_001";
        protected override void OnCreate(Bundle savedInstanceState)
        {
            deviceID = Android.Provider.Settings.Secure.GetString(Android.App.Application.Context.ContentResolver, Android.Provider.Settings.Secure.AndroidId);

            base.OnCreate(savedInstanceState);
            SetContentView(Resource.Layout.activity_main);
            loactionMessage = new LoactionMessage();
            Android.Support.V7.Widget.Toolbar toolbar = FindViewById<Android.Support.V7.Widget.Toolbar>(Resource.Id.toolbar);
            SetSupportActionBar(toolbar);
            txtTemp = FindViewById<TextView>(Resource.Id.txtTemprature);
            txtLocation = FindViewById<TextView>(Resource.Id.txtLocation);
            txtUpdatedTime = FindViewById<TextView>(Resource.Id.txtUpdatedTime);
            txtDeviceId = FindViewById<TextView>(Resource.Id.txtDeviceId);
            if (ContextCompat.CheckSelfPermission(this, Manifest.Permission.AccessFineLocation) == Android.Content.PM.Permission.Granted)
            {
                Console.WriteLine($"We've got permission!");
            }
            else
            {

                ActivityCompat.RequestPermissions(this, new String[] { Manifest.Permission.AccessFineLocation }, 99);

            }
            InitializeLocationManager();
            configureMqttClient();
            var timer = new System.Threading.Timer(
               e => setTemprature(),
                    null,
                   TimeSpan.Zero,
                   TimeSpan.FromSeconds(intervalSecond));
            var timerLocation = new System.Threading.Timer(
            e => setLoacation(),
                 null,
                TimeSpan.Zero,
                TimeSpan.FromSeconds(locationIntervalSecond));


        }
        enum MessageType
        {
            Temperature,
            Location
        }

        private void setTemprature()
        {
            intentFilter = new IntentFilter(Intent.ActionBatteryChanged);
            batteryIntent = RegisterReceiver(null, intentFilter);
            var rval = batteryIntent.GetIntExtra(BatteryManager.ExtraTemperature, -1);
            var temp = (rval * .1 - 1);
            iOTMesage = new IOTMesage() { DeviceId = deviceID, Type = MessageType.Temperature.ToString(), Value = temp };
            publishMessage(getJsonSrting(iOTMesage));
        }
        private void setLoacation()
        {
            publishMessage(getJsonSrting(new IOTMesage()
            {
                DeviceId = deviceID,
                Type = MessageType.Location.ToString()
                                                  ,
                Value = loactionMessage
            }));
        }

        private void SendData()
        {
            setTemprature();
            setLoacation();
        }

        public override bool OnCreateOptionsMenu(IMenu menu)
        {
            MenuInflater.Inflate(Resource.Menu.menu_main, menu);
            return true;
        }

        public override bool OnOptionsItemSelected(IMenuItem item)
        {
            int id = item.ItemId;
            if (id == Resource.Id.action_settings)
            {
                return true;
            }

            return base.OnOptionsItemSelected(item);
        }

        private async Task configureMqttClient()
        {
            try
            {
                // Create a new MQTT client.
                var factory = new MqttFactory();
                mqttClient = factory.CreateMqttClient();
                var tokenSource2 = new CancellationTokenSource();
                CancellationToken ct = tokenSource2.Token;

                var options = new MqttClientOptionsBuilder()
                    .WithTcpServer("broker.hivemq.com", 1883)
                    .Build();
                await mqttClient.ConnectAsync(options, ct);


                mqttClient.ApplicationMessageReceivedHandler = new MqttApplicationMessageReceivedHandlerDelegate
                    (e => client_MqttMsgPublishReceived(e.ApplicationMessage));
                subscribeTopic("hyperledgerdevices");

                Console.WriteLine("Press any key to exit.");
                Console.ReadLine();
            }
            catch (Exception ex)
            {
                txtDeviceId.Text = ex.ToString();
            }
        }



        private async Task subscribeTopic(String param)
        {
            topic = param;
            var tfl = new List<TopicFilter>();
            var tf = new TopicFilter();
            tf.Topic = param;
            tfl.Add(tf);
            var mqttClientSubscribeOptions = new MqttClientSubscribeOptions();
            mqttClientSubscribeOptions.TopicFilters = tfl;
            await mqttClient.SubscribeAsync(mqttClientSubscribeOptions, CancellationToken.None);
        }
        private async Task publishMessage(String param)
        {
            await mqttClient.PublishAsync(topic, param);
        }
        private void client_MqttMsgPublishReceived(MqttApplicationMessage e)
        {
            txtDeviceId.Text = "Device Id : " + deviceID;
            string result = System.Text.Encoding.UTF8.GetString(e.Payload);
            IOTMesage IOTMesage = getObject<IOTMesage>(result);
            RunOnUiThread(() =>
            {

                txtUpdatedTime.Text = "Last reading taken on : " + DateTime.Now.ToLongTimeString();
                if (IOTMesage.DeviceId == deviceID)
                {

                    if (IOTMesage.Type == MessageType.Temperature.ToString())
                        txtTemp.Text = result;
                    else if (IOTMesage.Type == MessageType.Location.ToString())
                        txtLocation.Text = result;
                }
            });
        }


        public string getJsonSrting(object parm)
        {
            return JsonConvert.SerializeObject(parm);
        }

        public T getObject<T>(string parm)
        {
            return JsonConvert.DeserializeObject<T>(parm);
        }

        private async Task InitializeLocationManager()
        {
            locationManager = (LocationManager)GetSystemService(LocationService);

            Location lastKnownGpsLocation = locationManager.GetLastKnownLocation(LocationManager.NetworkProvider);
            if (lastKnownGpsLocation != null)
            {
                loactionMessage.Latitude = lastKnownGpsLocation.Latitude.ToString();
                loactionMessage.Longitude = lastKnownGpsLocation.Longitude.ToString();
            }
            Criteria locationCriteria = new Criteria();
            locationCriteria.Accuracy = Accuracy.Fine;

            locationProvider = locationManager.GetBestProvider(locationCriteria, true);


        }
        protected override void OnResume()
        {
            try
            {
                base.OnResume();
                locationManager.RequestLocationUpdates(locationProvider, 0, 0, this);


            }
            catch
            {
            }
        }
        protected override void OnPause()
        {
            base.OnPause();
            locationManager.RemoveUpdates(this);
        }

        public void OnLocationChanged(Location location)
        {
            currentLocation = location;
            if (currentLocation == null)
            {
                //Error Message  
            }
            else
            {
                loactionMessage.Latitude = currentLocation.Latitude.ToString();
                loactionMessage.Longitude = currentLocation.Longitude.ToString();
            }
        }

        public void OnProviderDisabled(string provider)
        {
            //throw new NotImplementedException();
        }

        public void OnProviderEnabled(string provider)
        {
            //throw new NotImplementedException();
        }

        public void OnStatusChanged(string provider, [GeneratedEnum] Availability status, Bundle extras)
        {
            //throw new NotImplementedException();
        }
    }
    public class IOTMesage
    {
        public string DeviceId { get; set; }
        public string Type { get; set; }
        public object Value { get; set; }
    }

    public class LoactionMessage
    {

        public string Latitude { get; set; }

        public string Longitude { get; set; }

    }


}

