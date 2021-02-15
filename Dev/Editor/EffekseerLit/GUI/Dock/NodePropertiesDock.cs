using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Effekseer.GUI.Dock
{
	class NodePropertiesDock : DockPanel
	{
		public NodePropertiesDock()
		{
			Label = Icons.PanelOptions + Resources.GetString("Options") + "###NodePropertiesDock";

			Core.OnAfterLoad += OnAfter;
			Core.OnAfterNew += OnAfter;

			TabToolTip = Resources.GetString("Options");

			NoPadding = true;
			NoScrollBar = true;
			NoCloseButton = true;
			AllowsShortTab = false;
		}

		public override void OnDisposed()
		{
			Core.OnAfterLoad -= OnAfter;
			Core.OnAfterNew -= OnAfter;
		}

		protected override void UpdateInternal()
		{
		}

		void OnAfter(object sender, EventArgs e)
		{
		}
	}
}
