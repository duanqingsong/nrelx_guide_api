using NHibernate;
using nRelax.DAL;
using nRelax.Tour.BLL;
using System.Data;
using System.Data.SqlClient;

namespace nRelax.Tour.GuideApi.Service
{
    public class TourGroupService :TourGroupBiz
    {

        /// <summary>
        /// 獲取可以報賬的團列表
        /// </summary>
        /// <param name="guideid"></param>
        /// <returns></returns>
        public DataTable GetCanFeeListByGuideID(decimal guideid)
        {
            ISession session = DBSessions.GetSession();
            string sSql = @"SELECT Id AS tgid,
	productcode,productname,busno,flagno, processdays, convert(nvarchar(10),startdate,121) as startdate,
	CASE WHEN GETDATE()< StartDate THEN 1 --未出发
		WHEN GETDATE()>=StartDate AND GETDATE()<DATEADD(DAY,ProcessDays,StartDate) THEN 0 --进行中
		WHEN GETDATE()>=DATEADD(DAY,ProcessDays,StartDate) THEN 2 --已出发
	END AS status,ApplyFeeStatus,ApplyFeeDate,ApplyFeeIsCancel
FROM dbo.TG_TourGroup with(nolock) 
    
WHERE  Status >=0  AND Deleted=0 and status!=9 --排除已結算
    and DATEADD(DAY,ProcessDays+20,StartDate)>GETDATE()  --結束后20天內
	and DATEADD(DAY,0,StartDate)<=GETDATE() --出發當天的團
	AND GuideID=@GuideID 
order by ApplyFeeStatus,startdate 
";
            SqlParameter par0 = new SqlParameter("@GuideID", guideid);
            SqlParameter[] parCol = { par0 };
            DataSet ds = DirectRun.ExecuteSqlQuery(sSql, parCol);
            if (ds.Tables.Count > 0)
                return ds.Tables[0];
            else
                return null;

        }
    }
}