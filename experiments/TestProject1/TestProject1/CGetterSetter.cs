using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;

namespace TestProject1
{
    /// <summary>
    /// For a given class and an expression identifying a property on that class, where the
    /// property is of any type, this class will allow rapid access to that property both
    /// for retrieval (get) and assignment (set).
    /// </summary>
    /// <remarks>
    /// Running tests show the following performance indications for Release-mode builds:
    /// 
    /// <code>
    /// Using a "raw" lambda for get/set 
    ///     var lambdaSetter = new Action{MyType, object}( ( _p, _v ) => _p.Something = (int) _v );
    ///     var lambdaGetter = new Func{MyType, object}( _p => _p.Something );
    ///     
    ///     lambdaSetter( obj, lambdaGetter( obj ) );
    /// 
    /// 
    /// Using this class
    ///     var access = new CGetterSetter(typeof(MyObj)).GetField("MyField");
    ///     
    ///     var val = access.Get( obj );
    ///     access.Set( obj, newVal );
    /// 
    /// 
    /// Using Reflection/PropertyInfo
    ///     var pi = GetType(MyType).GetProperty("Something");
    ///     
    ///     pi.SetValue( obj, pi.GetValue( obj, null ), null );
    ///     
    /// </code>
    /// 
    /// <code>
    /// Results based on the "raw lambda" being the baseline:
    /// 
    /// Using this CGetterSetter is ~2.91 times slower
    /// Using Reflection is ~108 times slower than app-provided lambda
    /// Using Reflection is ~37 times slower than CGetterSetter
    /// 
    /// </code>
    /// 
    /// The EXTREME performance increase over Reflection makes this class a very viable
    /// alternative. Further, other tests showed that there is ZERO performance hit when
    /// using the GET functionality- The SET functionality is where performance is hit.
    /// </remarks>


    /*Performance over 1 Billion runs (100 Million for Reflection). Numbers in Nanoseconds.
 
       /--------------------------------------------------+-------------------------\
       |                                                  |                         |
       V                                                  ^                         ^

     Lambda   RefMultiple             CGS    Multiple      Reflection   Multiple
     2,908  <---    1.00  Ref Field Get   4,373     1.50        107,830    37.08 
     5,160      ^   1.77  Ref Field Set  10,390     2.01        162,050    31.41 
     2,930      |   1.01  Val Field Get  11,198     3.82        145,780    49.75 
     3,363      |   1.16  Val Field Set  13,325     3.96        197,900    58.85 
     2,916      |   1.00  Ref Prop Get   6,324     2.17        495,730   170.00 
     5,214      |   1.79  Ref Prop Set   9,585     1.84        663,660   127.28 
     2,922      |   1.00  Val Prop Get  13,410     4.59        539,100   184.50 
     3,092      |   1.06  Val Prop Set  14,483     4.68        758,740   245.39 

Summaries
     11,676 <---    1.00  Getters          35,305  3.02      1,288,440   110.35 
     16,829  ^   1.44  Setters          47,783  2.84      1,782,350   105.91 
     16,198  |   1.39  Reference Types  30,672  1.89      1,429,270    88.24 
     12,307  |   1.05  Value Types      52,416  4.26      1,641,520   133.38 
     14,361  |   1.23  Fields          39,286  2.74        613,560    42.72 
     14,144  |   1.21  Properties      43,802  3.10      2,457,230   173.73 

     28,505           All              83,088     2.91        3,070,790    107.73 

    */


    /// <summary>
    /// A Property or Field accessor object that uses compiled Expressions to access a
    /// property or a field. Properties are assumed to have both getter and setter methods
    /// for the respective methods to be available. The access modifiers do not matter, as
    /// the application must already have a <see cref="PropertyInfo"/> or a
    /// <see cref="FieldInfo"/> object to use this class.
    /// </summary>
    public class CGetterSetter
    {
        /// <summary>
        /// Get the helper method signature one time
        /// </summary>
        private static MethodInfo sm_valueAssignerMethod =
            typeof(CGetterSetter)
            .GetMethod("ValueAssigner", BindingFlags.Static | BindingFlags.NonPublic);

