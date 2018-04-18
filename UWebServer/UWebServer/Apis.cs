using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Web;

namespace AWebApis
{
	public class Apis
	{
		static Dictionary<string, string> dArgs = new Dictionary<string, string>();
		static List<string> largs = new List<string>();
		private static string ParseArgs(string argInput, params string[] args)
		{
			dArgs.Clear();
			largs.Clear();

			foreach (var s in args)
			{
				largs.Add(s);
			}

			AIniLoader il = new AIniLoader();
			il.LoadContext(argInput);
			foreach (var d in largs)
			{
				if (!il.ContainsNode(d))
				{
					return string.Format("缺少参数：{0}", d);
				}
				string svalue = il.OnGetValue(d);
				dArgs.Add(d, svalue);
			}
			foreach (var d in il.AllKeys)
			{
				if (dArgs.ContainsKey(d))
				{
					continue;
				}
				string svalue = il.OnGetValue(d);
				dArgs.Add(d, svalue);
			}
			return "";
		}

		static Dictionary<string, string> staticPages = new Dictionary<string, string>();
		public static void ReloadStaticPages()
		{
			staticPages.Clear();
		}
		static Dictionary<string, string> dWikiSessons = new Dictionary<string, string>();
		public static string WikiLogin(string args)
		{
			string serror = ParseArgs(args, "u", "p");
			if (!string.IsNullOrEmpty(serror))
			{
				return serror;
			}
			if (!Directory.Exists(AWebServer.AWebServer.RootDir + "/wiki/db/users"))
			{
				return "账号/密码错误。";
			}
			string userpath = AWebServer.AWebServer.RootDir + "/wiki/db/users" + "/" + dArgs["u"];
			if (!File.Exists(userpath))
			{
				string validatingpath = AWebServer.AWebServer.RootDir + "/wiki/db/validates" + "/" + dArgs["u"];
				if (File.Exists(validatingpath))
				{
					return "此账号还未经过邮箱验证。";
				}
				return "没有这个账号。";
			}
			AIniLoader ini = new AIniLoader();
			ini.LoadIniFile(userpath);
			if (ini.OnGetValue("p") != MD5String.Hash32(dArgs["p"]))
			{
				return "账号/密码错误。";
			}
			if (dWikiSessons.ContainsKey(dArgs["u"]))
			{
				if (dNewPendingIds.ContainsKey(dWikiSessons[dArgs["u"]]))
				{
					dNewPendingIds.Remove(dWikiSessons[dArgs["u"]]);
				}
				dWikiSessons.Remove(dArgs["u"]);
			}
			var guid = Guid.NewGuid().ToString().Replace("-", "");
			dWikiSessons.Add(dArgs["u"], guid);
			return "0," + guid;
		}
		const int ascii0 = 48;
		const int ascii9 = 57;
		const int asciiA = 65;
		const int asciiZ = 90;
		const int asciia = 97;
		const int asciiz = 122;
		private static bool IsCharValid(char ar)
		{
			if (ar >= '0' && ar <= '9')
			{
				return true;
			}
			if (ar >= 'A' && ar <= 'Z')
			{
				return true;
			}
			if (ar >= 'a' && ar <= 'z')
			{
				return true;
			}
			return ar == '@';
		}
		public static string WikiRegist(string args)
		{
			string serror = ParseArgs(args, "u", "p", "m");
			if (!string.IsNullOrEmpty(serror))
			{
				return serror;
			}
			string username = dArgs["u"].Trim();
			if (username.Length < 4)
			{
				return "用户名长度不能小于4个字节。";
			}
			if (username.Length > 20)
			{
				return "用户名长度不能大于20个字节。";
			}
			foreach (var a in username)
			{
				if (!IsCharValid(a))
				{
					return "用户名中包含无效字符，请修改后重新提交。";
				}
			}
			if (!Directory.Exists(AWebServer.AWebServer.RootDir + "/wiki/db/users"))
			{
				Directory.CreateDirectory(AWebServer.AWebServer.RootDir + "/wiki/db/users");
			}
			string userpath = AWebServer.AWebServer.RootDir + "/wiki/db/users" + "/" + username;
			if (File.Exists(userpath))
			{
				return username + " 已经被注册了。";
			}
			string validatingpath = AWebServer.AWebServer.RootDir + "/wiki/db/validates" + "/" + username;
			if (File.Exists(validatingpath))
			{
				return username + " 已经被注册了。";
			}
			string validatestr = Guid.NewGuid().ToString().Replace("-", "");
			var fs = File.Create(validatingpath);
			fs.Close();
			File.AppendAllText(validatingpath, "v=" + validatestr + "\r\n" + "p=" + MD5String.Hash32(dArgs["p"]) + "\r\n" + "m=" + dArgs["m"]);
			string validateurl = "http://fscoding.top/wiki/validate?u=" + dArgs["u"] + "&v=" + validatestr;
			string smailContent = "<head><meta http-equiv=\"Content-Type\" content=\"text/html; charset=utf-8\"/><title>validate</title></head>请点击下面的链接验证您的邮箱<br>" +
				"<a href=\"" + validateurl + "\" target=\"about:blank\"\">验证</a><br>" +
				  "<br>如果点击按钮无效，请复制下面的链接并粘贴到浏览器的地址栏中访问。<br>" + validateurl + "<script>function validate(){window.location.url=" + validateurl + ";}</script>";
			OnSendMail(dArgs["m"], "support@fscoding.top"
				, "wiki 注册验证信息", smailContent);
			return LoadStaticPage(AWebServer.AWebServer.RootDir + "/wiki/templates/logout.html");
		}
		private static string validateurl = AWebServer.AWebServer.RootDir + "/wiki/templates/validate.html";
		private const string validateTag = "`'`wikivalidate`'`";
		public static string WikiValidate(string args)
		{
			string serror = ParseArgs(args, "u", "v");
			if (!string.IsNullOrEmpty(serror))
			{
				return serror;
			}
			string username = dArgs["u"].Trim();
			string validatingpath = AWebServer.AWebServer.RootDir + "/wiki/db/validates" + "/" + username;
			Dictionary<string, string> dtag = new Dictionary<string, string>();
			if (!File.Exists(validatingpath))
			{
				dtag.Add(validateTag, "这个账号不需要进行邮箱验证。");
				return LoadStaticPage(validateurl, dtag);
			}
			AIniLoader ini = new AIniLoader();
			ini.LoadIniFile(validatingpath);
			if (ini.OnGetValue("v") != dArgs["v"])
			{
				dtag.Add(validateTag, "验证失败。");
				return LoadStaticPage(validateurl, dtag);
			}
			File.Delete(validatingpath);
			string userpath = AWebServer.AWebServer.RootDir + "/wiki/db/users" + "/" + username;
			if (File.Exists(userpath))
			{
				dtag.Add(validateTag, "内部错误，已经存在了相同账户，请重新登录。");
				return LoadStaticPage(validateurl, dtag);
			}
			File.AppendAllText(userpath, "p=" + ini.OnGetValue("p") + "\r\n" + "m=" + ini.OnGetValue("m"));
			dtag.Add(validateTag, "验证成功。<br><a href=\"/wiki/login\">登录</a>");
			return LoadStaticPage(validateurl, dtag);
		}

