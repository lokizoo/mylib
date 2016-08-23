using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DevelopLibrary.Enums;
using DevelopLibrary.DevelopAPI;

namespace mylib
{
    class Test_OnBarOpen : EventStrategyBase
    {
        private int m_iTickCount = 0;
        private int Close_TimeOut = 3;

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

        public DateTime TickNow
        {
            get { return SERVERTIME; }
        }


        public override void OnBarOpen(TickData td)
        {
            //PrintLine("Test OnBarOpen:" + DateTime.Now.ToString("HH:mm:ss.fff"));
        }

        public override void OnTick(TickData td)
        {
            // -------------------------------------------------------------------
            // 测试未成交的挂单
            //m_iTickCount++;
            //PrintLine("TickCoount = " + m_iTickCount);

            /*
            // 买入测试头寸
            if (m_iTickCount == 20)
            {
                double dOpenPrice = td.AskPrice1;
                int lots = 1;
                PrintLine("开仓");
                OpenOrder(dOpenPrice, lots, 0, INSTRUMENT.PriceTick, EnumDirectionType.Buy);
            }

            // 平仓
            if (m_iTickCount == 40)
            {
                double dClosePrice = 4007;
                PrintLine("平仓");
                CloseLongPosition(dClosePrice, 1, 0, INSTRUMENT.PriceTick);
            }
            // 检查是否有超时未成交的挂单（平仓单）
            CheckAllOrder_Timeout();
            // -------------------------------------------------------------------
            */
        }

        public override void OnOrderReturn(Order order)
        {
            PrintLine("OnOrderReturn");
            GetPosition();
        }

        public override void OnTrade(Trade trade)
        {
            PrintLine("OnTrade");
        }

        public override void OnPosition(Trade trade)
        {
            PrintLine("OnPosition");
        }

        public override void OnCancelOrderSucceeded(Order order)
        {
            PrintLine("OnCancelOrderSucceeded");
        }

        private void GetPosition()
        {
            int position_TD;
            int position_YD;
            int shortPosition_TD;
            int shortPosition_YD;

            // 获取本策略开仓的具体仓位            
            List<Position> lp = GetMyPositionList();
            int longPosition = CalculateLongPosition(lp, out position_TD, out position_YD);
            int shortPosition = CalculateShortPosition(lp, out shortPosition_TD, out shortPosition_YD);

            List<Order> orders = GetUnTraded_CloseOrder();

            foreach (Order order in orders)
            {
                PrintLine("Un Traded Close Order = " + order.ToString());
            }
            //PrintMemo("获取" + SYMBOL + "[MyID: " + MyID + "]持仓头寸: ", ENU_msgType.msg_Info);
            //PrintMemo("  -> {多[今，昨], 空[今，昨]} = {" + longPosition.ToString() + "[" + m_longPosition_TD + ", " + m_longPosition_YD + "], " +
            //    shortPosition.ToString() + "[" + m_shortPosition_TD + ", " + m_shortPosition_YD + "]}");            
            PrintLine("获取[" + SYMBOL + "]持仓头寸: [多, 空] = [" + longPosition + ", " + shortPosition + "]");

        }
        private int CalculateLongPosition(List<Position> posList, out int position_TD, out int position_YD)
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
                if (pos.PosiDirection == EnumPosiDirectionType.Long)
                {
                    position_TD += pos.TodayPosition;
                    position_YD += pos.YdPosition;
                }
            }

            lots = position_TD + position_YD;

            return lots;
        }
                
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

        private List<Order> GetUnTraded_CloseOrder()
        {
            List<Order> unTradedOrdLst = new List<Order>();
            List<Order> ordLst = GetUnAllTradedOrderList(SYMBOL);

            if (ordLst != null && ordLst.Count > 0)
            {
                PrintLine("有未成交单 = " + ordLst.Count.ToString() + ":");
                foreach (Order order in ordLst)
                {
                    PrintLine("    -> " + order.ToString());
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

        private List<Position> GetMyPositionList()
        {
            return GetPositionList(SYMBOL);
        }

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

        private void CloseLongPosition(double price, int volume, int slipPoint, double priceTick, EnumOrderType orderType = EnumOrderType.限价单)
        {
            CloseFuturesPositions(SYMBOL, EnumDirectionType.Buy, price, volume, slipPoint, orderType, EnumHedgeFlag.投机);        
        }

        private void CheckAllOrder_Timeout()
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
                            PrintLine("Dt = " + Dt);

                            if (Dt > Close_TimeOut)//超时未成交
                            {
                                PrintLine("有超时未成交平仓单");
                                CancelOrder(order);         // 撤单                                    
                            }
                        }
                    }
                }
            }
           
        }

        private double CalculateOrderDuration(Order order)
        {
            double Dt = (TickNow - ConvertToDateTime(order.InsertDate, order.InsertTime)).TotalSeconds;

            if (TickNow.ToString("HH:mm:ss").CompareTo(RestT1.ToString("HH:mm:ss")) >= 0 && order.InsertTime.Substring(0, 8).CompareTo(RestT0.ToString("HH:mm:ss")) <= 0)
            {
                Dt = Dt - (RestT2 - RestT0).TotalSeconds;       // 除去早盘休息时间
            }
            else if (TickNow.ToString("HH:mm:ss").CompareTo(RestT3.ToString("HH:mm:ss")) >= 0 && order.InsertTime.Substring(0, 8).CompareTo(RestT2.ToString("HH:mm:ss")) <= 0)
            {
                Dt = Dt - (RestT3 - RestT2).TotalSeconds;       // 除去中午休息时间
            }

            return Dt;
        }
    }
}
