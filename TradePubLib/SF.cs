using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DevelopLibrary.DevelopAPI;

namespace TradePubLib
{
    public class SF
    {
        /// <summary>
        /// 向前引用第N个数据
        /// </summary>
        /// <param name="f">数组</param>
        /// <param name="N">0：最后一个数据；1：倒数第一个数据，以此类推</param>
        /// <returns>数组中的值</returns>
        public static double Refdata(DataArray f, int N)
        {
            double value = double.NaN;

            if (N < 0) N = 0;
            if (f.IsNaN()) return value;
            if (f.Length < N) return value;

            value = f[f.Length - N - 1];

            return value;
        }

        /// <summary>
        /// 求数组中区间长度的得最大值
        /// </summary>
        /// <param name="f">数组</param>
        /// <param name="hhvLength">区间长度（从Offset偏移量开始往前算起）</param>
        /// <param name="offset">偏移量</param>
        /// <returns></returns>
        public static double HhvData(DataArray f, int hhvLength, int offset)
        {
            double hhvValue = 0;
            int loop = 0;

            if (f.Length >= hhvLength + offset)
            {
                loop = hhvLength;
            }
            else
            {
                loop = f.Length - offset;
            }

            if (loop > 0)
            {
                for (int i = 0; i < loop; i++)
                {
                    double tmpData = Refdata(f, offset + i);
                    if (hhvValue < tmpData) hhvValue = tmpData;
                }
            }

            return hhvValue;
        }

        public static double HhvData(DataArray f, int iStartPos, int iEndPos, bool bHaveStart, bool bHaveEnd)
        {
            double hhvValue = 0;
            
            if (iStartPos < 0) iStartPos = 0;
            if (iEndPos > f.Length - 1) iEndPos = f.Length - 1;

            if (!bHaveStart) iStartPos = iStartPos + 1;
            if (!bHaveEnd) iEndPos = iEndPos - 1;

            if (iStartPos <= iEndPos)
            {
                for (int i = iStartPos; i<= iEndPos; i++)
                {
                    if (hhvValue < f[i]) hhvValue = f[i];
                }
            }

            return hhvValue;
        }

        public static double LlvData(DataArray f, int llvLength, int offset)
        {
            double llvValue = 0;
            int loop = 0;

            if (f.Length >= llvLength + offset)
            {
                loop = llvLength;
            }
            else
            {
                loop = f.Length - offset;
            }

            if (loop > 0)
            {
                for (int i = 0; i < loop; i++)
                {
                    double tmpData = Refdata(f, offset + i);
                    if (llvValue <= 0) llvValue = tmpData;
                    if (llvValue > tmpData) llvValue = tmpData;
                }
            }

            return llvValue;
        }

        public static double LlvData(DataArray f, int iStartPos, int iEndPos, bool bHaveStart, bool bHaveEnd)
        {
            double llvValue = 0;

            if (iStartPos < 0) iStartPos = 0;
            if (iEndPos > f.Length - 1) iEndPos = f.Length - 1;

            if (!bHaveStart) iStartPos = iStartPos + 1;
            if (!bHaveEnd) iEndPos = iEndPos - 1;

            if (iStartPos <= iEndPos)
            {
                for (int i = iStartPos; i <= iEndPos; i++)
                {
                    if (llvValue <= 0) llvValue = f[i];
                    if (llvValue > f[i]) llvValue = f[i];
                }
            }

            return llvValue;
        }

        /// <summary>
        /// 寻找区间最低值的那根K线的位置
        /// </summary>
        /// <param name="f"></param>
        /// <param name="iStartPos"></param>
        /// <param name="iEndPos"></param>
        /// <returns></returns>
        public static int LlvPos(DataArray f, int iStartPos, int iEndPos)
        {
            int pos = -1;
            double dLlvValue = 0;

            if (iStartPos < 0) iStartPos = 0;
            if (iEndPos > f.Length - 1) iEndPos = f.Length - 1;

            if (iStartPos <= iEndPos)
            {
                pos = iStartPos;
                dLlvValue = f[pos];

                for (int i = iStartPos + 1; i <= iEndPos; i++)
                {
                    if (f[i] <= dLlvValue)
                    {
                        dLlvValue = f[i];
                        pos = i;
                    }
                }
            }
            
            return pos;
        }

