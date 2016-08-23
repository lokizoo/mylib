using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using DevelopLibrary.Enums;
using DevelopLibrary.DevelopAPI;
using TradePubLib;

namespace mylib
{
    class wt_SlowKD_03 : EventStrategyBase
    {
        private const string C_VERSION = "0.305";

        #region 参数
        [Parameter(Display = "N", Description = "小周期N参数", Category = "SKDJ")]
        public int N = 15;

        [Parameter(Display = "P1", Description = "", Category = "SKDJ")]
        public int P1 = 4;

        [Parameter(Display = "P2", Description = "", Category = "SKDJ")]
        public int P2 = 2;

        [Parameter(Display = "P3", Description = "", Category = "SKDJ")]
        public int P3 = 5;

        [Parameter(Display = "BpN", Description = "大周期N参数", Category = "SKDJ")]
        public int BpN = 15;
        
        // 建仓信号（以多头为例）
        // 0：当前周期KD值金叉
        // 1: 当前周期KD值金叉 且 大周期上KD值必须金叉(K > D)
        // 2：当前周期KD值金叉 且 大周期上K线必须拐头向上(K[0] > K[1])
        // 3: [条件2] 或者 [当前周期KD值金叉 且 大周期K线值 > 50]	
        [Parameter(Display = "信号[建仓]", Description = "建仓信号", Category = "模型")]
        public int InType = 2;

        // 平仓信号（以多头为例）
        // 1. 死叉离场; 反之
        // 2. 
        //    1) 死叉离场
        //    2) K值>80后，拐头离场	
        [Parameter(Display = "信号[平仓]", Description = "平仓信号", Category = "模型")]
        public int OffsetType = 2;
        
        [Parameter(Display = "K极大值", Description = "指标K的极大值范围", Category = "模型")]
        public double ExtremeHighK = 80;

        [Parameter(Display = "K极小值", Description = "指标K的极小值范围", Category = "模型")]
        public double ExtremeLowK = 20;

        [Parameter(Display = "K斜率均值参", Description = "计算大周期K值斜率的平均数的参数", Category = "模型")]
        public int KRatioLength = 5;

        [Parameter(Display = "K斜率交易阀值", Description = "大周期的K值得斜率必须大于此数字，才可交易", Category = "模型")]
        public int CanTradeThreshold = 1;

        [Parameter(Display = "止损类型", Description = "止损类型（0：不设置止损； 1：ATR移动止损法", Category = "模型")]
        public int StopLossType = 0;

        [Parameter(Display = "ATR长度", Description = "ATR移动止损中的ATR长度", Category = "ATR移动止损")]
        public int ST_AtrLength = 14;

        [Parameter(Display = "ATR倍数", Description = "ATR移动止损中的ATR放大倍数", Category = "ATR移动止损")]
        public double ST_Factor = 2;

        
        [Parameter(Display = "最大头寸", Description = "最大可持仓的头寸", Category = "资管")]
        public int MaxLot = 1;

        [Parameter(Display = "开仓资金系数", Description = "头寸开仓资金百分比 {[0, 1]: 百分比计算头寸; > 1: 固定资金计算头寸 ", Category = "资管")]
        public double CapitalRatio = 0.5;


        [Parameter(Display = "开仓滑点", Description = "开仓所允许的滑点", Category = "交易")]
        public int Open_SlipPoint = 0;

        [Parameter(Display = "平仓滑点", Description = "平仓所允许的滑点", Category = "交易")]
        public int Close_SlipPoint = 0;

        [Parameter(Display = "平仓未成交超时(s)", Description = "平仓未成交的超时时间(单位：s）", Category = "交易")]
        public double Close_TimeOut = 3;

        [Parameter(Display = "开仓未成交超时(根)", Description = "开仓未成交超过N根K线为超时(单位：根）", Category = "交易")]
        public int Open_BarOut = 3;

        [Parameter(Display = "需要收盘事件的时间", Description = "需要处理的收盘事件的时间列表（以空格隔开）", Category = "交易")]
        public string NeedBarCloseEventTime = "11:29:58 14:59:55 22:59:55";

        [Parameter(Display = "不需要开盘事件的时间", Description = "不需要处理的开盘事件的时间列表（以空格隔开）", Category = "交易")]
        public string NoBarOpenEventTime = "9:00:00 13:30:00 20:59:00 21:00:00 ";

        [Parameter(Display = "识别码", Description = "区分持仓所用的识别码", Category = "其它")]
        public string MyID = "skd03";

        [Parameter(Display = "版本号", Description = "模型的版本号", Category = "其它", IsReadOnly = true)]
        public string MyVersion = C_VERSION;

        [Parameter(Display = "写成交日志", Description = "是否记录成交的日志", Category = "其它")]
        public bool IsRecordTrade = false;

        [Parameter(Display = "自动运行", Description = "是否在设定的事件自动运行和停止", Category = "其它")]
        public bool IsAutoRun = true;

        [Parameter(Display = "同步时间", Description = "本地时间是否同步行情服务器时间", Category = "其它")]
        public bool SyncServerTime = false;

        #endregion

        private bool m_initOK = true;           // OnStart事件中初始化是否成功
        private double m_PriceTick = 0;         // 品种最小变动价位
        private EventStrategyBase m_bigPeriodEV = null;
        private EnumDataCycle m_bigPeriod = EnumDataCycle.TICK;
        private double m_bigPeriodRepeat = 0;

        private int m_longPosition_TD = 0;          // 本品种本策略所持的多头仓位（今仓）
        private int m_longPosition_YD = 0;          // 本品种本策略所持的多头仓位（昨仓）

        private int m_shortPosition_TD = 0;         // 本品种本策略所持的空头仓位（进仓）
        private int m_shortPosition_YD = 0;         // 本品种本策略所持的空头仓位（昨仓）

        private bool m_bCloseTimeOutStart = false;  // 是否开始平仓未成交超时计时
        private int m_afterOpenBarCount = 0;        // 发送开仓指令后K线数

        private DateTime m_dtLastBarOpenTime;       // 最后触发OnBarOpen事件的时间
        private bool m_bBarOpenSwitch;              // OnBarOpen事件开关
        private int m_TickCountOfBarOpenDelay = 0;  // 未触发OnBarOpen事件超时的Tick数量

        // 未触发OnBarOpen事件超时的Tick数量阀值（超过这个阀值 => 手动触发BarOpen事件)
        private const int C_TickCountOfBarOpenDelay_Threshold = 5;

        private int m_iInCount = 0;                 // 入场次数
        private int m_iOutCount = 0;                // 平仓次数
        private STU_Indicators m_Indicators;        // 模型需要的K线数据和指标数据


        private GF m_gf = null;                     // 通用函数对象
        
        /*
        private enum ENU_msgType
        {
            msg_Error = 1,
            msg_Info = 2            
        }
         */ 

        private enum ENU_CalculateModelEventType
        {
            ONBAROPEN = 1,
            ONBARCLOSE = 2
        }

        private struct STU_Indicators
        {
            // 本周期的K线收盘价、最低价、最高价
            public DataArray myC;
            public DataArray myL;
            public DataArray myH;

            // 大周期的K线收盘价、最低价、最高价
            public DataArray myBpC;
            public DataArray myBpL;
            public DataArray myBpH;

            // 本周期KD值
            public DataArray K;
            public DataArray D;

            // 大周期KD值
            public DataArray bpK;
            public DataArray bpD;

            // 本周期模型需要的KD单值
            public double K0;
            public double D0;
            public double K1;
            public double D1;
            public double K2;

            // 大周期模型需要的KD单值
            //public double bpPreK0;      // 当前大周期K0上一次计算的值
            //public double bpK0;         // 当前大周期K0此次计算的值

            private double m_bpPreK0;      // 当前大周期K0上一次计算的值
            private double m_bpK0;         // 当前大周期K0此次计算的值

            private double m_bpPreK1;
            private double m_bpK1;

            //public double bpK1;

            public double bpD0;
            public double bpD1;

            public double Atr_PreValue;         // 上一根K线计算出来的ATR值
            public double LongStopLossPrice;    // 计算出来的多头止损价格
            public double ShortStopLossPrice;   // 计算出来的空头止损价格

            //public double KRatio;       // 本周期K斜率
            //public double bpKRatio;     // 大周期K斜率     
            //public double bpKRatio_PREDICT; // 大周期K斜率预测的值
            //private double m_bpKRatio_PREDICT; // 大周期K斜率预测的值

            public int startPos;        // 起始位置

            public double bpK0 
            { 
                get { return m_bpK0;} 
                set
                {
                    if (m_bpPreK0 <= 0)
                    {
                        m_bpPreK0 = value;
                        m_bpK0 = value;
                    }
                    else
                    {
                        m_bpPreK0 = m_bpK0;
                        m_bpK0 = value;
                    }
                }
            }

            public double bpK1
            {
                get { return m_bpK1; }
                set
                {
                    if (m_bpPreK1 <= 0)
                    {
                        m_bpPreK1 = value;
                        m_bpK1 = value;
                    }
                    else
                    {
                        m_bpPreK1 = m_bpK1;
                        m_bpK1 = value;
                    }
                }
            }

