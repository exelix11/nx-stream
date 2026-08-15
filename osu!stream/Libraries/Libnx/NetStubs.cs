using System;
using System.Diagnostics;
using System.Net.Http;
using System.Text;
using System.Threading;
using osum.Helpers;

namespace osum.Libraries.NetLib
{
	public class AbortedException : Exception
	{
		public AbortedException()
			: base("Request has been aborted")
		{
		}
	}

	public static class NetManager
    {
        private const int MAX_CONCURRENT_REQUESTS = 3;

        public static bool ReportCompleted(NetRequest request)
        {
            return true;
        }

        public static bool AddRequest(NetRequest request)
        {
            request.Perform();
            return true;
        }
    }

	public abstract class NetRequest
	{
		public string m_url;
		internal Thread thread;
		internal bool AbortRequested;

		public NetRequest(string _url)
		{
			m_url = _url;
			Logging.Write("URL: " + _url);
		}

		public abstract void Perform();

		public virtual void Abort()
		{
			AbortRequested = true;
			NetManager.ReportCompleted(this);
		}

		public abstract bool Valid();
		public abstract void OnException(Exception e);

		public delegate void RequestStartHandler();
		public delegate void RequestUpdateHandler(object sender, long current, long total);
		public static string UrlEncode(string s) => s;
	}

	public class DataNetRequest : NetRequest
	{
		private readonly string method;
		private readonly string postData;

		public DataNetRequest(string _url, string method = "GET", string postData = null)
			: base(_url)
		{
			this.method = method;
			this.postData = postData;
		}

		public event RequestStartHandler onStart;
		public event RequestUpdateHandler onUpdate;
		public event RequestCompleteHandler onFinish;

		public byte[] data;
		public Exception error;

		public override void Perform()
		{
			try
			{
				onStart?.Invoke();
				error = new Exception("Net requests are not supported on libnx yet.");
			}
			catch (ThreadAbortException)
			{
			}
			catch (Exception e)
			{
				error = e;
			}
			
			processFinishedRequest();
		}

		public virtual void processFinishedRequest()
		{
			NetManager.ReportCompleted(this);

			if (AbortRequested) return;

			GameBase.Scheduler.Add(delegate
			{
				onFinish?.Invoke(data, error);
			});
		}

		public override bool Valid()
		{
			return true;
		}

		public override void OnException(Exception e)
		{
			processFinishedRequest();
		}

		public delegate void RequestCompleteHandler(byte[] data, Exception e);
	}

	public class StringNetRequest : DataNetRequest
    {
        public StringNetRequest(string _url, string method = "GET", string postData = null)
            : base(_url, method, postData) {}

        public new event RequestCompleteHandler onFinish;

        public override void processFinishedRequest()
        {
            NetManager.ReportCompleted(this);

            if (AbortRequested) return;

            GameBase.Scheduler.Add(delegate
            {
                if (onFinish != null)
                {
                    string output = null;
                    if (data != null && error == null)
                        output = Encoding.UTF8.GetString(data);
                    onFinish(output, error);
                }
            });
        }

        public delegate void RequestCompleteHandler(string _result, Exception e);
    }

	public class FileNetRequest : DataNetRequest
    {
        private readonly string path;

        public FileNetRequest(string path, string url, string method = "GET", string postData = null) : base(url, method, postData)
        {
            this.path = path;
        }
    }
}