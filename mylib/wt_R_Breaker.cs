using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DevelopLibrary.DevelopAPI;
using DevelopLibrary.Enums;

namespace mylib
{
    public class wt_R_Breaker : EventStrategyBase
    {
        #region 参数
        [Parameter(Display = "k1", Description = "", Category = "参数")]
        public double k1 = 0.4;

        [Parameter(Display = "k2", Description = "", Category = "参数")]
        public double k2 = 0.05;

        [Parameter(Display = "k3", Description = "", Category = "参数")]
        public double k3 = 0.2;
        #endregion

        /// <summary>
        /// 总开仓数量
        /// </summary>
        public int KN = 0;

        /// <summary>
        /// 开仓成交数量
        /// </summary>
        public int KN_Trd = 0;

        /// <summary>
        /// 平仓成交数量
        /// </summary>
        public int PN = 0;

        /// <summary>
        /// 上次交易时间
        /// </summary>
        public DateTime T0;

        /// <summary>
        /// 开盘时间
        /// </summary>
        public DateTime tOpen
        {
            get { return new DateTime(SERVERTIME.Year, SERVERTIME.Month, SERVERTIME.Day, 9, 0, 0); }
        }

        /// <summary>
        /// 收盘时间
        /// </summary>
        public DateTime tClose
        {
            get { return new DateTime(SERVERTIME.Year, SERVERTIME.Month, SERVERTIME.Day, 15, 0, 0); }
        }

        /// <summary>
        /// 上午开始休息时间
        /// </summary>
        public DateTime RestT0
        {
            get { return new DateTime(SERVERTIME.Year, SERVERTIME.Month, SERVERTIME.Day, 11, 30, 0); }
        }

        /// <summary>
        /// 下午开始交易时间
        /// </summary>
        public DateTime RestT1
        {
            get { return new DateTime(SERVERTIME.Year, SERVERTIME.Month, SERVERTIME.Day, 13, 30, 0); }
        }

        /// <summary>
        /// Tick时间戳
        /// </summary>
        public DateTime TickNow
        {
            get { return SERVERTIME; }
        }

        /// <summary>
        /// 最新价
        /// </summary>
        public double LastPrice = 0;
        /// <summary>
        /// 为0时没有开仓，为1时说明已经开仓买入，为-1说明已经开仓卖出
        /// </summary>
        public int Flag = 0;
        /// <summary>
        /// 停止交易的时间
        /// </summary>
        public DateTime tFinish;

        public double PriceTick;
        public int VolumeMultiple;
        public bool IfFinish = false;

        /// <summary>
        /// 开仓下单后等待撤单的秒数
        /// </summary>
        public double KCBC = 5;
        /// <summary>
        /// 平仓下单后等待撤单的秒数
        /// </summary>
        public double PCBC = 2;

        public double 收盘秒 = 60;

        /// <summary>
        /// 下单量
        /// </summary>
        public int Qty = 1;

        /// <summary>
        /// 是否开仓追
        /// </summary>
        public bool IfFKC = true;

        /// <summary>
        /// 开仓滑点跳数
        /// </summary>
        public int KCJP = 2;

        /// <summary>
        /// 平仓滑点跳数
        /// </summary>
        public int PCJP = 2;

        #region 核心逻辑

        #region 6个关键位置

        bool InitOk = false;

        /// <summary>
        /// 突破买入
        /// </summary>
        double BBreak;

        /// <summary>
        /// 观察卖出
        /// </summary>
        double SSetup;

        /// <summary>
        /// 反转卖出
        /// </summary>
        double SEnter;

        /// <summary>
        /// 反转买入
        /// </summary>
        double BEnter;

        /// <summary>
        /// 观察买入
        /// </summary>
        double BSetup;

        /// <summary>
        /// 突破卖出
        /// </summary>
        double SBreak;

        #endregion

        /// <summary>
        /// 最近是否到过高点
        /// </summary>
        bool IfSeeHigh = false;

        /// <summary>
        /// 最近是否到过低点
        /// </summary>
        bool IfSeeLow = false;

        /// <summary>
        /// 止损位置
        /// </summary>
        public double 止损位 = 0;

