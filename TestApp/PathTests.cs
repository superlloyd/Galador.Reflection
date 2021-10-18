using Galador.Reflection;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace TestApp
{
    public class PathTests
    {
        class Model : INotifyPropertyChanged
        {
            #region Name

            public string Name
            {
                get { return mName; }
                set
                {
                    if (value == mName)
                        return;

                    mName = value;
                    OnPropertyChanged(nameof(Name));
                }
            }
            string mName;

            #endregion

            #region Other

            public Model Other
            {
                get { return mOther; }
                set
                {
                    if (value == mOther)
                        return;

                    mOther = value;
                    OnPropertyChanged(nameof(Other));
                }
            }
            Model mOther;

            #endregion

            void OnPropertyChanged([CallerMemberName]string pName = null)
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(pName));
            }
            public event PropertyChangedEventHandler PropertyChanged;
        }

        [Fact]
        public void PathWorks()
        {
            var m = new Model();
            var values = new List<string>();

            var p = PropertyPath.Watch(m, x => x.Other.Name, x => {
                values.Add(x);
            });

            m.Name = "Albert";
            m.Other = m;
            m.Other = new Model { Name = "Fubar" };
            m.Other.Name = "ugh?";
            m.Other = null;

            Assert.Equal(4, values.Count);
            Assert.Equal("Albert", values[0]);
            Assert.Equal("Fubar", values[1]);
            Assert.Equal("ugh?", values[2]);
            Assert.Null(values[3]);


            GC.KeepAlive(m);
        }


        [Fact]
        public void WeakRefWorks()
        {
            var m = new Model();
            var refCont = new int[1];
            ForgetfulTrack(m, refCont);


            m.Name = "Albert";
            m.Other = m;
            m.Other = new Model { Name = "Fubar" };

            GC.Collect();
            m.Other.Name = "ugh?";
            m.Other = null;

            Assert.Equal(2, refCont[0]);
        }
        void ForgetfulTrack(Model m, int[] counter)
        {
            PropertyPath.Watch(m, x => x.Other.Name, x => {
                counter[0]++;
            });
        }



        [Fact]
        public void BindingWorks()
        {
            var m = new Model()
            {
                Other = new Model(),
            };
            var m0 = new Model()
            {
                Other = new Model(),
            };
            var b = PropertyBinding.Create(() => m.Name, () => m0.Name);

            m.Name = "Albert";
            Assert.Equal(m.Name, m0.Name);
            m.Name = "Einstein";
            Assert.Equal(m.Name, m0.Name);

            var m2 = new Model();
            var b2 = PropertyBinding.Create(() => m.Name, () => m2.Name);

            Assert.Equal(m.Name, m2.Name);
            m.Name = "Landau";
            Assert.Equal(m.Name, m2.Name);

            b.Dispose();
            m.Name = "fufuf";
            Assert.NotEqual(m.Name, m0.Name);

            GC.KeepAlive(m);
        }


        [Fact]
        public void WeakRefBindingWorks()
        {
            var m = new Model();
            var m2 = new Model();
            ForgetfulBind(m, m2);

            var s1 = m.Name = "Albert";
            Assert.Equal(s1, m2.Name);

            GC.Collect();
            m.Name = "ugh?";
            m.Other = null;

            Assert.Equal(s1, m2.Name);
        }
        void ForgetfulBind(Model m1, Model m2)
        {
            PropertyBinding.Create(() => m1.Name, () => m2.Name);
        }
    }
}
