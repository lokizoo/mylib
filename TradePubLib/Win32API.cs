using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.InteropServices;

namespace TradePubLib
{
    public struct SYSTEMTIME
    {
        public ushort wYear;
        public ushort wMonth;
        public ushort wDayOfWeek;
        public ushort wDay;
        public ushort wHour;
        public ushort wMinute;
        public ushort wSecond;
        public ushort wMilliseconds;

        /// <summary>
        /// 从System.DateTime转换。
        /// </summary>
        /// <param name="time">System.DateTime类型的时间。</param>
        public void From(DateTime time)
        {
            wYear = (ushort)time.Year;
            wMonth = (ushort)time.Month;
            wDayOfWeek = (ushort)time.DayOfWeek;
            wDay = (ushort)time.Day;
            wHour = (ushort)time.Hour;
            wMinute = (ushort)time.Minute;
            wSecond = (ushort)time.Second;
            wMilliseconds = (ushort)time.Millisecond;
        }

        public void From(int iYear, int iMonth, int iDay, int iHour, int iMiniute, int iSecond, int iMillisecond)
        {
            DateTime time = new DateTime(iYear, iMonth, iDay, iHour, iMiniute, iSecond, iMillisecond);
            this.From(time);
        }

        public void From(string s, string format)
        {
            DateTime time = DateTime.ParseExact(s, format, System.Globalization.CultureInfo.InvariantCulture);
            this.From(time);
        }
        /// <summary>
        /// 转换为System.DateTime类型。
        /// </summary>
        /// <returns></returns>
        public DateTime ToDateTime()
        {
            return new DateTime(wYear, wMonth, wDay, wHour, wMinute, wSecond, wMilliseconds);
        }
        /// <summary>
        /// 静态方法。转换为System.DateTime类型。
        /// </summary>
        /// <param name="time">SYSTEMTIME类型的时间。</param>
        /// <returns></returns>
        public static DateTime ToDateTime(SYSTEMTIME time)
        {
            return time.ToDateTime();
        }
    }

    public class Win32API
    {
        [DllImport("Kernel32.dll")]
        public static extern bool SetLocalTime(ref SYSTEMTIME Time);

        [DllImport("Kernel32.dll")]
        public static extern void GetLocalTime(ref SYSTEMTIME Time);
    }
}
