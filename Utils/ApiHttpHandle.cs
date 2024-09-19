using Newtonsoft.Json;
using nRelax.DevBase.BaseTools;
using System;
using System.Collections.Specialized;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;

namespace nRelax.Tour.WebApp
{
    public class ApiResult {
        public int success { get; set; }
        public object data { get; set; }
        public string errorcode { get; set; }
    }
    public class ApiListResult
    {
        public int success { get; set; }
        public object rows { get; set; }
        public string errorcode { get; set; }
    }
    public class ApiHttpHandle:IHttpHandler
    {
        protected const string md5key = "23uoiasdnfkJHJklasdfsadlsadf";
        public bool IsReusable
        {
            get
            {
                return false;
            }
        }

        public string action { get; set; }

        #region ReturnJsonResponse,ReturnTextResponse
        public void ReturnJsonResponse(string sValue)
        {
            HttpContext.Current.Response.ContentType = "application/json";

            //HttpContext.Current.Response.AddHeader("Access-Control-Allow-Origin", "*");
            //HttpContext.Current.Response.AddHeader("Access-Control-Allow-Methods", "POST");
            //HttpContext.Current.Response.AddHeader("Access-Control-Allow-Headers", "Content-type");
            HttpContext.Current.Response.ClearContent();
            HttpContext.Current.Response.Write(sValue);
        }

        public void ReturnJsonResponse(object oValue)
        {
            string strResult = JsonConvert.SerializeObject(oValue);
            ReturnJsonResponse(strResult);
        }

        public void ReturnTextResponse(string sValue)
        {
            HttpContext.Current.Response.ContentType = "text/plain";
            HttpContext.Current.Response.Write(sValue);
        }
        #endregion

        #region 签名检查 (每个请求都要检查）
        /// <summary>
        /// 检查签名是否正确(每个请求都要检查）
        /// </summary>
        /// <returns></returns>
        public bool CheckSign()
        {
            string sign = GetQueryString("sign");
            string sign2 = GetSign();
            if (sign.Length == 0)
            {
                return false;
            }
            if (sign == sign2)
                return true;
            else
            {
                return false;
            }
        }
        /// <summary>
        /// 获取签名
        /// </summary>
        /// <returns></returns>
        private string GetSign()
        {
            string strQuery = HttpContext.Current.Request.Url.Query.TrimStart('?');
            NameValueCollection cc = HttpUtility.ParseQueryString(strQuery,
                System.Text.Encoding.UTF8);
            if (cc.AllKeys.Contains("sign"))
                cc.Remove("sign");

            strQuery = cc.ToString() + md5key;
            string sign = MD516(strQuery);
            return sign;
        }

        /// <summary>
        /// 对字符进行MD5加密
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        public string MD5(string str)
        {
            byte[] data = Encoding.GetEncoding("UTF-8").GetBytes(str);
            MD5 md5 = new MD5CryptoServiceProvider();
            byte[] OutBytes = md5.ComputeHash(data);

            string OutString = "";
            for (int i = 0; i < OutBytes.Length; i++)
            {
                OutString += OutBytes[i].ToString("x2");
            }
            return OutString.ToLower();
        }

        public static string MD516(string str)
        {
            MD5CryptoServiceProvider md5 = new MD5CryptoServiceProvider();
            string strMd5 = BitConverter.ToString(md5.ComputeHash(UTF8Encoding.Default.GetBytes(str)), 4, 8);
            strMd5 = strMd5.Replace("-", "");
            strMd5 = strMd5.ToLower();
            return strMd5;
        }
        #endregion

        #region 获取查询字符串

        /// <summary>
        /// 查询字符串货Form提交均可查询
        /// </summary>
        /// <param name="skey"></param>
        /// <returns></returns>
        public string GetQueryString(string skey)
        {
            string sValue = string.Empty;
            if (HttpContext.Current.Request[skey] != null)
                sValue= HttpContext.Current.Request[skey].ToString();
            else if (HttpContext.Current.Request.Params[skey] != null)
                sValue= HttpContext.Current.Request.Params[skey].ToString();
            else
                sValue= "";
            sValue = sValue.Replace("undefined", "");
            return sValue;
        }

