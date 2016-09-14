using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DevelopLibrary.DevelopAPI;
using DevelopLibrary.Enums;

namespace TradePubLib
{
    public class WtEventStrategyBase : EventStrategyBase
    {

        protected string m_sInvestorID = "";

        public DateTime TickNow
        {
            get { return SERVERTIME; }
        }

        public override void OnStart()
        {
            base.OnStart();
            m_sInvestorID = GetInvestorID();
            
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

            for (int i = 1; i < CLOSE.Length; i++)
            {
                if (DATE[i - 1] == DATE[i])
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
                else if (DATE[i - 1] > DATE[i])
                {
                    sInfo = "日期序列重复[" + DATE[i - 1] + " " + TIME[i - 1] + "  ->  " + DATE[i] + " " + TIME[i] + "]";
                    bIsValid = false;
                    break;
                }
            }

            return bIsValid;
        }

        /// <summary>
        /// 开仓
        /// </summary>
        /// <param name="price">开仓价格</param>
        /// <param name="volume">开仓手数</param>
        /// <param name="slipPoint">允许的滑点</param>
        /// <param name="direction">开仓的方向[多 || 空]</param>
        /// <returns>对应Order的RequestID</returns>
        public int OpenOrder(double price, int volume, int slipPoint, EnumDirectionType direction)
        {
            double orderPrice = 0;
            int returnValue = 0;
            double priceTick = INSTRUMENT.PriceTick;

            if (direction == EnumDirectionType.Buy)         // 开多单
            {
                orderPrice = price + slipPoint * priceTick;
                returnValue = OpenBuy(orderPrice, volume, SYMBOL);
            }
            else if (direction == EnumDirectionType.Sell)   // 开空单
            {
                orderPrice = price - slipPoint * priceTick;
                returnValue = OpenSell(orderPrice, volume, SYMBOL);
            }

            return returnValue;
        }

        /// <summary>
        /// 平多头仓位
        /// </summary>
        /// <param name="price">平仓价格</param>
        /// <param name="volume">平仓手数</param>
        /// <param name="slipPoint">允许的滑点</param>        
        public void CloseLongPosition(double price, int volume, int slipPoint, EnumOrderType orderType = EnumOrderType.限价单)
        {
            CloseFuturesPositions(SYMBOL, EnumDirectionType.Buy, price, volume, slipPoint, orderType, EnumHedgeFlag.投机);            
        }

