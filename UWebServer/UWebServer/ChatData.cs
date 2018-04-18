using System;
namespace AWebApis
{
    internal class ChatData
    {
        public string idFrom, idTo, idContent;
        private int iTime;
        public string idTime
        {
            get
            {
                return (new DateTime(2018, 1, 1).AddSeconds(iTime)).ToString();
            }
            set
            {
                iTime = typeParser.intParse(value);
            }
        }
        new public string ToString()
        {
            return idFrom + "," + idTo + "," + idContent + "," + iTime;
        }
        public string ToDisplay(string from, string sraw)
        {
            string fromColor = from == idFrom ? "#0ff000" : "#ff0000";
            string toColor = from == idTo ? "#0ff000" : "#ff0000";
            string sresult = "[" + idTime + "]<font color=" + fromColor + ">&nbsp"
                + "<input type=\"button\" onclick=\"f" + idFrom + "()\" value=\"" + idFrom + "\">"
                + "&nbsp</font> yell to <font color=" + toColor + ">&nbsp"
                + "<input type=\"button\" onclick=\"f" + idTo + "()\" value=\"" + idTo + "\">"
                + "&nbsp</font>:" + idContent;
            string srequesturl = "    var requestUrl = \"" + Apis.surl + "check" + "=" + idFrom + "\";\r\n";
            if (!sraw.Contains("function f" + idFrom + "()"))
            {
                sresult += "\r\n<script>\r\nfunction f" + idFrom + "()\r\n" +
                      "{\r\n" +
                      //"    var yell = document.getElementById(\"yell\").value;\r\n" +
                      srequesturl +
                      "    window.location.href = requestUrl;" +
                      "\r\n}\r\n</script>\r\n";
            }
            srequesturl = "    var requestUrl = \"" + Apis.surl + "check" + "=" + idTo + "\";\r\n";
            if (!sraw.Contains("function f" + idTo + "()"))
            {
                sresult += "\r\n<script>\r\nfunction f" + idTo + "()\r\n" +
                      "{\r\n" +
                      //"    var yell = document.getElementById(\"yell\").value;\r\n" +
                      srequesturl +
                      "    window.location.href = requestUrl;" +
                      "\r\n}\r\n</script>\r\n";
            }
            return sresult;
        }
        public static ChatData FromString(string sdata)
        {
            var adata = sdata.Split(',');
            ChatData cd = new ChatData();
            cd.idFrom = adata[0];
            cd.idTo = adata[1];
            cd.idContent = adata[2];
            cd.idTime = adata[3];
            return cd;
        }
    }
}