        /// <summary>
        /// This is the internal method responsible for assigning one value to a member.
        /// This is required to make this class compiant with .NET 3.5 (Unity3d compatible)
        /// </summary>
        /// <typeparam name="T">The Type of the values to assign</typeparam>
        /// <param name="dest">The destination member</param>
        /// <param name="src">The value to assign</param>
        private static void ValueAssigner<T>(out T dest, T src)
        {
            dest = src;
        }


        /// <summary>
        /// The delegate for getting the value of the member
        /// </summary>
        private Func<object, object> m_getter;

        /// <summary>
        /// The delegate for setting the value of the member
        /// </summary>
        private Action<object, object> m_setter;

        /// <summary>
        /// Get the value of the member on a provided object.
        /// </summary>
        /// <param name="p_obj">The object to query for the member value</param>
        /// <returns>The value of the member on the provided object</returns>
        public object Get(object p_obj)
        {
            return m_getter(p_obj);
        }

        /// <summary>
        /// Set the value on a given object to a given value.
        /// </summary>
        /// <param name="p_obj">The object whose member value to set</param>
        /// <param name="p_value">The value to assign to the member</param>
        public void Set(object p_obj, object p_value)
        {
            m_setter(p_obj, p_value);
        }

        /// <summary>
        /// Construct a new member accessor based on a Reflection MemberInfo- either a
        /// PropertyInfo or a FieldInfo
        /// </summary>
        /// <param name="p_member">
        /// A PropertyInfo or a FieldInfo describing the member to access
        /// </param>
        public CGetterSetter(MemberInfo p_member)
        {
            if (p_member == null)
                throw new ArgumentNullException("Must initialize with a non-null Field or Property");

            MemberExpression exMember = null;

            if (p_member is FieldInfo)
            {
                var fi = p_member as FieldInfo;
                var assignmentMethod = sm_valueAssignerMethod.MakeGenericMethod(fi.FieldType);

                Init(fi.DeclaringType, fi.FieldType,
                    _ex => exMember = Expression.Field(_ex, fi), // Create a Field expression, and SAVE that field expression for the Call expression
                    (_, _val) => Expression.Call(assignmentMethod, exMember, _val) // We're going to call the static "ValueAssigner" method on this class
                );
            }
            else if (p_member is PropertyInfo)
            {
                var pi = p_member as PropertyInfo;
                var assignmentMethod = pi.GetSetMethod();

                Init(pi.DeclaringType, pi.PropertyType,
                    _ex => exMember = Expression.Property(_ex, pi), // Create a Property expression
                    (_obj, _val) => Expression.Call(_obj, assignmentMethod, _val) // We're going to call the SetMethod on the PropertyInfo object
                );
            }
            else
            {
                throw new ArgumentException("The member must be either a Field or a Property, not " + p_member.MemberType);
            }
        }


