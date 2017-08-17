using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DevelopLibrary.Enums;
using DevelopLibrary.DevelopAPI;

namespace mylib
{
    class DualMA : EventStrategyBase
    {
        #region
        [Parameter(Display = "W", Description = "", Category = "MA")]
        int W = 1;

        [Parameter(Display = "LengthQuick", Description = "Length of MAQuick", Category = "MA")]
        int LengthQuick = 5;

        [Parameter(Display = "LengthSlow", Description = "Length of MASlow", Category = "MA")]
        int LengthSlow = 13;

        [Parameter(Display = "Qty", Description = "Qty", Category = "Trade")]
        int Qty = 3;
        #endregion


        #region
        bool CanOpen = true;
        DateTime CloseTime;
        bool HasCloseAll = false;
        Instrument instrument = null;
        #endregion

        public override void OnStart()
        {            
            //DateTime EndT = new DateTime(SERVERTIME.Year, SERVERTIME.Month, SERVERTIME.Day, 15, 15, 0);
            // CloseTime = EndT.AddSeconds(-20);
            instrument = GetInstrument(SYMBOL);
            //LoadHisData(EnumDataCycle.MINUTE, 1);
            CreateChart("Main#基础.MA(" + LengthQuick + ")#基础.MA(" + LengthSlow + ");VOLMA(6);测试.w_SLOWKD(15,4,2,5)");

            List<Position> lp = GetPositionList(SYMBOL);
            for (int i = 0; i < lp.Count; i++)
            {
                if (lp[i].TodayPosition + lp[i].YdPosition > 0)
                {
                    CanOpen = false;//处理隔夜仓        
                    break;
                }
            }
        }

        public override void OnTick(TickData td)
        {
            /*
            if (C.Length > LengthSlow)//当数据达到一定长度才计算
            {
                //第一种计算。截短数据，这样计算只用到所需要的最长数据
                //DataArray tempc = new DataArray(C, LengthSlow + 1);
                //DataArray maquick = MA(tempc, LengthQuick);
                //DataArray maslow = MA(tempc, LengthSlow);
                //DataArray crossUP = CROSS(maquick, maslow);
                //DataArray crossDown = CROSS(maslow, maquick);
                //bool bcrossUP = crossUP.LAST > 0;
                //bool bcrossDown = crossDown.LAST > 0;

                //第二种计算。不截数据
                //DataArray maquick = MA(C, LengthQuick);
                //DataArray maslow = MA(C, LengthSlow);
                //DataArray crossUP = CROSS(maquick, maslow);
                //DataArray crossDown = CROSS(maslow, maquick);
                //bool bcrossUP=crossUP.LAST > 0;
                //bool bcrossDown=crossDown.LAST > 0;

                //第三种计算。最快
                double maquick1 = CustMA(LengthQuick, C.Length - 2);//快速线倒数第二个数值
                double maquick2 = CustMA(LengthQuick, C.Length - 1);//快速线倒数第二个数值
                double maslow1 = CustMA(LengthSlow, C.Length - 2);//慢速线倒数第二个数值
                double maslow2 = CustMA(LengthSlow, C.Length - 1);//慢速线倒数第二个数值

                bool bcrossUP = false;
                bool bcrossDown = false;

                if (maquick1 < maslow1 && maquick2 > maslow2)
                    bcrossUP = true;
                else if (maquick1 > maslow1 && maquick2 < maslow2)
                    bcrossDown = true;

                /////////////////////////////////////////////////
                if (CanOpen)
                {
                    if (bcrossUP)
                    {
                        //PrintLine(SERVERTIME.ToString("HH:mm:ss.fff") + "快速均线上穿慢速均线,开多头仓位");
                        //在卖一的基础上+3跳开仓 做多
                        double ordPrice = ASKPRICE(1).LASTVALUE + 3 * instrument.PriceTick;
                        OpenBuy(ordPrice, Qty);
                        CanOpen = false;
                    }
                    else if (bcrossDown)
                    {
                        //PrintLine(SERVERTIME.ToString("HH:mm:ss.fff") + "快速均线下穿慢速均线,开空头仓位");
                        //在买一的基础上-3跳开仓 做空
                        double ordPrice = BIDPRICE(1).LASTVALUE - 3 * instrument.PriceTick;
                        OpenSell(ordPrice, Qty);
                        CanOpen = false;
                    }
                }
                else
                {
                    //如果下穿则平仓
                    if (bcrossDown)
                    {
                        Position p = GetPosition(SYMBOL, EnumDirectionType.Buy);

                        //PrintLine(SERVERTIME.ToString("HH:mm:ss.fff") + "快速均线下穿慢速均线,平掉持有的多单,再开空头仓位");
                        //在卖一的基础上-3跳平仓
                        double ordPrice = BIDPRICE(1).LASTVALUE - 3 * instrument.PriceTick;
                        if (INSTRUMENT.ExchangeID == EnumExchange.SHFE)
                        {
                            if (p.TodayPosition > 0)
                                CloseSellToday(ordPrice, Qty);
                            else if (p.YdPosition > 0)
                                CloseSell(ordPrice, Qty);
                        }
                        else if (p.TodayPosition + p.YdPosition > 0)
                            CloseSell(ordPrice, Qty);


                    }
                    else if (bcrossUP)
                    {
                        Position p = GetPosition(SYMBOL, EnumDirectionType.Sell);

                        //PrintLine(SERVERTIME.ToString("HH:mm:ss.fff") + "快速均线上穿慢速均线,平掉持有的空单,再开多头仓位");
                        //在卖一的基础上+3跳平仓
                        double ordPrice = ASKPRICE(1).LASTVALUE + 3 * instrument.PriceTick;
                        if (INSTRUMENT.ExchangeID == EnumExchange.SHFE)
                        {
                            if (p.TodayPosition > 0)
                                CloseBuyToday(ordPrice, Qty);
                            else if (p.YdPosition > 0)
                                CloseBuy(ordPrice, Qty);
                        }
                        else if (p.TodayPosition + p.YdPosition > 0)
                            CloseBuy(ordPrice, Qty);
                    }
                    // }
                }
            }
             */


        }