		internal static string Wiki404()
		{
			return LoadStaticPage(AWebServer.AWebServer.RootDir + "/wiki/templates/404.html");
		}

		private static string LoadStaticPage(string path, Dictionary<string, string> dtag)
		{
			string svalidate = LoadStaticPage(path);
			foreach (var t in dtag.Keys)
			{
				svalidate = svalidate.Replace(t, dtag[t]);
			}
			return svalidate;
		}
		public static string DoValidShenFenZhengHao(string targetID)
		{
			int[] iargs = new int[] { 7, 9, 10, 5, 8, 4, 2, 1, 6, 3, 7, 9, 10, 5, 8, 4, 2 };
			string[] sargs = new string[] { "1", "0", "X", "9", "8", "7", "6", "5", "4", "3", "2" };

			int isum = 0;
			for (int i = 0; i < targetID.Length && i < iargs.Length; i++)
			{
				isum += typeParser.intParse(targetID[i].ToString()) * iargs[i];
			}
			return targetID + sargs[isum % 11];
		}

		static Dictionary<string, string> dNewPendingIds = new Dictionary<string, string>();
		public static string WikiNew(string args)
		{
			string serror = ParseArgs(args, "u", "s");
			if (!string.IsNullOrEmpty(serror))
			{
				return serror;
			}
			if (!dWikiSessons.ContainsKey(dArgs["u"]))
			{
				return ResolveInvalidSession();
			}
			if (dWikiSessons[dArgs["u"]] != dArgs["s"])
			{
				return ResolveInvalidSession();
			}
			if (!Directory.Exists(AWebServer.AWebServer.RootDir + "/wiki/db"))
			{
				Directory.CreateDirectory(AWebServer.AWebServer.RootDir + "/wiki/db");
			}
			var newID = Guid.NewGuid().ToString().Replace("-", "");
			if (dNewPendingIds.ContainsKey(dArgs["s"]))
			{
				dNewPendingIds[dArgs["s"]] = newID;
			}
			else
			{
				dNewPendingIds.Add(dArgs["s"], newID);
			}
			string sresult = LoadStaticPage(AWebServer.AWebServer.RootDir + "/wiki/templates/modify.html");
			sresult = sresult.Replace(WikiReplaceTag + "wikititle" + WikiReplaceTag, "");
			sresult = sresult.Replace(WikiReplaceTag + "username" + WikiReplaceTag, dArgs["u"]);
			sresult = sresult.Replace(WikiReplaceTag + "wikiid" + WikiReplaceTag, newID);
			sresult = sresult.Replace(WikiReplaceTag + "session" + WikiReplaceTag, dArgs["s"]);
			sresult = sresult.Replace(WikiReplaceTag + "wikicontent" + WikiReplaceTag, "");
			return sresult;
		}
		public static string WikiList(string args)
		{
			string serror = ParseArgs(args, "u", "s");
			if (!string.IsNullOrEmpty(serror))
			{
				return serror;
			}
			if (!dWikiSessons.ContainsKey(dArgs["u"]))
			{
				return ResolveInvalidSession();
			}
			if (dWikiSessons[dArgs["u"]] != dArgs["s"])
			{
				return ResolveInvalidSession();
			}
			if (!Directory.Exists(AWebServer.AWebServer.RootDir + "/wiki/db"))
			{
				Directory.CreateDirectory(AWebServer.AWebServer.RootDir + "/wiki/db");
			}
			string sresult = "";
			string listPath = AWebServer.AWebServer.RootDir + "/wiki/templates/list.html";
			sresult = LoadStaticPage(listPath);
			var wikifiles = Directory.EnumerateFiles(AWebServer.AWebServer.RootDir + "/wiki/db");
			if (string.IsNullOrEmpty(sresult))
			{
				List<string> lresult = new List<string>();
				foreach (var str in wikifiles)
				{
					var fileInfo = new FileInfo(str);
					lresult.Add(fileInfo.Name);
				}
				sresult = string.Join(',', lresult);
			}
			else
			{
				string swikis = "";
				foreach (var str in wikifiles)
				{
					if (!str.EndsWith(".wiki"))
					{
						continue;
					}
					FileInfo fi = new FileInfo(str);
					string wikiName = fi.Name.Replace(".wiki", "");
					string wikiTitlePath = AWebServer.AWebServer.RootDir + "/wiki/db/" + wikiName + ".title";
					if (!File.Exists(wikiTitlePath))
					{
						continue;
					}
					string fileExtention = fi.LastAccessTime.ToShortDateString() + " " + fi.LastAccessTime.ToLongTimeString();
					AIniLoader title = new AIniLoader();
					title.LoadIniFile(wikiTitlePath);
					var stitle = title.OnGetValue("title");
					if (string.IsNullOrEmpty(stitle))
					{
						stitle = File.ReadAllText(wikiTitlePath);
						title.OnSetValue("title", stitle);
						title.OnSetValue("author", dArgs["u"]);
						title.OnSaveBack();
					}
					else
					{
						fileExtention = "<font size=\"1\" align=\"right\" color=\"#0000ff\">" + title.OnGetValue("author") + "</font> " + "<font size=\"1\" align=\"right\">" + fileExtention + "</font>";
					}

					swikis += "<tr><td><a href=\"../wiki/read?" +
						"id=" + wikiName
						+ "&u=" + dArgs["u"]
						+ "&s=" + dArgs["s"]
						+ "\">"
						+ HttpUtility.UrlDecode(stitle) + "</a></td>"
						+ "<td class=\"td\">"
						+ fileExtention
						+ "</td></tr>";
					swikis += "<br>";
				}
				sresult = sresult.Replace(WikiReplaceTag + "wikilist" + WikiReplaceTag, swikis);
			}
			sresult = sresult.Replace(WikiReplaceTag + "username" + WikiReplaceTag, dArgs["u"]);
			sresult = sresult.Replace(WikiReplaceTag + "session" + WikiReplaceTag, dArgs["s"]);
			return sresult;
		}

