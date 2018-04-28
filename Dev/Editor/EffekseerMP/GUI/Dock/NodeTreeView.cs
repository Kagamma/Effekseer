﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Effekseer.GUI.Dock
{
    class NodeTreeView : DockPanel
    {
        internal Utils.DelayedList<NodeTreeViewNode> Children = new Utils.DelayedList<NodeTreeViewNode>();

		internal List<IControl> menuItems = new List<IControl>();

		NodeTreeViewNode SelectedNode = null;

		bool isPopupShown = false;

		internal string treePyloadName = "NodeTreeNode";
		internal byte[] treePyload = new byte[4];

		internal List<Tuple<int, int>> exchangeEvents = new List<Tuple<int, int>>();

		internal int exchangeTargetID_Offset = -0x10fff;

		public NodeTreeView()
        {
			Label = Resources.GetString("NodeTree") + "###NodeTree";

			Core.OnAfterNew += OnRenew;
			Core.OnAfterLoad += OnRenew;
			Core.OnAfterSelectNode += OnAfterSelect;

			Func<Func<bool>, Menu.MenuItem> create_menu_item_from_commands = (a) =>
			{
				var item = new Menu.MenuItem();
				var attributes = a.Method.GetCustomAttributes(false);
				var uniquename = UniqueNameAttribute.GetUniqueName(attributes);
				item.Label = NameAttribute.GetName(attributes);
				item.Shortcut = Shortcuts.GetShortcutText(uniquename);
				item.Clicked += () =>
				{
					a();
				};

				return item;
			};

			menuItems.Add(create_menu_item_from_commands(Commands.AddNode));
			menuItems.Add(create_menu_item_from_commands(Commands.InsertNode));
			menuItems.Add(create_menu_item_from_commands(Commands.RemoveNode));

			menuItems.Add(new Menu.MenuSeparator());

			menuItems.Add(create_menu_item_from_commands(Commands.Copy));
			menuItems.Add(create_menu_item_from_commands(Commands.Paste));
			menuItems.Add(create_menu_item_from_commands(Commands.PasteInfo));

			menuItems.Add(new Menu.MenuSeparator());

			menuItems.Add(create_menu_item_from_commands(Commands.Undo));
			menuItems.Add(create_menu_item_from_commands(Commands.Redo));

			Renew();
		}

		override protected void UpdateInternal()
		{
			isPopupShown = false;

			Manager.NativeManager.Columns(2);
			//Manager.NativeManager.SetColumnOffset(1, 300);

			Children.Lock();
			foreach (var child in Children.Internal)
			{
				child.Update();
			}
			Children.Unlock();

			Manager.NativeManager.Columns(1);

			// Run exchange events
			foreach (var pair in exchangeEvents)
			{
				Func<int, List<NodeTreeViewNode>, NodeTreeViewNode> findNode = null;

				findNode = (int id, List<NodeTreeViewNode> ns) => 
				{
					foreach(var n in ns)
					{
						if (n.UniqueID == id) return n;
						var ret = findNode(id, n.Children.Internal);
						if(ret != null)
						{
							return ret;
						}
					}

					return null;
				};

				var n1 = findNode(pair.Item1, Children.Internal);
				var n2 = findNode(pair.Item2, Children.Internal);
				var n2_end = findNode(pair.Item2 - exchangeTargetID_Offset, Children.Internal);

				if(n2_end != null)
				{
					Core.MoveNode(n1.Node as Data.Node, n2_end.Node.Parent, int.MaxValue);
				}
				else if(n2 != null)
				{
					Core.MoveNode(n1.Node as Data.Node, n2.Node.Parent, n2.Node.Parent.Children.Internal.IndexOf(n2.Node as Data.Node));
				}
			}
			exchangeEvents.Clear();
		}

        /// <summary>
        /// Renew all view
        /// </summary>
        public void Renew()
        {
			// Reset all
			if (Children.Count != 1 || Children[0].Node != Core.Root)
			{
				Children.Lock();
				foreach (var child in Children.Internal)
				{
					child.RemoveEvent(true);
				}
				Children.Unlock();
				Children.Clear();

				Children.Add(new NodeTreeViewNode(this, Core.Root));
			}

            Action<NodeTreeViewNode, Data.NodeBase> set_nodes = null;
            set_nodes = (treenode, node) =>
            {
                var tns = treenode.Children;
                var ns = node.Children;

                for (int ind = 0; ind < ns.Count;)
                {
                    // not need to change
                    if (ind < tns.Count && ((NodeTreeViewNode)tns[ind]).Node == ns[ind])
                    {
                        ind++;
                        continue;
                    }

                    // need to change
                    while (tns.Count > ind)
                    {
                        ((NodeTreeViewNode)tns[ind]).RemoveEvent(true);
                        tns.RemoveAt(ind);
                    }

                    while (ns.Count > ind)
                    {
                        tns.Add(new NodeTreeViewNode(this, ns[ind]));
                        ind++;
                    }
                }

                for (int i = 0; i < ns.Count; i++)
                {
                    set_nodes(tns[i], ns[i]);
                }
            };

            set_nodes(Children[0], Core.Root);

            ExpandAll();
        }

        public void ExpandAll()
        {
			foreach(var child in Children.Internal)
			{
				child.ExpandAll();
			}
        }

		internal void Popup()
		{
			if (isPopupShown) return;

			if (Manager.NativeManager.BeginPopupContextItem("##Popup"))
			{
				foreach(var item in menuItems)
				{
					item.Update();
				}

				Manager.NativeManager.EndPopup();
				isPopupShown = true;
			}
		}

		/// <summary>
		/// When renew, it is called.
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		void OnRenew(object sender, EventArgs e)
        {
            Renew();
        }

		void ReadSelect()
		{
			Func<Data.NodeBase, Utils.DelayedList<NodeTreeViewNode>, NodeTreeViewNode> search_node = null;
			search_node = (searched_node, treenodes) =>
			{
				if (search_node == null) return null;

				for (int i = 0; i < treenodes.Count; i++)
				{
					if (treenodes[i].Node == searched_node) return treenodes[i];
					var ret = search_node(searched_node, treenodes[i].Children);
					if (ret != null) return ret;
				}
				return null;
			};

			var node = search_node(Core.SelectedNode, Children);

			SelectedNode = node;
		}

		void OnAfterSelect(object sender, EventArgs e)
		{
			ReadSelect();
		}
	}

    class NodeTreeViewNode : IControl
    {
        string id = "";
		public int UniqueID { get; private set; }

        public Data.NodeBase Node { get; private set; } = null;

        internal Utils.DelayedList<NodeTreeViewNode> Children = new Utils.DelayedList<NodeTreeViewNode>();

		NodeTreeView treeView = null;

		bool isExpanding = false;

		public NodeTreeViewNode(NodeTreeView treeView, Data.NodeBase node, bool createChildren = false)
        {
			UniqueID = Manager.GetUniqueID();
			id = "###" + UniqueID.ToString();

			this.treeView = treeView;
            this.Node = node;

            node.OnAfterAddNode += OnAfterAddNode;
            node.OnAfterRemoveNode += OnAfterRemoveNode;
            node.OnAfterExchangeNodes += OnAfterExchangeNodes;

            if (createChildren)
            {
                for(int i = 0; i < node.Children.Count; i++)
                {
                    var newNode = new NodeTreeViewNode(treeView, node.Children[i], true);
                    Children.Add(newNode);
                }
            }
        }

		public void Expand()
		{
			isExpanding = true;
		}

		public void ChangeVisible(bool recursion, bool value)
		{
			Command.CommandManager.StartCollection();

			Action<Data.NodeBase.ChildrenCollection> recurse = null;

			Node.IsRendered.SetValue(value);

			if(recursion)
			{
				recurse = (t) =>
				{
					for (int i = 0; i < t.Count; i++)
					{
						t[i].IsRendered.SetValue(value);
					}

					for (int i = 0; i < t.Count; i++)
					{
						recurse(t[i].Children);
					}
				};

			}

			recurse(Node.Children);

			Command.CommandManager.EndCollection();

		}

		public void RemoveEvent(bool recursion)
        {
            if (Node is Data.Node)
            {
                var realNode = (Data.Node)Node;
            }

            Node.OnAfterAddNode -= OnAfterAddNode;
            Node.OnAfterRemoveNode -= OnAfterRemoveNode;
            Node.OnAfterExchangeNodes -= OnAfterExchangeNodes;
            if (recursion)
            {
                for (int i = 0; i < Children.Count; i++)
                {
                    (Children[i]).RemoveEvent(true);
                }
            }
        }


        public void Update()
        {
			var flag = swig.TreeNodeFlags.OpenOnArrow | swig.TreeNodeFlags.OpenOnDoubleClick | swig.TreeNodeFlags.DefaultOpen;

			if(Core.SelectedNode == this.Node)
			{
				flag = flag | swig.TreeNodeFlags.Selected;
			}

			if(this.Node.Children.Count == 0)
			{
				flag = flag | swig.TreeNodeFlags.Leaf;
			}

			UpdateDDTarget(false);

			if(isExpanding)
			{
				Manager.NativeManager.SetNextTreeNodeOpen(true);
				isExpanding = false;
			}

			// Tree
			var temp = new[] { false };

			var iconString = "NodeEmpty";
			var node = Node as Data.Node;
			if(node != null)
			{
				if(node.DrawingValues.Type.Value == Data.RendererValues.ParamaterType.Sprite) iconString = "NodeSprite";
				if (node.DrawingValues.Type.Value == Data.RendererValues.ParamaterType.Ring) iconString = "NodeRing";
				if (node.DrawingValues.Type.Value == Data.RendererValues.ParamaterType.Ribbon) iconString = "NodeRibbon";
				if (node.DrawingValues.Type.Value == Data.RendererValues.ParamaterType.Model) iconString = "NodeModel";
				if (node.DrawingValues.Type.Value == Data.RendererValues.ParamaterType.Track) iconString = "NodeTrack";
			}

			if (Manager.NativeManager.TreeNodeEx(Node.Name + id, temp, Images.GetIcon(iconString), flag))
            {
				if(Manager.NativeManager.IsItemClicked(0) ||
					Manager.NativeManager.IsItemClicked(1))
				{
					Core.SelectedNode = this.Node;
				}

				treeView.Popup();
				
				// D&D Source
				if(Manager.NativeManager.BeginDragDropSource())
				{
					byte[] idBuf = BitConverter.GetBytes(UniqueID);
					if(Manager.NativeManager.SetDragDropPayload(treeView.treePyloadName, idBuf, idBuf.Length))
					{
					}
					Manager.NativeManager.Text(this.Node.Name);

					Manager.NativeManager.EndDragDropSource();
				}

				Manager.NativeManager.NextColumn();

				var visible = Node.IsRendered;

				if(Manager.NativeManager.ImageButton(Images.GetIcon(visible ? "VisibleShow" : "VisibleHide"), 18, 18))
				{
					int LEFT_SHIFT = 340;
					int RIGHT_SHIFT = 344;

					if (Manager.NativeManager.IsKeyDown(LEFT_SHIFT) || Manager.NativeManager.IsKeyDown(RIGHT_SHIFT))
					{
						ChangeVisible(true, !visible);
					}
					else
					{
						ChangeVisible(false, !visible);
					}
				}

				Manager.NativeManager.NextColumn();

				Children.Lock();

                foreach (var child in Children.Internal)
                {
                    child.Update();
                }

				Children.Unlock();

				if(Children.Count != 0)
				{
					Children.Internal.Last().UpdateDDTarget(true);
				}

				Manager.NativeManager.TreePop();
            }
			else
			{
				//UpdateDDTarget(true);

				treeView.Popup();

				Manager.NativeManager.NextColumn();

				Manager.NativeManager.Checkbox("##Check", temp);

				Manager.NativeManager.NextColumn();
			}
		}

		/// <summary>
		/// Update D&D Target
		/// </summary>
		/// <param name="isEnd"></param>
		void UpdateDDTarget(bool isEnd)
		{
			Manager.NativeManager.HiddenSeparator();

			if (Manager.NativeManager.BeginDragDropTarget())
			{
				int size = 0;
				if (Manager.NativeManager.AcceptDragDropPayload(treeView.treePyloadName, treeView.treePyload, treeView.treePyload.Length, ref size))
				{
					var sourceID = BitConverter.ToInt32(treeView.treePyload, 0);
					treeView.exchangeEvents.Add(
						new Tuple<int, int>(sourceID, UniqueID + (isEnd ? treeView.exchangeTargetID_Offset : 0)));
				}

				Manager.NativeManager.EndDragDropTarget();
			}
		}

        public void ExpandAll()
        {
			Expand();
			foreach(var child in Children.Internal)
			{
				child.ExpandAll();
			}
        }

        void OnAfterAddNode(object sender, ChangedValueEventArgs e)
        {
            var node = e.Value as Data.NodeBase;

			Console.WriteLine(string.Format("OnAfterAddNode({0})", node.Name.Value));

			int ind = 0;
            for (; ind < Node.Children.Count; ind++)
            {
                if (node == Node.Children[ind]) break;
            }

            if (ind == Children.Count)
            {
                Children.Add(new NodeTreeViewNode(treeView ,node, true));
            }
            else
            {
                Children.Insert(ind, new NodeTreeViewNode(treeView, node, true));
            }

            ExpandAll();
        }

        void OnAfterRemoveNode(object sender, ChangedValueEventArgs e)
        {
            var node = e.Value as Data.NodeBase;

			Console.WriteLine(string.Format("OnAfterRemoveNode({0})", node.Name.Value));

			for (int i = 0; i < Children.Count; i++)
            {
                var treenode = Children[i];
                if (treenode.Node == node)
                {
                    treenode.RemoveEvent(true);
                    Children.Remove(treenode);
                    return;
                }
            }

            throw new Exception();
        }

        void OnAfterExchangeNodes(object sender, ChangedValueEventArgs e)
        {
            var node1 = (e.Value as Tuple<Data.Node, Data.Node>).Item1;
            var node2 = (e.Value as Tuple<Data.Node, Data.Node>).Item2;

            int ind1 = 0;
            int ind2 = 0;

            for (int i = 0; i < Children.Count; i++)
            {
                var treenode = Children[i];
                if (treenode.Node == node1)
                {
                    ind1 = i;
                }
                if (treenode.Node == node2)
                {
                    ind2 = i;
                }
            }

            if (ind1 > ind2)
            {
                var ind_temp = ind1;
                var node_temp = node1;
                ind1 = ind2;
                node1 = node2;
                ind2 = ind_temp;
                node2 = node_temp;
            }

            Children.Insert(ind2, new NodeTreeViewNode(treeView, node1, true));
            Children.RemoveAt(ind2 + 1);

            Children.Insert(ind1, new NodeTreeViewNode(treeView, node2, true));
            Children.RemoveAt(ind1 + 1);

            ExpandAll();
        }
    }
}