        /// <summary>
        /// 平空头仓位
        /// </summary>
        /// <param name="price">平仓价格</param>
        /// <param name="volume">平仓手数</param>
        /// <param name="slipPoint">允许的滑点</param>        
        public void CloseShortPosition(double price, int volume, int slipPoint, EnumOrderType orderType = EnumOrderType.限价单)
        {
            CloseFuturesPositions(SYMBOL, EnumDirectionType.Sell, price, volume, slipPoint, orderType, EnumHedgeFlag.投机);            
        }
        /// <summary>
        /// 获取本策略所有未成交的开仓单
        /// </summary>
        /// <returns></returns>
        public List<Order> GetUnTradedOpenOrder()
        {
            List<Order> unTradedOrdLst = new List<Order>();
            List<Order> ordLst = GetUnAllTradedOrderList(SYMBOL);
            if (ordLst != null && ordLst.Count > 0)
            {
                //PrintMemo("有未成交单[开仓] = " + ordLst.Count.ToString());
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
        public int CalculateLots(int maxLots, double capitalRatio, double totalCapital, double openPrice)
        {
            int lots = 0;
            Instrument instrument = INSTRUMENT;

            if (capitalRatio < 0) capitalRatio = 0;
            if (totalCapital < 0) totalCapital = 0;

            if (capitalRatio <= 1)
            {
                // 百分比计算方法
                // 头寸 = 资金 * 资金使用比率 / (开仓价 * 每手合约乘数 * 保证金率)
                if (openPrice > 0)
                {
                    lots = (int)Math.Round(totalCapital * capitalRatio / (openPrice * instrument.VolumeMultiple * Math.Max(instrument.LongMarginRatioByMoney, instrument.ShortMarginRatioByMoney)), 0);
                }
            }
            else
            {
                // 固定资金计算方法
                // 头寸 = 资金 / 资金使用比率 
                lots = (int)Math.Round(totalCapital / capitalRatio);
            }

            // 不能超过最大可开仓头寸设置
            if (lots > maxLots) lots = maxLots;
            if (lots <= 1) lots = 1;

            return lots;
        }

        public string BulidStagetyInstanceID(string sStragetyID)
        {
            string sInstanceID = sStragetyID + INSTRUMENT.InstrumentID + DataCycle.Repeat + DataCycle.CycleBase.ToString();
            return sInstanceID;
        }

        /// <summary>
        /// 加载本品种数据，包括本周期和大周期的
        /// </summary>
        public bool LoadMyData(int iBarLength)
        {
            bool bLoadReault = true;

            PrintMemo("加载本周期历史数据[" + DataCycle.ToString() + " " + SYMBOL + "]", ENU_msgType.msg_Info);
            UnLoadHisData();
            EnumMergeHisDataStatus status = LoadHisData(SYMBOL, DataCycle.CycleBase, DataCycle.Repeat, iBarLength);

            if (status == EnumMergeHisDataStatus.Success)
            {
                if (BARCOUNT < iBarLength)
                {
                    bLoadReault = false;
                    PrintMemo("加载本周期数据错误: 数据量不够(" + BARCOUNT + " < " + iBarLength + ").", ENU_msgType.msg_Error);
                }
            }
            else
            {
                bLoadReault = false;

                if (status == EnumMergeHisDataStatus.FileNotExist)
                {
                    PrintMemo("加载本周期数据错误: 历史数据不存在.", ENU_msgType.msg_Error);
                }
                else
                {
                    PrintMemo("加载本周期数据错误: 未知原因加载失败.", ENU_msgType.msg_Error);
                }
            }

            if (bLoadReault)
            {
                // 打印加载数据结果
                PrintMemo("加载数据量: " + BARCOUNT);
                PrintMemo("数据起始[" + GetBarData(0).CurDateTime.ToString("yyyy-MM-dd HH:mm:ss") + "] 数据结束[" + GetBarData(BARCOUNT - 1).CurDateTime.ToString("yyyy-MM-dd HH:mm:ss") + "]");
            }
            return bLoadReault;
        }

        /// <summary>
        /// 获取本策略本品种的所有持仓
        /// </summary>
        /// <param name="sStrategyID">策略ID</param>
        /// <param name="longPosition">返回多头持仓</param>
        /// <param name="shortPosition">返回空头持仓</param>
        /// <returns>空头和多头的仓位和</returns>
        public int GetPositionOfStrategy(string sStrategyID, out Position longPosition, out Position shortPosition)
        {
            // TODO

            int positionSum = 0;
            longPosition = null;
            shortPosition = null;

            // 获取本策略开仓的具体仓位    
            longPosition = GetPosition(SYMBOL, DevelopLibrary.Enums.EnumDirectionType.Buy);
            shortPosition = GetPosition(SYMBOL, DevelopLibrary.Enums.EnumDirectionType.Sell);

            if (longPosition != null)
            {
                positionSum += longPosition.TodayPosition + longPosition.YdPosition;
            }

            if (shortPosition != null)
            {
                positionSum += shortPosition.TodayPosition + shortPosition.YdPosition;
            }

            return positionSum;
        }

        public string GetInvestorID()
        {
            string sInvestorID = "";
            FuturesInvestor investor = GetFuturesInvestor();

            if (investor != null)
            {
                sInvestorID = investor.BrokerID + "." + investor.InvestorID;

                if (sInvestorID == ".") sInvestorID = "backtest";                
            }

            return sInvestorID;
        }

        /// <summary>
        /// 计算K线的平均真实波动幅度
        /// </summary>
        /// <param name="ev">K线数据</param>
        /// <param name="atrLength">ATR长度</param>
        /// <param name="offset">偏移量（从第几根K线起[最新一根为0])</param>
        /// <returns></returns>
        public double AtrData(int atrLength, int offset)
        {
            double atrValue = 0;
            int loop = 0;
            double tmpPreClose = 0;
            double tmpHigh = 0;
            double tmpLow = 0;
            double sumTR = 0;

            if (CLOSE.Length >= atrLength + offset + 1)   // 数据量足够
            {
                loop = atrLength;
            }
            else
            {
                loop = CLOSE.Length - offset - 1;
            }

            if (loop > 0)
            {
                //double[] trs = new double[loop];
                for (int i = 0; i < loop; i++)
                {
                    tmpHigh = SF.Refdata(HIGH, offset + i);
                    tmpLow = SF.Refdata(LOW, offset + i);
                    tmpPreClose = SF.Refdata(CLOSE, offset + i + 1);

                    sumTR += MAX(ABS(tmpHigh - tmpLow), ABS(tmpPreClose - tmpHigh), ABS(tmpPreClose - tmpLow));
                }

                atrValue = sumTR / loop;
            }

            return atrValue;
        }

        public void PrintMemo(string msg, ENU_msgType msgType = ENU_msgType.msg_Info, bool bPrintTime = true)
        {
            string sType = "";
            //string msgTime = Now.ToString("HH:mm:ss");
            string msgTime = TickNow.ToString("HH:mm:ss");

            switch (msgType)
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

        public void PrintInstrumentInfo()
        {
            Instrument instrument = INSTRUMENT;

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
    }
}
