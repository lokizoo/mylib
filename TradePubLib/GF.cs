using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DevelopLibrary.DevelopAPI;

namespace TradePubLib
{
    public enum ENU_Extreme_Type
    {
        EXTREME_TYPE_NONE,                  // 非极值点
        EXTREME_TYPE_NONE_BUT_LLOW,         // 非极值点,但是比前极低点更低
        EXTREME_TYPE_NONE_BUT_HHIGH,        // 非极值点,但是比前极高点点更高
        EXTREME_TYPE_HIGH,                  // 极高点
        EXTREME_THPE_LOW,                   // 极低点
    }

    public enum ENU_msgType
    {
        msg_Error = 1,
        msg_Info = 2
    }

    public class GF
    {
        private IndicatorBase m_strategyObj;

        private GF(IndicatorBase strategyObj)
        {
            m_strategyObj = strategyObj;
        }

        public static GF GetGenericFun(IndicatorBase strategyObj)
        {
            if (strategyObj == null)
            {
                return null;
            }
            else
            {
                return new GF(strategyObj);
            }
        }

        public DateTime TickNow
        {
            get { return m_strategyObj.SERVERTIME; }
        }

        /// <summary>
        /// 检查加载的历史数据是否合法（主要检查是否有重复数据）
        /// </summary>
        /// <param name="sInfo">如果非法,则返回非法的提示信息</param>
        /// <returns>True: 合法; False: 非法</returns>
        public bool CheckDataValid(out string sInfo)
        {
            bool bIsValid = true;
            sInfo = "数据检查合法";

            for (int i = 1; i < m_strategyObj.CLOSE.Length; i++)
            {
                if (m_strategyObj.DATE[i - 1] == m_strategyObj.DATE[i])
                {
                    if (m_strategyObj.TIME[i - 1] >= m_strategyObj.TIME[i])
                    {
                        if (m_strategyObj.TIME[i] != 90000)       // 因为有夜盘的存在，所以，上一日的夜盘属于今日的行情，所以这里要稍微处理一下
                        {
                            sInfo = "日期[" + m_strategyObj.DATE[i] + "] 时间序列重复 [" + m_strategyObj.TIME[i - 1] + " -> " + m_strategyObj.TIME[i] + "]";
                            bIsValid = false;
                            break;
                        }
                    }
                }
                else if (m_strategyObj.DATE[i - 1] > m_strategyObj.DATE[i])
                {
                    sInfo = "日期序列重复[" + m_strategyObj.DATE[i - 1] + " " + m_strategyObj.TIME[i - 1] + "  ->  " + m_strategyObj.DATE[i] + " " + m_strategyObj.TIME[i] + "]";
                    bIsValid = false;
                    break;
                }
            }
            
            return bIsValid;
        }


        /// <summary>
        /// 获取本策略本品种的所有持仓
        /// </summary>
        /// <param name="sStrategyID">策略ID</param>
        /// <param name="longPosition">返回多头持仓</param>
        /// <param name="shortPosition">返回空头持仓</param>
        public void GetPositionOfStrategy(string sStrategyID, out Position longPosition, out Position shortPosition)
        {
            // TODO
            // 获取本策略开仓的具体仓位    
            longPosition = m_strategyObj.GetPosition(m_strategyObj.SYMBOL, DevelopLibrary.Enums.EnumDirectionType.Buy);
            shortPosition = m_strategyObj.GetPosition(m_strategyObj.SYMBOL, DevelopLibrary.Enums.EnumDirectionType.Sell);            
        }

        public void PrintInstrumentInfo()
        {
            Instrument instrument = m_strategyObj.INSTRUMENT;

            string msg = "------------------------ 合约信息 ------------------------";
            PrintMemo(msg, ENU_msgType.msg_Info, false);

            msg = "合约代码: " + instrument.InstrumentID + "\t合约名称: " + instrument.InstrumentName;
            PrintMemo(msg, ENU_msgType.msg_Info, false);
            
            msg = "期货公司多头保证金率: " + instrument.LongMarginRatioByMoney.ToString("P") + "\t期货公司空头保证金率: " + instrument.ShortMarginRatioByMoney.ToString("P");
            PrintMemo(msg, ENU_msgType.msg_Info, false);
            
            if (instrument.OpenRatioByVolume > 0.1)
            {
                msg = "开仓手续费: " + instrument.OpenRatioByVolume.ToString() + "\t平仓手续费: " + instrument.CloseRatioByVolume.ToString() + "\t平今手续费: " + instrument.CloseTodayRatioByVolume.ToString();
            }
            else
            {
                msg = "开仓手续费: " + (instrument.OpenRatioByMoney * 1000).ToString() + "‰" + "\t平仓手续费: " + (instrument.CloseRatioByMoney * 1000).ToString() + "‰" +
                    "\t平今手续费: " + (instrument.CloseTodayRatioByMoney * 1000).ToString() + "‰";
            }
            PrintMemo(msg, ENU_msgType.msg_Info, false);

            msg = "---------------------------------------------------------";
            PrintMemo(msg, ENU_msgType.msg_Info, false);
        }