            /// <summary>
            /// 大周期K斜率预测的值
            /// </summary>
            public double bpKRatio_PREDICT
            {
                get
                {
                    double tempR = 0;
                    double ratio = 0;

                    if (m_bpK1 == m_bpPreK1)
                    {   // 在同一根K线内重复计算
                        tempR = m_bpK0 - m_bpPreK0;

                        if (KRatio * tempR <= 0)
                        {
                            // 小周期的K斜率和大周期的K斜率不同方向 = > 直接取大周期的斜率
                            //return ratio;
                            ratio = m_bpK0 - m_bpK1;
                        }
                        else
                        {
                            // 小周期的K斜率和大周期的K斜率同向 = > 大周期的斜率 * 小周期斜率
                            ratio = Math.Abs(KRatio) * tempR;
                        }   
                    }
                    else
                    {
                        // 一根新的大周期K线形成
                        ratio = m_bpK0 - m_bpK1;
                    }

                    return ratio;               
                }
            }

            /// <summary>
            /// 大周期K斜率  
            /// </summary>
            public double bpKRatio
            {
                get { return m_bpK0 - m_bpK1; }
            }

            /// <summary>
            /// 本周期K斜率
            /// </summary>
            public double KRatio
            {
                get { return K0 - K1; }
            }
        }

        

       
        public DateTime RestT0
        {
            get { return new DateTime(SERVERTIME.Year, SERVERTIME.Month, SERVERTIME.Day, 10, 15, 0); }
        }
               
        public DateTime RestT1
        {
            get { return new DateTime(SERVERTIME.Year, SERVERTIME.Month, SERVERTIME.Day, 10, 30, 0); }
        }

        public DateTime RestT2
        {
            get { return new DateTime(SERVERTIME.Year, SERVERTIME.Month, SERVERTIME.Day, 11, 30, 0); }
        }
                
        public DateTime RestT3
        {
            get { return new DateTime(SERVERTIME.Year, SERVERTIME.Month, SERVERTIME.Day, 13, 30, 0); }
        }
        
        /// <summary>
        /// 日收盘时间
        /// </summary>
        public DateTime MarketCloseTime
        {
            get { return new DateTime(SERVERTIME.Year, SERVERTIME.Month, SERVERTIME.Day, 15, 00, 0); }
        }

        /// <summary>
        /// Tick时间戳
        /// </summary>
        public DateTime TickNow
        {
            get { return SERVERTIME; }
        }


        [StrategyTask(Time = "15:30:00")]
        private void StopMe()
        {
            if (IsAutoRun)
            {
                Stop();
            }
            //System.Threading.Thread.Sleep(3000);        // 时延3秒
            //Start();
        }

        [StrategyTask(Time = "20:55:00")]
        private void StartMe()
        {
            if (IsAutoRun)
            {
                if (RunStatus != EnumRunStatus.Stop)
                {
                    Stop();
                    System.Threading.Thread.Sleep(3000);        // 时延3秒
                }

                Start();
            }
        }
                
        //[StrategyTask(Time = "15:05:00")]
        private void PrintTodayTrade()
        {
            FuturesAccount fa = GetFuturesAccount();
            PrintLine("********************************************");
            PrintLine("日期: " + SERVERTIME.ToString("yyyy-MM-dd HH:mm:ss"));
            PrintLine("静态权益: " + fa.StaticProfit.ToString() + "\t动态权益: " + fa.DynamicProfit.ToString());
            PrintLine("平仓盈亏: " + fa.CloseProfit.ToString() + "\t持仓盈亏: " + fa.PositionProfit.ToString() + "\t手续费: " + fa.Commission.ToString());
            PrintLine("今日盈亏: " + (Math.Round(fa.DynamicProfit - fa.StaticProfit, 3)).ToString());
            PrintLine("********************************************");
        }

        public override void OnStart()
        {
            // 创建通用函数对象
            m_gf = GF.GetGenericFun(this);

            m_initOK = true;
            m_PriceTick = INSTRUMENT.PriceTick;
            m_dtLastBarOpenTime = Now;

            // 打印合约信息
            m_gf.PrintInstrumentInfo();

            // 加载历史数据（本周期和大周期的）
            if (!LoadMyData()) m_initOK = false;

            //检查加载的数据是否合规（是否有重复数据）            
            if (!CheckDataValid()) m_initOK = false;
            
            // 获取本合约持仓头寸
            GetPosition();

            // 创建图表
            CreateChart("Main;测试.w_SLOWKD(15,4,2,5)");

            //m_gf.PrintMemo("启动完成[GF]", ENU_msgType.msg_Info);

            m_gf.PrintMemo("启动完成", ENU_msgType.msg_Info);
            //base.OnStart(); 
        }

        public override void OnStop()
        {
            //PrintTodayTrade();

            m_gf.PrintMemo(SYMBOL + "[" + MyID + "] 停止运行");
            m_gf = null;

            base.OnStop();
        }
        
        public override void OnTick(TickData td)
        {
            // 初始化未成功，事件不处理
            if (!m_initOK) return;

            // 检查是否触发止损
            if (StopLossType == 1) CheckStopLoss(td);

            // 检查是否有超时未成交的挂单（平仓单）
            CheckAllOrder_Timeout();

            // 检查是否正常触发OnBarOpen事件，若没有触发，则手工触发
            CheckBarOpenEvent(td);

            // 是否需要增加处理额外的"Bar Close"事件
            if (NeedBarCloseEventTime.Trim() != "")
            {
                string[] eventTimes = NeedBarCloseEventTime.Trim().Split(' ');
                foreach(string eventTime in eventTimes)
                {
                    DoBarCloseEvent(eventTime, td);
                }
            }
        }               
                
        public override void OnBarOpen(TickData td)
        {
            if (!m_initOK)
            {
                // 初始化未成功，事件不处理
                m_gf.PrintMemo("初始化未成功，请修正初始化错误的原因后，重启策略", ENU_msgType.msg_Error);
                return;
            }             

            m_gf.PrintMemo("IO:时间[" + td.Date.ToString("HH:mm:ss") + "]"); 
            PrintLine(TickNow.ToString("yyyy-MM-dd HH:mm:ss") + "[S]----------------------------------------");
            DateTime t1 = Now;

            m_bBarOpenSwitch = false;
            m_TickCountOfBarOpenDelay = 0;
            m_dtLastBarOpenTime = td.Date;
                        
            // 判断是否需要处理的BarOpen事件
            if (IsNeedDoBarOpenEvent(td))
            {
                // 需要处理的BarOpen事件
                CalculateModel(td, ENU_CalculateModelEventType.ONBAROPEN);
            }
            else
            {
                m_gf.PrintMemo("时间[" + td.Date.ToString("HH:mm:ss") + "] 的BarOpen事件不处理");
            }

            // 其它动作
            if (SyncServerTime)
            {
                // 同步本地时间
                //string sLocalDatetime = SyncTimeFromServer();
                //m_gf.PrintMemo("本地时间与服务器时间同步, 本地时间 => " + sLocalDatetime);            
            }

            DateTime t2 = Now;
            PrintLine("[E]-------------------------------------------------------" + (t2 - t1).TotalMilliseconds + " ms");
        }
        

        public override void OnInstrumentStatusChanged(InstrumentStatus instStatus)
        {
            string msg = "[" + instStatus.InstrumentID + "] " + instStatus.EnterTime + " " + instStatus.EnterReason.ToString() + " " + instStatus.InstStatus.ToString();
            m_gf.PrintMemo(msg);
        }
                
        public override void OnTrade(Trade trade)//有成交时执行
        {
            string msg = "成交: " + SYMBOL + " " + trade.Direction.ToString() + trade.OffsetFlag.ToString() + " " + trade.Price.ToString() + " * " + trade.Volume + " @ " + 
                trade.TradeTime + "[" + trade.TradeID + ", " + trade.OrderSysID + "]";
            m_gf.PrintMemo(msg);

            // 判断是否还有未成交平仓单
            m_bCloseTimeOutStart = false;
            
            List<Order> ordLst = GetUnAllTradedOrderList(SYMBOL);
            if (ordLst != null && ordLst.Count > 0)
            {
                foreach (Order order in ordLst)
                {
                    if (order.OffsetFlag == EnumOffsetFlagType.Close || order.OffsetFlag == EnumOffsetFlagType.CloseToday || order.OffsetFlag == EnumOffsetFlagType.CloseYesterday)
                    {
                        m_bCloseTimeOutStart = true;
                        break;
                    }
                }
            }

            // 写成交日志
            if (IsRecordTrade)
            {
                WriteTradeLog(trade);
            }
        }

        public override void OnOrderReturn(Order order)
        {
            //base.OnOrderReturn(order);
            //GetPosition();
        }

