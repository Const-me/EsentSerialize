using EsentSerialization;
using System;
using System.ComponentModel;

namespace EsentSerialization
{
	// This is the property descriptor for BookmarkedRow<tRow> type.
	// It forwards the GetValue/SetValue request to the underlying tRow object.
	// It was implemented to allow BookmarkedRecordset<> to be a data source for DataGridView controls.
	// Technically that base class is not WinForm-specific. Practically, I'm not sure it's used anywhere except WinForms, hence in this assembly.
	class EsePropertyDescriptor<tRow>
		 : PropertyDescriptor
		where tRow : new()
	{
		readonly PropertyDescriptor pdReal;

		public EsePropertyDescriptor( PropertyDescriptor _real ) :
			base( _real )
		{
			pdReal = _real;
		}

		object objReal( object component )
		{
			if( component is BookmarkedRow<tRow> )
			{
				var eo = component as BookmarkedRow<tRow>;
				return eo.obj;
			}
			return null;
		}

		public override object GetValue( object component )
		{
			return pdReal.GetValue( objReal( component ) );
		}
		public override void SetValue( object component, object value )
		{
			pdReal.SetValue( objReal( component ), value );
		}
		public override void ResetValue( object component )
		{
			pdReal.ResetValue( objReal( component ) );
		}
		public override bool CanResetValue( object component )
		{
			return pdReal.CanResetValue( objReal( component ) );
		}
		public override bool ShouldSerializeValue( object component )
		{
			return pdReal.ShouldSerializeValue( objReal( component ) );
		}
		public override Type PropertyType
		{
			get { return pdReal.PropertyType; }
		}
		public override bool IsReadOnly
		{
			get { return pdReal.IsReadOnly; }
		}
		public override Type ComponentType
		{
			get { return typeof( BookmarkedRow<tRow> ); }
		}
		public override TypeConverter Converter
		{
			get { return pdReal.Converter; }
		}
	}
}