        public void PrintMemo(string msg, ENU_msgType msgType = ENU_msgType.msg_Info, bool bPrintTime = true)
        {
            string sType = "";
            //string msgTime = Now.ToString("HH:mm:ss");
            string msgTime = TickNow.ToString("HH:mm:ss");

            switch (msgType)
            {
                case ENU_msgType.msg_Info:
                    sType = "[I][" + m_strategyObj.SYMBOL + "]";
                    break;
                case ENU_msgType.msg_Error:
                    sType = "[R][" + m_strategyObj.SYMBOL + "]";
                    break;
                default:
                    sType = "[U][" + m_strategyObj.SYMBOL + "]";
                    break;
            }

            if (bPrintTime)
            {
                m_strategyObj.PrintLine(sType + msgTime + ": " + msg);
            }
            else
            {
                m_strategyObj.PrintLine(sType + msg);
            }
        }
    }

    public class ExtremePoint : BarData
    {
        public ENU_Extreme_Type ExtremeType;        
        public int BarPosition;
        public bool Visible = true;

        public double ExtremePrice
        {
            get
            {
                double price = 0;
                switch(ExtremeType)
                {
                    case ENU_Extreme_Type.EXTREME_THPE_LOW:
                        price = this.Low;
                        break;
                    case ENU_Extreme_Type.EXTREME_TYPE_HIGH:
                        price = this.High;
                        break;
                    case ENU_Extreme_Type.EXTREME_TYPE_NONE:
                        price = double.NaN;
                        break;
                    default:
                        price = double.NaN;
                        break;
                }
                return price;
            }
        }

        public ExtremePoint(BarData bar)
        {
            if (bar != null)
            {
                this.AdjClose = bar.AdjClose;
                this.Amount = bar.Amount;
                this.AskPrice1 = bar.AskPrice1;
                this.AskVolume1 = bar.AskVolume1;
                this.AveragePrice = bar.AveragePrice;
                this.BidPrice1 = bar.BidPrice1;
                this.BidVolume1 = bar.BidVolume1;
                this.Close = bar.Close;
                this.CurDateTime = bar.CurDateTime;
                this.CurrDelta = bar.CurrDelta;
                this.High = bar.High;
                this.HighestPrice = bar.HighestPrice;
                this.Low = bar.Low;
                this.LowerLimitPrice = bar.LowerLimitPrice;
                this.LowestPrice = bar.LowestPrice;
                this.Open = bar.Open;
                this.OpenInterest = bar.OpenInterest;
                this.OpenPrice = bar.OpenPrice;
                this.PreClosePrice = bar.PreClosePrice;
                this.PreDelta = bar.PreDelta;
                this.PreOpenInterest = bar.PreOpenInterest;
                this.PreSettlementPrice = bar.PreSettlementPrice;
                this.UpperLimitPrice = bar.UpperLimitPrice;
                this.Volume = bar.Volume;                
            }
        }
        public ExtremePoint()
        {            
        }

        public override string ToString()
        {            
            string sPre = this.ExtremeType == ENU_Extreme_Type.EXTREME_TYPE_HIGH ? "HH" : "LL";
            string sVisual = this.Visible == true ? "Y" : "N";
            double dPrice = this.ExtremeType == ENU_Extreme_Type.EXTREME_TYPE_HIGH ? this.High : this.Low;

            string sInfo = sPre + "[T: " + this.CurDateTime.ToString("yyyy-MM-dd HH:mm:ss" + " V: " + sVisual + " P: " + dPrice + "]");
            return sInfo;
        }

    }
}