        /// <summary>
        /// 记录日志,包含客户提交的参数
        /// </summary>
        /// <param name="ex"></param>
        public void LogError(Exception ex) {
            string par = HttpContext.Current.Request.Params.ToString();
            Exception inex = ex.InnerException != null ? ex.InnerException : ex;
            string strErr = string.Format("参数:{0}\r\n错误信息：{1} \r\n 堆栈:{2} \r\n 源:{3}",
                par,
                inex.Message,
                inex.StackTrace,
                inex.Source);
            Logger.Error(strErr);
        }

        public int GetQueryInt(string skey)
        {
            string sv = GetQueryString(skey);
            if (sv.Length > 0)
            {
                int nv = 0;
                int.TryParse(sv, out nv);
                return nv;
            }
            return 0;
        }

        public decimal GetQueryDecimal(string skey)
        {
            string sv = GetQueryString(skey);
            if (sv == null)
                return 0;
            return StringTool.String2Decimal(sv);
            //return (decimal)GetQueryInt(skey);
        }

        #endregion

        public virtual void ProcessRequest(HttpContext context)
        {
        }

        #region 检测是否有Sql危险字符
        /// <summary>
        /// 检测是否有Sql危险字符
        /// </summary>
        /// <param name="str">要判断字符串</param>
        /// <returns>判断结果</returns>
        public bool IsSafeSqlString(string str)
        {
            return !Regex.IsMatch(str, @"[-|;|,|\/|\(|\)|\[|\]|\}|\{|%|@|\*|!|\']");
        }

        /// <summary>
        /// 检查危险字符
        /// </summary>
        /// <param name="Input"></param>
        /// <returns></returns>
        public string Filter(string sInput)
        {
            if (sInput == null || sInput == "")
                return null;
            string sInput1 = sInput.ToLower();
            string output = sInput;
            string pattern = @"*|and|exec|insert|select|delete|update|count|master|truncate|declare|char(|mid(|chr(|'";
            if (Regex.Match(sInput1, Regex.Escape(pattern), RegexOptions.Compiled | RegexOptions.IgnoreCase).Success)
            {
                throw new Exception("字符串中含有非法字符!");
            }
            else
            {
                output = output.Replace("'", "''");
            }
            return output;
        }

        /// <summary> 
        /// 检查过滤设定的危险字符
        /// </summary> 
        /// <param name="InText">要过滤的字符串 </param> 
        /// <returns>如果参数存在不安全字符，则返回true </returns> 
        public bool SqlFilter(string word, string InText)
        {
            if (InText == null)
                return false;
            foreach (string i in word.Split('|'))
            {
                if ((InText.ToLower().IndexOf(i + " ") > -1) || (InText.ToLower().IndexOf(" " + i) > -1))
                {
                    return true;
                }
            }
            return false;
        }
        #endregion


        
    }

    public static class ErrorCode
    {
        public const string EMPTY = "";

        /// <summary>
        /// 没有找到对应的action
        /// </summary>
        public const string NO_ACTION = "00000";
        /// <summary>
        /// 签名错误
        /// </summary>
        public const string CHECK_SIGN_ERROR = "40000";

        /// <summary>
        /// token过期
        /// </summary>
        public const string TOKEN_EXPIRED = "40001";

        /// <summary>
        ///时间戳为空
        /// </summary>
        public const string STAMP_EMPTY = "40002";

        /// <summary>
        /// 系统内部错误
        /// </summary>
        public const string SYSTEM_ERROR = "41000";

        /// <summary>
        /// 用户或密码错误
        /// </summary>
        public const string CHECK_PWD_FAIL = "41001";
        /// <summary>
        /// 登陆账号，密码，时间戳不能为空
        /// </summary>
        public const string LOGIN_INFO_EMPTY = "41002";

        /// <summary>
        /// 不安全的参数值
        /// </summary>
        public const string UNSAFEPARAM = "41003";

        /// <summary>
        /// 未查到符合条件的数据
        /// </summary>
        public const string OBJECT_NOT_FIND = "41004";
        /// <summary>
        /// 密碼強度不夠
        /// </summary>

