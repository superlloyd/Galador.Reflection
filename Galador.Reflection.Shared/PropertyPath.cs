using Galador.Reflection.Logging;
using Galador.Reflection.Utils;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace Galador.Reflection
{
    #region PropertyPath<T>

    public class PropertyPath<T, TProp> : PropertyPath
    {
        internal PropertyPath()
        {
        }
        public new T Root
        {
            get
            {
                var v = base.Value;
                if (v is T)
                    return (T)v;
                return default(T);
            }
            set { base.Root = value; }
        }
        public new TProp Value
        {
            get
            {
                var v = base.Value;
                if (v is TProp)
                    return (TProp)v;
                return default(TProp);
            }
            set { base.Value = value; }
        }
    }

    #endregion

    public class PropertyPath : INotifyPropertyChanged
    {
        #region Watch()

        public static PropertyPath<T, TP> Watch<T, TP>(T root, Expression<Func<T, TP>> e, Action<TP> onValueChanged)
        {
            var pv = new PropertyPath<T, TP>();
            pv.PropertyChanged += (o, eargs) =>
            {
                if (string.IsNullOrEmpty(eargs.PropertyName) || eargs.PropertyName == "Value")
                {
                    onValueChanged(pv.Value);
                }
            };
            pv.Initialize(e);
            pv.Root = root;
            return pv;
        }
        public static PropertyPath<DBNull, TP> Watch<TP>(Expression<Func<TP>> e, Action<TP> onValueChanged)
        {
            var pv = new PropertyPath<DBNull, TP>();
            pv.PropertyChanged += (o, eargs) =>
            {
                if (string.IsNullOrEmpty(eargs.PropertyName) || eargs.PropertyName == "Value")
                {
                    onValueChanged(pv.Value);
                }
            };
            pv.Initialize(e);
            return pv;
        }

        #endregion

        #region Create()

        public static PropertyPath<T, TP> Create<T, TP>(T root, Expression<Func<T, TP>> e)
        {
            var pv = new PropertyPath<T, TP>();
            pv.Initialize(e);
            pv.Root = root;
            return pv;
        }
        public static PropertyPath<DBNull, TP> Create<TP>(Expression<Func<TP>> e)
        {
            var pv = new PropertyPath<DBNull, TP>();
            pv.Initialize(e);
            return pv;
        }

        #endregion

        #region ctor()

        internal PropertyPath()
        {
        }
        void Initialize(LambdaExpression e)
        {
            object root;
            var path = ReflectionEx.GetLambdaPath(e, out root);
            if (path.Length == 0)
                throw new ArgumentException();
            pvalues = new PropertyValue[path.Length];
            roValues = new ReadOnlyCollection<PropertyValue>(pvalues);
            for (int i = 0; i < path.Length; i++)
            {
                var meIndex = i;
                var pv = new PropertyValue(this, i, path[i]);
                pvalues[i] = pv;
            }
            Root = root;
        } 

        #endregion

        PropertyValue[] pvalues;
        ReadOnlyCollection<PropertyValue> roValues;

        #region Root

        public object Root
        {
            get { return root; }
            set
            {
                if (Equals(Root, value))
                    return;
                if (value != null && !pvalues[0].Member.DeclaringType.IsInstanceOf(value))
                    throw new InvalidCastException();
                root = value;
                pvalues[0].Object = root;
                OnPropertyChanged();
            }
        }
        object root;
        /**/
        #endregion

        #region Value

        public object Value
        {
            get { return pvalues[pvalues.Length - 1].Value; }
            set { pvalues[pvalues.Length - 1].Value = value; }
        }

        #endregion

        public ReadOnlyCollection<PropertyValue> Elements { get { return roValues; } }

        public event PropertyChangedEventHandler PropertyChanged;

        void OnPropertyChanged([CallerMemberName]string pName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(pName));
        }

        #region class PropertyValue

        public class PropertyValue
        {
            internal PropertyValue(PropertyPath path, int index, MemberInfo member)
            {
                this.index = index;
                Path = path;
                Member = member;
            }
            int index;
            public PropertyPath Path { get; private set; }
            public MemberInfo Member { get; private set; }
            public string Name { get { return Member.Name; } }

            void SetMemberValue(object value)
            {
                if (Object == null)
                    return;
                if (Member is FieldInfo)
                    ((FieldInfo)Member).SetValue(Object, value);
                else if (Member is PropertyInfo)
                    ((PropertyInfo)Member).SetValue(Object, value);
                else
                    throw new InvalidOperationException();
            }
            object GetMemberValue()
            {
                if (Object == null)
                    return null;
                else if (Member is FieldInfo)
                    return ((FieldInfo)Member).GetValue(Object);
                else if (Member is PropertyInfo)
                    return ((PropertyInfo)Member).GetValue(Object, null);
                else
                    throw new InvalidOperationException();
            }

            public object Object
            {
                get { return mObject; }
                internal set
                {
                    if (value == mObject)
                        return;

                    if (mObject is INotifyPropertyChanged)
                    {
                        ((INotifyPropertyChanged)mObject).PropertyChanged -= watcher;
                        watcher = null;
                    }
                    mObject = value;
                    UpdateValue();
                    if (mObject is INotifyPropertyChanged)
                    {
                        watcher = WeakEvents.AddWeakHandler((INotifyPropertyChanged)mObject, this, (x, args) =>
                        {
                            if (args.PropertyName == null || args.PropertyName == x.Name)
                                x.UpdateValue();
                        });
                    }
                }
            }
            object mObject;
            PropertyChangedEventHandler watcher;

            void UpdateValue()
            {
                var v = GetMemberValue();
                if (Equals(v, Value))
                    return;
                mValue = v;
                OnValueChanged();
            }

            public object Value
            {
                get { return mValue; }
                internal set
                {
                    if (Equals(value, Value))
                        return;
                    SetMemberValue(value);
                    mValue = value;
                    OnValueChanged();
                }
            }
            object mValue;

            void OnValueChanged()
            {
                if (index == Path.Elements.Count - 1)
                    Path.OnPropertyChanged(nameof(Value));
                else
                    Path.Elements[index + 1].Object = Value;
                ValueChanged?.Invoke(this, EventArgs.Empty);
            }
            public event EventHandler ValueChanged;
        }

        #endregion
    }
}
