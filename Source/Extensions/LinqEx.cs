using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TD_Find_Lib
{
	public static class LinqEx
	{
		public static IEnumerable<TSource> MaybeWhere<TSource>(this IEnumerable<TSource> source, Func<TSource, bool> predicate) =>
			predicate == null ? source : source.Where(predicate);

	}
}