		private static string LoadStaticPage(string listPath)
		{
			string sresult = "";
			if (staticPages.ContainsKey(listPath))
			{
				sresult = staticPages[listPath];
			}
			else if (File.Exists(listPath))
			{
				sresult = File.ReadAllText(listPath);
				staticPages.Add(listPath, sresult);
			}
			return sresult;
		}
		public static string WikiLogout(string args)
		{
			string serror = ParseArgs(args, "u", "s");
			if (!string.IsNullOrEmpty(serror))
			{
				return serror;
			}
			if (dWikiSessons.ContainsKey(dArgs["u"]) && dWikiSessons[dArgs["u"]] != dArgs["s"])
			{
				return ResolveInvalidSession();
			}
			if (dWikiSessons.ContainsKey(dArgs["u"]))
			{
				if (dNewPendingIds.ContainsKey(dWikiSessons[dArgs["u"]]))
				{
					dNewPendingIds.Remove(dWikiSessons[dArgs["u"]]);
				}
				dWikiSessons.Remove(dArgs["u"]);
			}
			return LoadStaticPage(AWebServer.AWebServer.RootDir + "/wiki/templates/logout.html");
		}
		public static string WikiRead(string args)
		{
			string serror = ParseArgs(args, "u", "s", "id");
			if (!string.IsNullOrEmpty(serror))
			{
				return serror;
			}
			if (!dWikiSessons.ContainsKey(dArgs["u"]))
			{
				return ResolveInvalidSession();
			}
			if (dWikiSessons[dArgs["u"]] != dArgs["s"])
			{
				return ResolveInvalidSession();
			}
			if (!Directory.Exists(AWebServer.AWebServer.RootDir + "/wiki/db"))
			{
				return WikiList(args);
			}
			string spath = AWebServer.AWebServer.RootDir + "/wiki/db/" + dArgs["id"] + ".wiki";
			if (!File.Exists(spath))
			{
				return WikiList(args);
			}
			string wikiTitlePath = AWebServer.AWebServer.RootDir + "/wiki/db/" + dArgs["id"] + ".title";
			var contents = File.ReadAllText(spath);
			AIniLoader title = new AIniLoader();
			title.LoadIniFile(wikiTitlePath);
			var stitle = title.OnGetValue("title");
			if (string.IsNullOrEmpty(stitle))
			{
				stitle = File.ReadAllText(wikiTitlePath);
				title.OnSetValue("title", stitle);
				title.OnSetValue("author", dArgs["u"]);
				title.OnSaveBack();
			}

			string sresult = LoadStaticPage(AWebServer.AWebServer.RootDir + "/wiki/templates/read.html");
			sresult = sresult.Replace(WikiReplaceTag + "wikititle" + WikiReplaceTag, HttpUtility.UrlDecode(stitle));
			sresult = sresult.Replace(WikiReplaceTag + "username" + WikiReplaceTag, dArgs["u"]);
			sresult = sresult.Replace(WikiReplaceTag + "session" + WikiReplaceTag, dArgs["s"]);
			sresult = sresult.Replace(WikiReplaceTag + "wikicontent" + WikiReplaceTag, HttpUtility.UrlDecode(contents));
			sresult = sresult.Replace(WikiReplaceTag + "wikiid" + WikiReplaceTag, dArgs["id"]);
			if (dArgs["u"] == title.OnGetValue("author"))
			{
				sresult = sresult.Replace("disabled=\"disabled\"", "");
			}
			return sresult;
		}
		private const string WikiReplaceTag = "`'`";
		public static string WikiModify(string args)
		{
			string serror = ParseArgs(args, "u", "s", "id");
			if (!string.IsNullOrEmpty(serror))
			{
				return serror;
			}
			if (!dWikiSessons.ContainsKey(dArgs["u"]))
			{
				return ResolveInvalidSession();
			}
			if (dWikiSessons[dArgs["u"]] != dArgs["s"])
			{
				return ResolveInvalidSession();
			}
			if (!Directory.Exists(AWebServer.AWebServer.RootDir + "/wiki/db"))
			{
				return "没有找到wiki";
			}
			string spath = AWebServer.AWebServer.RootDir + "/wiki/db/" + dArgs["id"] + ".wiki";
			if (!File.Exists(spath))
			{
				return "没有找到wiki";
			}
			string wikiTitlePath = AWebServer.AWebServer.RootDir + "/wiki/db/" + dArgs["id"] + ".title";
			AIniLoader title = new AIniLoader();
			title.LoadIniFile(wikiTitlePath);
			var stitle = title.OnGetValue("title");
			if (string.IsNullOrEmpty(stitle))
			{
				stitle = File.ReadAllText(wikiTitlePath);
				title.OnSetValue("title", stitle);
				title.OnSetValue("author", dArgs["u"]);
				title.OnSaveBack();
			}
			if (dArgs["u"] != title.OnGetValue("author"))
			{
				return "你不能修改别人的wiki。";
			}
			var contents = File.ReadAllText(spath);
			string sresult = LoadStaticPage(AWebServer.AWebServer.RootDir + "/wiki/templates/modify.html");
			sresult = sresult.Replace(WikiReplaceTag + "wikititle" + WikiReplaceTag, HttpUtility.UrlDecode(stitle));
			sresult = sresult.Replace(WikiReplaceTag + "username" + WikiReplaceTag, dArgs["u"]);
			sresult = sresult.Replace(WikiReplaceTag + "wikiid" + WikiReplaceTag, dArgs["id"]);
			sresult = sresult.Replace(WikiReplaceTag + "session" + WikiReplaceTag, dArgs["s"]);
			sresult = sresult.Replace(WikiReplaceTag + "wikicontent" + WikiReplaceTag, HttpUtility.UrlDecode(contents));

			return sresult;
		}