        public override void OnStart()
        { 
            PrintLine("交易所是" + INSTRUMENT.ExchangeID.ToString());
            tFinish = tClose.AddSeconds(-5 * 收盘秒);//在收盘前5分钟清盘
            IfFKC = true;
            PrintLine("开仓追单=" + IfFKC);

            PriceTick = INSTRUMENT.PriceTick;
            VolumeMultiple = INSTRUMENT.VolumeMultiple;

            T0 = tOpen;

            LoadHisData(EnumDataCycle.DAY, 1);//加载历史数据

            DataArray DateLine = FML(DataProvider, "DATE", EnumDataCycle.DAY, 1, false);//获得日线数据，false表示无需与当前策略数据长度等长    
            DataArray DayLineO = FML(DataProvider, "OPEN", EnumDataCycle.DAY, 1, false);//            
            DataArray DayLineH = FML(DataProvider, "HIGH", EnumDataCycle.DAY, 1, false);
            DataArray DayLineL = FML(DataProvider, "LOW", EnumDataCycle.DAY, 1, false);
            DataArray DayLineC = FML(DataProvider, "CLOSE", EnumDataCycle.DAY, 1, false);

            string TradeDate = TRADEDAY.ToString("yyyyMMdd");
            int preDay_index = 0;

            for (int i = DateLine.Length - 1; i > -1; i--)//用于查找当前交易日的前一个交易日，如果不这样查找的话，在数据还没有来的时候，会找到大前天的日期。
            {
                if (DateLine[i].ToString().CompareTo(TradeDate) < 0)
                {
                    preDay_index = i;
                    break;
                }
            }

            if (DayLineC.Length >= 2 && Qty > 0)
            {
                // 计算6个关键位置
                InitOk = true;
                PrintLine("昨天的收盘线=" + DayLineC[preDay_index].ToString());

                // 计算今天的关键价格:
                // 观察买入价(Bsetup) = 昨低 - k1 * (昨高 - 昨收)
                // 观察卖出价(Ssetup) = 昨高 + k1 * (昨收 - 昨低)
                // 反转买入价(Benter) = (1 + k2) / 2 * (昨高 + 昨低) - K2 * 昨高
                // 反转卖出价(Senter) = (1 + k2) / 2 * (昨高 + 昨低) - k2 * 昨低
                // 突破买入价(Bbreak) = Ssetup + k3 * (Ssetup - Bsetup)
                // 突破卖出价(Sbreak) = Bsetup - K3 * (Ssetup - Bsetup)

                double YdOpen = DayLineO[preDay_index];
                double YdHigh = DayLineH[preDay_index];
                double YdLow = DayLineL[preDay_index];
                double YdClose = DayLineC[preDay_index];

                BSetup = YdLow - k1 * (YdHigh - YdClose);
                SSetup = YdHigh + k1 * (YdClose - YdLow);

                BEnter = (1 + k2) * (YdHigh + YdLow) / 2 - k2 * YdHigh;
                SEnter = (1 + k2) * (YdHigh + YdLow) / 2 - k2 * YdLow;                

                BBreak = SSetup + k3 * (SSetup - BSetup);
                SBreak = BSetup - k3 * (SSetup - BSetup);

                SSetup = Math.Round(SSetup, 2);
                SEnter = Math.Round(SEnter, 2);
                BEnter = Math.Round(BEnter, 2);
                BSetup = Math.Round(BSetup, 2);
                BBreak = Math.Round(BBreak, 2);
                SBreak = Math.Round(SBreak, 2);

                CreateChart("基础.MAIN");
                DrawHorizonLine(BBreak);
                DrawHorizonLine(SSetup);
                DrawHorizonLine(SEnter);
                DrawHorizonLine(BEnter);
                DrawHorizonLine(BSetup);
                DrawHorizonLine(SBreak);

                PrintLine("突破买入=" + BBreak);
                PrintLine("观察卖出=" + SSetup);
                PrintLine("反转卖出=" + SEnter);
                PrintLine("反转买入=" + BEnter);
                PrintLine("观察买入=" + BSetup);
                PrintLine("突破卖出=" + SBreak);
                
            }
            else
            {
                InitOk = false;
                PrintLine("初始化失败,DayLine.Length=" + DayLineC.Length + ",Qty=" + Qty);
            }
        }

