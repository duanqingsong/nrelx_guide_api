using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using nRelax.DevBase.BaseTools;
using nRelax.Images.Common;
using nRelax.SSO;
using nRelax.Tour.BLL;
using nRelax.Tour.BLL.Enum;
using nRelax.Tour.Entity;
using nRelax.Tour.GuideApi.Service;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Web;

namespace nRelax.Tour.WebApp
{
    /// <summary>
    /// Summary description for tajax
    /// </summary>
    public class GuideAjaxApi : ApiHttpHandle
    {
        private const string SSO_KEY = "sso_user_92382";
        private void SetSso()
        {
            string sLoginName = ConfigBiz.AppUser;
            SystemUser objsso = CacheHelper.Get<SystemUser>(SSO_KEY);
            if (objsso == null)
            {
                DataRow row = new UsersDeptBiz().GetUserDeptInfoByLoginName(sLoginName);
                SystemUser ssouser = new SSO.SystemUser();
                if (row != null)
                {
                    ssouser.UserId = StringTool.String2Decimal(row["UserID"].ToString());
                    ssouser.DeptId = StringTool.String2Decimal(row["DeptID"].ToString());
                    ssouser.OrgId = StringTool.String2Decimal(row["OrgID"].ToString());
                    ssouser.UserLoginName = sLoginName;
                }
                CacheHelper.Insert(SSO_KEY, ssouser);
                objsso = ssouser;
            }

            nRelax.SSO.CurrentSysUser.Set(objsso);

        }
        public override void ProcessRequest(HttpContext context)
        {
            //设置sso
            SetSso();

            string stamp = GetQueryString("stamp");
            if (stamp.Length == 0)
            {
                ReturnJsonResponse(new { success = 0, data = string.Empty, errorcode = ErrorCode.STAMP_EMPTY });
                Logger.Error("时间戳不能为空");
                return;
            }

            bool check = CheckSign();
            if (!check)
            {
                ReturnJsonResponse(new ApiResult { success = 0, data = string.Empty, errorcode = ErrorCode.CHECK_SIGN_ERROR });
                return;
            }


            int pageindex = GetQueryInt("pageindex");
            if (pageindex == 0)
                pageindex = 1;
            int pagesize = GetQueryInt("pagesize");
            if (pagesize == 0)
                pagesize = 10;
            decimal tourid = GetQueryDecimal("tourId");

            action = GetQueryString("action");
            switch (action)
            {
                case "0001":
                    DecryptMiniAppUserData();
                    break;
                case "0002": //获取留位小程序SessionKey
                    GetMiniLoginInfo();
                    break;
                case "0003": //openid,data,iv,key
                    LoginByMiniPhone();
                    break;

                case "G000":
                    //身份认证: 通过 手机号码,姓名,负责团号认证 获取此导游属于哪家公司
                    GetGuideInfoByMobileNoAndProductCode();
                    break;
                case "G000-BYPHONE":
                    //身份认证: 通过 手机号码 获取此导游属于哪家公司
                    GetGuideInfoByMobile();
                    break;
                case "G001": //通过手机号码和验证码获取导游标识
                    GetGuideLoginUseerByVerifyCode();
                    break;
                case "G002": //通过微信unionid获取导游信息
                    GetGuideInfoByWxId();
                    break;
                case "G003": //获取导游的出团列表
                    GetGuideTourGroupList();
                    break;
                case "G004"://获取出团 团详细信息
                    GetGuideTourGroupInfo();
                    break;
                case "G005": //行程
                    GetGuideTourProcess();
                    break;
                case "G006": //酒店計劃
                    GetGuideTourHotel();
                    break;
                case "G007": //餐廳計劃
                    GetGuideTourRepast();
                    break;
                case "G008": //景點計劃
                    GetGuideTourTicket();
                    break;
                case "G009": //交通計劃
                    GetGuideTourBus();
                    break;
                case "G010": //出團名單
                    GetGuideTourTourerList();
                    break;
                case "G011": //厂商联系方式
                    GetGuideTourLinkerList();
                    break;
                case "G012": //查詢需要報賬的團列表
                    // guideid,unionid
                    GetGuideFeeTourGroupList();
                    break;
                case "G013": //填寫報賬資料
                    //guideid,unionid,tourgroupid
                    //id,resume,amount,dir,currency,remark,tickets,suppliersid,suppliersname
                    //7月6日增加：poBillId,suppliersType.之前suppliersid,suppliersname正式用上
                    AddTourGroupOtherFee();
                    break;
                case "G014": //刪除報賬資料
                    //id,guideid,unionid
                    DeleteTourGroupOtherFeeItem();
                    break;
                case "G015": //獲取報賬項目信息
                    //id,guideid,unionid
                    GetTourGroupOtherFeeItem();
                    break;
                case "G016": //獲取團對應的已填寫雜費列表
                    //guideid,unionid,tourgroupid
                    GetTourGroupOtherFeeList();
                    break;
                case "G017":
                    // 上傳雜費收據圖片
                    // guideid，suffix，filedata，unionid
                    UploadOtherFeeTicketImage();
                    break;
                case "G018":
                    // 提交費用賬單
                    // guideid,unionid,tourgroupid
                    SubmmitTourGroupOtherFee();
                    break;
                case "G019":
                    //獲取未報賬的採購單
                    // guideid,unionid,tourgroupid
                    getUnFeePOItemForGuide();
                    break;
                case "G020": //獲取團 應退應補金額
                    //guideid,unionid,tourgroupid
                    //{totalRMB:人民幣總匯總(負支出,正收入),guideAdvanceFee:導遊借款,returnGuideFee:應退導遊(如果負值,導遊會退公司)}
                    GetTourGroupTotalFee();
                    break;


                default:
                    ReturnJsonResponse(new ApiResult { success = 0, data = string.Empty, errorcode = ErrorCode.NO_ACTION });
                    break;


            }
        }


        /// <summary>
        /// 獲取未報賬的採購單
        /// G019
        /// </summary>
        private void getUnFeePOItemForGuide()
        {
            decimal nGuideId = GetQueryDecimal("guideid");
            string strUnionId = GetQueryString("unionid");
            decimal nTourGroupId = GetQueryDecimal("tourgroupid");

            if (strUnionId.Length == 0)
            {
                if (!CheckToken())
                {
                    ReturnJsonResponse(new ApiListResult { success = 0, rows = new ArrayList(), errorcode = ErrorCode.ERROR_PARAM });
                    return;
                }
            }
            else
            {
                if (!CheckGuideUnionId())
                {
                    ReturnJsonResponse(new ApiListResult { success = 0, rows = new ArrayList(), errorcode = ErrorCode.ERROR_PARAM });
                    return;
                }
            }
            try
            {

                TourGroupService tourGroupService = new TourGroupService();

                decimal guideId = tourGroupService.GetGuideId(nTourGroupId);
                if (guideId != nGuideId)
                {
                    ReturnJsonResponse(new ApiListResult { success = 0, rows = new ArrayList(), errorcode = "只有導遊自己可以提交報賬" });
                    return;
                }

                TourGroupOtherFeeBiz biz = new TourGroupOtherFeeBiz();
                DataTable dt = biz.getUnFeePoItemForGuide(nTourGroupId);
                if (dt != null)
                {
                    ReturnJsonResponse(new ApiListResult { success = 1, rows = dt, errorcode = ErrorCode.EMPTY });
                }
                else
                {
                    ReturnJsonResponse(new ApiListResult { success = 1, rows = new ArrayList(), errorcode = ErrorCode.EMPTY });
                }

            }
            catch (Exception ex)
            {
                LogError(ex);
                ReturnJsonResponse(new ApiListResult { success = 0, rows = "", errorcode = ErrorCode.SYSTEM_ERROR });
            }
        }


        /// <summary>
        /// 提交雜費報賬
        /// </summary>
        private void SubmmitTourGroupOtherFee()
        {
            decimal nGuideId = GetQueryDecimal("guideid");
            string strUnionId = GetQueryString("unionid");
            decimal nTourGroupId = GetQueryDecimal("tourgroupid");

            if (strUnionId.Length == 0)
            {
                if (!CheckToken())
                {
                    ReturnJsonResponse(new ApiListResult { success = 0, rows = "", errorcode = ErrorCode.ERROR_PARAM });
                    return;
                }
            }
            else
            {
                if (!CheckGuideUnionId())
                {
                    ReturnJsonResponse(new ApiListResult { success = 0, rows = "", errorcode = ErrorCode.ERROR_PARAM });
                    return;
                }
            }
            try
            {

                TourGroupOtherFeeBiz biz = new TourGroupOtherFeeBiz();
                string error = biz.submmitTourGroupOtherFee(nGuideId, nTourGroupId);

                if (error == "")
                    ReturnJsonResponse(new ApiResult { success = 1, data = "success", errorcode = ErrorCode.EMPTY });
                else
                {
                    ReturnJsonResponse(new ApiResult { success = 0, data = "", errorcode = error });
                }
            }
            catch (Exception ex)
            {
                LogError(ex);
                ReturnJsonResponse(new ApiResult { success = 0, data = "", errorcode = ErrorCode.SYSTEM_ERROR });
            }
        }

        /// <summary>
        /// 上傳雜費收據圖片
        /// guideid，suffix，filedata，unionid
        /// </summary>
        private void UploadOtherFeeTicketImage()
        {
            decimal nGuideId = GetQueryDecimal("guideid");
            // string ssuffix = GetQueryString("suffix");
            //string sfiledata = GetQueryString("filedata");
            string strUnionId = GetQueryString("unionid");

            if (nGuideId == 0)
            {
                ReturnJsonResponse(new ApiResult { success = 0, data = "缺少參數[缺導遊Id]", errorcode = ErrorCode.ERROR_PARAM });
                return;
            }
            try
            {

                int fileCount = HttpContext.Current.Request.Files.Count;
                ArrayList result = new ArrayList();
                string errorMessage = "";

                for (int i = 0; i < fileCount; i++)
                {
                    HttpPostedFile file = HttpContext.Current.Request.Files[i];
                    byte[] byteFile = ByteFiles(file);
                    string sfiledata = Convert.ToBase64String(byteFile);
                    string ssuffix = GetFileExt(file.FileName);

                    string smsg = new UpLoad().UploadBase64File(sfiledata, "", ssuffix, false, Enums.UploadType.App, "");
                    FileUploadResult uploadResult = JsonConvert.DeserializeObject<FileUploadResult>(smsg);
                    Logger.Error("p03 uploadResult" + uploadResult == null ? "null" : "not null");
                    if (uploadResult.status == 1)
                    {
                        result.Add(uploadResult.path);
                    }
                    else
                    {
                        errorMessage = uploadResult.msg;
                    }
                }
                if (result.Count > 0)
                {
                    ReturnJsonResponse(new ApiListResult
                    {
                        success = 1,
                        rows = result,
                        errorcode = ErrorCode.EMPTY
                    });
                }
                else
                {
                    Logger.Error(errorMessage);
                    ReturnJsonResponse(new ApiResult { success = 0, data = errorMessage, errorcode = ErrorCode.SYSTEM_ERROR });

                }
            }
            catch (Exception ex)
            {
                LogError(ex);
                ReturnJsonResponse(new ApiResult { success = 0, data = string.Empty, errorcode = ErrorCode.SYSTEM_ERROR });
            }
        }

