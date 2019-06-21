//This program is free software: you can redistribute it and/or modify
//it under the terms of the GNU Lesser General Public License as published by
//the Free Software Foundation, either version 3 of the License, or
//(at your option) any later version.

//This program is distributed in the hope that it will be useful,
//but WITHOUT ANY WARRANTY; without even the implied warranty of
//MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//GNU Lesser General Public License for more details.

// Copyright (c) Petr Bena benapetr@gmail.com

using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Threading;
using System.Xml;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace XmlRcs
{
    /// <summary>
    /// Information about error
    /// </summary>
    public class ErrorEventArgs : EventArgs
    {
        private bool isFatal;
        private string message;
        /// <summary>
        /// Whether the error was fatal or not,
        /// fatal errors mean that XmlRcs
        /// daemon stopped working
        /// </summary>
        public bool Fatal { get { return this.isFatal; } }
        /// <summary>
        /// Message of the error
        /// </summary>
        public string Message { get { return message; } }

        /// <summary>
        /// Creates a new instance of this
        /// </summary>
        /// <param name="fatal"></param>
        /// <param name="ex"></param>
        public ErrorEventArgs(bool fatal, string ex)
        {
            this.isFatal = fatal;
            this.message = ex;
        }
    }

    /// <summary>
    /// Event args used when only a single string needs to be passed to client
    /// </summary>
    public class MessageEventArgs : EventArgs
    {
        public string Message;

        public MessageEventArgs(string text)
        {
            this.Message = text;
        }
    }

    /// <summary>
    /// Contains information about the RC item
    /// </summary>
    public class EditEventArgs : EventArgs
    {
        public RecentChange Change;

        public EditEventArgs(RecentChange change)
        {
            this.Change = change;
        }
    }

    public class ExEventArgs : EventArgs
    {
        public Exception Exception;

        public ExEventArgs(Exception exception)
        {
            this.Exception = exception;
        }
    }

    public class Provider
    {
        private StreamReader streamReader = null;
        private StreamWriter streamWriter = null;
        private NetworkStream networkStream = null;
        private TcpClient client = null;
        private DateTime lastPing;
        private List<string> lSubscriptions = new List<string>();
        private bool autoconn;
        public bool AutoResubscribe;
        private bool disconnecting = false;
        private CancellationTokenSource close = new CancellationTokenSource();

        public delegate void EditHandler(object sender, EditEventArgs args);
        public delegate void TimeoutErrorHandler(object sender, EventArgs args);
        public delegate void ErrorHandler(object sender, XmlRcs.ErrorEventArgs args);
        public delegate void OKHandler(object sender, XmlRcs.MessageEventArgs args);
        public delegate void ExceptionHandler(object sender, XmlRcs.ExEventArgs args);
        public event EditHandler On_Change;
        public event OKHandler On_OK;
        public event TimeoutErrorHandler On_Timeout;
        public event ErrorHandler On_Error;
        public event ExceptionHandler On_Exception;

        public List<string> Subscriptions
        {
            get
            {
                if (!this.IsConnected)
                    return new List<string>();

                // we copy a local list
                return new List<string>(this.lSubscriptions);
            }
        }

        public bool IsConnected
        {
            get
            {
                if (this.client == null)
                    return false;
                if (this.lastPing.AddSeconds(Configuration.PingTimeout) < DateTime.Now)
                {
                    // server timed out
                    this.__evt_Timeout();
                    this.kill();
                    return false;
                }
                return (this.client.Client.Connected);
            }
        }

        /// <summary>
        /// Creates a new provider
        /// </summary>
        /// <param name="autoreconnect">if true the provider will automatically try to reconnect in case it wasn't connected</param>
        /// <param name="autoresubscribe"></param>
        public Provider(bool autoreconnect = false, bool autoresubscribe = false)
        {
            this.autoconn = autoreconnect;
            this.AutoResubscribe = autoresubscribe;
        }

        ~Provider()
        {
            // clean up some resources - this will ensure that all references to thread will be removed
            this.Disconnect();
        }

        private void ping()
        {
            this.send("ping");
        }

        private bool isConnected()
        {
            if (!this.IsConnected)
            {
                if (!this.autoconn || this.disconnecting)
                    return false;
                return this.Connect();
            }
            return true;
        }

        private void __evt_ok(string text)
        {
            // check if there is some event for this
            if (this.On_OK != null)
            {
                this.On_OK(this, new MessageEventArgs(text));
            }
        }

        private void __evt_Error(XmlRcs.ErrorEventArgs er)
        {
            if (this.On_Error != null)
                this.On_Error(this, er);
        }

        private void __evt_Exception(Exception er)
        {
            if (this.On_Exception != null)
                this.On_Exception(this, new ExEventArgs(er));
        }

        private void __evt_Timeout()
        {
            if (this.On_Timeout != null)
                this.On_Timeout(this, new EventArgs());
        }

        private void __evt_Edit(RecentChange change)
        {
            if (this.On_Change != null)
            {
                // send a signal
                // everywhere
                this.On_Change(this, new EditEventArgs(change));
            }
        }

        private void send(string data)
        {
            if (!this.IsConnected)
                return;
            lock (this.streamWriter)
                this.streamWriter.WriteLine(data);
        }

        private static int TryParseIS(string input)
        {
            int result;
            if (!int.TryParse(input, out result))
                result = 0;
            return result;
        }

        private void processOutput(string data)
        {
            // put the text into XML document
            XmlDocument document = new XmlDocument();
            this.lastPing = DateTime.Now;
            document.LoadXml(data);
            switch (document.DocumentElement.Name)
            {
                case "ping":
                    this.send("pong");
                    break;
                case "fatal":
                    this.__evt_Error(new ErrorEventArgs(true, document.DocumentElement.InnerText));
                    break;
                case "error":
                    this.__evt_Error(new ErrorEventArgs(false, document.DocumentElement.InnerText));
                    break;
                case "ok":
                    this.__evt_ok(document.DocumentElement.InnerText);
                    break;
                case "edit":
                    {
                        RecentChange rc = new RecentChange();
                        foreach (XmlAttribute item in document.DocumentElement.Attributes)
                        {
                            switch (item.Name)
                            {
                                case "wiki":
                                    rc.Wiki = item.Value;
                                    break;
                                case "server_name":
                                    rc.ServerName = item.Value;
                                    break;
                                case "summary":
                                    rc.Summary = item.Value;
                                    break;
                                case "revid":
                                    rc.RevID = TryParseIS(item.Value);
                                    break;
                                case "oldid":
                                    rc.OldID = TryParseIS(item.Value);
                                    break;
                                case "title":
                                    rc.Title = item.Value;
                                    break;
                                case "namespace":
                                    rc.Namespace = TryParseIS(item.Value);
                                    break;
                                case "user":
                                    rc.User = item.Value;
                                    break;
                                case "bot":
                                    rc.Bot = bool.Parse(item.Value);
                                    break;
                                case "patrolled":
                                    rc.Patrolled = bool.Parse(item.Value);
                                    break;
                                case "minor":
                                    rc.Minor = bool.Parse(item.Value);
                                    break;
                                case "type":
                                    {
                                        switch (item.Value.ToLower())
                                        {
                                            case "new":
                                                rc.Type = RecentChange.ChangeType.New;
                                                break;
                                            case "log":
                                                rc.Type = RecentChange.ChangeType.Log;
                                                break;
                                            case "edit":
                                                rc.Type = RecentChange.ChangeType.Edit;
                                                break;
                                        }
                                    }
                                    break;
                                case "length_new":
                                    rc.LengthNew = TryParseIS(item.Value);
                                    break;
                                case "length_old":
                                    rc.LengthOld = TryParseIS(item.Value);
                                    break;
                                case "timestamp":
                                    rc.Timestamp = Configuration.UnixTimeStampToDateTime(double.Parse(item.Value));
                                    break;
                            }
                        }
                        rc.OriginalXml = data;
                        this.__evt_Edit(rc);
                    }
                    break;
            }
        }

        private async Task Pinger_Exec()
        {
            try
            {
                while (IsConnected && !close.IsCancellationRequested)
                {
                    this.ping();
                    await Task.Delay(TimeSpan.FromSeconds(Configuration.PingWait), close.Token);
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception fail)
            {
                this.__evt_Exception(fail);
            }
        }

        /// <summary>
        /// Connect to XmlRcs server, this function needs to be called before you can start subscribing to changes on wiki
        /// </summary>
        /// <returns>True on success</returns>
        public bool Connect()
        {
            if (this.IsConnected)
                return false;

			this.lastPing = DateTime.Now;
			try
			{
				this.client = new TcpClient(Configuration.Server, Configuration.Port);
				this.networkStream = this.client.GetStream();
				this.streamReader = new StreamReader(this.networkStream, Encoding.UTF8);
				this.streamWriter = new StreamWriter(this.networkStream, Encoding.ASCII);
				this.streamWriter.AutoFlush = true;
				// there is some weird bug in .Net that put garbage to first packet that is sent out
				// this is a dummy line that will flush out that garbage
				this.send("pong");
				if (this.AutoResubscribe)
				{
					foreach (string item in this.lSubscriptions)
					{
						this.send("S " + item);
					}
				}
                Task.Run(Provider_Exec);
                Task.Run(Pinger_Exec);
			}
			catch
			{
				return false;
			}
            return true;
        }

        private async Task Provider_Exec()
        {
            try
            {
                while (!this.streamReader.EndOfStream && this.IsConnected && !close.IsCancellationRequested)
                {
                    this.processOutput(await this.streamReader.ReadLineAsync());
                }
            }
            catch (Exception fail)
            {
                __evt_Exception(fail);
            }
            this.kill();
			// if we auto reconnect let's do that
			if (this.autoconn && !this.disconnecting)
				this.Connect();
        }

        /// <summary>
        /// Subscribe to a wiki, you can also use a magic word "all" in order to subscribe to all wikis
        /// </summary>
        /// <param name="wiki"></param>
        /// <returns></returns>
        public bool Subscribe(string wiki)
        {
            if (!this.isConnected())
                return false;
            if (!this.lSubscriptions.Contains(wiki))
                this.lSubscriptions.Add(wiki);
            this.send("S " + wiki);
            return true;
        }

        /// <summary>
        /// Remove a subscription to a site, if you use magic word "all" you will remove subscription to "all wikis" except wikis you explicitly requested
        /// in a separate "Subscribe" calls
        /// </summary>
        /// <param name="wiki"></param>
        /// <returns></returns>
        public bool Unsubscribe(string wiki)
        {
            if (!this.isConnected())
                return false;
            if (this.lSubscriptions.Contains(wiki))
                this.lSubscriptions.Remove(wiki);
            this.send("D " + wiki);
            return true;
        }

        private void kill()
        {
            if (this.client != null)
                this.client.Close();
            this.networkStream = null;
            this.streamReader = null;
            this.streamWriter = null;
            this.client = null;
            close.Cancel();
        }

        /// <summary>
        /// Disconnect from server
        /// </summary>
        public void Disconnect()
        {
            this.disconnecting = true;
            if (!this.IsConnected)
            {
                this.disconnecting = false;
                return;
            }
            this.send("exit");
            this.kill();
            this.disconnecting = false;
        }

        /// <summary>
        /// Reconnect to XmlRcs server, alias to Disconnect and Connect
        /// </summary>
        /// <returns></returns>
        public bool Reconnect()
        {
            this.Disconnect();
            return this.Connect();
        }
    }
}
