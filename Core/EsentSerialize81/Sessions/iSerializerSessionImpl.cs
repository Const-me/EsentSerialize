namespace EsentSerialization
{
	// Private session interface exposed to the SerializerTransaction object
	interface iSerializerSessionImpl : iSerializerSession
	{
		int onTransactionBegin( iSerializerTransaction trans );

		void onTransactionEnd( int lvl, bool bCommitted );
	}
}