        public override void OnBarOpen(TickData td)
        {
            
            PrintLine(td.UpdateTime.ToString() + ": Close[0] = " + CLOSE[CLOSE.Length - 1].ToString() + " Close[1] = " + CLOSE[CLOSE.Length - 2].ToString());

            if (C.Length > LengthSlow)//当数据达到一定长度才计算
            {
                //第一种计算。截短数据，这样计算只用到所需要的最长数据
                //DataArray tempc = new DataArray(C, LengthSlow + 1);
                //DataArray maquick = MA(tempc, LengthQuick);
                //DataArray maslow = MA(tempc, LengthSlow);
                //DataArray crossUP = CROSS(maquick, maslow);
                //DataArray crossDown = CROSS(maslow, maquick);
                //bool bcrossUP = crossUP.LAST > 0;
                //bool bcrossDown = crossDown.LAST > 0;

                //第二种计算。不截数据
                //DataArray maquick = MA(C, LengthQuick);
                //DataArray maslow = MA(C, LengthSlow);
                //DataArray crossUP = CROSS(maquick, maslow);
                //DataArray crossDown = CROSS(maslow, maquick);
                //bool bcrossUP=crossUP.LAST > 0;
                //bool bcrossDown=crossDown.LAST > 0;

                //第三种计算。最快
                double maquick1 = CustMA(LengthQuick, C.Length - 2);//快速线倒数第二个数值
                double maquick2 = CustMA(LengthQuick, C.Length - 1);//快速线倒数第二个数值
                double maslow1 = CustMA(LengthSlow, C.Length - 2);//慢速线倒数第二个数值
                double maslow2 = CustMA(LengthSlow, C.Length - 1);//慢速线倒数第二个数值

                bool bcrossUP = false;
                bool bcrossDown = false;

                if (maquick1 < maslow1 && maquick2 > maslow2)
                    bcrossUP = true;
                else if (maquick1 > maslow1 && maquick2 < maslow2)
                    bcrossDown = true;

                /////////////////////////////////////////////////
                /*
                if (CanOpen)
                {
                    if (bcrossUP)
                    {
                        //PrintLine(SERVERTIME.ToString("HH:mm:ss.fff") + "快速均线上穿慢速均线,开多头仓位");
                        //在卖一的基础上+3跳开仓 做多
                        double ordPrice = ASKPRICE(1).LASTVALUE + 3 * instrument.PriceTick;
                        OpenBuy(ordPrice, Qty);
                        CanOpen = false;
                    }
                    else if (bcrossDown)
                    {
                        //PrintLine(SERVERTIME.ToString("HH:mm:ss.fff") + "快速均线下穿慢速均线,开空头仓位");
                        //在买一的基础上-3跳开仓 做空
                        double ordPrice = BIDPRICE(1).LASTVALUE - 3 * instrument.PriceTick;
                        OpenSell(ordPrice, Qty);
                        CanOpen = false;
                    }
                }
                else
                {
                    //如果下穿则平仓
                    if (bcrossDown)
                    {
                        Position p = GetPosition(SYMBOL, EnumDirectionType.Buy);

                        //PrintLine(SERVERTIME.ToString("HH:mm:ss.fff") + "快速均线下穿慢速均线,平掉持有的多单,再开空头仓位");
                        //在卖一的基础上-3跳平仓
                        double ordPrice = BIDPRICE(1).LASTVALUE - 3 * instrument.PriceTick;
                        if (INSTRUMENT.ExchangeID == EnumExchange.SHFE)
                        {
                            if (p.TodayPosition > 0)
                                CloseSellToday(ordPrice, Qty);
                            else if (p.YdPosition > 0)
                                CloseSell(ordPrice, Qty);
                        }
                        else if (p.TodayPosition + p.YdPosition > 0)
                            CloseSell(ordPrice, Qty);


                    }
                    else if (bcrossUP)
                    {
                        Position p = GetPosition(SYMBOL, EnumDirectionType.Sell);

                        //PrintLine(SERVERTIME.ToString("HH:mm:ss.fff") + "快速均线上穿慢速均线,平掉持有的空单,再开多头仓位");
                        //在卖一的基础上+3跳平仓
                        double ordPrice = ASKPRICE(1).LASTVALUE + 3 * instrument.PriceTick;
                        if (INSTRUMENT.ExchangeID == EnumExchange.SHFE)
                        {
                            if (p.TodayPosition > 0)
                                CloseBuyToday(ordPrice, Qty);
                            else if (p.YdPosition > 0)
                                CloseBuy(ordPrice, Qty);
                        }
                        else if (p.TodayPosition + p.YdPosition > 0)
                            CloseBuy(ordPrice, Qty);
                    }
                    // }
                }
                 */
            }
        }

