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
        public int MaxBarCount = 100;

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

        public override void OnStart()
        {
            m_initOK = true;

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
                FindExtremePoint();

            }
            // 创建图表
            CreateChart("Main;测试.w_SLOWKD(15,4,2,5)");
            
            m_gf.PrintMemo("启动完成", ENU_msgType.msg_Info);
        }

        public override void OnTick(TickData td)
        {
            base.OnTick(td);
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
                if (this.CLOSE.Length < MaxBarCount)
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
            return bLoadReault;
        }

        private void GetPosition()
        {
            Position longPosition = null;
            Position shortPosition = null;

            m_gf.GetPositionOfStrategy(MyID, out longPosition, out shortPosition);

        }

        private void FindExtremePoint()
        {
            for (int i = 0; i < BARCOUNT; i++)
            {
                if (IsExtremePoint(i, LineMinimumBarCount) == ENU_Extreme_Type.EXTREME_TYPE_HIGH)
                {
                    BarData bar = GetBarData(i);
                    m_gf.PrintMemo("Bar[" + bar.CurDateTime.ToString("yyyy-MM-dd HH:mm:ss") + "] 是一个极高点");
                }
            }
        }

        private void FindFirstExtremePoint()
        {

        }

        private ENU_Extreme_Type IsExtremePoint(int iBarPosition, int iLineMinimumBarCount)
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
                iLeftStartPos = iBarPosition - iLineMinimumBarCount + 1;
                iRightEndPos = iBarPosition + iLineMinimumBarCount - 1;

                
                if (SF.HhvData(HIGH, iLeftStartPos, iBarPosition, true, true) == HIGH[iBarPosition] && SF.HhvData(HIGH, iBarPosition, iRightEndPos, true, true) == HIGH[iBarPosition])
                {
                    // 是一个潜在的极高点  => 继续判断 
                    int iLeftRegionStartPos = SF.FindSunkenRegion_Left(HIGH, iBarPosition);
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

                }
                else
                {
                    // 不是一个极值点
                }
                
            }


            return extremeType;
        }

    }
}
