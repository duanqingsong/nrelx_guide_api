using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Collections;
using nRelax.Tour.BLL;
using System.Web.Security;
using System.Text;
using nRelax.SSO;
using nRelax.Tour.Entity;

namespace nRelax.Tour.WebApp
{
    public static class SingleLogin
    {
        private const string WEBCONFIG_OPENSINGLELOGINKEY = "OpenSingleLogin";

        /// <summary>
        /// 确定配置是允许限制 单1台电脑登录
        /// </summary>
        /// <returns></returns>
        private static bool IsEnable()
        {
            string sConfigValue = System.Configuration.ConfigurationManager.AppSettings[WEBCONFIG_OPENSINGLELOGINKEY].ToString();
            if (sConfigValue == "1")
                return true;
            else
                return false;
        }
        /// <summary>
        /// 用户登录时调用
        /// </summary>
        /// <param name="nUserID"></param>
        public static void RegLoginInfo(decimal nUserID)
        {
            if (!IsEnable())
                return;

            Hashtable hOnline = (Hashtable)HttpContext.Current.Application["Online"];
            if (hOnline != null)
            {
                IDictionaryEnumerator oIDE = hOnline.GetEnumerator();
                string strKey = "";
                while (oIDE.MoveNext())
                {
                    if (oIDE.Value != null && oIDE.Value.ToString() == nUserID.ToString())
                    {
                        strKey = oIDE.Key.ToString();
                        if (strKey != HttpContext.Current.Session.SessionID)
                        {
                            hOnline[strKey] = "GOOUT";
                            break;
                        }
                    }
                }
            }
            else
                hOnline = new Hashtable();

            hOnline[HttpContext.Current.Session.SessionID] = nUserID;
            HttpContext.Current.Application.Lock();
            HttpContext.Current.Application["Online"] = hOnline;
            HttpContext.Current.Application.UnLock();

        }

        public static bool IsCanceled() {
            decimal userId = CurrentSysUser.Get().UserId;
            UsersBiz userBiz = new UsersBiz();
            Users user= userBiz.GetByUserId(userId);
            
            return false;
        }

        /// <summary>
        /// 页面初始化判断是否已经登录
        /// void PageBase_Init(object sender, EventArgs e) 方法中調用
        /// </summary>
        /// <returns></returns>
        public static bool IsLogin()
        {
            if (!IsEnable())
                return false;

            Hashtable hOnline = (Hashtable)HttpContext.Current.Application["Online"];
            if (hOnline != null)
            {
                if (hOnline.ContainsKey(HttpContext.Current.Session.SessionID))
                {
                    string sv = hOnline[HttpContext.Current.Session.SessionID].ToString();
                    if (sv == "GOOUT")
                    {
                        hOnline.Remove(HttpContext.Current.Session.SessionID);
                        HttpContext.Current.Application.Lock();
                        HttpContext.Current.Application["Online"] = hOnline;
                        HttpContext.Current.Application.UnLock();
                        //注销
                        CurrentSysUser.LoginOut();
                        FormsAuthentication.SignOut();
                        //"你的帐号已在别处登陆，你被强迫下线！;
                        return true;
                    }
                }
                return false;
            }
            return false;
        }

        //在Global.asax文件中的Session_End中添加如下代码：
        public static void Clear()
        {
            if (!IsEnable())
                return;

            Hashtable hOnline = (Hashtable)HttpContext.Current.Application["Online"];
            if (hOnline != null)
            {
                if (hOnline[HttpContext.Current.Session.SessionID] != null)
                {
                    hOnline.Remove(HttpContext.Current.Session.SessionID);
                    HttpContext.Current.Application.Lock();
                    HttpContext.Current.Application["Online"] = hOnline;
                    HttpContext.Current.Application.UnLock();
                }
            }
        }

        /// <summary>
        /// 获取退出脚本
        /// if (SingleLogin.IsLogin())
        /// {
        ///     string sScript = SingleLogin.GetLoginOutScript();
        ///     Response.Write(sScript);
        /// }
        /// </summary>
        /// <returns></returns>
        public static string GetLoginOutScript(string urlRoot)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("<script type=\"text/javascript\">").Append("\r\n");
            sb.Append("alert(\"您的賬號在別處已經登錄，您被強制下線。\");").Append("\r\n");
            sb.Append(string.Format("window.top.location=\"{0}login.aspx\";", urlRoot)).Append("\r\n");
            sb.Append("</script>");
            return sb.ToString();
        }
    }
}
//                IDictionaryEnumerator oIDE = hOnline.GetEnumerator();
//while (oIDE.MoveNext())
//{
//    if (oIDE.Key != null && oIDE.Key.ToString().Equals(HttpContext.Current.Session.SessionID))
//    {
//        //already login
//        if (oIDE.Value != null && "XXXXXX".Equals(oIDE.Value.ToString()))
//        {

//        }
//        break;
//    }
//}