        public override void OnPosition(Trade trade)//当平台设置里，设置为本地计算，才能触发。平台内部持仓信息更新后触发，在OnTrade后，也是策略隔离。
        {
            /*
            m_gf.PrintMemo("OnPosition{" + trade.Volume.ToString() + " * " + trade.Price.ToString() + "} [TradeID = " + trade.TradeID.ToString() + "] [" + "[OrderSysID = " +
                trade.OrderSysID.ToString() + "]", ENU_msgType.msg_Info);
            //PrintLine(trade.Volume);//打印成交的手数
            //PrintLine(trade.Price);//打印实际成交价格

            D_m_bQueryPos = true;
             */
            GetPosition();
        }

        public override void OnCancelOrderSucceeded(Order order)
        {
            m_gf.PrintMemo("撤单成功: " + SYMBOL + " " + order.OrderDirection.ToString() + order.OffsetFlag.ToString() + " " + order.StatusMsg);

            if (order.OffsetFlag == EnumOffsetFlagType.Close || order.OffsetFlag == EnumOffsetFlagType.CloseToday || order.OffsetFlag == EnumOffsetFlagType.CloseYesterday)
            {
                m_gf.PrintMemo("开始追平仓单");
                ResendCloseOrder(order, Close_SlipPoint, m_PriceTick);
            }
            //base.OnCancelOrderSucceeded(order);
        }
        
        /// <summary>
        /// 本地时间从服务器时间同步过来
        /// </summary>
        /// <returns>同步后的本地时间</returns>
        private string SyncTimeFromServer()
        {            
            // 取本地日期
            DateTime t = DateTime.Now;
            string sDate = t.ToString("yyyy-MM-dd");

            // 本地时间 = 本地日期 + 服务器时间
            string sDateTime = sDate + " " + TickNow.ToString("HH:mm:ss");
           

            //转换System.DateTime到SYSTEMTIME
            SYSTEMTIME st = new SYSTEMTIME();            
            st.From(sDateTime, "yyyy-MM-dd HH:mm:ss");

            //调用Win32 API设置系统时间
            Win32API.SetLocalTime(ref st);
            return sDateTime;
        }

        /// <summary>
        /// 检查加载的历史数据是否合法（主要检查是否有重复数据）
        /// </summary>
        /// <returns></returns>
        private bool CheckDataValid()
        {
            bool bIsValid = true;
            string sInfo = "数据检查合法";            

            for (int i = 1; i < this.CLOSE.Length; i++)
            {
                if (DATE[i-1] == DATE[i])
                {
                    if (TIME[i - 1] >= TIME[i])
                    {
                        if (TIME[i] != 90000)       // 因为有夜盘的存在，所以，上一日的夜盘属于今日的行情，所以这里要稍微处理一下
                        {
                            sInfo = "日期[" + DATE[i] + "] 时间序列重复 [" + TIME[i - 1] + " -> " + TIME[i] + "]";
                            bIsValid = false;
                            break;
                        }                        
                    }                    
                }
                else if (DATE[i-1] > DATE[i])
                {                    
                    sInfo = "日期序列重复[" + DATE[i - 1] + " " + TIME[i - 1] + "  ->  " + DATE[i] + " " + TIME[i] + "]";
                    bIsValid = false;
                    break;
                }
            }

            m_gf.PrintMemo(sInfo, bIsValid ? ENU_msgType.msg_Info : ENU_msgType.msg_Error);
            return bIsValid;
        }

        /// <summary>
        /// 检查是否触发止损
        /// </summary>
        /// <param name="td"></param>
        private void CheckStopLoss(TickData td)
        {
            int longPosition = m_longPosition_TD + m_longPosition_YD;
            int shortPosition = m_shortPosition_TD + m_shortPosition_YD;

            // 多头仓位检查止损
            if (longPosition > 0)
            {
                if (m_Indicators.LongStopLossPrice > 0 && td.LastPrice < m_Indicators.LongStopLossPrice)
                {
                    // 最新价 < 多头止损价 => 止损平仓
                    m_gf.PrintMemo("最新价[" + td.LastPrice.ToString() + "] < 多头止损价[" + Math.Round(m_Indicators.LongStopLossPrice, 2).ToString() + "] => 止损平仓 @ " + td.BidPrice1.ToString() + " * " + longPosition.ToString());
                    CloseLongPosition(td.BidPrice1, longPosition, Close_SlipPoint, m_PriceTick);                    
                    //m_iOutCount++;
                }
            }
            // 空头仓位检查止损
            if (shortPosition > 0)
            {
                if (m_Indicators.ShortStopLossPrice > 0 && td.LastPrice > m_Indicators.ShortStopLossPrice)
                {
                    // 最新价 > 空头止损价 => 止损平仓
                    m_gf.PrintMemo("最新价[" + td.LastPrice.ToString() + "] > 空头止损价[" + Math.Round(m_Indicators.ShortStopLossPrice, 2) + "] => 止损平仓 @ " + td.AskPrice1.ToString() + " * " + shortPosition.ToString());
                    CloseShortPosition(td.AskPrice1, shortPosition, Close_SlipPoint, m_PriceTick);                    
                    //m_iOutCount++;
                }
            }
        }