        public override void OnTick(TickData td)
        {
            if (!InitOk)
            {
                return;
            }

            LastPrice = Math.Round((ASKPRICE(1).LASTDATA + BIDPRICE(1).LASTDATA) / 2, 2);

            // 委托不成交，立刻撤单重发
            List<Order> ordLst = GetUnAllTradedOrderList(SYMBOL);
            if (ordLst != null && ordLst.Count > 0)
            {
                //说明有可撤单
                //注意看止损
                foreach (Order order in ordLst)
                {
                    if (ORDER_KEY_LIST.Contains(order.Key))//说明是本策略下的订单
                    {                       
                        double Dt = (TickNow - ConvertToDateTime(order.InsertDate, order.InsertTime)).TotalSeconds;
                        if (TickNow.ToString("HH:mm:ss").CompareTo(RestT1.ToString("HH:mm:ss")) > 0 && order.InsertTime.Substring(0, 8).CompareTo(RestT0.ToString("HH:mm:ss")) < 0)//时间已到下午，但是order又是在上午下的单
                        {
                            Dt = Dt - (RestT1 - RestT0).TotalSeconds;//除去中午休息时间
                        }

                        if (order.OrderStatus == EnumOrderStatusType.NoTradeQueueing || order.OrderStatus == EnumOrderStatusType.PartTradedQueueing)
                        {
                            if (Dt > KCBC)//超时未成交
                            {
                                CancelOrder(order);
                                if (!IfFKC)
                                {
                                    KN -= (order.VolumeTotalOriginal - order.VolumeTraded);//更新总开仓的剩余数量
                                    if (PN >= KN_Trd)
                                    {
                                        Clear();
                                    }
                                    else
                                    {
                                        PrintLine("开仓超时，但是PN=" + PN + ",KN_Trd=" + KN_Trd + ",已开的仓还没有全平。");
                                    }
                                }
                            }
                        }                       
                    }
                }
            }

            if (TickNow.ToString("HH:mm:ss").CompareTo(tFinish.ToString("HH:mm:ss")) < 0)
            { 
                // 首先确认是否经过高点或者地点
                if (!IfSeeHigh)
                {
                    if (LastPrice >= SSetup)
                    {
                        IfSeeHigh = true;
                        PrintLine(TickNow.ToString("HH:mm:ss.fff") + "->" + "最新价" + LastPrice + ">SSetup=" + SSetup + "，设定进入观察卖空");
                    }
                }

                if (!IfSeeLow)
                {
                    if (LastPrice <= BSetup)
                    {
                        IfSeeLow = true;
                        PrintLine(TickNow.ToString("HH:mm:ss.fff") + "->" + "最新价" + LastPrice + "<BSetup=" + BSetup + "，设定进入观察卖空");
                    }
                }
                
                if (Flag == 0)//没有开仓
                {                    
                    // 突破开仓
                    if (LastPrice >= BBreak)
                    {

                        Flag = 1;
                        止损位 = SSetup;
                        PrintLine(TickNow.ToString("HH:mm:ss.fff") + "->" + "向上突破" + LastPrice + ">" + BBreak + ",开仓做多,设置止损位=" + 止损位);

                        KN = Qty;
                        PN = 0;
                        KN_Trd = 0;
                        T0 = SERVERTIME;

                        SendOrder(KCJP, Qty, EnumDirectionType.Buy, EnumOffsetFlagType.Open, PriceTick);
                    }
                    else if (LastPrice <= SBreak)
                    {
                        Flag = -1;
                        止损位 = BSetup;
                        PrintLine(TickNow.ToString("HH:mm:ss.fff") + "->" + "向下突破" + LastPrice + "<" + SBreak + ",开仓做空，设置止损位=" + 止损位);

                        KN = Qty;
                        PN = 0;
                        KN_Trd = 0;
                        T0 = SERVERTIME;

                        SendOrder(KCJP, Qty, EnumDirectionType.Sell, EnumOffsetFlagType.Open, PriceTick);
                    }                    

                    // 观察买入 卖出
                    if (IfSeeHigh && LastPrice <= SEnter)
                    {
                        IfSeeHigh = false;
                        Flag = -1;
                        止损位 = SSetup;
                        PrintLine(TickNow.ToString("HH:mm:ss.fff") + "->" + "观察后向下" + LastPrice + "<" + SEnter + ",开仓做空，设置止损位=" + 止损位);

                        KN = Qty;
                        PN = 0;
                        KN_Trd = 0;
                        T0 = SERVERTIME;

                        SendOrder(KCJP, Qty, EnumDirectionType.Sell, EnumOffsetFlagType.Open, PriceTick);
                    }
                    else if (IfSeeLow && LastPrice >= BEnter)
                    {
                        IfSeeHigh = false;
                        Flag = 1;
                        止损位 = BSetup;
                        PrintLine(TickNow.ToString("HH:mm:ss.fff") + "->" + "观察后向上" + LastPrice + ">" + BEnter + ",开仓做多,设置止损位=" + 止损位);

                        KN = Qty;
                        PN = 0;
                        KN_Trd = 0;
                        T0 = SERVERTIME;

                        SendOrder(KCJP, Qty, EnumDirectionType.Buy, EnumOffsetFlagType.Open, PriceTick);
                    }
                }
                else
                {
                    // 如果有Flag，需要检查止损
                    if (Flag > 0)//已经买入开仓
                    {
                        //用买1
                        //卖1 - 跳
                        if (BIDPRICE(1).LASTDATA < 止损位)
                        {
                            //如果发现价格仍然高于突破买入(换句话说，平仓后还要买入)---调整止损位
                            if (LastPrice >= BBreak)
                            {
                                //多单超过了最高的一条线BBreak,将止损线从BSetup上移到SSetup
                                止损位 = SSetup;
                            }
                            else
                            {
                                SendOrder(PCJP, KN_Trd,
                                    EnumDirectionType.Sell, EnumOffsetFlagType.Close, PriceTick);

                                PrintLine(TickNow.ToString("HH:mm:ss.fff") + "->" + "多头持仓触发止损,BidPrice1="
                                    + BIDPRICE(1).LASTDATA + ",止损位=" + 止损位 + ",T0=" + T0);

                                Clear();
                            }
                        }
                    }
                    else if (Flag < 0)//已经卖出开仓
                    {
                        if (ASKPRICE(1).LASTDATA > 止损位)
                        {
                            if (LastPrice <= SBreak)
                            {
                                //空单跌破了最低的一条线SBreak,将止损线从SSetup下移到BSetup
                                止损位 = BSetup;
                            }
                            else
                            {
                                SendOrder(PCJP,
                                    KN_Trd,
                                    EnumDirectionType.Buy,
                                    EnumOffsetFlagType.Close, PriceTick);
                                PrintLine(TickNow.ToString("HH:mm:ss.fff") + "->" + "空头持仓触发止损,AskPrice1=" + ASKPRICE(1).LASTDATA
                                    + ",止损位=" + 止损位
                                    + ",T0=" + T0);
                                Clear();
                            }

                        }
                    }
                }
            }
            else
            {
                //中间有5分钟的缓冲
                if (!IfFinish)
                {
                    RealeaseAccount();
                }
            }
        }

