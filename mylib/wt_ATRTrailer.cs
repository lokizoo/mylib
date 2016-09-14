using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DevelopLibrary.DevelopAPI;
using DevelopLibrary.Enums;
using TradePubLib;

namespace mylib
{
    public class wt_ATRTrailer : WtEventStrategyBase
    {
        private const string C_VERSION = "0.101";

        #region 参数
        [Parameter(Display = "ATR_周期", Description = "ATR的长度", Category = "模型")]
        public int P_ATR_Period = 24;

        [Parameter(Display = "ATR_系数", Description = "ATR的系数,来确定止损的大小", Category = "模型")]
        public double P_ATR_Multiplier = 3;

        [Parameter(Display = "初始仓方向", Description = "初始仓的方向 1: 多头; -1: 空头", Category = "模型")]
        public int P_StartWith = 1;

        [Parameter(Display = "最大头寸", Description = "最大可持仓的头寸", Category = "资管")]
        public int P_MaxLot = 1;

        [Parameter(Display = "开仓资金系数", Description = "头寸开仓资金百分比 {[0, 1]: 百分比计算头寸; > 1: 固定资金计算头寸 ", Category = "资管")]
        public double P_CapitalRatio = 0.5;


        [Parameter(Display = "开仓滑点", Description = "开仓所允许的滑点", Category = "交易")]
        public int P_Open_SlipPoint = 0;

        [Parameter(Display = "平仓滑点", Description = "平仓所允许的滑点", Category = "交易")]
        public int P_Close_SlipPoint = 0;

        [Parameter(Display = "策略码", Description = "区分持仓所用的识别码", Category = "其它", IsReadOnly = true)]
        public string P_StragetyID = "at001";

        [Parameter(Display = "版本号", Description = "模型的版本号", Category = "其它", IsReadOnly = true)]
        public string P_MyVersion = C_VERSION;

        [Parameter(Display = "写成交日志", Description = "是否记录成交的日志", Category = "其它")]
        public bool P_IsRecordTrade = false;

        [Parameter(Display = "自动运行", Description = "是否在设定的事件自动运行和停止", Category = "其它")]
        public bool P_IsAutoRun = true;
        #endregion


        private double m_ATRValue = 0;          // 策略中需要的ATR值

        private int m_longPosition = 0;         // 本品种本策略所持的多头仓位
        private int m_shortPosition = 0;        // 本品种本策略所持的空头仓位
        private Position m_oLongPosition = null;
        private Position m_oShortPosition = null;
        private bool m_bHaveFirstOpen = false;

        private string m_StragetyInstanceID = "";   // 策略实例识别码, 通过此识别码来区分持仓是否为此策略实例所拥有
        private bool m_initOK = true;               // OnStart事件中初始化是否成功
        

        public override void OnStart()
        {
            DateTime t1 = Now;
            base.OnStart();

            m_initOK = true;            
            m_StragetyInstanceID = BulidStagetyInstanceID(P_StragetyID);       // 生成策略实例识别码(策略ID + 合约 + 周期)

            // 打印合约信息
            PrintInstrumentInfo();

            // 加载数据
            if (!LoadMyData(100)) m_initOK = false;

            //检查加载的数据是否合规（是否有重复数据）  
            string sInfo = "";
            if (CheckDataValid(out sInfo))
            {
                PrintMemo(sInfo, ENU_msgType.msg_Info);
            }
            else
            {
                m_initOK = false;
                PrintMemo(sInfo, ENU_msgType.msg_Error);
            }

            // 获取持仓数据
            CalculatePos();

            if (m_initOK)
            {
                
            }

            // 创建图表
            CreateChart("Main");

            DateTime t2 = Now;
            PrintMemo("启动完成[" + (t2 - t1).TotalMilliseconds + " ms]", ENU_msgType.msg_Info);
        }
                

