using Microsoft.Isam.Esent.Interop;

namespace EsentSerialization
{
	static class Ext
	{
		public static readonly CreateIndexGrbit JET_bitIndexTuples = (CreateIndexGrbit)( 0x1000 );

		public static readonly JET_param JET_paramIndexTuplesLengthMin = (JET_param)( 110 );
		public static readonly JET_param JET_paramIndexTuplesLengthMax = (JET_param)( 111 );
		public static readonly JET_param JET_paramIndexTuplesToIndexMax = (JET_param)( 112 );
	}
}