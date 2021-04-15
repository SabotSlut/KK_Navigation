using System;
using System.Collections;

namespace EMK.Cartography
{
	/// <summary>
	/// Graph structure. It is defined with :
	/// It is defined with both a list of nodes and a list of arcs.
	/// </summary>
	[Serializable]
	public class Graph
	{
		ArrayList LN;
		ArrayList LA;

		/// <summary>
		/// Constructor.
		/// </summary>
		public Graph()
		{
			LN = new ArrayList();
			LA = new ArrayList();
		}

		/// <summary>
		/// Gets the List interface of the nodes in the graph.
		/// </summary>
		public IList Nodes => LN;

        /// <summary>
		/// Gets the List interface of the arcs in the graph.
		/// </summary>
		public IList Arcs => LA;

        /// <summary>
		/// Empties the graph.
		/// </summary>
		public void Clear()
		{
			LN.Clear();
			LA.Clear();
		}

		/// <summary>
		/// Directly Adds a node to the graph.
		/// </summary>
		/// <param name="NewNode">The node to add.</param>
		/// <returns>'true' if it has actually been added / 'false' if the node is null or if it is already in the graph.</returns>
		public bool AddNode(Node NewNode)
		{
			if ( NewNode==null || LN.Contains(NewNode) ) return false;
			LN.Add(NewNode);
			return true;
		}

		/// <summary>
		/// Creates a node, adds to the graph and returns its reference.
		/// </summary>
		/// <param name="x">X coordinate.</param>
		/// <param name="y">Y coordinate.</param>
		/// <param name="z">Z coordinate.</param>
		/// <returns>The reference of the new node / null if the node is already in the graph.</returns>
		public Node AddNode(int id, Node.NodeType type)
		{
			Node NewNode = new Node(id, type);
			return AddNode(NewNode) ? NewNode : null;
		}

		/// <summary>
		/// Directly Adds an arc to the graph.
		/// </summary>
		/// <exception cref="ArgumentException">Cannot add an arc if one of its extremity nodes does not belong to the graph.</exception>
		/// <param name="NewArc">The arc to add.</param>
		/// <returns>'true' if it has actually been added / 'false' if the arc is null or if it is already in the graph.</returns>
		public bool AddArc(Arc NewArc)
		{
			if ( NewArc==null || LA.Contains(NewArc) ) return false;
			if ( !LN.Contains(NewArc.StartNode) || !LN.Contains(NewArc.EndNode) )
				throw new ArgumentException("Cannot add an arc if one of its extremity nodes does not belong to the graph.");
			LA.Add(NewArc);
			return true;
		}

		/// <summary>
		/// Creates an arc between two nodes that are already registered in the graph, adds it to the graph and returns its reference.
		/// </summary>
		/// <exception cref="ArgumentException">Cannot add an arc if one of its extremity nodes does not belong to the graph.</exception>
		/// <param name="StartNode">Start node for the arc.</param>
		/// <param name="EndNode">End node for the arc.</param>
		/// <param name="Weight">Weight for the arc.</param>
		/// <returns>The reference of the new arc / null if the arc is already in the graph.</returns>
		public Arc AddArc(Node StartNode, Node EndNode, float distance)
		{
			Arc NewArc = new Arc(StartNode, EndNode, distance);
			return AddArc(NewArc) ? NewArc : null;
		}

		/// <summary>
		/// Removes a node from the graph as well as the linked arcs.
		/// </summary>
		/// <param name="NodeToRemove">The node to remove.</param>
		/// <returns>'true' if succeeded / 'false' otherwise.</returns>
		public bool RemoveNode(Node NodeToRemove)
		{
			if ( NodeToRemove==null ) return false;
			try
			{
				foreach ( Arc A in NodeToRemove.IncomingArcs )
				{
					A.StartNode.OutgoingArcs.Remove(A);
					LA.Remove(A);
				}
				foreach ( Arc A in NodeToRemove.OutgoingArcs )
				{
					A.EndNode.IncomingArcs.Remove(A);
					LA.Remove(A);
				}
				LN.Remove(NodeToRemove);
			}
			catch { return false; }
			return true;
		}

		/// <summary>
		/// Removes a node from the graph as well as the linked arcs.
		/// </summary>
		/// <param name="ArcToRemove">The arc to remove.</param>
		/// <returns>'true' if succeeded / 'false' otherwise.</returns>
		public bool RemoveArc(Arc ArcToRemove)
		{
			if ( ArcToRemove==null ) return false;
			try
			{
				LA.Remove(ArcToRemove);
				ArcToRemove.StartNode.OutgoingArcs.Remove(ArcToRemove);
				ArcToRemove.EndNode.IncomingArcs.Remove(ArcToRemove);
			}
			catch { return false; }
			return true;
		}
	}
}
