using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using ClashN.Mode;
using ClashN.Tool;

namespace ClashN.Handler
{
    class SpeedtestHandler
    {
        private Config _config;
        private CoreHandler _coreHandler;
        private List<ServerTestItem> _selecteds;
        Action<string, string> _updateFunc;

        public SpeedtestHandler(ref Config config)
        {
            _config = config;
        }

        public SpeedtestHandler(ref Config config, CoreHandler coreHandler, List<ProfileItem> selecteds, ESpeedActionType actionType, Action<string, string> update)
        {
            _config = config;
            _coreHandler = coreHandler;
            //_selecteds = Utils.DeepCopy(selecteds);
            _updateFunc = update;

            _selecteds = new List<ServerTestItem>();
            foreach (var it in selecteds)
            {
                _selecteds.Add(new ServerTestItem()
                {
                    indexId = it.indexId,
                    address = it.address
                });
            }

            if (actionType == ESpeedActionType.Ping)
            {
                Task.Run(() => RunPing());
            }
            else if (actionType == ESpeedActionType.Tcping)
            {
                Task.Run(() => RunTcping());
            }
        }

        private void RunPingSub(Action<ServerTestItem> updateFun)
        {
            try
            {
                foreach (var it in _selecteds)
                {

                    try
                    {
                        updateFun(it);
                    }
                    catch (Exception ex)
                    {
                        Utils.SaveLog(ex.Message, ex);
                    }
                }

                Thread.Sleep(10);
            }
            catch (Exception ex)
            {
                Utils.SaveLog(ex.Message, ex);
            }
        }


        private void RunPing()
        {
            RunPingSub((ServerTestItem it) =>
            {
                var time = Utils.Ping(it.address);

                _updateFunc(it.indexId, FormatOut(time, "ms"));
            });
        }

        private void RunTcping()
        {
            RunPingSub((ServerTestItem it) =>
            {
                var time = GetTcpingTime(it.address, it.port);

                _updateFunc(it.indexId, FormatOut(time, "ms"));
            });
        }


        public int RunAvailabilityCheck() // alias: isLive
        {
            try
            {
                var httpPort = _config.httpPort;

                var t = Task.Run(() =>
                {
                    try
                    {
                        var webProxy = new WebProxy(Global.Loopback, httpPort);
                        var responseTime = -1;
                        var status = GetRealPingTime(LazyConfig.Instance.GetConfig().constItem.speedPingTestUrl, webProxy, out responseTime);
                        var noError = Utils.IsNullOrEmpty(status);
                        return noError ? responseTime : -1;
                    }
                    catch (Exception ex)
                    {
                        Utils.SaveLog(ex.Message, ex);
                        return -1;
                    }
                });
                return t.Result;
            }
            catch (Exception ex)
            {
                Utils.SaveLog(ex.Message, ex);
                return -1;
            }
        }

        private int GetTcpingTime(string url, int port)
        {
            var responseTime = -1;

            try
            {
                if (!IPAddress.TryParse(url, out var ipAddress))
                {
                    var ipHostInfo = System.Net.Dns.GetHostEntry(url);
                    ipAddress = ipHostInfo.AddressList[0];
                }

                var timer = new Stopwatch();
                timer.Start();

                var endPoint = new IPEndPoint(ipAddress, port);
                var clientSocket = new Socket(endPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

                var result = clientSocket.BeginConnect(endPoint, null, null);
                if (!result.AsyncWaitHandle.WaitOne(TimeSpan.FromSeconds(5)))
                    throw new TimeoutException("connect timeout (5s): " + url);
                clientSocket.EndConnect(result);

                timer.Stop();
                responseTime = timer.Elapsed.Milliseconds;
                clientSocket.Close();
            }
            catch (Exception ex)
            {
                Utils.SaveLog(ex.Message, ex);
            }
            return responseTime;
        }

        private string GetRealPingTime(string url, WebProxy webProxy, out int responseTime)
        {
            var msg = string.Empty;
            responseTime = -1;
            try
            {
                var myHttpWebRequest = (HttpWebRequest)WebRequest.Create(url);
                myHttpWebRequest.Timeout = 5000;
                myHttpWebRequest.Proxy = webProxy;//new WebProxy(Global.Loopback, Global.httpPort);

                var timer = new Stopwatch();
                timer.Start();

                var myHttpWebResponse = (HttpWebResponse)myHttpWebRequest.GetResponse();
                if (myHttpWebResponse.StatusCode != HttpStatusCode.OK
                    && myHttpWebResponse.StatusCode != HttpStatusCode.NoContent)
                {
                    msg = myHttpWebResponse.StatusDescription;
                }
                timer.Stop();
                responseTime = timer.Elapsed.Milliseconds;

                myHttpWebResponse.Close();
            }
            catch (Exception ex)
            {
                Utils.SaveLog(ex.Message, ex);
                msg = ex.Message;
            }
            return msg;
        }
        private string FormatOut(object time, string unit)
        {
            if (time.ToString().Equals("-1"))
            {
                return "Timeout";
            }
            return string.Format("{0}{1}", time, unit).PadLeft(8, ' ');
        }
    }
}