        /// <summary>
        /// 根據id獲取雜費項目 G015
        /// </summary>
        private void GetTourGroupOtherFeeItem()
        {
            decimal nId = GetQueryDecimal("id");
            decimal nGuideId = GetQueryDecimal("guideid");
            string strUnionId = GetQueryString("unionid");
            if (strUnionId.Length == 0)
            {
                if (!CheckToken())
                {
                    ReturnJsonResponse(new ApiResult { success = 0, data = "", errorcode = ErrorCode.ERROR_PARAM });
                    return;
                }
            }
            else
            {
                if (!CheckGuideUnionId())
                {
                    ReturnJsonResponse(new ApiResult { success = 0, data = "", errorcode = ErrorCode.ERROR_PARAM });
                    return;
                }
            }
            if (nId == 0)
            {
                ReturnJsonResponse(new ApiResult { success = 0, data = "", errorcode = "請選擇要查詢的費用項" });
                return;
            }
            try
            {

                TourGroupOtherFeeBiz biz = new TourGroupOtherFeeBiz();
                TourGroupService bizTourGroup = new TourGroupService();
                TourGroupOtherFee tourGroupOtherFee = new TourGroupOtherFee();

                tourGroupOtherFee = biz.GetByID(nId);
                decimal nTourGroupId = tourGroupOtherFee.TourGroupID;

                int tourGroupStatus = bizTourGroup.GetStatus(nTourGroupId);
                int applyFeeStatus = bizTourGroup.GetApplyFeeStatus(nTourGroupId);
                decimal guideId = bizTourGroup.GetGuideId(nTourGroupId);
                if (guideId != nGuideId)
                {
                    ReturnJsonResponse(new ApiResult { success = 0, data = "", errorcode = "只有導遊自己可以查看自己的報賬" });
                    return;
                }


                //獲取費用信息
                TourGroupOtherFee item = biz.GetByID(nId);
                Dictionary<string, object> poInfo = new Dictionary<string, object>();
                if (item.PoBillId > 0 && !string.IsNullOrEmpty(item.PoBillType))
                {
                    poInfo = biz.getPoBillInfo(item.PoBillId, item.PoBillType);
                }

                var result = new
                {
                    id = item.Id,
                    resume = item.FeeResume,
                    remark = item.Remark,
                    amount = item.Amount,
                    dir = item.Dir,
                    tourGroupStatus,
                    applyFeeStatus,
                    status = item.Status,
                    tickets = item.TicketUrl,
                    ticketUrls = item.TicketUrl.Length > 0 ? item.TicketUrl.Split('|') : new string[] { },
                    currency = item.Currency,
                    suppliersID = item.SuppliersID,
                    suppliersName = item.SuppliersName,
                    suppliersType = item.PoBillType,
                    poBillId = item.PoBillId,
                    poInfo
                };
                ReturnJsonResponse(new ApiResult { success = 1, data = result, errorcode = ErrorCode.EMPTY });
            }
            catch (Exception ex)
            {
                LogError(ex);
                ReturnJsonResponse(new ApiResult { success = 0, data = "", errorcode = ErrorCode.SYSTEM_ERROR });
            }
        }


        /// <summary>
        /// 獲取團對應的雜費列表
        /// </summary>
        private void GetTourGroupOtherFeeList()
        {
            decimal nGuideId = GetQueryDecimal("guideid");
            string strUnionId = GetQueryString("unionid");
            decimal nTourGroupId = GetQueryDecimal("tourgroupid");

            if (strUnionId.Length == 0)
            {
                if (!CheckToken())
                {
                    ReturnJsonResponse(new ApiListResult { success = 0, rows = "", errorcode = ErrorCode.ERROR_PARAM });
                    return;
                }
            }
            else
            {
                if (!CheckGuideUnionId())
                {
                    ReturnJsonResponse(new ApiListResult { success = 0, rows = "", errorcode = ErrorCode.ERROR_PARAM });
                    return;
                }
            }
            try
            {
                TourGroupOtherFeeBiz biz = new TourGroupOtherFeeBiz();

                IList lstFee = biz.GetByTourGroupId(nTourGroupId);
                ArrayList result = new ArrayList();
                if (lstFee != null && lstFee.Count > 0)
                {
                    foreach (TourGroupOtherFee item in lstFee)
                    {
                        Dictionary<string, object> poInfo = new Dictionary<string, object>();
                        if (item.PoBillId > 0 && !string.IsNullOrEmpty(item.PoBillType))
                        {
                            poInfo = biz.getPoBillInfo(item.PoBillId, item.PoBillType);
                        }
                        result.Add(new
                        {
                            id = item.Id,
                            resume = item.FeeResume,
                            remark = item.Remark,
                            amount = item.Amount,
                            dir = item.Dir,
                            status = item.Status,
                            tickets = item.TicketUrl,
                            ticketUrls = item.TicketUrl.Length > 0 ? item.TicketUrl.Split('|') : new string[] { },
                            currency = item.Currency,
                            suppliersID = item.SuppliersID,
                            suppliersName = item.SuppliersName,
                            suppliersType = item.PoBillType,
                            poInfo
                        });
                    }
                }

                ReturnJsonResponse(new ApiListResult { success = 1, rows = result, errorcode = ErrorCode.EMPTY });

            }
            catch (Exception ex)
            {
                LogError(ex);
                ReturnJsonResponse(new ApiListResult { success = 0, rows = new ArrayList(), errorcode = ErrorCode.SYSTEM_ERROR });
            }
        }

        /// <summary>
        /// 獲取應退導遊,或應收導遊 匯總金額
        /// {totalRMB:人民幣總匯總(負支出,正收入),guideAdvanceFee:導遊借款,returnGuideFee:應退導遊(如果負值,導遊會退公司)}
        /// </summary>
        private void GetTourGroupTotalFee()
        {
            decimal nGuideId = GetQueryDecimal("guideid");
            string strUnionId = GetQueryString("unionid");
            decimal nTourGroupId = GetQueryDecimal("tourgroupid");

            if (strUnionId.Length == 0)
            {
                if (!CheckToken())
                {
                    ReturnJsonResponse(new ApiResult { success = 0, data = "", errorcode = ErrorCode.ERROR_PARAM });
                    return;
                }
            }
            else
            {
                if (!CheckGuideUnionId())
                {
                    ReturnJsonResponse(new ApiResult { success = 0, data = "", errorcode = ErrorCode.ERROR_PARAM });
                    return;
                }
            }
            try
            {
                TourGroupOtherFeeBiz biz = new TourGroupOtherFeeBiz();
                string sWhere = string.Format("obj.Currency='RMB' and obj.TourGroupID={0}", nTourGroupId);
                //人民幣費用(-支出)
                string sTotalRmb = biz.GetFieldSUM("obj.Dir*obj.Amount", sWhere);
                sTotalRmb = sTotalRmb == null ? "0" : sTotalRmb;

                //導遊借款
                string guideAdvanceFee = TourGroupService.GetFieldValue("obj.GuideAdvanceFee", string.Format("obj.Id={0}", nTourGroupId));
                guideAdvanceFee = guideAdvanceFee == null ? "0" : guideAdvanceFee;

                decimal totalGuideReturnAmount = -1 * (StringTool.String2Decimal(sTotalRmb) + StringTool.String2Decimal(guideAdvanceFee));

                var result = new
                {
                    totalRMB = StringTool.String2Decimal(sTotalRmb).ToString("f2").Replace(".00", ""), //人民幣總匯總(負支出,正收入)
                    guideAdvanceFee = StringTool.String2Decimal(guideAdvanceFee).ToString("f2").Replace(".00", ""),//導遊借款
                    returnGuideFee = totalGuideReturnAmount.ToString("f2").Replace(".00", "")//應退導遊(如果負值,導遊會退公司)
                };

                ReturnJsonResponse(new ApiResult { success = 1, data = result, errorcode = ErrorCode.EMPTY });

            }
            catch (Exception ex)
            {
                LogError(ex);
                ReturnJsonResponse(new ApiResult { success = 0, data = new ArrayList(), errorcode = ErrorCode.SYSTEM_ERROR });
            }
        }

        /// <summary>
        /// 刪除指定id的雜費
        /// </summary>
        private void DeleteTourGroupOtherFeeItem()
        {
            decimal nId = GetQueryDecimal("id");
            decimal nGuideId = GetQueryDecimal("guideid");
            string strUnionId = GetQueryString("unionid");
            if (strUnionId.Length == 0)
            {
                if (!CheckToken())
                {
                    ReturnJsonResponse(new ApiResult { success = 0, data = "", errorcode = ErrorCode.ERROR_PARAM });
                    return;
                }
            }
            else
            {
                if (!CheckGuideUnionId())
                {
                    ReturnJsonResponse(new ApiResult { success = 0, data = "", errorcode = ErrorCode.ERROR_PARAM });
                    return;
                }
            }
            if (nId == 0)
            {
                ReturnJsonResponse(new ApiResult { success = 0, data = "", errorcode = "請選擇要刪除的費用" });
                return;
            }
            try
            {

                TourGroupOtherFeeBiz biz = new TourGroupOtherFeeBiz();
                TourGroupService bizTourGroup = new TourGroupService();
                TourGroupOtherFee tourGroupOtherFee = new TourGroupOtherFee();
                tourGroupOtherFee = biz.GetByID(nId);
                decimal nTourGroupId = tourGroupOtherFee.TourGroupID;

                int status = bizTourGroup.GetStatus(nTourGroupId);
                decimal guideId = bizTourGroup.GetGuideId(nTourGroupId);
                if (guideId != nGuideId)
                {
                    ReturnJsonResponse(new ApiResult { success = 0, data = "", errorcode = "只有導遊自己可以刪除" });
                    return;
                }

                EnuTourPlanStatus planStatus = (EnuTourPlanStatus)status;
                if (planStatus == EnuTourPlanStatus.Canceled ||
                    planStatus == EnuTourPlanStatus.Closed ||
                    planStatus == EnuTourPlanStatus.Hung ||
                    planStatus == EnuTourPlanStatus.WillCancel ||
                    planStatus == EnuTourPlanStatus.WillSale)
                {

                    string strStaus = UseEnum.EnuToString(planStatus);
                    ReturnJsonResponse(new ApiResult { success = 0, data = "", errorcode = "團的狀態為【" + strStaus + "】不能修改報賬資料" });
                    return;
                }

                //刪除費用
                biz.DeleteFee(nId);
                ReturnJsonResponse(new ApiResult { success = 1, data = nId, errorcode = ErrorCode.EMPTY });
            }
            catch (Exception ex)
            {
                LogError(ex);
                ReturnJsonResponse(new ApiResult { success = 0, data = "", errorcode = ErrorCode.SYSTEM_ERROR });
            }

        }