        /// <summary>
        /// 寻找区间最高值的那根K线的位置
        /// </summary>
        /// <param name="f"></param>
        /// <param name="iStartPos"></param>
        /// <param name="iEndPos"></param>
        /// <returns></returns>
        public static int HhvPos(DataArray f, int iStartPos, int iEndPos)
        {
            int pos = -1;
            double dHhvValue = 0;

            if (iStartPos < 0) iStartPos = 0;
            if (iEndPos > f.Length - 1) iEndPos = f.Length - 1;

            if (iStartPos <= iEndPos)
            {
                for (int i = iStartPos; i <= iEndPos; i++)
                {
                    if (f[i] >= dHhvValue)
                    {
                        dHhvValue = f[i];
                        pos = i;
                    }
                }
            }

            return pos;
        }

        /// <summary>
        /// 在指定位置向左寻找凹区间（左边界点的值 > 起始位置的值)
        /// </summary>
        /// <param name="f"></param>
        /// <param name="iPos"></param>
        /// <returns></returns>
        public static int FindSunkenRegion_Left(DataArray f, int iPos, int iEndPos = 0)
        {
            int pos = -1;
            if (iPos < 0) iPos = 0;
            if (iEndPos < 0) iEndPos = 0;
            if (iPos > f.Length) iPos = f.Length - 1;

            if (f.Length > 0)
            {
                pos = 0;
                for (int i = iPos - 1; i >= iEndPos; i--)
                {
                    if (f[i] > f[iPos])
                    {
                        pos = i;
                        break;
                    }
                }
            }
            return pos;
        }

        /// <summary>
        /// 在指定位置向右寻找凹区间（左边界点的值 > 起始位置的值)
        /// </summary>
        /// <param name="f"></param>
        /// <param name="iPos"></param>
        /// <returns></returns>
        public static int FindSunkenRegion_Right(DataArray f, int iPos)
        {
            int pos = -1;
            if (iPos < 0) iPos = 0;
            if (iPos > f.Length) iPos = f.Length - 1;

            if (f.Length > 0)
            {
                pos = f.Length - 1;
                for (int i = iPos + 1; i < f.Length; i++)
                {
                    if (f[i] > f[iPos]){
                        pos = i;
                        break;
                    }
                }
            }

            return pos;                 
        }

        /// <summary>
        /// 在指定位置向左寻找凸区间（左边界点的值 < 起始位置的值)
        /// </summary>
        /// <param name="f">数据数组</param>
        /// <param name="iPos">起始点</param>
        /// <param name="iEndPos">结束点</param>
        /// <returns></returns>
        public static int FindBulgeRegion_Left(DataArray f, int iPos, int iEndPos = 0)
        {
            int pos = -1;
            if (iPos < 0) iPos = 0;
            if (iEndPos < 0) iEndPos = 0;
            if (iPos > f.Length) iPos = f.Length - 1;

            if (f.Length > 0)
            {
                pos = 0;
                for (int i = iPos - 1; i >= iEndPos; i--)
                {
                    if (f[i] < f[iPos])
                    {
                        pos = i;
                        break;
                    }
                }
            }
            return pos;
        }

        public static int FindBulgeRegion_Right(DataArray f, int iPos)
        {
            int pos = -1;
            if (iPos < 0) iPos = 0;
            if (iPos > f.Length) iPos = f.Length - 1;

            if (f.Length > 0)
            {
                pos = f.Length - 1;
                for (int i = iPos + 1; i < f.Length; i++)
                {
                    if (f[i] < f[iPos])
                    {
                        pos = i;
                        break;
                    }
                }
            }

            return pos;
        }
    }
}
