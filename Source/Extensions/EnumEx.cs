using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TD_Find_Lib
{
	public static class EnumEx2<T>
	{
		public static readonly T[] Arr = (T[])Enum.GetValues(typeof(T));
	}
	public static class EnumEx
	{ 
		public static T Next<T>(this T src) where T: System.Enum
		{
			if (!typeof(T).IsEnum) throw new ArgumentException(String.Format("Argument {0} is not an Enum", typeof(T).FullName));

			int j = Array.IndexOf<T>(EnumEx2<T>.Arr, src) + 1;
			return (EnumEx2<T>.Arr.Length == j) ? EnumEx2<T>.Arr[0] : EnumEx2<T>.Arr[j];
		}
	}
}
