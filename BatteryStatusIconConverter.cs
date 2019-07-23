using System;
using System.Globalization;
using System.Windows.Data;
using MaterialIcons;

namespace NotifySync {
	public class BatteryStatusIconConverter: IValueConverter {
		public object Convert(object value, Type targetType, object parameter, CultureInfo cultureInfo) {
			if (value == null) return null;
			var status = (BatteryStatus) value;
			if (status.Charging) {
				if (status.CurrentLevel >= 95) {
					return MaterialIconType.ic_battery_charging_full;
				}
				if (status.CurrentLevel >= 90) {
					return MaterialIconType.ic_battery_charging_90;
				}
				if (status.CurrentLevel >= 80) {
					return MaterialIconType.ic_battery_charging_80;
				}
				if (status.CurrentLevel >= 60) {
					return MaterialIconType.ic_battery_charging_60;
				}
				if (status.CurrentLevel >= 50) {
					return MaterialIconType.ic_battery_charging_50;
				}
				if (status.CurrentLevel >= 30) {
					return MaterialIconType.ic_battery_charging_30;
				}
				return MaterialIconType.ic_battery_charging_20;
			}
			if (status.CurrentLevel >= 95) {
				return MaterialIconType.ic_battery_full;
			}
			if (status.CurrentLevel >= 90) {
				return MaterialIconType.ic_battery_90;
			}
			if (status.CurrentLevel >= 80) {
				return MaterialIconType.ic_battery_80;
			}
			if (status.CurrentLevel >= 60) {
				return MaterialIconType.ic_battery_60;
			}
			if (status.CurrentLevel >= 50) {
				return MaterialIconType.ic_battery_50;
			}
			if (status.CurrentLevel >= 30) {
				return MaterialIconType.ic_battery_30;
			}
			if (status.CurrentLevel >= 20) {
				return MaterialIconType.ic_battery_20;
			}
			return status.CurrentLevel >= 0 ? MaterialIconType.ic_battery_alert : MaterialIconType.ic_battery_unknown;
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo cultureInfo) {
			return null;
		}
	}
}
