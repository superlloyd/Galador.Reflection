using System;
using Android.App;
using Android.Content;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using Android.OS;
using System.Threading.Tasks;
using TestApp;

namespace TestAndroid
{
    [Activity(Label = "TestAndroid", MainLauncher = true, Icon = "@drawable/icon")]
    public class MainActivity : Activity
    {
        protected override void OnCreate(Bundle bundle)
        {
            base.OnCreate(bundle);

            var tadapter = new TestAdapter(this, typeof(RegistryTests), typeof(PathTests),typeof(SerializationTests));

            SetContentView(Resource.Layout.Main);
            var list = FindViewById<ListView>(Resource.Id.listView1);
            list.Adapter = tadapter;
            var button = FindViewById<Button>(Resource.Id.MyButton);
            button.Click += async delegate
            {
                if (tadapter.IsRunning)
                    return;
                button.Text = "Running Tests...";
                await tadapter.Run();
                button.Text = "Start Tests";
            };
        }
    }
}

