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
        private const string C_VERSION = "0.001";

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
        
        private string m_StragetyInstanceID = "";   // 策略实例识别码, 通过此识别码来区分持仓是否为此策略实例所拥有
        private bool m_initOK = true;               // OnStart事件中初始化是否成功
        

        public override void OnStart()
        {
            DateTime t1 = Now;
            
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

            CalculatePos();

            if (m_initOK)
            {
                
            }

            // 创建图表
            CreateChart("Main");

            DateTime t2 = Now;
            PrintMemo("启动完成[" + (t2 - t1).TotalMilliseconds + " ms]", ENU_msgType.msg_Info);
        }

        private void CalculatePos()
        {
            // 获取本合约持仓头寸
            Position longPosition = null;
            Position shortPosition = null;
            GetPositionOfStrategy(m_StragetyInstanceID, longPosition, shortPosition);

            if (longPosition != null) m_longPosition = longPosition.TodayPosition + longPosition.YdPosition;
            if (shortPosition != null) m_shortPosition = shortPosition.TodayPosition + shortPosition.YdPosition;
        }

        public override void OnBarOpen(TickData td)
        {
            double dOpenPrice = 0;
            int lots = 0;
            double totalCapital = 0;

            BarData bar = GetBarData(BARCOUNT - 2);

            if (P_StartWith > 0 && m_longPosition <= 0)
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
                }
            }
            else if (P_StartWith < 0 && m_shortPosition <= 0)
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
                }
            }

            // 计算ATR值
            m_ATRValue = AtrData(P_ATR_Period, 1);
        }

        public override void OnTick(TickData td)
        {
            if (m_longPosition > 0 || m_shortPosition > 0)
            {
                AdjustSLTP(td);
            }

            if (m_longPosition > 0)
            {
                
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
            // 写成交日志
        }

        public override void OnPosition(Trade trade)
        {
            CalculatePos();
        }

        private void AdjustSLTP(TickData td)
        {
            double dSetSL = 0;
            double dCalculateSL = 0;

            Position longPosition = null;
            Position shortPosition = null;
            GetPositionOfStrategy(m_StragetyInstanceID, longPosition, shortPosition);

            if (longPosition != null && longPosition.YdPosition + longPosition.TodayPosition > 0)
            {
                dSetSL = longPosition.StopLossPrice;
                dCalculateSL = AdjustFuturePrice(td.BidPrice1 - m_ATRValue * P_ATR_Multiplier - INSTRUMENT.PriceTick);

                if (dCalculateSL > dSetSL || dSetSL == double.NaN)
                {
                    SetTakeProfitStopLoss(SYMBOL, EnumDirectionType.Buy, dCalculateSL, longPosition.YdPosition + longPosition.TodayPosition, double.NaN, double.NaN, double.NaN);                    
                }                
            }

            if (shortPosition != null && shortPosition.YdPosition + shortPosition.TodayPosition > 0)
            {
                dSetSL = shortPosition.StopLossPrice;
                dCalculateSL = AdjustFuturePrice(td.AskPrice1 + m_ATRValue * P_ATR_Multiplier + INSTRUMENT.PriceTick);

                if (dCalculateSL < dSetSL || dSetSL == double.NaN)
                {
                    SetTakeProfitStopLoss(SYMBOL, EnumDirectionType.Sell, dCalculateSL, shortPosition.YdPosition + shortPosition.TodayPosition, double.NaN, double.NaN, double.NaN);                    
                }
            }
        }
    }
}
