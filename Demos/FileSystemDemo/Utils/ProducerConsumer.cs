using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

// A simple cross-thread producer-consumer buffers.
class ProducerConsumer
{
	const int s_bufferLength = 256 * 1024;
	public readonly int bufferLength = s_bufferLength;

	// This state info is here only for debugging.
	enum eBuffState
	{
		free,
		lockedProducer,
		produced,
		lockedConsumer,
		consumed = free
	}

	class Buff
	{
		public Buff()
		{
			buff = new byte[ s_bufferLength ];
			state = eBuffState.free;
		}
		byte[] buff;

		int buffUsedLength = 0;

		public eBuffState state { get; private set; }

		public byte[] prodGetBuffer()
		{
			state = eBuffState.lockedProducer;
			return buff;
		}

		public void prodSubmitChunk( int nBufferUsed )
		{
			buffUsedLength = nBufferUsed;
			state = eBuffState.produced;
		}

		public bool consGetChunk( out byte[] _buff, out int len )
		{
			_buff = this.buff;
			len = this.buffUsedLength;
			state = eBuffState.lockedConsumer;
			return ( len > 0 );
		}

		public void consFreeChunk()
		{
			buffUsedLength = 0;
			state = eBuffState.consumed;
		}
	}

	Buff[] m_buffs = new Buff[ 2 ] { new Buff(), new Buff() };

	Semaphore semFree = new Semaphore( 2, 2 );

	Semaphore semProduced = new Semaphore( 0, 2 );

	int prodNextBuffer = 0;

	public byte[] prodGetBuffer()
	{
		semFree.WaitOne();
		return m_buffs[ prodNextBuffer ].prodGetBuffer();
	}

	public void prodSubmitChunk( int nBufferUsed )
	{
		m_buffs[ prodNextBuffer ].prodSubmitChunk( nBufferUsed );
		prodNextBuffer ^= 1;
		semProduced.Release();
	}

	int consNextBuffer = 0;

	public bool consGetChunk( out byte[] buff, out int len )
	{
		semProduced.WaitOne();
		return m_buffs[ consNextBuffer ].consGetChunk( out buff, out len );
	}

	// You must call consFreeChunk even after the consGetChunk returned false..
	public void consFreeChunk()
	{
		m_buffs[ consNextBuffer ].consFreeChunk();
		consNextBuffer ^= 1;
		semFree.Release();
	}
}