using EsentSerialization.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;

namespace EsentSerialization
{
	internal static class ReflectionUtils
	{
		public static EseColumnAttrubuteBase[] getColumnAttributes( this MemberInfo mi )
		{
			return mi.GetCustomAttributes<EseColumnAttrubuteBase>().ToArray();
		}

		public static EseColumnAttrubuteBase getColumnAttribute( this MemberInfo mi )
		{
			return mi.GetCustomAttributes<EseColumnAttrubuteBase>().First();
		}

		public static EseTableAttribute getTableAttribute( this Type tp )
		{
			if( null == tp )
				return null;
			EseTableAttribute[] attrs = tp
				.GetTypeInfo()
				.GetCustomAttributes<EseTableAttribute>(true).ToArray();
			if( null == attrs || attrs.Length <= 0 )
				return null;
			if( attrs.Length > 1 )
				throw new SerializationException( "The [EseTable] attribute is applied to '" + tp.Name + "' type more then once." );
			return attrs[ 0 ];
		}

		/// <summary>Check whether the property suitable for ESENT column.</summary>
		static bool isFieldOk( FieldInfo fi )
		{
			if( fi.IsSpecialName )
				return false;
			if( fi.IsStatic )
				return false;
			if( fi.IsInitOnly )
				return false;
			return true;
		}

		/// <summary>Check whether the property suitable for ESENT column.</summary>
		static bool isPropertyOk( PropertyInfo pi )
		{
			if( pi.IsSpecialName )
				return false;
			if( !pi.CanRead )
				return false;
			if( !pi.CanWrite )
				return false;
			if( pi.GetMethod.IsStatic )
				return false;
			return true;
		}

		static IEnumerable<MemberInfo> enumMembersInType( this TypeInfo ti )
		{
			return ti.DeclaredFields
				.Where( isFieldOk )
				.Cast<MemberInfo>()
				.Concat( ti.DeclaredProperties
					.Where( isPropertyOk )
					.Cast<MemberInfo>()
				);
		}

		/// <summary>Enumerate base types + this type, starting from root base class (but skip System.Object for being too obvious).</summary>
		static IEnumerable<TypeInfo> includeBaseClasses( this TypeInfo ti )
		{
			Stack<TypeInfo> types = new Stack<TypeInfo>( 4 );
			while( true )
			{
				types.Push( ti );
				Type t = ti.BaseType;
				if( null == t || t == typeof( object ) )
					break;
				ti = t.GetTypeInfo();
			}
			return types;
		}

		/// <summary>Enumerate all public non-static writeable fields + properties, including those of base classes.</summary>
		public static IEnumerable<MemberInfo> enumMembers( this Type tp )
		{
			return tp.GetTypeInfo()
				.includeBaseClasses()
				.SelectMany( ti => ti.enumMembersInType() );
		}

		/// <summary>Get the attributes, including those applied to base classes.</summary>
		public static IEnumerable<T> getCustomAttributes<T>( this Type tp ) where T : Attribute
		{
			return tp.GetTypeInfo()
				.includeBaseClasses()
				.SelectMany( ti => ti.GetCustomAttributes<T>() );
		}

		public static bool isGenericType( this Type tp )
		{
#if NETFX_CORE
			return tp.GetTypeInfo().IsGenericType;
#else
			return tp.IsGenericType;
#endif
		}
	}
}