        public override void OnStop()
        {
            RealeaseAccount();
        }

        public override void OnTrade(Trade trade)
        {

            PrintLine("成交回报");

            if (trade.OffsetFlag == EnumOffsetFlagType.Open)
            {
                KN_Trd += trade.Volume;
            }
            else
            {
                PN += trade.Volume;
            }
        }

        public void RealeaseAccount()
        {
            //如果有Flag，需要检查止损
            IfFinish = true;

            PrintLine("收盘处理--撤单");
            List<Order> ordLst = GetUnAllTradedOrderList(SYMBOL);
            if (ordLst != null && ordLst.Count > 0)
            {
                foreach (Order order in ordLst)
                {
                    if (ORDER_KEY_LIST.Contains(order.Key))//本策略下的订单
                    {

                        if (order.OffsetFlag == EnumOffsetFlagType.Open && (order.OrderStatus == EnumOrderStatusType.NoTradeQueueing || order.OrderStatus == EnumOrderStatusType.PartTradedQueueing))
                        {
                            CancelOrder(order);
                        }
                    }
                }
            }

            CloseAllFuturesPositions(SYMBOL);
        }

        void Clear()
        {
            Flag = 0;
            KN = 0;
            PN = 0;
            KN_Trd = 0;
            IfSeeHigh = false;
            IfSeeLow = false;
        }
        #endregion


        #region 委托被拒绝 要重新发出去
        public override void OnOrderRejected(Order order)
        {
            PrintLine("委托拒绝!!!" + order.StatusMsg);
        }

        public override void OnCancelOrderSucceeded(Order order)
        {
            if (!IfFinish)
            {
                PrintLine("撤单回报");
                if (order.OffsetFlag == EnumOffsetFlagType.Open)
                {
                    if (IfFKC)//开仓撤单后是不是要追单
                    {
                        //撤单时按照2跳滑点
                        ResendOrder(order, 2);
                    }
                    else
                    {
                        PrintLine("开仓不追 return！！！");
                    }
                }
                else
                {
                    //平仓是一定要追单平
                    //撤单时按照2跳滑点
                    ResendOrder(order, 2);
                }
            }
        }
        #endregion

        #region 辅助
        public void SendOrder(int jump, int Qty, EnumDirectionType direction, EnumOffsetFlagType oc, double PriceTick)
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

        public void ResendOrder(Order order, int jump)
        {
            if (!IfFKC && order.OffsetFlag == EnumOffsetFlagType.Open)
            {
                PrintLine("开仓return");
            }

            PrintLine("重发委托");

            if ((order.VolumeTotalOriginal - order.VolumeTraded) > 0)
            {
                SendOrder(jump, order.VolumeTotalOriginal - order.VolumeTraded, order.OrderDirection, order.OffsetFlag, PriceTick);
            }
            else
            {
                PrintLine("委托剩余量为0，不需要重发");
            }
        }
        #endregion
    }
}