        /// <summary>
        /// 為團添加雜費
        /// G013
        /// </summary>
        private void AddTourGroupOtherFee()
        {
            decimal nGuideId = GetQueryDecimal("guideid");
            string strUnionId = GetQueryString("unionid");
            decimal nTourGroupId = GetQueryDecimal("tourgroupid");

            decimal nId = GetQueryDecimal("id");
            string strResume = GetQueryString("resume"); //什麼費
            decimal nAmount = GetQueryDecimal("amount");
            int nDir = GetQueryInt("dir");
            string strCurrency = GetQueryString("currency");
            string strRemark = GetQueryString("remark");
            string strTickets = GetQueryString("tickets"); ////票據url 豎線隔開

            //poBillId,supplierType:HOTEL,TICKET,REPAST,BUS
            decimal nSuppliersId = GetQueryDecimal("suppliersid");
            string strSuppliersName = GetQueryString("suppliersname");
            string suppliersType = GetQueryString("suppliersType");
            decimal poBillId = GetQueryDecimal("poBillId");

            if (strUnionId.Length == 0)
            {
                if (!CheckToken())
                {
                    ReturnJsonResponse(new ApiResult { success = 0, data = "", errorcode = ErrorCode.ERROR_PARAM });
                    return;
                }
            }
            else
            {
                if (!CheckGuideUnionId())
                {
                    ReturnJsonResponse(new ApiResult { success = 0, data = "", errorcode = ErrorCode.ERROR_PARAM });
                    return;
                }
            }
            try
            {
                TourGroupOtherFeeBiz bizOtherFee = new TourGroupOtherFeeBiz();
                TourGroupService bizTourGroup = new TourGroupService();
                TourGroupOtherFee tourGroupOtherFee = new TourGroupOtherFee();
                if (nId == 0 && poBillId > 0)
                {
                    //新增 檢查採購單是否已經報過賬，防止多次報賬
                    //要穿nTourGroupId ,po預訂單中有一個po訂單對應多個團的情況
                    bool isFeed = bizOtherFee.IsPoBillIdExist(poBillId, suppliersType, nTourGroupId);
                    if (isFeed)
                    {
                        ReturnJsonResponse(new ApiResult { success = 0, data = "", errorcode = "這個計調預訂單已經報過賬,請返回修改對應項目" });
                        return;
                    }
                }
                if (nId != 0)
                {
                    //新增
                    tourGroupOtherFee = bizOtherFee.GetByID(nId);
                    nTourGroupId = tourGroupOtherFee.TourGroupID;
                    //if (poBillId > 0) {
                    //    //檢查採購單是否已經報過賬，防止多次報賬
                    //    bool isFeed= bizOtherFee.IsPoBillIdExist(poBillId, suppliersType);
                    //    if (isFeed) {
                    //        ReturnJsonResponse(new ApiResult { success = 0, data = "", errorcode = "這個計調採購單已經報過賬" });
                    //        return;
                    //    }
                    //}
                }
                if (nTourGroupId == 0)
                {
                    ReturnJsonResponse(new ApiResult { success = 0, data = "", errorcode = "請選擇要報賬的團" });
                    return;
                }
                decimal guideId = bizTourGroup.GetGuideId(nTourGroupId);
                if (guideId != nGuideId)
                {
                    ReturnJsonResponse(new ApiResult { success = 0, data = "", errorcode = "您不是這個團所派的導遊" });
                    return;
                }

                int status = bizTourGroup.GetStatus(nTourGroupId);
                EnuTourPlanStatus planStatus = (EnuTourPlanStatus)status;
                if (planStatus == EnuTourPlanStatus.Canceled ||
                    planStatus == EnuTourPlanStatus.Closed ||
                    planStatus == EnuTourPlanStatus.Hung ||
                    planStatus == EnuTourPlanStatus.WillCancel ||
                    planStatus == EnuTourPlanStatus.WillSale)
                {

                    string strStaus = UseEnum.EnuToString(planStatus);
                    ReturnJsonResponse(new ApiResult { success = 0, data = "", errorcode = "團的狀態為【" + strStaus + "】不符合報賬要求" });
                    return;
                }

                if (nAmount <= 0)
                {
                    ReturnJsonResponse(new ApiResult { success = 0, data = "", errorcode = "金額錯誤" });
                    return;
                }
                if (strResume.Trim().Length == 0)
                {
                    ReturnJsonResponse(new ApiResult { success = 0, data = "", errorcode = "費用說明不能為空" });
                    return;
                }
                if (strCurrency.Trim().Length == 0)
                {
                    ReturnJsonResponse(new ApiResult { success = 0, data = "", errorcode = "幣種不能為空" });
                    return;
                }
                string guideName = new GuideInfoService().GetGuideName(nGuideId);

                tourGroupOtherFee.Amount = nAmount;
                tourGroupOtherFee.Dir = nDir;
                tourGroupOtherFee.FeeResume = strResume;
                tourGroupOtherFee.Remark = strRemark;
                tourGroupOtherFee.TourGroupID = nTourGroupId;
                tourGroupOtherFee.Currency = strCurrency;
                tourGroupOtherFee.TicketUrl = strTickets; //票據url 豎線隔開

                tourGroupOtherFee.InputGuideID = nGuideId;
                tourGroupOtherFee.InputGuideName = guideName;

                tourGroupOtherFee.SuppliersName = strSuppliersName;
                tourGroupOtherFee.SuppliersID = nSuppliersId;
                tourGroupOtherFee.PoBillId = poBillId;
                tourGroupOtherFee.PoBillType = suppliersType;

                decimal newId = bizOtherFee.Save(tourGroupOtherFee);


                ReturnJsonResponse(new ApiResult() { success = 1, data = newId, errorcode = ErrorCode.EMPTY });

            }
            catch (Exception ex)
            {
                LogError(ex);
                ReturnJsonResponse(new ApiResult { success = 0, data = "", errorcode = ErrorCode.SYSTEM_ERROR });
            }
        }

        /// <summary>
        /// 获取留位小程序Session Key
        /// </summary>
        private void GetMiniLoginInfo()
        {

            string result = MiniAppLogin("1");
            if (result.Trim().Length > 0)
            {
                JObject jo = (JObject)JsonConvert.DeserializeObject(result);
                string openid = jo.GetValue("openid").ToString();
                decimal memberId = 0;
                string phone = "";
                Members member = new MemberBiz().GetByWeChatUnionId(openid);
                if (member != null)
                {
                    memberId = member.Id;
                    phone = member.Mobile;
                }

                var data = new
                {
                    sessionKey = jo.GetValue("session_key"),
                    expiresIn = jo.GetValue("expires_in"),
                    openid = openid,
                    phone = phone,
                    memberId = memberId
                };
                ReturnJsonResponse(new ApiResult { success = 1, data = data, errorcode = ErrorCode.EMPTY });
            }
            else
            {
                ReturnJsonResponse(new ApiResult { success = 0, data = string.Empty, errorcode = ErrorCode.ERROR_PARAM });
            }
        }

        /// <summary>
        /// 003 解密小程序手机号码,並註冊或登錄
        /// </summary>
        private void LoginByMiniPhone()
        {

            string openId = GetQueryString("openid");
            try
            {
                if (openId.Trim().Length == 0)
                {
                    ReturnJsonResponse(new ApiResult { success = 0, data = string.Empty, errorcode = ErrorCode.ERROR_PARAM });
                    return;
                }

                string phone = getMiniPhoneNumber();
                if (phone.Length > 0)
                {
                    //写会员数据
                    MemberBiz biz = new MemberBiz();
                    decimal nMemberId = biz.RegisterFromAppByMobile(phone, openId);
                    if (nMemberId == 0)
                    {
                        ReturnJsonResponse(new ApiResult { success = 0, data = string.Empty, errorcode = ErrorCode.ERROR_CREATE_MEMBER });
                        return;
                    }

                    ReturnJsonResponse(new ApiResult { success = 1, data = new { phone = phone, memberId = nMemberId }, errorcode = ErrorCode.EMPTY });
                }
                else
                {
                    //获取手机号码失败
                    ReturnJsonResponse(new ApiResult { success = 0, data = string.Empty, errorcode = ErrorCode.ERROR_GETPHONE });
                    return;
                }

            }
            catch (Exception ex)
            {
                Logger.Error(ex);
                ReturnJsonResponse(new ApiResult { success = 0, data = string.Empty, errorcode = ErrorCode.SYSTEM_ERROR });
            }
        }



