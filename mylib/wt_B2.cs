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
    public class wt_B2 : EventStrategyBase
    {
        private const string C_VERSION = "0.001";

        #region 参数
        [Parameter(Display = "一笔最少K线数", Description = "形成头和底最少需要的K线数", Category = "模型")]
        public int P_LineMinimumBarCount = 4;

        [Parameter(Display = "最大K线数", Description = "两个头部(底部)之间最大的K线数", Category = "模型")]
        public int P_MaxBarCount = 200;

        [Parameter(Display = "突破后检查K线数", Description = "突破后,可开仓最大可检查的K线数", Category = "模型")]        
        public int P_CheckBarCountAfterBreaked = 3;

        [Parameter(Display = "止损类型", Description = "止损类型（0：不设置止损； 1：ATR移动止损法", Category = "模型")]
        public int P_StopLossType = 0;

        [Parameter(Display = "止盈类型", Description = "止盈类型（0：不设置止损； 1：ATR移动止损法", Category = "模型")]
        public int P_StopProfitType = 0;

        [Parameter(Display = "最大头寸", Description = "最大可持仓的头寸", Category = "资管")]
        public int P_MaxLot = 1;

        [Parameter(Display = "开仓资金系数", Description = "头寸开仓资金百分比 {[0, 1]: 百分比计算头寸; > 1: 固定资金计算头寸 ", Category = "资管")]
        public double P_CapitalRatio = 0.5;


        [Parameter(Display = "开仓滑点", Description = "开仓所允许的滑点", Category = "交易")]
        public int P_Open_SlipPoint = 0;

        [Parameter(Display = "平仓滑点", Description = "平仓所允许的滑点", Category = "交易")]
        public int P_Close_SlipPoint = 0;

        [Parameter(Display = "策略码", Description = "区分持仓所用的识别码", Category = "其它", IsReadOnly = true)]
        public string P_StragetyID = "b2001";

        [Parameter(Display = "版本号", Description = "模型的版本号", Category = "其它", IsReadOnly = true)]
        public string P_MyVersion = C_VERSION;

        [Parameter(Display = "写成交日志", Description = "是否记录成交的日志", Category = "其它")]
        public bool P_IsRecordTrade = false;

        [Parameter(Display = "自动运行", Description = "是否在设定的事件自动运行和停止", Category = "其它")]
        public bool P_IsAutoRun = true;
        #endregion

        private string m_StragetyInstanceID = "";   // 策略实例识别码, 通过此识别码来区分持仓是否为此策略实例所拥有
        private bool m_initOK = true;               // OnStart事件中初始化是否成功
        private GF m_gf = null;                     // 通用函数对象
        private List<ExtremePoint> m_extremePoints = null;      // 极值点列表
        private ExtremePoint m_lastHHPoint = null;              // 最后一个极值高低
        private ExtremePoint m_lastLLPoint = null;              // 最后一个极值低点

        private int m_BarCountAfterBreakUp = 0;     // 向上突破后运行的K线数(包括向上突破的那根K线)
        private int m_BarCountAfterBreakDown = 0;   // 向下突破后允许的K线数(包括向下突破的那根K线)
        private BarData m_BreakBar = null;

        public override void OnStart()
        {
            DateTime t1 = Now;

            // 创建通用函数对象
            m_gf = GF.GetGenericFun(this);


            m_initOK = true;
            m_extremePoints = new List<ExtremePoint>();
            m_StragetyInstanceID = m_gf.BulidStagetyInstanceID(P_StragetyID);       // 生成策略实例识别码(策略ID + 合约 + 周期)
            
            // 打印合约信息
            m_gf.PrintInstrumentInfo();

            // 加载数据
            if (!LoadMyData()) m_initOK = false;

            //检查加载的数据是否合规（是否有重复数据）  
            string sInfo = "";
            if (m_gf.CheckDataValid(out sInfo))
            {
                m_gf.PrintMemo(sInfo, ENU_msgType.msg_Info);
            }
            else
            {
                m_initOK = false;
                m_gf.PrintMemo(sInfo, ENU_msgType.msg_Error);                
            }

            // 获取本合约持仓头寸
            GetPosition();

            if (m_initOK)
            {
                FindExtremePoint(0, false);     // 找出图表中的极值点
                PrintAllExtremePoint();         // 打印所有的极值点
            }
            // 创建图表
            //CreateChart("Main;测试.w_SLOWKD(15,4,2,5)");
            CreateChart("Main");
            DateTime t2 = Now;
            m_gf.PrintMemo("启动完成[" + (t2 - t1).TotalMilliseconds + " ms]", ENU_msgType.msg_Info);
        }

        public override void OnTick(TickData td)
        {
            double dStopPrice = 0;
            if (m_BreakBar == null) return;

            if (m_BarCountAfterBreakUp > 0)
            {
                // 向上突破,开始检查是否突破失败后做空
                dStopPrice = m_BreakBar.Low;

                if (td.LastPrice < dStopPrice)
                {
                    // 计算可用的资金
                    FuturesAccount fa = GetFuturesAccount();
                    double totalCapital = fa.DynamicProfit;
                    double dOpenPrice = td.BidPrice1;
                    int lots = CalculateLots(P_MaxLot, P_CapitalRatio, totalCapital, dOpenPrice);      // 计算可开仓的仓位
                    m_gf.OpenOrder(dOpenPrice, lots, P_Open_SlipPoint, EnumDirectionType.Sell);

                    m_BarCountAfterBreakUp = 0;     // 取消模型检查
                }
            }

            if (m_BarCountAfterBreakDown > 0)
            {
                // 向下突破,开始检查是否突破失败后做多
                dStopPrice = m_BreakBar.High;
                if (td.LastPrice > dStopPrice)
                {
                    // 计算可用的资金
                    FuturesAccount fa = GetFuturesAccount();
                    double totalCapital = fa.DynamicProfit;
                    double dOpenPrice = td.AskPrice1;
                    int lots = CalculateLots(P_MaxLot, P_CapitalRatio, totalCapital, dOpenPrice);      // 计算可开仓的仓位
                    m_gf.OpenOrder(dOpenPrice, lots, P_Open_SlipPoint, EnumDirectionType.Buy);

                    m_BarCountAfterBreakDown = 0;
                }
            }
        }

        public override void OnBarOpen(TickData td)
        {
            int iStartPos = 0;

            PrintLine(m_gf.TickNow.ToString("yyyy-MM-dd HH:mm:ss") + "[S]----------------------------------------");
            DateTime t1 = Now;           
            
            // 模型检查启动后,是否已超时
            if (m_BarCountAfterBreakUp > 0)
            {
                m_BarCountAfterBreakUp++;
                m_gf.PrintMemo("向上突破, K线数 = " + m_BarCountAfterBreakUp);

                // 更新突破K线的高低点，就是合并突破后所有的K线
                BarData bar = GetBarData(BARCOUNT - 2);
                if (bar.Low < m_BreakBar.Low) m_BreakBar.Low = bar.Low;
                if (bar.High > m_BreakBar.High) m_BreakBar.High = bar.High;

                if (m_BarCountAfterBreakUp >= P_CheckBarCountAfterBreaked + 1)
                {
                    m_BarCountAfterBreakUp = 0;                    
                }                
            }
            if (m_BarCountAfterBreakDown > 0)
            {
                m_BarCountAfterBreakDown++;
                m_gf.PrintMemo("向下突破, K线数 = " + m_BarCountAfterBreakDown);

                BarData bar = GetBarData(BARCOUNT - 2);
                if (bar.Low < m_BreakBar.Low) m_BreakBar.Low = bar.Low;
                if (bar.High > m_BreakBar.High) m_BreakBar.High = bar.High;

                if (m_BarCountAfterBreakDown >= P_CheckBarCountAfterBreaked + 1)
                {
                    m_BarCountAfterBreakDown = 0;
                }
            }


            // 判断上一根K线是否突破上下极值点
            int iBreakDirection = IsBarBreakExtremePoint(BARCOUNT - 2);
            if (iBreakDirection > 0)
            {
                // 向上突破
                m_BreakBar = GetBarData(BARCOUNT - 2);
                m_gf.PrintMemo("Bar[" + m_BreakBar.CurDateTime.ToString("yyyy-MM-dd HH:mm:ss") + "] 向上突破极高点[" + m_lastHHPoint.High + "]");

                // 记录突破的K线,并开始启动模型中的做空检查
                m_BarCountAfterBreakUp = 1;
            }
            else if (iBreakDirection < 0)
            {
                // 向下突破
                m_BreakBar = GetBarData(BARCOUNT - 2);
                m_gf.PrintMemo("Bar[" + m_BreakBar.CurDateTime.ToString("yyyy-MM-dd HH:mm:ss") + "] 向下突破极低点[" + m_lastLLPoint.Low + "]");

                // 记录突破的K线,并开始启动模型中的做多检查
                m_BarCountAfterBreakDown = 1;
            }

            if (m_extremePoints.Count > 0) iStartPos = m_extremePoints[m_extremePoints.Count - 1].BarPosition + 1;
            FindExtremePoint(iStartPos, true);  // 寻找新的极值点
            GetLastHHLLPoint();                 // 获取最后极值高低点，此时，最后一根K线在此区间内
            PrintLastHHLLPoint();               // 打印出最后极值高低点

            DateTime t2 = Now;
            PrintLine("[E]-------------------------------------------------------" + (t2 - t1).TotalMilliseconds + " ms");

        }

        public override void OnTrade(Trade trade)
        {
            string msg = "成交: " + SYMBOL + " " + trade.Direction.ToString() + trade.OffsetFlag.ToString() + " " + trade.Price.ToString() + " * " + trade.Volume + " @ " +
                trade.TradeTime + "[" + trade.TradeID + ", " + trade.OrderSysID + "]";
            m_gf.PrintMemo(msg);

            // 设置止盈止损
            if (m_BreakBar != null)
            {
                double dStopLossPrice = 0;
                double dStopProfitPrice = 0;

                if (trade.Direction == EnumDirectionType.Buy)
                {
                    // 多单
                    dStopLossPrice = m_BreakBar.Low - INSTRUMENT.PriceTick;
                    dStopProfitPrice = trade.Price + (m_BreakBar.High - m_BreakBar.Low);
                }
                else if (trade.Direction == EnumDirectionType.Sell)
                {
                    // 空单
                    dStopLossPrice = m_BreakBar.High + INSTRUMENT.PriceTick;
                    dStopProfitPrice = trade.Price - (m_BreakBar.High - m_BreakBar.Low);
                }

                m_gf.PrintMemo("设置止损价 = " + dStopLossPrice + " 止盈价 = " + dStopProfitPrice);
                SetTakeProfitStopLoss(SYMBOL, trade.Direction, dStopLossPrice, trade.Volume, double.NaN, dStopProfitPrice, trade.Volume);
            }
            
            // 写成交日志
            
        }

        private int IsBarBreakExtremePoint(int iBarPos)
        {
            int iBreakDirection = 0;
            if (iBarPos > BARCOUNT - 1) return iBreakDirection;

            if (m_lastHHPoint != null && HIGH[iBarPos] > m_lastHHPoint.High && HIGH[iBarPos - 1] <= m_lastHHPoint.High)
            {
                // 突破极高点
                iBreakDirection = 1;
            }

            if (m_lastLLPoint != null && LOW[iBarPos] < m_lastLLPoint.Low && LOW[iBarPos - 1] >= m_lastLLPoint.Low)
            {
                // 突破极低点
                iBreakDirection = -1;
            }
            return iBreakDirection;
        }

        /// <summary>
        /// 加载本品种数据，包括本周期和大周期的
        /// </summary>
        private bool LoadMyData()
        {
            bool bLoadReault = true;
            
            m_gf.PrintMemo("加载本周期历史数据[" + DataCycle.ToString() + " " + SYMBOL + "]", ENU_msgType.msg_Info);
            UnLoadHisData();
            EnumMergeHisDataStatus status =  LoadHisData(SYMBOL, DataCycle.CycleBase, DataCycle.Repeat, P_MaxBarCount);

            if (status == EnumMergeHisDataStatus.Success)
            {            
                if (BARCOUNT < P_MaxBarCount)
                {
                    bLoadReault = false;
                    m_gf.PrintMemo("加载本周期数据错误: 数据量不够(" + CLOSE.Length + " < " + P_MaxBarCount + ").", ENU_msgType.msg_Error);
                }
            }
            else
            {
                bLoadReault = false;

                if (status == EnumMergeHisDataStatus.FileNotExist)
                {
                    m_gf.PrintMemo("加载本周期数据错误: 历史数据不存在.", ENU_msgType.msg_Error);
                }
                else
                {
                    m_gf.PrintMemo("加载本周期数据错误: 未知原因加载失败.", ENU_msgType.msg_Error);
                }
            }

            if (bLoadReault)
            {
                // 打印加载数据结果
                m_gf.PrintMemo("加载数据量: "+ BARCOUNT);
                m_gf.PrintMemo("数据起始[" + GetBarData(0).CurDateTime.ToString("yyyy-MM-dd HH:mm:ss") + "] 数据结束[" + GetBarData(BARCOUNT - 1).CurDateTime.ToString("yyyy-MM-dd HH:mm:ss") + "]");
            }
            return bLoadReault;
        }


        private void GetPosition()
        {
            Position longPosition = null;
            Position shortPosition = null;

            m_gf.GetPositionOfStrategy(m_StragetyInstanceID, out longPosition, out shortPosition);

        }

        /// <summary>
        /// 从起始位置开始寻找所有极值点
        /// </summary>
        /// <param name="iStartPos">起始位置</param>
        /// <param name="bPrint">是否打印过程信息</param>
        private void FindExtremePoint(int iStartPos = 0, bool bPrint = true)
        {
            ExtremePoint preExtremePoint = null;
            ExtremePoint extremePoint = null;
            
            for (int i = iStartPos; i < BARCOUNT; i++)
            {
                if (m_extremePoints.Count > 0)
                {
                    preExtremePoint = m_extremePoints[m_extremePoints.Count - 1];
                }
                else
                {
                    preExtremePoint = null;
                }     

                ENU_Extreme_Type extremeType = IsExtremePoint(i, P_LineMinimumBarCount, preExtremePoint);                

                if (extremeType == ENU_Extreme_Type.EXTREME_TYPE_HIGH || extremeType == ENU_Extreme_Type.EXTREME_THPE_LOW)
                {
                    BarData bar = GetBarData(i);
                    extremePoint = new ExtremePoint(bar);
                    extremePoint.ExtremeType = extremeType;
                    extremePoint.BarPosition = i;
                    extremePoint.Visible = true;

                    int result = AddExtremePointToList(extremePoint);

                    switch (result)
                    {
                        case -1:
                            if (bPrint) m_gf.PrintMemo("非法极值点,增加失败");
                            break;
                        case 1:
                            if (bPrint) m_gf.PrintMemo("极值点增加成功: Bar[" + bar.CurDateTime.ToString("yyyy-MM-dd HH:mm:ss") + "] 是一" + (extremeType == ENU_Extreme_Type.EXTREME_TYPE_HIGH ? "极高点" : "极低点"));
                            break;
                        case 2:
                            if (bPrint) m_gf.PrintMemo("极值点Bar[" + bar.CurDateTime.ToString("yyyy-MM-dd HH:mm:ss") + "]与最后一个极值点类型相同, 但级别更小, 故不增加");
                            break;
                        case 3:
                            if (bPrint) m_gf.PrintMemo("极值点Bar[" + bar.CurDateTime.ToString("yyyy-MM-dd HH:mm:ss") + "]与最后一个极值点类型相同, 但级别更大,故替换之");
                            break;
                        default:
                            break;
                    }
                    
                }
                else if (extremeType == ENU_Extreme_Type.EXTREME_TYPE_NONE_BUT_LLOW || extremeType == ENU_Extreme_Type.EXTREME_TYPE_NONE_BUT_HHIGH)
                {
                    // 不是极值点，但比前低更低或前高更高
                    BarData bar = GetBarData(i);
                    if (bPrint) m_gf.PrintMemo("Bar[" + bar.CurDateTime.ToString("yyyy-MM-dd HH:mm:ss") + "]不是极值点，但比前低更低或前高更高");
                    m_extremePoints.Remove(m_extremePoints[m_extremePoints.Count - 1]);
                    i--;
                }
                else
                {
                    // 不是极值点
                }                           
            }

            AdjustExtremePointVisual();         // 调整各个极值点的视野
        }

        /// <summary>
        /// 调整各个极值点的视野（可见性）
        /// </summary>
        private void AdjustExtremePointVisual()
        {
            ExtremePoint HHPoint = null;        // 极值点集合中的最高点
            ExtremePoint LLPoint = null;        // 极值点集合中的最低点
            ExtremePoint point = null;

            double dLastBarHigh = SF.Refdata(HIGH, 1);
            double dLastBarLow = SF.Refdata(LOW, 1);

            for (int i = m_extremePoints.Count - 1; i >= 0; i--)
            {
                point = m_extremePoints[i];

                if (point.Visible)
                {                
                    switch(point.ExtremeType)
                    {
                        case ENU_Extreme_Type.EXTREME_TYPE_HIGH:
                            if (HHPoint == null)
                            {
                                HHPoint = point;
                                if (point.High >= dLastBarHigh)
                                { 
                                    point.Visible = true;
                                }
                                else
                                {
                                    point.Visible = false;
                                }                            
                            }
                            else
                            {
                                if (point.High > HHPoint.High && point.High >= dLastBarHigh)
                                {
                                    point.Visible = true;
                                    HHPoint = point;
                                }
                                else
                                {
                                    point.Visible = false;
                                }
                            }
                            break;
                        case ENU_Extreme_Type.EXTREME_THPE_LOW:
                            if (LLPoint == null)
                            {
                                LLPoint = point;
                                if (point.Low <= dLastBarLow)
                                {
                                    point.Visible = true;
                                }
                                else
                                {
                                    point.Visible = false;
                                }                            
                            }
                            else
                            {
                                if (point.Low < LLPoint.Low && point.Low <= dLastBarLow)
                                {
                                    point.Visible = true;
                                    LLPoint = point;
                                }
                                else
                                {
                                    point.Visible = false;
                                }
                            }
                            break;
                        default:
                            break;
                    }
                }
            }
        }

        /// <summary>
        /// 增加极值点到极值点列表中
        /// </summary>
        /// <param name="extremePoint">极值点</param>
        /// <returns>
        /// -1: 非法极值点增加
        /// 1: 增加成功
        /// 2: 极值点与最后一个极值点类型相同,但级别更小,故不增加
        /// 3: 极值点与最后一个极值点类型相同,但级别更大,故替换之
        /// </returns>
        private int AddExtremePointToList(ExtremePoint extremePoint)
        {
            int iAddResult = -1;
            
            if (extremePoint == null)
            {
                return iAddResult;
            }

            ENU_Extreme_Type extremeType = extremePoint.ExtremeType;
            if (extremeType == ENU_Extreme_Type.EXTREME_TYPE_NONE)
            {
                return iAddResult;
            }
                        

            if (m_extremePoints.Count <= 0)
            {
                // 极值点列表中没有极值点 => 直接增加极值点
                m_extremePoints.Add(extremePoint);
                iAddResult = 1;
            }
            else
            {
                ExtremePoint lastExtremePoint = m_extremePoints[m_extremePoints.Count - 1];     // 最后一个极值点

                if (lastExtremePoint.ExtremeType == extremeType)
                {
                    iAddResult = 2;

                    // 列表中最后一个极值点的类型 == 新的极值点的类型
                    if (extremeType == ENU_Extreme_Type.EXTREME_TYPE_HIGH)       // 极高点
                    {
                        // 新的极值点的高点 >= 最后一个极值点的高点 => 新的极值点替换最后一个极值点
                        if (extremePoint.High >= lastExtremePoint.High)
                        {
                            m_extremePoints.Remove(lastExtremePoint);
                            m_extremePoints.Add(extremePoint);
                            iAddResult = 3;
                        }
                    }
                    else if (extremeType == ENU_Extreme_Type.EXTREME_THPE_LOW)  // 极低点
                    {
                        // 新的极值点的低点 <= 最后一个极值点的低点 => 新的极值点替换最后一个极值点
                        if (extremePoint.Low <= lastExtremePoint.Low)
                        {
                            m_extremePoints.Remove(lastExtremePoint);
                            m_extremePoints.Add(extremePoint);
                            iAddResult = 3;
                        }
                    }
                }
                else
                {
                    // 在列表中增加新的极值点
                    m_extremePoints.Add(extremePoint);
                    iAddResult = 1;
                }
            }

            return iAddResult;
        }

        /// <summary>
        /// 判断K线是否是一个极值点
        /// </summary>
        /// <param name="iBarPosition">K线索引</param>
        /// <param name="iLineMinimumBarCount">一笔所需要的最少K线数</param>
        /// <param name="preExtremePoint">上一个极值点</param>
        /// <returns>K线的类型</returns>
        private ENU_Extreme_Type IsExtremePoint(int iBarPosition, int iLineMinimumBarCount, ExtremePoint preExtremePoint = null)
        {
            int iLeftStartPos = 0;
            int iRightEndPos = 0;

            ENU_Extreme_Type extremeType = ENU_Extreme_Type.EXTREME_TYPE_NONE;

            if ( (iBarPosition + iLineMinimumBarCount > BARCOUNT) || (iBarPosition - iLineMinimumBarCount + 1 < 0))
            {
                // 这根K线的左右没有足够的K线 => 此K线目前不能判断高低点
                extremeType = ENU_Extreme_Type.EXTREME_TYPE_NONE;
            }
            else
            {
                if (preExtremePoint == null)
                {
                    iLeftStartPos = iBarPosition - iLineMinimumBarCount + 1;
                }
                else
                {
                    iLeftStartPos = preExtremePoint.BarPosition;
                }

                iRightEndPos = iBarPosition + iLineMinimumBarCount - 1;

                
                if (SF.HhvData(HIGH, iLeftStartPos, iBarPosition, true, true) == HIGH[iBarPosition] && SF.HhvData(HIGH, iBarPosition, iRightEndPos, true, true) == HIGH[iBarPosition])
                {
                    // 是一个潜在的极高点  => 继续判断 
                    int iLeftRegionStartPos = 0;
                    if (preExtremePoint == null)
                    {
                        iLeftRegionStartPos = SF.FindSunkenRegion_Left(HIGH, iBarPosition);
                    }
                    else
                    {
                        iLeftRegionStartPos = preExtremePoint.BarPosition;
                    }
                    int iRightRegionEndPos = SF.FindSunkenRegion_Right(HIGH, iBarPosition);

                    int iLeftRegionLLVPos = SF.LlvPos(LOW, iLeftRegionStartPos, iBarPosition);
                    int iRightRegionLLVPos = SF.LlvPos(LOW, iBarPosition, iRightRegionEndPos);

                    if (iBarPosition - iLeftRegionLLVPos + 1 >= iLineMinimumBarCount && iRightRegionLLVPos - iBarPosition + 1 >= iLineMinimumBarCount)
                    {
                        // 这是一个极高点
                        extremeType = ENU_Extreme_Type.EXTREME_TYPE_HIGH;
                    }
                }
                else if (SF.LlvData(LOW, iLeftStartPos, iBarPosition, true, true) == LOW[iBarPosition] && SF.LlvData(LOW, iBarPosition, iRightEndPos, true, true) == LOW[iBarPosition])
                {
                    // 是一个潜在的极低点
                    int iLeftRegionStartPos = 0;
                    if (preExtremePoint == null)
                    {
                        iLeftRegionStartPos = SF.FindBulgeRegion_Left(LOW, iBarPosition);
                    }
                    else
                    {
                        iLeftRegionStartPos = preExtremePoint.BarPosition;
                    }
                    int iRightRegionEndPos = SF.FindBulgeRegion_Right(LOW, iBarPosition);

                    int iLeftRegionHHVPos = SF.HhvPos(HIGH, iLeftRegionStartPos, iBarPosition);
                    int iRightRegionHHVPos = SF.HhvPos(HIGH, iBarPosition, iRightRegionEndPos);

                    if (iBarPosition - iLeftRegionHHVPos +  1 >= iLineMinimumBarCount && iRightRegionHHVPos - iBarPosition + 1 >= iLineMinimumBarCount)
                    {
                        // 这是一个极低点
                        extremeType = ENU_Extreme_Type.EXTREME_THPE_LOW;
                    }
                }
                else
                {
                    // 不是一个极值点
                }                
            }

            // 检查若此点不是极值点的情况下，是否比前低更低或者比前高更高
            if (extremeType == ENU_Extreme_Type.EXTREME_TYPE_NONE && preExtremePoint != null)
            {
                BarData bar = GetBarData(iBarPosition);

                switch (preExtremePoint.ExtremeType)
                {
                    case ENU_Extreme_Type.EXTREME_THPE_LOW:
                        if (bar.Low <= preExtremePoint.Low)
                        {
                            extremeType = ENU_Extreme_Type.EXTREME_TYPE_NONE_BUT_LLOW;
                        }
                        break;
                    case ENU_Extreme_Type.EXTREME_TYPE_HIGH:
                        if (bar.High >= preExtremePoint.High)
                        {
                            extremeType = ENU_Extreme_Type.EXTREME_TYPE_NONE_BUT_HHIGH;
                        }
                        break;
                    default:
                        break;
                }
            }

            return extremeType;
        }

        /// <summary>
        /// 获取最后的极高和极低点
        /// </summary>
        private void GetLastHHLLPoint()
        {
            ExtremePoint point = null;
            m_lastHHPoint = null;
            m_lastLLPoint = null;

            for (int i = m_extremePoints.Count - 1; i >= 0; i--)
            {
                point = m_extremePoints[i];

                if (point.Visible && point.ExtremeType == ENU_Extreme_Type.EXTREME_TYPE_HIGH && m_lastHHPoint == null)
                {
                    m_lastHHPoint = point;
                }
                else if (point.Visible && point.ExtremeType == ENU_Extreme_Type.EXTREME_THPE_LOW && m_lastLLPoint == null)
                {
                    m_lastLLPoint = point;
                }
            }
        }

        /// <summary>
        /// 打印所有的极值点
        /// </summary>
        /// <param name="bOnlyPrintVisible">是否只打印可见的极值点</param>
        private void PrintAllExtremePoint(bool bOnlyPrintVisible = false)
        { 
            for (int i = 0; i < m_extremePoints.Count; i++)
            {
                if ( (bOnlyPrintVisible && m_extremePoints[i].Visible == true) || (! bOnlyPrintVisible) )
                {
                    m_gf.PrintMemo(m_extremePoints[i].ToString());
                }                
            }
        }

        /// <summary>
        /// 打印最后的极高和极低点
        /// </summary>
        private void PrintLastHHLLPoint()
        {
            if (m_lastHHPoint != null)
            {
                m_gf.PrintMemo(m_lastHHPoint.ToString());
            }

            if (m_lastLLPoint != null)
            {
                m_gf.PrintMemo(m_lastLLPoint.ToString());
            }
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
                    lots = (int)Math.Round(totalCapital * capitalRatio / (openPrice * INSTRUMENT.VolumeMultiple * Math.Max(INSTRUMENT.LongMarginRatioByMoney, INSTRUMENT.ShortMarginRatioByMoney)), 0);
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
    }
}
