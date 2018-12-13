using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using System.Reflection;
using System.Threading.Tasks;
using Xunit;
using Galador.Reflection.Utils;

namespace TestAndroid
{
    class TestRow
    {
        public bool IsHeader;
        public Type TestType;
        public MethodInfo TestMethod;
        public bool Success;
    }
    public class TestAdapter : BaseAdapter
    {
        Activity activity;
        List<TestRow> testInfo;
        List<TestRow> list = new List<TestRow>();

        public TestAdapter(Activity activity, params Type[] testTypes)
        {
            this.activity = activity;
            testInfo = new List<TestRow>();
            foreach (var t in testTypes)
            {
                testInfo.Add(new TestRow
                {
                    IsHeader = true,
                    TestType = t,
                });
                var methods = from m in t.GetMethods()
                              let fa = Attribute.GetCustomAttribute(m, typeof(FactAttribute))
                              where fa != null
                              select m;
                foreach (var item in methods)
                    testInfo.Add(new TestRow
                    {
                        TestMethod = item,
                    });
            }
        }

        public Task Run()
        {
            if (IsRunning)
                return Task.FromResult(false);
            IsRunning = true;
            list.Clear();
            base.NotifyDataSetChanged();
            var t = new TaskCompletionSource<bool>();
            new Java.Lang.Thread(() =>
            {
                try { RunImpl(); }
                finally
                {
                    IsRunning = false;
                    t.TrySetResult(true);
                }
            }).Start();
            return t.Task;
        }
        public bool IsRunning { get; private set; }

        void RunImpl()
        {
            object target = null;
            foreach (var item in testInfo)
            {
                if (item.IsHeader)
                {
                    if (item is IDisposable)
                        ((IDisposable)item).Dispose();
                    target = Activator.CreateInstance(item.TestType);
                }
                else if (!item.IsHeader)
                {
                    try
                    {
                        item.TestMethod.Invoke(target, Array.Empty<object>());
                        item.Success = true;
                    }
                    catch
                    {
                        item.Success = false;
                    }
                }
                activity.RunOnUiThread(() => 
                {
                    list.Add(item);
                    base.NotifyDataSetChanged();
                });
            }
        }

        public class JavaJobObject : Java.Lang.Object
        {
            internal TestRow Item;
        }

        public override int Count { get { lock (list) return list != null ? list.Count : 0; } }

        public override Java.Lang.Object GetItem(int position)
        {
            lock (list)
            {
                if (list == null || position < 0 || position >= list.Count)
                    return null;
                return new JavaJobObject { Item = list[position] };
            }
        }

        public override long GetItemId(int position)
        {
            return position;
        }

        public override Android.Views.View GetView(int position, Android.Views.View convertView, ViewGroup parent)
        {
            TestRow row;
            lock (list) { row = list[position]; }

            if (row.IsHeader)
            {
                var view = (convertView as TextView);
                if (view == null)
                {
                    view = new TextView(parent.Context);
                    view.SetPadding(5, 5, 5, 5);
                    view.TextSize = 22;
                }
                var name = row.TestType.Name;
                var sb = new StringBuilder(name.Length + 2);
                for (int i = 0; i < name.Length; i++)
                {
                    if (i > 0 && char.IsUpper(name[i]))
                        sb.Append(' ');
                    sb.Append(char.ToUpper(name[i]));
                }
                view.Text = sb.ToString();
                return view;
            }
            else
            {
                var view = (convertView as RelativeLayout);
                if (view == null)
                {
                    var inflated = LayoutInflater.From(activity).Inflate(Resource.Layout.LayoutTest, parent, false);
                    view = (RelativeLayout)inflated;
                }
                var mm = view.FindViewById<TextView>(Resource.Id.resultMethod);
                mm.Text = row.TestMethod.Name;
                var cr = view.FindViewById<CheckBox>(Resource.Id.resultCheckBox);
                cr.Checked = row.Success;
                return view;
            }
        }
    }
}