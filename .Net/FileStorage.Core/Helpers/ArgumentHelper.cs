using System;

namespace FileStorage.Core.Helpers
{
	public static class ArgumentHelper
	{
		public static void Check<T>(this T o, Predicate<T> invalid, Exception ex) where T:class
		{
			if (o == null || invalid(o))
				throw ex;
		}

		public static void CheckArgument<T>(this T o, Predicate<T> invalid, string name) where T : class
		{
			if (o == null || invalid(o))
				throw new ArgumentException(name);
		}

		public static void CheckArgument(this bool error, string name)
		{
			if (error)
				throw new ArgumentException(name);
		}

		public static void Check(this bool error, Exception ex)
		{
			if (error)
				throw ex;
		}
	}
}