        public const string PASSWORD_STRONG_ERROR = "41005";

        /// <summary>
        /// 错误的参数
        /// </summary>
        public const string ERROR_PARAM = "41006";

        /// <summary>
        /// 获取手机号码失败
        /// </summary>
        public const string ERROR_GETPHONE = "41007";

        /// <summary>
        /// 未找到会员资料
        /// </summary>
        public const string CANT_FOUNT_MEMBER = "51000";

        /// <summary>
        /// 会员ID不能为空
        /// </summary>
        public const string MEMBER_ID_CANT_EMPTY = "51001";

        /// <summary>
        /// 身份证不能为空
        /// </summary>
        public const string MEMBER_SFZ_CANT_EMPTY = "51002";
        /// <summary>
        /// 回乡证不能为空
        /// </summary>
        public const string MEMBER_HXZ_CANT_EMPTY = "51003";

        /// <summary>
        /// 手机号码不能为空
        /// </summary>
        public const string MEMBER_MOBILE_CANT_EMPTY = "51004";
        /// <summary>
        /// 中文姓名不能为空
        /// </summary>
        public const string MEMBER_CNNAME_CANT_EMPTY = "51005";
        /// <summary>
        /// 英文姓名不能为空
        /// </summary>
        public const string MEMBER_ENNAME_CANT_EMPTY = "51006";

        /// <summary>
        /// 身份证号码已经存在
        /// </summary>
        public const string MEMBER_SFZ_EXIST = "51010";
        /// <summary>
        /// 回乡证号码已经存在
        /// </summary>
        public const string MEMBER_HXZ_EXIST = "51011";
        /// <summary>
        /// 手机号码已经存在
        /// </summary>
        public const string MEMBER_MOBILE_EXIST = "51012";




        /// <summary>
        /// 沒有可銷售的團，或座位不夠
        /// </summary>
        public const string NO_TOURGROUP_CANSALE = "60001";

        /// <summary>
        /// 沒有找到保險價格
        /// </summary>
        public const string NO_INSURPRICE_CANSALE = "60002";

        /// <summary>
        /// 訂單創建錯誤
        /// </summary>
        public const string CANDNT_BILL = "60003";

        /// <summary>
        /// 此会员有未支付订单
        /// </summary>
        public const string HAVEUNPAYBILL = "60004";

        /// <summary>
        /// 传入的订单标示错误
        /// </summary>
        public const string BILLIDERROR = "60005";

        /// <summary>
        /// 订单已取消
        /// </summary>
        public const string BILL_CANCELED = "60006";

        /// <summary>
        /// 团已满
        /// </summary>
        public const string NO_CANUSESEAT = "60007";

        /// <summary>
        /// 缺少游客信息
        /// </summary>
        public const string TOURBII_NEED_TOURIBFO = "60008";

        /// <summary>
        /// 证件号码或手机号码错误
        /// </summary>
        public const string CERTNO_OR_MOBILENO_ERROR = "60009";

        /// <summary>
        /// 一单只能评论一次
        /// </summary>
        public const string COMMENT_ONLY_ONETIME = "60010";

        /// <summary>
        /// 验证码错误
        /// </summary>
        public const string VERIFY_CODE_ERROR = "60011";

        /// <summary>
        /// 缺少token
        /// </summary>
        public const string CHECK_TOKEN_EXISTS_ERROR = "60012";
        /// <summary>
        /// token 错误
        /// </summary>
        public const string CHECK_TOKEN_ERROR = "60013";

        /// <summary>
        /// 已支付订单不可取消
        /// </summary>
        public const string CANCEL_CANTNOT = "60014";

        /// <summary>
        /// 人數超出限制
        /// </summary>
        public const string PERSION_OVER_LIMIT = "60015";

        /// <summary>
        /// 一人必須補房差
        /// </summary>
        public const string MUST_ADD_ROOM = "60016";

        /// <summary>
        /// 小程序获取手机号码时创建会员资料失败
        /// </summary>
        public const string ERROR_CREATE_MEMBER = "60017";
    }
}