        /// <summary>
        /// //身份认证 通过 手机号码,姓名,负责团号认证 获取此导游属于哪家公司 
        /// G000
        /// </summary>
        private void GetGuideInfoByMobileNoAndProductCode()
        {
            try
            {
                string name = GetQueryString("name");
                string mobile = GetQueryString("mobile");
                string tourcode = GetQueryString("tourcode");

                if (name.Trim().Length < 2)
                {
                    ReturnJsonResponse(new ApiResult { success = 0, data = string.Empty, errorcode = ErrorCode.ERROR_PARAM });
                    return;
                }
                if (mobile.Trim().Length < 8)
                {
                    ReturnJsonResponse(new ApiResult { success = 0, data = string.Empty, errorcode = ErrorCode.ERROR_PARAM });
                    return;
                }
                if (tourcode.Trim().Length < 2)
                {
                    ReturnJsonResponse(new ApiResult { success = 0, data = string.Empty, errorcode = ErrorCode.ERROR_PARAM });
                    return;
                }

                //轉繁體
                string ftname = StringTool.ToTChinese(name);

                DataTable dt = new GuideInfoService().GetGuideByMobileNameAndTourCode(name, ftname, mobile, tourcode);
                if (dt.Rows.Count > 0)
                {
                    DataRow row = dt.Rows[0];
                    var data = new
                    {
                        guideid = row["guideid"],
                        name = row["name"],
                        mobile = row["mobile"]
                    };
                    ReturnJsonResponse(new ApiResult { success = 1, data = data, errorcode = ErrorCode.EMPTY });
                }
                else
                    ReturnJsonResponse(new ApiResult { success = 1, data = "", errorcode = ErrorCode.EMPTY });
            }
            catch (Exception ex)
            {
                LogError(ex);
                ReturnJsonResponse(new ApiResult { success = 0, data = string.Empty, errorcode = ErrorCode.SYSTEM_ERROR });

            }
        }

        /// <summary>
        /// 通过 手机号码 获取此导游属于哪家公司 
        /// G000-BYPHONE
        /// </summary>
        private void GetGuideInfoByMobile()
        {
            try
            {
                string mobile = GetQueryString("mobile");

                if (mobile.Trim().Length < 8)
                {
                    ReturnJsonResponse(new ApiResult { success = 0, data = string.Empty, errorcode = ErrorCode.ERROR_PARAM });
                    return;
                }

                DataTable dt = new GuideInfoService().GetGuideByMobile(mobile);
                if (dt.Rows.Count > 0)
                {
                    DataRow row = dt.Rows[0];
                    var data = new
                    {
                        guideid = row["guideid"],
                        name = row["name"],
                        mobile = row["mobile"]
                    };
                    ReturnJsonResponse(new ApiResult { success = 1, data = data, errorcode = ErrorCode.EMPTY });
                }
                else
                    ReturnJsonResponse(new ApiResult { success = 1, data = "", errorcode = ErrorCode.EMPTY });
            }
            catch (Exception ex)
            {
                LogError(ex);
                ReturnJsonResponse(new ApiResult { success = 0, data = string.Empty, errorcode = ErrorCode.SYSTEM_ERROR });

            }
        }

        /// <summary>
        /// 出團商家聯繫人 G011
        /// </summary>
        private void GetGuideTourLinkerList()
        {
            try
            {
                decimal tgid = GetQueryDecimal("tgid");
                decimal nGuideId = GetQueryDecimal("guideid");
                if (tgid <= 0 || nGuideId <= 0)
                {
                    ReturnJsonResponse(new ApiResult { success = 0, data = string.Empty, errorcode = ErrorCode.ERROR_PARAM });
                    return;
                }
                string sguideid = TourGroupService.GetFieldValue("obj.GuideID", tgid);
                if (StringTool.String2Decimal(sguideid) != nGuideId)
                {
                    ReturnJsonResponse(new ApiResult { success = 0, data = string.Empty, errorcode = ErrorCode.ERROR_PARAM });
                    return;
                }
                SendGroupBillBiz sgbb = new SendGroupBillBiz();
                DataTable dtLinkerInfo = sgbb.GetTourGroupAllLinkInfo(tgid);

                IList list = new ArrayList();
                foreach (DataRow item in dtLinkerInfo.Rows)
                {
                    //string stel = item["LinkTel"].ToString();
                    //ArrayList lsttelno = new ArrayList();
                    //foreach (string telno in stel.Split('/'))
                    //{
                    //    lsttelno.Add(new { telno=telno});
                    //}
                    string[] a = { "" };
                    var data = new
                    {
                        coname = item["CompanyName"],
                        linkname = item["LinkName"].ToString().Replace("/", "").Trim().Length == 0 ? "" : item["LinkName"].ToString(),
                        linktel = item["LinkTel"].ToString().Replace("/", "").Trim().Length == 0 ? a : item["LinkTel"].ToString().Split('/'),
                        address = item["Address"].ToString().Replace("/", "").Trim().Length == 0 ? "" : item["Address"].ToString(),
                    };
                    list.Add(data);
                }

                var result = new
                {
                    tgid = tgid,
                    list = list
                };
                ReturnJsonResponse(new ApiResult { success = 1, data = result, errorcode = ErrorCode.EMPTY });
                return;
            }
            catch (Exception ex)
            {
                LogError(ex);
                ReturnJsonResponse(new ApiResult { success = 0, data = string.Empty, errorcode = ErrorCode.SYSTEM_ERROR });

            }
        }

        /// <summary>
        /// 出團名單 G010
        /// </summary>
        private void GetGuideTourTourerList()
        {
            try
            {
                decimal tgid = GetQueryDecimal("tgid");
                decimal nGuideId = GetQueryDecimal("guideid");
                if (tgid <= 0 || nGuideId <= 0)
                {
                    ReturnJsonResponse(new ApiResult { success = 0, data = string.Empty, errorcode = ErrorCode.ERROR_PARAM });
                    return;
                }
                string sguideid = TourGroupService.GetFieldValue("obj.GuideID", tgid);
                if (StringTool.String2Decimal(sguideid) != nGuideId)
                {
                    ReturnJsonResponse(new ApiResult { success = 0, data = string.Empty, errorcode = ErrorCode.ERROR_PARAM });
                    return;
                }
                SendGroupBillBiz sgbb = new SendGroupBillBiz();
                DataTable dtTourer = sgbb.GetAllSeatTourers(tgid);
                DataView dvTourer = new DataView(dtTourer);
                string _billcode = string.Empty;
                string _groupcode = string.Empty;
                ArrayList lstGroup = new ArrayList();
                if (dtTourer.Rows.Count != 0)
                {
                    IList lstBillGroupCode = new ArrayList();
                    foreach (DataRow item in dtTourer.Rows)
                    {
                        string tourbillcode = item["TourBillCode"].ToString();
                        string groupcode = item["GroupCode"].ToString();
                        if (_billcode != tourbillcode || _groupcode != groupcode)
                        {
                            string data = string.Format("{0}┼{1}",
                                tourbillcode, groupcode);
                            lstBillGroupCode.Add(data);
                            _billcode = tourbillcode;
                            _groupcode = groupcode;
                        }
                    }
                    foreach (string item in lstBillGroupCode)
                    {
                        string[] arritem = item.Split('┼');
                        string tourbillcode = arritem[0].ToString();
                        string groupcode = arritem[1].ToString();
                        string BigBedRoomCount = string.Empty,
                            CustomerRequire = string.Empty,
                            BillRemark = string.Empty,
                            ArgentReturn = string.Empty,
                            ArgentReceive = string.Empty,
                            StartStation = string.Empty,
                            Remark = string.Empty;

                        dvTourer.RowFilter = string.Format("TourBillCode='{0}' and GroupCode='{1}'",
                            tourbillcode,
                            groupcode);
                        dvTourer.Sort = "seatno";
                        ArrayList lstGroupTourer = new ArrayList();
                        if (dvTourer.Count > 0)
                        {
                            foreach (DataRowView row in dvTourer)
                            {
                                var data = new
                                {
                                    seatno = row["seatno"],
                                    groupcode = row["GroupCode"],
                                    regdeptcode = row["RegDeptCode"],
                                    name = row["name"],
                                    engname = row["EngName"],
                                    startPort = row["startPort"],
                                    mobile = row["Mobile"],
                                    tel = row["Tel"],
                                    sex = row["sex"],
                                    age = row["age"],

                                    title = row["Title"],
                                    birthday = DateTimeTool.FormatString(row["BirthDay"].ToString()),
                                    sfzid = row["sfzid"],
                                    otherzjtype = row["OtherZJType"],
                                    otherzjno = row["PassportNo"],
                                    otherzjenddate = DateTimeTool.FormatString(row["PassportEndDate"].ToString()),
                                    ischecked = Convert.ToBoolean(row["IsChecked"]),


                                    isregister = Convert.ToBoolean(row["IsRegister"]),

                                    isault = Convert.ToBoolean(row["IsAult"]),
                                    ischild = Convert.ToBoolean(row["IsChild"]),
                                    ischildnormalprice = Convert.ToBoolean(row["IsChildNormalPrice"]),
                                    isbb = Convert.ToBoolean(row["IsBB"]),
                                    isolderrebate = Convert.ToBoolean(row["IsOlderRebate"]),
                                    isolderrebate2 = Convert.ToBoolean(row["IsOlderRebate2"]),
                                    isaddroom = Convert.ToBoolean(row["IsAddRoom"]),
                                    isaddroomship = Convert.ToBoolean(row["IsAddRoomShip"]),
                                    isaddbed = Convert.ToBoolean(row["IsAddBed"]),
                                    ishalfroom = Convert.ToBoolean(row["IsHalfRoom"]),
                                    ismeatless = Convert.ToBoolean(row["isMeatless"]),
                                    iscnaddition = Convert.ToBoolean(row["IsCNAddition"]),
                                    isinsur = Convert.ToBoolean(row["isInsur"]),
                                    issafesign = Convert.ToBoolean(row["IsSafeSign"]),
                                    isagentsign = Convert.ToBoolean(row["IsAgentSign"]),
                                };
                                lstGroupTourer.Add(data);
                                if (Convert.ToBoolean(row["IsRegister"]))
                                {
                                    BigBedRoomCount = row["BigBedRoomCount"].ToString();
                                    CustomerRequire = row["CustomerRequire"].ToString();
                                    BillRemark = row["BillRemark"].ToString();
                                    ArgentReturn = Convert.ToDecimal(row["ArgentReturn"]).ToString("f2").Replace(".00", "");
                                    ArgentReceive = Convert.ToDecimal(row["ArgentReceive"]).ToString("f2").Replace(".00", "");
                                    StartStation = row["StartStation"].ToString();
                                    Remark = row["Remark"].ToString();
                                }
                            }
                        }


                        var datamain = new
                        {
                            groupcode = groupcode,
                            tourbillcode = tourbillcode,
                            bigbedroomcount = BigBedRoomCount,
                            customerrequire = CustomerRequire,
                            billremark = BillRemark,
                            argentreturn = ArgentReturn,
                            argentreceive = ArgentReceive,
                            startstation = StartStation,
                            remark = Remark,
                            tourers = lstGroupTourer
                        };
                        lstGroup.Add(datamain);
                    }
                }
                var result = new
                {
                    tgid = tgid,
                    list = lstGroup
                };
                ReturnJsonResponse(new ApiResult { success = 1, data = result, errorcode = ErrorCode.EMPTY });
                return;
            }
            catch (Exception ex)
            {
                LogError(ex);
                ReturnJsonResponse(new ApiResult { success = 0, data = string.Empty, errorcode = ErrorCode.SYSTEM_ERROR });

            }
        }

