using System;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;
using Xunit;

namespace TestProject1
{
    public class Target
    {
        public int Value { get; set; }
        public string Text { get; set; }

        public int FieldValue;
    }

    public class UnitTest1
    {
        [Fact]
        public void Test1()
        {

        }

        

        static void Test(string[] args)
        {
            FieldInfo valueField = typeof(Target).GetFields(BindingFlags.NonPublic | BindingFlags.Instance).First();
            var getValue = CreateGetter<Target, int>(valueField);
            var setValue = CreateSetter<Target, int>(valueField);

            Target target = new Target();

            setValue(target, 42);
            Console.WriteLine(getValue(target));
        }

        static Func<S, T> CreateGetter<S, T>(FieldInfo field)
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

        static Action<S, T> CreateSetter<S, T>(FieldInfo field)
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

        [Fact]
        static void TestDelegate()
        {
            int max = 10000000;
            int i = 0;

            Target target = new Target() { Value = 42, Text = "xxx" };


            Func<Target, int> getValue;
            Action<Target, int> setValue;

            
            Type t = typeof(Target);
            MethodInfo getter = t.GetMethod("get_Value");
            MethodInfo setter = t.GetMethod("set_Value");

            getValue = (Func<Target, int>)Delegate.CreateDelegate(typeof(Func<Target, int>), null, getter);
            setValue = (Action<Target, int>)Delegate.CreateDelegate(typeof(Action<Target, int>), null, setter);
            
            //
            //var members = typeof(Target).GetMembers();
            //var fields = typeof(Target).GetMembers();
            //FieldInfo valueField = typeof(Target).GetFields(BindingFlags.Public | BindingFlags.Instance).Where(w => w.Name == "Value").First();
            //var getValue = CreateGetter<Target, int>(valueField);
            //var setValue = CreateSetter<Target, int>(valueField);

            Stopwatch sw = Stopwatch.StartNew();

            for (i = 0; i < max; i++)
            {
                var xget = getValue(target);
                setValue(target, i);
            }

            sw.Stop();
            Debug.WriteLine("time delegate: " + sw.ElapsedMilliseconds + " ms");
        }

        [Fact]
        public void TestGetValue()
        {
            int max = 10000000;
            int i = 0;

            Target t = new Target(){Value =42,Text = "xxx"};
            Expression<Func<Target, int>> expression = target => target.Value;
            Func<Target, int> func = (Target x) => { return x.Value; };
            PropertyInfo propertyInfo = GetProperty(expression);

            Stopwatch sw = Stopwatch.StartNew();

            for (i = 0; i < max; i++)
            {
                var xget = propertyInfo.GetValue(t);
                propertyInfo.SetValue(t, i);
            }

            sw.Stop();
            Debug.WriteLine("time setvalue: " + sw.ElapsedMilliseconds + " ms" );
        }

        [Fact]
        public void TestGetFieldValue()
        {
            int max = 10000000;
            int i = 0;

            Target t = new Target() { Value = 42, Text = "xxx", FieldValue = 43};
            Expression<Func<Target, int>> expression = target => target.FieldValue;
            Func<Target, int> func = (Target x) => { return x.FieldValue; };
            FieldInfo propertyInfo = GetField(expression);

            Stopwatch sw = Stopwatch.StartNew();

            for (i = 0; i < max; i++)
            {
                var xget = propertyInfo.GetValue(t);
                propertyInfo.SetValue(t, i);
            }

            sw.Stop();
            Debug.WriteLine("time set FieldValue: " + sw.ElapsedMilliseconds + " ms");
        }

        private static PropertyInfo GetProperty<TEntity, TProperty>(Expression<Func<TEntity, TProperty>> expression)
        {
            var member = GetMemberExpression(expression).Member;
            return member as PropertyInfo ?? throw new InvalidOperationException($"Member with Name '{member.Name}' is not a property.");
        }

        private static FieldInfo GetField<TEntity, TProperty>(Expression<Func<TEntity, TProperty>> expression)
        {
            var member = GetMemberInfoFromExpression(expression);
            //Debug.WriteLine(member.NodeType);
            //Debug.WriteLine(member.CanReduce);
            //Debug.WriteLine(member.Type.Name);
            return member as FieldInfo;
            //return member as FieldInfo ?? throw new InvalidOperationException($"Member with Name '{member.Name}' is not a property.");
        }


        private static MemberInfo GetMemberInfoFromExpression<TEntity, TProperty>(Expression<Func<TEntity, TProperty>> expression)
        {
            MemberInfo memberInfo = null;
            if (expression.Body.NodeType == ExpressionType.Convert)
            {
                var body = (UnaryExpression)expression.Body;
                //memberExpression = body.Operand as MemberExpression;
            }
            else if (expression.Body.NodeType == ExpressionType.MemberAccess)
            {
                Debug.WriteLine(expression.Body.Type.Name);
                Debug.WriteLine(expression.Body.GetType().Name);
                Debug.WriteLine(expression.Body.NodeType);
                if (expression.Body is MemberExpression bodyMemberExpression)
                {
                    Debug.WriteLine(bodyMemberExpression.Member);
                    Debug.WriteLine(bodyMemberExpression.Member.GetType().Name);
                    //Debug.WriteLine(bodyMemberExpression.Member);
                    memberInfo = bodyMemberExpression.Member;
                }
                else
                {
                    //memberExpression = expression.Body as MemberExpression;
                }

            }

            if (memberInfo == null)
                throw new ArgumentException("Not memberInfo", nameof(MemberInfo));

            return memberInfo;
        }