		private static string ResolveInvalidSession()
		{
			return LoadStaticPage(AWebServer.AWebServer.RootDir + "/wiki/templates/logout.html");
		}

		public static string WikiWrite(string args)
		{
			string serror = ParseArgs(args, "u", "s", "t", "c", "id");
			if (!string.IsNullOrEmpty(serror))
			{
				return serror;
			}
			if (string.IsNullOrEmpty(dArgs["t"]))
			{
				return "标题不能为空！";
			}
			if (string.IsNullOrEmpty(dArgs["c"]))
			{
				return "内容不能为空！";
			}
			if (!dWikiSessons.ContainsKey(dArgs["u"]))
			{
				return ResolveInvalidSession();
			}
			if (dWikiSessons[dArgs["u"]] != dArgs["s"])
			{
				return ResolveInvalidSession();
			}
			if (!Directory.Exists(AWebServer.AWebServer.RootDir + "/wiki/db"))
			{
				Directory.CreateDirectory(AWebServer.AWebServer.RootDir + "/wiki/db");
			}
			string spath = AWebServer.AWebServer.RootDir + "/wiki/db/" + dArgs["id"] + ".wiki";
			if (!File.Exists(spath))
			{
				var fs = File.Create(spath);
				fs.Close();
			}
			string wikiTitlePath = AWebServer.AWebServer.RootDir + "/wiki/db/" + dArgs["id"] + ".title";
			AIniLoader title = new AIniLoader();
			title.LoadIniFile(wikiTitlePath);
			title.OnSetValue("title", dArgs["t"]);
			title.OnSetValue("author", dArgs["u"]);
			if (dArgs.ContainsKey("private"))
			{
				title.OnSetValue("private", "1");
			}
			title.OnSaveBack();

			File.WriteAllText(spath, HttpUtility.UrlEncode(dArgs["c"].Replace(WikiReplaceTag, "")));
			return "0";
		}
		private static string OnSendMail(string to, string from, string subject, string content)
		{
			AIniLoader ini = new AIniLoader();
			ini.LoadIniFile("mailconfig.ini");
			ini.OnSetDefaultValue("mailserver", "smtp.fscoding.top");
			ini.OnSetDefaultValue("mailusername", "support@fscoding.top");
			ini.OnSetDefaultValue("mailpassword", "pwd");
			ini.OnSetDefaultValue("mailport", "25");
			ini.OnSetDefaultValue("mailssl", "0");
			ini.OnSetDefaultValue("mailcheckpwd", "1");
			if (string.IsNullOrEmpty(ini.OnGetValue("mailserver")))
			{
				ini.OnSaveBack();
			}
			return OnSendMail(ini.OnGetValue("mailserver"), to, from, subject, content, ini.OnGetValue("mailusername")
					, ini.OnGetValue("mailpassword")
					, ini.OnGetValue("mailport")
					, ini.OnGetIntValue("mailssl") == 1
					, ini.OnGetIntValue("mailcheckpwd") == 1);
		}
		private static string OnSendMail(string mailServer, string to, string from, string subject, string content, string mailuser, string mailpassword, string mailport, bool ssl, bool checkpwd)
		{
			try
			{
				MyEmail e = new MyEmail(mailServer
					, to, from, subject, content
					, mailuser
					, mailpassword
					, mailport
					, ssl
					, checkpwd
					);
				e.Send();
			}
			catch (Exception ex)
			{
				return ex.Message;
			}
			return "0";
		}