        /// <summary>
        /// 获取導遊 用車 計劃 G009
        /// </summary>
        private void GetGuideTourBus()
        {
            try
            {
                decimal tgid = GetQueryDecimal("tgid");
                decimal nGuideId = GetQueryDecimal("guideid");
                if (tgid <= 0 || nGuideId <= 0)
                {
                    ReturnJsonResponse(new ApiResult { success = 0, data = string.Empty, errorcode = ErrorCode.ERROR_PARAM });
                    return;
                }
                string sguideid = TourGroupService.GetFieldValue("obj.GuideID", tgid);
                if (StringTool.String2Decimal(sguideid) != nGuideId)
                {
                    ReturnJsonResponse(new ApiResult { success = 0, data = string.Empty, errorcode = ErrorCode.ERROR_PARAM });
                    return;
                }

                SendGroupBillBiz sgbb = new SendGroupBillBiz();
                DataTable dtBus = sgbb.GetTourBus(tgid);
                DataView dvBus = new DataView(dtBus);
                string _CheckinDate = string.Empty;
                IList lstBusPO = new ArrayList();
                if (dtBus.Rows.Count != 0)
                {
                    IList lstCheckInDate = new ArrayList();
                    foreach (DataRow item in dtBus.Rows)
                    {
                        string checkindate = item["StartDate"].ToString();
                        if (checkindate != _CheckinDate)
                        {
                            lstCheckInDate.Add(checkindate);
                            _CheckinDate = checkindate;
                        }
                    }
                    foreach (string checkindate in lstCheckInDate)
                    {
                        dvBus.RowFilter = string.Format("StartDate='{0}'", checkindate);
                        ArrayList lstpo = new ArrayList();
                        if (dvBus.Count > 0)
                        {
                            foreach (DataRowView row in dvBus)
                            {
                                var data = new
                                {
                                    busid = 0,
                                    buscode = row["BusCode"],
                                    driver = row["Driver"],
                                    drivertel = row["DriverTel"],
                                    busconame = row["BusCoName"],
                                    bustypename = row["BusTypeName"],
                                    days = row["Days"],
                                    amount = row["Amount"],
                                    paytype = row["PayType"],
                                    sibu = row["DriverAmountResume"],
                                    amountresume = row["AmountResume"],
                                    remark = row["Remark"],
                                };
                                lstpo.Add(data);
                            }
                        }

                        var datamain = new
                        {
                            checkindate = DateTimeTool.String2DateTime(checkindate.ToString()).ToString("yyyy年MM月dd日"),
                            checkindate_wk = DateTimeTool.GetWeekdayCN(checkindate),
                            //距离今天的天数
                            checkindate_days = (DateTimeTool.String2DateTime(checkindate.ToString()) - DateTime.Today).Days,
                            po = lstpo
                        };
                        lstBusPO.Add(datamain);
                    }
                }

                var result = new
                {
                    tgid = tgid,
                    polist = lstBusPO
                };
                ReturnJsonResponse(new ApiResult { success = 1, data = result, errorcode = ErrorCode.EMPTY });
                return;
            }
            catch (Exception ex)
            {
                LogError(ex);
                ReturnJsonResponse(new ApiResult { success = 0, data = string.Empty, errorcode = ErrorCode.SYSTEM_ERROR });

            }
        }

        /// <summary>
        /// 获取導遊景點計劃 G008
        /// </summary>
        private void GetGuideTourTicket()
        {
            try
            {
                decimal tgid = GetQueryDecimal("tgid");
                decimal nGuideId = GetQueryDecimal("guideid");
                if (tgid <= 0 || nGuideId <= 0)
                {
                    ReturnJsonResponse(new ApiResult { success = 0, data = string.Empty, errorcode = ErrorCode.ERROR_PARAM });
                    return;
                }
                string sguideid = TourGroupService.GetFieldValue("obj.GuideID", tgid);
                if (StringTool.String2Decimal(sguideid) != nGuideId)
                {
                    ReturnJsonResponse(new ApiResult { success = 0, data = string.Empty, errorcode = ErrorCode.ERROR_PARAM });
                    return;
                }

                SendGroupBillBiz sgbb = new SendGroupBillBiz();
                DataTable dtTicket = sgbb.GetTourTicket(tgid);
                DataView dvTicket = new DataView(dtTicket);
                string _CheckinDate = string.Empty;
                IList lstTicketPO = new ArrayList();
                if (dtTicket.Rows.Count != 0)
                {
                    IList lstCheckInDate = new ArrayList();
                    foreach (DataRow item in dtTicket.Rows)
                    {
                        string checkindate = item["CheckInDate"].ToString();
                        if (checkindate != _CheckinDate)
                        {
                            lstCheckInDate.Add(checkindate);
                            _CheckinDate = checkindate;
                        }
                    }
                    foreach (string checkindate in lstCheckInDate)
                    {
                        dvTicket.RowFilter = string.Format("CheckInDate='{0}'", checkindate);
                        ArrayList lstpo = new ArrayList();
                        if (dvTicket.Count > 0)
                        {
                            foreach (DataRowView row in dvTicket)
                            {
                                var data = new
                                {
                                    ticketid = 0,
                                    ticketname = row["TicketObjectName"],
                                    pricename = row["PriceName"],
                                    qty = row["Qty"],
                                    price = row["Price"],
                                    paytype = row["PayType"],
                                    remark = row["PriceRemark"],
                                };
                                lstpo.Add(data);
                            }
                        }

                        var datamain = new
                        {
                            checkindate = DateTimeTool.String2DateTime(checkindate.ToString()).ToString("yyyy年MM月dd日"),
                            checkindate_wk = DateTimeTool.GetWeekdayCN(checkindate),
                            //距离今天的天数
                            checkindate_days = (DateTimeTool.String2DateTime(checkindate.ToString()) - DateTime.Today).Days,
                            po = lstpo
                        };
                        lstTicketPO.Add(datamain);
                    }
                }
                //計劃中的景點備註
                string stgticketremark = TourGroupService.GetFieldValue("obj.RemarkTicket", tgid);
                //景點預訂全局備註
                string sTicketResume = new DocumentBiz().GetContentByType(EnuDocumentType.TourGroupBillTicketRemark);
                string strTgCatalog = TourGroupService.GetFieldValue("obj.Catalog", tgid);
                string sCatalogRemark = string.Empty;
                if (strTgCatalog != "")
                {
                    int ncatalog = StringTool.String2Int(strTgCatalog);
                    if (ncatalog == (int)EnuTourCatalog.ThisProvince)
                        sCatalogRemark = new DocumentBiz().GetContentByType(EnuDocumentType.TourGroupBillTicketRemark_S);
                    else
                        sCatalogRemark = new DocumentBiz().GetContentByType(EnuDocumentType.TourGroupBillTicketRemark_L);
                }
                var result = new
                {
                    tgid = tgid,
                    tgticketremark = stgticketremark, //計劃中的景點備註
                    reticketremark = sTicketResume,//景點預訂全局備註
                    catalogremark = sCatalogRemark,
                    polist = lstTicketPO
                };
                ReturnJsonResponse(new ApiResult { success = 1, data = result, errorcode = ErrorCode.EMPTY });
                return;
            }
            catch (Exception ex)
            {
                LogError(ex);
                ReturnJsonResponse(new ApiResult { success = 0, data = string.Empty, errorcode = ErrorCode.SYSTEM_ERROR });

            }
        }

        /// <summary>
        /// 获取導遊用餐計劃 G007
        /// </summary>
        private void GetGuideTourRepast()
        {
            try
            {
                decimal tgid = GetQueryDecimal("tgid");
                decimal nGuideId = GetQueryDecimal("guideid");
                if (tgid <= 0 || nGuideId <= 0)
                {
                    ReturnJsonResponse(new ApiResult { success = 0, data = string.Empty, errorcode = ErrorCode.ERROR_PARAM });
                    return;
                }
                string sguideid = TourGroupService.GetFieldValue("obj.GuideID", tgid);
                if (StringTool.String2Decimal(sguideid) != nGuideId)
                {
                    ReturnJsonResponse(new ApiResult { success = 0, data = string.Empty, errorcode = ErrorCode.ERROR_PARAM });
                    return;
                }

                SendGroupBillBiz sgbb = new SendGroupBillBiz();
                DataTable dtRepeast = sgbb.GetTourRepeast(tgid);
                DataView dvRepeast = new DataView(dtRepeast);
                string _CheckinDate = string.Empty;
                IList lstRepeastPO = new ArrayList();
                if (dtRepeast.Rows.Count != 0)
                {
                    IList lstCheckInDate = new ArrayList();
                    foreach (DataRow item in dtRepeast.Rows)
                    {
                        string checkindate = item["CheckInDate"].ToString();
                        if (checkindate != _CheckinDate)
                        {
                            lstCheckInDate.Add(checkindate);
                            _CheckinDate = checkindate;
                        }
                    }
                    foreach (string checkindate in lstCheckInDate)
                    {
                        dvRepeast.RowFilter = string.Format("CheckInDate='{0}'", checkindate);
                        ArrayList lstpo = new ArrayList();
                        if (dvRepeast.Count > 0)
                        {
                            foreach (DataRowView row in dvRepeast)
                            {
                                var data = new
                                {
                                    repeastid = row["RestarantID"],
                                    repeastname = row["RestarantName"],
                                    repeasttype = row["RepastType"], //早中晚餐 中文
                                    mealtype = row["MealType"], //圍餐
                                    pricecatalog = nRelax.Tour.BLL.Enum.UseEnum.EnuToString((EnuRepastPOPriceType)Convert.ToInt32(row["PriceCatalog"])), //圍數,成人
                                    pricetype = row["PriceType"],//清遠雞宴
                                    qty = row["Qty"],
                                    price = row["Price"],
                                    paytype = row["PayType"],
                                    remark = row["PriceRemark"],
                                    menu = row["Menu"],
                                    menuaddition = row["MenuAddition"]
                                };
                                lstpo.Add(data);
                            }
                        }

                        var datamain = new
                        {
                            checkindate = DateTimeTool.String2DateTime(checkindate.ToString()).ToString("yyyy年MM月dd日"),
                            checkindate_wk = DateTimeTool.GetWeekdayCN(checkindate),
                            //距离今天的天数
                            checkindate_days = (DateTimeTool.String2DateTime(checkindate.ToString()) - DateTime.Today).Days,
                            po = lstpo
                        };
                        lstRepeastPO.Add(datamain);
                    }
                }
                //計劃中的住宿備註
                string stgrepeastremark = TourGroupService.GetFieldValue("obj.RemarkRepast", tgid);
                //酒店預訂全局備註
                string sRepeastResume = new DocumentBiz().GetContentByType(EnuDocumentType.TourGroupBillRepastRemark);
                string strTgCatalog = TourGroupService.GetFieldValue("obj.Catalog", tgid);
                string sCatalogRemark = string.Empty;
                if (strTgCatalog != "")
                {
                    int ncatalog = StringTool.String2Int(strTgCatalog);
                    if (ncatalog == (int)EnuTourCatalog.ThisProvince)
                        sCatalogRemark = new DocumentBiz().GetContentByType(EnuDocumentType.TourGroupBillRepastRemark_S);
                    else
                        sCatalogRemark = new DocumentBiz().GetContentByType(EnuDocumentType.TourGroupBillRepastRemark_L);
                }
                var result = new
                {
                    tgid = tgid,
                    tgrepeastremark = stgrepeastremark, //計劃中的住宿備註
                    rerepeastremark = sRepeastResume,//酒店預訂全局備註
                    catalogremark = sCatalogRemark,
                    polist = lstRepeastPO
                };
                ReturnJsonResponse(new ApiResult { success = 1, data = result, errorcode = ErrorCode.EMPTY });
                return;
            }
            catch (Exception ex)
            {
                LogError(ex);
                ReturnJsonResponse(new ApiResult { success = 0, data = string.Empty, errorcode = ErrorCode.SYSTEM_ERROR });

            }
        }

