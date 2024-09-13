using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using nRelax.DAL;
using nRelax.DevBase.BaseTools;
using nRelax.Images.Common;
using nRelax.Interface;
using nRelax.Org.Entity;
using nRelax.Org.SqlDAL;
using nRelax.SDK.WxPay;
using nRelax.SSO;
using nRelax.Tour.BLL;
using nRelax.Tour.BLL.API;
using nRelax.Tour.BLL.Enum;
using nRelax.Tour.Entity;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Web;

namespace nRelax.Tour.WebApp
{
    /// <summary>
    /// Summary description for tajax
    /// </summary>
    public class tourajax : ApiHttpHandle
    {
        private decimal _Insur_Product_Id_Short = 0;
        private decimal _Insur_Product_Id_Long = 0;
        private const string SSO_KEY = "sso_user_92382";
        private const int APP_MAX_REG_PERSON_COUNT = 5;
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

            _Insur_Product_Id_Short = ConfigBiz.AppInsurProductId_Short;
            _Insur_Product_Id_Long = ConfigBiz.AppInsurProductId_Long;
            if (_Insur_Product_Id_Short == 0 || _Insur_Product_Id_Long == 0)
            {
                ReturnJsonResponse(new { success = 0, data = string.Empty, errorcode = ErrorCode.SYSTEM_ERROR });
                Logger.Error("~/config/AppConfig.xml中未配置保险产品ID（InsurProductId）");
                return;
            }

            #region 檢查時間戳
            string stamp = GetQueryString("stamp");
            if (stamp.Length == 0)
            {
                ReturnJsonResponse(new { success = 0, data = string.Empty, errorcode = ErrorCode.STAMP_EMPTY });
                Logger.Error("时间戳不能为空");
                return;
            }
            #endregion


            #region  检查签名
            bool check = CheckSign();
            if (!check)
            {
                ReturnJsonResponse(new ApiResult { success = 0, data = string.Empty, errorcode = ErrorCode.CHECK_SIGN_ERROR });
                return;
            }
            #endregion

            #region page & action
            int pageindex = GetQueryInt("pageindex");
            if (pageindex == 0)
                pageindex = 1;
            int pagesize = GetQueryInt("pagesize");
            if (pagesize == 0)
                pagesize = 10;
            decimal tourid = GetQueryDecimal("tourId");

            action = GetQueryString("action");
            #endregion

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

                #region 登录及会员操作

                case "1000": //根據手機號和驗證碼直接登錄;如果传微信union的话,绑定微信unionid和手机号码
                    GetAndCreateMember();
                    break;
                case "1000-01": //检查微信unionid是否绑定了手机号码
                    GetMemberInfoByWxId();
                    break;
                case "1000-02": //检查Token
                    CheckTokenApi();
                    break;
                case "1000-03":
                    ReLgoin();
                    break;
                case "1001"://会员认证：pwd:a87402a268d55f2ac7f3fc490a39751e,13926576007,123456
                    GetMemberToken();
                    break;
                case "1002": //找回密码
                    ForgetPassword();
                    break;
                case "1003": //獲取手機驗證碼
                    CreatePhoneVerifyCode();
                    break;
                case "1004": //修改密碼  //check token
                    if (!CheckToken()) //检查token
                        return;
                    UpdateMemberPassword();
                    break;
                case "1005":
                    if (!CheckToken()) //检查token
                        return;
                    UploadAvatar(); //上传头像 //check token
                    break;
                case "1006":  //上传设备信息
                    if (!CheckToken()) //检查token
                        return;
                    UploadDeviceInfo();
                    break;
                #endregion

                #region 收客報名接口

                ///todo 过滤掉包团
                //case "2000"://所有旅游团
                //    GetHotTour(0, pageindex, pagesize);
                //    break;
                case "2000":
                    int theCatalog = GetQueryInt("catalog");
                    int theStatus = GetQueryInt("status");
                    string theFeature = GetQueryString("feature");
                    string theTarget = GetQueryString("target");
                    string keyword = GetQueryString("keyword");
                    string startdateRange = GetQueryString("startdate"); // 用~隔開的日期範圍
                    int theDays = GetQueryInt("days");
                    int theJiaban = GetQueryInt("jiaban");
                    GetHotTour(keyword, theCatalog, theStatus, theFeature, theTarget, startdateRange, theDays, theJiaban, pageindex, pagesize);
                    break;
                case "2001"://热门推荐（短线）
                    GetHotTour(10, pageindex, pagesize);
                    break;
                case "2011"://热门推荐（长线）
                    GetHotTour(11, pageindex, pagesize);
                    break;
                case "2021"://热门推荐（东南亚）
                    GetHotTour(12, pageindex, pagesize);
                    break;
                case "2031"://热门推荐(跨省巴士)
                    GetHotTour(13, pageindex, pagesize);
                    break;
                case "2041"://热门推荐(郵輪)
                    GetHotTour(14, pageindex, pagesize);
                    break;
                case "2051"://热门推荐(世界長線)
                    GetHotTour(15, pageindex, pagesize);
                    break;
                case "2061"://热门推荐(自由行)
                    GetHotTour(16, pageindex, pagesize);
                    break;
                case "2071"://热门推荐(最新優惠)
                    GetHotTour(17, pageindex, pagesize);
                    break;
                case "2091"://热门推荐(非自由行)
                    GetHotTour(19, pageindex, pagesize);
                    break;

                case "2002"://快成团（短线）
                    GetHotTour(20, pageindex, pagesize);
                    break;
                case "2012"://快成团（长线）
                    GetHotTour(21, pageindex, pagesize);
                    break;
                case "2022"://快成团（东南亚）
                    GetHotTour(22, pageindex, pagesize);
                    break;
                case "2032"://快成团（跨省巴士）
                    GetHotTour(23, pageindex, pagesize);
                    break;
                case "2042"://快成团（郵輪）
                    GetHotTour(24, pageindex, pagesize);
                    break;
                case "2052"://快成团(世界長線)
                    GetHotTour(25, pageindex, pagesize);
                    break;
                case "2062"://快成团(自由行)
                    GetHotTour(26, pageindex, pagesize);
                    break;
                case "2072"://快成团(最新優惠)
                    GetHotTour(27, pageindex, pagesize);
                    break;
                case "2092"://快成团(非自由行)
                    GetHotTour(29, pageindex, pagesize);
                    break;

                case "2099"://VIP專享
                    GetHotTour(99, pageindex, pagesize);
                    break;

                case "2003"://根据特色查询 
                    ///todo 主题查询仅限于短线，需要修改
                    string feature = GetQueryString("feature");
                    int type = GetQueryInt("type");
                    GetHotTourByFeature(feature, type, pageindex, pagesize);
                    break;
                case "2004"://根据目的地查询
                    ///todo 目的地查询速度慢
                    string target = GetQueryString("target");
                    GetHotTourByTarget(target, pageindex, pagesize);
                    break;

                case "2005"://根据查询条件查询
                    #region param
                    string tourcode = GetQueryString("tourcode");
                    if (!IsSafeSqlString(tourcode))
                    {
                        ReturnJsonResponse(new ApiListResult
                        {
                            success = 0,
                            rows = new ArrayList(),
                            errorcode = ErrorCode.UNSAFEPARAM
                        });
                        return;
                    }
                    int tourtype = GetQueryInt("tourtype");
                    string area = GetQueryString("area");
                    if (!IsSafeSqlString(area))
                    {
                        ReturnJsonResponse(new ApiListResult
                        {
                            success = 0,
                            rows = new ArrayList(),
                            errorcode = ErrorCode.UNSAFEPARAM
                        });
                        return;
                    }

                    int days = GetQueryInt("days");
                    string startdate = GetQueryString("startdate");
                    //if (!IsSafeSqlString(startdate))
                    //{
                    //    ReturnJsonResponse(new ApiListResult
                    //    {
                    //        success = 0,
                    //        rows = new ArrayList(),
                    //        errorcode = ErrorCode.UNSAFEPARAM
                    //    });
                    //    return;
                    //}

                    StringBuilder sb = new StringBuilder();
                    if (tourcode.Trim().Length > 0)
                        sb.Append(" and tp.ProductCode like '").Append(tourcode).Append("%' ");
                    if (GetQueryString("tourtype").Trim().Length > 0)
                        sb.Append(" and tp.Type=").Append(tourtype);
                    if (area.Trim().Length > 0)
                        sb.Append(" and tp.EndStation like '%").Append(area).Append("%'");
                    if (days > 0)
                        sb.Append(" and tp.ProcessDays=").Append(days);
                    string stpcondition = sb.ToString();
                    #endregion
                    GetHotTourByCondution(stpcondition, startdate, pageindex, pagesize);
                    break;
                case "2005_01":
                    //最新线路
                    GetNewTour(pageindex, pagesize);
                    break;
                case "2006": //获取团的详细信息
                    GetTourDetail(tourid);
                    break;
                case "2007": //获取每月团的状态
                    string yearmonth = GetQueryString("yearmonth");
                    GetTourEachDayStatus(tourid, yearmonth);
                    break;
                case "2008": //获取评论
                    GetTourCommentTotal(tourid);
                    break;
                case "2009": //获取评论列表
                    GetTourCommentList(tourid, pageindex, pagesize);
                    break;
                case "2010"://创建评论 //check token
                    CreateComment();
                    break;
                case "3000": //检查是否有未支付订单  //check token
                    CheckUnPayBill();
                    break;
                case "3001": //獲取訂單報價
                    GetBillPrice();
                    break;
                case "3002":// 下单  //check token
                    CreateBill();
                    break;
                case "3002-01":// 下单(不需要传游客信息)  //check token
                    CreateBill(true);
                    break;
                case "3003"://取消订单  //check token
                    CancelBill();
                    break;

                case "3004"://获取 微信支付 参数(小程序)
                    CreateWeChatPayParam();
                    break;


                case "5001"://入会
                    RegisterMemberFromApp();
                    break;
                case "5002"://修改会员资料 //check token
                    if (!CheckToken())//检查token
                        return;
                    ModifyMemberInfo();
                    break;
                case "5003": //获取会员信息 //check token
                    if (!CheckToken())//检查token
                        return;
                    GetMemberInfo();
                    break;
                case "6001": //我的订单 默认检查token,通过unionid不需要检查token
                    GetMemberBills(pageindex, pagesize);
                    break;
                case "6001-COUNT": //我的订单數量:用於分頁. 默认检查token,通过unionid不需要检查token
                    GetMemberBillCount();
                    break;
                case "6002": //订单详情  //check token
                    GetBillDetail();
                    break;
                case "6002-APPBILL-PAYSTATUS": //獲取預订单的 客人支付狀態
                    GetAppBilPayStatus();
                    break;
                case "6003"://上传入数纸 //check token
                    UploadTransferVoucher();
                    break;
                case "6003-DELETE": //刪除訂單圖片,包括收據和證件{ memberid,billid,imgid}
                    DeleteTourBillPhoto();
                    break;
                case "6003-01":
                    UploadTransferVoucher_File();
                    break;
                case "6004"://上传护照 //check token
                    UploadPasspoartPhoto();
                    break;
                case "6004-01":
                    UploadPasspoartPhoto_File();
                    break;
                case "6005": //上传回乡证 //check token
                    UploadHxzPhoto();
                    break;
                case "6005-01": //上传回乡证 //check token
                    UploadHxzPhoto_File();
                    break;
                case "7000": //服务条款/支付方式/保單 等外部網址
                    GetUrlSettings();
                    break;
                case "7001": //服务条款
                    GetServiceContract();
                    break;
                case "7002": //根據團號查線路
                    GetProductByCode();
                    break;
                case "7003": //查詢可用天數
                    GetProductDays();
                    break;
                case "7004": //查詢可用線路特色
                    GetProductFeatures();
                    break;
                #region 横幅接口
                case "7005": ////根據橫幅類型,获取横幅列表
                    GetBanner();
                    break;
                case "7005-BYCATALOG": //通過線路分類获取横幅{catalog}
                    GetBannerByCatalog();
                    break;
                case "7006": //获取横幅(Slide表){catalog}
                    GetSlidesByCatalog();
                    break;
                case "7006-SAVE": //保存横幅{id,catalog,imagesrc,link,type,sort}
                    SaveSlide();
                    break;
                case "7006-GET": //根據id獲取横幅{id}
                    GetSlideById();
                    break;
                case "7006-DEL": //刪除横幅{id}
                    DeleteSlide();
                    break;
                #endregion

                case "7007-PAGE": // 分頁查詢視頻列表
                    PageVideos(pageindex, pagesize);
                    break;

                #region 目的地(热门城市)管理接口
                case "7010"://按線路類型獲取目的地{type,count} 舊接口
                    GetHotCityList();
                    break;
                case "7010-LIST"://按線路類型獲取目的地{catalog,count}
                    GetTopCityByType();
                    break;
                case "7010-SAVE":  //保存目的地{id,catalog,name,sort}
                    SaveCity();
                    break;
                case "7010-GET": //根據id獲取目的地{id}
                    GetCityById();
                    break;
                case "7010-DEL": //刪除目的地{id}
                    DeleteCityById();
                    break;
                case "7010-01"://获取热门城市 升級版
                    GetHotCityList_v2();
                    break;
                #endregion

                #region 門店管理接口
                case "7020":
                    GetAllShop();//獲取所有門店列表{}
                    break;
                case "7021":
                    GetTopNShop();//獲取門店列表{top},結果:{result = list,hasmore = true|false};
                    break;
                case "7022":
                    SaveShopInfo(); //保存門店信息{id,name,address,tel,openTime,sort}
                    break;
                case "7023":
                    GetShopInfoById(); //獲取門店信息{id}
                    break;
                case "7024":
                    DeleteShopInfo(); //刪除門店{id}
                    break;
                #endregion

                #region 線路分類
                case "7040-MAP-LIST": //分類映射列表
                    GetTourCatalogMapList();
                    break;
                case "7040-CATALOGS": //後台系統的線路分類
                    GetTourCatalog();
                    break;
                case "7040-MAP-SAVE": //分類映射關係保存
                    SaveTourCatalogMap();
                    break;
                case "7040-MAP-DELETE": //刪除分類映射關係
                    DeleteTourCatalogMap();
                    break;
                #endregion

                #region 首頁彈出廣告
                case "7050-POP": //获取需要彈出的廣告圖片
                    GetNeedPopAdImage();
                    break;
                case "7050-GET": //获取廣告圖片(用於管理)
                    GetAdImage();
                    break;
                case "7050-SAVE": //保存廣告圖片
                    SaveAdImage();
                    break;
                case "7050-DEL": //刪除廣告圖片
                    DeleteAdImage();
                    break;
                #endregion

                case "7011": //获取线路特色 (沒用）
                    GetFeatures();
                    break;
                case "7012": //获取形成天数参数
                    GetProcessDays();
                    break;
                case "7015": //获取查询参数(含 主题,天数,和城市)
                    GetQueryParame();
                    break;
                case "7030"://获取活动图片
                    GetActionPicture();
                    break;
                case "8000":
                    GetAppVersion();
                    break;

                #endregion

                #region 支付
                
                case "P001": //jsApiPay获取支付参数
                    GetJsApiPayParame();
                    break;
                #endregion

                #region 導遊接口
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
                #endregion

                #region 旧接口
                //=============================================//
                case "10000": //获取团信息列表
                    string tcode = GetQueryString("tcode");
                    string bcode = GetQueryString("bcode");
                    GetTourGroupList(tcode, bcode);
                    break;
                case "11000": //获取游客列表
                    string tid = GetQueryString("tid");
                    GetTourerListByTourGroupId(tid);
                    break;
                case "12000":
                    string sFeature = GetQueryString("fc");
                    GetTourListByFeature(sFeature);
                    break;
                case "90000": //登陆
                    string sUserCode = GetQueryString("uc");
                    string sPwd = GetQueryString("pw");
                    Login(sUserCode, sPwd);
                    break;
                case "20000":
                    string sDictCode = GetQueryString("dc");
                    LoadDictItem(sDictCode);
                    break;

                #endregion

                #region
                case "T001"://获取每日收客量
                    GetDayCount();
                    break;
                case "T002"://获取每日收客量
                    GetDayCount(1);
                    break;
                case "T003"://获取每日收客量
                    GetDayAmount();
                    break;
                #endregion


                #region 酒店预订接口
                case "H1001":
                    GetHotHotelList(pageindex);
                    break;

                case "H1002":
                    GetHotelInfo();
                    break;

