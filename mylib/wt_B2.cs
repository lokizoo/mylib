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
        public int LineMinimumBarCount = 4;

        [Parameter(Display = "最大K线数", Description = "两个头部(底部)之间最大的K线数", Category = "模型")]
        public int MaxBarCount = 200;

        [Parameter(Display = "止损类型", Description = "止损类型（0：不设置止损； 1：ATR移动止损法", Category = "模型")]
        public int StopLossType = 0;

        [Parameter(Display = "止盈类型", Description = "止盈类型（0：不设置止损； 1：ATR移动止损法", Category = "模型")]
        public int StopProfitType = 0;

        [Parameter(Display = "最大头寸", Description = "最大可持仓的头寸", Category = "资管")]
        public int MaxLot = 1;

        [Parameter(Display = "开仓资金系数", Description = "头寸开仓资金百分比 {[0, 1]: 百分比计算头寸; > 1: 固定资金计算头寸 ", Category = "资管")]
        public double CapitalRatio = 0.5;


        [Parameter(Display = "开仓滑点", Description = "开仓所允许的滑点", Category = "交易")]
        public int Open_SlipPoint = 0;

        [Parameter(Display = "平仓滑点", Description = "平仓所允许的滑点", Category = "交易")]
        public int Close_SlipPoint = 0;

        [Parameter(Display = "识别码", Description = "区分持仓所用的识别码", Category = "其它")]
        public string MyID = "b2001";

        [Parameter(Display = "版本号", Description = "模型的版本号", Category = "其它", IsReadOnly = true)]
        public string MyVersion = C_VERSION;

        [Parameter(Display = "写成交日志", Description = "是否记录成交的日志", Category = "其它")]
        public bool IsRecordTrade = false;

        [Parameter(Display = "自动运行", Description = "是否在设定的事件自动运行和停止", Category = "其它")]
        public bool IsAutoRun = true;
        #endregion

        private bool m_initOK = true;           // OnStart事件中初始化是否成功
        private GF m_gf = null;                 // 通用函数对象
        private List<ExtremePoint> m_extremePoints = null;      // 极值点列表

        private ExtremePoint m_lastHHPoint = null;              // 最后一个极值高低
        private ExtremePoint m_lastLLPoint = null;              // 最后一个极值低点

        public override void OnStart()
        {
            DateTime t1 = Now;

            m_initOK = true;
            m_extremePoints = new List<ExtremePoint>();

            // 创建通用函数对象
            m_gf = GF.GetGenericFun(this);

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
                // TODO 找出图表中的高低点
                FindExtremePoint(0, false);
                PrintAllExtremePoint();

            }
            // 创建图表
            CreateChart("Main;测试.w_SLOWKD(15,4,2,5)");

            DateTime t2 = Now;
            m_gf.PrintMemo("启动完成[" + (t2 - t1).TotalMilliseconds + " ms]", ENU_msgType.msg_Info);
        }

        public override void OnTick(TickData td)
        {
            base.OnTick(td);
        }

        public override void OnBarOpen(TickData td)
        {
            PrintLine(m_gf.TickNow.ToString("yyyy-MM-dd HH:mm:ss") + "[S]----------------------------------------");
            DateTime t1 = Now;

            int iStartPos = 0;
            if (m_extremePoints.Count > 0) iStartPos = m_extremePoints[m_extremePoints.Count - 1].BarPosition + 1;

            FindExtremePoint(iStartPos, true);  // 寻找新的极值点
            GetLastHHLLPoint();                 // 获取最后极值高低点，此时，最后一根K线在此区间内
            PrintLastHHLLPoint();               // 打印出最后极值高低点

            DateTime t2 = Now;
            PrintLine("[E]-------------------------------------------------------" + (t2 - t1).TotalMilliseconds + " ms");

        }

        /// <summary>
        /// 加载本品种数据，包括本周期和大周期的
        /// </summary>
        private bool LoadMyData()
        {
            bool bLoadReault = true;
            
            m_gf.PrintMemo("加载本周期历史数据[" + DataCycle.ToString() + " " + SYMBOL + "]", ENU_msgType.msg_Info);
            UnLoadHisData();
            EnumMergeHisDataStatus status =  LoadHisData(SYMBOL, DataCycle.CycleBase, DataCycle.Repeat, MaxBarCount);

            if (status == EnumMergeHisDataStatus.Success)
            {            
                if (BARCOUNT < MaxBarCount)
                {
                    bLoadReault = false;
                    m_gf.PrintMemo("加载本周期数据错误: 数据量不够(" + CLOSE.Length + " < " + MaxBarCount + ").", ENU_msgType.msg_Error);
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

            m_gf.GetPositionOfStrategy(MyID, out longPosition, out shortPosition);

        }

        private void FindExtremePoint(int iStartPos = 0, bool bPrint = true)
        {
            ExtremePoint preExtremePoint = null;
            ExtremePoint extremePoint = null;
            bool bHaveChaned = false;           // 极值点是否有变化

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

                ENU_Extreme_Type extremeType = IsExtremePoint(i, LineMinimumBarCount, preExtremePoint);                

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
                            bHaveChaned = true;
                            if (bPrint) m_gf.PrintMemo("极值点增加成功: Bar[" + bar.CurDateTime.ToString("yyyy-MM-dd HH:mm:ss") + "] 是一" + (extremeType == ENU_Extreme_Type.EXTREME_TYPE_HIGH ? "极高点" : "极低点"));
                            break;
                        case 2:
                            if (bPrint) m_gf.PrintMemo("极值点Bar[" + bar.CurDateTime.ToString("yyyy-MM-dd HH:mm:ss") + "]与最后一个极值点类型相同, 但级别更小, 故不增加");
                            break;
                        case 3:
                            bHaveChaned = true;
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
                    bHaveChaned = true;
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
        /// <param name="extremeType">极值点的类型</param>
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
    }
}