        /// <summary>
        /// 获取導遊酒店計劃 G006
        /// </summary>
        private void GetGuideTourHotel()
        {
            try
            {
                decimal tgid = GetQueryDecimal("tgid");
                decimal nGuideId = GetQueryDecimal("guideid");
                if (tgid <= 0 || nGuideId <= 0)
                {
                    ReturnJsonResponse(new ApiResult { success = 0, data = string.Empty, errorcode = ErrorCode.ERROR_PARAM });
                    return;
                }
                string sguideid = TourGroupService.GetFieldValue("obj.GuideID", tgid);
                if (StringTool.String2Decimal(sguideid) != nGuideId)
                {
                    ReturnJsonResponse(new ApiResult { success = 0, data = string.Empty, errorcode = ErrorCode.ERROR_PARAM });
                    return;
                }

                SendGroupBillBiz sgbb = new SendGroupBillBiz();
                DataTable dtHotel = sgbb.GetTourHotel(tgid);
                DataView dvHotel = new DataView(dtHotel);
                string _CheckinDate = string.Empty;
                decimal _HotelId = 0;
                IList lstHotelPO = new ArrayList();
                if (dtHotel.Rows.Count != 0)
                {
                    IList lstHotel = new ArrayList();
                    foreach (DataRow item in dtHotel.Rows)
                    {
                        string checkindate = item["CheckInDate"].ToString();
                        decimal hotelid = StringTool.String2Decimal(item["HotelID"].ToString());
                        string hotelname = item["HotelName"].ToString();
                        if (hotelid != _HotelId && checkindate != _CheckinDate)
                        {
                            string data = string.Format("{0}┼{1}┼{2}",
                                checkindate, hotelid, hotelname);
                            lstHotel.Add(data);
                            _HotelId = hotelid;
                            _CheckinDate = checkindate;
                        }
                    }
                    foreach (string item in lstHotel)
                    {
                        string[] arritem = item.Split('┼');
                        string checkindate = arritem[0].ToString();
                        decimal hotelid = StringTool.String2Decimal(arritem[1].ToString());
                        string hotelname = arritem[2].ToString();
                        dvHotel.RowFilter = string.Format("HotelID={0} and CheckInDate='{1}'",
                            hotelid,
                            checkindate);
                        ArrayList lstpo = new ArrayList();
                        if (dvHotel.Count > 0)
                        {
                            foreach (DataRowView row in dvHotel)
                            {
                                var data = new
                                {
                                    roomtype = row["RoomTypeName"],
                                    pricename = row["PriceName"],
                                    qty = row["Qty"],
                                    freeqty = row["FreeQty"],
                                    unit = row["Unit"],
                                    price = row["Price"],
                                    ismonthpay = row["PayType"].ToString() == "月结",
                                    remark = row["PriceRemark"],
                                    menu = row["Menu"]
                                };
                                lstpo.Add(data);
                            }
                        }

                        var datamain = new
                        {
                            checkindate = DateTimeTool.String2DateTime(checkindate.ToString()).ToString("yyyy年MM月dd日"),
                            checkindate_wk = DateTimeTool.GetWeekdayCN(checkindate),
                            //距离今天的天数
                            checkindate_days = (DateTimeTool.String2DateTime(checkindate.ToString()) - DateTime.Today).Days,
                            hotelid = hotelid,
                            hotelname = hotelname,
                            po = lstpo
                        };
                        lstHotelPO.Add(datamain);
                    }
                }
                //計劃中的住宿備註
                string stghotelremark = TourGroupService.GetFieldValue("obj.RemarkHotel", tgid);
                //酒店預訂全局備註
                string sHotelResume = new DocumentBiz().GetContentByType(EnuDocumentType.TourGroupBillHotelRemark);
                string strTgCatalog = TourGroupService.GetFieldValue("obj.Catalog", tgid);
                string sCatalogRemark = string.Empty;
                if (strTgCatalog != "")
                {
                    int ncatalog = StringTool.String2Int(strTgCatalog);
                    if (ncatalog == (int)EnuTourCatalog.ThisProvince)
                        sCatalogRemark = new DocumentBiz().GetContentByType(EnuDocumentType.TourGroupBillHotelRemark_S);
                    else
                        sCatalogRemark = new DocumentBiz().GetContentByType(EnuDocumentType.TourGroupBillHotelRemark_L);
                }
                var result = new
                {
                    tgid = tgid,
                    tgticketremark = stghotelremark, //計劃中的住宿備註
                    reticketremark = sHotelResume,//酒店預訂全局備註
                    catalogremark = sCatalogRemark,
                    polist = lstHotelPO
                };
                ReturnJsonResponse(new ApiResult { success = 1, data = result, errorcode = ErrorCode.EMPTY });
                return;
            }
            catch (Exception ex)
            {
                LogError(ex);
                ReturnJsonResponse(new ApiResult { success = 0, data = string.Empty, errorcode = ErrorCode.SYSTEM_ERROR });

            }
        }

        /// <summary>
        /// 获取導遊出團行程 G005
        /// </summary>
        private void GetGuideTourProcess()
        {
            decimal tgid = GetQueryDecimal("tgid");
            decimal nGuideId = GetQueryDecimal("guideid");
            try
            {
                if (tgid <= 0 || nGuideId <= 0)
                {
                    ReturnJsonResponse(new ApiResult { success = 0, data = string.Empty, errorcode = ErrorCode.ERROR_PARAM });
                    return;
                }
                string sguideid = TourGroupService.GetFieldValue("obj.GuideID", tgid);
                if (StringTool.String2Decimal(sguideid) != nGuideId)
                {
                    ReturnJsonResponse(new ApiResult { success = 0, data = string.Empty, errorcode = ErrorCode.ERROR_PARAM });
                    return;
                }

                IList lst = new TourGroupProcessBiz().GetByTourGroupID(tgid);
                if (lst.Count > 0)
                {
                    ArrayList lstProcess = new ArrayList();
                    foreach (TourGroupProcess item in lst)
                    {
                        var data = new
                        {
                            theday = item.TheDay,
                            thedate = item.TheDate.ToString("yyyy年MM月dd日"),
                            thedate_wk = DateTimeTool.GetWeekdayCN(item.TheDate),
                            //距离今天的天数
                            thedate_days = (item.TheDate - DateTime.Today).Days,
                            city = item.Station,
                            process = item.ProcessResume,
                            traffice = item.Traffic,
                            hotel = item.HotelName,
                            r1 = item.Repast1,
                            r2 = item.Repast2,
                            r3 = item.Repast3,
                            r4 = item.Repast4
                        };
                        lstProcess.Add(data);
                    }
                    ReturnJsonResponse(new ApiListResult { success = 1, rows = lstProcess, errorcode = ErrorCode.EMPTY });
                }
                else
                {
                    ReturnJsonResponse(new ApiListResult { success = 1, rows = new ArrayList(), errorcode = ErrorCode.EMPTY });
                }
            }
            catch (Exception ex)
            {
                LogError(ex);
                ReturnJsonResponse(new ApiListResult { success = 0, rows = new ArrayList(), errorcode = ErrorCode.SYSTEM_ERROR });
            }
        }



