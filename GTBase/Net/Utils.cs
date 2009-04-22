using System;
using System.Collections.Generic;
using System.Diagnostics;
using GT.Utils;
using Common.Logging;

namespace GT.Net.Utils
{
    /// <summary>
    /// A simple utility class that drops transports that do not respond to
    /// GT's periodic heart-beat within a certain time.  This utility uses
    /// GT's events to automatically become aware of new connexions and
    /// transports.  The utility is installed onto a <see cref="Client"/> 
    /// or <see cref="Server"/> through <see cref="Install(GT.Net.Communicator,System.TimeSpan)"/>.
    /// </summary>
    public class PingBasedDisconnector
    {
        /// <summary>
        /// Install a disconnector on the provided communicator.
        /// </summary>
        /// <param name="c"></param>
        /// <param name="timeout"></param>
        /// <returns></returns>
        public static PingBasedDisconnector Install(Communicator c, TimeSpan timeout)
        {
            PingBasedDisconnector instance = new PingBasedDisconnector(c, timeout);
            instance.Start();
            return instance;
        }


        /// <summary>
        /// Provide notification of any errors that may occur.
        /// </summary>
        public event ErrorEventNotication ErrorEvent;
        
        protected ILog log;
        protected Communicator comm;
        protected TimeSpan timeout;
        protected WeakKeyDictionary<ITransport, Stopwatch> timers =
            new WeakKeyDictionary<ITransport, Stopwatch>();

        protected PingBasedDisconnector(Communicator c, TimeSpan timeout)
        {
            log = LogManager.GetLogger(GetType());

            comm = c;
            this.timeout = timeout;
        }

        /// <summary>
        /// Start the disconnector.
        /// </summary>
        public void Start()
        {
            if (timeout.CompareTo(comm.PingInterval) < 0)
            {
                NotifyError(new ErrorSummary(Severity.Warning, SummaryErrorCode.Configuration, 
                    "Timeout period is less than the ping time", null));
            }
            comm.Tick += _comm_Tick;
            comm.ConnexionAdded += _comm_ConnexionAdded;
            comm.ConnexionRemoved += _comm_ConnexionRemoved;
            foreach (IConnexion cnx in comm.Connexions)
            {
                _comm_ConnexionAdded(comm, cnx);
            }
        }

        /// <summary>
        /// Stop the disconnector.
        /// </summary>
        public void Stop()
        {
            comm.Tick -= _comm_Tick;
            comm.ConnexionAdded -= _comm_ConnexionAdded;
            comm.ConnexionRemoved -= _comm_ConnexionRemoved;
            foreach(IConnexion cnx in comm.Connexions)
            {
                _comm_ConnexionRemoved(comm, cnx);
            }
        }

        private void _comm_Tick(Communicator obj)
        {
            foreach (ITransport t in timers.Keys) 
            {
                Stopwatch sw = timers[t];
                if (timeout.CompareTo(sw.Elapsed) >= 0)
                {
                    log.Warn(String.Format("Stopped transport {0}: no ping received in {1}",
                        t, sw.Elapsed));
                    t.Dispose();
                }
            }
        }

        private void _comm_ConnexionAdded(Communicator c, IConnexion cnx)
        {
            cnx.TransportAdded += _cnx_TransportAdded;
            cnx.TransportRemoved += _cnx_TransportRemoved;
            cnx.PingRequested += _cnx_PingRequested;
            cnx.PingReceived += _cnx_PingReceived;
            foreach(ITransport t in cnx.Transports)
            {
                _cnx_TransportAdded(cnx, t);
            }
        }

        private void _cnx_TransportAdded(IConnexion connexion, ITransport transport)
        {
            timers[transport] = Stopwatch.StartNew();
        }

        private void _cnx_TransportRemoved(IConnexion connexion, ITransport transport)
        {
            timers.Remove(transport);
        }

        private void _comm_ConnexionRemoved(Communicator c, IConnexion conn)
        {
            conn.PingRequested -= _cnx_PingRequested;
            conn.PingReceived -= _cnx_PingReceived;
        }

        private void _cnx_PingReceived(ITransport transport, uint sequence, TimeSpan roundtrip)
        {
            Stopwatch sw;
            if (!timers.TryGetValue(transport, out sw))
            {
                log.Warn("Unexpected: no record of pinged transport: " + transport);
                return;
            }
            sw.Reset();
            sw.Start();
        }

        private void _cnx_PingRequested(ITransport transport, uint sequence)
        {
            if (!timers.ContainsKey(transport))
            {
                timers[transport] = Stopwatch.StartNew();
            }
        }

        protected void NotifyError(ErrorSummary es)
        {
            if (ErrorEvent == null)
            {
                es.LogTo(log);
                return;
            }

            try { ErrorEvent(es); }
            catch (Exception e)
            {
                log.Warn("Exception occurred when processing application ErrorEvent handlers", e);
                ErrorEvent(new ErrorSummary(Severity.Information, SummaryErrorCode.UserException,
                    "Exception occurred when processing application ErrorEvent handlers", e));
            }
        }

    }
}