using nRelax.DevBase.BaseTools;
using System;
using System.Collections;
using System.Web;

namespace nRelax.Tour.WebApp
{
    public class Global : System.Web.HttpApplication
    {

        protected void Application_Start(object sender, EventArgs e)
        {


            //在应用程序启动时运行的代码
            string sbinPath = Server.MapPath("~").TrimEnd('\\');
            if (!sbinPath.ToLower().EndsWith("bin"))
                sbinPath = sbinPath + "\\bin\\";
            string sPath = System.IO.Path.Combine(sbinPath, "config\\log4net.cfg.xml");
            System.IO.FileInfo fi = new System.IO.FileInfo(sPath);
            if (fi.Exists)
            {
                log4net.Config.XmlConfigurator.Configure(fi);
            }
            else
                throw new Exception("未能加載Log4net配置文件."+ sPath);

            //在Application_Start时注册的Routing规则
            //RegisterRoutes(System.Web.Routing.RouteTable.Routes);
        }
        public static void RegisterRoutes(System.Web.Routing.RouteCollection routes)
        {
            //routes.Ignore("{resource}.axd/{*pathInfo}");
            //routes.MapPageRoute("bizproxy", "biz/{bizname}/{pindex}/{pcount}/index.ashx", "~/ashx/bizproxy.ashx");
        }
        protected void Session_Start(object sender, EventArgs e)
        {

        }

        protected void Application_BeginRequest(object sender, EventArgs e)
        {

        }

        protected void Application_AuthenticateRequest(object sender, EventArgs e)
        {

        }

        protected void Application_Error(object sender, EventArgs e)
        {
            Exception ex = Server.GetLastError().GetBaseException();
            Logger.Error(ex);
            Exception inex = ex.InnerException == null ? ex : ex.InnerException;

            try
            {
                System.Text.StringBuilder sb = new System.Text.StringBuilder();
                SSO.SystemUser sysUser = SSO.CurrentSysUser.Get();
                sb.AppendFormat("用户:{0}", sysUser != null ? sysUser.UserLoginName : "").AppendLine()
                    .AppendFormat("密码:{0}", sysUser != null ? sysUser.PWD : "").AppendLine()
                    .AppendFormat("网址:{0}", HttpContext.Current.Request.Url.AbsoluteUri).AppendLine();

                ArrayList arrayQuery = new ArrayList();
                foreach (string item in HttpContext.Current.Request.QueryString)
                {
                    string value = HttpContext.Current.Request.QueryString[item];
                    arrayQuery.Add(new { name = item, value = value });
                }

                sb.AppendFormat("QueryString参数:{0}", Newtonsoft.Json.JsonConvert.SerializeObject(arrayQuery)).AppendLine();


                ArrayList arrayForm = new ArrayList();
                foreach (string item in HttpContext.Current.Request.Form)
                {
                    if (item != "__EVENTTARGET" && item != "__EVENTARGUMENT" && item != "__VIEWSTATE" && item != "__VIEWSTATEGENERATOR")
                    {
                        string value = HttpContext.Current.Request.Form[item];
                        string key = item;
                        if (item.IndexOf('$') >= 0)
                        {
                            string[] itemItems = item.Split('$');
                            key = itemItems[itemItems.Length - 1];
                        }
                        arrayForm.Add(new { name = key, value = value });
                    }
                }
                sb.AppendFormat("Form参数:{0}", Newtonsoft.Json.JsonConvert.SerializeObject(arrayForm)).AppendLine();
                sb.AppendFormat("错误:{0}", inex.Message).AppendLine()
                    .AppendFormat("详情:{0}", inex.StackTrace);
                DingTalkTool tool = new DingTalkTool("cf6d3591ec7a46373e130ce09affc726d5089558c9e25718bd50109f8c7b3b80");
                tool.SendMessage(sb.ToString());
            }
            catch (Exception)
            {
            }
        }

        protected void Session_End(object sender, EventArgs e)
        {
            //在会话结束时运行的代码。 
            // 注意: 只有在 Web.config 文件中的 sessionstate 模式设置为
            // InProc 时，才会引发 Session_End 事件。如果会话模式 
            //设置为 StateServer 或 SQLServer，则不会引发该事件。


            nRelax.Tour.WebApp.SingleLogin.Clear();
        }

        protected void Application_End(object sender, EventArgs e)
        {

        }
    }
}