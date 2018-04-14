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

    /// <summary>
    /// A strongly typed subclass of <see cref="PropertyPath"/> for easier use.
    /// </summary>
    /// <typeparam name="T">The type of the root of the path</typeparam>
    /// <typeparam name="TProp">The type of the last property at the end of the path.</typeparam>
    public class PropertyPath<T, TProp> : PropertyPath
    {
        internal PropertyPath()
        {
        }
        /// <summary>
        /// The root or first object of the property path.
        /// </summary>
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
        /// <summary>
        /// The value of the last member of the property path.
        /// </summary>
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

    /// <summary>
    /// A class that make it ease to observe and change a property path, i.e. an expression such as <c>myObject.Property1.Property2.Property3</c>.
    /// Only works with fields and properties.
    /// </summary>
    /// <remarks>
    /// PropertyPaths register weak <see cref="INotifyPropertyChanged"/> on all part of the path to track for change. Change won't be detected if 
    /// this interface is not fired. Very much like XAML UI. They are also weak event, keep a reference to the <see cref="PropertyPath"/>
    /// or it will disappear and no event will be fired.
    /// </remarks>
    public class PropertyPath : INotifyPropertyChanged
    {
        #region Watch()

        /// <summary>
        /// Create a property watch with a method to be called when it <see cref="Value"/> change, with the new value.
        /// </summary>
        /// <typeparam name="T">Type of the root, i.e. first object, of the path</typeparam>
        /// <typeparam name="TP">The type of the last member of the property path.</typeparam>
        /// <param name="root">The root of the path.</param>
        /// <param name="e">The expression (i.e. <c>myObject.A.B.C</c>) that describe the path. Only properties and fields are supported.</param>
        /// <param name="onValueChanged">Action called an time <see cref="Value"/> change.</param>
        /// <returns>The property path instance. Keep it to avoid garbage collection (in which case <paramref name="onValueChanged"/>
        /// will stop being called)</returns>
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
        /// <summary>
        /// Create a property watch with a method to be called when it <see cref="Value"/> change, with the new value.
        /// </summary>
        /// <typeparam name="TP">The type of the last member of the property path.</typeparam>
        /// <param name="e">The expression (i.e. <c>myObject.A.B.C</c>) that describe the path. Only properties and fields are supported.</param>
        /// <param name="onValueChanged">Action called an time <see cref="Value"/> change.</param>
        /// <returns>The property path instance. Keep it to avoid garbage collection (in which case <paramref name="onValueChanged"/>
        /// will stop being called)</returns>
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

        /// <summary>
        /// Creates a string typed <see cref="PropertyPath"/> from a root and an <see cref="Expression{TDelegate}"/>.
        /// Only member path is supported.
        /// </summary>
        /// <typeparam name="T">The type of <see cref="Root"/></typeparam>
        /// <typeparam name="TP">The type of the last member of the path.</typeparam>
        /// <param name="root">The root of the expression.</param>
        /// <param name="e">The member expression to navigate from the root to the value.</param>
        /// <returns>A newly create <see cref="PropertyPath"/></returns>
        public static PropertyPath<T, TP> Create<T, TP>(T root, Expression<Func<T, TP>> e)
        {
            var pv = new PropertyPath<T, TP>();
            pv.Initialize(e);
            pv.Root = root;
            return pv;
        }

        /// <summary>
        /// Creates a string typed <see cref="PropertyPath"/> from a no root (or a static property) and an <see cref="Expression{TDelegate}"/>.
        /// Only member path is supported.
        /// </summary>
        /// <typeparam name="TP">The type of the last member of the path.</typeparam>
        /// <param name="e">A member expression to navigate from to the value.</param>
        /// <returns>A newly create <see cref="PropertyPath"/></returns>
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

        /// <summary>
        /// The root of the path. i.e. if the property path is <c>myObject.A.B.C</c>, it will be the value of <c>myObject</c>.
        /// </summary>
        /// <exception cref="System.InvalidCastException">If one try to set a value that can't be fit in the expression path</exception>
        public object Root
        {
            get { return root; }
            set
            {
                if (Equals(Root, value))
                    return;
                if (value != null && !pvalues[0].Member.Member.DeclaringType.IsInstanceOf(value))
                    throw new InvalidCastException();
                root = value;
                pvalues[0].Object = root;
                OnPropertyChanged();
            }
        }
        object root;

        #endregion

        #region Value

        /// <summary>
        /// The value at the end of the path. i.e. if the property path is <c>myObject.A.B.C</c>, it will be the value of <c>C</c>.
        /// </summary>
        public object Value
        {
            get { return pvalues[pvalues.Length - 1].Value; }
            set { pvalues[pvalues.Length - 1].Value = value; }
        }

        #endregion

        /// <summary>
        /// Gets the elements of the path.
        /// i.e. if the property path is <c>myObject.A.B.C</c>, it will be the values of <c>A, B, C</c>.
        /// </summary>
        public ReadOnlyCollection<PropertyValue> Elements { get { return roValues; } }

        /// <summary>
        /// Occurs when either <see cref="Root"/> or <see cref="Value"/> change.
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        void OnPropertyChanged([CallerMemberName]string pName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(pName));
        }

        #region class PropertyValue

        /// <summary>
        /// Represent each individual path of the property path. 
        ///  i.e. if the property path is <c>myObject.A.B.C</c>, there will be one for <c>A</c>, <c>B</c> and <c>C</c>.
        ///  Can either represent a path using a <see cref="PropertyInfo"/> or <see cref="FieldInfo"/>.
        /// </summary>
        public class PropertyValue
        {
            internal PropertyValue(PropertyPath path, int index, MemberInfo member)
            {
                this.index = index;
                Path = path;

                var ot = FastType.GetType(member.DeclaringType);
                Member = ot.DeclaredMembers[member.Name];
            }
            int index;

            /// <summary>
            /// The <see cref="PropertyPath"/> that owns this <see cref="PropertyValue"/>.
            /// </summary>
            public PropertyPath Path { get; private set; }
            /// <summary>
            /// The <see cref="MemberInfo"/> that this object maps too.
            /// </summary>
            public FastMember Member { get; private set; }
            /// <summary>
            /// <see cref="Member"/>'s name.
            /// </summary>
            public string Name { get { return Member.Name; } }

            void SetMemberValue(object value)
            {
                if (Object == null)
                    return;
                if (!Member.SetValue(Object, Value))
                    throw new InvalidOperationException();
            }
            object GetMemberValue()
            {
                if (Object == null)
                    return null;
                return Member.GetValue(Object);
            }

            /// <summary>
            /// The instance to which this <see cref="Member"/> applies.
            /// </summary>
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

            /// <summary>
            /// The value of the <see cref="Member"/> for <see cref="Object"/>.
            /// </summary>
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

            /// <summary>
            /// Fired when <see cref="Value"/> changes.
            /// </summary>
            public event EventHandler ValueChanged;
        }

        #endregion
    }
}