        /// <summary>
        /// 检查是否有超时的未成交平仓单，若有则撤单，重新挂单
        /// </summary>
        private void CheckAllOrder_Timeout()
        {
            if (m_bCloseTimeOutStart)
            {
                List<Order> ordLst = GetUnAllTradedOrderList(SYMBOL);
                if (ordLst != null && ordLst.Count > 0)
                {
                    foreach (Order order in ordLst)
                    {
                        if (ORDER_KEY_LIST.Contains(order.Key))
                        {
                            if ((order.OffsetFlag == EnumOffsetFlagType.Close || order.OffsetFlag == EnumOffsetFlagType.CloseToday || order.OffsetFlag == EnumOffsetFlagType.CloseYesterday) &&
                                (order.OrderStatus == EnumOrderStatusType.NoTradeQueueing || order.OrderStatus == EnumOrderStatusType.PartTradedQueueing))
                            {
                                double Dt = CalculateOrderDuration(order);

                                if (Dt > Close_TimeOut)//超时未成交
                                {
                                    m_gf.PrintMemo("有超时未成交平仓单");
                                    CancelOrder(order);         // 撤单                                    
                                }
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 计算挂单持续到现在的时长
        /// </summary>
        /// <param name="order"></param>
        /// <returns></returns>
        private double CalculateOrderDuration(Order order)
        {
            double Dt = (TickNow - ConvertToDateTime(order.InsertDate, order.InsertTime)).TotalSeconds;

            if (TickNow.ToString("HH:mm:ss").CompareTo(RestT1.ToString("HH:mm:ss")) >= 0 && order.InsertTime.Substring(0, 8).CompareTo(RestT0.ToString("HH:mm:ss")) <= 0) 
            {
                Dt = Dt - (RestT1 - RestT0).TotalSeconds;       // 除去早盘休息时间
            }
            else if (TickNow.ToString("HH:mm:ss").CompareTo(RestT3.ToString("HH:mm:ss")) >= 0 && order.InsertTime.Substring(0, 8).CompareTo(RestT2.ToString("HH:mm:ss")) <= 0)
            {
                Dt = Dt - (RestT3 - RestT2).TotalSeconds;       // 除去中午休息时间
            }

            return Dt;
        }

        /// <summary>
        /// 检查OnBarOpen事件是否正常触发
        /// </summary>
        /// <param name="td"></param>
        private void CheckBarOpenEvent(TickData td)
        {            
            // 检测OnBarOpen事件是否已经触发 
            if (DataProvider.DataCycle.CycleBase == EnumDataCycle.MINUTE)
            {
                if (td.Date.Minute % (int)DataProvider.DataCycle.Repeat == 0)           // 应该触发OnBarOpen事件的整数时间
                {
                    // 检查此时的时间与上次触发OnBarOpen事件的时间差
                    TimeSpan ts = td.Date.Subtract(m_dtLastBarOpenTime);
                    if ((ts.Hours * 60 + ts.Minutes) >= (int)DataProvider.DataCycle.Repeat)
                    {
                        m_TickCountOfBarOpenDelay++;

                        if (!m_bBarOpenSwitch)
                        {
                            // 若时间差 >= 1 Min 且 未触发需要BarOpen事件开关 => 打开开关(表明这里需要触发OnBarOpen事件了）
                            m_bBarOpenSwitch = true;
                        }
                        else
                        {
                            // 若时间差 >= 1 Min 且 已触发需要BarOpen事件开关 => 触发 OnBarOpen事件
                            if (m_TickCountOfBarOpenDelay >= C_TickCountOfBarOpenDelay_Threshold)
                            {
                                m_gf.PrintMemo("已丢失一个OnBarOpen事件，现在手动触发[" + m_TickCountOfBarOpenDelay.ToString() + "] @ " + td.Date.ToString("yyyy-MM-dd HH:mm:ss.fff"));
                                OnBarOpen(td);
                            }
                            else
                            {
                                m_gf.PrintMemo("检测到将丢失一个OnBarOpen事件，Tick Count = " + m_TickCountOfBarOpenDelay.ToString());
                            }
                        }
                    }

                }
            }
            else if (DataProvider.DataCycle.CycleBase == EnumDataCycle.DAY)
            {
                //TODO
            }
            else
            {
                //TODO
            }
        }

        /// <summary>
        /// 触发BarClose Event
        /// </summary>
        /// <param name="eventTime"></param>
        /// <param name="td"></param>
        private void DoBarCloseEvent(string eventTime, TickData td)
        {
            if (eventTime != "")
            {
                try
                {
                    DateTime needCloseEventTime = DateTime.ParseExact(eventTime, "H:m:s", System.Globalization.CultureInfo.CurrentCulture);

                    if (td.Date.Hour == needCloseEventTime.Hour && td.Date.Minute == needCloseEventTime.Minute && td.Date.Second == needCloseEventTime.Second)
                    {
                        // 触发Bar Close 事件
                        m_gf.PrintMemo("额外处理时间[" + eventTime + "]事件");
                        CalculateModel(td, ENU_CalculateModelEventType.ONBARCLOSE);
                    }
                }
                catch (Exception e)
                {
                    m_gf.PrintMemo("额外处理事件中时间[" + eventTime + "]转换错误");
                }
            }
        }

        private bool IsNeedDoBarOpenEvent(TickData td)
        {
            bool bNeed = true;

            string sNoBarOpenEventTime = NoBarOpenEventTime.Trim();

            if (sNoBarOpenEventTime != "")
            {
                string[] eventTimes = sNoBarOpenEventTime.Split(' ');
                foreach (string eventTime in eventTimes)
                {
                    try 
                    { 
                        DateTime noNeedTime = DateTime.ParseExact(eventTime, "H:m:s", System.Globalization.CultureInfo.CurrentCulture);

                        if (td.Date.Hour == noNeedTime.Hour && td.Date.Minute == noNeedTime.Minute && td.Date.Second >= noNeedTime.Second && td.Date.Second <= noNeedTime.Second + 5)
                        {
                            bNeed = false;
                            break;
                        }
                    }
                    catch(Exception e)
                    {
                        m_gf.PrintMemo("不需要处理事件中时间[" + eventTime + "]转换错误");
                    }
                }
            }

            return bNeed;
        }

        /// <summary>
        /// 计算模型并判断开平仓条件
        /// </summary>
        /// <param name="td"></param>
        private void CalculateModel(TickData td, ENU_CalculateModelEventType eventType = ENU_CalculateModelEventType.ONBAROPEN)
        {
            double K0, D0, K1, D1, K2;
            double bpK0, bpD0, bpK1, bpD1;
            double KRatio, bpKRatio;
            int iStartPos;

            int iSN = 0;
            bool bHaveExtreValue = false;           // K值是否经过极值（极大值或极小值）
            bool bHaveClosePosition = false;        // 此事件运行过程中是否已平仓过
            bool bCanTradeOfRatio = false;          // 大周期K线斜率是否为可交易
            bool bEnterLong = false;                // 是否可以开多头仓位
            bool bEnterShort = false;               // 是否可以开空头仓位
            int lots = MaxLot;                      // 头寸
            double dOpenPrice = 0;                  // 开仓价格
                        
            if (m_bigPeriodEV == null)
            {
                m_gf.PrintMemo("未获取到大周期数据", ENU_msgType.msg_Error);
                return;
            }
                        
            // ------------------------------------------              
            #region 指标计算            
            CalculateIndicator(eventType);

            DataArray K = m_Indicators.K; ;
            DataArray D = m_Indicators.D;
            DataArray bpK = m_Indicators.bpK;
            DataArray bpD = m_Indicators.bpD;

            K0 = m_Indicators.K0;
            K1 = m_Indicators.K1;
            K2 = m_Indicators.K2;
            D0 = m_Indicators.D0;
            D1 = m_Indicators.D1;
            bpK0 = m_Indicators.bpK0;
            bpK1 = m_Indicators.bpK1;
            bpD0 = m_Indicators.bpD0;
            bpD1 = m_Indicators.bpD1;            
            iStartPos = m_Indicators.startPos;

            KRatio = m_Indicators.KRatio;
            //bpKRatio = m_Indicators.bpKRatio;
            bpKRatio = m_Indicators.bpKRatio_PREDICT;

            bCanTradeOfRatio = (Math.Abs(bpKRatio) > CanTradeThreshold);

            //PrintKD(K0, D0, K1, D1, bpK0, bpD0, bpK1, bpD1, KRatio, bpKRatio);
            PrintKD();
            #endregion
            // ------------------------------------------            

            // ------------------------------------------            
            #region 仓位检查和未成交开仓单检查
            GetPosition();      // 更新仓位
            int longPosition = m_longPosition_TD + m_longPosition_YD;
            int shortPosition = m_shortPosition_TD + m_shortPosition_YD;

            // 检查是否有超时未成交的开仓单，若有则取消此开仓单
            List<Order> unTradedOrdLst = GetUnTraded_OpenOrder();

            if (unTradedOrdLst != null && unTradedOrdLst.Count > 0)
            {
                if (m_afterOpenBarCount > Open_BarOut)
                {
                    foreach (Order order in unTradedOrdLst)
                    {
                        CancelOrder(order);
                    }
                    m_afterOpenBarCount = 0;
                }
            }
            #endregion
            // ------------------------------------------            

            // ------------------------------------------            
            #region 开平仓模型
            // 1. K线向下拐头            
            if (K0 < K1 && K1 > K2)
            {
                m_gf.PrintMemo("K线向下拐头");
                if (OffsetType == 2 && longPosition > 0)        // 平仓模型 = 2 且 有多头仓位
                {
                    // 判断拐头前是否有K值 >= 极大值[80]
                    iSN = iStartPos + 1;
                    bHaveExtreValue = false;
                    
                    while (Refdata(K, iSN) >= K0 && (!bHaveExtreValue))
                    {
                        if (Refdata(K, iSN) >= ExtremeHighK)
                        {
                            bHaveExtreValue = true;
                        }
                        iSN++;
                    }

                    if (bHaveExtreValue)    // K值有经过极大值线                            
                    {
                        // K值出现过极大值(>=80)且现在已经拐头 => 平多仓
                        m_gf.PrintMemo("K走势有超过极大值[" + ExtremeHighK.ToString() + "]线 => 平多头仓位 @ " + td.BidPrice1.ToString() + " * " + longPosition.ToString());
                        CloseLongPosition(td.BidPrice1, longPosition, Close_SlipPoint, m_PriceTick);
                        bHaveClosePosition = true;
                        m_iOutCount++;
                    }
                    else
                    {
                        m_gf.PrintMemo("K走势未超过极大值[" + ExtremeHighK.ToString() + "]线");
                    }
                }
            }

            // 2. K线向上拐头            
            if (K0 > K1 && K1 < K2)
            {
                m_gf.PrintMemo("K线向上拐头");
                if (OffsetType == 2 && shortPosition > 0)       // 平仓模型 = 2 且 有空头仓位
                {
                    // 判断拐头前是否有K值 <= 极小值[20]
                    iSN = iStartPos + 1;
                    bHaveExtreValue = false;
                    
                    while (Refdata(K, iSN) <= K0 && (!bHaveExtreValue))
                    {
                        if (Refdata(K, iSN) <= ExtremeLowK)
                        {
                            bHaveExtreValue = true;
                        }
                        iSN++;
                    }

                    if (bHaveExtreValue)    // K值有经过极小值线                            
                    {
                        // K值出现过极大值(<=80)且现在已经拐头 => 平空仓
                        m_gf.PrintMemo("K走势有超过极小值[" + ExtremeLowK.ToString() + "]线 => 平空头仓位 @ " + td.AskPrice1.ToString() + " * " + shortPosition.ToString());
                        CloseShortPosition(td.AskPrice1, shortPosition, Close_SlipPoint, m_PriceTick);
                        bHaveClosePosition = true;
                        m_iOutCount++;
                    }
                    else
                    {
                        m_gf.PrintMemo("K走势未超过极小值[" + ExtremeLowK.ToString() + "]线");
                    }
                }
            }

            // 3. KD 金叉            
            if (K0 > D0 && K1 <= D1)
            {
                m_gf.PrintMemo("KD金叉");
                if ((!bHaveClosePosition) && shortPosition > 0)     // 此事件运行过程中未平过仓 且 有空头仓位  => 平空仓
                {
                    m_gf.PrintMemo("平空头仓位 @ " + td.AskPrice1.ToString() + " * " + shortPosition.ToString());
                    CloseShortPosition(td.AskPrice1, shortPosition, Close_SlipPoint, m_PriceTick);
                    bHaveClosePosition = true;
                    m_iOutCount++;
                }

                // 检查开多仓模型条件
                switch (InType)
                {
                    case 0:     // 不需要大周期上过滤即可开多单
                        m_gf.PrintMemo("不需要大周期上过滤");
                        bEnterLong = true;
                        break;
                    case 1:     // 且需要大周期上 KD 金叉后 K向上走                        
                        if (bpK0 > bpD0 && bpK0 > bpK1 && bCanTradeOfRatio)
                        {
                            m_gf.PrintMemo("大周期上KD金叉后 K[0] > K[1]");
                            bEnterLong = true;
                        }
                        break;
                    case 2:     // 且需要大周期上 (K[0] > K[1])                        
                        if (bpK0 > bpK1 && bCanTradeOfRatio)
                        {
                            m_gf.PrintMemo("大周期上 K[0] > K[1]");
                            bEnterLong = true;
                        }
                        break;
                    case 3:     // 且需要大周期上 {K[0] > K[1] 或 K[0] > 50}                        
                        if ((bpK0 > bpK1 || bpK0 > 50) && bCanTradeOfRatio)
                        {
                            m_gf.PrintMemo("大周期上 K[0] > K[1] 或 K[0] > 50");
                            bEnterLong = true;
                        }
                        break;
                    default:
                        bEnterLong = false;
                        break;
                }

                if (bEnterLong)     // 开多单
                {
                    if (longPosition <= 0)
                    {
                        
                        dOpenPrice = Refdata(CLOSE, iStartPos);
                        lots = CalculateLots(MaxLot, CapitalRatio, 0, dOpenPrice);      // 计算可开仓的仓位
                        m_gf.PrintMemo("满足开多条件 => 多头建仓 @ " + dOpenPrice.ToString() + " * " + lots.ToString());
                        OpenOrder(dOpenPrice, lots, Open_SlipPoint, m_PriceTick, EnumDirectionType.Buy);

                        // 发送开多指令后，若N（默认=3）根K线后还没成交，则撤单
                        m_afterOpenBarCount = 1;
                        m_iInCount++;
                    }
                    else
                    {
                        m_gf.PrintMemo("已有多头仓位 [" + longPosition.ToString() + "]");
                    }
                }
            }

            // 4. KD死叉            
            if (K0 < D0 && K1 >= D1)
            {
                m_gf.PrintMemo("KD死叉");
                if ((!bHaveClosePosition) && longPosition > 0)      // 此事件运行过程中未平过仓 且 有多头仓位 => 平多仓
                {
                    m_gf.PrintMemo("平多头仓位 @ " + td.BidPrice1.ToString() + " * " + longPosition.ToString());
                    CloseLongPosition(td.BidPrice1, longPosition, Close_SlipPoint, m_PriceTick);
                    bHaveClosePosition = true;
                    m_iOutCount++;
                }

                // 检查开空仓模型条件
                switch (InType)
                {
                    case 0:     // 不需要大周期上过滤即可开空单
                        m_gf.PrintMemo("不需要大周期上过滤");
                        bEnterShort = true;
                        break;
                    case 1:     // 且需要大周期上KD死叉 且 K向下（K < D）                        
                        if (bpK0 < bpD0 && bpK0 < bpK1 && bCanTradeOfRatio)
                        {
                            m_gf.PrintMemo("大周期上死叉后 K[0] < K[1]");
                            bEnterShort = true;
                        }
                        break;
                    case 2:     // 且需要大周期上 (K[0] < K[1])                        
                        if (bpK0 < bpK1 && bCanTradeOfRatio)
                        {
                            m_gf.PrintMemo("大周期上 K[0] < K[1]");
                            bEnterShort = true;
                        }
                        break;
                    case 3:     // 且需要大周期上 {K[0] < K[1] 或 K[0] < 50}                        
                        if ((bpK0 < bpK1 || bpK0 < 50) && bCanTradeOfRatio)
                        {
                            m_gf.PrintMemo("大周期上 K[0] < K[1] 或 K[0] < 50");
                            bEnterShort = true;
                        }
                        break;
                    default:
                        bEnterShort = false;
                        break;
                }

                if (bEnterShort)    // 开空单
                {
                    if (shortPosition <= 0)
                    {                        
                        dOpenPrice = Refdata(CLOSE, iStartPos);
                        lots = CalculateLots(MaxLot, CapitalRatio, 0, dOpenPrice);      // 计算可开仓的仓位
                        m_gf.PrintMemo("满足开空条件 => 空头建仓 @ " + dOpenPrice.ToString() + " * " + lots.ToString());
                        OpenOrder(dOpenPrice, lots, Open_SlipPoint, m_PriceTick, EnumDirectionType.Sell);

                        // 发送开多指令后，若N（默认=3）根K线后还没成交，则撤单
                        m_afterOpenBarCount = 1;
                        m_iInCount++;
                    }
                    else
                    {
                        m_gf.PrintMemo("已有空头仓位 [" + shortPosition.ToString() + "]");
                    }
                }
            }
            // ------------------------------------------
            #endregion
            
            PrintTodayTradeStatus();        // 打印今日交易状态            
        }

        private void CalculateIndicator(out DataArray K, out DataArray D, out DataArray bpK, out DataArray bpD)
        {
            DataArray myC = new DataArray(CLOSE, 200);
            DataArray myL = new DataArray(LOW, 200);
            DataArray myH = new DataArray(HIGH, 200);

            DataArray RSV = (myC - LLV(myL, N)) / (HHV(myH, N) - LLV(myL, N)) * 100;
            DataArray FASTK = SMA(RSV, P1, 1);
            K = SMA(FASTK, P2, 1);
            D = SMA(K, P3, 1);

            // 计算大周期的KD值 
            DataArray myBpC = new DataArray(m_bigPeriodEV.CLOSE, 200);
            DataArray myBpL = new DataArray(m_bigPeriodEV.LOW, 200);
            DataArray myBpH = new DataArray(m_bigPeriodEV.HIGH, 200);

            DataArray bpRSV = (myBpC - LLV(myBpL, BpN)) / (HHV(myBpH, BpN) - LLV(myBpL, BpN)) * 100;
            DataArray bpFASTK = SMA(bpRSV, P1, 1);
            bpK = SMA(bpFASTK, P2, 1);
            bpD = SMA(bpK, P3, 1);

            /*
            内置微指标.测试.w_SLOWKD wskd = new 内置微指标.测试.w_SLOWKD();
            wskd.N = this.N;
            wskd.P1 = this.P1;
            wskd.P2 = this.P2;
            wskd.P3 = this.P3;

            IndicatorPackage ip = wskd.Run(m_bigPeriodEV.DataProvider);
            DataArray ipK = ip["K"];
            DataArray ipD = ip["D"];
            */
        }

        private void CalculateIndicator(ENU_CalculateModelEventType eventType = ENU_CalculateModelEventType.ONBAROPEN)
        {
            int iDataLength = 300;
            // 计算本周期的KD值
            m_Indicators.myC = new DataArray(CLOSE, iDataLength);
            m_Indicators.myL = new DataArray(LOW, iDataLength);
            m_Indicators.myH = new DataArray(HIGH, iDataLength);
                        
            DataArray RSV = (m_Indicators.myC - LLV(m_Indicators.myL, N)) / (HHV(m_Indicators.myH, N) - LLV(m_Indicators.myL, N)) * 100;
            DataArray FASTK = SMA(RSV, P1, 1);
            m_Indicators.K = SMA(FASTK, P2, 1);
            m_Indicators.D = SMA(m_Indicators.K, P3, 1);

            // 计算大周期的KD值 
            m_Indicators.myBpC = new DataArray(m_bigPeriodEV.CLOSE, iDataLength);
            m_Indicators.myBpL = new DataArray(m_bigPeriodEV.LOW, iDataLength);
            m_Indicators.myBpH = new DataArray(m_bigPeriodEV.HIGH, iDataLength);
                        
            DataArray bpRSV = (m_Indicators.myBpC - LLV(m_Indicators.myBpL, BpN)) / (HHV(m_Indicators.myBpH, BpN) - LLV(m_Indicators.myBpL, BpN)) * 100;
            DataArray bpFASTK = SMA(bpRSV, P1, 1);
            m_Indicators.bpK = SMA(bpFASTK, P2, 1);
            m_Indicators.bpD = SMA(m_Indicators.bpK, P3, 1);
            
            // 根据不同的事件类型来获取模型需要的数据
            m_Indicators.bpK0 = m_Indicators.bpK.LASTDATA;
            m_Indicators.bpD0 = m_Indicators.bpD.LASTDATA;
            m_Indicators.bpK1 = Refdata(m_Indicators.bpK, 1);
            m_Indicators.bpD1 = Refdata(m_Indicators.bpD, 1);

            switch (eventType)
            {
                case ENU_CalculateModelEventType.ONBAROPEN:     // K线 Open事件
                    m_Indicators.startPos = 1;
                    m_Indicators.K0 = Refdata(m_Indicators.K, 1);
                    m_Indicators.K1 = Refdata(m_Indicators.K, 2);
                    m_Indicators.K2 = Refdata(m_Indicators.K, 3);
                    m_Indicators.D0 = Refdata(m_Indicators.D, 1);
                    m_Indicators.D1 = Refdata(m_Indicators.D, 2);
                    break;
                case ENU_CalculateModelEventType.ONBARCLOSE:    // K线 Close事件
                    m_Indicators.startPos = 0;
                    m_Indicators.K0 = m_Indicators.K.LASTVALUE;
                    m_Indicators.K1 = Refdata(m_Indicators.K, 1);
                    m_Indicators.K2 = Refdata(m_Indicators.K, 2);
                    m_Indicators.D0 = m_Indicators.D.LASTVALUE;                    
                    m_Indicators.D1 = Refdata(m_Indicators.D, 1);
                    break;
                default:                                        // 其它
                    m_Indicators.startPos = 1;
                    m_Indicators.K0 = Refdata(m_Indicators.K, 1);
                    m_Indicators.K1 = Refdata(m_Indicators.K, 2);
                    m_Indicators.K2 = Refdata(m_Indicators.K, 3);
                    m_Indicators.D0 = Refdata(m_Indicators.D, 1);
                    m_Indicators.D1 = Refdata(m_Indicators.D, 2);
                    break;
            }
            
            // 计算ATR移动止损价格            
            double hhvValue = HhvData(this.HIGH, 5, 1);
            double llvValue = LlvData(this.LOW, 5, 1);
            m_Indicators.Atr_PreValue = AtrData(this, ST_AtrLength, 1); 

            m_Indicators.LongStopLossPrice = hhvValue - m_Indicators.Atr_PreValue * ST_Factor;
            m_Indicators.ShortStopLossPrice = llvValue + m_Indicators.Atr_PreValue * ST_Factor;
        }

        /// <summary>
        /// 加载本品种数据，包括本周期和大周期的
        /// </summary>
        private bool LoadMyData()
        {
            bool bLoadReault = true;

            m_gf.PrintMemo("计算本周期所对应的大周期", ENU_msgType.msg_Info);
            CalculateBigPeriod();
            m_gf.PrintMemo("  -> BigPeriod = " + m_bigPeriodRepeat.ToString() + m_bigPeriod.ToString());

            m_gf.PrintMemo("加载本周期历史数据[" + DataCycle.ToString() + " " + SYMBOL + "]", ENU_msgType.msg_Info);
            UnLoadHisData();
            LoadHisData(SYMBOL, DataCycle.CycleBase, DataCycle.Repeat, 500);

            if (m_bigPeriodRepeat > 0)
            {
                if (m_bigPeriodEV != null)
                {
                    m_bigPeriodEV.UnLoadHisData();
                    m_bigPeriodEV.Dispose();
                    m_bigPeriodEV = null;
                }

                if (m_bigPeriodEV == null)
                { 
                    m_bigPeriodEV = CreateSymbolData(SYMBOL, m_bigPeriod, m_bigPeriodRepeat);        // 加载本品种大周期的数据
                }

                m_gf.PrintMemo("加载大周期历史数据[" + m_bigPeriodEV.DataCycle.ToString() + " " + SYMBOL + "]", ENU_msgType.msg_Info);                
                m_bigPeriodEV.LoadHisData(m_bigPeriod, m_bigPeriodRepeat, 500);

                //this.time
                PrintLoadDataResult();

                // 检查加载后的数据是否正常
                if (m_bigPeriodEV != null && this.CLOSE.Length > 100 && m_bigPeriodEV.CLOSE.Length > 100)
                {
                    bLoadReault = true;
                }
                else
                {
                    bLoadReault = false;
                }
            }
            else
            {
                bLoadReault = false;
                m_gf.PrintMemo("计算本周期所对应的大周期发生错误", ENU_msgType.msg_Error);
            }

            return bLoadReault;
        }

        /// <summary>
        /// 计算可开仓的头寸
        /// </summary>
        /// <param name="maxLots">最大头寸</param>
        /// <param name="capitalRatio">
        /// 资金使用比率:
        ///     [0, 1]: 采用百分比计算方法
        ///     > 1 : 采用固定资金计算方法
        /// </param>
        /// <param name="totalCapital"></param>
        /// <param name="openPrice"></param>
        /// <returns></returns>
        private int CalculateLots(int maxLots, double capitalRatio, double totalCapital, double openPrice)
        {
            int lots = 0;
            
            if (capitalRatio < 0) capitalRatio = 0;
            
            if (totalCapital <= 0)
            {
                // 计算可用的资金
                FuturesAccount fa = GetFuturesAccount();
                totalCapital = fa.DynamicProfit;
            }
            
            if (capitalRatio <= 1)      
            {
                // 百分比计算方法
                // 头寸 = 资金 * 资金使用比率 / (开仓价 * 每手合约乘数 * 保证金率)
                if (openPrice > 0)
                { 
                    lots =(int) Math.Round(totalCapital * capitalRatio / (openPrice * INSTRUMENT.VolumeMultiple * Math.Max(INSTRUMENT.LongMarginRatioByMoney, INSTRUMENT.ShortMarginRatioByMoney)), 0);
                }
            }
            else
            {
                // 固定资金计算方法
                // 头寸 = 资金 / 资金使用比率 
                lots = (int)Math.Round(totalCapital / capitalRatio);
            }

            // 不能超过最大可开仓头寸设置
            if (lots > MaxLot) lots = maxLots;
            if (lots <= 1) lots = 1;

            return lots;
        }

        /// <summary>
        /// 重新发送未成交平仓单
        /// </summary>
        /// <param name="order"></param>
        /// <param name="slipPoint"></param>
        /// <param name="priceTick"></param>
        /// <returns></returns>
        public int ResendCloseOrder(Order order, int slipPoint, double priceTick)
        {
            double price = 0;
            
            if ((order.VolumeTotalOriginal - order.VolumeTraded) > 0)
            {
                if (order.OrderDirection == EnumDirectionType.Buy)
                {
                    price = ASKPRICE(1).LASTDATA;
                }
                else if (order.OrderDirection == EnumDirectionType.Sell)
                {
                    price = BIDPRICE(1).LASTDATA;
                }

                // TODO 未测试
                return OpenOrder(price, order.VolumeTotalOriginal - order.VolumeTraded, slipPoint, priceTick, order.OrderDirection);
                //return 1;
            }
            else
            {
                m_gf.PrintMemo("委托剩余量为0，不需要重发");
                return -1;
            }
        }

        /// <summary>
        /// 获取本策略所有未成交的开仓单
        /// </summary>
        /// <returns></returns>
        private List<Order> GetUnTraded_OpenOrder()
        {
            List<Order> unTradedOrdLst = new List<Order>();
            List<Order> ordLst = GetUnAllTradedOrderList(SYMBOL);
            if (ordLst != null && ordLst.Count > 0)
            {
                m_gf.PrintMemo("有未成交单[开仓] = " + ordLst.Count.ToString());
                foreach (Order order in ordLst)
                {
                    if (ORDER_KEY_LIST.Contains(order.Key))
                    {

                        if ((order.OffsetFlag == EnumOffsetFlagType.Open) &&
                            (order.OrderStatus == EnumOrderStatusType.NoTradeQueueing || order.OrderStatus == EnumOrderStatusType.PartTradedQueueing))
                        {
                            unTradedOrdLst.Add(order);
                        }                        
                    }
                }
            }

            return unTradedOrdLst;
        }

        /// <summary>
        /// 获取本策略所有未成交的平仓单
        /// </summary>
        /// <returns></returns>
        private List<Order> GetUnTraded_CloseOrder()
        {
            List<Order> unTradedOrdLst = new List<Order>();
            List<Order> ordLst = GetUnAllTradedOrderList(SYMBOL);

            if (ordLst != null && ordLst.Count > 0)
            {
                m_gf.PrintMemo("有未成交单[平仓] = " + ordLst.Count.ToString());
                foreach (Order order in ordLst)
                {
                    if (ORDER_KEY_LIST.Contains(order.Key))
                    {

                        if ((order.OffsetFlag == EnumOffsetFlagType.Close || order.OffsetFlag == EnumOffsetFlagType.CloseToday || order.OffsetFlag == EnumOffsetFlagType.CloseYesterday) &&
                            (order.OrderStatus == EnumOrderStatusType.NoTradeQueueing || order.OrderStatus == EnumOrderStatusType.PartTradedQueueing))
                        {
                            unTradedOrdLst.Add(order);
                        }
                    }
                }
            }

            return unTradedOrdLst;
        }

        /// <summary>
        /// 发送开仓指令
        /// </summary>
        /// <param name="price"></param>
        /// <param name="volume"></param>
        /// <param name="slipPoint"></param>
        /// <param name="priceTick"></param>
        /// <param name="direction"></param>
        /// <returns></returns>
        private int OpenOrder(double price, int volume, int slipPoint, double priceTick, EnumDirectionType direction)
        {
            double orderPrice = 0;
            int returnValue = 0;

            if (direction == EnumDirectionType.Buy)         // 开多单
            {
                orderPrice = price + slipPoint * priceTick;
                returnValue = OpenBuy(orderPrice, volume);
            }
            else if (direction == EnumDirectionType.Sell)   // 开空单
            {
                orderPrice = price - slipPoint * priceTick;
                returnValue = OpenSell(orderPrice, volume);
            }

            return returnValue;
        }

        
        /// <summary>
        /// 获取本策略本品种的所有持仓
        /// </summary>
        private void GetPosition()
        {
            // 获取本策略开仓的具体仓位            
            List<Position> lp = GetMyPositionList();
            int longPosition = CalculateLongPosition(lp, out m_longPosition_TD, out m_longPosition_YD);
            int shortPosition = CalculateShortPosition(lp, out m_shortPosition_TD, out m_shortPosition_YD);
                        
            //PrintMemo("获取" + SYMBOL + "[MyID: " + MyID + "]持仓头寸: ", ENU_msgType.msg_Info);
            //PrintMemo("  -> {多[今，昨], 空[今，昨]} = {" + longPosition.ToString() + "[" + m_longPosition_TD + ", " + m_longPosition_YD + "], " +
            //    shortPosition.ToString() + "[" + m_shortPosition_TD + ", " + m_shortPosition_YD + "]}");            
            m_gf.PrintMemo("获取[" + SYMBOL + "]持仓头寸: [多, 空] = [" + longPosition + ", " + shortPosition + "]", ENU_msgType.msg_Info);
        }
                

        /// <summary>
        /// 平多头
        /// </summary>
        /// <param name="price"></param>
        /// <param name="volume"></param>
        /// <param name="slipPoint"></param>
        /// <param name="priceTick"></param>
        /// <param name="orderType"></param>
        private void CloseLongPosition(double price, int volume, int slipPoint, double priceTick, EnumOrderType orderType = EnumOrderType.限价单)
        {
            CloseFuturesPositions(SYMBOL, EnumDirectionType.Buy, price, volume, slipPoint, orderType, EnumHedgeFlag.投机);
            m_bCloseTimeOutStart = true;        // 开始平仓未成交超时计时
        }

        /// <summary>
        /// 平空头
        /// </summary>
        /// <param name="price"></param>
        /// <param name="volume"></param>
        /// <param name="slipPoint"></param>
        /// <param name="priceTick"></param>
        /// <param name="orderType"></param>
        private void CloseShortPosition(double price, int volume, int slipPoint, double priceTick, EnumOrderType orderType = EnumOrderType.限价单)
        {
            CloseFuturesPositions(SYMBOL, EnumDirectionType.Sell, price, volume, slipPoint, orderType, EnumHedgeFlag.投机);
            m_bCloseTimeOutStart = true;        // 开始平仓未成交超时计时
        }
              
        /// <summary>
        /// 获取本策略开仓的仓位
        /// </summary>
        /// <returns></returns>
        private List<Position> GetMyPositionList()
        {
            return GetPositionList(SYMBOL);
        }

        /// <summary>
        /// 计算多头仓位
        /// </summary>
        /// <param name="posList"></param>
        /// <param name="position_TD"></param>
        /// <param name="position_YD"></param>
        /// <returns></returns>
        private int CalculateLongPosition(List<Position> posList, out int position_TD, out int position_YD)
        {
            int lots = 0;
            position_TD = 0;
            position_YD = 0;

            if (posList == null)
            {
                return -1;
            }

            foreach(Position pos in posList)
            {
                if (pos.PosiDirection == EnumPosiDirectionType.Long)
                {
                    position_TD += pos.TodayPosition;
                    position_YD += pos.YdPosition;
                }
            }

            lots = position_TD + position_YD;

            return lots;
        }

        /// <summary>
        /// 计算空头仓位
        /// </summary>
        /// <param name="posList"></param>
        /// <param name="position_TD"></param>
        /// <param name="position_YD"></param>
        /// <returns></returns>
        private int CalculateShortPosition(List<Position> posList, out int position_TD, out int position_YD)
        {
            int lots = 0;
            position_TD = 0;
            position_YD = 0;

            if (posList == null)
            {
                return -1;
            }

            foreach (Position pos in posList)
            {
                if (pos.PosiDirection == EnumPosiDirectionType.Short)
                {
                    position_TD += pos.TodayPosition;
                    position_YD += pos.YdPosition;
                }
            }

            lots = position_TD + position_YD;
            return lots;
        }

        private void MySendOrder(int jump, int Qty, EnumDirectionType direction, EnumOffsetFlagType oc, double PriceTick)
        {

            double ordPrice = C.LASTDATA;
            if (direction == EnumDirectionType.Buy)
            {
                ordPrice = BIDPRICE(1).LASTDATA + jump * PriceTick;
                if (oc == EnumOffsetFlagType.Open)
                    OpenBuy(ordPrice, Qty);
                else
                    CloseBuy(ordPrice, Qty);
            }
            else
            {
                ordPrice = ASKPRICE(1).LASTDATA - jump * PriceTick;
                if (oc == EnumOffsetFlagType.Open)
                    OpenSell(ordPrice, Qty);
                else
                    CloseSell(ordPrice, Qty);
            }

            PrintLine(TickNow.ToString("HH:mm:ss.fff") + "->" + "下单" + Qty + "手" + oc.ToString() + direction.ToString() + SYMBOL
                + "@" + ordPrice.ToString() + ",t=" + SERVERTIME.ToString());
        }

        // 获取本周期的大周期，具体如下：
        //      本周期         大周期
        //      1 Min           5 Min
        //      5 Min           30 Min
        //      15 Min          60 Min
        //      30 Min          1 Day
        //      60 Min          1 Day
        //      1 Day           1 Week 
        private void CalculateBigPeriod()
        {

            if (DataProvider.DataCycle.CycleBase == EnumDataCycle.MINUTE)
            {
                switch ((int)DataProvider.DataCycle.Repeat)
                {
                    case 1:         // 1 Min -> 5 Min
                        m_bigPeriod = EnumDataCycle.MINUTE;
                        m_bigPeriodRepeat = 5;
                        break;
                    case 5:         // 5 Min -> 30 Min
                        m_bigPeriod = EnumDataCycle.MINUTE;
                        m_bigPeriodRepeat = 30;
                        break;
                    case 10:        // 10 Min -> 60 Min
                    case 15:        // 15 Min -> 60 Min
                        m_bigPeriod = EnumDataCycle.MINUTE;
                        m_bigPeriodRepeat = 60;
                        break;
                    case 30:        // 30 Min -> 1 Day
                    case 60:        // 60 Min -> 1 Day
                        m_bigPeriod = EnumDataCycle.DAY;
                        m_bigPeriodRepeat = 1;
                        break;                    
                    default :
                        m_bigPeriodRepeat = -1;
                        break;
                }
            }
            else if (DataProvider.DataCycle.CycleBase == EnumDataCycle.HOUR)
            { 
                switch ((int)DataProvider.DataCycle.Repeat)
                {
                    case 1:     // 1 Hour -> 1 Day                        
                    case 2:     // 2 Hour -> 1 Day
                        m_bigPeriod = EnumDataCycle.DAY;
                        m_bigPeriodRepeat = 1;
                        break;
                    default:
                        m_bigPeriodRepeat = -1;
                        break;
                }
            }
            else if (DataProvider.DataCycle.CycleBase == EnumDataCycle.DAY)
            {
                switch ((int)DataProvider.DataCycle.Repeat)
                {
                    case 1:     // 1 Day -> 1 Week
                        m_bigPeriod = EnumDataCycle.WEEK;
                        m_bigPeriodRepeat = 1;
                        break;
                    default:
                        m_bigPeriodRepeat = -1;
                        break;
                }
            }
            else
            {
                m_bigPeriodRepeat = -1;    
            }            
        }

        private void WriteTradeLog(Trade trade)
        {
            FileStream fs = null;
            StreamWriter sw = null;
            
            try
            {
                string path = "c:\\trade.log";
                string msg = "" + SYMBOL + ": " + TickNow.ToString("yyyy-MM-dd HH:mm:ss") + " " + trade.Direction.ToString() + trade.OffsetFlag.ToString() + " " + trade.Price.ToString() + " * " + trade.Volume + " @ " +
                    trade.TradeTime + "[" + trade.TradeID + ", " + trade.OrderSysID + "]";

                fs = new FileStream(path, FileMode.Append);
                sw = new StreamWriter(fs);

                sw.WriteLine(msg);
                sw.Flush();
            }
            catch (Exception e)
            {
                m_gf.PrintMemo("写交易日志文件错误: " + e.Message, ENU_msgType.msg_Error);
            }
            finally
            { 
                if (sw != null) sw.Close();
                if (fs != null) fs.Close();
            }
        }

        /*
        private void PrintMemo(string msg, ENU_msgType msgType = ENU_msgType.msg_Info, bool bPrintTime = true)
        {
            string sType = "";
            //string msgTime = Now.ToString("HH:mm:ss");
            string msgTime = TickNow.ToString("HH:mm:ss");

            switch(msgType)
            {
                case ENU_msgType.msg_Info:
                    sType = "[I][" + SYMBOL + "]";
                    break;
                case ENU_msgType.msg_Error:
                    sType = "[R][" + SYMBOL + "]";
                    break;
                default:
                    sType = "[U][" + SYMBOL + "]";
                    break;
            }

            if (bPrintTime)
            {
                PrintLine(sType + msgTime + ": " + msg);
            }
            else
            {
                PrintLine(sType + msg);
            }
        }
         */ 

        private void PrintKD()
        {
            string msg = "";

            msg = SYMBOL + "[" + MyID + "]\t" + "K0\t" + "D0\t" + "K1\t" + "D1\t" + "K斜率\t" + "K率[预]\t" + "H\t" + "L\t" + "C\t" + "多损\t" + "空损\t" + "ATR";
            PrintLine(msg);

            msg = "\t现周期\t" + Math.Round(m_Indicators.K0, 3).ToString() + "\t" + Math.Round(m_Indicators.D0, 3).ToString() + "\t" + Math.Round(m_Indicators.K1, 3).ToString() + "\t" +
               Math.Round(m_Indicators.D1, 3).ToString() + "\t" + Math.Round(m_Indicators.KRatio, 3).ToString() + "\t\t" + Refdata(m_Indicators.myH, m_Indicators.startPos).ToString() + "\t" +
               Refdata(m_Indicators.myL, m_Indicators.startPos).ToString() + "\t" + Refdata(m_Indicators.myC, m_Indicators.startPos).ToString() + "\t" +
               Math.Round(m_Indicators.LongStopLossPrice, 2).ToString() + "\t" + Math.Round(m_Indicators.ShortStopLossPrice, 2).ToString() + "\t" +
               Math.Round(m_Indicators.Atr_PreValue, 2).ToString();

            PrintLine(msg);

            msg = "\t大周期\t" + Math.Round(m_Indicators.bpK0, 3).ToString() + "\t" + Math.Round(m_Indicators.bpD0, 3).ToString() + "\t" + Math.Round(m_Indicators.bpK1, 3).ToString() + "\t" +
                Math.Round(m_Indicators.bpD1, 3).ToString() + "\t" + Math.Round(m_Indicators.bpKRatio, 3).ToString() + "\t" + Math.Round(m_Indicators.bpKRatio_PREDICT, 3).ToString() + "\t" +
                m_Indicators.myBpH.LASTDATA.ToString() + "\t" + m_Indicators.myBpL.LASTDATA.ToString() + "\t" + m_Indicators.myBpC.LASTDATA.ToString();
            PrintLine(msg);
        }

        private void PrintKD(DataArray K, DataArray D, DataArray bpK, DataArray bpD)
        {
            string msg = "";

            msg = "\t" +  SYMBOL + "\t" + "K0\t" + "D0\t" + "K1\t" + "D1";
            PrintLine(msg);

            msg = "\t现周期\t" + Math.Round(Refdata(K, 1), 3).ToString() + "\t" + Math.Round(Refdata(D, 1), 3).ToString() + "\t" +
                Math.Round(Refdata(K, 2), 3).ToString() + "\t" + Math.Round(Refdata(D, 2), 3).ToString();
            PrintLine(msg);

            msg = "\t大周期\t" + Math.Round(bpK.LASTVALUE, 2).ToString() + "\t" + Math.Round(bpD.LASTVALUE, 2).ToString() + "\t" +
                Math.Round(Refdata(bpK, 1), 2).ToString() + "\t" + Math.Round(Refdata(bpD, 1), 2).ToString();
            PrintLine(msg);                      
        }

        private void PrintKD(double K0, double D0, double K1,double D1, double bpK0, double bpD0, double bpK1, double bpD1, double KRatio, double bpKRatio)
        {
            string msg = "";

            msg = SYMBOL + "[" + MyID + "]\t" + "K0\t" + "D0\t" + "K1\t" + "D1\t" + "K斜率";
            PrintLine(msg);

            msg = "\t现周期\t" + Math.Round(K0, 2).ToString() + "\t" + Math.Round(D0, 2).ToString() + "\t" + Math.Round(K1, 2).ToString() + "\t" + Math.Round(D1, 2).ToString() + "\t" + Math.Round(KRatio, 2).ToString();
            PrintLine(msg);

            msg = "\t大周期\t" + Math.Round(bpK0, 2).ToString() + "\t" + Math.Round(bpD0, 2).ToString() + "\t" + Math.Round(bpK1, 2).ToString() + "\t" + Math.Round(bpD1, 2).ToString() + "\t" + Math.Round(bpKRatio, 2).ToString();
            PrintLine(msg);            
        }

        private void PrintLoadDataResult()
        {
            string msg = "";
            BarData bar0, bar1, bpBar0, bpBar1;

            if (m_bigPeriodEV != null)
            {
                m_gf.PrintMemo("加载数据结束:");

                msg = "\t\t" + "本周期[" + CLOSE.Length.ToString() + "]\t\t" + "\t大周期[" + m_bigPeriodEV.CLOSE.Length.ToString() + "]";
                m_gf.PrintMemo(msg);

                msg = "\t\t" + "C[0]\t\t" + "C[1]\t\t" + "bpC[0]\t\t" + "bpC[1]";
                m_gf.PrintMemo(msg);

                bar0 = this.GetBarData(BARCOUNT - 1);
                bar1 = this.GetBarData(BARCOUNT - 2);
                bpBar0 = m_bigPeriodEV.GetBarData(m_bigPeriodEV.BARCOUNT - 1);
                bpBar1 = m_bigPeriodEV.GetBarData(m_bigPeriodEV.BARCOUNT - 2);

                //msg = "\t时间:\t" + TIME.LASTDATA.ToString() + "\t\t" + REF(TIME, 1).LASTDATA.ToString() + "\t\t" + m_bigPeriodEV.TIME.LASTDATA.ToString() + "\t\t" + REF(m_bigPeriodEV.TIME, 1).LASTDATA.ToString();
                //m_gf.PrintMemo(msg);
                msg = "\t时间:\t" + bar0.CurDateTime.ToString("HH:mm:ss") + "\t" + bar1.CurDateTime.ToString("HH:mm:ss") + "\t" + bpBar0.CurDateTime.ToString("HH:mm:ss") + "\t" + bpBar1.CurDateTime.ToString("HH:mm:ss");
                m_gf.PrintMemo(msg);
                
                msg = "\t数值:\t" + bar0.Close.ToString() + "\t\t" + bar1.Close.ToString() + "\t\t" + bpBar0.Close.ToString() + "\t\t" + bpBar1.Close.ToString();
                m_gf.PrintMemo(msg);
            }
        }
        
        private void PrintTodayTradeStatus()
        {
            string msg = "";

            msg = "入场次数 = " + m_iInCount.ToString() + "\t平仓次数 = " + m_iOutCount.ToString();
            m_gf.PrintMemo(msg);
        }

        /// <summary>
        /// 向前引用第N个数据
        /// </summary>
        /// <param name="f"></param>
        /// <param name="N"></param>
        /// <returns></returns>
        public double Refdata(DataArray f, int N)
        {
            double value = double.NaN;

            if (N < 0) N = 0;            
            if (f.IsNaN()) return value;
            if (f.Length < N) return value;

            value = f[f.Length - N - 1];

            return value;
        }

        /// <summary>
        /// 计算K线的平均真实波动幅度
        /// </summary>
        /// <param name="ev">K线数据</param>
        /// <param name="atrLength">ATR长度</param>
        /// <param name="offset">偏移量（从第几根K线起[最新一根为0])</param>
        /// <returns></returns>
        public double AtrData(EventStrategyBase ev, int atrLength, int offset)
        {
            double atrValue = 0;
            int loop = 0;            
            double tmpPreClose = 0;
            double tmpHigh = 0;
            double tmpLow = 0;
            double sumTR = 0;

            if (ev.CLOSE.Length >= atrLength + offset + 1)   // 数据量足够
            {
                loop = atrLength;
            }
            else
            {
                loop = ev.CLOSE.Length - offset - 1;
            }

            if (loop > 0)
            {
                //double[] trs = new double[loop];
                for (int i = 0; i < loop; i++)
                {
                    tmpHigh = Refdata(ev.HIGH, offset + i);
                    tmpLow = Refdata(ev.LOW, offset + i);
                    tmpPreClose = Refdata(ev.CLOSE, offset + i + 1);

                    sumTR += MAX(ABS(tmpHigh - tmpLow), ABS(tmpPreClose - tmpHigh), ABS(tmpPreClose - tmpLow));
                }

                atrValue = sumTR / loop;
            }

            return atrValue;
        }

        /// <summary>
        /// 求数组中区间长度的得最大值
        /// </summary>
        /// <param name="f">数组</param>
        /// <param name="hhvLength">区间长度（从Offset偏移量开始算起）</param>
        /// <param name="offset">偏移量</param>
        /// <returns></returns>
        public double HhvData(DataArray f, int hhvLength, int offset)
        {
            double hhvValue = 0;
            int loop = 0;

            if (f.Length >= hhvLength + offset)
            {
                loop = hhvLength;
            }
            else
            {
                loop = f.Length - offset;
            }

            if (loop > 0)
            {
                for (int i = 0; i < loop; i++)
                {
                    double tmpData = Refdata(f, offset + i);
                    if (hhvValue < tmpData) hhvValue = tmpData;
                }
            }

            return hhvValue;
        }

        public double LlvData(DataArray f, int llvLength, int offset)
        {
            double llvValue = 0;
            int loop = 0;

            if (f.Length >= llvLength + offset)
            {
                loop = llvLength;
            }
            else
            {
                loop = f.Length - offset;
            }

            if (loop > 0)
            {
                for (int i = 0; i < loop; i++)
                {
                    double tmpData = Refdata(f, offset + i);
                    if (llvValue <= 0) llvValue = tmpData;
                    if (llvValue > tmpData) llvValue = tmpData;
                }
            }

            return llvValue;
        }
    }
}
