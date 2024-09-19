using NHibernate;
using nRelax.DAL;
using nRelax.DevBase.BaseTools;
using nRelax.Tour.BLL;
using nRelax.Tour.Entity;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Web;

namespace nRelax.Tour.GuideApi.Service
{
    public class GuideInfoService: GuideInfoBiz
    {

        /// <summary>
        /// 根據手機號碼登錄,并更新微信UnionID
        /// </summary>
        /// <param name="sMobileNo"></param>
        /// <param name="unionid"></param>
        /// <returns></returns>
        public GuideInfo GetByMobileNoAndUpdateUnionId(string sMobileNo, string unionid)
        {
            ISession session = DBSessions.GetSession();
            using (ITransaction tran = session.BeginTransaction(IsolationLevel.ReadCommitted))
            {
                try
                {
                    decimal nid = 0;
                    GuideInfoBiz biz = new GuideInfoBiz();
                    GuideInfo guide = biz.GetByMobileNo(session, sMobileNo);
                    if (guide != null)
                    {
                        nid = guide.Id;
                        //补充更新unionid
                        string oldunionid = guide.UnionId == null ? "" : guide.UnionId;
                        if (oldunionid != unionid && unionid.Length > 0)
                        {
                            biz.UpdateFieldValue(session, "obj.UnionId", unionid, guide.Id);
                        }
                    }
                    tran.Commit();
                    return guide;
                }
                catch (Exception ex)
                {
                    tran.Rollback();
                    throw ex;
                }
            }
        }


        /// <summary>
        /// 身份认证 通过 手机号码,姓名,负责团号认证 
        /// 同时用于 获取此导游属于哪家公司 
        /// </summary>
        /// <param name="name"></param>
        /// <param name="ftname">繁體姓名</param>
        /// <param name="mobile"></param>
        /// <param name="tourcode"></param>
        /// <returns></returns>
        public DataTable GetGuideByMobileNameAndTourCode(string name, string ftname, string mobile, string tourcode)
        {

            string sSql = @"
select b.Id as guideid,b.GuideName as name,b.Mobile as mobile
from TG_TourGroup a with(nolock) ,BS_GuideInfo b with(nolock)  ,TP_Tour c with(nolock) 
where a.GuideID=b.Id and a.ProductID=c.Id
	and a.Deleted=0 and b.Deleted=0
	and DATEDIFF(day,getdate(),a.startdate)<=30
	and DATEDIFF(day,getdate(),a.startdate)>=-30
	
    and ( c.ProductCode=@tourcode 
            or charindex(@tourcode+'-',a.ProductCode,0)>0 
            or a.ProductCode=@tourcode )
	and b.Mobile=@tel
	and ( charindex(@name,b.GuideName,0)>0 
          or charindex(@ftname,b.GuideName,0)>0 )
";
            sSql = sSql.Replace("@name", StringTool.SqlQ(name))
                .Replace("@ftname", StringTool.SqlQ(ftname))
                .Replace("@tel", StringTool.SqlQ(mobile))
                .Replace("@tourcode", StringTool.SqlQ(tourcode));

            DataTable dt = DirectRun.ExecuteToDataTable(sSql);
            if (dt != null)
                return dt;
            else
                return null;
        }

        /// <summary>
        /// 身份认证 通过 手机号码 
        /// </summary>
        /// <param name="mobile"></param>
        /// <returns></returns>
        public DataTable GetGuideByMobile(string mobile)
        {
            string sSql = @"
            select b.Id as guideid,b.GuideName as name,b.Mobile as mobile
            from BS_GuideInfo b with(nolock)  
            where b.Deleted=0
	            and b.Mobile=@tel
            order by b.InputDate desc
            ";
            sSql = sSql.Replace("@tel", StringTool.SqlQ(mobile));

            DataTable dt = DirectRun.ExecuteToDataTable(sSql);
            if (dt != null)
                return dt;
            else
                return null;
        }
    }
}