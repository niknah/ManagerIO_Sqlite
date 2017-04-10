using System;
using System.Net;
using System.Threading;

namespace ManagerIO_Sqlite
{
	public class WebServer
	{
		private readonly HttpListener _listener = new HttpListener();
		private readonly Func<HttpListenerContext, byte[]> _responderMethod;

		public WebServer(string[] prefixes, Func<HttpListenerContext, byte[]> method)
		{
			if (!HttpListener.IsSupported)
				throw new NotSupportedException(
					"Needs Windows XP SP2, Server 2003 or later.");

			// URI prefixes are required, for example 
			// "http://localhost:8080/index/".
			if (prefixes == null || prefixes.Length == 0)
				throw new ArgumentException("prefixes");

			// A responder method is required
			if (method == null)
				throw new ArgumentException("method");

			foreach (string s in prefixes)
				_listener.Prefixes.Add(s);

			_responderMethod = method;
			_listener.Start();
		}

		public WebServer(Func<HttpListenerContext, byte[]> method, params string[] prefixes)
			: this(prefixes, method) { }

		public void Run()
		{
			ThreadPool.QueueUserWorkItem((o) =>
				{
					Console.WriteLine("Webserver running...");
					try
					{
						while (_listener.IsListening)
						{
							ThreadPool.QueueUserWorkItem((c) =>
								{
									var ctx = c as HttpListenerContext;
									try
									{
										byte[] buf = _responderMethod(ctx);
										ctx.Response.OutputStream.Write(buf, 0, buf.Length);
									}
									catch(Exception e) {
										byte[] buf = System.Text.Encoding.UTF8.GetBytes(e.ToString());
										ctx.Response.OutputStream.Write(buf, 0, buf.Length);
									} // suppress any exceptions
									finally
									{
										// always close the stream
										ctx.Response.OutputStream.Close();
									}
								}, _listener.GetContext());
						}
					}
					catch { } // suppress any exceptions
				});
		}

		public void Stop()
		{
			_listener.Stop();
			_listener.Close();
		}
	}
}