        private double CustMA(int count, int lastIndex)
        {
            double sum = 0;
            for (int i = lastIndex; i >= lastIndex - count + 1; i--)
            {
                sum = sum + C[i];
            }
            return sum / count;
        }

        public override void OnTrade(Trade trade)
        {

            if (trade.OffsetFlag == EnumOffsetFlagType.Open)
            {
                //PrintLine("开仓成功,方向" + trade.Direction.ToString());
            }
            else
            {
                if (trade.Direction == EnumDirectionType.Sell)
                {
                    double ordPrice = BIDPRICE(1).LASTVALUE - 3 * instrument.PriceTick;
                    //先平后开		
                    // PrintLine(SERVERTIME.ToString("HH:mm:ss.fff") + "反手开仓做空");
                    //OpenSell(ordPrice, Qty);
                    OpenSell(ordPrice, trade.Volume);
                }
                else
                {
                    double ordPrice = ASKPRICE(1).LASTVALUE + 3 * instrument.PriceTick;
                    //先平后开						
                    //PrintLine(SERVERTIME.ToString("HH:mm:ss.fff") + "反手开仓做多");
                    //OpenBuy(ordPrice, Qty);
                    OpenBuy(ordPrice, trade.Volume);
                }
            }
        }

        public override void OnOrderRejected(Order order)
        {
            PrintLine(order.OrderStatus);
        }
    }
}