        /// <summary>
        /// Internal initialization routine. The difference between Field and Property
        /// access is extremely similar, but just different enough to require the two
        /// delegates back into the calling routine provide the specialized information.
        /// </summary>
        /// <param name="p_objectType">
        /// The Type of the objects that will have this member accessed
        /// </param>
        /// <param name="p_valueType">The Type of the member</param>
        /// <param name="p_fnGetMember">
        /// A delegate that returns the correct Expression for the member- either
        /// <see cref="Expression.Property"/> or <see cref="Expression.Field"/>
        /// </param>
        /// <param name="p_fnMakeCallExpression">
        /// Get a method that actually calls the Assignment function appropriate for the
        /// MemberType. The order of the parameters for Fields vs Properties is slightly
        /// different, as the Field assignment is static while the Property assignment is an
        /// instance method.
        /// </param>
        private void Init(
            Type p_objectType,
            Type p_valueType,
            Func<Expression, MemberExpression> p_fnGetMember,
            Func<Expression, Expression, MethodCallExpression> p_fnMakeCallExpression)
        {
            var exObjParam = Expression.Parameter(typeof(object), "theObject");
            var exValParam = Expression.Parameter(typeof(object), "theProperty");

            var exObjConverted = Expression.Convert(exObjParam, p_objectType);
            var exValConverted = Expression.Convert(exValParam, p_valueType);

            Expression exMember = p_fnGetMember(exObjConverted);

            Expression getterMember = p_valueType.IsValueType ? Expression.Convert(exMember, typeof(object)) : exMember;
            m_getter = Expression.Lambda<Func<object, object>>(getterMember, exObjParam).Compile();

            Expression exAssignment = p_fnMakeCallExpression(exObjConverted, exValConverted);
            m_setter = Expression.Lambda<Action<object, object>>(exAssignment, exObjParam, exValParam).Compile();
        }

#if false // The following code was refactored because of the extreme similarities between the methods.
        public CGenGetterSetter( MemberInfo p_member )
        {
            if (p_member == null)
                throw new ArgumentNullException( "Must initialize with a non-null Field or Property" );

            if (p_member is FieldInfo)
                InitAsField( p_member as FieldInfo );
            else if (p_member is PropertyInfo)
                InitAsProperty( p_member as PropertyInfo );
            else
                throw new ArgumentException( "The member must be either a Field or a Property, not " + p_member.MemberType );
        }


        private void InitAsProperty( PropertyInfo p_propertyInfo )
        {
            var objType = p_propertyInfo.DeclaringType;
            var valType = p_propertyInfo.PropertyType;

            var assignmentMethod = p_propertyInfo.GetSetMethod();



            var exObjParam = Expression.Parameter( typeof( object ), "theObject" );
            var exValParam = Expression.Parameter( typeof( object ), "theProperty" );

            var exObjConverted = Expression.Convert( exObjParam, objType );
            var exValConverted = Expression.Convert( exValParam, valType );

            /**/
            Expression exMember = Expression.Property( exObjConverted, p_propertyInfo );

            Expression getterMember = valType.IsValueType ? Expression.Convert( exMember, typeof( object ) ) : exMember;
            m_getter = Expression.Lambda<Func<object, object>>( getterMember, exObjParam ).Compile();

            /**/
            Expression exAssignment = Expression.Call( exObjConverted, assignmentMethod, exValConverted );
            m_setter = Expression.Lambda<Action<object, object>>( exAssignment, exObjParam, exValParam ).Compile();
        }

        private void InitAsField( FieldInfo p_fieldInfo )
        {
            var objType = p_fieldInfo.DeclaringType;
            var valType = p_fieldInfo.FieldType;

            var assignmentMethod = typeof( CGenGetterSetter )
                            .GetMethod( "ValueAssigner", BindingFlags.Static | BindingFlags.NonPublic )
                            .MakeGenericMethod( valType );



            var exObjParam = Expression.Parameter( typeof( object ), "theObject" );
            var exValParam = Expression.Parameter( typeof( object ), "theProperty" );

            var exObjConverted = Expression.Convert( exObjParam, objType );
            var exValConverted = Expression.Convert( exValParam, valType );

            /**/
            Expression exMember = Expression.Field( exObjConverted, p_fieldInfo );

            Expression getterMember = valType.IsValueType ? Expression.Convert( exMember, typeof( object ) ) : exMember;
            m_getter = Expression.Lambda<Func<object, object>>( getterMember, exObjParam ).Compile();

            /**/
            var exAssignment = Expression.Call( assignmentMethod, exMember, exValConverted );
            m_setter = Expression.Lambda<Action<object, object>>( exAssignment, exObjParam, exValParam ).Compile();
        }
#endif

    }
}
