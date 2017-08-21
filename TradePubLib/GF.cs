using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DevelopLibrary.DevelopAPI;

namespace TradePubLib
{
    public class GF
    {
        public enum ENU_msgType
        {
            msg_Error = 1,
            msg_Info = 2
        }

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
}
