using System;
using System.Globalization;
using System.Windows.Data;

namespace NotifySync {
	public class BatteryLevelConverter: IValueConverter {
		public object Convert(object value, Type targetType, object parameter, CultureInfo cultureInfo) {
			if (value == null) return null;
			var level = (int) value;
			return level >= 0 ? level + "%" : Properties.Resources.NA;
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo cultureInfo) {
			return null;
		}
	}
}
