#if NET472 || NETCOREAPP2_1
// EMIT is only on .NET Standard 2.1

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace Galador.Reflection.Utils
{
    // http://theinstructionlimit.com/fast-net-reflection

    delegate object MethodHandler(object target, params object[] args);

    static class EmitHelper
    {
        static readonly Module Module = typeof(EmitHelper).GetTypeInfo().Module;
        static readonly Type[] SingleObject = new[] { typeof(object) };
        static readonly Type[] TwoObjects = new[] { typeof(object), typeof(object) };
        static readonly Type[] ManyObjects = new[] { typeof(object), typeof(object[]) };

        static EmitHelper()
        {
            try
            {
                var dynam = new DynamicMethod(string.Empty, typeof(int), new Type[] { typeof(int) }, Module, true);
                ILGenerator il = dynam.GetILGenerator();
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Mul);
                il.Emit(OpCodes.Ret);
                var sq = (Func<int, int>)dynam.CreateDelegate(typeof(Func<int, int>));
                if (sq(2) == 4)
                {
                    SupportsEmit = true;
                }
            }
            catch { }
        }
        public static bool SupportsEmit { get; private set; }

#region CreateMethodHandler() CreateParameterlessConstructorHandler()

        public static MethodHandler CreateMethodHandler(MethodBase method, bool ctorDoNotCreate = false)
        {
            var dynam = new DynamicMethod(string.Empty, typeof(object), ManyObjects, Module, true);
            ILGenerator il = dynam.GetILGenerator();

            ParameterInfo[] args = method.GetParameters();

            Label argsOK = il.DefineLabel();

            il.Emit(OpCodes.Ldarg_1);
            il.Emit(OpCodes.Ldlen);
            il.Emit(OpCodes.Ldc_I4, args.Length);
            il.Emit(OpCodes.Beq, argsOK);

            il.Emit(OpCodes.Newobj, typeof(TargetParameterCountException).GetTypeInfo().GetConstructor(Type.EmptyTypes));
            il.Emit(OpCodes.Throw);

            il.MarkLabel(argsOK);

            if (!method.IsConstructor || ctorDoNotCreate)
                il.PushInstance(method.DeclaringType);

            for (int i = 0; i < args.Length; i++)
            {
                il.Emit(OpCodes.Ldarg_1);
                il.Emit(OpCodes.Ldc_I4, i);
                il.Emit(OpCodes.Ldelem_Ref);
                il.UnboxIfNeeded(args[i].ParameterType);
            }

            if (method.IsConstructor)
            {
                if (ctorDoNotCreate)
                {
                    il.Emit(OpCodes.Call, method as ConstructorInfo);
                    throw new NotImplementedException("This is not yet working ... :'(");
                }
                else
                {
                    il.Emit(OpCodes.Newobj, method as ConstructorInfo);
                }
            }
            else if (method.IsFinal || !method.IsVirtual)
            {
                il.Emit(OpCodes.Call, method as MethodInfo);
            }
            else
            {
                il.Emit(OpCodes.Callvirt, method as MethodInfo);
            }

            Type returnType = method.IsConstructor ? method.DeclaringType : (method as MethodInfo).ReturnType;
            if (returnType != typeof(void))
                il.BoxIfNeeded(returnType);
            else
                il.Emit(OpCodes.Ldnull);

            il.Emit(OpCodes.Ret);

            return (MethodHandler)dynam.CreateDelegate(typeof(MethodHandler));
        }

        public static Func<object> CreateParameterlessConstructorHandler(Type type)
        {
            var dynam = new DynamicMethod(string.Empty, typeof(object), Type.EmptyTypes, Module, true);
            ILGenerator il = dynam.GetILGenerator();

            if (type.GetTypeInfo().IsValueType)
            {
                il.DeclareLocal(type);
                il.Emit(OpCodes.Ldloc_0);
                il.Emit(OpCodes.Box, type);
            }
            else
                il.Emit(OpCodes.Newobj, type.GetTypeInfo().GetConstructor(Type.EmptyTypes));

            il.Emit(OpCodes.Ret);

            return (Func<object>)dynam.CreateDelegate(typeof(Func<object>));
        }
        public static Func<object> CreateParameterlessConstructorHandler(ConstructorInfo ctor)
        {
            var dynam = new DynamicMethod(string.Empty, typeof(object), Type.EmptyTypes, Module, true);
            ILGenerator il = dynam.GetILGenerator();

            var type = ctor.DeclaringType;
            if (type.GetTypeInfo().IsValueType)
            {
                il.DeclareLocal(type);
                il.Emit(OpCodes.Ldloc_0);
                il.Emit(OpCodes.Box, type);
            }
            else
                il.Emit(OpCodes.Newobj, ctor);

            il.Emit(OpCodes.Ret);

            return (Func<object>)dynam.CreateDelegate(typeof(Func<object>));
        }

#endregion

#region CreateFieldSetterHandler() CreatePropertySetterHandler() CreateFieldGetterHandler() CreatePropertyGetterHandler()

        public static Action<object, object> CreateFieldSetterHandler(FieldInfo fieldInfo)
        {
            var dynam = new DynamicMethod(string.Empty, typeof(void), TwoObjects, Module, true);
            ILGenerator il = dynam.GetILGenerator();

            if (!fieldInfo.IsStatic)
                il.PushInstance(fieldInfo.DeclaringType);

            il.Emit(OpCodes.Ldarg_1);
            il.UnboxIfNeeded(fieldInfo.FieldType);
            il.Emit(OpCodes.Stfld, fieldInfo);
            il.Emit(OpCodes.Ret);

            return (Action<object, object>)dynam.CreateDelegate(typeof(Action<object, object>));
        }

        public static Func<object, object> CreateFieldGetterHandler(FieldInfo fieldInfo)
        {
            var dynam = new DynamicMethod(string.Empty, typeof(object), SingleObject, Module, true);
            ILGenerator il = dynam.GetILGenerator();

            if (!fieldInfo.IsStatic)
                il.PushInstance(fieldInfo.DeclaringType);

            il.Emit(OpCodes.Ldfld, fieldInfo);
            il.BoxIfNeeded(fieldInfo.FieldType);
            il.Emit(OpCodes.Ret);

            return (Func<object, object>)dynam.CreateDelegate(typeof(Func<object, object>));
        }

        public static Action<object, object> CreatePropertySetterHandler(PropertyInfo propertyInfo)
        {
            var dynam = new DynamicMethod(string.Empty, typeof(void), TwoObjects, Module, true);
            ILGenerator il = dynam.GetILGenerator();
            MethodInfo methodInfo = propertyInfo.SetMethod;

            if (!methodInfo.IsStatic)
                il.PushInstance(propertyInfo.DeclaringType);

            il.Emit(OpCodes.Ldarg_1);
            il.UnboxIfNeeded(propertyInfo.PropertyType);

            if (methodInfo.IsFinal || !methodInfo.IsVirtual)
                il.Emit(OpCodes.Call, methodInfo);
            else
                il.Emit(OpCodes.Callvirt, methodInfo);
            il.Emit(OpCodes.Ret);

            return (Action<object, object>)dynam.CreateDelegate(typeof(Action<object, object>));
        }

        public static Func<object, object> CreatePropertyGetterHandler(PropertyInfo propertyInfo)
        {
            var dynam = new DynamicMethod(string.Empty, typeof(object), SingleObject, Module, true);
            ILGenerator il = dynam.GetILGenerator();
            MethodInfo methodInfo = propertyInfo.GetMethod;

            if (!methodInfo.IsStatic)
                il.PushInstance(propertyInfo.DeclaringType);

            if (methodInfo.IsFinal || !methodInfo.IsVirtual)
                il.Emit(OpCodes.Call, methodInfo);
            else
                il.Emit(OpCodes.Callvirt, methodInfo);

            il.BoxIfNeeded(propertyInfo.PropertyType);
            il.Emit(OpCodes.Ret);

            return (Func<object, object>)dynam.CreateDelegate(typeof(Func<object, object>));
        }

#endregion

#region Private Helpers

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static void PushInstance(this ILGenerator il, Type type)
        {
            il.Emit(OpCodes.Ldarg_0);
            if (type.GetTypeInfo().IsValueType)
                il.Emit(OpCodes.Unbox, type);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static void BoxIfNeeded(this ILGenerator il, Type type)
        {
            if (type.GetTypeInfo().IsValueType)
                il.Emit(OpCodes.Box, type);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static void UnboxIfNeeded(this ILGenerator il, Type type)
        {
            if (type.GetTypeInfo().IsValueType)
                il.Emit(OpCodes.Unbox_Any, type);
        }

#endregion

#region addition: Create[Field|Property][Getter|Setter]<T>()

        public static Action<object, T> CreateFieldSetter<T>(FieldInfo member)
        {
            var dynam = new DynamicMethod(string.Empty, typeof(void), new Type[] { typeof(object), typeof(T) }, Module, true);
            ILGenerator il = dynam.GetILGenerator();

            if (!member.IsStatic)
                il.PushInstance(member.DeclaringType);
            il.Emit(OpCodes.Ldarg_1);
            il.Emit(OpCodes.Stfld, member);
            il.Emit(OpCodes.Ret);

            return (Action<object, T>)dynam.CreateDelegate(typeof(Action<object, T>));
        }
        public static Func<object, T> CreateFieldGetter<T>(FieldInfo member)
        {
            var dynam = new DynamicMethod(string.Empty, typeof(T), new Type[] { typeof(object) }, Module, true);
            ILGenerator il = dynam.GetILGenerator();

            if (!member.IsStatic)
                il.PushInstance(member.DeclaringType);
            il.Emit(OpCodes.Ldfld, member);
            il.Emit(OpCodes.Ret);

            return (Func<object, T>)dynam.CreateDelegate(typeof(Func<object, T>));
        }
        public static Action<object, T> CreatePropertySetter<T>(PropertyInfo member)
        {
            var dynam = new DynamicMethod(string.Empty, typeof(void), new Type[] { typeof(object), typeof(T) }, Module, true);
            ILGenerator il = dynam.GetILGenerator();

            var method = member.SetMethod;
            if (!method.IsStatic)
                il.PushInstance(method.DeclaringType);
            il.Emit(OpCodes.Ldarg_1);
            if (method.IsFinal || !method.IsVirtual) il.Emit(OpCodes.Call, method);
            else il.Emit(OpCodes.Callvirt, method);
            il.Emit(OpCodes.Ret);

            return (Action<object, T>)dynam.CreateDelegate(typeof(Action<object, T>));
        }
        public static Func<object, T> CreatePropertyGetter<T>(PropertyInfo member)
        {
            var dynam = new DynamicMethod(string.Empty, typeof(T), new Type[] { typeof(object) }, Module, true);
            ILGenerator il = dynam.GetILGenerator();

            var method = member.GetMethod;
            if (!method.IsStatic)
                il.PushInstance(method.DeclaringType);
            if (method.IsFinal || !method.IsVirtual) il.Emit(OpCodes.Call, method);
            else il.Emit(OpCodes.Callvirt, method);
            il.Emit(OpCodes.Ret);

            return (Func<object, T>)dynam.CreateDelegate(typeof(Func<object, T>));
        }

#endregion
    }
}

#endif