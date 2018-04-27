using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Web;

namespace AWebServer
{
    class Program
    {
        public static void Main(string[] args)
        {
            int port = 801;
            AIniLoader ini = new AIniLoader();
            ini.LoadIniFile("config.ini");
            port = ini.OnGetIntValue("port");
            ini.OnSetDefaultValue("root", "i://wwwroot");
            ini.OnSaveBack();
            if (args.Length > 0)
            {
                int.TryParse(args[0], out port);
            }
            new AWebServer(port, ini.OnGetValue("root"));
        }

        private static void Run()
        {
            while (true)
            {
                string str = Console.ReadLine();
                if (str == "exit")
                {
                    Environment.Exit(0);
                    return;
                }
            }
        }
    }
    public class AWebServer
    {
        Socket _socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        public static string RootDir;
        public AWebServer(int port, string rootDir)
        {
            RootDir = rootDir;
            try
            {
                _socket.Bind(new IPEndPoint(IPAddress.Any, port));
                Console.WriteLine("Listening port " + port + "\r\n");
                _socket.Listen(100);
                _socket.BeginAccept(new AsyncCallback(OnAccept), _socket);
                while (true)
                {
                    string s = Console.ReadLine();
                    if (s == "q")
                    {
                        break;
                    }
                    else if (s == "r")
                    {
                        AWebApis.Apis.ReloadStaticPages();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message + "\r\n will retry after 2 seconds, ctrl+c to break.");
                //Environment.Exit(0);

                Thread.Sleep(2000);
                new AWebServer(port, rootDir);
            }
        }

        private void OnAccept(IAsyncResult ar)
        {
            try
            {
                Socket socket = ar.AsyncState as Socket;
                Socket new_client = socket.EndAccept(ar);

                socket.BeginAccept(new AsyncCallback(OnAccept), socket);

                byte[] recv_buffer = new byte[1024 * 640];
                int real_recv = new_client.Receive(recv_buffer);
                string recv_request = Encoding.UTF8.GetString(recv_buffer, 0, real_recv);
                Console.WriteLine("[" + DateTime.Now.ToLongDateString() + " " + DateTime.Now.ToLongTimeString() + "]");
                Console.WriteLine(recv_request);

                Resolve(recv_request, new_client);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Web Error:" + ex.Message + "\r\n\r\n");
            }
        }

        Dictionary<string, Func<string, string>> _dActions;
        Dictionary<string, Func<string, string>> dActions
        {
            get
            {
                if (_dActions == null)
                {
                    _dActions = new Dictionary<string, Func<string, string>>();
                    _dActions.Add("wikilogin", AWebApis.Apis.WikiLogin);
                    _dActions.Add("wikiregist", AWebApis.Apis.WikiRegist);
                    _dActions.Add("wikilist", AWebApis.Apis.WikiList);
                    _dActions.Add("wikiread", AWebApis.Apis.WikiRead);
                    _dActions.Add("wikiwrite", AWebApis.Apis.WikiWrite);
                    _dActions.Add("wikimodify", AWebApis.Apis.WikiModify);
                    _dActions.Add("wikilogout", AWebApis.Apis.WikiLogout);
                    _dActions.Add("wikinew", AWebApis.Apis.WikiNew);
                    _dActions.Add("wikivalidate", AWebApis.Apis.WikiValidate);
                    _dActions.Add("sfz", AWebApis.Apis.DoValidShenFenZhengHao);
                    _dActions.Add("quit", AWebApis.Apis.QuitWeb);
                    _dActions.Add("reload", AWebApis.Apis.ReloadPages);
                }
                return _dActions;
            }
        }
        private void Resolve(string recv_request, Socket client)
        {
            string[] areq = recv_request.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var req in areq)
            {
                //if (aarg[0] == "GET")
                {
                    string[] aarg = req.Split(' ');
                    var aargs = aarg[1].Split('?');
                    string spath = Path.Combine(Environment.CurrentDirectory.Replace("\\", "/"), aargs[0]);
                    if (aargs.Length == 2)
                    {
                        var scmd = aargs[0].Replace("/", "");
                        if (dActions.ContainsKey(scmd))
                        {
                            SendResult(dActions[scmd](aargs[1]), client, "");
                        }
                        else
                        {
                            SendResult("Not implemented " + scmd + "?" + aargs[1], client);
                        }
                    }
                    else if (aargs[0].Contains("."))
                    {
                        string filePath = RootDir + aargs[0];
                        SendFile(filePath, client);
                    }
                    else
                    {
                        string indexPath = RootDir + aargs[0] + "/index.html";
                        if (File.Exists(indexPath))
                        {
                            SendFile(indexPath, client);
                        }
                        else
                        {
                            SendResult(AWebApis.Apis.Wiki404(), client);
                            //SendResult("Hello " + spath, client);
                        }
                    }
                    break;
                }
            }
        }