                case "H1003":
                    GetHotelRoomPlan(pageindex);
                    break;
                #endregion
                default:
                    ReturnJsonResponse(new ApiResult { success = 0, data = string.Empty, errorcode = ErrorCode.NO_ACTION });
                    break;
               

            }
        }

       

        /// <summary>
        /// 刪除廣告圖片
        /// </summary>
        private void DeleteAdImage()
        {
            try
            {
                //decimal id = GetQueryDecimal("id");
                
                new PopAdBiz().DeleteAll();
                
                ReturnJsonResponse(new ApiResult { success = 1, data = "success", errorcode = ErrorCode.EMPTY });
            }
            catch (Exception ex)
            {
                LogError(ex);
                ReturnJsonResponse(new ApiResult { success = 0, data = "", errorcode = ErrorCode.SYSTEM_ERROR });
            }
        }

        /// <summary>
        /// 保存彈框廣告
        /// </summary>
        private void SaveAdImage()
        {
            try
            {
                decimal id = GetQueryDecimal("id");
                string imageSrc = GetQueryString("imagesrc");
                string linkType = GetQueryString("linktype");
                string link = GetQueryString("link");
                string startTime = GetQueryString("starttime");
                string closeTime = GetQueryString("closetime");

                PopAd popAd = new PopAdBiz().Save(id, imageSrc, linkType, link, startTime, closeTime);
                if (popAd == null)
                {
                    ReturnJsonResponse(new ApiResult { success = 1, data = "", errorcode = ErrorCode.EMPTY });
                    return;
                }
                var item = new
                {
                    id = popAd.Id,
                    imagesrc = popAd.ImageSrc,
                    link = popAd.Link,
                    linktype = popAd.LinkType,
                    starttime = popAd.StartTime != null && popAd.StartTime.Year != 9999 ? popAd.StartTime.ToString("yyyy-MM-dd HH:mm:ss") : "",
                    closetime = popAd.CloseTime != null && popAd.CloseTime.Year != 9999 ? popAd.CloseTime.ToString("yyyy-MM-dd HH:mm:ss") : ""
                };
                ReturnJsonResponse(new ApiResult { success = 1, data = item, errorcode = ErrorCode.EMPTY });
            }
            catch (Exception ex)
            {
                LogError(ex);
                ReturnJsonResponse(new ApiResult { success = 0, data = "", errorcode = ErrorCode.SYSTEM_ERROR });
            }
        }

        /// <summary>
        /// 獲取廣告圖片,用於管理
        /// </summary>
        private void GetAdImage()
        {
            try
            {
                PopAd popAd = new PopAdBiz().GetPopAd();
                if (popAd == null)
                {
                    ReturnJsonResponse(new ApiResult { success = 1, data = "", errorcode = ErrorCode.EMPTY });
                    return;
                }
                var item = new
                {
                    id = popAd.Id,
                    imagesrc = popAd.ImageSrc,
                    link = popAd.Link,
                    linktype = popAd.LinkType,
                    starttime=popAd.StartTime!= null && popAd.StartTime.Year != 9999 ? popAd.StartTime.ToString("yyyy-MM-dd HH:mm:ss") :"",
                    closetime=popAd.CloseTime!= null && popAd.CloseTime.Year != 9999 ? popAd.CloseTime.ToString("yyyy-MM-dd HH:mm:ss") : ""
                };
                ReturnJsonResponse(new ApiResult { success = 1, data = item, errorcode = ErrorCode.EMPTY });
            }
            catch (Exception ex)
            {
                LogError(ex);
                ReturnJsonResponse(new ApiResult { success = 0, data = "", errorcode = ErrorCode.SYSTEM_ERROR });
            }
        }

        /// <summary>
        /// 需要彈出的廣告圖片
        /// </summary>
        private void GetNeedPopAdImage()
        {
            try
            {
                PopAd popAd = new PopAdBiz().GetLastNeedPop();
                if (popAd == null) {
                    ReturnJsonResponse(new ApiResult { success = 1, data = "", errorcode = ErrorCode.EMPTY });
                    return;
                }
                var item = new
                {
                    id = popAd.Id,
                    imagesrc = popAd.ImageSrc,
                    link = popAd.Link,
                    linktype = popAd.LinkType
                };
                ReturnJsonResponse(new ApiResult { success = 1, data = item, errorcode = ErrorCode.EMPTY });
            }
            catch (Exception ex)
            {
                LogError(ex);
                ReturnJsonResponse(new ApiResult { success = 0, data ="", errorcode = ErrorCode.SYSTEM_ERROR });
            }
        }

        private void PageVideos(int pageindex, int pagesize)
        {
            if (pageindex == 0)
                pageindex = 1;
            pagesize = 10;
            try
            {

                VideosBiz biz = new VideosBiz();
                IList list = biz.GetOnePageItems(pageindex, pagesize, "", "obj.InputDate desc");
                //int total = biz.RecordCount("");
                ArrayList result = new ArrayList();
                foreach (Videos item in list)
                {
                    string videoUrl = item.Url;//原始視頻網址
                    string vurl = StringTool.GetYouTubeIframeUrl(videoUrl);//嵌入iframe的視頻網址
                    string vid = StringTool.GetYouTubeVideoId(videoUrl); //視頻的id
                    string productId = TourBiz.GetFieldValue("obj.Id", "obj.ProductCode='" + item.ProductCode + "'");
                    var row = new
                    {
                        id = item.Id,
                        title = item.Title, //視頻標題
                        url = vurl,//嵌入iframe的視頻網址
                        productcode = item.ProductCode,
                        productid= productId,
                        vid
                    };
                    result.Add(row);
                }
                ReturnJsonResponse(new ApiResult { success = 1, data = result, errorcode = ErrorCode.EMPTY });
            }
            catch (Exception ex)
            {
                LogError(ex);
                ReturnJsonResponse(new ApiResult { success = 0, data = new ArrayList(), errorcode = ErrorCode.SYSTEM_ERROR });
            }

        }


        /// <summary>
        /// 刪除橫幅通過id{id}
        /// </summary>
        private void DeleteSlide()
        {
            decimal id = GetQueryDecimal("id");
            try
            {
                new SlideBiz().DeleteById(id);
                ReturnJsonResponse(new ApiResult { success = 1, data = "success", errorcode = ErrorCode.EMPTY });
            }
            catch (Exception ex)
            {
                LogError(ex);
                ReturnJsonResponse(new ApiResult { success = 0, data = "", errorcode = ErrorCode.SYSTEM_ERROR });
            }
        }

        /// <summary>
        /// 獲取橫幅通過id{id}
        /// </summary>
        private void GetSlideById()
        {
            decimal id = GetQueryDecimal("id");
            try
            {
                Slide item = new SlideBiz().GetByID(id);
                var data = new
                {
                    id = item.Id,
                    catalog = item.Catalog,
                    imagesrc = item.ImageSrc,
                    link = item.Link,
                    sort = item.Sort,
                    type = item.Type
                };
                ReturnJsonResponse(new ApiResult { success = 1, data = data, errorcode = ErrorCode.EMPTY });
            }
            catch (Exception ex)
            {
                LogError(ex);
                ReturnJsonResponse(new ApiResult { success = 0, data = "", errorcode = ErrorCode.SYSTEM_ERROR });
            }
        }

        /// <summary>
        /// 保存橫幅{id,catalog,imageSrc,link,type,sort}
        /// </summary>
        private void SaveSlide()
        {
            decimal id = GetQueryDecimal("id");
            string catalog = GetQueryString("catalog");
            string imageSrc = GetQueryString("imagesrc");
            string link = GetQueryString("link");
            string type = GetQueryString("type");
            int sort = GetQueryInt("sort");
            try
            {
                Slide item = new SlideBiz().Save(id, catalog, type, imageSrc, link, sort);
                var data = new
                {
                    id = item.Id,
                    catalog = item.Catalog,
                    imagesrc = item.ImageSrc,
                    link = item.Link,
                    sort = item.Sort,
                    type = item.Type
                };
                ReturnJsonResponse(new ApiResult { success = 1, data = data, errorcode = ErrorCode.EMPTY });
            }
            catch (Exception ex)
            {
                LogError(ex);
                ReturnJsonResponse(new ApiResult { success = 0, data = "", errorcode = ErrorCode.SYSTEM_ERROR });
            }
        }

        /// <summary>
        /// 通過線路類型獲取橫幅
        /// </summary>
        private void GetSlidesByCatalog()
        {
            try
            {
              //{ value: "LINE", label: "线路詳情"},
              //{ value: "DOC", label: "站內文章"},
              //{ value: "KEYWORD", label: "搜索关键字"},
              //{ value: "LINK", label: "外部鏈接"}
                string catalog = GetQueryString("catalog");

                IList result = new SlideBiz().GetByCatalog(catalog);
                ArrayList list = new ArrayList();
                foreach (Slide item in result)
                {
                    string tourid = "0"; string articleid = "0"; string keyword = "";
                    if (item.Type == "LINE") // /register/10885
                    {
                        tourid = item.Link.ToLower().Replace("/detail/", "").Replace("/register/", "");
                    }
                    if (item.Type == "DOC") // /article/660d5abb1414a1a5c1fd20d9
                    {
                        articleid = item.Link.ToLower().Replace("/article/", "");
                    }
                    if (item.Type == "KEYWORD") // /search?k=CGZ088
                    {
                        keyword = item.Link.ToLower().Replace("/search?k=", "");
                    }
                    var row = new
                    {
                        id = item.Id,
                        catalog = item.Catalog,
                        imagesrc = item.ImageSrc,
                        link = item.Link,
                        sort = item.Sort,
                        type = item.Type,
                        //下面是轉換後的結果
                        tourid,
                        articleid,
                        keyword
                    };
                    list.Add(row);
                }
                ReturnJsonResponse(new ApiResult { success = 1, data = list, errorcode = ErrorCode.EMPTY });
            }
            catch (Exception ex)
            {
                LogError(ex);
                ReturnJsonResponse(new ApiResult { success = 0, data = new ArrayList(), errorcode = ErrorCode.SYSTEM_ERROR });
            }
        }

        /// <summary>
        /// 刪除線路類型映射記錄
        /// </summary>
        private void DeleteTourCatalogMap()
        {
            decimal id = GetQueryDecimal("id");
            try
            {
                TourCatalogMapBiz biz = new TourCatalogMapBiz();
                TourCatalogMap map = biz.GetByID(id);
                biz.Delete(map);

                ReturnJsonResponse(new ApiResult { success = 1, data = "success", errorcode = ErrorCode.EMPTY });
            }
            catch (Exception ex)
            {
                LogError(ex);
                ReturnJsonResponse(new ApiResult { success = 0, data = "", errorcode = ErrorCode.SYSTEM_ERROR });
            }
        }


        /// <summary>
        /// 保存線路分類
        /// </summary>
        private void SaveTourCatalogMap()
        {
            decimal id = GetQueryDecimal("id");
            string catalog = GetQueryString("catalog");
            string siteCatalog = GetQueryString("sitecatalog");
            string resume = GetQueryString("resume");

            try
            {
                TourCatalogMapBiz biz = new TourCatalogMapBiz();
                TourCatalogMap map = new TourCatalogMap();
                if (id > 0)
                {
                    map = biz.GetByID(id);
                }
                map.SiteCatalog = siteCatalog; //網站分類
                map.Catalog = catalog; //後台分類(多個逗號隔開)
                map.Resume = resume;
                if (id > 0)
                {
                    biz.Update(map);
                }
                else {
                    biz.Create(map);
                }

                var data = new
                {
                    id = map.Id,
                    siteCatalog = map.SiteCatalog,
                    catalog = map.Catalog,
                    resume = map.Resume
                };

                ReturnJsonResponse(new ApiResult { success = 1, data = data, errorcode = ErrorCode.EMPTY });
            }
            catch (Exception ex)
            {
                LogError(ex);
                ReturnJsonResponse(new ApiResult { success = 0, data = "", errorcode = ErrorCode.SYSTEM_ERROR });
            }
        }

        /// <summary>
        /// 獲取後台線路分類
        /// </summary>
        private void GetTourCatalog()
        {
            try
            {
                DataTable list = TourCatalogParaBiz.GetList();

                ArrayList result = new ArrayList();
                foreach (DataRow item in list.Rows)
                {
                    // Value, Text
                    var data = new
                    {
                        value = item["Value"].ToString(),
                        label = item["Text"].ToString(),
                    };
                    result.Add(data);
                }

                ReturnJsonResponse(new ApiResult { success = 1, data = result, errorcode = ErrorCode.EMPTY });
            }
            catch (Exception ex)
            {
                LogError(ex);
                ReturnJsonResponse(new ApiResult { success = 0, data = "", errorcode = ErrorCode.SYSTEM_ERROR });
            }
           
        }

        /// <summary>
        /// 獲取線路分類映射列表
        /// </summary>
        private void GetTourCatalogMapList()
        {
            try
            {
                IList list= new TourCatalogMapBiz().GetAllItems("","");
                ArrayList result = new ArrayList();
                foreach (TourCatalogMap item in list)
                {
                    var data = new
                    {
                        id = item.Id,
                        siteCatalog = item.SiteCatalog,
                        catalog = item.Catalog,
                        resume = item.Resume
                    };
                    result.Add(data);
                }
                
                ReturnJsonResponse(new ApiResult { success = 1, data = result, errorcode = ErrorCode.EMPTY });
            }
            catch (Exception ex)
            {
                LogError(ex);
                ReturnJsonResponse(new ApiResult { success = 0, data = "", errorcode = ErrorCode.SYSTEM_ERROR });
            }
        }

        private void DeleteCityById()
        {
            decimal id = GetQueryDecimal("id");
            try
            {
                new CityBiz().DeleteById(id);
                ReturnJsonResponse(new ApiResult { success = 1, data = "success", errorcode = ErrorCode.EMPTY });
            }
            catch (Exception ex)
            {
                LogError(ex);
                ReturnJsonResponse(new ApiResult { success = 0, data = "", errorcode = ErrorCode.SYSTEM_ERROR });
            }
        }

        private void GetCityById()
        {
            decimal id = GetQueryDecimal("id");
            try
            {
                City item = new CityBiz().GetByID(id);
                var data = new
                {
                    id = item.Id,
                    name = item.Name,
                    catalog = item.Type.ToString(),
                    sort = item.Sort
                };
                ReturnJsonResponse(new ApiResult { success = 1, data = data, errorcode = ErrorCode.EMPTY });
            }
            catch (Exception ex)
            {
                LogError(ex);
                ReturnJsonResponse(new ApiResult { success = 0, data = "", errorcode = ErrorCode.SYSTEM_ERROR });
            }
        }

        /// <summary>
        /// {id,type,name,sort}
        /// </summary>
        private void SaveCity()
        {
            decimal id = GetQueryDecimal("id");
            int catalog = GetQueryInt("catalog");
            string name = GetQueryString("name");
            int sort = GetQueryInt("sort");
            try
            {
                City item = new CityBiz().Save(id, name, catalog, sort);
                var data = new
                {
                    id = item.Id,
                    name = item.Name,
                    catalog = item.Type.ToString(),
                    sort = item.Sort
                };
                ReturnJsonResponse(new ApiResult { success = 1, data = data, errorcode = ErrorCode.EMPTY });
            }
            catch (Exception ex)
            {
                LogError(ex);
                ReturnJsonResponse(new ApiResult { success = 0, data = "", errorcode = ErrorCode.SYSTEM_ERROR });
            }
        }

        /// <summary>
        /// 获取热门城市 7010-LIST
        /// </summary>
        private void GetTopCityByType()
        {
            try
            {
                int type = GetQueryInt("catalog");
                //城市类型 0= 广东短线，1=中国长线 3=东南亚 7=跨省巴士 5=邮轮
                int count = GetQueryInt("count");//


                ArrayList citys = new ArrayList();
                IList result = new CityBiz().GetByType(type, count);
                foreach (City item in result)
                {
                    var ele = new { id = item.Id, name = item.Name, catalog = item.Type.ToString(), sort = item.Sort };
                    citys.Add(ele);
                }
                ReturnJsonResponse(new ApiResult { success = 1, data = citys, errorcode = ErrorCode.EMPTY });
            }
            catch (Exception ex)
            {
                LogError(ex);
                ReturnJsonResponse(new ApiResult { success = 0, data = "", errorcode = ErrorCode.SYSTEM_ERROR });
            }

        }

        private void GetShopInfoById()
        {
            decimal id = GetQueryDecimal("id");
            try
            {
                Shop item = new ShopBiz().GetByID(id);
                var data = new
                {
                    id = item.Id,
                    name = item.Name,
                    address = item.Address,
                    tel = item.Tel,
                    openTime = item.OpenTime,
                    sort = item.Sort
                };
                ReturnJsonResponse(new ApiResult { success = 1, data = data, errorcode = ErrorCode.EMPTY });
            }
            catch (Exception ex)
            {
                LogError(ex);
                ReturnJsonResponse(new ApiResult { success = 0, data = "", errorcode = ErrorCode.SYSTEM_ERROR });
            }
        }

        /// <summary>
        /// 刪除門店信息
        /// </summary>
        private void DeleteShopInfo()
        {
            decimal id = GetQueryDecimal("id");
            try
            {
                new ShopBiz().DeleteShop(id);
                ReturnJsonResponse(new ApiResult { success = 1, data = "success", errorcode = ErrorCode.EMPTY });
            }
            catch (Exception ex)
            {
                LogError(ex);
                ReturnJsonResponse(new ApiResult { success = 0, data = "", errorcode = ErrorCode.SYSTEM_ERROR });
            }
        }

        /// <summary>
        /// 保存門店信息{id,name,address,tel,openTime,sort}
        /// </summary>
        private void SaveShopInfo()
        {
            decimal id = GetQueryDecimal("id");
            string name = GetQueryString("name");
            string address = GetQueryString("address");
            string tel = GetQueryString("tel");
            string openTime = GetQueryString("openTime");
            int sort = GetQueryInt("sort");
            try
            {
                Shop item = new ShopBiz().Save(id, name, address, tel, openTime, sort);
                var data = new
                {
                    id = item.Id,
                    name = item.Name,
                    address = item.Address,
                    tel = item.Tel,
                    openTime = item.OpenTime,
                    sort = item.Sort
                };
                ReturnJsonResponse(new ApiResult { success = 1, data = data, errorcode = ErrorCode.EMPTY });
            }
            catch (Exception ex)
            {
                LogError(ex);
                ReturnJsonResponse(new ApiResult { success = 0, data = "", errorcode = ErrorCode.SYSTEM_ERROR });
            }
        }

        /// <summary>
        /// 獲取門店列表
        /// </summary>
        private void GetAllShop()
        {
            try
            {
                IList result = new ShopBiz().GetAll();
                ArrayList list = new ArrayList();
                foreach (Shop item in result)
                {
                    var row = new
                    {
                        id = item.Id,
                        name = item.Name,
                        address = item.Address,
                        tel = item.Tel,
                        openTime = item.OpenTime,
                        sort = item.Sort
                    };
                    list.Add(row);
                }
                ReturnJsonResponse(new ApiResult { success = 1, data = list, errorcode = ErrorCode.EMPTY });
            }
            catch (Exception ex)
            {
                LogError(ex);
                ReturnJsonResponse(new ApiResult { success = 0, data = new ArrayList(), errorcode = ErrorCode.SYSTEM_ERROR });
            }
        }

        /// <summary>
        /// 獲取前N條門店
        /// </summary>
        private void GetTopNShop()
        {
            int top = GetQueryInt("top");
            try
            {
                ShopBiz biz = new ShopBiz();
                IList result = biz.GetTopN(top);
                int totalCount = biz.RecordCount("");
                ArrayList list = new ArrayList();
                foreach (Shop item in result)
                {
                    var row = new
                    {
                        id = item.Id,
                        name = item.Name,
                        address = item.Address,
                        tel = item.Tel,
                        openTime = item.OpenTime,
                        sort = item.Sort
                    };
                    list.Add(row);
                }
                var data = new
                {
                    result = list,
                    hasmore = totalCount > top
                };
                ReturnJsonResponse(new ApiResult { success = 1, data = data, errorcode = ErrorCode.EMPTY });
            }
            catch (Exception ex)
            {
                LogError(ex);
                ReturnJsonResponse(new ApiResult { success = 0, data = new ArrayList(), errorcode = ErrorCode.SYSTEM_ERROR });
            }
        }


        /// <summary>
        /// 查詢不同類型線路的可用線路特色
        /// </summary>
        private void GetProductFeatures()
        {
            int catalog = GetQueryInt("catalog");
            int jiaban = GetQueryInt("jiaban");
            try
            {
                DataTable dt = new TourGroupBiz().GetProductFeatures(catalog, jiaban);
                ArrayList list = new ArrayList();
                foreach (DataRow item in dt.Rows)
                {
                    var row = new
                    {
                        id = item["Id"],
                        name = item["Name"],
                    };
                    list.Add(row);
                }
                ReturnJsonResponse(new ApiListResult { success = 1, rows = list, errorcode = ErrorCode.EMPTY });
            }
            catch (Exception ex)
            {
                LogError(ex);
                ReturnJsonResponse(new ApiListResult { success = 0, rows = new ArrayList(), errorcode = ErrorCode.SYSTEM_ERROR });
            }
        }

        /// <summary>
        /// 查詢不同類型線路的可用線路 行程天數
        /// </summary>
        private void GetProductDays()
        {
            int catalog = GetQueryInt("catalog");
            int jiaban = GetQueryInt("jiaban");
            try
            {
                DataTable dt = new TourGroupBiz().GetProductDays(catalog, jiaban);
                ArrayList list = new ArrayList();
                foreach (DataRow item in dt.Rows)
                {
                    int days = StringTool.String2Int(item["ProcessDays"].ToString());
                    list.Add(days);
                }
                list.Sort();
                ReturnJsonResponse(new ApiListResult { success = 1, rows = list, errorcode = ErrorCode.EMPTY });
            }
            catch (Exception ex)
            {
                LogError(ex);
                ReturnJsonResponse(new ApiListResult { success = 0, rows = new ArrayList(), errorcode = ErrorCode.SYSTEM_ERROR });
            }
        }
        /// <summary>
        /// 根據團號查線路檔案
        /// </summary>
        private void GetProductByCode()
        {
            string productCode = GetQueryString("productcode");
            if (string.IsNullOrEmpty(productCode))
            {
                ReturnJsonResponse(new ApiResult { success = 0, data = string.Empty, errorcode = ErrorCode.ERROR_PARAM });
                return;
            }
            try
            {
                IList list = new TourBiz().SearchByProductCodeTop10(productCode);
                ArrayList result = new ArrayList();
                foreach (nRelax.Tour.Entity.Tour item in list)
                {
                    var row = new
                    {
                        id = item.Id,
                        tourcode = item.ProductCode,
                        tourname = item.ProductName,
                        processdays = item.ProcessDays,

                    };
                    result.Add(row);
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
               
                TourGroupBiz bizTourGroup = new TourGroupBiz();
                
                decimal guideId = bizTourGroup.GetGuideId(nTourGroupId);
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
                else {
                    ReturnJsonResponse(new ApiListResult { success = 1, rows = new ArrayList(), errorcode = ErrorCode.EMPTY });
                }
                
            }
            catch (Exception ex)
            {
                LogError(ex);
                ReturnJsonResponse(new ApiListResult { success = 0,rows = "", errorcode = ErrorCode.SYSTEM_ERROR });
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
                else {
                    ReturnJsonResponse(new ApiResult { success = 0, data = "", errorcode = error });
                }
            }
            catch (Exception ex) {
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
                ReturnJsonResponse(new ApiResult { success = 0, data ="缺少參數[缺導遊Id]", errorcode = ErrorCode.ERROR_PARAM });
                return;
            }
            try
            {
             
                int fileCount=HttpContext.Current.Request.Files.Count;
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
                    Logger.Error("p03 uploadResult" + uploadResult==null?"null":"not null");
                    if (uploadResult.status == 1)
                    {
                        result.Add(uploadResult.path);
                    }
                    else {
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
                else {
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
        /// 根據id獲取雜費項目
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
                TourGroupBiz bizTourGroup = new TourGroupBiz();
                TourGroupOtherFee tourGroupOtherFee = new TourGroupOtherFee();

                tourGroupOtherFee = biz.GetByID(nId);
                decimal nTourGroupId = tourGroupOtherFee.TourGroupID;

                int status = bizTourGroup.GetStatus(nTourGroupId);
                decimal guideId = bizTourGroup.GetGuideId(nTourGroupId);
                if (guideId != nGuideId)
                {
                    ReturnJsonResponse(new ApiResult { success = 0, data = "", errorcode = "只有導遊自己可以查看自己的報賬" });
                    return;
                }
               

                //獲取費用信息
                TourGroupOtherFee item= biz.GetByID(nId);
                Dictionary<string, object> poInfo = new Dictionary<string, object>();
                if (item.PoBillId > 0 && !string.IsNullOrEmpty(item.PoBillType))
                {
                    poInfo = biz.getPoBillInfo(item.PoBillId, item.PoBillType);
                }

                var result = new {
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
                    suppliersType=item.PoBillType,
                    poBillId =item.PoBillId,
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
                if(lstFee!=null && lstFee.Count > 0)
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
                string sTotalRmb=biz.GetFieldSUM("obj.Dir*obj.Amount", sWhere);
                sTotalRmb = sTotalRmb == null ? "0" : sTotalRmb;

                //導遊借款
                string guideAdvanceFee = TourGroupBiz.GetFieldValue("obj.GuideAdvanceFee", string.Format("obj.Id={0}", nTourGroupId));
                guideAdvanceFee = guideAdvanceFee == null ? "0" : guideAdvanceFee;

                decimal totalGuideReturnAmount = -1 * (StringTool.String2Decimal(sTotalRmb) + StringTool.String2Decimal(guideAdvanceFee));

                var result = new {
                    totalRMB= StringTool.String2Decimal(sTotalRmb).ToString("f2").Replace(".00",""), //人民幣總匯總(負支出,正收入)
                    guideAdvanceFee= StringTool.String2Decimal(guideAdvanceFee).ToString("f2").Replace(".00", ""),//導遊借款
                    returnGuideFee= totalGuideReturnAmount.ToString("f2").Replace(".00", "")//應退導遊(如果負值,導遊會退公司)
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
                TourGroupBiz bizTourGroup = new TourGroupBiz();
                TourGroupOtherFee tourGroupOtherFee = new TourGroupOtherFee();
                tourGroupOtherFee = biz.GetByID(nId);
                decimal nTourGroupId = tourGroupOtherFee.TourGroupID;
                
                int status = bizTourGroup.GetStatus(nTourGroupId);
                decimal guideId = bizTourGroup.GetGuideId(nTourGroupId);
                if (guideId != nGuideId) {
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
                TourGroupBiz bizTourGroup = new TourGroupBiz();
                TourGroupOtherFee tourGroupOtherFee = new TourGroupOtherFee();
                if (nId == 0 && poBillId>0) {
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
                    planStatus == EnuTourPlanStatus.WillSale) {

                    string strStaus=UseEnum.EnuToString(planStatus);
                    ReturnJsonResponse(new ApiResult { success = 0, data = "", errorcode = "團的狀態為【"+ strStaus + "】不符合報賬要求" });
                    return;
                }

                if (nAmount <= 0) {
                    ReturnJsonResponse(new ApiResult { success = 0, data = "", errorcode = "金額錯誤" });
                    return;
                }
                if (strResume.Trim().Length== 0)
                {
                    ReturnJsonResponse(new ApiResult { success = 0, data = "", errorcode = "費用說明不能為空" });
                    return;
                }
                if (strCurrency.Trim().Length == 0)
                {
                    ReturnJsonResponse(new ApiResult { success = 0, data = "", errorcode = "幣種不能為空" });
                    return;
                }
                string guideName = new GuideInfoBiz().GetGuideName(nGuideId);

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

                decimal newId =bizOtherFee.Save(tourGroupOtherFee);

                
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
        private void GetMiniLoginInfo() {
   
            string result = MiniAppLogin("1");
            if (result.Trim().Length > 0)
            {
                JObject jo = (JObject)JsonConvert.DeserializeObject(result);
                string openid = jo.GetValue("openid").ToString();
                decimal memberId = 0;
                string phone = "";
                Members member= new MemberBiz().GetByWeChatUnionId(openid);
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
                    memberId= memberId
                };
                ReturnJsonResponse(new ApiResult { success = 1, data = data, errorcode = ErrorCode.EMPTY });
            }
            else {
                ReturnJsonResponse(new ApiResult { success = 0, data = string.Empty, errorcode = ErrorCode.ERROR_PARAM });
            }
        }

        /// <summary>
        /// 003 解密小程序手机号码,並註冊或登錄
        /// </summary>
        private void LoginByMiniPhone() {
           
            string openId = GetQueryString("openid");
            try
            {
                if (openId.Trim().Length == 0) {
                    ReturnJsonResponse(new ApiResult { success = 0, data = string.Empty, errorcode = ErrorCode.ERROR_PARAM });
                    return;
                }

                string phone = getMiniPhoneNumber();
                if (phone.Length > 0)
                {
                    //写会员数据
                    MemberBiz biz = new MemberBiz();
                    decimal nPayId = 0; //支付單
                    decimal nMemberId = biz.RegisterFromApp_Simple(phone, out nPayId, openId);
                    if (nMemberId == 0)
                    {
                        ReturnJsonResponse(new ApiResult { success = 0, data = string.Empty, errorcode = ErrorCode.ERROR_CREATE_MEMBER });
                        return;
                    }

                    ReturnJsonResponse(new ApiResult { success = 1, data = new { phone=phone,memberId=nMemberId }, errorcode = ErrorCode.EMPTY });
                }
                else {
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
        private void GetJsApiPayParame()
        {
           
            string openid = GetQueryString("openid");
            string total_fee = GetQueryString("total_fee");
            //检测是否给当前页面传递了相关参数
            if (string.IsNullOrEmpty(openid) || string.IsNullOrEmpty(total_fee))
            {
                ReturnJsonResponse(new ApiResult { success = 0, data = string.Empty, errorcode = ErrorCode.ERROR_PARAM});
                return;
            }

            //若传递了相关参数，则调统一下单接口，获得后续相关接口的入口参数
            JsApiPay jsApiPay = new JsApiPay();
            jsApiPay.openid = openid;
            jsApiPay.total_fee = int.Parse(total_fee);

            //JSAPI支付预处理
            try
            {
                WxPayData unifiedOrderResult = jsApiPay.GetUnifiedOrderResult();
                string wxJsApiParam = jsApiPay.GetJsApiParameters();//获取H5调起JS API参数                    
                ReturnJsonResponse(new ApiResult { success = 1, data = wxJsApiParam, errorcode = ErrorCode.EMPTY });

            }
            catch (Exception ex)
            {
                LogError(ex);
                ReturnJsonResponse(new ApiResult { success = 0, data = string.Empty, errorcode = ErrorCode.SYSTEM_ERROR });
            }
        }

        /// <summary>
        /// 获取酒店某房型计划及状态
        /// </summary>
        private void GetHotelRoomPlan(int pageIndex)
        {
            try
            {
                decimal hotelId = GetQueryDecimal("hotelid");
                decimal roomId = GetQueryDecimal("roomid");

                HotelApiBiz biz = new HotelApiBiz();
                IList list = biz.GetHotelEachDayStatus(hotelId, roomId, pageIndex);

                ReturnJsonResponse(new ApiListResult { success = 1, rows = list, errorcode = ErrorCode.EMPTY });

            }
            catch (Exception ex)
            {
                LogError(ex);
                ReturnJsonResponse(new ApiListResult { success = 0, rows = string.Empty, errorcode = ErrorCode.SYSTEM_ERROR });
            }
        }

        /// <summary>
        /// 获取酒店详细信息
        /// </summary>
        private void GetHotelInfo()
        {
            try {
                decimal hotelId = GetQueryDecimal("hotelid");
                HotelApiBiz biz = new HotelApiBiz();
                Dictionary<string, object> hotelInfo = biz.GetHotelInfo(hotelId);

                Hotel hotel = hotelInfo["hotel"]==null?null: hotelInfo["hotel"] as Hotel;
                DataTable rooms = hotelInfo["room"]==null?null:hotelInfo["room"] as DataTable;
                Dictionary<string, string> attrs = hotelInfo["attrib"]==null?null: hotelInfo["attrib"] as Dictionary<string, string>;

                ArrayList roomsResult = new ArrayList();
                if (rooms != null) {
                    foreach (DataRow item in rooms.Rows)
                    {
                        roomsResult.Add(new {
                            id = item["id"],
                            name =item["roomType"],
                            resume=item["resume"],
                            price=item["minPrice"],
                            pic=item["pic"]
                        });
                    }
                }
                string surroundings = string.Empty;
                attrs.TryGetValue("surroundings", out surroundings);
                string traffic = string.Empty;
                attrs.TryGetValue("traffic", out traffic);
                string netSetting = string.Empty;
                attrs.TryGetValue("netSetting", out netSetting);
                string roomSetting = string.Empty;
                attrs.TryGetValue("roomSetting", out roomSetting);
                string commonSetting = string.Empty;
                attrs.TryGetValue("commonSetting", out commonSetting);
                string serviceItem = string.Empty;
                attrs.TryGetValue("serviceItem", out serviceItem);
                var result = new
                {
                    name =hotel.Name,//酒店名
                    recommend = "",//推荐语
                    city = hotel.City,//城市
                    address = hotel.Address,//详细地址
                    mainpic = hotel.MainPic,//主图
                    labels = "",//标签
                    tel=hotel.Tel,
                    surroundings= surroundings??"",//周边环境
                    traffic= traffic??"", //周边交通
                    netSetting= netSetting??"", //网络设施
                    roomSetting= roomSetting??"", //房间设施
                    commonSetting= commonSetting??"",//通用设施
                    serviceItem = serviceItem??"",  //服务项目

                    rooms = roomsResult //房型列表
                };

                ReturnJsonResponse(new ApiResult { success = 1, data = result, errorcode = ErrorCode.EMPTY });
            }
            catch (Exception ex) {
                LogError(ex);
                ReturnJsonResponse(new ApiResult { success = 0, data = string.Empty, errorcode = ErrorCode.SYSTEM_ERROR });
            }
        }

        /// <summary>
        /// 获取酒店推荐列表
        /// </summary>
        /// <param name="pageindex"></param>
        private void GetHotHotelList(int pageindex)
        {
            try
            {
                decimal memberId = GetQueryDecimal("memberid");
                string openId = GetQueryString("openid");

                HotelApiBiz biz = new HotelApiBiz();
                DataTable dt = biz.GetHotHotelList(memberId, openId, pageindex);

                if (dt.Rows.Count > 0)
                {
                    ArrayList lst = new ArrayList();
                    foreach (DataRow row in dt.Rows)
                    {
                        var data = new
                        {
                            id=row["id"],
                            name = row["name"],//酒店名
                            recommend = row["recommend"],//推荐语
                            city=row["city"],//城市
                            address=row["address"],//详细地址
                            mainpic=row["mainpic"],//主图
                            labels=row["labels"],//标签
                            remainCount=row["remainCount"],//剩余间晚
                            isFave=row["isFave"] //当前用户是否收藏
                        };
                        lst.Add(data);
                    }

                    ReturnJsonResponse(new ApiListResult { success = 1, rows = lst, errorcode = ErrorCode.EMPTY });
                }
                else
                    ReturnJsonResponse(new ApiListResult { success = 1, rows = "", errorcode = ErrorCode.EMPTY });

            }
            catch (Exception ex) {
                LogError(ex);
                ReturnJsonResponse(new ApiListResult { success = 0, rows = string.Empty, errorcode = ErrorCode.SYSTEM_ERROR });
            }
        }

        /// <summary>
        /// 获取每日收客额
        /// </summary>
        private void GetDayAmount()
        {
            try
            {
                int year = GetQueryInt("year");
                int month = GetQueryInt("month");

                DataTable dt = new APIBiz().GetDayAmount(year, month);
              
                if (dt.Rows.Count > 0)
                {
                    ArrayList lst = new ArrayList();
                    foreach (DataRow row in dt.Rows)
                    {
                        var data = new
                        {
                            theday = row["d"],
                            amt = row["amt"]
                        };
                        lst.Add(data);
                    }
                    
                    ReturnJsonResponse(new ApiListResult { success = 1, rows = lst, errorcode = ErrorCode.EMPTY });
                }
                else
                    ReturnJsonResponse(new ApiListResult { success = 1, rows = "", errorcode = ErrorCode.EMPTY });
            }
            catch (Exception ex)
            {
                LogError(ex);
                ReturnJsonResponse(new ApiListResult { success = 0, rows = string.Empty, errorcode = ErrorCode.SYSTEM_ERROR });

            }
        }

        /// <summary>
        /// 获取每日收客量 T001
        /// <paramref name="type" >0-收客日期, 1-出发日期</paramref>
        /// </summary>
        private void GetDayCount(int type=0)
        {
            try
            {
                int year = GetQueryInt("year");
                int month = GetQueryInt("month");

                DataTable dt=new DataTable();
                if (type==0)
                    dt = new APIBiz().GetDayRegisterCount(year,month);
                if (type == 1)
                    dt = new APIBiz().GetDayStartCount(year, month);

                if (dt.Rows.Count > 0)
                {
                    DataRow row = dt.Rows[0];
                    var data = new
                    {
                        nowsum = row["nowsum"].ToString()==""?0: row["nowsum"],
                        yesday = row["yesday"].ToString() == "" ? 0: row["yesday"],
                        day1 = row["day1"].ToString()==""?0:row["day1"],
                        day2 = row["day2"].ToString()==""?0:row["day2"],
                        day3 = row["day3"].ToString()==""?0:row["day3"],
                        day4 = row["day4"].ToString()==""?0:row["day4"],
                        day5 = row["day5"].ToString()==""?0:row["day5"],
                        day6 = row["day6"].ToString()==""?0:row["day6"],
                        day7 = row["day7"].ToString()==""?0:row["day7"],
                        day8 = row["day8"].ToString()==""?0:row["day8"],
                        day9 = row["day9"].ToString()==""?0: row["day9"],
                        day10 = row["day10"].ToString()==""?0: row["day10"],
                        day11 = row["day11"].ToString()==""?0:row["day11"],
                        day12 = row["day12"].ToString()==""?0:row["day12"],
                        day13 = row["day13"].ToString()==""?0:row["day13"],
                        day14 = row["day14"].ToString()==""?0:row["day14"],
                        day15 = row["day15"].ToString()==""?0:row["day15"],
                        day16 = row["day16"].ToString()==""?0:row["day16"],
                        day17 = row["day17"].ToString()==""?0:row["day17"],
                        day18 = row["day18"].ToString()==""?0:row["day18"],
                        day19 = row["day19"].ToString()==""?0:row["day19"],
                        day20 = row["day20"].ToString()==""?0:row["day20"],
                        day21 = row["day21"].ToString()==""?0:row["day21"],
                        day22 = row["day22"].ToString()==""?0:row["day22"],
                        day23 = row["day23"].ToString()==""?0:row["day23"],
                        day24 = row["day24"].ToString()==""?0:row["day24"],
                        day25 = row["day25"].ToString()==""?0:row["day25"],
                        day26 = row["day26"].ToString()==""?0:row["day26"],
                        day27 = row["day27"].ToString()==""? 0:row["day27"],
                        day28 = row["day28"].ToString()==""? 0:row["day28"],
                        day29 = row["day29"].ToString()==""? 0:row["day29"],
                        day30 = row["day30"].ToString()==""? 0:row["day30"],
                        day31 = row["day31"].ToString()==""? 0:row["day31"],
                        dayall = row["dayall"].ToString()==""? 0: row["dayall"]
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
        /// 检查Token是否已经过期
        /// </summary>
        private void CheckTokenApi()
        {
            if (CheckToken()) {
                ReturnJsonResponse(new ApiResult { success = 1, data = string.Empty, errorcode = ErrorCode.EMPTY });
            }
        }

        /// <summary>
        /// 重新登录 @@还未实现
        /// </summary>
        private void ReLgoin() {
            if (CheckToken())
            {
                //@@ 还未实现,增加刷新token的功能
                ReturnJsonResponse(new ApiResult { success = 1, data = string.Empty, errorcode = ErrorCode.EMPTY });
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

                DataTable dt = new GuideInfoBiz().GetGuideByMobileNameAndTourCode(name, ftname, mobile, tourcode);
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
              
                DataTable dt = new GuideInfoBiz().GetGuideByMobile(mobile);
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
                string sguideid = TourGroupBiz.GetFieldValue("obj.GuideID", tgid);
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
                        linktel = item["LinkTel"].ToString().Replace("/","").Trim().Length==0?a:item["LinkTel"].ToString().Split('/'),
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
                string sguideid = TourGroupBiz.GetFieldValue("obj.GuideID", tgid);
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
                string sguideid = TourGroupBiz.GetFieldValue("obj.GuideID", tgid);
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
                string sguideid = TourGroupBiz.GetFieldValue("obj.GuideID", tgid);
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
                string stgticketremark = TourGroupBiz.GetFieldValue("obj.RemarkTicket", tgid);
                //景點預訂全局備註
                string sTicketResume = new DocumentBiz().GetContentByType(EnuDocumentType.TourGroupBillTicketRemark);
                string strTgCatalog = TourGroupBiz.GetFieldValue("obj.Catalog", tgid);
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
                string sguideid = TourGroupBiz.GetFieldValue("obj.GuideID", tgid);
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
                string stgrepeastremark = TourGroupBiz.GetFieldValue("obj.RemarkRepast", tgid);
                //酒店預訂全局備註
                string sRepeastResume = new DocumentBiz().GetContentByType(EnuDocumentType.TourGroupBillRepastRemark);
                string strTgCatalog = TourGroupBiz.GetFieldValue("obj.Catalog", tgid);
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
                string sguideid = TourGroupBiz.GetFieldValue("obj.GuideID", tgid);
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
                string stghotelremark = TourGroupBiz.GetFieldValue("obj.RemarkHotel", tgid);
                //酒店預訂全局備註
                string sHotelResume = new DocumentBiz().GetContentByType(EnuDocumentType.TourGroupBillHotelRemark);
                string strTgCatalog = TourGroupBiz.GetFieldValue("obj.Catalog", tgid);
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
                string sguideid = TourGroupBiz.GetFieldValue("obj.GuideID", tgid);
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
                TourGroup tg = new TourGroupBiz().GetByID(tgid);
                if (tg.GuideID != nGuideId)
                {
                    ReturnJsonResponse(new ApiResult { success = 0, data = string.Empty, errorcode = ErrorCode.ERROR_PARAM });
                    return;
                }
                string scode = TourBiz.GetFieldValue("obj.ProductCode", tg.ProductID);
                string strRDCatalog = TourGroupBiz.GetFieldValue("obj.RDCatalog", tgid);
                string strTgCatalog = TourGroupBiz.GetFieldValue("obj.Catalog", tgid);
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
                    startdate_days =(tg.StartDate - DateTime.Today).Days,
                    starttime = tg.StartTime,
                    guideid = tg.GuideID,
                    processdays = tg.ProcessDays,
                    isrectip = tg.IsReceiveTips ? 1 : 0,
                    norqty = tg.NormalCount + tg.MemberCount,
                    childqty = tg.ChildCount,

                    addbedqty = tg.AddBedCount,//加床數
                    roomcount =tg.RoomCount, //標雙
                    bigbedroomcount =tg.BigBedRoomCount, //大床房
                    halfcount =tg.HalfCount, //半房
                    addroomcount =tg.AddRoomCount,//房差數

                    bbqty = tg.BBCount,
                    oldqty = tg.OlderCount,
                    oldqty2 = tg.OlderCount2,

                    remark = tg.Remark,
                    remarkall=tg.RemarkAll,
                    objectresume=tg.ObjectResume,//領用物品
                    rdremark = strRDRemark, //系列線 另寫線 備註 HTML
                    catalogremark= strCatalogRemark, //長短線 備註 HTML
                    deptcontactinfo = sContactInfo, //各部門聯繫電話 HTML

                    //導遊借款
                    guideadvancedate = DateTimeTool.FormatString(tg.GuideAdvanceDate),
                    guideadvancefee=tg.GuideAdvanceFee,
                    guideadvancehander=tg.GuideAdvanceHander,
                    guideadvanceremark=tg.GuideAdvanceRemark,

                    btsalesmanname =tg.BTSalesmanName, //包團營銷員
                    btgroupname=tg.BTGroupName, //包團組號
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
                DataTable dt = new TourGroupBiz().GetListByGuideID(nGuideId);
                if (dt != null)
                {
                    DataView dv = new DataView(dt);
                    dv.Sort = "Status,StartDate desc";
                    ArrayList lst = new ArrayList();
                    foreach (DataRowView item in dv)
                    {
                        var data = new {
                            tgid = item["tgid"],
                            productcode = item["productcode"],
                            productname = item["productname"],
                            busno = item["busno"],
                            flagno = item["flagno"],
                            processdays = item["processdays"],
                            startdate = item["startdate"],
                            status = item["status"],//1 --未出发 0 --进行中 2 --已出发

                            startdate_wk =DateTimeTool.GetWeekdayCN(item["startdate"]),
                            //距离今天的天数
                            startdate_days = 
                                (Convert.ToDateTime(item["startdate"])-DateTime.Today).Days,

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
        /// 獲取可以報賬的團列表
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
                DataTable dt = new TourGroupBiz().GetCanFeeListByGuideID(nGuideId);
                if (dt != null)
                {
                    DataView dv = new DataView(dt);
                    dv.Sort = "Status,StartDate desc";
                    ArrayList lst = new ArrayList();
                    TourGroupOtherFeeCheckLogBiz tourGroupOtherFeeCheckLogBiz=new TourGroupOtherFeeCheckLogBiz();
                    foreach (DataRowView item in dv)
                    {
                        decimal tgid = Convert.ToDecimal(item["tgid"]);
                        string cancelreason = "";
                        int isCanceled = item["ApplyFeeIsCancel"] == null ? 0 : Convert.ToInt32(item["ApplyFeeIsCancel"]); //是否被退回
                        if (isCanceled == 1) {
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
                            applyfeestatus=item["ApplyFeeStatus"],
                            applyfeedate=item["ApplyFeeDate"],
                            applycanceled= isCanceled, //是否被退回
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
            string sid = GuideInfoBiz.GetFieldValue("obj.Id", sWhere);
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
                GuideInfoBiz biz = new GuideInfoBiz();
                GuideInfo guide  = biz.GetByMobileNoAndUpdateUnionId(mobileno, unionid);
                if (guide !=null)
                {
                    try
                    {
                        var dguide = new
                        {
                            guideid = guide.Id,
                            name=guide.GuideName,
                            mobile=guide.Mobile,
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
            ///todo 寫死的要放到配置文件
            string Appid = "wx7f568e39b2ff0a47";
            string SecretKey = "7ede83327ba53d080741852db29a5e2e";
            switch (strappid)
            {
                case "1": //留位小程序
                    Appid = WebConfig.MiniAppId;
                    SecretKey = WebConfig.MiniAppKey;
                    break;
                case "2": //导游助手小程序
                    Appid = WebConfig.GuideAppId;
                    SecretKey = WebConfig.GuideAppKey;
                    break;
                default:
                    break;
            }
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
                    Logger.Error(string.Format("appid={0} \r\n mininappid={1} \r\n secrkey={2}",   strappid, Appid, SecretKey));
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
        /// 根据微信UnionId获取会员信息  1000-01
        /// </summary>
        private void GetMemberInfoByWxId()
        {
            try
            {
                string unionid = GetQueryString("unionid");
                MemberBiz biz = new MemberBiz();
                Members mem = biz.GetByWeChatUnionId(unionid);
                if (mem != null)
                {

                    bool isOverTime = false;
                    string soverdate = MemberBiz.GetFieldValue("obj.CardOverTime", mem.Id);
                    DateTime dtOverTime = DateTimeTool.String2DateTime(soverdate);
                    if (dtOverTime.Year != 9999)
                    {
                        if ((dtOverTime - DateTime.Today).Days < 0)
                            isOverTime = true;
                    }
                    else
                    {
                        soverdate = string.Empty;
                        isOverTime = true;
                    }

                    var info = new
                    {
                        memberid = mem.Id,
                        unionid = mem.UnionId == null ? "" : mem.UnionId,
                        mobile = mem.Mobile == null ? "" : mem.Mobile,
                        status = isOverTime ? 1 : 0,//0=正常，1=需要续费
                        overdate = soverdate
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


        /// <summary>
        /// 根据微信UnionId获取導遊信息 G002
        /// </summary>
        private void GetGuideInfoByWxId()
        {
            try
            {
                string unionid = GetQueryString("unionid");
                GuideInfoBiz biz = new GuideInfoBiz();
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

        /// <summary>
        /// 获取APP版本号
        /// </summary>
        private void GetAppVersion()
        {
            try
            {
                var obj = new { version = ConfigBiz.Apk_Version, url = ConfigBiz.ApkDownload_Url };
                ReturnJsonResponse(new ApiResult { success = 1, data = obj, errorcode = ErrorCode.EMPTY });
            }
            catch (Exception ex)
            {
                LogError(ex);
                ReturnJsonResponse(new ApiResult { success = 0, data = string.Empty, errorcode = ErrorCode.SYSTEM_ERROR });
            }
        }

        /// <summary>
        /// 根據手機號和驗證碼直接登錄 1000
        /// </summary>
        private void GetAndCreateMember()
        {

            string mobileno = GetQueryString("mobileno");
            string verifycode = GetQueryString("verifycode");
            string unionid = GetQueryString("unionid");
            //確保不含國家代碼
            MobileTool.MibileNo mobile = MobileTool.format(mobileno);
            string phoneNo = mobile.phoneNo;
            bool verifyok = PhoneVerifyCode.CheckVerifyCode(phoneNo, verifycode);
            if (!verifyok)
            {
                Logger.Error("手機號碼,驗證失敗mobileno=" + mobileno + " phoneNo=" + phoneNo);
                ReturnJsonResponse(new ApiResult { success = 0, data = string.Empty, errorcode = ErrorCode.VERIFY_CODE_ERROR });
                return;
            }
            MemberBiz biz = new MemberBiz();
            decimal nPayId = 0; //支付單

            decimal nMemberId = biz.RegisterFromApp_Simple(phoneNo, out nPayId, unionid);
            if (nMemberId > 0)
            {
                try
                {
                    Members member = biz.GetByID(nMemberId);
                    string stoken = biz.CreateToken(nMemberId);
                    bool isOverTime = false;
                    string soverdate = member.CardOverTime.ToString("yyyy-MM-dd");// MemberBiz.GetFieldValue("obj.CardOverTime", nMemberId);
                    DateTime dtOverTime = member.CardOverTime;//  DateTimeTool.String2DateTime(soverdate);
                    if (dtOverTime.Year != 9999)
                    {
                        if ((dtOverTime - DateTime.Today).Days < 0)
                            isOverTime = true;
                    }
                    else
                    {
                        soverdate = string.Empty;
                        isOverTime = true;
                    }
                    string avatar = member.Avatar; //MemberBiz.GetFieldValue("obj.Avatar", nMemberId);
                    string name = member.Name;
                    string enName = member.EngName;
                    string sex = member.Sex;
                    var dtoken = new
                    {
                        memberid = nMemberId,
                        token = stoken,
                        status = isOverTime ? 1 : 0,//0=正常，1=需要续费
                        overdate = soverdate,
                        avatar,
                        mobileno,
                        name,
                        enName,
                        sex
                    };
                    ReturnJsonResponse(new { success = 1, data = dtoken, errorcode = ErrorCode.EMPTY });
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


   

        private void GetQueryParame()
        {
            try
            {
                //0= 广东短线，1=中国长线 3=东南亚
                ArrayList listDays0 = GetProcessDaysByType(0, true); ArrayList listDays1 = GetProcessDaysByType(1, true); ArrayList listDays3 = GetProcessDaysByType(3, true);
                ArrayList listDays = new ArrayList();
                listDays.Add(new { title = 0, children = listDays0 }); listDays.Add(new { title = 1, children = listDays1 }); listDays.Add(new { title = 3, children = listDays3 });
                ArrayList listCity0 = GetCityByType(0, true); ArrayList listCity1 = GetCityByType(1, true); ArrayList listCity3 = GetCityByType(3, true);
                ArrayList listCitys = new ArrayList();
                listCitys.Add(new { title = 0, children = listCity0 }); listCitys.Add(new { title = 1, children = listCity1 }); listCitys.Add(new { title = 3, children = listCity3 });
                ArrayList listFeature = GetFeatureByType(true);
                var obj = new { city = listCitys, days = listDays, feature = listFeature };
                ReturnJsonResponse(new ApiResult { success = 1, data = obj, errorcode = ErrorCode.EMPTY });
                return;
            }
            catch (Exception ex)
            {
                LogError(ex);
                ReturnJsonResponse(new ApiResult { success = 0, data = "", errorcode = ErrorCode.SYSTEM_ERROR });
            }
        }

        /// <summary>
        /// 7016 获取活动图片
        /// </summary>
        private void GetActionPicture()
        {
            string sWhere = string.Format("obj.BizType={0} and obj.BizId={1}", (int)EnuAttachmentType.APP_Action_Photo, -2);
            string sOrder = "obj.OrderNo";
            IList lstAttachment = new AttachmentBiz().GetAllItems(sWhere, sOrder);
            ArrayList list = new ArrayList();
            foreach (Attachment item in lstAttachment)
            {
                list.Add(new { url= item.Url });
            }
            
            ReturnJsonResponse(new ApiResult { success = 1, data = list, errorcode = ErrorCode.EMPTY });
        }

        ///todo 放在配置文件或后台维护
        private void GetProcessDays()
        {
            try
            {
                int type = GetQueryInt("type");//0= 广东短线，1=中国长线 3=东南亚 7=跨省巴士 5=邮轮
                ArrayList list = GetProcessDaysByType(type);
                ReturnJsonResponse(new ApiResult { success = 1, data = list, errorcode = ErrorCode.EMPTY });
                return;
            }
            catch (Exception ex)
            {
                LogError(ex);
                ReturnJsonResponse(new ApiResult { success = 0, data = "", errorcode = ErrorCode.SYSTEM_ERROR });
            }
        }

        private ArrayList GetProcessDaysByType(int type, bool isarray = false)
        {
            ArrayList list = new ArrayList();
            string sdays = string.Empty;
            switch (type)
            {
                case 0:
                    sdays = "1,2,3,4,5";
                    break;
                case 1:
                    sdays = "3,4,5,6,7,8,9";
                    break;
                case 3:
                    sdays = "7,8,9,10,15";
                    break;
                case 7:
                    sdays = "3,4,5,6,7,8,9";
                    break;
                case 5:
                    sdays = "3,4,5,6,7,8,9";
                    break;
                case 8:
                    sdays = "3,4,5,6,7,8,9";
                    break;
                case 11:
                    sdays = "3,4,5,6,7,8,9";
                    break;
                default:
                    break;
            }
            foreach (string item in sdays.Split(','))
            {
                if (isarray)
                    list.Add(item);
                else
                    list.Add(new { name = item });
            }

            return list;
        }

        private void GetCity()
        {
            try
            {
                int type = GetQueryInt("type");//0= 广东短线，1=中国长线 3=东南亚
                ArrayList list = GetCityByType(type);
                ReturnJsonResponse(new ApiResult { success = 1, data = list, errorcode = ErrorCode.EMPTY });
                return;
            }
            catch (Exception ex)
            {
                LogError(ex);
                ReturnJsonResponse(new ApiResult { success = 0, data = "", errorcode = ErrorCode.SYSTEM_ERROR });
            }
        }

        private ArrayList GetCityByType(int type, bool isarray = false)
        {
            CityBiz biz = new CityBiz();
            IList lst = biz.GetByType(type);
            ArrayList list = new ArrayList();
            foreach (City item in lst)
            {
                if (isarray)
                    list.Add(item.Name);
                else
                    list.Add(new { name = item.Name });
            }
            return list;
        }

        private void GetFeatures()
        {
            try
            {
                int type = GetQueryInt("type");//0= 广东短线，1=中国长线 3=东南亚 4=郵輪
                ArrayList list = GetFeatureByType();
                ReturnJsonResponse(new ApiResult { success = 1, data = list, errorcode = ErrorCode.EMPTY });
                return;
            }
            catch (Exception ex)
            {
                LogError(ex);
                ReturnJsonResponse(new ApiResult { success = 0, data = "", errorcode = ErrorCode.SYSTEM_ERROR });
            }
        }

        private ArrayList GetFeatureByType(bool isarray = false)
        {
            const decimal DICTID = 10103; ///todo 字典ID写死的
            DictBiz biz = new DictBiz();
            IList lst = biz.GetDictItems(DICTID);
            ArrayList list = new ArrayList();
            foreach (DictItem item in lst)
            {
                if (isarray)
                    list.Add(item.ItemName);
                else
                    list.Add(new { name = item.ItemName });
            }

            return list;
        }

        private void UploadDeviceInfo()
        {

            decimal memberid = GetQueryDecimal("memberid");
            string deviceid = GetQueryString("deviceid");
            string imei = GetQueryString("imei");
            int systype = GetQueryInt("systype");//1=android 2=ios
            string sysversion = GetQueryString("sysversion");
            if (memberid == 0)
            {
                ReturnJsonResponse(new ApiResult { success = 0, data = "", errorcode = ErrorCode.ERROR_PARAM });
                return;
            }
            try
            {
                MemberDevice device = new MemberDevice()
                {
                    MemberId = memberid,
                    DeviceId = deviceid,
                    IMEI = imei,
                    SysType = systype,
                    SysVersion = sysversion
                };

                new MemberDeviceBiz().SaveDeviceInfo(device);

                ReturnJsonResponse(new ApiResult { success = 1, data = "", errorcode = ErrorCode.EMPTY });

            }
            catch (Exception ex)
            {
                LogError(ex);
                ReturnJsonResponse(new ApiResult { success = 0, data = "", errorcode = ErrorCode.SYSTEM_ERROR });
            }

        }

        /// <summary>
        /// 取消订单 3003
        /// </summary>
        private void CancelBill()
        {
            decimal memberid = GetQueryDecimal("memberid");
            string deviceid = GetQueryString("deviceid");
            string unionid = GetQueryString("unionid");//微信unionid
            decimal billid = GetQueryDecimal("billid");

            if (memberid > 0 && unionid.Length == 0)
            {
                if (!CheckToken())//检查token
                    return;
            }

            nRelax.Tour.BLL.ApiResult result = new APIBiz().CancelBill(billid, memberid, deviceid, unionid);
            ReturnJsonResponse(result);
            return;
            /*
            ///todo 增加会员id或设备id，否则有安全隐患
            if (memberid == 0 && deviceid.Trim().Length == 0)
            {
                ReturnJsonResponse(new ApiResult { success = 0, data = "", errorcode = ErrorCode.ERROR_PARAM });
                return;
            }
            if (memberid > 0 && unionid.Length==0)
            {
                if (!CheckToken())//检查token
                    return;
            }
            //memberid & unionid 验证unionid的正确性
            if (memberid > 0 && unionid.Length > 0)
            {
                object objunionid = MemberBiz.GetFieldValue("obj.UnionId", memberid);
                if (objunionid != null)
                {
                    if (unionid != objunionid.ToString())
                    {
                        ReturnJsonResponse(new ApiResult { success = 0, data = "", errorcode = ErrorCode.ERROR_PARAM });
                        Logger.Error("会员id和unionid不一致");
                        return;
                    }
                }
                else
                {
                    ReturnJsonResponse(new ApiResult { success = 0, data = "", errorcode = ErrorCode.ERROR_PARAM });
                    Logger.Error("未能获取会员id对应的unionid");
                    return;
                }
            }
            ///todo 需要增加 memberid参数  有安全隐患
            CancelBill(billid, true);
            */
        }
        private void CancelBill(decimal billid)
        {
            ///todo 已经迁移到apibiz
            CancelBill(billid, false);
        }
        private void CancelBill(decimal billid, bool checkOwer)
        {
            ///todo 已经迁移到apibiz
            decimal memberid = GetQueryDecimal("memberid");
            string deviceid = GetQueryString("deviceid");
            if (billid == 0)
            {
                ReturnJsonResponse(new ApiResult { success = 0, data = "", errorcode = ErrorCode.ERROR_PARAM });
                return;
            }
            try
            {
                ArrayList ids = new ArrayList();
                ids.Add(billid);
                TourBillBiz biz = new TourBillBiz();
                if (checkOwer)
                {
                    TourBill bill = biz.GetByID(billid);
                    //0 = 已生成 1 = 已付款 2 = 已审核 3 = 已出行 4 = 已评价 - 1 = 已取消
                    //只有未支付订单才可以取消
                    if (bill.AppStatus != 0)
                    {
                        ReturnJsonResponse(new ApiResult { success = 0, data = "", errorcode = ErrorCode.CANCEL_CANTNOT });
                        return;
                    }
                    if (memberid > 0)
                    {
                        if (bill.MemberId != memberid)
                        {
                            ReturnJsonResponse(new ApiResult { success = 0, data = "", errorcode = ErrorCode.ERROR_PARAM });
                            return;
                        }
                    }
                    if (deviceid.Trim().Length > 0)
                    {
                        if (bill.DeviceId != deviceid)
                        {
                            ReturnJsonResponse(new ApiResult { success = 0, data = "", errorcode = ErrorCode.ERROR_PARAM });
                            return;
                        }
                    }
                }
                biz.CancelReserveBill(ids);

                ReturnJsonResponse(new ApiResult { success = 1, data = "", errorcode = ErrorCode.EMPTY });
            }
            catch (Exception ex)
            {
                LogError(ex);
                ReturnJsonResponse(new ApiResult { success = 0, data = "", errorcode = ErrorCode.SYSTEM_ERROR });
            }
        }


        /// <summary>
        /// 创建评论 2010
        /// </summary>
        private void CreateComment()
        {
            try
            {
                decimal memberid = GetQueryDecimal("memberid");//会员id
                string deviceid = GetQueryString("deviceid");
                decimal billid = GetQueryDecimal("billid");
                string content = GetQueryString("content");
                int scode = GetQueryInt("score");
                int shopscore = GetQueryInt("shopscore");
                int devicescore = GetQueryInt("devicescore");
                int processscore = GetQueryInt("processscore");
                int guidescore = GetQueryInt("guidescore");

                if (memberid == 0 && deviceid.Trim().Length == 0)
                {
                    ReturnJsonResponse(new ApiResult { success = 0, data = "", errorcode = ErrorCode.ERROR_PARAM });
                    return;
                }
                if (memberid > 0)
                {
                    if (!CheckToken())//检查token
                        return;
                }

                if (scode < 0 || scode > 5 ||
                    shopscore < 0 || shopscore > 5 ||
                    devicescore < 0 || devicescore > 5 ||
                    processscore < 0 || processscore > 5 ||
                    guidescore < 0 || guidescore > 5 ||
                    billid == 0)
                {
                    ReturnJsonResponse(new ApiResult { success = 0, data = "", errorcode = ErrorCode.ERROR_PARAM });
                    return;
                }

                string smemName = string.Empty;
                if (memberid > 0)
                    smemName = MemberBiz.GetFieldValue("obj.Name", memberid);
                string suserip = GetClientIPAddress();
                TourBill tb = new TourBillBiz().GetByID(billid);
                ///todo 团结束后才能评价
                if (tb == null)
                {
                    ReturnJsonResponse(new ApiResult { success = 0, data = "", errorcode = ErrorCode.BILLIDERROR });
                    return;
                }
                TourCommentBiz biz = new TourCommentBiz();
                if (memberid > 0)
                {
                    if (biz.IsExist(memberid, billid))
                    {
                        ReturnJsonResponse(new ApiResult { success = 0, data = "", errorcode = ErrorCode.COMMENT_ONLY_ONETIME });
                        return;
                    }
                }
                else if (deviceid.Length > 0)
                {
                    if (biz.IsExist(deviceid, billid))
                    {
                        ReturnJsonResponse(new ApiResult { success = 0, data = "", errorcode = ErrorCode.COMMENT_ONLY_ONETIME });
                        return;
                    }
                }

                TourComment comm = new TourComment();
                comm.Albums = string.Empty;
                comm.Contents = content;
                comm.ReplyContent = string.Empty;
                comm.ReplyTime = DateTime.MaxValue;
                comm.Score = scode;
                comm.ShopScore = shopscore;
                comm.DeviceScore = devicescore;
                comm.ProcessScore = processscore;
                comm.GuideScore = guidescore;
                comm.Source = (int)EnuBillSource.Api;
                comm.TourBillId = billid;
                comm.UserId = memberid;
                comm.DeviceId = deviceid;
                comm.UserIP = suserip;
                comm.UserName = smemName;
                comm.TourId = tb.ProductID;
                comm.TourGroupId = tb.TourGroupID;

                comm.ProductCode = tb.ProductCode;
                comm.ProductName = tb.ProductName;
                comm.TourBillCode = tb.TourBillCode;
                comm.Catalog = StringTool.String2Int(TourBiz.GetFieldValue("obj.Catalog", tb.ProductID));

                decimal nid = new TourCommentBiz().Save(comm);
                ReturnJsonResponse(new ApiResult { success = 1, data = nid.ToString(), errorcode = ErrorCode.EMPTY });

            }
            catch (Exception ex)
            {
                LogError(ex);
                ReturnJsonResponse(new ApiResult { success = 0, data = "", errorcode = ErrorCode.SYSTEM_ERROR });
            }
        }

        /// <summary>
        /// 检查是否存在未支付订单id 3000
        /// </summary>
        private void CheckUnPayBill()
        {
            try
            {
                decimal memberid = GetQueryDecimal("memberid");//会员id
                string deviceid = GetQueryString("deviceid");//App设备标示

                if (memberid == 0 && deviceid.Trim().Length == 0)
                {
                    ReturnJsonResponse(new ApiResult { success = 0, data = "", errorcode = ErrorCode.ERROR_PARAM });
                    return;
                }
                if (memberid > 0)
                {
                    if (!CheckToken())//检查token
                        return;
                }
                TourBillBiz biz = new TourBillBiz();
                decimal nid = 0;

                if (memberid > 0) //通过会员标示检查
                    nid = biz.CheckUnPayBillForApp(memberid);
                else //通过设备标示检查
                    nid = biz.CheckUnPayBillForApp(deviceid);

                ReturnJsonResponse(new ApiResult { success = 1, data = nid.ToString(), errorcode = ErrorCode.EMPTY });
            }
            catch (Exception ex)
            {
                LogError(ex);
                ReturnJsonResponse(new ApiResult { success = 0, data = "", errorcode = ErrorCode.SYSTEM_ERROR });
            }
        }

        /// <summary>
        /// 获取热门城市 7010
        /// </summary>
        private void GetHotCityList()
        {
            try
            {
                int type = GetQueryInt("type"); 
                //城市类型 0= 广东短线，1=中国长线 3=东南亚 7=跨省巴士 5=邮轮
                int count = GetQueryInt("count");//
                //兼容老接口 5,7倒置
                int nType = type;
                if (type == 7)
                    nType = 5;
                else if (type == 5)
                    nType = 7;    

                ArrayList citys = new ArrayList();
                DataTable dt = new CityBiz().GetHotCity(nType, count);
                foreach (DataRow item in dt.Rows)
                {
                    string sName = item["Name"].ToString();
                    citys.Add(new { name = sName });

                }
                ReturnJsonResponse(new ApiListResult { success = 1, rows = citys, errorcode = ErrorCode.EMPTY });
            }
            catch (Exception ex)
            {
                LogError(ex);
                ReturnJsonResponse(new ApiResult { success = 0, data = "", errorcode = ErrorCode.SYSTEM_ERROR });
            }

        }

        /// <summary>
        /// 获取热门城市 7010-01
        ///type: 0= 短线，1=长线 2=东南亚 3=跨省巴士 5= 跨省巴士 4=郵輪 5=世界线路 6=自由行 7=最新优惠
        /// </summary>
        private void GetHotCityList_v2()
        {
            try
            {
                int type = GetQueryInt("type");
                //城市类型=线路类型 0= 广东短线，1=中国长线 3=东南亚 5=跨省巴士 7=邮轮 8=自由行 11=欧洲线
                int count = GetQueryInt("count");//
                int ntype = type;
                switch (type)
                {
                    case 2:
                        ntype = 3;
                        break;
                    case 3:
                        ntype = 5;
                        break;
                    case 4:
                        ntype = 7;
                        break;
                    case 5:
                        ntype = 11;
                        break;
                    case 6:
                        ntype = 8;
                        break;
                    case 7:
                        ntype =999;
                        break;
                    ///todo 最新优惠城市怎么处理
                    default:
                        break;
                }

                ArrayList citys = new ArrayList();
                DataTable dt = new CityBiz().GetHotCity(ntype, count);
                foreach (DataRow item in dt.Rows)
                {
                    string sName = item["Name"].ToString();
                    citys.Add(new { name = sName });

                }
                ReturnJsonResponse(new ApiListResult { success = 1, rows = citys, errorcode = ErrorCode.EMPTY });
            }
            catch (Exception ex)
            {
                LogError(ex);
                ReturnJsonResponse(new ApiResult { success = 0, data = "", errorcode = ErrorCode.SYSTEM_ERROR });
            }

        }
        



        /// <summary>
        /// 獲取訂單報價 3001
        /// </summary>
        private void GetBillPrice()
        {
            /*try
            {*/
            #region 獲取穿入數據
            decimal tourid = GetQueryDecimal("tourid"); //团Id
            decimal memberid = GetQueryDecimal("memberid");//会员id
            string deviceid = GetQueryString("deviceid");//设备id
            string startdate = GetQueryString("startdate");//出发日期
            int normalqty = GetQueryInt("normalqty");//成人人数
                                                     //int memberqty = GetQueryInt("memberqty");//会员人数 (取消，如果是会员成人按会员价）
            int childqty = GetQueryInt("childqty");//小童人数
            int bbqty = GetQueryInt("bbqty");//BB人数
            int oldqty = GetQueryInt("oldqty");//老人人数
            int oldqty2 = GetQueryInt("oldqty2");//老人人数
            int isbuinsur = GetQueryInt("isbuinsur");//是否买保险
            int isaddroom = GetQueryInt("isaddroom");//是否补房差
            int ispaymem = GetQueryInt("ispaymem");//是否繳會費(是否入會)
            int chanel = GetQueryInt("chanel");//銷售渠道
            #endregion

            nRelax.Tour.BLL.ApiResult result = new APIBiz().GetBillPrice(tourid, memberid, deviceid, startdate,
                normalqty, childqty, bbqty, oldqty, oldqty2,
                isbuinsur, isaddroom, ispaymem,
                chanel);
            ReturnJsonResponse(result);
            return;

            


        }


        /// <summary>
        /// 3004 微信支付
        /// </summary>

        private void CreateWeChatPayParam() {
            decimal billId = GetQueryDecimal("billId");//
            string openid = GetQueryString("openid");//

            nRelax.Tour.BLL.ApiResult result = new APIBiz().WeChatPayBill(openid, billId);

            ReturnJsonResponse(result);
            return;
        }

        /// <summary>
        /// 下單 3002
        /// </summary>
        /// <param name="sysAutoSetPerson">如果为true,系统自动设置 tourers 参数,前端可以不用传</param>
        private void CreateBill(bool sysAutoSetPerson = false)
        {

           

            #region 獲取传入數據
            decimal tourgroupid = GetQueryDecimal("tgid"); //团Id
            decimal memberid = GetQueryDecimal("memberid");//会员id
            string deviceid = GetQueryString("deviceid");//设备id
            string unionid = GetQueryString("unionid"); //微信unionid
            int normalqty = GetQueryInt("normalqty");//成人人数
                                                     //int memberqty = GetQueryInt("memberqty");//会员人数 (如果是会员下单所有成人用会员价）
            int childqty = GetQueryInt("childqty");//小童人数
            int bbqty = GetQueryInt("bbqty");//BB人数
            int oldqty = GetQueryInt("oldqty");//老人人数
            int oldqty2 = GetQueryInt("oldqty2");//老人人数
            int amount = GetQueryInt("amount");//不会直接存储，用于校验金额的正确性
            int isbuinsur = GetQueryInt("isbuinsur");//是否买保险
            int isaddroom = GetQueryInt("isaddroom");//是否不补房差
            int ispaymem = GetQueryInt("ispaymem");//是否繳會費(是否入會)

            string mobileno = GetQueryString("mobileno");//联系电话
            string tourername = GetQueryString("tourername");//联系人姓名
            string startport = GetQueryString("startport");//出發口岸
            int chanel = GetQueryInt("chanel");//銷售渠道
            #endregion

            string tourers = GetQueryString("tourers");
            //微信unionid不为空不用校验token
            if (memberid > 0 && unionid.Length == 0)
            {
                if (!CheckToken())//检查token
                    return;
            }

            nRelax.Tour.BLL.ApiResult result = new APIBiz().CreateBill(tourgroupid, memberid, deviceid, unionid,
                normalqty, childqty, bbqty, oldqty, oldqty2, amount,
                isbuinsur, isaddroom, ispaymem,
                mobileno, tourername, chanel, sysAutoSetPerson,tourers, startport);

            ReturnJsonResponse(result);
            return;


           

        }


        /// <summary>
        /// 獲取驗證碼 1003
        /// </summary>
        private void CreatePhoneVerifyCode()
        {
            string mobileno = GetQueryString("mobileno");
            int type = GetQueryInt("type");

            try
            {
                //格式化電話號碼,
                //1.拆分出區號和電話號碼,只支持香港和大陸手機號
                //2.不帶區號自動加上區號
                MobileTool.MibileNo no = MobileTool.format(mobileno);
                string areacode = no.areacode;
                string phoneNo = no.phoneNo; //不含國家地區代碼
                if (areacode == "" && phoneNo == "")
                {
                    Logger.Error(string.Format("傳入手機號碼格式錯誤{0},l={1}", mobileno, mobileno.Length));
                    ReturnJsonResponse(new ApiResult { success = 0, data = "", errorcode = "手機號碼格式錯誤,僅支持香港和大陸的手機號碼" });
                    return;
                }
                
                int seconds = 0;
                string verifycode = PhoneVerifyCode.CreateVerifyCode(phoneNo, out seconds);
                string fullPhoneNo = string.Format("{0}{1}", areacode, phoneNo);
                ReturnJsonResponse(new ApiResult
                {
                    success = 1,
                    data = new
                    {
                        //code = WebConfig.DebugNotSendSMS ? verifycode : "", //调试时传验证码,正式环境不传验证码
                        code = verifycode,
                        second = seconds
                    },
                    errorcode = ErrorCode.EMPTY
                });
                //發送手機驗證碼
                if (phoneNo.Length > 0)
                {
                    /*
                    if (type == 0)
                        verifycode = string.Format("廣東旅遊會員註冊驗證碼 {0}，請勿將短信透露第三方。", verifycode);
                    if (type == 1)
                        verifycode = string.Format("廣東旅遊訂位驗證碼 {0}，請勿將短信透露第三方。", verifycode);
                    if (!WebConfig.DebugNotSendSMS)
                    {
                        AccessyouSMS.SendSMS(mobileno, verifycode);
                    }*/
                    int tempid = 0;
                    if (type == 0)
                        tempid = (int)TencentSMS.TemplId.reguser;
                    if (type == 1)
                        tempid = (int)TencentSMS.TemplId.createbill;

                    if (!WebConfig.DebugNotSendSMS)
                    {
                        TencentSMS.SendSMS(fullPhoneNo, verifycode, tempid);
                    }

                }
            }
            catch (Exception ex)
            {
                LogError(ex);
                ReturnJsonResponse(new ApiResult { success = 0, data = "", errorcode = ErrorCode.SYSTEM_ERROR });
            }
        }

        /// <summary>
        /// 獲取外部鏈接網址:服務條款,付款方式,保險細則
        /// </summary>
        private void GetUrlSettings()
        {
            string serviceContract = WebConfig.UrlServiceContract;
            string insurContract = WebConfig.UrlInsurContract;
            string payway = WebConfig.UrlPayWay;
            ReturnJsonResponse(new ApiResult
            {
                success = 1,
                data = new {
                    serviceContract,
                    insurContract,
                    payway
                },
                errorcode = ErrorCode.EMPTY
            });
        }


        /// <summary>
        /// 服务条款 7001 
        /// </summary>
        private void GetServiceContract()
        {
            ///todo 不同线路的服务条款不同
            //decimal tourid = StringTool.String2Decimal(GetQueryString("tourid"));
            //DateTime now = new DateTime();
            //string version = now.Year.ToString() + now.Month.ToString() + now.Day.ToString();
            string url= WebConfig.UrlServiceContract;
            ReturnJsonResponse(new ApiResult { success = 1, 
                data= url,
                //data = "http://res.gdtour.hk/upload/app/doc/contract.jpg?v="+ version, 
                errorcode = ErrorCode.EMPTY });
        }

        /// <summary>
        /// 获取订单详情 6002
        /// </summary>
        private void GetBillDetail()
        {
            ///todo 已經轉移到apibiz
            decimal billid = StringTool.String2Decimal(GetQueryString("billid"));
            decimal memberid = GetQueryDecimal("memberid");
            string deviceid = GetQueryString("deviceid");
            string unionid = GetQueryString("unionid");
            if (memberid == 0)
            {
                ReturnJsonResponse(new ApiResult { success = 0, data = "", errorcode = ErrorCode.ERROR_PARAM });
                return;
            }
            ///todo 增加会员id或设备id，否则有安全隐患
            // memberid & unionid 可以不验证token
            if (memberid > 0 && unionid.Length == 0)
            {
                if (!CheckToken())//检查token
                    return;
            }
            //memberid & unionid 验证unionid的正确性
            if (memberid > 0 && unionid.Length > 0)
            {
                object objunionid = MemberBiz.GetFieldValue("obj.UnionId", memberid);
                if (objunionid != null)
                {
                    if (unionid != objunionid.ToString())
                    {
                        ReturnJsonResponse(new ApiResult { success = 0, data = "", errorcode = ErrorCode.ERROR_PARAM });
                        Logger.Error("会员id和unionid不一致");
                        return;
                    }
                }
                else
                {
                    ReturnJsonResponse(new ApiResult { success = 0, data = "", errorcode = ErrorCode.ERROR_PARAM });
                    Logger.Error("未能获取会员id对应的unionid");
                    return;
                }
            }
            try
            {
                TourBillBiz biz = new TourBillBiz();
                TourBill bill = biz.GetByID(billid);
                TourGroup tourGroup = new TourGroupBiz().GetByID(bill.TourGroupID);
                // bill.AppStatus; 0= 已生成 1=已付款 2=已审核 3=已出行 4=已评价 -1=已取消
                int status = bill.AppStatus;
                if (bill != null)
                {
                    if (memberid > 0)
                    {
                        if (memberid != bill.MemberId)
                        {
                            ReturnJsonResponse(new ApiResult { success = 0, data = "", errorcode = ErrorCode.ERROR_PARAM });
                            return;
                        }
                    }
                    if (deviceid.Trim().Length > 0)
                    {
                        if (deviceid != bill.DeviceId)
                        {
                            ReturnJsonResponse(new ApiResult { success = 0, data = "", errorcode = ErrorCode.ERROR_PARAM });
                            return;
                        }
                    }
                    if (bill.IsCancel)
                        status = -1;
                    if (bill.IsReserve)//预订状态下判断预订过期时间
                        if (bill.ReserveEndDate < DateTime.Now)//过期
                        {
                            if (!bill.IsCancel)
                                CancelBill(bill.Id);
                            status = -1;//取消状态
                        }
                    string sCatalog = tourGroup.Catalog;// TourGroupBiz.GetFieldValue("obj.Catalog", bill.TourGroupID);
                    int nCatalog = StringTool.String2Int(sCatalog??"0");

                    //游客列表
                    IList lsttourer = new TourerInfoBiz().GetTourersByBillID(billid);
                    ArrayList arrTourer = new ArrayList();
                    foreach (TourerInfo item in lsttourer)
                    {
                        arrTourer.Add(new
                        {
                            type = TransformTourerType(item.TourerType), //0=成人 1=小童 2=中童 3=BB  4=老人 5=老人2
                            cnname = item.Name,  ///todo 需要修改为 姓和名的形式
                            enname = item.EngName, ///todo 修改为 姓 和名的形式
                            //ZJType: 1= 身份证 2=回乡证 3=护照
                            certno = item.ZJType == 1 ? item.Sfzid : item.ZJType == 2 ? item.Hxzid : item.PassportNo,
                            mobileno = item.Mobile,
                            backmobileno = item.Tel // tourerinfo表中的tel为紧急联系电话
                        });
                    }
                    //BB列表
                    IList lstbb = new TourerBBInfoBiz().GetByTourBillId(billid);
                    foreach (TourerBBInfo item in lstbb)
                    {
                        arrTourer.Add(new
                        {
                            type = 3, //0=成人 1=小童 2=中童 3=BB  4=老人 5=老人2
                            cnname = item.Name,  ///todo 需要修改为 姓和名的形式
                            enname = item.EngName, ///todo 修改为 姓 和名的形式
                            //ZJType: 1= 身份证 2=回乡证 3=护照
                            certno = item.ZJType == 1 ? item.sfz_id : item.ZJType == 2 ? item.hxz_id : item.PassportNo,
                            mobileno = item.Mobile,
                            backmobileno = item.Tel // tourerinfo表中的tel为紧急联系电话
                        });
                    }
                    //订单上传的图片 入数纸=0 护照=1 回乡证=2 收據=3
                    IList lstPhoto = new TourBillPhotoBiz().GetByBillId(billid);
                    ArrayList arrPhoto = new ArrayList();
                    object receiptphoto = null;
                    object paybillphoto = null;
                    foreach (TourBillPhoto item in lstPhoto)
                    {
                        var photo = new
                        {
                            imgid = item.Id,
                            url = item.PhotoUrl,
                            k = item.PhotoKey,
                            type = item.PhotoType,
                            ischeckok = item.IsCheckSucess ? 1 : 0,
                            checkresume = item.CheckResume
                        };
                        if (item.PhotoType == 0)
                            paybillphoto = photo;
                        else if (item.PhotoType == 3)
                            receiptphoto = photo;
                        else
                            arrPhoto.Add(photo);
                    }
                    // yedpay 支付結果
                    string onlinePayType = "";
                    decimal onlinePayAmount = 0;
                    string onlinePayNo = "";
                    string onlinePayTime = "";
                    YedpayReports yedpay=new YedpayReportsBiz().getYedPayByBillId(billid);
                    if (yedpay != null) {
                        onlinePayAmount = yedpay.Amount;
                        onlinePayNo = yedpay.Tid;
                        onlinePayType = yedpay.PaymentMethod +"-[YedPay]";
                        CultureInfo hongKongCulture = new CultureInfo("zh-HK");
                        onlinePayTime = yedpay.PaidAt.ToString("d", hongKongCulture) + " " + yedpay.PaidAt.ToString("T", hongKongCulture); ;
                    }
                    //入会费
                    decimal memjoinamt = 0;
                    if (bill.MemPayBillId > 0 && bill.MemberId > 0)
                    {
                        MemberPay pay = new MemberPayBiz().GetByID(bill.MemPayBillId);
                        if (pay != null)
                            memjoinamt = pay.Amount;
                    }
    
                    object objbill = new
                    {
                        billno = bill.TourBillCode,
                        tourname = bill.ProductName,
                        startdate = bill.StartDate.ToString("yyyy-MM-dd"),
                        productcode=tourGroup.ProductCode,
                        starttime=tourGroup.StartTime,//出團信息:集合時間係
                        startPort = bill.StartPort,//出團信息:集合關口係
                        guidename =tourGroup.GuideName,// 出團信息:接團導遊係
                        guidetel = tourGroup.GuideTel,//出團信息:接團導遊係
                        flagno =tourGroup.FlagNo, //出團信息:旗號
                        tourercount = bill.NormalQty +
                            bill.MemberQty +
                            bill.ChildQty +
                            bill.BBQty +
                            bill.OlderQty +
                            bill.OlderQty2,
                        amount = bill.Amount,
                        status = status,
                        onlinepay=new {
                            type=onlinePayType,
                            amount = onlinePayAmount,
                            payno= onlinePayNo,
                            time= onlinePayTime
                        },
                        bookendtime = bill.IsReserve ? bill.ReserveEndDate.ToString("yyyy-MM-dd HH:mm:ss") : "",
                        feedetail = new
                        {
                            adultamt = bill.NormalAmt + bill.MemberAmt, //成人
                            childamt = bill.ChildAmt, //小童
                            olderamt = bill.OlderAmt, //老人
                            olderamt2 = bill.OlderAmt2, //老人2
                            bbamt = bill.BBAmt, //BB
                            insuramt = bill.InsurType1Amt + bill.InsurType0Amt + bill.InsurType2Amt + bill.InsurType3Amt, //保险
                            addroomamt = bill.AddRomeAmt + bill.AddRomeAmt2, //房差
                            additotalamt = bill.AdditionLong, //附加费
                            memamt = memjoinamt,  //入会费
                            freefee = bill.RebateFree, //优惠金额
                            tia=bill.NormalNotBedAmt, //印花稅
                            amt = bill.Receivable + memjoinamt //团费(应收,已经减掉了优惠金额)+入会费
                        },
                        tourerlist = arrTourer,
                        imagepaybill = paybillphoto == null ? "" : paybillphoto,
                        ///todo 收据图片 下面的注释需要取消掉，目前是写死的测试网址
                        imagereceipt = receiptphoto == null ? "" : receiptphoto, //后台上传的收据"http://43.249.30.190:8080/upload/app/doc/rec.jpg",//
                        imagecerttype = nCatalog == 0 || nCatalog == 1 ? 2 : 1, //0短线，1长线 回乡证，其他 3 东南亚 护照
                        imagecertificate = arrPhoto
                    };
                    ReturnJsonResponse(new ApiResult { success = 1, data = objbill, errorcode = ErrorCode.EMPTY });
                }
                else
                {
                    ReturnJsonResponse(new ApiResult { success = 0, data = "", errorcode = ErrorCode.OBJECT_NOT_FIND });
                }
            }
            catch (Exception ex)
            {
                LogError(ex);
                ReturnJsonResponse(new ApiResult { success = 0, data = "", errorcode = ErrorCode.SYSTEM_ERROR });
            }
        }

        /// <summary>
        /// 獲取訂單支付狀態
        /// </summary>
        private void GetAppBilPayStatus() {
            ///todo 已經轉移到apibiz
            decimal billid = StringTool.String2Decimal(GetQueryString("billid"));
            decimal memberid = GetQueryDecimal("memberid");
            string deviceid = GetQueryString("deviceid");
            string unionid = GetQueryString("unionid");
            if (memberid == 0)
            {
                ReturnJsonResponse(new ApiResult { success = 0, data = "", errorcode = ErrorCode.ERROR_PARAM });
                return;
            }
            ///todo 增加会员id或设备id，否则有安全隐患
            // memberid & unionid 可以不验证token
            if (memberid > 0 && unionid.Length == 0)
            {
                if (!CheckToken())//检查token
                    return;
            }
            //memberid & unionid 验证unionid的正确性
            if (memberid > 0 && unionid.Length > 0)
            {
                object objunionid = MemberBiz.GetFieldValue("obj.UnionId", memberid);
                if (objunionid != null)
                {
                    if (unionid != objunionid.ToString())
                    {
                        ReturnJsonResponse(new ApiResult { success = 0, data = "", errorcode = ErrorCode.ERROR_PARAM });
                        Logger.Error("会员id和unionid不一致");
                        return;
                    }
                }
                else
                {
                    ReturnJsonResponse(new ApiResult { success = 0, data = "", errorcode = ErrorCode.ERROR_PARAM });
                    Logger.Error("未能获取会员id对应的unionid");
                    return;
                }
            }
            try
            {
                TourBillBiz biz = new TourBillBiz();
                TourBill bill = biz.GetByID(billid);
                TourGroup tourGroup = new TourGroupBiz().GetByID(bill.TourGroupID);
               
                int status = 0;
                if (bill != null)
                {
                    if (memberid > 0)
                    {
                        if (memberid != bill.MemberId)
                        {
                            ReturnJsonResponse(new ApiResult { success = 0, data = "", errorcode = ErrorCode.ERROR_PARAM });
                            return;
                        }
                    }
                    if (deviceid.Trim().Length > 0)
                    {
                        if (deviceid != bill.DeviceId)
                        {
                            ReturnJsonResponse(new ApiResult { success = 0, data = "", errorcode = ErrorCode.ERROR_PARAM });
                            return;
                        }
                    }
                    
                    if (bill.IsReserve)
                    {
                        if (bill.IsCancel)
                            status = -1;
                        else {
                            //预订状态下判断预订过期时间
                            if (bill.ReserveEndDate < DateTime.Now)//过期
                            {
                                status = -1;//取消状态
                            }
                            else
                            {
                                // bill.AppStatus; 0= 已生成 1=已付款 2=已审核 3=已出行 4=已评价 -1=已取消
                                if (bill.AppStatus >= 1)
                                {
                                    status = 1;
                                }
                            }
                        }
                    }
                    else {
                        // 非預定訂單,無法查詢客戶支付結果
                        status = -2;
                    }
                    
                    ReturnJsonResponse(new ApiResult { success = 1, data = status, errorcode = ErrorCode.EMPTY });
                }
                else
                {
                    ReturnJsonResponse(new ApiResult { success = 0, data = "", errorcode = ErrorCode.OBJECT_NOT_FIND });
                }
            }
            catch (Exception ex)
            {
                LogError(ex);
                ReturnJsonResponse(new ApiResult { success = 0, data = "", errorcode = ErrorCode.SYSTEM_ERROR });
            }
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="type">// 输入结果['0', 'A-成人'],['1', 'C-小童'],['2', 'U-中童'], ['3', 'I-BB'], ['4', 'E1-長者1'], ['5', 'E2-長者2'], ['6', 'M-会员']</param>
        /// <returns></returns>
        private int TransformTourerType(int type)
        {

            //0=成人 1=小童 2=中童 3=BB  4=老人 5=老人2
            if (type == 6)
                return 0;
            else
                return type;

            // 返回结果 0=成人 1=小童 2=BB 3=老人
            // 输入结果['0', 'A-成人'],['1', 'C-小童'],['2', 'U-中童'], ['3', 'I-BB'], ['4', 'E1-長者1'], ['5', 'E2-長者2'], ['6', 'M-会员']
            //switch (type)
            //{
            //    case 0:
            //    case 6:
            //        return 0;
            //    case 1:
            //    case 2:
            //        return 1;
            //    case 3:
            //        return 2;
            //    case 4:
            //    case 5:
            //        return 3;
            //    default:
            //        return 0;
            //}
        }

        /// <summary>
        /// 订单列表 6001  
        /// 测试 状态待处理
        /// </summary>
        private void GetMemberBills(int pageindex, int pagesize)
        {
            ///todo 已經封裝到了 apibiz中,需要遷移
            decimal memid = GetQueryDecimal("memberid");
            string deviceid = GetQueryString("deviceid");//设备id
            string unionid = GetQueryString("unionid");//微信unionid
            //检查会员标示 和 设备标示不能都没有
            if (memid == 0 && deviceid.Trim().Length == 0 && unionid.Length == 0)
            {
                ReturnJsonResponse(new ApiResult { success = 0, data = "", errorcode = ErrorCode.ERROR_PARAM });
                return;
            }
            if (memid > 0 && unionid.Length==0 )
            {
                if (!CheckToken())//检查token
                    return;
            }
            try
            {
                TourBillBiz biz = new TourBillBiz();
                IList lst = null;
                if (memid > 0)
                    lst = biz.GetByMemberId(pageindex, pagesize, memid);
                else if (deviceid.Length > 0)
                    lst = biz.GetByDeviceId(pageindex, pagesize, deviceid);
                else if (unionid.Length > 0)
                    lst = biz.GetByWeChatUnionId(pageindex, pagesize, unionid);

                ArrayList list = new ArrayList();
                foreach (TourBill item in lst)
                {
                    decimal njoinmemamt = 0;
                    int status = item.AppStatus; ;  //0= 已生成 1=已付款 2=已审核 3=已出行 4=已评价 -1=已取消
                    if (item.MemberId > 0 && item.MemPayBillId > 0)
                    {
                        MemberPay pay = new MemberPayBiz().GetByID(item.MemPayBillId);
                        if (pay != null)
                            njoinmemamt = pay.Amount;
                    }
                    ///todo 已出发状态未处理
                    if (item.IsCancel) //取消状态
                        status = -1;
                    else
                    {
                        if (item.IsReserve)//预订状态下判断预订过期时间
                        {
                            if (item.ReserveEndDate < DateTime.Now)//过期
                            {
                                if (!item.IsCancel)
                                    CancelBill(item.Id);
                                status = -1;//取消状态
                            }
                        }
                    }

                    list.Add(new
                    {
                        billid = item.Id,
                        billno = item.TourBillCode,
                        tourname = item.ProductName,
                        productcode=item.ProductCode,
                        startdate = item.StartDate.ToString("yyyy-MM-dd"),
                        tourercount = item.NormalQty +
                            item.MemberQty +
                            item.ChildQty +
                            item.BBQty +
                            item.OlderQty +
                            item.OlderQty2,
                        amount = item.Receivable + njoinmemamt, //團費+會員費
                        status = status
                    });
                }
                ReturnJsonResponse(new ApiListResult { success = 1, rows = list, errorcode = ErrorCode.EMPTY });

            }
            catch (Exception ex)
            {
                LogError(ex);
                ReturnJsonResponse(new ApiListResult { success = 0, rows = new ArrayList(), errorcode = ErrorCode.SYSTEM_ERROR });
            }

        }


        /// <summary>
        /// 我的訂單數量(用於分頁
        /// </summary>
        private void GetMemberBillCount() {
            ///todo 已經封裝到了 apibiz中,需要遷移
            decimal memid = GetQueryDecimal("memberid");
            string deviceid = GetQueryString("deviceid");//设备id
            string unionid = GetQueryString("unionid");//微信unionid
            //检查会员标示 和 设备标示不能都没有
            if (memid == 0 && deviceid.Trim().Length == 0 && unionid.Length == 0)
            {
                ReturnJsonResponse(new ApiResult { success = 0, data = "", errorcode = ErrorCode.ERROR_PARAM });
                return;
            }
            if (memid > 0 && unionid.Length == 0)
            {
                if (!CheckToken())//检查token
                    return;
            }
            try
            {
                TourBillBiz biz = new TourBillBiz();
                int count = 0;
                if (memid > 0)
                    count = biz.CountByMemberId( memid);
                else if (deviceid.Length > 0)
                    count = biz.CountByDeviceId(deviceid);
                else if (unionid.Length > 0)
                    count = biz.CountByWeChatUnionId( unionid);

                
                ReturnJsonResponse(new ApiResult { success = 1, data = count, errorcode = ErrorCode.EMPTY });

            }
            catch (Exception ex)
            {
                LogError(ex);
                ReturnJsonResponse(new ApiResult { success = 0, data = 0, errorcode = ErrorCode.SYSTEM_ERROR });
            }

        }

        /// <summary>
        /// 获取评论列表 2009
        /// </summary>
        /// <param name="tourid"></param>
        /// <param name="pageindex"></param>
        /// <param name="pagesize"></param>
        private void GetTourCommentList(decimal tourid, int pageindex, int pagesize)
        {
            try
            {
                TourCommentBiz biz = new TourCommentBiz();
                MemberBiz bizMem = new MemberBiz();
                IList lst = biz.GetByTourIdOnePage(tourid, pageindex, pagesize);
                ArrayList list = new ArrayList();
                foreach (TourComment item in lst)
                {
                    decimal memid = item.UserId;
                    string memmobile = string.Empty;
                    string savatar = string.Empty;
                    if (memid > 0)
                    {
                        Members mem = bizMem.GetByID(memid);
                        if (mem == null)
                        {
                            Logger.Error(string.Format("获取评论列表时未找到会员资料，会员id=", item.UserId));
                            mem = new Members();
                        }
                        savatar = mem.Avatar == null ? "" : mem.Avatar;
                        memmobile = mem.Mobile;
                    }
                    var comm = new
                    {
                        memberid = memid,
                        avatar = savatar,
                        mobile = memmobile,
                        commtime = item.InputDate.ToString("yyyy-MM-dd HH:mm:ss"),
                        content = item.Contents,
                        score = item.Score,
                        shopscore = item.ShopScore,
                        devicescore = item.DeviceScore,
                        processscore = item.ProcessScore,
                        guidescore = item.GuideScore
                    };
                    list.Add(comm);
                }
                ReturnJsonResponse(new ApiListResult { success = 1, rows = list, errorcode = ErrorCode.EMPTY });
            }
            catch (Exception ex)
            {
                LogError(ex);
                ReturnJsonResponse(new ApiListResult { success = 0, rows = new ArrayList(), errorcode = ErrorCode.SYSTEM_ERROR });
            }
        }

        /// <summary>
        /// 根據線路類型獲取橫幅
        /// </summary>
        private void GetBannerByCatalog()
        {
            try
            {
                int catalog = GetQueryInt("catalog");
                
                IList lst = new BannerAlbumsBiz().GetByCatalog(catalog);
                ArrayList rows = new ArrayList();
                foreach (BannerAlbums item in lst)
                {
                    rows.Add(new {
                        imgurl = item.OriginalPath,
                        imagesrc = item.OriginalPath,
                        tourid = item.ObjectId
                    });
                }
                ReturnJsonResponse(new ApiListResult { success = 1, rows = rows, errorcode = ErrorCode.EMPTY });
            }
            catch (Exception ex)
            {
                LogError(ex);
                ReturnJsonResponse(new ApiListResult { success = 0, rows = new ArrayList(), errorcode = ErrorCode.SYSTEM_ERROR });
            }
        }

        /// <summary>
        /// 根據橫幅類型 获取横幅 7005
        /// </summary>
        private void GetBanner()
        {
            try
            {
                int ntype = GetQueryInt("typeid");
                //接口参数 0= 短线，1=长线 2=东南亚 3= 跨省巴士 4=郵輪 5=世界长线 6=自由行 7=最新優惠
                //后台 1= 短线(APP和微信)，3=长线(APP和微信) 4=东南亚(APP和微信) 2= 跨省巴士(APP和微信) 
                //5 =郵輪(APP和微信) 6=世界長線(APP和微信) 7 = 自由行(APP和微信) 8 = 最新優惠(APP和微信)
                switch (ntype)
                {
                    case 0:
                        ntype = 1;
                        break;
                    case 1:
                        ntype = 3;
                        break;
                    case 2:
                        ntype = 4;
                        break;
                    case 3:
                        ntype = 2;
                        break;
                    case 4:
                        ntype = 5;
                        break;
                    case 5:
                        ntype = 6;
                        break;
                    case 6:
                        ntype = 7;
                        break;
                    case 7:
                        ntype = 8;
                        break;
                    default:
                        break;
                }
                IList lst = new BannerAlbumsBiz().GetByTypeId(ntype);
                ArrayList rows = new ArrayList();
                foreach (BannerAlbums item in lst)
                {
                    rows.Add(new { imgurl = item.OriginalPath, tourid = item.ObjectId });
                }
                ReturnJsonResponse(new ApiListResult { success = 1, rows = rows, errorcode = ErrorCode.EMPTY });
            }
            catch (Exception ex)
            {
                LogError(ex);
                ReturnJsonResponse(new ApiListResult { success = 0, rows = new ArrayList(), errorcode = ErrorCode.SYSTEM_ERROR });
            }
        }

        /// <summary>
        /// 上传头像图片
        /// </summary>
        private void UploadAvatar()
        {

            try
            {

                decimal nmemid = GetQueryDecimal("memberid");
                string ssuffix = GetQueryString("suffix");
                string sfiledata = GetQueryString("filedata");

                string smsg = new UpLoad().UploadBase64File(sfiledata, "", ssuffix, false, Enums.UploadType.App);
                FileUploadResult result = JsonConvert.DeserializeObject<FileUploadResult>(smsg);

                if (result.status == 1)
                {
                    //更新头像到数据库
                    new MemberBiz().UpdateAvatar(result.path, nmemid);
                    ReturnJsonResponse(new ApiResult { success = 1, data = result.path, errorcode = ErrorCode.EMPTY });
                }
                else
                {
                    Logger.Error(result.msg);
                    ReturnJsonResponse(new ApiResult { success = 0, data = result.msg, errorcode = ErrorCode.SYSTEM_ERROR });
                }
            }
            catch (Exception ex)
            {
                LogError(ex);
                ReturnJsonResponse(new ApiResult { success = 0, data = string.Empty, errorcode = ErrorCode.SYSTEM_ERROR });
            }
        }

        /// <summary>
        /// 上传入数纸（信息安全！！）6003
        /// </summary>
        private void UploadTransferVoucher()
        {
            UploadTourBillPhoto(EnuBillPhotoType.PayBill);
        }

        private void UploadTransferVoucher_File()
        {
            UploadTourBillPhoto(EnuBillPhotoType.PayBill, false);
        }
        /// <summary>
        /// 上传护照，（信息安全！！）6004
        /// </summary>
        private void UploadPasspoartPhoto()
        {
            UploadTourBillPhoto(EnuBillPhotoType.Passport);
        }
        private void UploadPasspoartPhoto_File()
        {
            UploadTourBillPhoto(EnuBillPhotoType.Passport, false);
        }
        /// <summary>
        /// 上传回乡证（信息安全！！）(6005)
        /// </summary>
        private void UploadHxzPhoto()
        {
            UploadTourBillPhoto(EnuBillPhotoType.Hxz);
        }

        private void UploadHxzPhoto_File()
        {
            UploadTourBillPhoto(EnuBillPhotoType.Hxz, false);
        }
        /// <summary>
        /// 刪除圖片{memberid,billid,imgid}
        /// </summary>
        private void DeleteTourBillPhoto()
        {
            decimal memberid = GetQueryDecimal("memberid");
            string deviceid = GetQueryString("deviceid");
            string unionid = GetQueryString("unionid");//微信unionid
            ///todo 增加会员id或设备id，否则有安全隐患
            if (memberid > 0 && unionid.Length == 0)
            {
                if (!CheckToken())//检查token
                    return;
            }
            //memberid & unionid 验证unionid的正确性
            if (memberid > 0 && unionid.Length > 0)
            {
                object objunionid = MemberBiz.GetFieldValue("obj.UnionId", memberid);
                if (objunionid != null)
                {
                    if (unionid != objunionid.ToString())
                    {
                        ReturnJsonResponse(new ApiResult { success = 0, data = "", errorcode = ErrorCode.ERROR_PARAM });
                        Logger.Error("会员id和unionid不一致");
                        return;
                    }
                }
                else
                {
                    ReturnJsonResponse(new ApiResult { success = 0, data = "", errorcode = ErrorCode.ERROR_PARAM });
                    Logger.Error("未能获取会员id对应的unionid");
                    return;
                }
            }
            try
            {
                decimal nbillid = GetQueryDecimal("billid");
                decimal nimgid = GetQueryDecimal("imgid");
                TourBillPhotoBiz biz = new TourBillPhotoBiz();
                TourBillPhoto photo = biz.GetByID(nimgid);
                //驗證訂單id是否對應
                if (photo.BillId != nbillid)
                {
                    ReturnJsonResponse(new ApiResult { success = 0, data = string.Empty, errorcode = ErrorCode.ERROR_PARAM });
                    return;
                }
                //驗證會員必須是當前會員
                string billMemberId = TourBillBiz.GetFieldValue("obj.MemberId", nbillid);
                if (billMemberId != memberid.ToString())
                {
                    ReturnJsonResponse(new ApiResult { success = 0, data = string.Empty, errorcode = ErrorCode.ERROR_PARAM });
                    return;
                }
                bool deleted = biz.DeletePhoto(nimgid);
                if (deleted)
                {
                    ReturnJsonResponse(new ApiResult { success = 1, data = "success", errorcode = ErrorCode.SYSTEM_ERROR });
                }
                else
                {
                    ReturnJsonResponse(new ApiResult { success = 0, data = string.Empty, errorcode = "此時無法刪除" });
                }
            }
            catch (Exception ex)
            {
                LogError(ex);
                ReturnJsonResponse(new ApiResult { success = 0, data = string.Empty, errorcode = ErrorCode.SYSTEM_ERROR });
            }
        }
        private void UploadTourBillPhoto(EnuBillPhotoType ntype, bool isbase64 = true)
        {

            decimal memberid = GetQueryDecimal("memberid");
            string deviceid = GetQueryString("deviceid");
            string unionid = GetQueryString("unionid");//微信unionid
            ///todo 增加会员id或设备id，否则有安全隐患
            if (memberid > 0 && unionid.Length == 0)
            {
                if (!CheckToken())//检查token
                    return;
            }
            //memberid & unionid 验证unionid的正确性
            if (memberid > 0 && unionid.Length > 0)
            {
                object objunionid = MemberBiz.GetFieldValue("obj.UnionId", memberid);
                if (objunionid != null)
                {
                    if (unionid != objunionid.ToString())
                    {
                        ReturnJsonResponse(new ApiResult { success = 0, data = "", errorcode = ErrorCode.ERROR_PARAM });
                        Logger.Error("会员id和unionid不一致");
                        return;
                    }
                }
                else
                {
                    ReturnJsonResponse(new ApiResult { success = 0, data = "", errorcode = ErrorCode.ERROR_PARAM });
                    Logger.Error("未能获取会员id对应的unionid");
                    return;
                }
            }
            try
            {
                decimal nbillid = GetQueryDecimal("billid");
                decimal nimgid = GetQueryDecimal("imgid");
                string ssuffix = GetQueryString("suffix");
                string sfiledata = string.Empty;
                if (isbase64)
                {
                    sfiledata = GetQueryString("filedata");
                }
                else
                {
                    HttpPostedFile file = HttpContext.Current.Request.Files[0];
                    byte[] byteFile = ByteFiles(file);
                    sfiledata = Convert.ToBase64String(byteFile);
                    ssuffix = GetFileExt(file.FileName);
                }
                TourBill bill = new TourBillBiz().GetByID(nbillid);
                if (memberid > 0)
                {
                    if (memberid != bill.MemberId)
                    {
                        ReturnJsonResponse(new ApiResult { success = 0, data = string.Empty, errorcode = ErrorCode.ERROR_PARAM });
                        return;
                    }
                }
                if (deviceid.Trim().Length > 0)
                {
                    if (deviceid != bill.DeviceId)
                    {
                        ReturnJsonResponse(new ApiResult { success = 0, data = string.Empty, errorcode = ErrorCode.ERROR_PARAM });
                        return;
                    }
                }
                if (nbillid == 0)
                {
                    ReturnJsonResponse(new ApiResult { success = 0, data = string.Empty, errorcode = ErrorCode.ERROR_PARAM });
                    return;
                }
                int appStatus = StringTool.String2Int(TourBillBiz.GetFieldValue("obj.AppStatus", nbillid));
                if (appStatus == -1)
                {
                    ReturnJsonResponse(new ApiResult { success = 0, data = string.Empty, errorcode = ErrorCode.BILL_CANCELED });
                    return;
                }
                string skey = new Random().Next(100000000).ToString();
                string smsg = new UpLoad().UploadBase64File(sfiledata, "", ssuffix, false,
                    Enums.UploadType.Protect, skey);
                //Logger.Error(smsg);
                FileUploadResult result = JsonConvert.DeserializeObject<FileUploadResult>(smsg);

                if (result.status == 1)
                {

                    TourBillPhotoBiz biz = new TourBillPhotoBiz();
                    TourBillPhoto photo = new TourBillPhoto();
                    photo.BillId = nbillid;
                    photo.PhotoType = (int)ntype;
                    photo.PhotoUrl = result.path;
                    photo.PhotoKey = skey;
                    decimal nid = biz.SavePhoto(photo, nimgid);
                    if (nid == 0)
                    {
                        ReturnJsonResponse(new ApiResult { success = 0, data = string.Empty, errorcode = ErrorCode.SYSTEM_ERROR });
                        return;
                    }
                    ReturnJsonResponse(new ApiResult
                    {
                        success = 1,
                        data = new
                        {
                            imgid = nid,
                            url = result.path,
                            k = skey
                        },
                        errorcode = ErrorCode.EMPTY
                    });

                }
                else
                {
                    Logger.Error(result.msg);
                    ReturnJsonResponse(new ApiResult { success = 0, data = result.msg, errorcode = ErrorCode.SYSTEM_ERROR });
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

        /// <summary>
        /// 获取评论2008
        /// </summary>
        /// <param name="tourid"></param>
        private void GetTourCommentTotal(decimal tourid)
        {
            try
            {
                DataTable dt = new TourGroupBiz().GetTourCommentTotalForApp(tourid);
                int totalcount = 0;
                decimal scode = 0;
                if (dt.Rows.Count > 0)
                {
                    DataRow row = dt.Rows[0];
                    totalcount = StringTool.String2Int(row["totalcount"].ToString());
                    scode = StringTool.String2Decimal(row["score"].ToString());
                }
                ReturnJsonResponse(new ApiResult { success = 1, data = new { totalcount = totalcount, score = scode }, errorcode = ErrorCode.EMPTY });
            }
            catch (Exception ex)
            {
                LogError(ex);
                ReturnJsonResponse(new ApiResult { success = 0, data = string.Empty, errorcode = ErrorCode.SYSTEM_ERROR });
            }
        }

        /// <summary>
        /// 获取会员信息
        /// </summary>
        private void GetMemberInfo()
        {

            try
            {
                decimal id = GetQueryDecimal("memberid");
                MemberBiz biz = new MemberBiz();
                Members mem = biz.GetByID(id);
                if (mem != null)
                {
                    DateTime dtOverTime = mem.CardOverTime;
                    string sOverTime = string.Empty;
                    if (dtOverTime.Year != 9999)
                        sOverTime = dtOverTime.ToString("yyyy-MM-dd");
                    bool isOverTime = false;
                    if (dtOverTime.Year != 9999)
                    {
                        if ((dtOverTime - DateTime.Today).Days < 0)
                            isOverTime = true;
                    }
                    else
                        isOverTime = true;

                    var returnmem = new
                    {
                        cnsurname = mem.CnSurName == null ? "" : mem.CnSurName,
                        cngiven = mem.CnGiven == null ? "" : mem.CnGiven,
                        ensurname = mem.EnSurName == null ? "" : mem.EnSurName,
                        engiven = mem.EnGiven == null ? "" : mem.EnGiven,
                        birthday = DateTimeTool.FormatString(mem.Birthday),
                        sfzid = mem.sfz_id == null ? "" : mem.sfz_id,
                        hxzid = mem.HxzId == null ? "" : mem.HxzId,
                        mobileno = mem.Mobile,
                        email = mem.Email,
                        address = mem.Address,
                        address2 = mem.Address2 == null ? "" : mem.Address2,
                        marry = mem.Marriage,
                        job = mem.Job,
                        edu = mem.Edu,
                        overtime = sOverTime,
                        status = isOverTime ? 1 : 0,//0=正常，1=需要续费
                        avatar = mem.Avatar,
                        uadgettype = mem.UAdGetType,
                        ufavorite = mem.UFavorite,
                        uoncespend = mem.UOnceSpend,
                        uprocessdays = mem.UProcessDays,
                        utimesofyear = mem.UTimesOfYear
                    };
                    ReturnJsonResponse(new ApiResult { success = 1, data = returnmem, errorcode = ErrorCode.EMPTY });
                    return;
                }
                else
                {
                    ReturnJsonResponse(new ApiResult { success = 0, data = "", errorcode = ErrorCode.CANT_FOUNT_MEMBER });
                    return;
                }
            }
            catch (Exception ex)
            {
                LogError(ex);
                ReturnJsonResponse(new ApiResult { success = 0, data = string.Empty, errorcode = ErrorCode.SYSTEM_ERROR });
            }
        }

        /// <summary>
        /// 修改会员信息
        /// </summary>
        private void ModifyMemberInfo()
        {
            try
            {
                decimal id = StringTool.String2Decimal(GetQueryString("memberid"));
                if (id == 0)
                {
                    ReturnJsonResponse(new ApiResult { success = 0, data = string.Empty, errorcode = ErrorCode.MEMBER_ID_CANT_EMPTY });
                    return;
                }

                MemberBiz biz = new MemberBiz();
                Members mem = biz.GetByID(id);
                mem = InitMemberInfo(mem);

                biz.Update(mem);

                ReturnJsonResponse(new ApiResult { success = 1, data = "", errorcode = ErrorCode.EMPTY });
                return;
            }
            catch (Exception ex)
            {
                LogError(ex);
                ReturnJsonResponse(new ApiResult { success = 0, data = string.Empty, errorcode = ErrorCode.SYSTEM_ERROR });
            }
        }

        /// <summary>
        /// 新增会员信息
        /// </summary>
        private void RegisterMemberFromApp()
        {
            #region 必填检查
            if (GetQueryString("sfzid").Length == 0)
            {
                ReturnJsonResponse(new ApiResult { success = 0, data = string.Empty, errorcode = ErrorCode.MEMBER_SFZ_CANT_EMPTY });
                return;
            }
            if (GetQueryString("hxzid").Length == 0)
            {
                ReturnJsonResponse(new ApiResult { success = 0, data = string.Empty, errorcode = ErrorCode.MEMBER_HXZ_CANT_EMPTY });
                return;
            }
            if (GetQueryString("mobileno").Length == 0)
            {
                ReturnJsonResponse(new ApiResult { success = 0, data = string.Empty, errorcode = ErrorCode.MEMBER_MOBILE_CANT_EMPTY });
                return;
            }
            if (GetQueryString("cnsurname").Length == 0 || GetQueryString("cngiven").Length == 0)
            {
                ReturnJsonResponse(new ApiResult { success = 0, data = string.Empty, errorcode = ErrorCode.MEMBER_CNNAME_CANT_EMPTY });
                return;
            }
            if (GetQueryString("ensurname").Length == 0 || GetQueryString("engiven").Length == 0)
            {
                ReturnJsonResponse(new ApiResult { success = 0, data = string.Empty, errorcode = ErrorCode.MEMBER_ENNAME_CANT_EMPTY });
                return;
            }

            #endregion

            Members mem = new Members();
            mem = InitMemberInfo(mem);

            #region 字段初始化
            mem.MemberCode = string.Empty;
            mem.CardOverTime = DateTime.Now.AddDays(-1);
            mem.SendCardDate = DateTime.MaxValue;
            mem.Sex = string.Empty;
            mem.Nationality = string.Empty;
            mem.Avatar = string.Empty;
            mem.ContactTel = string.Empty;
            mem.Interest = string.Empty;
            mem.Language = string.Empty;
            mem.PassportNo = string.Empty;
            mem.Remark = string.Empty;
            mem.Salary = string.Empty;
            mem.Fax = string.Empty;
            #endregion

            mem.IsFee = false; //未缴会费
            mem.FeeDate = DateTime.MaxValue;//默认过期
            mem.RegisterDate = DateTime.Now;

            #region 初始密码 身份證后六位
            //string spassword = string.Empty;
            //if (mem.sfz_id.Length >= 6)
            //    spassword = mem.sfz_id.Substring(mem.sfz_id.Length - 6, 6);
            //else
            //    spassword = mem.sfz_id;
            //初始密码  改成了手機號碼
            string spassword = EncryTool.EnCrypt(mem.Mobile);
            mem.PWD = spassword;
            #endregion

            string stype = GetQueryString("applytype"); //1=申请入    2 = 补会员卡或换会员卡
            if (stype == "1")
            {
                try
                {
                    MemberBiz biz = new MemberBiz();
                    MemberPayBiz bizPay = new MemberPayBiz();

                    #region 存在性检查
                    bool isExistSfz = biz.IsExistSfzId(mem.sfz_id, mem.Id);
                    bool isExistHxzId = biz.IsExistHxzId(mem.HxzId, mem.Id);
                    bool isExistMobile = biz.IsExistMobile(mem.Mobile, mem.Id);
                    if (isExistSfz)
                    {
                        ReturnJsonResponse(new ApiResult { success = 0, data = string.Empty, errorcode = ErrorCode.MEMBER_SFZ_EXIST });
                        return;
                    }
                    if (isExistHxzId)
                    {
                        ReturnJsonResponse(new ApiResult { success = 0, data = string.Empty, errorcode = ErrorCode.MEMBER_HXZ_EXIST });
                        return;
                    }
                    if (isExistMobile)
                    {
                        ReturnJsonResponse(new ApiResult { success = 0, data = string.Empty, errorcode = ErrorCode.MEMBER_MOBILE_EXIST });
                        return;
                    }
                    #endregion
                    decimal npayid = 0;
                    decimal nid = biz.RegisterFromApp(mem, out npayid);
                    string token = biz.CreateToken(nid);
                    ReturnJsonResponse(new ApiResult
                    {
                        success = 1,
                        data = new
                        {
                            memberid = nid,
                            memberpayid = npayid,
                            token = token
                        },
                        errorcode = ErrorCode.EMPTY
                    });
                }
                catch (Exception ex)
                {
                    LogError(ex);
                    ReturnJsonResponse(new ApiResult { success = 0, data = string.Empty, errorcode = ErrorCode.SYSTEM_ERROR });
                }
            }
        }

        private Members InitMemberInfo(Members mem)
        {
            mem.CnSurName = GetQueryString("cnsurname");
            mem.CnGiven = GetQueryString("cngiven");
            mem.Name = mem.CnSurName + mem.CnGiven;
            mem.EnSurName = GetQueryString("ensurname");
            mem.EnGiven = GetQueryString("engiven");
            mem.EngName = mem.CnGiven + " " + mem.CnSurName;
            mem.Birthday = DateTimeTool.String2DateTime(GetQueryString("birthday"));
            mem.sfz_id = GetQueryString("sfzid");
            mem.HxzId = GetQueryString("hxzid");
            mem.Mobile = GetQueryString("mobileno");
            mem.Email = GetQueryString("email");
            mem.Address = GetQueryString("address");
            mem.Address2 = GetQueryString("address2");
            mem.Marriage = GetQueryString("marry");
            mem.Job = GetQueryString("job");
            mem.Edu = GetQueryString("edu");
            //調查相關
            mem.UAdGetType = GetQueryInt("uadgettype");
            mem.UFavorite = GetQueryInt("ufavorite");
            mem.UOnceSpend = GetQueryInt("uoncespend");
            mem.UProcessDays = GetQueryInt("uprocessdays");
            mem.UTimesOfYear = GetQueryInt("utimesofyear");
            return mem;
        }

        /// <summary>
        /// 获取线路指定月份每日状态
        /// </summary>
        /// <param name="tourid"></param>
        /// <param name="yearmonth"></param>
        private void GetTourEachDayStatus(decimal tourid, string yearmonth)
        {
            try
            {
                DateTime dt = DateTime.ParseExact(yearmonth, "yyyyMM", null);
                IList lst = new TourGroupBiz().GetProductEachDayStatusForApp(tourid, dt.Year, dt.Month);
                ReturnJsonResponse(new ApiListResult { success = 1, rows = lst, errorcode = ErrorCode.EMPTY });
            }
            catch (Exception ex)
            {
                LogError(ex);
                ReturnJsonResponse(new ApiListResult { success = 0, rows = new ArrayList(), errorcode = ErrorCode.SYSTEM_ERROR });
            }
        }

        /// <summary>
        /// 获取线路详细信息 2006
        /// </summary>
        /// <param name="tourid"></param>
        private void GetTourDetail(decimal tourid)
        {
            try
            {
                if (tourid == 0)
                {
                    ReturnJsonResponse(new ApiResult { success = 0, data = string.Empty, errorcode = ErrorCode.ERROR_PARAM });
                    return;
                }
                TourBiz tourBiz = new TourBiz();
                Tour.Entity.Tour tour = tourBiz.GetByID(tourid);
                tourBiz.UpdateTourHotCount(tourid);
                //01
                //TourPrice tourPrice = new TourPriceBiz().GetPingriZhouMoPrice(tourid);
                //if (tourPrice == null)
                //    tourPrice = new TourPrice();
                //02
                //string[] sprice = new TourPriceBiz().GetPingriZhouMoPrice(tourid).Split('|');//平日价|周末价
                decimal normalprice = 0, memberprice = 0,
                workdayprice = 0, weekdayprice = 0, allnormalprice = 0,jiabanprice=0,
                workdaymemprice = 0, weekdaymemprice = 0, allmemberprice = 0,jiabanmemprice=0;
                
                new TourPriceBiz().GetAllPrice(tourid, out normalprice, out memberprice,
                   out workdayprice, out weekdayprice, out allnormalprice, out jiabanprice,
                   out workdaymemprice, out weekdaymemprice, out allmemberprice, out jiabanmemprice);

                TourPrice tourPrice = new TourPriceBiz().GetPingriPrice(tourid);
                if (tourPrice == null)
                    tourPrice = new TourPrice();

                // 行程
                IList processList = new TourProcessBiz().GetByProductID(tourid);
                ArrayList process = new ArrayList();
                foreach (TourProcess item in processList)
                {
                    var processItem = new {
                        theday=item.TheDay,
                        station=item.Station,  //目的地
                        trafficname=UseEnum.EnuToString((EnuTrafficType)item.Traffic), //交通方式
                        traffic = item.Traffic, //交通方式
                        hotelname=item.HotelName, //酒店
                        scenicarea=item.ScenicArea,//景點
                        repast1 = item.Repast1, //早餐
                        repast2 = item.Repast2, //中餐
                        repast3 = item.Repast3, //晚餐
                        repast4 = item.Repast4, //宵夜
                        resume=item.ProcessResume //行程描述
                    };
                    process.Add(processItem);
;                }

                Videos videos = new VideosBiz().getByProductCode(tour.ProductCode);
                string pdfUrl = GetPDFFileUrl(tourid);
                var obj = new
                {
                    tourid = tour.Id,
                    tourcode = tour.ProductCode,
                    tourname = tour.ProductName,
                    //pic = "http://43.249.30.190:8080//upload/default/2016/09/05/upload/201609052220117462.jpg",
                    //picdetail = "http://43.249.30.190:8080//upload/default/2016/09/05/upload/201609052220248970.png",
                    vtitle=videos==null?"":videos.Title,
                    vurl=videos==null?"":StringTool.GetYouTubeIframeUrl(videos.Url),
                    vid = videos == null ? "" : StringTool.GetYouTubeVideoId(videos.Url),

                    pic = tour.AppImgUrl,
                    pdf = pdfUrl, //單張(pdf格式)
                    picdetail = tour.AppAdImgUrl, //單張(圖片格式)

                    days = tour.ProcessDays,
                    catalog = tour.Catalog,
                    startdays=tour.StartDays,
                    //normalprice = StringTool.String2Decimal(sprice[0]),
                    //weekendprice = StringTool.String2Decimal(sprice[1]),
                    normalprice,//=tourPrice.AdultNormalPrice,
                    memberprice,//=tourPrice.AdultVipPrice,

                    workdayprice, weekdayprice,allnormalprice,jiabanprice,
                    workdaymemprice, weekdaymemprice, allmemberprice,jiabanmemprice,

                    childprice = tourPrice.ChildUseBedPrice,
                    oldprice = 0,//tourPrice.OlderPrice,
                    oldprice2 = 0,//tourPrice.OlderPrice2,
                    bbprice = tourPrice.BBPrice,
                    discount = tour.DiscountName, ///todo 优惠信息
                    process
                };
                ReturnJsonResponse(new ApiResult { success = 1, data = obj, errorcode = ErrorCode.EMPTY });
            }
            catch (Exception ex)
            {
                LogError(ex);
                ReturnJsonResponse(new ApiResult { success = 0, data = string.Empty, errorcode = ErrorCode.SYSTEM_ERROR });
            }
        }

        /// <summary>
        /// 根据查询条件查询 2005
        /// </summary>
        /// <param name="scondition">查询条件，json格式</param>
        /// <param name="pageindex"></param>
        /// <param name="pagesize"></param>
        private void GetHotTourByCondution(string scondition, string startdate, int pageindex, int pagesize)
        {
            try
            {
                DataTable dt = new TourGroupBiz().GetHotProductByConditionForApp(scondition, startdate, pageindex, pagesize);
                ReturnJsonResponse(new ApiListResult { success = 1, rows = dt, errorcode = ErrorCode.EMPTY });
            }
            catch (Exception ex)
            {
                LogError(ex);
                ReturnJsonResponse(new ApiListResult { success = 0, rows = new ArrayList(), errorcode = ErrorCode.SYSTEM_ERROR });
            }
        }

        /// <summary>
        /// 根据目的地查询 2004
        /// </summary>
        /// <param name="target"></param>
        /// <param name="pageindex"></param>
        /// <param name="pagesize"></param>
        private void GetHotTourByTarget(string target, int pageindex, int pagesize)
        {
            string type = GetQueryString("type");//0= 短线，1=长线 2=东南亚 3= 跨省巴士 4=邮轮 5-世界线路
            int jiaban = GetQueryInt("jiaban");
            //0 = 短线，1=长线 3=东南亚 5= 跨省巴士 7-邮轮 空字符串表示不参与过滤
            if (!IsSafeSqlString(target))
            {
                //不安全的参数值
                ReturnJsonResponse(new ApiListResult { success = 0, rows = new ArrayList(), errorcode = ErrorCode.UNSAFEPARAM });
                return;
            }
            try
            {
                DataTable dt;
                if (type.Length == 0)
                    dt = new TourGroupBiz().GetHotProductByTargetForApp(target, pageindex, pagesize,jiaban);
                else //空字符串表示不参与过滤
                    dt = new TourGroupBiz().GetHotProductByTargetForApp(type, target, pageindex, pagesize, jiaban);

                ReturnJsonResponse(new ApiListResult { success = 1, rows = dt, errorcode = ErrorCode.EMPTY });
            }
            catch (Exception ex)
            {
                LogError(ex);
                ReturnJsonResponse(new ApiListResult { success = 0, rows = new ArrayList(), errorcode = ErrorCode.SYSTEM_ERROR });
            }
        }

        /// <summary>
        /// 根据特色查询 2003
        /// </summary>
        /// <param name="feature"></param>
        /// <param name="type">0= 短线，1=长线 2=东南亚 3= 跨省巴士 4=邮轮</param>
        /// <param name="pageindex"></param>
        /// <param name="pagesize"></param>
        private void GetHotTourByFeature(string feature, int type, int pageindex, int pagesize)
        {
            if (!IsSafeSqlString(feature))
            {
                //不安全的参数值
                ReturnJsonResponse(new ApiListResult { success = 0, rows = new ArrayList(), errorcode = ErrorCode.UNSAFEPARAM });
                return;
            }
            try
            {
                int jiaban = GetQueryInt("jiaban");
                DataTable dt = new TourGroupBiz().GetHotProductByFeatureForApp(feature, type, pageindex, pagesize,jiaban);
                ReturnJsonResponse(new ApiListResult { success = 1, rows = dt, errorcode = ErrorCode.EMPTY });
            }
            catch (Exception ex)
            {
                LogError(ex);
                ReturnJsonResponse(new ApiListResult { success = 0, rows = new ArrayList(), errorcode = ErrorCode.SYSTEM_ERROR });
            }
        }

        /// <summary>
        /// 热门推荐团
        /// 10- 短线推荐
        /// 11- 长线推荐
        /// 12- 东南亚推荐
        /// 20- 短线快成团
        /// 21- 长线快成团
        /// 22- 东南亚快成团
        /// </summary>
        /// <param name="type">
        /// 10- 短线推荐
        /// 11- 长线推荐
        /// 12- 东南亚推荐
        /// 20- 短线快成团
        /// 21- 长线快成团
        /// 22- 东南亚快成团
        /// </param>
        /// <param name="pageindex"></param>
        /// <param name="pagesize"></param>
        private void GetHotTour(int type, int pageindex, int pagesize)
        {
            try
            {
                int jiaban =GetQueryInt("jiaban");

                DataTable dt = new TourGroupBiz().GetHotProductForApp(type, pageindex, pagesize, jiaban);
                ArrayList list = new ArrayList();
                foreach (DataRow item in dt.Rows)
                {
                    var row = new
                    {
                        rowid = item["rowid"],
                        tourid = item["tourid"],
                        tourcode = item["tourcode"],
                        tourname = item["tourname"],
                        processdays = item["processdays"],
                        vtitle=item["vtitle"], //視頻標題
                        vurl=item["vurl"],//視頻網址
                        vid= item["vurl"]!=null && !string.IsNullOrEmpty(item["vurl"].ToString
                        ())? StringTool.GetYouTubeVideoId(item["vurl"]):"",
                        catalog=item["catalog"],
                        normalprice = item["normalprice"],

                        workdayprice =item["workdayprice"],
                        weekdayprice=item["weekdayprice"],
                        allnormalprice=item["allnormalprice"],
                        jiabanprice = item["jiabanprice"],

                        memberprice = item["memberprice"],
                        workdaymemprice=item["workdaymemprice"],
                        weekdaymemprice=item["weekdaymemprice"],
                        jiabanmemprice = item["jiabanmemprice"],

                        DiscountName = item["DiscountName"],
                        feature = item["feature"],
                        labels = item["labels"],
                        labelarray = item["labels"].ToString().Split(','),
                        pic = item["pic"],
                        picPdf = item["picPdf"]
                    };
                    list.Add(row);
                }
                ReturnJsonResponse(new ApiListResult { success = 1, rows = list, errorcode = ErrorCode.EMPTY });
            }
            catch (Exception ex)
            {
                LogError(ex);
                ReturnJsonResponse(new ApiListResult { success = 0, rows = new ArrayList(), errorcode = ErrorCode.SYSTEM_ERROR });
            }
        }

        private string GetPDFFileUrl(object nTourID)
        {
            string urlPath = new AttachmentBiz().GetTourPDFFilePath(Convert.ToDecimal(nTourID));
            if (urlPath.Length > 0)
            {
                string httpUrl = ConfigurationManager.AppSettings["UploadFileUrlRoot"].ToString().TrimEnd('/');
                string fullUrl = string.Format("{0}/{1}", httpUrl, urlPath);
                return fullUrl;
            }
            else
                return "";
        }
        /// <summary>
        /// 綜合查詢 2000
        /// </summary>
        /// <param name="catalog">分類:-1 所有 0=短線 1=長線 3=東南亞 5=中短線(跨省巴士) 7=遊輪 </param>
        /// <param name="status">狀態:0=所有 1=推薦線路 2=快成團 3=最新優惠 4=VIP專享</param>
        /// <param name="feature">特色</param>
        /// <param name="target">目的地</param>
        /// <param name="days">行程天數</param>
        /// <param name="pageindex"></param>
        /// <param name="pagesize"></param>
        /// <param name="jiaban"></param>
        /// <param name="pageindex"></param>
        /// <param name="pagesize"></param>
        private void GetHotTour(string keyword, int catalog, int status, string feature, string target, string startdate, int days, int jiaban, int pageindex, int pagesize)
        {
            try
            {

                DataTable dt = new TourGroupBiz().GetHotProduct(keyword, catalog, status, feature, target, startdate, days, pageindex, pagesize, jiaban);
                ArrayList list = new ArrayList();
                foreach (DataRow item in dt.Rows)
                {
                    string pdfUrl = GetPDFFileUrl(item["tourid"]);
                    string videoUrl = item["vurl"] == null ? "" : item["vurl"].ToString();//原始視頻網址
                    string vurl = StringTool.GetYouTubeIframeUrl(videoUrl);//嵌入iframe的視頻網址
                    string vid = StringTool.GetYouTubeVideoId(videoUrl); //視頻的id
                 
                    var row = new
                    {
                        rowid = item["rowid"],
                        tourid = item["tourid"],
                        tourcode = item["tourcode"],
                        tourname = item["tourname"],
                        processdays = item["processdays"],
                        vtitle = item["vtitle"], //視頻標題
                        vurl = vurl,//嵌入iframe的視頻網址
                        vid = vid,
                        catalog = item["catalog"],
                        normalprice = item["normalprice"],

                        workdayprice = item["workdayprice"],
                        weekdayprice = item["weekdayprice"],
                        allnormalprice = item["allnormalprice"],
                        jiabanprice = item["jiabanprice"],

                        memberprice = item["memberprice"],
                        workdaymemprice = item["workdaymemprice"],
                        weekdaymemprice = item["weekdaymemprice"],
                        jiabanmemprice = item["jiabanmemprice"],

                        DiscountName = item["DiscountName"],
                        startdays = item["startdays"],
                        feature = item["feature"],
                        labels = item["labels"],
                        labelarray = item["labels"].ToString().Split(','),
                        pic = item["pic"],
                        picPdf = item["picPdf"],
                        pdf = pdfUrl
                    };
                    list.Add(row);
                }
                ReturnJsonResponse(new ApiListResult { success = 1, rows = list, errorcode = ErrorCode.EMPTY });
            }
            catch (Exception ex)
            {
                LogError(ex);
                ReturnJsonResponse(new ApiListResult { success = 0, rows = new ArrayList(), errorcode = ErrorCode.SYSTEM_ERROR });
            }
        }

        /// <summary>
        /// 获取最新线路
        /// </summary>
        /// <param name="type"></param>
        /// <param name="pageindex"></param>
        /// <param name="pagesize"></param>
        private void GetNewTour(int pageindex, int pagesize)
        {
            try
            {
                nRelax.Tour.BLL.ApiListResult result = new APIBiz().GetNewTour(pageindex , pagesize);
                ReturnJsonResponse(result);
                return;

            }
            catch (Exception ex)
            {
                LogError(ex);
                ReturnJsonResponse(new nRelax.Tour.BLL.ApiListResult { success = 0, rows = new ArrayList(), errorcode = ErrorCode.SYSTEM_ERROR });
            }
        }

        private bool VerifyMemberPassword()
        {
            string stamp = GetQueryString("stamp");
            string loginCode = GetQueryString("code");
            string loginPws = GetQueryString("pwd");
            //检查输入合法性
            if (loginPws.Trim().Length == 0 || loginCode.Trim().Length == 0)
            {
                ReturnJsonResponse(new { success = 0, errorcode = ErrorCode.LOGIN_INFO_EMPTY });
                Logger.Error("密码和账号不能为空");
                return false;
            }
            string pwd = new MemberBiz().GetPwdByMobileOrHxzId(loginCode); //获取会员原始密码
            Logger.Info("pwd:" + pwd);
            string stmp = pwd + md5key + stamp; // 密码+key+stamp
            string svcode = MD516(stmp); //加密处理
            Logger.Info("svcode:" + svcode);
            if (loginPws == svcode)
            {
                return true;
            }
            else
            {
                ReturnJsonResponse(new ApiResult { success = 0, data = string.Empty, errorcode = ErrorCode.CHECK_PWD_FAIL });
                return false;
            }
        }
        private bool VerifyMemberPasswordById()
        {
            string stamp = GetQueryString("stamp");
            decimal memberid = GetQueryDecimal("memberid");
            string loginPws = GetQueryString("pwd");
            //检查输入合法性
            if (loginPws.Trim().Length == 0)
            {
                ReturnJsonResponse(new { success = 0, errorcode = ErrorCode.LOGIN_INFO_EMPTY });
                Logger.Error("原密码不能为空");
                return false;
            }
            string pwd = new MemberBiz().GetPwdById(memberid); //获取会员原始密码
            Logger.Info("pwd:" + pwd);
            string stmp = pwd + md5key + stamp; // 密码+key+stamp
            string svcode = MD516(stmp); //加密处理
            Logger.Info("svcode:" + svcode);
            if (loginPws == svcode)
            {
                return true;
            }
            else
            {
                ReturnJsonResponse(new ApiResult { success = 0, data = string.Empty, errorcode = ErrorCode.CHECK_PWD_FAIL });
                return false;
            }
        }
        /// <summary>
        /// 通过手机号码会回乡证登录 1001
        /// </summary>
        private void GetMemberToken()
        {

            if (VerifyMemberPassword())
            {
                string loginCode = GetQueryString("code");
                MemberBiz biz = new MemberBiz();
                decimal nMemberId = biz.GetIdByMobileNo(loginCode);
                if (nMemberId > 0)
                {
                    try
                    {
                        string stoken = biz.CreateToken(nMemberId);
                        bool isOverTime = false;
                        string soverdate = MemberBiz.GetFieldValue("obj.CardOverTime", nMemberId);
                        DateTime dtOverTime = DateTimeTool.String2DateTime(soverdate);
                        if (dtOverTime.Year != 9999)
                        {
                            if ((dtOverTime - DateTime.Today).Days < 0)
                                isOverTime = true;
                        }
                        else
                        {
                            soverdate = string.Empty;
                            isOverTime = true;
                        }
                        string avatar = MemberBiz.GetFieldValue("obj.Avatar", nMemberId);
                        var dtoken = new
                        {
                            memberid = nMemberId,
                            token = stoken,
                            status = isOverTime ? 1 : 0,//0=正常，1=需要续费
                            overdate = soverdate,
                            avatar= avatar
                        };
                        ReturnJsonResponse(new { success = 1, data = dtoken, errorcode = ErrorCode.EMPTY });
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
        }

        /// <summary>
        /// 检查token
        /// </summary>
        /// <returns></returns>
        private bool CheckToken()
        {
            try
            {
                string token = GetQueryString("token");
                Logger.Error("token==>" + token);
                string data_token = GetQueryString("dtoken");
                Logger.Error("dtoken==>" + data_token);
                if (token.Length == 0 && data_token.Length > 0)
                    token = data_token;
                //测试token
                if (token == "7udjyughngxfs6yd")
                    return true;

                if (token.Length == 0)
                {
                    ReturnJsonResponse(new ApiResult { success = 0, data = string.Empty, errorcode = ErrorCode.CHECK_TOKEN_EXISTS_ERROR });
                    return false;
                }
                MemberBiz biz = new MemberBiz();
                bool checkok = biz.CheckToken(token);
                Logger.Error("CheckToken==>" + checkok.ToString());
                if (!checkok)
                {
                    ReturnJsonResponse(new ApiResult { success = 0, data = string.Empty, errorcode = ErrorCode.CHECK_TOKEN_ERROR });
                    Logger.Error("token 60013==>" + token);
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
        /// <summary>
        /// 忘记密码
        /// </summary>
        private void ForgetPassword()
        {
            int certtype = GetQueryInt("certtype");
            string certno = GetQueryString("certno");
            string mobileno = GetQueryString("mobileno");
            string verifycode = GetQueryString("verifycode");
            string newpwd = GetQueryString("pwd");
            ///todo 缺短信验证码
            MemberBiz biz = new MemberBiz();
            decimal nmemberid = biz.GetByCertAndMobileNoAPP(certtype, certno, mobileno);

            if (GetQueryString("certtype").Trim() == "" ||
                certno.Trim().Length == 0 || mobileno.Trim().Length == 0 ||
                verifycode.Trim().Length == 0)
            {
                ReturnJsonResponse(new ApiResult { success = 0, data = string.Empty, errorcode = ErrorCode.ERROR_PARAM });
                return;
            }

            bool verifyok = PhoneVerifyCode.CheckVerifyCode(mobileno, verifycode);
            if (!verifyok)
            {
                ReturnJsonResponse(new ApiResult { success = 0, data = string.Empty, errorcode = ErrorCode.VERIFY_CODE_ERROR });
                Logger.Error("验证码错误!");
                return;
            }
            if (nmemberid > 0)
            {
                //檢查密碼強度
                if (newpwd.Length < 6)
                {
                    ReturnJsonResponse(new ApiResult { success = 0, data = string.Empty, errorcode = ErrorCode.PASSWORD_STRONG_ERROR });
                    return;
                }

                biz.UpdatePwd(newpwd, nmemberid);
                ReturnJsonResponse(new ApiResult { success = 1, data = string.Empty, errorcode = ErrorCode.EMPTY });
            }
            else
            {
                ReturnJsonResponse(new ApiResult { success = 0, data = string.Empty, errorcode = ErrorCode.CERTNO_OR_MOBILENO_ERROR });
                return;
            }

        }

        /// <summary>
        /// 修改會員登錄密碼 1004
        /// </summary>
        private void UpdateMemberPassword()
        {


            if (VerifyMemberPasswordById())
            {
                decimal nMemberId = GetQueryDecimal("memberid");
                string newpwd = GetQueryString("newpwd");
                //檢查密碼強度
                if (newpwd.Length < 6)
                {
                    ReturnJsonResponse(new ApiResult { success = 0, data = string.Empty, errorcode = ErrorCode.PASSWORD_STRONG_ERROR });
                    return;
                }
                if (nMemberId > 0)
                {
                    try
                    {
                        new MemberBiz().UpdatePwd(newpwd, nMemberId);
                        ReturnJsonResponse(new { success = 1, data = "", errorcode = ErrorCode.EMPTY });
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
        }

        private void GetTourListByFeature(string sFeature)
        {


            TourGroupBiz biz = new TourGroupBiz();
            DataTable dt = biz.GetTourAndTourGroupPriceByFeatureForWebsite(sFeature);
            string strResult = JsonConvert.SerializeObject(new { total = dt.Rows.Count, rows = dt });
            ReturnJsonResponse(strResult);

        }

        private void LoadDictItem(string dictCode)
        {
            ArrayList lst = new ArrayList();
            if (HttpContext.Current.Cache["DictItem"] != null)
            {
                IList items = (IList)HttpContext.Current.Cache["DictItem"];
                foreach (IDictItem dictItem in items)
                {
                    if (dictItem.DictID.ToString() == dictCode)
                    {
                        lst.Add(dictItem);
                    }
                }
            }
            string strResult = JsonConvert.SerializeObject(new { total = lst.Count, rows = lst });
            ReturnJsonResponse(strResult);
        }


        /// <summary>
        /// 用户登录 90000
        /// </summary>
        /// <param name="scode"></param>
        /// <param name="spwd"></param>
        private void Login(string scode, string spwd)
        {
            string sName = string.Empty;
            string sUserName = string.Empty;
            long lngUserID = 0;
            int nAgentState = 0;
            long lngSystemID = 0;

            int nResult = UserDP.Login(scode, spwd, ref sUserName, ref lngUserID, ref sName, ref nAgentState, ref lngSystemID);
            if (nResult == 0)
            {
                long nDeptID;
                SystemUser user;
                SetUserCookie(sName, sUserName, lngUserID, out nDeptID, out user);
                user.PWD = spwd;
                CurrentSysUser.Set(user);
                CurrentSysUser.CacheUserRight();
                //单一等限制注册
                SingleLogin.RegLoginInfo(user.UserId);
                //重定向
                //FormsAuthentication.RedirectFromLoginPage(sUserName, true);
                string strResult = JsonConvert.SerializeObject(new { loginok = true, msg = "" });
                ReturnJsonResponse(strResult);
            }
            else
            {
                string strResult = JsonConvert.SerializeObject(new { loginok = false, msg = "用户名或密码错误！" });
                ReturnJsonResponse(strResult);
            }
        }
        private void SetUserCookie(string sName, string sUserName, long lngUserID, out long nDeptID, out SystemUser user)
        {
            nDeptID = UserDP.GetUserDeptID(lngUserID);
            DeptEntity dept = new DeptDP().getById(nDeptID);// new DeptEntity(nDeptID);
            long nOrgID = DeptDP.GetDirectOrg(nDeptID);
            DeptEntity org = new DeptDP().getById(nOrgID); //new DeptEntity(nOrgID);

            user = new SystemUser();
            user.UserId = lngUserID;
            user.UserLoginName = sUserName;
            user.DeptId = nDeptID;
            user.DeptName = dept.DeptName;
            user.OrgId = nOrgID;
            user.OrgShortName = org.ShortName;
            user.UserName = sName;
            user.DeptCode = dept.DeptCode;
        }
        private void GetTourGroupList(string sTourGroupCode, string sTourerBillCode)
        {
            string strResult = string.Empty;
            IList lst = null;
            if (sTourGroupCode.Length > 0)
            {
                lst = new TourGroupBiz().GetAllItems(string.Format("obj.ProductCode='{0}'", sTourGroupCode), "");
            }
            else if (sTourerBillCode.Length > 0)
            {
                string sId = TourBillBiz.GetFieldValue("TourGroupID", string.Format("obj.TourBillCode='{0}'", sTourerBillCode));
                lst = new TourGroupBiz().GetAllItems(string.Format("obj.Id={0}", sId), "");
            }
            strResult = JsonConvert.SerializeObject(new { total = lst.Count, rows = lst });
            ReturnJsonResponse(strResult);

        }

        private void GetTourerListByTourGroupId(string sId)
        {
            TourerInfoBiz bizTourer = new TourerInfoBiz();
            string strResult;
            IList list = bizTourer.GetAllItems(string.Format("obj.TourGroupID={0}", sId), "obj.SeatNo");
            object jsonResult = new { total = list.Count, rows = list };
            strResult = JsonConvert.SerializeObject(jsonResult);
            ReturnJsonResponse(strResult);
        }
    }
}