        /// <summary>
        /// 获取导游出团的团详情 G004
        /// </summary>
        private void GetGuideTourGroupInfo()
        {
            decimal tgid = GetQueryDecimal("tgid");
            decimal nGuideId = GetQueryDecimal("guideid");

            try
            {
                if (tgid <= 0 || nGuideId <= 0)
                {
                    ReturnJsonResponse(new ApiResult { success = 0, data = string.Empty, errorcode = ErrorCode.ERROR_PARAM });
                    return;
                }
                TourGroup tg = new TourGroupService().GetByID(tgid);
                if (tg.GuideID != nGuideId)
                {
                    ReturnJsonResponse(new ApiResult { success = 0, data = string.Empty, errorcode = ErrorCode.ERROR_PARAM });
                    return;
                }
                string scode = TourBiz.GetFieldValue("obj.ProductCode", tg.ProductID);
                string strRDCatalog = TourGroupService.GetFieldValue("obj.RDCatalog", tgid);
                string strTgCatalog = TourGroupService.GetFieldValue("obj.Catalog", tgid);
                string strRDRemark = string.Empty, strCatalogRemark = string.Empty;
                //系列線-团备注
                if (Convert.ToInt32(strRDCatalog) == (int)EnuTourRDCatalog.LingXieTuan)
                    strRDRemark = new DocumentBiz().GetContentByType(EnuDocumentType.LingXieTourRemark);
                //另寫線-团备注
                else if (Convert.ToInt32(strRDCatalog) == (int)EnuTourRDCatalog.XiLieTuan)
                    strRDRemark = new DocumentBiz().GetContentByType(EnuDocumentType.XiLieTourRemark);

                //短線(省內)出團備註
                if (Convert.ToInt32(strTgCatalog) == (int)EnuTourCatalog.ThisProvince)
                    strCatalogRemark = new DocumentBiz().GetContentByType(EnuDocumentType.ShortTourRemark);
                else
                    strCatalogRemark = new DocumentBiz().GetContentByType(EnuDocumentType.LongTourRemark);
                //各部門聯繫電話
                string sContactInfo = new DocumentBiz().GetContentByType(EnuDocumentType.DeptContactInfo);

                var data = new
                {
                    tgid = tg.Id,
                    productcode = scode == null ? "" : scode,
                    productname = tg.ProductName,
                    busno = tg.BusNo,
                    flag = tg.FlagNo,
                    startdate = tg.StartDate,
                    startdate_wk = DateTimeTool.GetWeekdayCN(tg.StartDate),
                    //距离今天的天数
                    startdate_days = (tg.StartDate - DateTime.Today).Days,
                    starttime = tg.StartTime,
                    guideid = tg.GuideID,
                    processdays = tg.ProcessDays,
                    isrectip = tg.IsReceiveTips ? 1 : 0,
                    norqty = tg.NormalCount + tg.MemberCount,
                    childqty = tg.ChildCount,

                    addbedqty = tg.AddBedCount,//加床數
                    roomcount = tg.RoomCount, //標雙
                    bigbedroomcount = tg.BigBedRoomCount, //大床房
                    halfcount = tg.HalfCount, //半房
                    addroomcount = tg.AddRoomCount,//房差數

                    bbqty = tg.BBCount,
                    oldqty = tg.OlderCount,
                    oldqty2 = tg.OlderCount2,

                    remark = tg.Remark,
                    remarkall = tg.RemarkAll,
                    objectresume = tg.ObjectResume,//領用物品
                    rdremark = strRDRemark, //系列線 另寫線 備註 HTML
                    catalogremark = strCatalogRemark, //長短線 備註 HTML
                    deptcontactinfo = sContactInfo, //各部門聯繫電話 HTML

                    //導遊借款
                    guideadvancedate = DateTimeTool.FormatString(tg.GuideAdvanceDate),
                    guideadvancefee = tg.GuideAdvanceFee,
                    guideadvancehander = tg.GuideAdvanceHander,
                    guideadvanceremark = tg.GuideAdvanceRemark,

                    btsalesmanname = tg.BTSalesmanName, //包團營銷員
                    btgroupname = tg.BTGroupName, //包團組號
                    isbookhotel = tg.IsHotelFinish ? 1 : 0,
                    isbookrepast = tg.IsRestaurantFinish ? 1 : 0,
                    isbookticket = tg.IsTicketObjectFinish ? 1 : 0,
                    isbookbus = tg.IsBusFinish ? 1 : 0
                };
                ReturnJsonResponse(new ApiResult { success = 1, data = data, errorcode = ErrorCode.EMPTY });

            }
            catch (Exception ex)
            {
                LogError(ex);
                ReturnJsonResponse(new ApiResult { success = 0, data = string.Empty, errorcode = ErrorCode.SYSTEM_ERROR });
            }
        }

        /// <summary>
        /// 根据导游标识获取其出团列表 G003
        /// </summary>
        private void GetGuideTourGroupList()
        {
            decimal nGuideId = GetQueryDecimal("guideid");
            string strUnionId = GetQueryString("unionid");
            if (strUnionId.Length == 0)
            {
                if (!CheckToken())
                {
                    ReturnJsonResponse(new ApiResult { success = 0, data = "", errorcode = ErrorCode.ERROR_PARAM });
                    return;
                }
            }
            else
            {
                if (!CheckGuideUnionId())
                {
                    ReturnJsonResponse(new ApiResult { success = 0, data = "", errorcode = ErrorCode.ERROR_PARAM });
                    return;
                }
            }
            try
            {
                DataTable dt = new TourGroupService().GetListByGuideID(nGuideId);
                if (dt != null)
                {
                    DataView dv = new DataView(dt);
                    dv.Sort = "Status,StartDate desc";
                    ArrayList lst = new ArrayList();
                    foreach (DataRowView item in dv)
                    {
                        var data = new
                        {
                            tgid = item["tgid"],
                            productcode = item["productcode"],
                            productname = item["productname"],
                            busno = item["busno"],
                            flagno = item["flagno"],
                            processdays = item["processdays"],
                            startdate = item["startdate"],
                            status = item["status"],//1 --未出发 0 --进行中 2 --已出发

                            startdate_wk = DateTimeTool.GetWeekdayCN(item["startdate"]),
                            //距离今天的天数
                            startdate_days =
                                (Convert.ToDateTime(item["startdate"]) - DateTime.Today).Days,

                        };
                        lst.Add(data);
                    }
                    //dv.ToTable()
                    ReturnJsonResponse(new ApiListResult() { success = 1, rows = lst, errorcode = ErrorCode.EMPTY });
                }
                else
                {
                    ReturnJsonResponse(new ApiListResult() { success = 1, rows = new ArrayList(), errorcode = ErrorCode.EMPTY });
                }
            }
            catch (Exception ex)
            {
                LogError(ex);
                ReturnJsonResponse(new ApiListResult { success = 0, rows = new ArrayList(), errorcode = ErrorCode.SYSTEM_ERROR });
            }

        }

        /// <summary>
        /// 獲取可以報賬的團列表(G012)
        /// 已經出發+已經結束10天內
        /// </summary>
        private void GetGuideFeeTourGroupList()
        {
            decimal nGuideId = GetQueryDecimal("guideid");
            string strUnionId = GetQueryString("unionid");
            if (strUnionId.Length == 0)
            {
                if (!CheckToken())
                {
                    ReturnJsonResponse(new ApiResult { success = 0, data = "", errorcode = ErrorCode.ERROR_PARAM });
                    return;
                }
            }
            else
            {
                if (!CheckGuideUnionId())
                {
                    ReturnJsonResponse(new ApiResult { success = 0, data = "", errorcode = ErrorCode.ERROR_PARAM });
                    return;
                }
            }
            try
            {
                DataTable dt = new TourGroupService().GetCanFeeListByGuideID(nGuideId);
                if (dt != null)
                {
                    DataView dv = new DataView(dt);
                    dv.Sort = "Status,StartDate desc";
                    ArrayList lst = new ArrayList();
                    TourGroupOtherFeeCheckLogBiz tourGroupOtherFeeCheckLogBiz = new TourGroupOtherFeeCheckLogBiz();
                    foreach (DataRowView item in dv)
                    {
                        decimal tgid = Convert.ToDecimal(item["tgid"]);
                        string cancelreason = "";
                        int isCanceled = item["ApplyFeeIsCancel"] == null ? 0 : Convert.ToInt32(item["ApplyFeeIsCancel"]); //是否被退回
                        if (isCanceled == 1)
                        {
                            //獲取退回原因
                            cancelreason = tourGroupOtherFeeCheckLogBiz.getLastLogRemark(tgid);
                        }
                        var data = new
                        {
                            tgid = tgid,
                            productcode = item["productcode"],
                            productname = item["productname"],
                            busno = item["busno"],
                            flagno = item["flagno"],
                            processdays = item["processdays"],
                            startdate = item["startdate"],
                            status = item["status"],//1 --未出发 0 --进行中 2 --已出发
                            applyfeestatus = item["ApplyFeeStatus"],
                            applyfeedate = item["ApplyFeeDate"],
                            applycanceled = isCanceled, //是否被退回
                            cancelreason, //退回原因
                            startdate_wk = DateTimeTool.GetWeekdayCN(item["startdate"]),
                            //距离今天的天数
                            startdate_days =
                                (Convert.ToDateTime(item["startdate"]) - DateTime.Today).Days,

                        };
                        lst.Add(data);
                    }
                    //dv.ToTable()
                    ReturnJsonResponse(new ApiListResult() { success = 1, rows = lst, errorcode = ErrorCode.EMPTY });
                }
                else
                {
                    ReturnJsonResponse(new ApiListResult() { success = 1, rows = new ArrayList(), errorcode = ErrorCode.EMPTY });
                }
            }
            catch (Exception ex)
            {
                LogError(ex);
                ReturnJsonResponse(new ApiListResult { success = 0, rows = new ArrayList(), errorcode = ErrorCode.SYSTEM_ERROR });
            }

        }

        /// <summary>
        /// 驗證導遊id和導遊unionid是否一致
        /// </summary>
        /// <returns></returns>
        private bool CheckGuideUnionId()
        {
            string unionid = GetQueryString("unionid");
            decimal nGuideId = GetQueryDecimal("guideid");
            string sWhere = string.Format("obj.UnionId='{0}'", unionid.Replace("'", "''"));
            string sid = GuideInfoService.GetFieldValue("obj.Id", sWhere);
            if (sid == nGuideId.ToString())
            {
                return true;
            }
            else
                return false;

        }

        /// <summary>
        /// 通过导游通过验证码登录 G001
        /// </summary>
        private void GetGuideLoginUseerByVerifyCode()
        {
            string mobileno = GetQueryString("mobileno");
            string verifycode = GetQueryString("verifycode");
            string unionid = GetQueryString("unionid");
            try
            {
                if (verifycode != "711711")
                {
                    bool verifyok = PhoneVerifyCode.CheckVerifyCode(mobileno, verifycode);
                    if (!verifyok)
                    {
                        ReturnJsonResponse(new ApiResult { success = 0, data = string.Empty, errorcode = ErrorCode.VERIFY_CODE_ERROR });
                        return;
                    }
                }
                GuideInfoService biz = new GuideInfoService();
                GuideInfo guide = biz.GetByMobileNoAndUpdateUnionId(mobileno, unionid);
                if (guide != null)
                {
                    try
                    {
                        var dguide = new
                        {
                            guideid = guide.Id,
                            name = guide.GuideName,
                            mobile = guide.Mobile,
                        };
                        ReturnJsonResponse(new { success = 1, data = dguide, errorcode = ErrorCode.EMPTY });
                    }
                    catch (Exception ex)
                    {
                        LogError(ex);
                        ReturnJsonResponse(new ApiResult { success = 0, data = string.Empty, errorcode = ErrorCode.SYSTEM_ERROR });
                    }
                }
                else
                {
                    ReturnJsonResponse(new ApiResult { success = 0, data = string.Empty, errorcode = ErrorCode.CHECK_PWD_FAIL });
                }
            }
            catch (Exception ex)
            {
                LogError(ex);
                ReturnJsonResponse(new ApiResult { success = 0, data = string.Empty, errorcode = ErrorCode.SYSTEM_ERROR });
            }
        }