		private const string pubFile = "pub.txt";
		private static Dictionary<string, List<ChatData>> _dPublicChatData;
		private static Dictionary<string, List<ChatData>> dPublicChatData
		{
			get
			{
				if (_dPublicChatData == null)
				{
					_dPublicChatData = new Dictionary<string, List<ChatData>>();
					if (File.Exists(pubFile))
					{
						var contents = File.ReadAllLines(pubFile);
						foreach (var content in contents)
						{
							var acontent = content.Split('=');
							if (acontent.Length == 2)
							{
								ChatData cd = ChatData.FromString(acontent[1]);
								if (!_dPublicChatData.ContainsKey(acontent[0]))
								{
									_dPublicChatData.Add(acontent[0], new List<ChatData>());
								}
								_dPublicChatData[acontent[0]].Add(cd);
								if (!_dPublicChatData.ContainsKey(cd.idTo))
								{
									_dPublicChatData.Add(cd.idTo, new List<ChatData>());
								}
								_dPublicChatData[cd.idTo].Add(cd);
							}
						}
					}
				}
				return _dPublicChatData;
			}
		}
		private static void SavePublicChatData(ChatData cd)
		{
			File.AppendAllText(pubFile, cd.idFrom + "=" + cd.ToString() + "\r\n");
		}
		private static Dictionary<string, List<ChatData>> dPrivateChatData = new Dictionary<string, List<ChatData>>();
		enum EType
		{
			list,
			check,
			to,
			register,
		}
		public const string surl = "http://com1.fscoding.top/chat?";
		public static string yell(string args)
		{
			string sresult = "";
			try
			{
				EType eType = EType.list;
				sresult = ParseArgs(args, "list");
				if (!string.IsNullOrEmpty(sresult))
				{
					sresult = ParseArgs(args, "check");
					if (!string.IsNullOrEmpty(sresult))
					{
						sresult = ParseArgs(args, "from", "to", "content");
						if (!string.IsNullOrEmpty(sresult))
						{
							sresult = ParseArgs(args, "register");
							if (!string.IsNullOrEmpty(sresult))
							{
								return "缺少参数";
							}
							else
							{
								eType = EType.register;
							}
						}
						else
						{
							eType = EType.to;
						}
					}
					else
					{
						eType = EType.check;
					}
				}
				sresult = "<html><head><meta charset=\"utf-8\"/><title>chat</title></head>";
				sresult += "<body>";
				string spaypal = "";
				string pp = "<div id=\"paypal-button\"></div>\r\n\r\n" +
					"<script src=\"https://www.paypalobjects.com/api/checkout.js\">" +
					"</script>\r\n\r\n<script>" +
					"\r\n    paypal.Button.render({" +
					"\r\n" +
					"\r\n        env: 'production', // Or 'sandbox'\r\n" +
					"\r\n        client: " +
					"{" +
					"\r\n            sandbox:    'AZadfbIutaSdHAR-VHxlDc0WOTu_AnadHlTdXvs9lNuM-JcIakuwVtcIUBFt78oaKp1oMcMEFbNDcJ8B'," +
					"\r\n            production: 'AepOvR0z_m7PQvXigclbwqjPOiOo8mMzg10a7rGvyuoV2w-PbVkWDLNX0t3HqrQIhdafQEdIEBeUiFN_'\r\n        },\r\n" +
					"\r\n        commit: true, // Show a 'Pay Now' button" +
					"\r\n\r\n        payment: function(data, actions) {\r\n   " +
					"         return actions.payment.create({" +
					"\r\n                payment: " +
					"{" +
					"\r\n                    transactions: [\r\n                        {\r\n                            amount: { total: '1.00', currency: 'USD' }\r\n                        }\r\n                    ]\r\n                }\r\n            });\r\n        },\r\n\r\n        onAuthorize: function(data, actions) {\r\n            return actions.payment.execute().then(function(payment) {\r\n\r\n                // The payment is complete!\r\n                // You can now show a confirmation message to the customer\r\n            });\r\n        }\r\n\r\n    }, '#paypal-button');\r\n</script>";
				string wxzfb = "&nbsp&nbsp&nbsp&nbsp<h6>微信</h6><br><img src=\"wx.png\"><br>&nbsp&nbsp&nbsp&nbsp<h6>支付宝</h6><br><img src=\"zfb.png\">";
				spaypal = wxzfb + pp;
				if (eType == EType.list)
				{
					sresult += GetRequest(eType, "List Public Chat Data", dArgs["list"]);
				}
				else if (eType == EType.check)
				{
					sresult += GetRequest(eType, "Talk To", dArgs["check"], dArgs.ContainsKey("from") ? dArgs["from"] : "");

					sresult += "<hr><br>";

					sresult += "history<br>";
					if (!dPublicChatData.ContainsKey(dArgs["check"]) || dPublicChatData[dArgs["check"]].Count == 0)
					{
						sresult += "&nbsp&nbsp&nbsp&nbsp" + dArgs["check"] + " got a empty list.<br>";
					}
					else
					{
						foreach (var c in dPublicChatData[dArgs["check"]])
						{
							sresult += "&nbsp&nbsp&nbsp&nbsp" + c.ToDisplay(dArgs["check"], sresult) + "<br>";
						}
					}

					sresult += "<hr>\t\t<h6>赞助我</h6>" + spaypal + "</body>";
				}
				else if (eType == EType.to)
				{
					sresult += GetRequest(eType, "Talk To", dArgs["check"], dArgs.ContainsKey("from") ? dArgs["from"] : "");

					sresult += "<hr><br>";

					ChatData cd = new ChatData() { idFrom = dArgs["from"], idTo = dArgs["to"], idContent = dArgs["content"], idTime = (DateTime.Now - new DateTime(2018, 1, 1)).TotalSeconds.ToString("f0") };
					if (!dPublicChatData.ContainsKey(cd.idFrom))
					{
						dPublicChatData.Add(cd.idFrom, new List<ChatData>());
					}
					dPublicChatData[cd.idFrom].Add(cd);
					if (!dPublicChatData.ContainsKey(cd.idTo))
					{
						dPublicChatData.Add(cd.idTo, new List<ChatData>());
					}
					dPublicChatData[cd.idTo].Add(cd);

					SavePublicChatData(cd);

					sresult += cd.idTo + " history<br>";
					foreach (var c in dPublicChatData[cd.idTo])
					{
						sresult += "&nbsp&nbsp&nbsp&nbsp" + c.ToDisplay(cd.idFrom, sresult) + "<br>";
					}
					sresult += cd.idFrom + " history<br>";
					foreach (var c in dPublicChatData[cd.idFrom])
					{
						sresult += "&nbsp&nbsp&nbsp&nbsp" + c.ToDisplay(cd.idFrom, sresult) + "<br>";
					}
					sresult += "<hr>                 <h6>赞助我</h6><br>" + spaypal + "</body>";
				}
			}
			catch (Exception ex)
			{
				sresult = ex.Message + "<br>" + ex.StackTrace;
			}
			return sresult;
		}
		private static string GetRequest(EType eType, string btnName, string inValue, string inValue2 = "")
		{
			string sinput = "", srequesturl = "", sbutton = "";
			sbutton = "<input type=\"button\" onclick=\"" + btnName.Replace(" ", "") + "()\" value=\"" + btnName + "\">\r\n";
			switch (eType)
			{
				case EType.list:
					sinput = "yell to : " + surl + EType.check + "=<input id=\"yell\" type=\"text\" value=\"" + inValue + "\" style=\"width:200px;\"><br>\r\n";
					srequesturl = "    var requestUrl = \"" + surl + EType.check + "=\" %2b yell;\r\n";
					break;
				case EType.check:
					sinput += "" + surl + "<br>from : " + "<input id=\"yellf\" type=\"text\" value=\""
						+ (string.IsNullOrEmpty(inValue2) ? new Random().Next(10000).ToString() : inValue2)
						+ "\" style=\"width:200px;\"><br>\r\n";
					sinput += EType.to + " : <input id=\"yell\" type=\"text\" value=\"" + inValue + "\" style=\"width:200px;\"><br>\r\n";
					sinput += "content" + " : </br><textarea id=\"yellc\" type=\"text\" value=\"" + "" + "\" rows=\"10\" cols=\"60\"></textarea><br>\r\n";
					srequesturl += "    var yellf = document.getElementById(\"yellf\").value;\r\n";
					srequesturl += "    var yellc = document.getElementById(\"yellc\").value;\r\n";
					srequesturl += "    var requestUrl = \"" + surl + EType.to + "=\"%2byell%2b\"&from=\"%2byellf%2b\"&content=\"%2byellc;" +
						"\r\n";
					break;
				case EType.to:
					sinput += "register: <input id=\"yell\" type=\"text\" value=\"" + inValue + "\" style=\"width:200px;\"><br>\r\n";
					sinput += "password: <input id=\"yellp\" type=\"text\" value=\"" + "" + "\" style=\"width:200px;\"><br>\r\n";
					srequesturl += "    var yellp = document.getElementById(\"yellp\").value;\r\n";
					srequesturl += "    var requestUrl = \"" + surl + EType.register + "=\" %2b yell&password=\" %2b yellp";
					srequesturl += ";\r\n";
					break;
			}
			return "\r\n<div id=\"list\">" +
				sinput +
				sbutton +
				"<script>\r\nfunction " + btnName.Replace(" ", "") + "()\r\n" +
				"{\r\n" +
				"    var yell = document.getElementById(\"yell\").value;\r\n" +
				srequesturl +
				"    window.location.href = requestUrl;" +
				"}\r\n</script>" +
				"</div>\r\n" +
				"<div id=\"check\">" +
				"</div>" +
				"</center>\r\n";
		}
	}
}