        private void SendFile(string filePath, Socket client)
        {
            if (File.Exists(filePath))
            {
                var fs = File.OpenRead(filePath);
                int b1;
                System.IO.MemoryStream tempStream = new System.IO.MemoryStream();
                while ((b1 = fs.ReadByte()) != -1)
                {
                    tempStream.WriteByte(((byte)b1));
                }
                SendResult(tempStream.ToArray(), client, new FileInfo(filePath).Name);
                fs.Close();
            }
            else
            {
                SendResult("Cannot find file " + filePath, client);
            }
        }

        public static string AddLastPageScript(string content)
        {
            return content;
        }

        private void SendResult(string result, Socket client, string fileName = "")
        {
            SendResult(Encoding.UTF8.GetBytes(HttpUtility.UrlDecode(result)), client, fileName);
        }
        private void SendResult(byte[] result, Socket client, string fileName)
        {
            String sBuffer = "";
            string sHttpVersion = "HTTP/1.1";
            string sMIMEHeader = "text/html"; // 默认 text/html
            sBuffer = sBuffer + sHttpVersion + " 200 OK" + "\r\n";
            if (!string.IsNullOrEmpty(fileName)
                && !fileName.EndsWith(".html")
                && !fileName.EndsWith(".htm"))
            {
                sMIMEHeader = "application/octet-stream";
                sBuffer = sBuffer + "Content-Disposition: attachment; filename=" + HttpUtility.UrlEncode(fileName, System.Text.Encoding.UTF8) + "\r\n";
            }
            sBuffer = sBuffer + "Server: cx1193719-b\r\n";
            sBuffer = sBuffer + "Access-Control-Allow-Origin: *\r\n";
            sBuffer = sBuffer + "Content-Type: " + sMIMEHeader + "\r\n";
            sBuffer = sBuffer + "Accept-Ranges: bytes\r\n";
            sBuffer = sBuffer + "Content-Length: " + result.Length + "\r\n\r\n";
            client.Send(Encoding.UTF8.GetBytes(sBuffer));
            client.Send(result);
            client.Close();
        }
    }

    /// <summary>
    /// cannot be used in .net core, but works fine in .net framework
    /// </summary>
    class RemoteDomain : MarshalByRefObject
    {
        public string Remote(string[] assembleArgs)
        {
            string[] r = assembleArgs[0].Split(new char[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            string args = assembleArgs[1];
            Assembly assembly = Assembly.LoadFrom(r[0] + ".dll");
            try
            {
                Type t = assembly.GetType(r[0] + "." + r[1]);
                if (t == null)
                {
                    return "找不到类型" + r[1];
                }
                MethodInfo mi = t.GetMethod(r[2]);
                if (mi == null)
                {
                    return "找不到方法" + r[2];
                }
                return mi.Invoke(null, new object[] { args }).ToString();
            }
            catch (Exception ex1)
            {
                return "错误：" + ex1.Message;
            }
        }
    }
}
