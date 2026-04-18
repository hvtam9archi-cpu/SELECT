using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.ApplicationServices;

namespace UnifiedAutoCADTools.Utils
{
	public class SysVarGuard : IDisposable
	{
		private readonly Dictionary<string, object> _originalValues = new Dictionary<string, object>();
		private readonly string[] _varsToMonitor = { "PICKFIRST", "PICKADD" };

		public SysVarGuard()
		{
			foreach (var name in _varsToMonitor)
			{
				try { _originalValues[name] = Application.GetSystemVariable(name); }
				catch { /* Bỏ qua nếu biến không tồn tại */ }
			}
		}

		public void Dispose()
		{
			foreach (var pair in _originalValues)
			{
				try
				{
					object currentVal = Application.GetSystemVariable(pair.Key);
					if (!currentVal.Equals(pair.Value))
					{
						Application.SetSystemVariable(pair.Key, pair.Value);
					}
				}
				catch { /* Bỏ qua lỗi cập nhật biến */ }
			}
		}
	}
}