        private static MemberExpression GetMemberExpression<TEntity, TProperty>(Expression<Func<TEntity, TProperty>> expression)
        {
            MemberExpression memberExpression = null;
            if (expression.Body.NodeType == ExpressionType.Convert)
            {
                var body = (UnaryExpression)expression.Body;
                memberExpression = body.Operand as MemberExpression;
            }
            else if (expression.Body.NodeType == ExpressionType.MemberAccess)
            {
                Debug.WriteLine(expression.Body.Type.Name);
                Debug.WriteLine(expression.Body.GetType().Name);
                Debug.WriteLine(expression.Body.NodeType);
                if (expression.Body is MemberExpression bodyMemberExpression)
                {
                    Debug.WriteLine(bodyMemberExpression.Member);
                    Debug.WriteLine(bodyMemberExpression.Member.GetType().Name);
                    //Debug.WriteLine(bodyMemberExpression.Member);
                    memberExpression = expression.Body as MemberExpression;
                }
                else
                {
                    memberExpression = expression.Body as MemberExpression;
                }
                
            }

            if (memberExpression == null)
                throw new ArgumentException("Not a member access", nameof(memberExpression));

            return memberExpression;
        }

        [Fact]
        static void TestProperty()
        {
            int max = 10000000;
            int i = 0;

            Target target = new Target() { Value = 42, Text = "xxx" };


            Stopwatch sw = Stopwatch.StartNew();

            for (i = 0; i < max; i++)
            {
                var xget = target.Value;
                target.Value = i;
            }

            sw.Stop();
            Debug.WriteLine("time property: " + sw.ElapsedMilliseconds + " ms");
        }

        //CGetterSetter
        [Fact]
        static void TestCGetterSetter()
        {
            int max = 10000000;
            int i = 0;

            Target target = new Target() { Value = 42, Text = "xxx" };

            var valueMember = typeof(Target).GetMembers(BindingFlags.Public | BindingFlags.Instance).Where(w => w.Name == "Value").First();

            CGetterSetter cGetterSetter = new CGetterSetter(valueMember);

            Stopwatch sw = Stopwatch.StartNew();

            for (i = 0; i < max; i++)
            {
                var xget = cGetterSetter.Get(target);
                cGetterSetter.Set(target, i);
            }

            sw.Stop();
            Debug.WriteLine("time CGetterSetter: " + sw.ElapsedMilliseconds + " ms");
        }

        [Fact]
        static void TestGetterSetterBetter()
        {
            int max = 10000000;
            int i = 0;

            Target target = new Target() { Value = 42, Text = "xxx" };

            var valueMember = typeof(Target).GetMembers(BindingFlags.Public | BindingFlags.Instance).Where(w => w.Name == "Value").First();

            var cGetterSetter = new GetterSetterBetter<Target, int>(valueMember);

            Stopwatch sw = Stopwatch.StartNew();

            for (i = 0; i < max; i++)
            {
                var xget = cGetterSetter.Get(target);
                cGetterSetter.Set(target, i);
            }

            sw.Stop();
            Debug.WriteLine("time GetterSetterBetter: " + sw.ElapsedMilliseconds + " ms");
        }

        [Fact]
        static void TestGetterSetterBetterField()
        {
            int max = 10000000;
            int i = 0;

            Target target = new Target() { Value = 42, Text = "xxx", FieldValue = 43};

            var valueMember = typeof(Target).GetMembers(BindingFlags.Public | BindingFlags.Instance).Where(w => w.Name == "FieldValue").First();

            var cGetterSetter = new GetterSetterBetter<Target, int>(valueMember);

            Stopwatch sw = Stopwatch.StartNew();

            for (i = 0; i < max; i++)
            {
                var xget = cGetterSetter.Get(target);
                cGetterSetter.Set(target, i);
            }

            sw.Stop();
            Debug.WriteLine("time GetterSetterBetter Field: " + sw.ElapsedMilliseconds + " ms");
        }

        [Fact]
        static void TestField()
        {
            int max = 10000000;
            int i = 0;

            Target target = new Target() { Value = 42, Text = "xxx", FieldValue = 43};


            Stopwatch sw = Stopwatch.StartNew();

            for (i = 0; i < max; i++)
            {
                var xget = target.FieldValue;
                target.FieldValue = i;
            }

            sw.Stop();
            Debug.WriteLine("time Field: " + sw.ElapsedMilliseconds + " ms");
        }
    }
}
