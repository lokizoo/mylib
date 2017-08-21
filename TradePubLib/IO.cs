using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DevelopLibrary.DevelopAPI;

namespace TradePubLib
{
    public class IO : IndicatorBase
    {
        public enum ENU_msgType 
        {
            msg_Error = 1,
            msg_Info = 2
        }
         
        /*
        public static void PrintMemo(string msg, DateTime time, ENU_msgType msgType = ENU_msgType.msg_Info, bool bPrintTime = true)
        {
            string sType = "";            
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
         * */

    }
}
