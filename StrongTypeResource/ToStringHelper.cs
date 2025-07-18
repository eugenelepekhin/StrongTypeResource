﻿using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Reflection;

namespace StrongTypeResource {
	/// <summary>
	/// Utility class to produce culture-oriented representation of an object as a string.
	/// </summary>
	internal sealed class ToStringHelper {
		private IFormatProvider formatProvider = CultureInfo.InvariantCulture;

		/// <summary>
		/// Gets or sets format provider to be used by ToStringWithCulture method.
		/// </summary>
		public IFormatProvider FormatProvider {
			get { return this.formatProvider; }
			set { this.formatProvider = value ?? CultureInfo.InvariantCulture; }
		}

		/// <summary>
		/// This is called from the compile/run appdomain to convert objects within an expression block to a string
		/// </summary>
		/// <param name="objectToConvert"></param>
		/// <returns></returns>
		public string? ToStringWithCulture(object objectToConvert) {
			if(objectToConvert is null) {
				throw new ArgumentNullException(nameof(objectToConvert));
			}
			Type type = objectToConvert.GetType();
			MethodInfo? method = type.GetMethod("ToString", new Type[] { typeof(IFormatProvider) });
			if(method != null) {
				return (string?)method.Invoke(objectToConvert, new object[] { this.FormatProvider });
			} else {
				return objectToConvert.ToString();
			}
		}

		[SuppressMessage("Performance", "CA1822:Mark members as static")]
		public string? ToStringWithCulture(string text) {
			return text;
		}
	}
}