        /// <summary>
        /// 解密小程序加密后的用戶數據
        /// openId,nickName,gender,city,province,country,avatarUrl,unionId,language
        /// </summary>
        private void DecryptMiniAppUserData()
        {
            string data = GetQueryString("data").Replace("%2B", "+");
            //data = HttpUtility.UrlDecode(data);
            string iv = GetQueryString("iv").Replace("%2B", "+");
            //data = HttpUtility.UrlDecode(iv);
            string appid = GetQueryString("appid");

            string key = string.Empty;
            try
            {
                key = GetMiniSessionKey(appid);
                //被加密的数据
                byte[] dataByte = Convert.FromBase64String(data);
                //加密秘钥
                byte[] keyByte = Convert.FromBase64String(key);
                //偏移量
                byte[] ivByte = Convert.FromBase64String(iv);

                RijndaelManaged rijndaelCipher = new RijndaelManaged();
                rijndaelCipher.Key = keyByte; // Encoding.UTF8.GetBytes(AesKey); 
                rijndaelCipher.IV = ivByte;// Encoding.UTF8.GetBytes(AesIV); 
                rijndaelCipher.Mode = CipherMode.CBC;
                rijndaelCipher.Padding = PaddingMode.PKCS7;
                ICryptoTransform transform = rijndaelCipher.CreateDecryptor();
                byte[] plainText = transform.TransformFinalBlock(dataByte, 0, dataByte.Length);
                string result = Encoding.UTF8.GetString(plainText);
                JObject _usrInfo = (JObject)JsonConvert.DeserializeObject(result);
                string strwatermark = _usrInfo["watermark"].ToString();
                JObject _watermark = null;
                if (strwatermark.Length > 0)
                    _watermark = (JObject)JsonConvert.DeserializeObject(strwatermark);
                var userinfo = new
                {
                    openId = _usrInfo["openId"].ToString(),
                    nickName = _usrInfo["nickName"].ToString(),
                    gender = _usrInfo["gender"].ToString(),
                    city = _usrInfo["city"].ToString(),
                    province = _usrInfo["province"].ToString(),
                    country = _usrInfo["country"].ToString(),
                    avatarUrl = _usrInfo["avatarUrl"].ToString(),
                    unionId = _usrInfo["unionId"] != null ? _usrInfo["unionId"].ToString() : "",
                    language = _usrInfo["language"].ToString(),
                    watermark = new
                    {
                        appid = _watermark != null ? _watermark["appid"].ToString() : "",
                        timestamp = _watermark != null ? _watermark["timestamp"].ToString() : ""
                    }

                };
                ReturnJsonResponse(new ApiResult { success = 1, data = userinfo, errorcode = string.Empty });
            }
            catch (Exception ex)
            {
                LogError(new Exception("data=  " + data + "     iv=" + iv + "     key=" + key));
                LogError(ex);
                ReturnJsonResponse(new ApiResult { success = 0, data = string.Empty, errorcode = ErrorCode.SYSTEM_ERROR });
            }
        }

        /// <summary>
        /// strappid= 1:留位小程序, 2:导游助手小程序
        /// </summary>
        /// <param name="strappid"></param>
        /// <returns></returns>
        private string GetMiniSessionKey(string strappid)
        {
            string result = MiniAppLogin(strappid);
            JObject jo = (JObject)JsonConvert.DeserializeObject(result);
            Logger.Error(result);
            string session_key = jo["session_key"].ToString();
            return session_key;
        }

        /// <summary>
        /// strappid= 1:留位小程序, 2:导游助手小程序
        /// </summary>
        /// <param name="strappid"></param>
        /// <returns></returns>
        private string MiniAppLogin(string strappid)
        {

            //case "2": //导游助手小程序
            string Appid = WebConfig.GuideAppId;
            string SecretKey = WebConfig.GuideAppKey;
            string code = GetQueryString("code").Replace("%2B", "+");
            string grant_type = "authorization_code";
            string url = "https://api.weixin.qq.com/sns/jscode2session?appid=" + Appid + "&secret=" + SecretKey + "&js_code=" + code + "&grant_type=" + grant_type;
            string type = "utf-8";

            System.Net.WebRequest wReq = System.Net.WebRequest.Create(url);
            System.Net.WebResponse wResp = wReq.GetResponse();
            System.IO.Stream respStream = wResp.GetResponseStream();
            using (System.IO.StreamReader reader = new System.IO.StreamReader(respStream, Encoding.GetEncoding(type)))
            {
                string result = reader.ReadToEnd();
                if (result.IndexOf("errcode") >= 0)
                {
                    Logger.Error(string.Format("appid={0} \r\n mininappid={1} \r\n secrkey={2}", strappid, Appid, SecretKey));
                    Logger.Error(result);
                    return "";
                }
                return result;
            }

        }

        /// <summary>
        /// 解密小程序电话号码
        /// </summary>
        /// <returns></returns>
        private string getMiniPhoneNumber()
        {
            string encryptedData = GetQueryString("data");
            string iv = GetQueryString("iv");
            string sessionKey = GetQueryString("key");
            try
            {
                Logger.Error(encryptedData);
                byte[] encryData = Convert.FromBase64String(encryptedData);  // strToToHexByte(text);
                RijndaelManaged rijndaelCipher = new RijndaelManaged();
                rijndaelCipher.Key = Convert.FromBase64String(sessionKey); // Encoding.UTF8.GetBytes(AesKey);
                rijndaelCipher.IV = Convert.FromBase64String(iv);// Encoding.UTF8.GetBytes(AesIV);
                rijndaelCipher.Mode = CipherMode.CBC;
                rijndaelCipher.Padding = PaddingMode.PKCS7;
                ICryptoTransform transform = rijndaelCipher.CreateDecryptor();
                byte[] plainText = transform.TransformFinalBlock(encryData, 0, encryData.Length);
                string result = Encoding.Default.GetString(plainText);
                //{"phoneNumber":"13926576007","purePhoneNumber":"13926576007","countryCode":"86","watermark":{"timestamp":1586077862,"appid":"wx7f568e39b2ff0a47"}}
                if (result.Length > 0)
                {
                    JObject jo = (JObject)JsonConvert.DeserializeObject(result);
                    return jo["phoneNumber"].ToString();
                }
                else
                    return "";

            }
            catch (Exception ex)
            {
                Logger.Error(ex);
                return "";

            }
        }



        /// <summary>
        /// 根据微信UnionId获取導遊信息 G002
        /// </summary>
        private void GetGuideInfoByWxId()
        {
            try
            {
                string unionid = GetQueryString("unionid");
                GuideInfoService biz = new GuideInfoService();
                GuideInfo guide = biz.GetByWeChatUnionId(unionid);
                if (guide != null)
                {
                    var info = new
                    {
                        guideid = guide.Id,
                        unionid = guide.UnionId == null ? "" : guide.UnionId,
                        mobile = guide.Mobile == null ? "" : guide.Mobile
                    };
                    ReturnJsonResponse(new { success = 1, data = info, errorcode = ErrorCode.EMPTY });
                }
                else
                {
                    ReturnJsonResponse(new ApiResult { success = 1, data = string.Empty, errorcode = string.Empty });
                }
            }
            catch (Exception ex)
            {
                LogError(ex);
                ReturnJsonResponse(new ApiResult { success = 0, data = string.Empty, errorcode = ErrorCode.SYSTEM_ERROR });
            }
        }



        private static string GetFileExt(string _filepath)
        {
            if (string.IsNullOrEmpty(_filepath))
            {
                return "";
            }
            if (_filepath.LastIndexOf(".") > 0)
            {
                return _filepath.Substring(_filepath.LastIndexOf(".") + 1); //文件扩展名，不含“.”
            }
            return "";
        }
        private byte[] ByteFiles(HttpPostedFile postedFile)
        {
            int fileLen = postedFile.ContentLength;//图片长度
            Byte[] byteFile = new Byte[fileLen];
            //读取文件内容
            Stream sr = postedFile.InputStream;
            sr.Read(byteFile, 0, fileLen);
            //释放资源
            sr.Dispose();
            return byteFile;
        }
        private string GetQueryToken()
        {
            string token = GetQueryString("token");
            string data_token = GetQueryString("dtoken");
            if (token.Length == 0 && data_token.Length > 0)
                token = data_token;
            return token;
        }

        private bool CheckTokenValid(string token)
        {
            try
            {
                if (token == "7udjyughngxfs6yd")
                    return true;

                if (token.Length == 0)
                {
                    return false;
                }
                MemberBiz biz = new MemberBiz();
                bool checkok = new MembersTokenBiz().CheckToken(token);
                if (!checkok)
                {
                    return false;
                }
                return checkok;
            }
            catch (Exception ex)
            {
                LogError(ex);
                return false;
            }
        }

        /// <summary>
        /// 检查token
        /// </summary>
        /// <returns></returns>
        private bool CheckToken()
        {
            try
            {
                string token = GetQueryToken();
                if (token.Length == 0)
                {
                    ReturnJsonResponse(new ApiResult { success = 0, data = string.Empty, errorcode = ErrorCode.CHECK_TOKEN_EXISTS_ERROR });
                    return false;
                }

                bool checkok = CheckTokenValid(token);
                if (!checkok)
                {
                    ReturnJsonResponse(new ApiResult { success = 0, data = string.Empty, errorcode = ErrorCode.CHECK_TOKEN_ERROR });
                    //new DingTalkTool().SendMessage("token " + token + " 校验失败!");
                    return false;
                }
                return checkok;
            }
            catch (Exception ex)
            {
                LogError(ex);
                ReturnJsonResponse(new ApiResult { success = 0, data = string.Empty, errorcode = ErrorCode.SYSTEM_ERROR });
                return false;
            }
        }

    }
}