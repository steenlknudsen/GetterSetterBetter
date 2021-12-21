using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;

namespace TestProject1
{
    public class GetterSetterBetter<TTarget, TValue>
    {
        public GetterSetterBetter(MemberInfo memberInfo)
        {
            if (memberInfo is FieldInfo)
            {
                var fi = memberInfo as FieldInfo;
                Getter = CreateFieldGetter<TTarget, TValue>(fi);
                Setter = CreateFieldSetter<TTarget, TValue>(fi);
            }
            else if (memberInfo is PropertyInfo)
            {
                var pi = memberInfo as PropertyInfo;

                MethodInfo getter = memberInfo.DeclaringType.GetMethod("get_Value");
                MethodInfo setter = memberInfo.DeclaringType.GetMethod("set_Value");

                Getter = (Func<TTarget, TValue>)Delegate.CreateDelegate(typeof(Func<TTarget, TValue>), null, getter);
                Setter = (Action<TTarget, TValue>)Delegate.CreateDelegate(typeof(Action<TTarget, TValue>), null, setter);
            }
            else
            {
                throw new ArgumentException("The member must be either a Field or a Property, not " + memberInfo.MemberType);
            }
        }


        static Func<S, T> CreateFieldGetter<S, T>(FieldInfo field)
        {
            string methodName = field.ReflectedType.FullName + ".get_" + field.Name;
            DynamicMethod setterMethod = new DynamicMethod(methodName, typeof(T), new Type[1] { typeof(S) }, true);
            ILGenerator gen = setterMethod.GetILGenerator();
            if (field.IsStatic)
            {
                gen.Emit(OpCodes.Ldsfld, field);
            }
            else
            {
                gen.Emit(OpCodes.Ldarg_0);
                gen.Emit(OpCodes.Ldfld, field);
            }
            gen.Emit(OpCodes.Ret);
            return (Func<S, T>)setterMethod.CreateDelegate(typeof(Func<S, T>));
        }

        static Action<S, T> CreateFieldSetter<S, T>(FieldInfo field)
        {
            string methodName = field.ReflectedType.FullName + ".set_" + field.Name;
            DynamicMethod setterMethod = new DynamicMethod(methodName, null, new Type[2] { typeof(S), typeof(T) }, true);
            ILGenerator gen = setterMethod.GetILGenerator();
            if (field.IsStatic)
            {
                gen.Emit(OpCodes.Ldarg_1);
                gen.Emit(OpCodes.Stsfld, field);
            }
            else
            {
                gen.Emit(OpCodes.Ldarg_0);
                gen.Emit(OpCodes.Ldarg_1);
                gen.Emit(OpCodes.Stfld, field);
            }
            gen.Emit(OpCodes.Ret);
            return (Action<S, T>)setterMethod.CreateDelegate(typeof(Action<S, T>));
        }

        /// <summary>
        /// The delegate for getting the value of the member
        /// </summary>
        private Func<TTarget, TValue> Getter;

        /// <summary>
        /// The delegate for setting the value of the member
        /// </summary>
        private Action<TTarget, TValue> Setter;

        /// <summary>
        /// Get the value of the member on a provided object.
        /// </summary>
        /// <param name="p_obj">The object to query for the member value</param>
        /// <returns>The value of the member on the provided object</returns>
        public TValue Get(TTarget p_obj)
        {
            return Getter(p_obj);
        }

        /// <summary>
        /// Set the value on a given object to a given value.
        /// </summary>
        /// <param name="p_obj">The object whose member value to set</param>
        /// <param name="p_value">The value to assign to the member</param>
        public void Set(TTarget p_obj, TValue p_value)
        {
            Setter(p_obj, p_value);
        }
    

    }
}