        public override void OnBarOpen(TickData td)
        {
            double dOpenPrice = 0;
            int lots = 0;
            double totalCapital = 0;

            // 计算ATR值
            m_ATRValue = AtrData(P_ATR_Period, 1);
            PrintATR();

            BarData bar = GetBarData(BARCOUNT - 2);
            if (m_longPosition + m_shortPosition > 0)
            {
                m_bHaveFirstOpen = true;
            }
            else
            { 
                if ( (P_StartWith == 1 || P_StartWith == 0) && m_longPosition <= 0 && !m_bHaveFirstOpen)
                {
                    if (bar.Close > bar.Open)
                    {
                        // 开多单                    
                        FuturesAccount fa = GetFuturesAccount();
                        totalCapital = fa.DynamicProfit;
                        dOpenPrice = bar.Close;
                        lots = CalculateLots(P_MaxLot, P_CapitalRatio, totalCapital, dOpenPrice);      // 计算可开仓的仓位
                        PrintMemo("建头个仓位 => 多头建仓 @ " + dOpenPrice.ToString() + " * " + lots.ToString());
                        OpenOrder(dOpenPrice, lots, P_Open_SlipPoint, EnumDirectionType.Buy);
                        m_bHaveFirstOpen = true;
                    }
                }
                else if ( (P_StartWith == -1 || P_StartWith == 0) && m_shortPosition <= 0 && !m_bHaveFirstOpen)
                {
                    if (bar.Close < bar.Open)
                    {
                        // 开空单
                        FuturesAccount fa = GetFuturesAccount();
                        totalCapital = fa.DynamicProfit;
                        dOpenPrice = bar.Close;
                        lots = CalculateLots(P_MaxLot, P_CapitalRatio, totalCapital, dOpenPrice);
                        PrintMemo("建头个仓位 => 空头建仓 @ " + dOpenPrice.ToString() + " * " + lots.ToString());
                        OpenOrder(dOpenPrice, lots, P_Open_SlipPoint, EnumDirectionType.Sell);
                        m_bHaveFirstOpen = true;
                    }
                }  
            }            
        }

        public override void OnTick(TickData td)
        {
            double dOpenPrice = 0;
            int lots = 0;
            double totalCapital = 0;

            if (m_longPosition > 0 || m_shortPosition > 0)
            {
                AdjustSLTP(td);
            }

            if (m_longPosition > 0)
            {
                if (td.LastPrice <= m_oLongPosition.StopLossPrice  )
                {
                    // 平多

                    if ((m_shortPosition - m_longPosition) < P_MaxLot)
                    {
                        // 反手做空
                        FuturesAccount fa = GetFuturesAccount();
                        totalCapital = fa.DynamicProfit;
                        dOpenPrice = td.BidPrice1;
                        lots = CalculateLots(P_MaxLot, P_CapitalRatio, totalCapital, dOpenPrice);
                        PrintMemo("  => 空头建仓 @ " + dOpenPrice.ToString() + " * " + lots.ToString());
                        OpenOrder(dOpenPrice, lots, P_Open_SlipPoint, EnumDirectionType.Sell);
                    }                    
                }
            }

            if (m_shortPosition > 0)
            {
                if (td.LastPrice >= m_oShortPosition.StopLossPrice)
                {
                    // 平多

                    if ((m_longPosition - m_shortPosition) < P_MaxLot)
                    {
                        // 反手做多
                        FuturesAccount fa = GetFuturesAccount();
                        totalCapital = fa.DynamicProfit;
                        dOpenPrice = td.AskPrice1;
                        lots = CalculateLots(P_MaxLot, P_CapitalRatio, totalCapital, dOpenPrice);      // 计算可开仓的仓位
                        PrintMemo("  => 多头建仓 @ " + dOpenPrice.ToString() + " * " + lots.ToString());
                        OpenOrder(dOpenPrice, lots, P_Open_SlipPoint, EnumDirectionType.Buy);
                    }
                }
            }
        }

