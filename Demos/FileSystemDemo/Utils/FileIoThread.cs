using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.IO;
using EseFileSystem;

interface iFileIo: IDisposable
{
	long Read( Stream sOutput, string pathSourceFile );
	long Write( Stream sInput, string pathDestFile );
}

class TrivialFileIo: iFileIo
{
	public long Read( Stream sOutput, string pathSourceFile )
	{
		using( var sInput = new FileStream( pathSourceFile, FileMode.Open, FileAccess.Read ) )
		{
			EFS.CopyStream( sInput, sOutput );
			return sInput.Length;
		}
	}

	public long Write( Stream sInput, string pathDestFile )
	{
		using( var sOutput = new FileStream( pathDestFile, FileMode.CreateNew, FileAccess.Write ) )
		{
			EFS.CopyStream( sInput, sOutput );
			return sOutput.Length;
		}
	}

	public void Dispose()
	{
	}
}

class FileIoThread : iFileIo
{
	Thread m_thread;

	ManualResetEvent threadWakeUp, threadDone;
	bool bShouldQuit = false;

	public FileIoThread()
	{
		threadWakeUp = new ManualResetEvent( false );
		threadDone = new ManualResetEvent( false );
		m_thread = new Thread( ThreadProc );
		m_thread.Start();
	}

	ProducerConsumer pc = new ProducerConsumer();

	FileStream m_fileStream = null;

	bool bReading;

	public long Read( Stream sOutput, string pathSourceFile )
	{
		threadDone.Reset();

		long res = 0;
		using( FileStream fs = new FileStream( pathSourceFile, FileMode.Open, FileAccess.Read ) )
		{
			res = fs.Length;
			bReading = true;
			m_fileStream = fs;
			threadWakeUp.Set();

			byte[] buff;
			int len;
			do
			{
				pc.consGetChunk( out buff, out len );
				if( len > 0 )
					sOutput.Write( buff, 0, len );
				pc.consFreeChunk();
			}
			while( len > 0 );

			m_fileStream = null;
		}
		return res;
	}

	public long Write( Stream sInput, string pathDestFile )
	{
		threadDone.Reset();

		long res = 0;
		using( FileStream fs = new FileStream( pathDestFile, FileMode.CreateNew, FileAccess.Write ) )
		{
			res = fs.Length;

			bReading = false;
			m_fileStream = fs;
			threadWakeUp.Set();

			while( true )
			{
				int read = sInput.Read( pc.prodGetBuffer(), 0, pc.bufferLength );
				pc.prodSubmitChunk( read );
				if( read <= 0 )
					break;
			}

			threadDone.WaitOne();
			fs.Flush();

			m_fileStream = null;
		}
		return res;
	}

	void ThreadProc()
	{
		Thread.CurrentThread.Name = "FileIoThread.ThreadProc";

		while( true )
		{
			threadWakeUp.WaitOne();
			threadWakeUp.Reset();

			if( bShouldQuit )
				return;

			byte[] buff;
			int len;

			if( bReading )
			{
				do
				{
					buff = pc.prodGetBuffer();
					len = m_fileStream.Read( buff, 0, pc.bufferLength );
					pc.prodSubmitChunk( len );
				}
				while( len > 0 );
			}
			else
			{
				do
				{
					pc.consGetChunk( out buff, out len );
					if( len > 0 )
						m_fileStream.Write( buff, 0, len );
					pc.consFreeChunk();
				}
				while( len > 0 );
			}

			threadDone.Set();
		}
	}

	public void Dispose()
	{
		if( null != m_thread )
		{
			bShouldQuit = true;
			threadWakeUp.Set();
			m_thread.Join();
			m_thread = null;
		}

		if( null != threadWakeUp )
		{
			IDisposable ds = threadWakeUp;
			ds.Dispose();
			threadWakeUp = null;
		}

		if( null != threadDone )
		{
			IDisposable ds = threadDone;
			ds.Dispose();
			threadWakeUp = null;
		}
	}
}