        public override void OnTrade(Trade trade)
        {
            string msg = "成交: " + SYMBOL + " " + trade.Direction.ToString() + trade.OffsetFlag.ToString() + " " + trade.Price.ToString() + " * " + trade.Volume + " @ " +
                trade.TradeTime + "[" + trade.TradeID + ", " + trade.OrderSysID + "]";
            PrintMemo(msg);

            // 设置止盈止损            
            double dStopLossPrice = 0;            

            if (trade.Direction == EnumDirectionType.Buy)
            {
                // 多单
                dStopLossPrice = AdjustFuturePrice(trade.Price  -  m_ATRValue * P_ATR_Multiplier -  INSTRUMENT.PriceTick);                
            }
            else if (trade.Direction == EnumDirectionType.Sell)
            {
                // 空单
                dStopLossPrice = AdjustFuturePrice(trade.Price + m_ATRValue * P_ATR_Multiplier + INSTRUMENT.PriceTick);                
            }

            PrintMemo("设置止损价 = " + dStopLossPrice);
            SetTakeProfitStopLoss(SYMBOL, trade.Direction, dStopLossPrice, trade.Volume, double.NaN, double.NaN, double.NaN);

            // 更新持仓数据
            CalculatePos();

            if (m_oLongPosition != null && m_longPosition > 0) m_oLongPosition.StopLossPrice = dStopLossPrice;
            if (m_oShortPosition != null && m_shortPosition > 0) m_oShortPosition.StopLossPrice = dStopLossPrice;

            // 写成交日志

        }

        public override void OnPosition(Trade trade)
        {
            // 
        }

        private void CalculatePos()
        {
            // 获取本合约持仓头寸
            //Position longPosition = null;
            //Position shortPosition = null;
            GetPositionOfStrategy(m_StragetyInstanceID, out m_oLongPosition, out m_oShortPosition);

            if (m_oLongPosition != null) m_longPosition = m_oLongPosition.TodayPosition + m_oLongPosition.YdPosition;
            if (m_oShortPosition != null) m_shortPosition = m_oShortPosition.TodayPosition + m_oShortPosition.YdPosition;
        }

        private void AdjustSLTP(TickData td)
        {
            double dSetSL = 0;
            double dCalculateSL = 0;
                        
            if (m_longPosition > 0)
            {
                dSetSL = m_oLongPosition.StopLossPrice;
                dCalculateSL = AdjustFuturePrice(td.BidPrice1 - m_ATRValue * P_ATR_Multiplier - INSTRUMENT.PriceTick);

                if (dCalculateSL > dSetSL || dSetSL == double.NaN)
                {
                    SetTakeProfitStopLoss(SYMBOL, EnumDirectionType.Buy, dCalculateSL, m_longPosition, double.NaN, double.NaN, double.NaN);
                    //PrintMemo("移动止损价： " + dSetSL + " => " + dCalculateSL);
                    m_oLongPosition.StopLossPrice = dCalculateSL;
                }                
            }

            if (m_shortPosition > 0)
            {
                dSetSL = m_oShortPosition.StopLossPrice;
                dCalculateSL = AdjustFuturePrice(td.AskPrice1 + m_ATRValue * P_ATR_Multiplier + INSTRUMENT.PriceTick);

                if (dCalculateSL < dSetSL || dSetSL == double.NaN)
                {
                    SetTakeProfitStopLoss(SYMBOL, EnumDirectionType.Sell, dCalculateSL, m_shortPosition, double.NaN, double.NaN, double.NaN);
                    //PrintMemo("移动止损价： " + dSetSL + " => " + dCalculateSL);
                    m_oShortPosition.StopLossPrice = dCalculateSL;
                }
            }
        }

        private void PrintATR()
        {
            string msg = "";

            msg = SYMBOL + "[" + m_StragetyInstanceID + "]:\t" + "ATR(" + P_ATR_Period + ") = " + m_ATRValue;
            PrintMemo(msg, ENU_msgType.msg_Info, true);
        }
    }
}
